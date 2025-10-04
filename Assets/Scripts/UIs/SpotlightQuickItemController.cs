using UnityEngine;
using UnityEngine.UI;

public class SpotlightQuickItemController : MonoBehaviour
{
    public static SpotlightQuickItemController instance { get; private set; }

    [Header("スポットライト画像のゲームオブジェクト")]
    [SerializeField]
    private GameObject spotlightObject = null; // 子オブジェクトであるスポットライト画像

    [Header("スポットライトの画像")]
    [SerializeField, Tooltip("通常時のスポットライト画像")]
    private Sprite normalSprite = null; // 通常時のスポットライト画像

    [SerializeField, Tooltip("コントロールガイド表示時のスポットライト画像")]
    private Sprite withControlGuideSprite = null; // コントロールガイド表示時のスポットライト画像
    private TimeManager timeManager;
    private InputManager inputManager;
    public bool IsHighlighting { get; private set; } = false; // スポットライトが表示されているかどうかのフラグ
    private bool isMenuOpen = false; // UIManagerから通知されたメニューの表示状態を保存する変数
    private bool isTalking = false; // 会話状態を保存するローカル変数

    private void Awake()
    {

        //ゲームがまだ開始されていない場合は何もしない
        if (!GameManager.isFirstGameSceneOpen)
        {
            return;
        }
        
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 自分の最初の子オブジェクト（スポットライト画像）を自動的に取得する
        // これにより、Inspectorでの手動設定が不要になります。
        if (spotlightObject == null && transform.childCount > 0)
        {
            spotlightObject = transform.GetChild(0).gameObject;

            if (spotlightObject == null)
            {
                // 子オブジェクトが見つからなかった場合にエラーを出す
                Debug.LogError(
                    "SpotlightControllerに子オブジェクト（スポットライト画像）が見つかりません！"
                );
                return;
            }
        }

        if (normalSprite == null || withControlGuideSprite == null)
        {
            Debug.LogError("SpotlightControllerのスポットライト画像が設定されていません！");
        }

        Image spriteRenderer = spotlightObject.GetComponent<Image>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = SaveLoadManager.instance.Settings.isShowingControlsGuide
                ? withControlGuideSprite
                : normalSprite;
        }

        // ゲーム開始時は確実に非表示にしておく
        spotlightObject.SetActive(false);
    }

    // このスクリプトを持つゲームオブジェクトは非アクティブにしないでください
    // それを考慮して、Startメソッドでイベントの購読を行います
    private void Start()
    {
        // イベントを購読する
        UIManager.OnMenuStateChanged += HandleMenuStateChanged;
        GameManager.OnTalkingStateChanged += HandleTalkingStateChanged;

        timeManager = TimeManager.instance;
        if (timeManager == null)
        {
            Debug.LogError(
                "TimeManagerが見つかりません。SpotlightControllerは正常に動作しません。"
            );
            return;
        }

        inputManager = InputManager.instance;
        if (inputManager == null)
        {
            Debug.LogError(
                "InputManagerが見つかりません。SpotlightControllerは正常に動作しません。"
            );
            return;
        }
    }

    private void OnDisable()
    {
        // オブジェクトが非アクティブになったら、購読を解除（メモリリーク防止）
        UIManager.OnMenuStateChanged -= HandleMenuStateChanged;
        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
    }

    private void Update()
    {
        if (inputManager == null)
        {
            return; // InputManagerが見つからなければ何もしない
        }

        // 特定のボタンが押されていて、かつメニューが開いていなく、会話中でない
        IsHighlighting = inputManager.QuickItemHighlightHold() && !isMenuOpen && !isTalking;

        // spotlightObjectがnullでないこと、そして現在の状態とキー入力の状態が異なる場合のみ更新
        if (spotlightObject != null && spotlightObject.activeSelf != IsHighlighting)
        {
            // キーが押されていればtrue, 押されていなければfalseをSetActiveに渡す
            spotlightObject.SetActive(IsHighlighting);

            if (IsHighlighting)
            {
                // スポットライトが表示されたときに時間を停止
                timeManager.RequestPause();
            }
            else
            {
                // スポットライトが非表示になったときに時間を再開
                timeManager.ReleasePause();
            }
        }
    }

    /// <summary>
    /// UIManagerからイベント通知を受け取ったときに呼ばれるメソッド
    /// </summary>
    private void HandleMenuStateChanged(bool menuState)
    {
        isMenuOpen = menuState;
    }

    /// <summary>
    /// GameManagerから会話状態の変更通知を受け取る
    /// </summary>
    private void HandleTalkingStateChanged(bool talkState)
    {
        isTalking = talkState;
    }
}
