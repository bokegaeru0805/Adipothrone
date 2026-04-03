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
/// 1. Rigidbody2D: Body Typeは「Kinematic」推奨。「Dynamic」だと自動配置が正しく動作しません。
/// 2. Pivot設定: アイテムは「Center」、宝箱は「Bottom」推奨。
///
/// ■ 注意事項:
/// このスクリプトは、宝箱(isTreasureBox = true)の場合は地面への自動配置を行いません。
///
/// ■ ObjectPoolerの設計方針について (重要):
/// このオブジェクトは `ObjectPooler.SceneInstance`（シーン固有プール）で管理します。
/// 理由は以下の通りです：
/// 1. 【メモリ効率】: シーンによってドロップ数が大きく異なるため、永続化せずシーン終了時にメモリを解放するため。
/// 2. 【親子関係の整合性】: 生成時に `EnemyActivator`（シーンオブジェクト）の子として設定されるため、
///    シーン遷移時に親と一緒に破棄されないと、参照エラー（MissingReference）の原因になるため。
/// 3. 【状態リセット】: 宝箱化など状態変化が激しいため、シーン遷移で確実にリセットし、バグの持ち越しを防ぐため。
/// </remarks>
public class DropItem : PoolableObject
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

    [Header("ドロップアニメーション設定")]
    [Tooltip("ドロップ時に上方向に加える力の強さ")]
    [SerializeField]
    private float dropInitialUpForce = 5f;
    private float groundCheckRaycastDistance = 5f; //地面を探すために真下に飛ばすRaycastの最大距離
    private int TreasuresortingOrder = 20;
    private int CoinsortingOrder = 30;
    private int DropItemsortingOrder = 40;
    private SpriteRenderer spriteRenderer;
    private CircleCollider2D mycollider;
    private CapsuleCollider2D groundCheckerCollider;
    private Rigidbody2D rbody;
    private Animator animator;

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
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// 変数やコンポーネントの状態を初期化します。
    /// </summary>
    private void ResetState()
    {
        isTreasureBox = false;
        DropMoney = 0;
        DropID = null;

        // 宝箱化によって変更されたタグを元に戻す
        this.tag = GameConstants.UNTAGGED_TAG_NAME;

        // 物理挙動のリセット（地面判定ロジックが走るまではKinematicで静止させておく）
        if (rbody != null)
        {
            rbody.bodyType = RigidbodyType2D.Kinematic;
            rbody.velocity = Vector2.zero;
        }

        // コライダーサイズを初期値に戻す
        if (mycollider != null)
        {
            mycollider.radius = originalColliderSize / 2;
        }

        // スケールはObjectPooler側で初期値(prefabの状態)に戻されるが、
        // SetDropItemSpriteで変更される可能性があるため、必要ならここでも戻す
    }

    /// <summary>
    /// ドロップアイテムのスプライトを設定します。
    /// </summary>
    public void SetDropItemSprite()
    {
        if (animator != null)
        {
            animator.enabled = false; // ドロップアイテムのアニメーションは基本的に不要なので無効化
        }

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

    /// <summary>
    /// ドロップするお金のスプライトを設定します。
    /// </summary>
    public void SetMoneySprite()
    {
        if (animator != null)
        {
            animator.enabled = true; // ドロップ時のコインアニメーションを有効化
        }

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
        if (animator != null)
        {
            animator.enabled = false; // 宝箱のアニメーションは基本的に不要なので無効化
        }

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
        this.tag = GameConstants.INTERACTABLE_OBJECT_TAG_NAME; //タグを変更
        mycollider.radius = originalTreasureColliderRadius; //当たり判定のcolliderの半径を調整
        groundCheckerCollider.offset = new Vector2(0, GroundCheckerColliderOffsetY); //地面当たり判定のcolliderのoffsetを調整
        // //  宝箱は手動で配置するため、座標調整は行わない

        // 宝箱も地面に配置、または落下させるために座標調整を呼び出す
        AdjustPositionToGroundSurface();
    }

    /// <summary>
    /// オブジェクトの登場処理。
    /// 地面に埋まっていれば表面にスナップし、空中にいればドロップ（落下）させる。
    /// </summary>
    private void AdjustPositionToGroundSurface()
    {
        // レイヤーが未設定の場合は何もしない
        if (groundLayer.value == 0)
        {
            // 安全のためKinematicにしてその場でホバー（宝箱以外）
            rbody.bodyType = RigidbodyType2D.Kinematic;
            rbody.velocity = Vector2.zero;
            StartHoverAnimation();
            return;
        }

        // --- 1. まず、現在地が地面に埋まっているか（または接しているか）をチェック ---

        float checkRayDistance;
        Vector2 checkRayStart = transform.position;

        // Pivotの位置に応じて「埋まり判定」のRayの長さを変える
        if (isTreasureBox)
        {
            // 宝箱 (Pivot: Bottom) の場合：
            // 原点（下端）からごくわずか下（0.1f）にRayを撃つ
            checkRayDistance = 0.1f;
        }
        else
        {
            // アイテム/お金 (Pivot: Center) の場合：
            // スプライトの高さの半分（中心から下端までの距離）を取得
            float halfHeight = spriteRenderer.bounds.extents.y;
            checkRayDistance = halfHeight + 0.1f; // 中心から下端+αまで
        }

        // オブジェクトの中心（または下端）から、スプライトの下端より少し下まで短いRayを撃つ
        RaycastHit2D checkHit = Physics2D.Raycast(
            checkRayStart,
            Vector2.down,
            checkRayDistance,
            groundLayer
        );

        // --- 2. 判定に応じて処理を分岐 ---

        if (checkHit.collider != null)
        {
            // 【ケースA: 地面に埋まっている（または接している）場合】
            // 即座に地面の表面にスナップさせる

            // Raycastの開始位置を、オブジェクトの現在位置より少し高い場所（上空）に設定する
            Vector2 rayStartPosition = new Vector2(transform.position.x, transform.position.y + 1f);

            // 上空から真下に向けて地面を探すRaycastを発射
            RaycastHit2D snapHit = Physics2D.Raycast(
                rayStartPosition,
                Vector2.down,
                groundCheckRaycastDistance, // 5fなどの十分な距離
                groundLayer
            );

            if (snapHit.collider != null)
            {
                // 物理演算を停止し、手動で座標を制御できるようにする
                rbody.bodyType = RigidbodyType2D.Kinematic;
                rbody.velocity = Vector2.zero; // 念のため速度をリセット

                // Pivotの位置に応じて、スナップさせるY座標を変える
                float newY;
                if (isTreasureBox)
                {
                    // 宝箱 (Pivot: Bottom) の場合：
                    // オブジェクトの原点（＝スプライトの下端）を地面の接触点に合わせる
                    newY = snapHit.point.y;
                }
                else
                {
                    // アイテム/お金 (Pivot: Center) の場合：
                    // スプライトの高さの半分（中心から下端までの距離）を取得
                    float halfHeight = spriteRenderer.bounds.extents.y;
                    // 新しいY座標を計算 = 地面の接触点 ＋ スプライトの高さの半分
                    newY = snapHit.point.y + halfHeight;
                }

                // 新しい座標を設定
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);

                // ホバーアニメーションは宝箱以外で開始
                StartHoverAnimation();
            }
            else
            {
                // 埋まっているが真下に地面が見つからない（崖の端など）レアケース
                // 念のためDynamicにして落下させる
                rbody.bodyType = RigidbodyType2D.Dynamic;
            }
        }
        else
        {
            // 【ケースB: 空中にいる場合】
            // ピョンと跳ねるドロップ処理を実行する

            // 1. 重力で落下させるために物理演算を有効にする
            rbody.bodyType = RigidbodyType2D.Dynamic;

            // 2. 上方向の初速（Impulse）を与える
            Vector2 initialForce = new Vector2(0, dropInitialUpForce);
            rbody.velocity = Vector2.zero; // 既存の速度をリセット
            rbody.AddForce(initialForce, ForceMode2D.Impulse);

            // 3. ホバーアニメーションは OnCollisionEnter2D で着地時に呼ばれるのを待つ
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
        if (Time.timeScale > 0 && this.CompareTag(GameConstants.INTERACTABLE_OBJECT_TAG_NAME))
        {
            //プレイヤーの所得動作との兼合いで、Tagで判断する
            if (
                InputManager.instance.GetInteract()
                && collision.CompareTag(GameConstants.PLAYER_TAG_NAME)
            )
            {
                ForceAcquire(); // アイテム獲得処理を実行する
            }
        }
    }

    /// <summary>
    /// アイテム獲得処理を強制的に実行する
    /// </summary>
    public void ForceAcquire()
    {
        var treasureData = GameManager.instance.savedata.TreasureData;
        if (treasureData == null)
        {
            Debug.LogWarning("宝箱に関するセーブデータが存在しません");
            return;
        }

        // 既に開いている(Untagged)かつ、スプライトが表示されているなら二重取得防止
        if (
            this.tag == GameConstants.UNTAGGED_TAG_NAME
            && spriteRenderer.sprite == _currentTargetOpenSprite
        )
            return;

        var baseItemData = ItemDataManager.instance.GetBaseItemDataByID(DropID);
        this.tag = GameConstants.UNTAGGED_TAG_NAME;

        if (_currentTargetOpenSprite != null)
            spriteRenderer.sprite = _currentTargetOpenSprite;

        SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.ItemGet2);
        GameManager.instance.TreasureFungus(baseItemData, 1);
        //インベントリにアイテムを保存はFungusのFlowchartで行います
        // GameManager.instance.AddAllTypeIDToInventory(DropID); //インベントにアイテムを保存
    }

    /// <summary>
    /// スプライトを表示せず、即座に獲得処理を行ってプールに戻る
    /// （ユニークアイテムのロスト防止用）
    /// </summary>
    public void AcquireInstantly()
    {
        // 1. 獲得処理を実行
        ForceAcquire();

        // 2. 即座にプールへ返却
        ReturnToPool();
    }

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
            StartHoverAnimation(); // 着地後にホバーアニメーションを開始
        }
    }

    private void OnEnable()
    {
        // プールから取り出された際に状態をリセットする
        ResetState();
    }

    private void OnDisable()
    {
        // プールに戻る際（または非アクティブ化時）にアニメーションを確実に停止する
        // これを忘れると、次に取り出した時に変な位置に飛んだりする
        transform.DOKill();
    }
}
