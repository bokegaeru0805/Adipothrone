using System;
using UnityEngine;

[Serializable]
public class LotteryItemEntry
{
    [Header("アイテム設定")]
    public BaseItemData itemData; // 当たりアイテム（nullならハズレ）
    public int count = 1; // 個数

    [Header("抽選確率")]
    [Range(0, 100)]
    public int weight = 10; // 抽選の重み
}

/// <summary>
/// くじ引き用宝箱の個体コントローラー。
/// プレイヤーのインタラクトを検知し、見た目を変更する機能を持つ。
/// </summary>
public class LotteryChestController : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField]
    private Sprite openSprite; // 開いている状態（初期/開封後）

    [SerializeField]
    private Sprite closeSprite; // 閉じている状態（ゲーム中）

    private SpriteRenderer spriteRenderer;
    private LotteryGameManager gameManager;
    private int chestIndex;
    private bool isOpened = true; // 初期状態は開いている

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        // 初期状態では開いたスプライトを表示
        if (openSprite != null)
        {
            spriteRenderer.sprite = openSprite;
            this.tag = GameConstants.UNTAGGED_TAG_NAME;
        }
    }

    /// <summary>
    /// マネージャーによって初期化される
    /// </summary>
    public void Initialize(LotteryGameManager manager, int index)
    {
        this.gameManager = manager;
        this.chestIndex = index;
    }

    /// <summary>
    /// 宝箱を閉じてゲーム準備完了状態にする
    /// </summary>
    public void ResetToClose()
    {
        isOpened = false;
        if (closeSprite != null)
            spriteRenderer.sprite = closeSprite;
        this.tag = GameConstants.INTERACTABLE_OBJECT_TAG_NAME;
    }

    /// <summary>
    /// 宝箱を開く（見た目の変更のみ）
    /// </summary>
    public void OpenVisual()
    {
        isOpened = true;
        if (openSprite != null)
            spriteRenderer.sprite = openSprite;
        this.tag = GameConstants.UNTAGGED_TAG_NAME;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // ゲーム中でない、または既に開いている場合は無視
        if (Time.timeScale > 0 && !isOpened && gameManager != null)
        {
            // プレイヤーがインタラクトボタンを押した場合
            if (
                InputManager.instance.GetInteract()
                && collision.CompareTag(GameConstants.PLAYER_TAG_NAME)
            )
            {
                // マネージャーに「この箱が選ばれた」と通知
                gameManager.OnChestSelected(chestIndex);
            }
        }
    }
}
