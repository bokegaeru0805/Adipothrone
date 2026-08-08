using UnityEngine;

/// <summary>
/// 順番押しギミックの個別ボタン。攻撃判定と表示更新を担当します。
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class SequentialButton : MonoBehaviour
{
    private SequentialButtonGroupController controller;
    private Sprite offSprite;
    private Sprite onSprite;
    private SpriteRenderer spriteRenderer;
    private Collider2D buttonCollider;
    private LayerMask groundLayer;
    private bool isPushed;
    private bool isStopped;
    private bool preventWallPenetration;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        buttonCollider = GetComponent<Collider2D>();
        groundLayer = LayerMask.GetMask(GameConstants.PHYSICS_LAYER_NAME_GROUND);

        if (controller != null)
            UpdateVisual();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isStopped || !other.CompareTag(GameConstants.PLAYER_ATTACK_TAG_NAME))
            return;

        if (preventWallPenetration && IsBlockedByWall())
            return;

        controller?.NotifyButtonPressed(this);
    }

    /// <summary>
    /// 親コントローラーから参照と表示設定を受け取ります。
    /// </summary>
    public void Configure(
        SequentialButtonGroupController owner,
        Sprite configuredOffSprite,
        Sprite configuredOnSprite,
        bool shouldPreventWallPenetration,
        bool shouldUpdateVisual
    )
    {
        controller = owner;
        offSprite = configuredOffSprite;
        onSprite = configuredOnSprite;
        preventWallPenetration = shouldPreventWallPenetration;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (shouldUpdateVisual && !isStopped)
            UpdateVisual();
    }

    public void SetPushed(bool value)
    {
        isStopped = false;
        isPushed = value;
        UpdateVisual();
    }

    public void SetStopped(Sprite stoppedSprite)
    {
        isStopped = true;
        isPushed = false;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            spriteRenderer.sprite = stoppedSprite;
    }

    private void UpdateVisual()
    {
        if (spriteRenderer != null)
            spriteRenderer.sprite = isPushed ? onSprite : offSprite;
    }

    private bool IsBlockedByWall()
    {
        if (PlayerManager.instance == null || buttonCollider == null)
            return false;

        Vector2 playerCenter =
            PlayerManager.instance.GetPlayerPosition()
            + new Vector2(0f, GameConstants.PLAYER_BASE_HEIGHT / 2f);
        Vector2 buttonCenter = buttonCollider.bounds.center;
        RaycastHit2D hit = Physics2D.Linecast(playerCenter, buttonCenter, groundLayer);
        return hit.collider != null;
    }
}
