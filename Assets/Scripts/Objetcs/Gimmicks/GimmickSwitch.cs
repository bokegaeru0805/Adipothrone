using System.Collections;
using Fungus;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 汎用ギミックスイッチ。
/// 移動はWaypointMoverに委譲し、状態管理（Key/Flag）とイベント実行（Fungus等）を担当します。
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class GimmickSwitch : MonoBehaviour
{
    // 管理タイプ
    public enum SwitchManageType
    {
        KeyID, // 単純なID管理（ダンジョンギミックなど）
        GameFlag // ストーリー進行フラグ（SetGameBoolFlagCommand準拠）
        ,
    }

    // フラグのカテゴリ（SetGameBoolFlagCommandと同じ定義）
    public enum FlagCategory
    {
        Tutorial,
        Prologue,
        Chapter1,
        Chapter2,
    }

    // 移動制御の挙動
    public enum MovementBehavior
    {
        None, // 制御しない
        StopWhenActivated, // ONになったら止まる（押すと止まるボタン）
        StartWhenActivated // ONになったら動く（押すと動き出すリフト起動ボタンなど）
        ,
    }

    // ボタンの種類の定義
    public enum ButtonType
    {
        TutorialStage = 1, // チュートリアル（標準）
        DesertTemple = 2, // 砂漠の神殿（石っぽい音など）
        SnowMan = 3, // 雪だるま（雪っぽい音など）
        SnowMountain = 4, // 雪山（雪っぽい音など）
    }

    #region Inspector Settings

    [Header("基本設定")]
    [Tooltip("管理タイプを選択")]
    [SerializeField]
    private SwitchManageType manageType = SwitchManageType.KeyID;

    [Tooltip("ボタンの種類（SEや演出に影響します）")]
    [SerializeField]
    private ButtonType buttonType = ButtonType.TutorialStage;

    [Tooltip("ONになったときのスプライト")]
    [SerializeField]
    private Sprite onSprite;

    // --- KeyID 設定 ---
    [SerializeField]
    [ShowIf("IsKeyType")]
    [BoxGroup("Key Settings")]
    private KeyID keyID;

    // --- GameFlag 設定 (SetGameBoolFlagCommand準拠) ---
    [SerializeField]
    [ShowIf("IsFlagType")]
    [BoxGroup("Flag Settings")]
    private FlagCategory flagCategory = FlagCategory.Tutorial;

    [SerializeField]
    [AllowNesting]
    [ShowIf("IsTutorialFlag")]
    [BoxGroup("Flag Settings")]
    [Label("Flag Name")]
    private TutorialEvent tutorialFlag;

    [SerializeField]
    [AllowNesting]
    [ShowIf("IsPrologueFlag")]
    [BoxGroup("Flag Settings")]
    [Label("Flag Name")]
    private PrologueTriggeredEvent prologueFlag;

    [SerializeField]
    [AllowNesting]
    [ShowIf("IsChapter1Flag")]
    [BoxGroup("Flag Settings")]
    [Label("Flag Name")]
    private Chapter1TriggeredEvent chapter1Flag;

    [SerializeField]
    [AllowNesting]
    [ShowIf("IsChapter2Flag")]
    [BoxGroup("Flag Settings")]
    [Label("Flag Name")]
    private Chapter2TriggeredEvent chapter2Flag;

    [SerializeField]
    [AllowNesting]
    [ShowIf("IsChapter3Flag")]
    [BoxGroup("Flag Settings")]
    [Label("Flag Name")]
    private Chapter3TriggeredEvent chapter3Flag;

    [Header("挙動設定")]
    [Tooltip("スイッチON時に自動でフラグ/KeyをSaveデータに書き込むか")]
    [SerializeField]
    private bool autoSaveState = true;

    [Tooltip(
        "一度押したら押しっぱなしになるか（Falseなら離すとOFFに戻る挙動も作れるが、今回はONのみ実装）"
    )]
    [SerializeField]
    private bool isOneWay = true;

    [Header("判定設定")]
    [Tooltip("壁越し（GroundLayer）の攻撃ヒットを無効にするか")]
    [SerializeField]
    private bool preventWallPenetration = false;

    [Header("移動連携 (WaypointMover)")]
    [Tooltip("連携する移動コンポーネント（空欄なら自身から検索）")]
    [SerializeField]
    private WaypointMover targetMover;

    [Tooltip("スイッチの状態に応じた移動制御")]
    [SerializeField]
    private MovementBehavior movementBehavior = MovementBehavior.StopWhenActivated;

    [Header("イベント")]
    [Tooltip("スイッチがONになった瞬間に実行されるイベント（Fungus起動などはここ）")]
    [SerializeField]
    private UnityEvent onActivated;

    [Tooltip("ロード時など、既にONだった場合に実行されるイベント（初期化用）")]
    [SerializeField]
    private UnityEvent onAlreadyActive;

    #endregion

    #region Internal State

    private bool isPushed = false;
    private Sprite offSprite;
    private SpriteRenderer spriteRenderer;
    private Collider2D switchCollider;
    private LayerMask groundLayer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );
        spriteRenderer = GetComponent<SpriteRenderer>();
        switchCollider = GetComponent<Collider2D>();
        offSprite = spriteRenderer.sprite;

        // Moverがアタッチされていなくて、自動検索設定なら取得
        if (targetMover == null)
        {
            targetMover = GetComponent<WaypointMover>();
        }
    }

    private void OnEnable()
    {
        // 状態の読み込みと初期化
        // FlagManagerのインスタンス生成タイミングによっては遅延が必要な場合があるため注意
        if (FlagManager.instance != null)
        {
            InitializeState();
        }
        else
        {
            // マネージャーがない場合（単体テスト等）は遅延初期化などを検討
            StartCoroutine(DelayedInit());
        }
    }

    private IEnumerator DelayedInit()
    {
        yield return null;
        if (FlagManager.instance != null)
            InitializeState();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // プレイヤーの攻撃タグであればスイッチを作動させる
        bool isValidTrigger = other.CompareTag(GameConstants.PLAYER_ATTACK_TAG_NAME);

        if (isValidTrigger && !isPushed)
        {
            // 壁抜け防止チェック
            if (preventWallPenetration)
            {
                // PlayerManagerからプレイヤーの現在位置を取得
                if (PlayerManager.instance != null)
                {
                    // 始点: プレイヤーの足元ではなく、中心（高さの半分）にする
                    Vector2 playerCenter =
                        PlayerManager.instance.GetPlayerPosition()
                        + new Vector2(0, GameConstants.PLAYER_BASE_HEIGHT / 2.0f);

                    // 終点: スイッチのPivotがBottomでも地面に引っかからないよう、コライダーの中心にする
                    Vector2 switchCenter = switchCollider.bounds.center;

                    // プレイヤーの中心からスイッチのコライダーの中心へLinecastを飛ばす
                    RaycastHit2D hit = Physics2D.Linecast(playerCenter, switchCenter, groundLayer);

                    // もし何かに当たった（＝間に壁がある）場合は、起動せずに処理を抜ける
                    if (hit.collider != null)
                    {
                        return;
                    }
                }
            }

            ActivateSwitch();
        }
    }

    #endregion

    #region Logic

    /// <summary>
    /// 現在のセーブデータから状態を読み込み、見た目と挙動を反映する
    /// </summary>
    private void InitializeState()
    {
        // 1. 現在の状態を取得
        bool currentState = GetCurrentFlagState();

        // 2. 内部状態更新
        isPushed = currentState;

        // 3. 見た目の更新
        UpdateVisuals();

        // 4. 移動挙動の反映
        UpdateMovementState();

        // 5. 初期化イベント発火（既にONだった場合の演出など）
        if (isPushed)
        {
            onAlreadyActive?.Invoke();
        }
    }

    /// <summary>
    /// スイッチを作動させる
    /// </summary>
    private void ActivateSwitch()
    {
        if (isOneWay && isPushed)
            return;

        isPushed = true;

        // 1. データの保存
        if (autoSaveState)
        {
            SetCurrentFlagState(true);
        }

        // 2. 見た目の更新
        UpdateVisuals();

        // 3. 移動挙動の反映
        UpdateMovementState();

        // 4. イベント実行 (Fungusの起動、SE再生など)
        onActivated?.Invoke();

        // 5. 種類に応じたSE再生 (修正箇所)
        PlaySwitchSE();
    }

    /// <summary>
    /// スプライトの切り替え
    /// </summary>
    private void UpdateVisuals()
    {
        if (spriteRenderer != null && onSprite != null)
        {
            spriteRenderer.sprite = isPushed ? onSprite : offSprite;
        }
    }

    /// <summary>
    /// 設定されたButtonTypeに基づいて適切なSEを再生する
    /// </summary>
    private void PlaySwitchSE()
    {
        if (SEManager.instance == null)
            return;

        switch (buttonType)
        {
            case ButtonType.TutorialStage:
                SEManager.instance.PlayFieldSE(SE_Field.SwitchOn);
                break;

            case ButtonType.DesertTemple:
                SEManager.instance.PlayFieldSE(SE_Field.LeverPull1);
                break;

            case ButtonType.SnowMan:
                // TODO: 雪だるまが崩れる音を再生
                break;
            case ButtonType.SnowMountain:
                SEManager.instance.PlayFieldSE(SE_Field.LeverPull1);
                break;
            default:
                SEManager.instance.PlayFieldSE(SE_Field.SwitchOn);
                break;
        }
    }

    /// <summary>
    /// 設定に基づいてWaypointMoverを制御する
    /// </summary>
    private void UpdateMovementState()
    {
        if (targetMover == null || movementBehavior == MovementBehavior.None)
            return;

        bool shouldMove = true;

        switch (movementBehavior)
        {
            case MovementBehavior.StopWhenActivated:
                // ONなら止まる、OFFなら動く
                shouldMove = !isPushed;
                break;
            case MovementBehavior.StartWhenActivated:
                // ONなら動く、OFFなら止まる
                shouldMove = isPushed;
                break;
        }

        if (shouldMove)
        {
            targetMover.StartMoving();
        }
        else
        {
            targetMover.StopMoving();
        }
    }

    #endregion

    #region Flag Management Wrappers

    /// <summary>
    /// 現在の設定に基づいてフラグの状態を取得する
    /// </summary>
    private bool GetCurrentFlagState()
    {
        if (manageType == SwitchManageType.KeyID)
        {
            return FlagManager.instance.GetKeyOpened(keyID);
        }
        else
        {
            // FlagManagerにジェネリックなGetメソッドがあると仮定、なければ各型で分岐
            // ここでは SetGameBoolFlagCommand と同様に分岐して取得します
            switch (flagCategory)
            {
                case FlagCategory.Tutorial:
                    return FlagManager.instance.GetBoolFlag(tutorialFlag);
                case FlagCategory.Prologue:
                    return FlagManager.instance.GetBoolFlag(prologueFlag);
                case FlagCategory.Chapter1:
                    return FlagManager.instance.GetBoolFlag(chapter1Flag);
                case FlagCategory.Chapter2:
                    return FlagManager.instance.GetBoolFlag(chapter2Flag);
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// 現在の設定に基づいてフラグの状態を設定する
    /// </summary>
    private void SetCurrentFlagState(bool value)
    {
        if (manageType == SwitchManageType.KeyID)
        {
            FlagManager.instance.SetKeyOpened(keyID, value);
        }
        else
        {
            switch (flagCategory)
            {
                case FlagCategory.Tutorial:
                    FlagManager.instance.SetBoolFlag(tutorialFlag, value);
                    break;
                case FlagCategory.Prologue:
                    FlagManager.instance.SetBoolFlag(prologueFlag, value);
                    break;
                case FlagCategory.Chapter1:
                    FlagManager.instance.SetBoolFlag(chapter1Flag, value);
                    break;
                case FlagCategory.Chapter2:
                    FlagManager.instance.SetBoolFlag(chapter2Flag, value);
                    break;
            }
        }
    }

    #endregion

    #region NaughtyAttributes Validators

    private bool IsKeyType() => manageType == SwitchManageType.KeyID;

    private bool IsFlagType() => manageType == SwitchManageType.GameFlag;

    private bool IsTutorialFlag() => IsFlagType() && flagCategory == FlagCategory.Tutorial;

    private bool IsPrologueFlag() => IsFlagType() && flagCategory == FlagCategory.Prologue;

    private bool IsChapter1Flag() => IsFlagType() && flagCategory == FlagCategory.Chapter1;

    private bool IsChapter2Flag() => IsFlagType() && flagCategory == FlagCategory.Chapter2;

    #endregion
}
