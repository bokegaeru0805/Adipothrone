using Fungus;
using UnityEngine;

/// <summary>
/// プレイヤーの状態変化イベントを受け取り、操作ガイドUIの表示・非表示を切り替えるコンポーネント。
/// </summary>
public class ControlGuideUIImageSwitcher : MonoBehaviour
{
    private PlayerManager playerManager;
    private SpotlightQuickItemController spotlightController;

    [Header("UIのフォルダオブジェクト")]
    [Tooltip("操作方法パネル")]
    [SerializeField]
    private GameObject controlGuidePanel;

    [Tooltip("通常時の操作ガイドUI")]
    [SerializeField]
    private GameObject normalControlGuide;

    [Tooltip("クィックアイテムリストがハイライト時の操作ガイドUI")]
    [SerializeField]
    private GameObject quickItemHighlightControlGuide;

    [Header("各操作ガイドUIのゲームオブジェクト")]
    [Tooltip("「左移動」のガイドUI")]
    [SerializeField]
    private GameObject moveleftGuide;

    [Tooltip("「右移動」のガイドUI")]
    [SerializeField]
    private GameObject moverightGuide;

    [Tooltip("「ジャンプ」のガイドUI")]
    [SerializeField]
    private GameObject jumpGuide;

    [Tooltip("「ダッシュ」のガイドUI")]
    [SerializeField]
    private GameObject dashGuide;

    [Tooltip("「攻撃」のガイドUI")]
    [SerializeField]
    private GameObject attackGuide;

    [Tooltip("「インタラクト」のガイドUI")]
    [SerializeField]
    private GameObject interactGuide;

    [Tooltip("「武器変更」のガイドUI")]
    [SerializeField]
    private GameObject changeWeaponGuide;

    [Tooltip("「クイックアイテムを開く」のガイドUI")]
    [SerializeField]
    private GameObject quickItemOpenGuide;

    [Tooltip("「メニュー」のガイドUI")]
    [SerializeField]
    private GameObject menuGuide;
    private Heroin_move playerScript = null;
    private Robot_move robotScript = null;
    private bool canRobotAttack = false;
    private bool canChangeAttackType = false;
    private bool isRobotVisible = false;
    private bool previousQuickItemHighlightState = false;

    private void Start()
    {
        // エディタ実行時など、特別なケースへの対応
        if (!GameManager.isFirstGameSceneOpen)
            return;

        // すでにロードが完了しているかチェック
        if (!SaveLoadManager.IsLoading)
        {
            // ロード済みなら即座に初期化
            InitializeControlGuide();
        }
        else
        {
            // ロード中なら、完了イベントを待つ
            SaveLoadManager.OnLoadingStateChanged += OnLoadingStateChanged;
        }
    }

    /// <summary>
    /// ロード状態が変化した時に呼ばれる
    /// </summary>
    private void OnLoadingStateChanged(bool isLoading)
    {
        if (!isLoading)
        {
            // ロード完了！ イベント解除して初期化実行
            SaveLoadManager.OnLoadingStateChanged -= OnLoadingStateChanged;
            InitializeControlGuide();
        }
    }

    /// <summary>
    /// 実際の初期化処理を行うメソッド
    /// </summary>
    private void InitializeControlGuide()
    {
        // 1. SaveLoadManagerの確認
        SaveLoadManager saveLoadManager = SaveLoadManager.instance;
        if (saveLoadManager == null)
        {
            Debug.LogWarning("SaveLoadManagerが見つかりません。");
            return;
        }

        // 2. 設定の確認（パフォーマンス最適化の要）
        // 表示設定がOFFなら、オブジェクトごと非表示にして処理を終了する
        if (!saveLoadManager.Settings.isShowingControlsGuide)
        {
            if (controlGuidePanel != null)
            {
                controlGuidePanel.SetActive(false);
            }
            // コンポーネント自体を無効化し、Updateが走らないようにする（軽量化）
            this.enabled = false;
            return;
        }

        // --- ここまで到達したら「表示する」ということなので、各種参照を取得 ---

        // PlayerManagerの取得
        playerManager = PlayerManager.instance;
        if (playerManager != null)
        {
            playerManager.OnBoolStatusChanged += OnAnyBoolStatusChanged;
        }
        else
        {
            Debug.LogError("PlayerManagerが見つかりません。");
            this.enabled = false; // 動けないので無効化
            return;
        }

        // SpotlightControllerの取得
        spotlightController = SpotlightQuickItemController.instance;
        if( spotlightController == null)
        {
            Debug.LogError("SpotlightQuickItemControllerが見つかりません。");
            this.enabled = false; // 動けないので無効化
            return;
        }

        // GameManagerイベント購読
        GameManager.OnTalkingStateChanged += OnTalkingStateChanged;

        // プレイヤーとロボットの取得（ロード完了後なので Find で見つかるはず）
        GameObject playerObject = GameObject.FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME);
        if (playerObject != null)
        {
            playerScript = playerObject.GetComponent<Heroin_move>();
            if (playerScript != null)
            {
                playerScript.OnPlayerVisibilityChanged += OnPlayerVisibilityChanged;
            }

            // ロボットの検索（構造依存: Playerの子要素）
            Transform robotTrans = playerObject.transform.Find(GameConstants.ROBOT_OBJECT_NAME);
            // ※ GetChild(0)より名前検索(Find)の方が安全です。構造変化に強いため。

            if (robotTrans != null)
            {
                robotScript = robotTrans.GetComponent<Robot_move>();
                if (robotScript != null)
                {
                    robotScript.OnRobotVisibilityChanged += OnRobotVisibilityChanged;
                }
            }
        }
        else
        {
            Debug.LogError("Playerオブジェクトが見つかりません。");
            this.enabled = false; // 動けないので無効化
            return;
        }

        // 3. UIの初期セットアップ
        InitialUISetup();

        // 4. Updateを動かすためにコンポーネントを有効化
        this.enabled = true;
    }

    private void Update()
    {
        // spotlightControllerのnullチェックは Initialize で担保するが、念のため残す
        if (spotlightController == null)
            return;

        bool currentHighlightState = spotlightController.IsHighlighting;

        if (currentHighlightState == previousQuickItemHighlightState)
            return;

        normalControlGuide?.SetActive(!currentHighlightState);
        quickItemHighlightControlGuide?.SetActive(currentHighlightState);

        previousQuickItemHighlightState = currentHighlightState;
    }

    /// <summary>
    /// 破棄時の処理
    /// </summary>
    private void OnDisable()
    {
        // イベントの解除漏れを防ぐ
        SaveLoadManager.OnLoadingStateChanged -= OnLoadingStateChanged;
        GameManager.OnTalkingStateChanged -= OnTalkingStateChanged;

        if (playerManager != null)
            playerManager.OnBoolStatusChanged -= OnAnyBoolStatusChanged;

        if (playerScript != null)
            playerScript.OnPlayerVisibilityChanged -= OnPlayerVisibilityChanged;

        if (robotScript != null)
            robotScript.OnRobotVisibilityChanged -= OnRobotVisibilityChanged;
    }

    // --- イベントハンドラ（イベント発生時に呼び出されるメソッド） ---

    /// <summary>
    /// PlayerManagerのいずれかのbool値が変更されたときに呼び出されます。
    /// </summary>
    /// <param name="flag">どのステータスが変更されたかを示すEnum</param>
    /// <param name="isEnabled">ステータスの新しい値 (true/false)</param>
    private void OnAnyBoolStatusChanged(PlayerStatusBoolName flag, bool isEnabled)
    {
        // どのフラグが変更されたかをswitch文で判定し、対応するUIを更新
        switch (flag)
        {
            // ロボットが攻撃可能かどうかの状態
            case PlayerStatusBoolName.isRobotattack:
                canRobotAttack = isEnabled;
                UpdateRobotAttackGuideVisibility();
                UpdateChangeWeaponGuideVisibility();
                break;

            // 武器変更が可能かどうかの状態
            case PlayerStatusBoolName.isChangeAttackType:
                canChangeAttackType = isEnabled;
                UpdateChangeWeaponGuideVisibility();
                break;
        }
    }

    private void OnPlayerVisibilityChanged(bool isVisible)
    {
        moveleftGuide.SetActive(isVisible);
        moverightGuide.SetActive(isVisible);
        dashGuide.SetActive(isVisible);
        jumpGuide.SetActive(isVisible);
        interactGuide.SetActive(isVisible);
        quickItemOpenGuide.SetActive(isVisible);
    }

    private void OnRobotVisibilityChanged(bool isVisible)
    {
        isRobotVisible = isVisible;
        UpdateRobotAttackGuideVisibility();
        UpdateChangeWeaponGuideVisibility();
    }

    //ロボットの攻撃ガイドの表示状態を更新
    private void UpdateRobotAttackGuideVisibility()
    {
        // 全ての条件がtrueの場合のみ、表示をtrueにする
        bool shouldBeVisible = canRobotAttack && isRobotVisible;
        attackGuide.SetActive(shouldBeVisible);
    }

    //武器変更ガイドの表示状態を更新
    private void UpdateChangeWeaponGuideVisibility()
    {
        // 全ての条件がtrueの場合のみ、表示をtrueにする
        bool shouldBeVisible = canRobotAttack && canChangeAttackType && isRobotVisible;
        changeWeaponGuide.SetActive(shouldBeVisible);
    }

    // 操作ガイド全般の表示状態を更新
    private void OnTalkingStateChanged(bool isTalking)
    {
        // 会話中は操作ガイドを非表示にする
        controlGuidePanel.SetActive(!isTalking);
    }

    // --- 初期化メソッド ---

    /// <summary>
    /// ゲーム開始時や有効化された際に、一度だけ現在の状態でUIをまとめて更新します。
    /// </summary>
    private void InitialUISetup()
    {
        if (playerManager == null)
            return;

        // --- PlayerManagerが管理する状態の初期化 ---
        // isRobotattackの現在の状態でUIを初期化
        OnAnyBoolStatusChanged(
            PlayerStatusBoolName.isRobotattack,
            playerManager.GetPlayerBoolStatus(PlayerStatusBoolName.isRobotattack)
        );
        // isChangeAttackTypeの現在の状態でUIを初期化
        OnAnyBoolStatusChanged(
            PlayerStatusBoolName.isChangeAttackType,
            playerManager.GetPlayerBoolStatus(PlayerStatusBoolName.isChangeAttackType)
        );

        if (normalControlGuide == null || quickItemHighlightControlGuide == null)
        {
            Debug.LogError("ControlGuideUIImageSwitcherのUIオブジェクトが設定されていません！");
            return;
        }
        else
        {
            normalControlGuide.SetActive(true);
            quickItemHighlightControlGuide.SetActive(false);
        }
    }
}
