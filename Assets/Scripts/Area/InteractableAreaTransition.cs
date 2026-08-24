using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// プレイヤーの操作によって、同一シーン内の指定座標へ移動させます。
/// 必要に応じて、移動先にある共通帰還口へ今回の戻り先を登録できます。
/// </summary>
public class InteractableAreaTransition : MonoBehaviour
{
    #region インスペクター設定

    [Header("基本設定")]
    [InspectorName("移動先座標")]
    [Tooltip("このオブジェクトを使用したときの移動先座標")]
    [SerializeField]
    private Vector2 movepos = Vector2.zero;

    [InspectorName("ドアの種類")]
    [Tooltip("ドアの種類。種類に応じて効果音などが変わります")]
    [SerializeField]
    private DoorOpener.DoorType doorType = DoorOpener.DoorType.None;

    [Header("帰還先の登録（任意）")]
    [InspectorName("共通帰還口")]
    [Tooltip("移動時に戻り先を登録する、行き先側の共通帰還口")]
    [SerializeField]
    private InteractableAreaTransition returnTransition;

    [InspectorName("戻り先座標")]
    [Tooltip("共通帰還口から戻る際の移動先座標。初期値はこのオブジェクトの座標です")]
    [SerializeField, ShowIf(nameof(HasReturnTransition))]
    private Vector2 returnPoint;

    #endregion

    #region 実行時データ

    // 入口から登録された帰還先です。使用後に破棄し、古い入口へ戻ることを防ぎます。
    private Vector2? oneTimeMovePos;

    #endregion

    #region Unityイベント

    private void Reset()
    {
        SetReturnPointToCurrentPosition();
    }

    private void OnValidate()
    {
        // 帰還口を設定するまでは配置位置へ追従させ、各配置個体に適切な初期値を入れます。
        if (returnTransition == null)
        {
            SetReturnPointToCurrentPosition();
        }
    }

    private void Awake()
    {
        ValidateSettings();
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!enabled || Time.timeScale <= 0f)
        {
            return;
        }

        if (
            PlayerManager.instance.isControlLocked
            || !InputManager.instance.GetInteract()
            || !collision.CompareTag(GameConstants.PLAYER_TAG_NAME)
        )
        {
            return;
        }

        Vector2 destination = GetAndClearMovePos();
        RegisterReturnPoint();
        DoorOpener.OpenDoor(destination, this, doorType);
    }

    private void OnDrawGizmos()
    {
        DrawTransitionGizmos();

        if (returnTransition != null)
        {
            DrawReturnGizmos();
        }
    }

    #endregion

    #region 公開メソッド

    /// <summary>
    /// 次にこのドアを使用したときだけ有効な移動先を登録します。
    /// 一時移動先の使用後は、Inspectorで設定された通常の移動先へ戻ります。
    /// </summary>
    /// <param name="destination">次回の使用時にプレイヤーを移動させる座標</param>
    public void SetOneTimeMovePos(Vector2 destination)
    {
        oneTimeMovePos = destination;
    }

    /// <summary>
    /// 外部からドア機能の有効・無効を切り替えます。
    /// 無効時はタグもUntaggedへ変更し、プレイヤー側の検知対象から外します。
    /// </summary>
    /// <param name="isActive">ドアを有効にする場合はtrue</param>
    public void SetDoorActive(bool isActive)
    {
        enabled = isActive;
        gameObject.tag = isActive
            ? GameConstants.AREA_TRANSITION_TAG_NAME
            : GameConstants.UNTAGGED_TAG_NAME;
    }

    #endregion

    #region 初期化・検証

    /// <summary>
    /// 共通帰還口が設定されているかを返します。
    /// Inspectorで戻り先座標を表示する条件にも使用します。
    /// </summary>
    private bool HasReturnTransition()
    {
        return returnTransition != null;
    }

    /// <summary>
    /// returnPointへ自身の現在のワールド座標を設定します。
    /// </summary>
    private void SetReturnPointToCurrentPosition()
    {
        returnPoint = transform.position;
    }

    /// <summary>
    /// Inspector設定の不足を通知します。
    /// </summary>
    private void ValidateSettings()
    {
        if (movepos == Vector2.zero)
        {
            Debug.LogError($"{name}のmoveposが設定されていません", this);
        }

        if (doorType == DoorOpener.DoorType.None)
        {
            Debug.LogWarning($"{name}のdoorTypeが設定されていません", this);
        }
    }

    #endregion

    #region 移動先の管理

    /// <summary>
    /// 帰還先の登録対象が設定されている場合、今回の戻り先を登録します。
    /// </summary>
    private void RegisterReturnPoint()
    {
        if (returnTransition == null)
        {
            return;
        }

        returnTransition.SetOneTimeMovePos(returnPoint);
    }

    /// <summary>
    /// 一時移動先があればそれを取得して破棄し、なければ通常の移動先を返します。
    /// </summary>
    private Vector2 GetAndClearMovePos()
    {
        if (!oneTimeMovePos.HasValue)
        {
            return movepos;
        }

        Vector2 destination = oneTimeMovePos.Value;
        oneTimeMovePos = null;
        return destination;
    }

    #endregion

    #region Gizmo表示

    /// <summary>
    /// 通常の移動先を緑色で表示します。
    /// </summary>
    private void DrawTransitionGizmos()
    {
        Collider2D col = GetComponent<Collider2D>();
        float yOffset = col != null ? col.bounds.extents.y : 0f;

        Vector3 startPosition = transform.position + Vector3.up * yOffset;
        Vector3 destination = new Vector3(movepos.x, movepos.y, startPosition.z);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(startPosition, destination);
        Gizmos.DrawWireSphere(destination, 0.5f);

        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
        Gizmos.DrawSphere(destination, 0.5f);
    }

    /// <summary>
    /// 共通帰還口から今回登録する戻り先までをオレンジ色で表示します。
    /// </summary>
    private void DrawReturnGizmos()
    {
        Vector3 startPosition = returnTransition.transform.position;
        Vector3 destination = new Vector3(returnPoint.x, returnPoint.y, startPosition.z);
        Color returnColor = new Color(1f, 0.5f, 0f, 1f);

        Gizmos.color = returnColor;
        Gizmos.DrawLine(startPosition, destination);
        Gizmos.DrawWireSphere(destination, 0.4f);

        Gizmos.color = new Color(returnColor.r, returnColor.g, returnColor.b, 0.4f);
        Gizmos.DrawSphere(destination, 0.4f);
    }

    #endregion
}
