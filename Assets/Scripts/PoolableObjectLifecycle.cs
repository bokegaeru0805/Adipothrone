using System;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 時間経過または特定の接触条件によって、オブジェクトをプールに返却（消滅）させるライフサイクル管理クラス
/// </summary>
public class PoolableObjectLifecycle : PoolableObject
{
    /// <summary>
    /// 接触回数制限に達して消滅する直前に呼ばれるイベント
    /// </summary>
    public event Action OnContactLimitReached;

    [Header("時間制限設定")]
    [SerializeField]
    [Tooltip("出現からの寿命（秒）。0以下の場合は時間で消滅しない。")]
    private float lifetime = 3.0f;

    [Header("接触制限設定")]
    [Tooltip("接触したら消滅させるかどうか")]
    [SerializeField]
    private bool enableContactLimit = false;

    [Tooltip("このオブジェクトがGroundLayerに接触可能な最大回数")]
    [SerializeField, ShowIf(nameof(enableContactLimit))]
    private int maxContactCount = 1;

    [Header("SE設定")]
    [Tooltip("最大接触回数を越えて接触した場合、SEを再生するかどうか")]
    [SerializeField]
    private bool playSeOnMaxContact = false;

    [Tooltip("最大接触回数を越えたときに再生するSE")]
    [SerializeField, ShowIf(nameof(playSeOnMaxContact))]
    private SeSelector maxContactSe;
    private int currentContactCount = 0;

    public int MaxContactCount
    {
        get => maxContactCount;
        set => maxContactCount = Mathf.Max(0, value);
    }
    private LayerMask groundLayer; //接触をカウントする対象のレイヤー
    private Coroutine currentDespawnCoroutine; // 現在実行中の自動消滅コルーチンを保持する変数
    CriWare.Assets.CriAtomSePlayer sePlayer;

    private void Awake()
    {
        groundLayer = LayerMask.GetMask(GameConstants.PHYSICS_LAYER_NAME_GROUND); // Groundレイヤーを取得

        if (playSeOnMaxContact)
        {
            sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
            if (sePlayer == null)
            {
                Debug.LogWarning(
                    "PoolableObjectLifecycle: SEを再生する設定ですが、CriAtomSePlayerコンポーネントがアタッチされていません。"
                );
                playSeOnMaxContact = false; // SE再生を無効化
            }
        }
    }

    private void OnEnable()
    {
        currentContactCount = 0; // 接触カウントをリセット
        // イベントの購読者が前回のまま残らないように、OnEnableなどではリセットしませんが、
        // プールシステムの設計によっては、Spawn時にActionを登録し直す運用が一般的です。
        // ※このクラス自体はイベントのリセットを行わないため、購読側で適切に解除するか、
        //   使い捨てのインスタンスでない場合は注意が必要です。
        if (lifetime > 0f)
        {
            // lifetime秒後に自動で返却
            StartCoroutine(ReturnToPoolDelayCoroutine(lifetime));
        }
    }

    private void OnDisable()
    {
        // オブジェクトが無効化されたらコルーチンの参照を切る
        currentDespawnCoroutine = null;

        // イベント購読者をクリア
        OnContactLimitReached = null;
    }

    /// <summary>
    /// 現在の寿命タイマーをキャンセルし、指定した時間後に消えるように再設定します。
    /// </summary>
    /// <param name="duration">新しく設定する消滅までの時間（秒）</param>
    public void OverrideDespawnTimer(float duration)
    {
        // 既に動いている寿命コルーチンがあれば停止する
        if (currentDespawnCoroutine != null)
        {
            StopCoroutine(currentDespawnCoroutine);
        }

        // 新しい時間でコルーチンを開始し直す
        currentDespawnCoroutine = StartCoroutine(ReturnToPoolDelayCoroutine(duration));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 相手がTriggerの場合は無視（物理的な接触ではないため）
        // また、接触制限が無効な場合も無視
        if (other.isTrigger || !enableContactLimit)
            return;

        // 相手のレイヤーがgroundLayerに含まれているか確認
        if ((groundLayer.value & (1 << other.gameObject.layer)) == 0)
            return;

        currentContactCount++;

        if (currentContactCount >= maxContactCount)
        {
            // イベント発火
            OnContactLimitReached?.Invoke();

            // 接触回数が上限に達したらプールに返却
            ReturnToPool();

            // 必要ならSEを再生
            if (playSeOnMaxContact)
            {
                sePlayer.Play(maxContactSe.GetSelectedEnum());
            }
        }
    }
}
