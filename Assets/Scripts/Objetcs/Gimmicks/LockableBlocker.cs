using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 特定の鍵（KeyID）条件を満たすと開くドアを制御するクラス。
/// FlagManagerの状態を監視し、見た目の更新とSE再生を行います。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class LockableBlocker : MonoBehaviour
{
    // --- 定義 ---

    /// <summary>
    /// ドアの見た目や音のタイプを定義するEnum
    /// </summary>
    public enum BlockerType
    {
        TutorialStage, // チュートリアル（金属的）
        DesertTemple, // 砂漠（石、重い音）
        Tower, // タワー（ガラス的）
        // 必要に応じて追加（SciFi, Dungeon, Forest...）
    }

    #region Inspector Settings

    [Header("基本設定")]
    [Tooltip("FlagManagerで設定されたブロックID")]
    [SerializeField]
    private int blockerID;

    [Tooltip("ブロックの種類（SEや演出に影響）")]
    [SerializeField]
    private BlockerType blockerType = BlockerType.TutorialStage;

    [Header("見た目の設定")]
    [Tooltip(
        "Trueの場合、開いた時にオブジェクトを非表示にします。\nFalseの場合、Spriteを開放状態のものに差し替えます。"
    )]
    [SerializeField]
    private bool hideWhenOpen = true;

    [Tooltip("hideWhenOpenがFalseの場合に使用される、開いた状態のスプライト")]
    [SerializeField, HideIf(nameof(hideWhenOpen))]
    private Sprite openSprite;

    #endregion

    #region Internal State

    // 現在の開閉状態
    private bool isBlockerOpen = false;

    // コンポーネント参照
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private Sprite closedSprite; // 初期（閉じている）スプライト
    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        // 初回チェック：音を鳴らさずに状態を即時反映
        UpdateBlockerState(playEffect: false);
    }

    private void OnEnable()
    {
        // フラグ変更イベントの購読
        if (FlagManager.instance != null)
        {
            FlagManager.OnKeyFlagChanged += HandleKeyFlagChanged;
        }
    }

    private void OnDisable()
    {
        // イベント購読の解除
        if (FlagManager.instance != null)
        {
            FlagManager.OnKeyFlagChanged -= HandleKeyFlagChanged;
        }
    }

    #endregion

    #region Logic & Events

    /// <summary>
    /// コンポーネントの取得と初期化
    /// </summary>
    private void InitializeComponents()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            closedSprite = spriteRenderer.sprite;
        }
    }

    /// <summary>
    /// KeyIDのフラグが変更された際に呼び出されるイベントハンドラ
    /// </summary>
    private void HandleKeyFlagChanged(KeyID keyId, bool isOpened)
    {
        // 鍵の状態が変わったので、このブロックが開くべきか再チェックする
        // (演出ありで更新)
        UpdateBlockerState(playEffect: true);
    }

    /// <summary>
    /// ブロック の開閉状態を確認し、見た目と判定を更新する
    /// </summary>
    /// <param name="playEffect">SE再生などの演出を行うかどうか（Start時はfalse）</param>
    private void UpdateBlockerState(bool playEffect)
    {
        if (FlagManager.instance == null)
            return;

        // 1. 現在開くべき状態かを確認
        bool shouldBeOpen = FlagManager.instance.IsDoorUnlocked(blockerID);

        // 2. 状態が変わっていないなら何もしない
        if (shouldBeOpen == isBlockerOpen)
        {
            return;
        }

        // 3. 内部状態の更新
        isBlockerOpen = shouldBeOpen;

        // 4. 物理的な判定の更新（開いていれば通り抜け可能＝Collider無効）
        if (boxCollider != null)
        {
            boxCollider.enabled = !isBlockerOpen;
        }

        // 5. 見た目の更新
        UpdateVisuals();

        // 6. SE再生（演出が必要な場合のみ）
        if (playEffect)
        {
            PlayBlockerSE(isBlockerOpen);
        }
    }

    /// <summary>
    /// スプライトの表示切り替えを行う
    /// </summary>
    private void UpdateVisuals()
    {
        if (spriteRenderer == null)
            return;

        if (isBlockerOpen)
        {
            if (hideWhenOpen)
            {
                // 開いたら消える設定
                spriteRenderer.enabled = false;
            }
            else
            {
                // 開いた画像に切り替える設定
                spriteRenderer.enabled = true;
                if (openSprite != null)
                {
                    spriteRenderer.sprite = openSprite;
                }
            }
        }
        else
        {
            // 閉じている状態
            spriteRenderer.enabled = true;
            spriteRenderer.sprite = closedSprite;
        }
    }

    /// <summary>
    /// Enum設定に基づいて適切なSEを再生する
    /// </summary>
    private void PlayBlockerSE(bool isOpen)
    {
        if (SEManager.instance == null)
            return;

        // 閉じる時の音（共通または別途定義）
        if (!isOpen)
        {
            // 現状はロック音を使用
            SEManager.instance.PlayFieldSE(SE_Field.DoorOpenLock);
            return;
        }

        // 開く時の音（Biomeによって分岐）
        switch (blockerType)
        {
            case BlockerType.TutorialStage:
                // 金属的なドア音
                SEManager.instance.PlayFieldSE(SE_Field.DoorOpen_Metal);
                break;

            case BlockerType.DesertTemple:
                SEManager.instance.PlayFieldSE(SE_Field.DoorOpen_Metal);
                break;

            case BlockerType.Tower:
                SEManager.instance.PlayFieldSE(SE_Field.DoorOpen_Metal);
                break;

            default:
                // デフォルト
                SEManager.instance.PlayFieldSE(SE_Field.DoorOpen_Metal);
                break;
        }
    }

    #endregion
}
