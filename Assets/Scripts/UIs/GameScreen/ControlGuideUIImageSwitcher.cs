using Fungus;
using UnityEngine;

/// <summary>
/// プレイヤーの状態変化イベントを受け取り、操作ガイドUIの表示・非表示を切り替えるコンポーネント。
/// Update()を使わないイベント駆動設計により、パフォーマンスと拡張性を高めています。
/// </summary>
public class ControlGuideUIImageSwitcher : MonoBehaviour
{
    private PlayerManager playerManager;

    [Header("UIの表示切り替えを行うオブジェクトを設定")]
    [Tooltip("操作方法パネル")]
    [SerializeField]
    private GameObject controlGuidePanel;

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

    [Tooltip("「クイックアイテム使用」のガイドUI")]
    [SerializeField]
    private GameObject quickItemUseGuide;

    [Tooltip("「クイックアイテム移動」のガイドUI")]
    [SerializeField]
    private GameObject quickItemMoveGuide;

    [Tooltip("「メニュー」のガイドUI")]
    [SerializeField]
    private GameObject menuGuide;
    private Heroin_move playerScript = null;
    private Robot_move robotScript = null;
    private bool canRobotAttack = false;
    private bool canChangeAttackType = false;
    private bool isRobotVisible = false;

    private void Start()
    {
        //ゲームがまだ開始されていない場合は何もしない
        if (!GameManager.isFirstGameSceneOpen)
            return;

        // SaveLoadManagerから設定を読み込む
        SaveLoadManager saveLoadManager = SaveLoadManager.instance;
        if (saveLoadManager != null)
        {
            //「操作方法UIを表示する」設定がオフ（false）の場合
            if (!saveLoadManager.Settings.isShowingControlsGuide)
            {
                // パネル全体を非表示にして、このスクリプトの以降の初期化処理をすべて中断する
                if (controlGuidePanel != null)
                {
                    controlGuidePanel.SetActive(false);
                }
                return; // ここで処理を終了
            }
        }
        else
        {
            Debug.LogWarning("SaveLoadManagerが見つかりません。操作ガイドの表示設定を読み込めませんでした。", this);
            // SaveLoadManagerが見つからない場合は、デフォルトで表示する前提で処理を続行
        }

        // 他コンポーネントのAwake()での初期化を保証するため、イベント購読はStart()で行う。
        GameManager.OnTalkingStateChanged += OnTalkingStateChanged;
        playerManager = PlayerManager.instance;
        if (playerManager == null)
        {
            Debug.LogError(
                "PlayerManagerが見つかりません。ControlGuideUIImageSwitcherは機能しません。"
            );
            return;
        }
        else
        {
            playerManager.OnBoolStatusChanged += OnAnyBoolStatusChanged;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(GameConstants.PlayerTagName);
        if (playerObject != null)
        {
            playerScript = playerObject.GetComponent<Heroin_move>();
            if (playerScript != null)
            {
                // プレイヤーの可視状態が変化したときのイベントを購読
                playerScript.OnPlayerVisibilityChanged += OnPlayerVisibilityChanged;
            }
            else
            {
                Debug.LogWarning(
                    "プレイヤーのスクリプトが見つかりません。ControlGuideUIImageSwitcherは機能しません。"
                );
            }
        }

        GameObject robotObject = playerObject.transform.GetChild(0).gameObject;
        if (robotObject != null && robotObject.name == GameConstants.RobotObjectName)
        {
            // ロボットの可視状態が変化したときのイベントを購読
            robotScript = robotObject.GetComponent<Robot_move>();
            if (robotScript != null)
            {
                robotScript.OnRobotVisibilityChanged += OnRobotVisibilityChanged;
            }
            else
            {
                Debug.LogWarning(
                    "ロボットのスクリプトが見つかりません。ControlGuideUIImageSwitcherは機能しません。"
                );
            }
        }
        else
        {
            Debug.LogWarning(
                "ロボットオブジェクトが見つかりません。ControlGuideUIImageSwitcherは機能しません。"
            );
        }

        // 現在の状態に基づいて、UIの初期表示を一度だけ設定
        InitialUISetup();
    }

    /// <summary>
    /// このオブジェクトが無効になった時に呼び出されます。
    /// メモリリークやエラーを防ぐため、購読したイベントを必ず解除します。
    /// </summary>
    private void OnDisable()
    {
        //ゲームがまだ開始されていない場合は何もしない
        if (!GameManager.isFirstGameSceneOpen)
            return;

        // イベント購読を解除
        GameManager.OnTalkingStateChanged -= OnTalkingStateChanged;
        if (playerManager != null)
        {
            playerManager.OnBoolStatusChanged -= OnAnyBoolStatusChanged;
        }

        if (playerScript != null)
        {
            // プレイヤーの可視状態が変化したときのイベントを解除
            playerScript.OnPlayerVisibilityChanged -= OnPlayerVisibilityChanged;
        }

        if (robotScript != null)
        {
            // ロボットの可視状態が変化したときのイベントを解除
            robotScript.OnRobotVisibilityChanged -= OnRobotVisibilityChanged;
        }
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
        quickItemUseGuide.SetActive(isVisible);
        quickItemMoveGuide.SetActive(isVisible);
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
    }
}
