using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 足場上のプレイヤーと物理オブジェクトを足場の移動に追従させます。
/// プレイヤーには足場の移動速度を渡し、物理オブジェクトは足場の子として運搬します。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PassengerCarrier : MonoBehaviour
{
    private const float DetectionHeight = 0.2f;
    private const float DetectionWidthShrink = 0.1f;
    private const float DetectionOverlap = 0.05f;
    private const float MinimumDetectionWidth = 0.1f;
    private const float PlayerPositionTolerance = 0.3f;

    #region Inspector設定

    [SerializeField]
    [Tooltip(
        "Trueの場合、SpriteRendererのサイズに合わせてコライダーを自動調整します。Falseの場合は手動設定を使用します。"
    )]
    private bool autoAdjustCollider = true;

    #endregion

    #region フィールド

    private SpriteRenderer _spriteRenderer;
    private Vector3 _lastPosition;
    private Vector2 _currentVelocity;
    private float _lastRotationAngle;
    private float _angularVelocityRadians;

    private readonly HashSet<Heroin_move> _playerPassengers = new HashSet<Heroin_move>();
    private readonly Dictionary<Heroin_move, Coroutine> _disconnectCoroutines =
        new Dictionary<Heroin_move, Coroutine>();

    // 着地時にTriggerから一瞬外れた場合を考慮し、実際の離脱まで待機する時間。
    private float _disconnectDelay = 0f;
    private WaitForSeconds _disconnectWait;

    #endregion

    #region 公開API

    /// <summary>
    /// 現在乗っているプレイヤーを取得します。乗っていない場合はnullを返します。
    /// </summary>
    public Heroin_move CurrentPlayerPassenger
    {
        get
        {
            foreach (Heroin_move player in _playerPassengers)
            {
                if (player != null)
                    return player;
            }

            return null;
        }
    }

    /// <summary>
    /// 登録中のプレイヤーと物理オブジェクトをすべて足場から降ろします。
    /// プールへの返却など、足場を停止・破棄する前に使用します。
    /// </summary>
    public void EjectAllPassengers()
    {
        foreach (Coroutine disconnectCoroutine in _disconnectCoroutines.Values)
        {
            if (disconnectCoroutine != null)
                StopCoroutine(disconnectCoroutine);
        }
        _disconnectCoroutines.Clear();

        foreach (Heroin_move player in _playerPassengers)
        {
            if (player != null)
                player.ExitCarrier();
        }
        _playerPassengers.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.CompareTag(GameConstants.PHYSICS_OBJECT_TAG_NAME))
                child.SetParent(null);
        }
    }

    #endregion

    #region Unityイベント

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateColliderSize();

        _lastPosition = transform.position;
        _lastRotationAngle = transform.eulerAngles.z;
        _disconnectWait = new WaitForSeconds(_disconnectDelay);
    }

    private void FixedUpdate()
    {
        UpdateCarrierVelocity();
        ApplyVelocityToPlayers();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(GameConstants.PLAYER_TAG_NAME))
        {
            RegisterPlayer(other);
            return;
        }

        if (other.CompareTag(GameConstants.PHYSICS_OBJECT_TAG_NAME))
            other.transform.SetParent(transform);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(GameConstants.PLAYER_TAG_NAME))
        {
            BeginPlayerDisconnect(other);
            return;
        }

        if (!other.CompareTag(GameConstants.PHYSICS_OBJECT_TAG_NAME))
            return;

        // 非アクティブ化中の親子関係変更はUnityのエラーになるため行わない。
        if (!gameObject.activeInHierarchy)
            return;

        if (other.transform.parent == transform)
            other.transform.SetParent(null);
    }

    private void OnValidate()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateColliderSize();

        if (Application.isPlaying)
            _disconnectWait = new WaitForSeconds(_disconnectDelay);
    }

    #endregion

    #region プレイヤーの運搬・乗降管理

    private void UpdateCarrierVelocity()
    {
        Vector3 currentPosition = transform.position;
        float currentRotationAngle = transform.eulerAngles.z;

        if (Time.fixedDeltaTime > 0f)
        {
            _currentVelocity = (currentPosition - _lastPosition) / Time.fixedDeltaTime;
            float rotationDelta = Mathf.DeltaAngle(_lastRotationAngle, currentRotationAngle);
            _angularVelocityRadians = rotationDelta * Mathf.Deg2Rad / Time.fixedDeltaTime;
        }

        _lastPosition = currentPosition;
        _lastRotationAngle = currentRotationAngle;
    }

    private void ApplyVelocityToPlayers()
    {
        foreach (Heroin_move player in _playerPassengers)
        {
            if (player != null)
                player.SetCarrierVelocity(GetVelocityAtPoint(player.transform.position));
        }
    }

    /// <summary>
    /// 足場の並進速度と、回転によって指定地点に生じる接線速度を合成します。
    /// </summary>
    private Vector2 GetVelocityAtPoint(Vector3 worldPosition)
    {
        Vector2 offsetFromPivot = worldPosition - transform.position;
        Vector2 tangentialVelocity = new Vector2(
            -_angularVelocityRadians * offsetFromPivot.y,
            _angularVelocityRadians * offsetFromPivot.x
        );

        return _currentVelocity + tangentialVelocity;
    }

    private void RegisterPlayer(Collider2D playerCollider)
    {
        // Triggerは足場上面に置くが、横や下からの接触も座標で除外する。
        // ローカル座標で比較することで、SpriteのPivot位置や足場の傾きにも対応する。
        Vector3 localPlayerPosition = transform.InverseTransformPoint(
            playerCollider.transform.position
        );
        float platformTopPositionY = _spriteRenderer.localBounds.max.y;
        if (localPlayerPosition.y < platformTopPositionY - PlayerPositionTolerance)
            return;

        Heroin_move player = playerCollider.GetComponent<Heroin_move>();
        if (player == null)
            return;

        CancelPlayerDisconnect(player);
        _playerPassengers.Add(player);
    }

    private void BeginPlayerDisconnect(Collider2D playerCollider)
    {
        Heroin_move player = playerCollider.GetComponent<Heroin_move>();
        if (
            player == null
            || !_playerPassengers.Contains(player)
            || _disconnectCoroutines.ContainsKey(player)
        )
        {
            return;
        }

        Coroutine disconnectCoroutine = StartCoroutine(DisconnectAfterDelay(player));
        _disconnectCoroutines.Add(player, disconnectCoroutine);
    }

    private void CancelPlayerDisconnect(Heroin_move player)
    {
        if (!_disconnectCoroutines.TryGetValue(player, out Coroutine disconnectCoroutine))
            return;

        if (disconnectCoroutine != null)
            StopCoroutine(disconnectCoroutine);

        _disconnectCoroutines.Remove(player);
    }

    /// <summary>
    /// Triggerから一時的に外れただけなら再進入時にキャンセルできるよう、離脱を遅延させます。
    /// </summary>
    private IEnumerator DisconnectAfterDelay(Heroin_move player)
    {
        yield return _disconnectWait;

        // 再進入時に辞書から削除されていれば、離脱処理は不要。
        if (!_disconnectCoroutines.ContainsKey(player))
            yield break;

        if (_playerPassengers.Remove(player) && player != null)
            player.ExitCarrier();

        _disconnectCoroutines.Remove(player);
    }

    #endregion

    #region Collider自動調整

    /// <summary>
    /// SpriteRendererに合わせて物理Colliderと上面の乗車検知Triggerを調整します。
    /// </summary>
    private void UpdateColliderSize()
    {
        if (!autoAdjustCollider || _spriteRenderer == null)
            return;

        foreach (BoxCollider2D targetCollider in GetComponents<BoxCollider2D>())
        {
            if (targetCollider.isTrigger)
                UpdateDetectionCollider(targetCollider);
            else
                UpdatePlatformCollider(targetCollider);
        }
    }

    private void UpdatePlatformCollider(BoxCollider2D platformCollider)
    {
        Bounds spriteBounds = _spriteRenderer.localBounds;
        platformCollider.size = new Vector2(spriteBounds.size.x, spriteBounds.size.y);
        platformCollider.offset = new Vector2(spriteBounds.center.x, spriteBounds.center.y);
    }

    private void UpdateDetectionCollider(BoxCollider2D detectionCollider)
    {
        Bounds spriteBounds = _spriteRenderer.localBounds;
        detectionCollider.size = new Vector2(
            Mathf.Max(MinimumDetectionWidth, spriteBounds.size.x - DetectionWidthShrink),
            DetectionHeight
        );

        // Boundsの上辺を使い、中央以外のSprite Pivotでも正しい位置へ配置する。
        // Triggerを足場へ少し重ね、接地中に検知が途切れにくくする。
        float detectionOffsetY =
            spriteBounds.max.y + (DetectionHeight * 0.5f) - DetectionOverlap;
        detectionCollider.offset = new Vector2(spriteBounds.center.x, detectionOffsetY);
    }

    #endregion
}
