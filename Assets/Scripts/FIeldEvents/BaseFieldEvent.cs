using Fungus;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 全てのフィールドイベントの基底クラス。
/// 共通のトリガー判定、初期化、Gizmoおよびゲーム画面上のデバッグエリア描画を担当します。
/// </summary>
public abstract class BaseFieldEvent : MonoBehaviour
{
    #region インスペクター設定

    [Header("基本設定")]
    [Tooltip("このイベントが実行するFlowchart")]
    public Flowchart targetFlowchart = null;

    #endregion

    #region 内部状態・キャッシュ

    protected FlagManager flagManager;
    protected bool isEventTriggered = false;
    private GameObject debugOverlayObj;

    #endregion

    #region プロパティ

    /// <summary>
    /// GizmosやデバッグUIに表示するイベント名を返すプロパティ。
    /// 子クラスで override して、Enum.ToString() などを返させるようにします。
    /// </summary>
    protected virtual string EventName => "";

    #endregion

    #region Unityライフサイクル

    protected virtual void Awake()
    {
        if (targetFlowchart == null)
        {
            Debug.LogWarning($"{gameObject.name} に Flowchart が設定されていません", this);
        }
    }

    protected virtual void Start()
    {
        flagManager = FlagManager.instance;
        if (flagManager == null)
        {
            Debug.LogError("FlagManager が見つかりません。", this);
        }

        // デバッグモードが解放されていれば、ゲーム画面上に可視化オブジェクトを生成する
        if (DebugMenuManager.isDebugModeUnlocked)
        {
            CreateDebugOverlay();

            // 初期状態を反映し、トグル操作による表示切替イベントを購読
            SetDebugOverlayVisible(DebugMenuManager.isShowEventArea);
            DebugMenuManager.OnEventAreaDisplayToggled += SetDebugOverlayVisible;
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // Flowchartが存在し、かつ何かブロックを実行中なら、新しいトリガーを無視する
        bool isFungusBusy = targetFlowchart != null && targetFlowchart.HasExecutingBlocks();

        // 共通の実行条件チェック
        bool canTrigger =
            collision.CompareTag(GameConstants.PLAYER_TAG_NAME)
            && Time.timeScale > 0
            && !isEventTriggered
            && !isFungusBusy
            && (PlayerManager.instance?.isControlLocked ?? false) == false;

        if (canTrigger && flagManager != null)
        {
            // 条件を満たしたら、子クラスで実装された具体的な処理を呼ぶ
            HandleEvent();
        }
    }

    private void OnDestroy()
    {
        // メモリリーク防止のため、オブジェクト破棄時に購読解除
        DebugMenuManager.OnEventAreaDisplayToggled -= SetDebugOverlayVisible;
    }

    #endregion

    #region イベント処理（サブクラス実装用）

    /// <summary>
    /// 各章ごとの具体的なイベント分岐処理を記述する抽象メソッド。
    /// サブクラスで必ず実装する必要があります。
    /// </summary>
    protected abstract void HandleEvent();

    /// <summary>
    /// イベント実行時の定型処理（ブロック実行、フラグ立て、ログ登録など）を補助するメソッド。
    /// </summary>
    /// <param name="blockName">実行するFlowchartのブロック名</param>
    protected void ExecuteEventBlock(string blockName)
    {
        isEventTriggered = true;
        FungusHelper.ExecuteBlock(targetFlowchart, blockName);
    }

    #endregion

    #region デバッグ・可視化処理

    /// <summary>
    /// デバッグオーバーレイの有効・無効を切り替えます。
    /// </summary>
    /// <param name="visible">表示状態</param>
    private void SetDebugOverlayVisible(bool visible)
    {
        if (debugOverlayObj != null)
        {
            debugOverlayObj.SetActive(visible);
        }
    }

    /// <summary>
    /// ゲーム画面上にコライダーの範囲とイベント名を表示するためのオブジェクトを動的に生成します。
    /// （ビルド後でもデバッグモードONなら表示されます）
    /// </summary>
    private void CreateDebugOverlay()
    {
        BoxCollider2D box2D = GetComponent<BoxCollider2D>();
        if (box2D == null)
            return;

        // --- ルートオブジェクトの生成 ---
        debugOverlayObj = new GameObject("DebugOverlay_EventArea");
        debugOverlayObj.transform.SetParent(transform, false);
        debugOverlayObj.transform.localPosition = (Vector3)box2D.offset;

        // --- 1. 背景（コライダー範囲の可視化） ---
        GameObject bgObj = new GameObject("AreaVisual");
        bgObj.transform.SetParent(debugOverlayObj.transform, false);
        bgObj.transform.localPosition = Vector3.zero;

        SpriteRenderer sr = bgObj.AddComponent<SpriteRenderer>();
        int texWidth = Texture2D.whiteTexture.width;
        int texHeight = Texture2D.whiteTexture.height;

        // PPUをテクスチャ幅と一致させ、基本サイズを正確な 1x1 ユニットに固定
        sr.sprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0, 0, texWidth, texHeight),
            new Vector2(0.5f, 0.5f),
            texWidth
        );

        bgObj.transform.localScale = (Vector3)box2D.size;
        sr.sortingLayerName = GameConstants.SORTING_LAYER_NAME_CHARACTER;
        sr.sortingOrder = 1000;
        sr.color = new Color(0f, 1f, 1f, 0.3f);

        // --- 2. テキスト（イベント名の表示） ---
        GameObject textObj = new GameObject("EventNameLabel");
        textObj.transform.SetParent(debugOverlayObj.transform, false);
        textObj.transform.localPosition = Vector3.zero;

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = EventName;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        // 親の非一様スケール（7.5, 16など）による文字の歪みを防ぐための補正
        Vector3 worldScale = transform.lossyScale;
        if (worldScale.x != 0 && worldScale.y != 0)
        {
            textObj.transform.localScale = new Vector3(1f / worldScale.x, 1f / worldScale.y, 1f);
        }

        // TextMeshProの描画枠を、コライダーのワールド空間での実寸大に合わせる
        RectTransform rt = textObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(box2D.size.x * worldScale.x, box2D.size.y * worldScale.y);
        }

        // 自動サイズ調整（Auto Sizing）をONにして枠内に文字を収める
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 1.0f;
        tmp.fontSizeMax = 6.0f;
        tmp.enableWordWrapping = true;

        // TextMeshProのレイヤーはMeshRenderer経由で設定する
        MeshRenderer meshRenderer = tmp.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingLayerName = GameConstants.SORTING_LAYER_NAME_CHARACTER;
            meshRenderer.sortingOrder = 1001;
        }
    }

    protected virtual void OnDrawGizmos()
    {
        BoxCollider2D box2D = GetComponent<BoxCollider2D>();
        if (box2D == null)
            return;

        Color fillColor = new Color(0f, 1f, 1f, 0.2f);
        Color borderColor = Color.cyan;

        // コライダーの中心座標を計算
        Vector3 centerPos = transform.position + (Vector3)box2D.offset;

        Gizmos.matrix = Matrix4x4.TRS(centerPos, transform.rotation, transform.lossyScale);
        Gizmos.color = fillColor;
        Gizmos.DrawCube(Vector3.zero, (Vector3)box2D.size);
        Gizmos.color = borderColor;
        Gizmos.DrawWireCube(Vector3.zero, (Vector3)box2D.size);

#if UNITY_EDITOR
        // 文字列が空でない場合のみ、Enum名などの文字を表示
        if (!string.IsNullOrEmpty(EventName))
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.black; // 背景がシアンなので黒文字が見やすい
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 12;
            style.fontStyle = FontStyle.Bold;

            // Gizmos.matrixの影響を受けないよう、ワールド座標を直接指定して描画
            Handles.Label(centerPos, EventName, style);
        }
#endif
    }

    #endregion
}
