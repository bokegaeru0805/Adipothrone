using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// アイテムランクに応じた宝箱のスプライト（開閉）を管理するクラス
/// </summary>
[System.Serializable]
public class TreasureSpriteSet
{
    public ItemRank rank; // 対応するアイテムランク
    public Sprite closeSprite; // 閉じている状態のスプライト
    public Sprite openSprite; // 開いている状態のスプライト
}

/// <summary>
/// ドロップアイテム、お金、宝箱の表示と基本的な動作を管理するクラス。
/// アイテムの種類に応じてスプライトを設定し、地面への自動配置やホバーアニメーション、宝箱の開閉処理などを担当します。
/// </summary>
/// <remarks>
/// ■ 前提条件:
/// 1. Rigidbody2D: このコンポーネントがアタッチされている場合、Body Typeは「Kinematic」に設定してください。
///    「Dynamic」だと物理演算が働き、自動配置やアニメーションが正しく動作しません。
/// 2. Pivot設定:
///    - 自動配置されるアイテムやお金のスプライト: Pivotは「Center」を想定しています。
///    - 手動配置される宝箱のスプライト: Pivotは「Bottom」に設定することを推奨します。
///
/// ■ 注意事項:
/// このスクリプトは、宝箱(isTreasureBox = true)の場合は地面への自動配置を行いません。
/// 宝箱はシーンに直接、手動で配置されることを前提としています。
/// </remarks>
public class DropItem : MonoBehaviour
{
    private float maxUnitPixel = 2.0f; //スプライトの最大表示サイズ（Unity単位）
    private float originalColliderSize = 2.0f; //元のColliderサイズ（固定）
    private float originalTreasureColliderRadius = 1f; //宝箱のColliderの半径（固定）
    private float GroundCheckerColliderOffsetY = 0f; //地面判定のcolliderのy座標のoffset (固定)

    [HideInInspector]
    public Enum DropID;

    [HideInInspector]
    public int DropMoney = 0;

    [HideInInspector]
    public bool isTreasureBox = false;

    [Header("宝箱のスプライト設定")]
    [Tooltip("アイテムランクごとの宝箱の開閉スプライトを設定します")]
    [SerializeField]
    private List<TreasureSpriteSet> treasureSpritesByRank;

    [Tooltip("どのランクにも一致しない場合の、デフォルトの『閉じている』宝箱スプライト")]
    [SerializeField]
    private Sprite defaultCloseSprite;

    [Tooltip("どのランクにも一致しない場合の、デフォルトの『開いている』宝箱スプライト")]
    [SerializeField]
    private Sprite defaultOpenSprite;

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
    private float groundCheckRaycastDistance = 5f; //地面を探すために真下に飛ばすRaycastの最大距離
    private int TreasuresortingOrder = 20;
    private int CoinsortingOrder = 30;
    private int DropItemsortingOrder = 40;
    private SpriteRenderer spriteRenderer;
    private CircleCollider2D mycollider;
    private CapsuleCollider2D groundCheckerCollider;
    private Rigidbody2D rbody;

    // 現在の宝箱に適用すべき開閉スプライトを保存しておく変数
    private Sprite _currentTargetCloseSprite;
    private Sprite _currentTargetOpenSprite;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        mycollider = this.gameObject.GetComponent<CircleCollider2D>();
        groundCheckerCollider = this
            .gameObject.transform.GetChild(0)
            .gameObject.GetComponent<CapsuleCollider2D>();
        rbody = GetComponent<Rigidbody2D>();
    }

    public void SetDropItemSprite()
    {
        Sprite dropSprite = ItemDataManager.instance.GetItemSpriteByID(DropID); // アイテムの見た目（スプライト）を取得
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

    /// <summary>
    /// オブジェクトを宝箱として設定します。
    /// 画像、表示順、タグ、および当たり判定（コライダー）の半径とオフセットを変更し、宝箱の状態に初期化します。
    /// このメソッドは、DropIDが設定された後に呼び出されることを想定しています。
    /// </summary>
    public void SetTreasureSprite()
    {
        isTreasureBox = true; //宝箱かどうかのフラグをON
        ItemRank itemRank = ItemDataManager.instance.GetItemRankByID(DropID); //アイテムのランクを取得

        // 1. まず、デフォルトのスプライトを変数に設定
        _currentTargetCloseSprite = defaultCloseSprite;
        _currentTargetOpenSprite = defaultOpenSprite;

        // 2. リストの中から、現在のアイテムランクに一致するスプライト設定を探す
        foreach (var spriteSet in treasureSpritesByRank)
        {
            if (spriteSet.rank == itemRank)
            {
                // 一致するものが見つかったら、変数の内容を上書き
                _currentTargetCloseSprite = spriteSet.closeSprite;
                _currentTargetOpenSprite = spriteSet.openSprite;
                break;
            }
        }

        // 3. 保存しておいた「閉じている」スプライトを初期表示として適用する
        spriteRenderer.sprite = _currentTargetCloseSprite;

        spriteRenderer.sortingOrder = TreasuresortingOrder; //画像の表示順を設定
        this.tag = GameConstants.InteractableObjectTagName; //タグを変更
        mycollider.radius = originalTreasureColliderRadius; //当たり判定のcolliderの半径を調整
        groundCheckerCollider.offset = new Vector2(0, GroundCheckerColliderOffsetY); //地面当たり判定のcolliderのoffsetを調整
        // //  宝箱は手動で配置するため、座標調整は行わない

        // 宝箱も地面に配置、または落下させるために座標調整を呼び出す
        AdjustPositionToGroundSurface();
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

        // Raycastの開始位置を、オブジェクトの現在位置より少し高い場所（上空）に設定する
        Vector2 rayStartPosition = new Vector2(transform.position.x, transform.position.y + 5f);

        // 上空から真下に向けてRaycastを発射（距離を長めに設定）
        RaycastHit2D hit = Physics2D.Raycast(
            rayStartPosition,
            Vector2.down,
            groundCheckRaycastDistance,
            groundLayer
        );

        // Rayが地面レイヤーに衝突した場合
        if (hit.collider != null)
        {
            // 物理演算を停止し、手動で座標を制御できるようにする
            rbody.bodyType = RigidbodyType2D.Kinematic;
            rbody.velocity = Vector2.zero; // 念のため速度をリセット

            // スプライトの高さの半分（中心から下端までの距離）を取得
            float halfHeight = spriteRenderer.bounds.extents.y;

            // 新しいY座標を計算 = 地面の接触点 ＋ スプライトの高さの半分
            float newY = hit.point.y + halfHeight;

            // 新しい座標を設定
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            // ホバーアニメーションは宝箱以外で開始
            StartHoverAnimation();
        }
        else
        {
            // 重力で落下させるために物理演算を有効にする
            rbody.bodyType = RigidbodyType2D.Dynamic;
        }
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

                var baseItemData = ItemDataManager.instance.GetBaseItemDataByID(DropID); //アイテムのデータを取得
                this.tag = "Untagged"; //tagを外す
                spriteRenderer.sprite = _currentTargetOpenSprite; //予め保存しておいた「開いた」スプライトに変更
                SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.ItemGet2); //効果音を鳴らす
                GameManager.instance.TreasureFungus(baseItemData, 1); //Fungusを起動
            }
        }
    }

    /// <summary>
    /// オブジェクトが他のコライダーと衝突したときに呼び出される
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 落下中(Dynamic)でなければ何もしない
        if (rbody == null || rbody.bodyType != RigidbodyType2D.Dynamic)
        {
            return;
        }

        // 衝突した相手が地面レイヤーかどうかを判定
        if ((groundLayer.value & (1 << collision.gameObject.layer)) > 0)
        {
            // 地面に着地したら、物理演算を停止してその場に固定する
            rbody.bodyType = RigidbodyType2D.Kinematic;
            rbody.velocity = Vector2.zero; // 完全に静止させる
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
