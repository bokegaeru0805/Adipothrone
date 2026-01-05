using Fungus;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 全てのフィールドイベントの基底クラス。
/// 共通のトリガー判定、初期化、Gizmo描画を担当します。
/// </summary>
public abstract class BaseFieldEvent : MonoBehaviour
{
    [Header("基本設定")]
    [Tooltip("このイベントが実行するFlowchart")]
    public Flowchart targetFlowchart = null;

    protected FlagManager flagManager;
    protected bool isEventTriggered = false;

    protected virtual void Awake()
    {
        if (targetFlowchart == null)
        {
            Debug.LogWarning($"{this.gameObject.name} に Flowchart が設定されていません", this);
        }
    }

    protected virtual void Start()
    {
        flagManager = FlagManager.instance;
        if (flagManager == null)
        {
            Debug.LogError("FlagManager が見つかりません。", this);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // 共通の実行条件チェック
        bool canTrigger =
            collision.CompareTag(GameConstants.PLAYER_TAG_NAME)
            && Time.timeScale > 0
            && !isEventTriggered
            && (PlayerManager.instance?.isControlLocked ?? false) == false;

        if (canTrigger && flagManager != null)
        {
            // 条件を満たしたら、子クラスで実装された具体的な処理を呼ぶ
            HandleEvent();
        }
    }

    /// <summary>
    /// 各章ごとの具体的なイベント分岐処理を記述する抽象メソッド
    /// </summary>
    protected abstract void HandleEvent();

    /// <summary>
    /// イベント実行時の定型処理（ブロック実行、フラグ立て、ログ登録）を補助するメソッド
    /// </summary>
    protected void ExecuteEventBlock(string blockName)
    {
        isEventTriggered = true;
        FungusHelper.ExecuteBlock(targetFlowchart, blockName);
    }

    /// <summary>
    /// Gizmosに表示するイベント名を返すプロパティ。
    /// 子クラスで override して、Enum.ToString() を返させる。
    /// </summary>
    protected virtual string EventName => "";

    protected virtual void OnDrawGizmos()
    {
        Color fillColor = new Color(0f, 1f, 1f, 0.2f);
        Color borderColor = Color.cyan;

        BoxCollider2D box2D = GetComponent<BoxCollider2D>();
        if (box2D == null)
            return;

        // コライダーの中心座標を計算
        Vector3 centerPos = transform.position + (Vector3)box2D.offset;

        Gizmos.matrix = Matrix4x4.TRS(centerPos, transform.rotation, transform.lossyScale);
        Gizmos.color = fillColor;
        Gizmos.DrawCube(Vector3.zero, (Vector3)box2D.size);
        Gizmos.color = borderColor;
        Gizmos.DrawWireCube(Vector3.zero, (Vector3)box2D.size);

        // --- 追加: Enum名の文字表示 ---
#if UNITY_EDITOR
        // 文字列が空でない場合のみ表示
        if (!string.IsNullOrEmpty(EventName))
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.black; // 背景がシアン(水色)なので黒文字が見やすい
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 12;
            style.fontStyle = FontStyle.Bold;

            // Gizmos.matrixの影響を受けないよう、ワールド座標(centerPos)を直接指定して描画
            Handles.Label(centerPos, EventName, style);
        }
#endif
    }
}
