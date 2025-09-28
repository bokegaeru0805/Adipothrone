using System;
using DG.Tweening;
using UnityEngine;

public class DropItem : MonoBehaviour
{
    private float maxUnitPixel = 2.0f; //スプライトの最大表示サイズ（Unity単位）
    private float originalColliderSize = 2.0f; //元のColliderサイズ（固定）
    private float originalTreasureColliderRadius = 1f; //宝箱のColliderの半径（固定）
    private float TreasureColliderOffsetY = 1f; //宝箱のColliderのy座標のoffset（固定）
    private float GroundCheckerColliderOffsetY = 0f; //地面判定のcolliderのy座標のoffset (固定)

    [HideInInspector]
    public Enum DropID;

    [HideInInspector]
    public int DropMoney = 0;

    [HideInInspector]
    public bool isTreasureBox = false;

    [SerializeField]
    private Sprite closesprite; //開いている状態のスプライト

    [SerializeField]
    private Sprite opensprite; //開いている状態のスプライト

    [Header("地面への自動配置設定")]
    [Tooltip("地面として判定するレイヤー")]
    [SerializeField]
    private LayerMask groundLayer;

    [Header("ホバーアニメーション設定")]
    [Tooltip("揺れの高さの倍率。実際の揺れ幅は「この値 × スプライトの高さ」になります。")]
    [SerializeField]
    [Range(0f, 1f)]
    private float hoverHeightMultiplier = 0.2f;

    [Tooltip("揺れアニメーションの片道にかかる時間（秒）")]
    [SerializeField]
    private float hoverDuration = 1.5f;
    private int TreasuresortingOrder = 20;
    private int CoinsortingOrder = 30;
    private int DropItemsortingOrder = 40;
    private SpriteRenderer spriteRenderer;
    private CircleCollider2D mycollider;
    private CapsuleCollider2D groundCheckerCollider;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        mycollider = this.gameObject.GetComponent<CircleCollider2D>();
        groundCheckerCollider = this
            .gameObject.transform.GetChild(0)
            .gameObject.GetComponent<CapsuleCollider2D>();
    }

    public void SetDropItemSprite()
    {
        Sprite dropSprite = GameManager.instance.GetAllTypeIDtoSprite(DropID); // アイテムの見た目（スプライト）を取得
        spriteRenderer.sprite = dropSprite; //スプライトを設定
        spriteRenderer.sortingOrder = DropItemsortingOrder; //画像の表示順を設定
        if (dropSprite != null)
        {
            // スプライトのサイズ（Unity単位）を取得
            float unitWidth = dropSprite.rect.width / dropSprite.pixelsPerUnit;
            float unitHeight = dropSprite.rect.height / dropSprite.pixelsPerUnit;
            float biggerUnit = Mathf.Max(unitWidth, unitHeight);

            // プレハブに指定された最大表示サイズを超えていれば縮小する
            if (maxUnitPixel < biggerUnit)
            {
                float scale = maxUnitPixel / biggerUnit;
                this.gameObject.transform.localScale = Vector2.one * scale;
            }

            // Colliderサイズを元のサイズに戻す
            if (mycollider != null)
            {
                mycollider.radius = originalColliderSize / 2;
            }
        }

        // スプライト設定後に座標を調整
        AdjustPositionToGroundSurface();
    }

    public void SetMoneySprite()
    {
        Animator animator = this.GetComponent<Animator>();
        animator.enabled = true; //アニメーションを有効化
        switch (DropMoney)
        {
            case 1:
                animator.SetTrigger("TriggerCopperCoin");
                break;
            case 10:
                animator.SetTrigger("TriggerSilverCoin");
                break;
            case 100:
                animator.SetTrigger("TriggerGoldCoin");
                break;
            default:
                Debug.LogWarning($"指定された{DropMoney}の金額のスプライトは存在しません");
                break;
        }
        spriteRenderer.sortingOrder = CoinsortingOrder; //画像の表示順を設定

        // アニメーション設定後に座標を調整
        AdjustPositionToGroundSurface();
    }

    public void SetTreasureSprite()
    {
        isTreasureBox = true; //宝箱かどうかのフラグをON
        spriteRenderer.sprite = closesprite; //画像を変更
        spriteRenderer.sortingOrder = TreasuresortingOrder; //画像の表示順を設定
        this.tag = GameConstants.InteractableObjectTagName; //タグを変更
        mycollider.radius = originalTreasureColliderRadius; //当たり判定のcolliderの半径を調整
        mycollider.offset = new Vector2(0, TreasureColliderOffsetY); //当たり判定のcolliderのoffsetを調整
        groundCheckerCollider.offset = new Vector2(0, GroundCheckerColliderOffsetY); //地面当たり判定のcolliderのoffsetを調整
        //  宝箱は手動で配置するため、座標調整は行わない
    }

    /// <summary>
    /// Raycastを使い、このオブジェクトを指定した地面レイヤーの表面に配置する
    /// </summary>
    private void AdjustPositionToGroundSurface()
    {
        // レイヤーが未設定の場合は何もしない
        if (groundLayer.value == 0)
        {
            return;
        }

        // オブジェクトの真下に向けてRaycastを発射
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 10f, groundLayer);

        // Rayが地面レイヤーに衝突した場合
        if (hit.collider != null)
        {
            // スプライトの高さの半分（中心から下端までの距離）を取得
            float halfHeight = spriteRenderer.bounds.extents.y;

            // 新しいY座標を計算 = 地面の接触点 ＋ スプライトの高さの半分
            float newY = hit.point.y + halfHeight;

            // 新しい座標を設定
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }

        //地面への配置が完了した後にアニメーションを開始
        StartHoverAnimation();
    }

    /// <summary>
    /// 宝箱以外の場合に、上下に揺れるアニメーションを開始します。
    /// </summary>
    private void StartHoverAnimation()
    {
        // アイテムが宝箱の場合は何もしない
        if (isTreasureBox)
        {
            return;
        }

        // 現在のY座標を基準点とする
        float startY = transform.position.y;

        // スプライトのワールド空間での実際の高さに基づいて、揺れ幅を計算
        float hoverAmount = spriteRenderer.bounds.size.y * hoverHeightMultiplier;

        // Y軸方向へ、計算した揺れ幅分を、指定した時間かけて移動し、ヨーヨーのように往復し続ける
        transform
            .DOMoveY(startY + hoverAmount, hoverDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (Time.timeScale > 0 && this.CompareTag(GameConstants.InteractableObjectTagName))
        {
            //プレイヤーの所得動作との兼合いで、Tagで判断する
            if (
                InputManager.instance.GetInteract()
                && collision.CompareTag(GameConstants.PlayerTagName)
            )
            {
                var treasureData = GameManager.instance.savedata.TreasureData;
                if (treasureData == null)
                {
                    Debug.LogWarning("宝箱に関するセーブデータが存在しません");
                    return;
                }

                //インベントリにアイテムを保存はFungusのFlowchartで行います
                // GameManager.instance.AddAllTypeIDToInventory(DropID); //インベントにアイテムを保存

                var baseItemData = GameManager.instance.GetBaseItemDataByID(DropID);
                this.tag = "Untagged"; //tagを外す
                spriteRenderer.sprite = opensprite; //spriteを変更
                SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.ItemGet2); //効果音を鳴らす
                GameManager.instance.TreasureFungus(baseItemData, 1); //Fungusを起動
            }
        }
    }

    /// <summary>
    /// このオブジェクトが破棄される際に、実行中のDOTweenアニメーションを停止します。
    /// </summary>
    private void OnDestroy()
    {
        // このTransformで実行中のすべてのアニメーションを安全に停止・破棄する
        transform.DOKill();
    }
}
