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
/// スキルカテゴリーに応じたスプライトを管理するクラス
/// </summary>
[System.Serializable]
public class SkillSpriteSet
{
    public SkillCategory category; // スキルカテゴリー
    public Sprite skillSprite; // スキルのスプライト
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
    #region 定数・変数設定

    // --- 固定パラメータ ---
    private float maxUnitPixel = 2.0f; // スプライトの最大表示サイズ（Unity単位）
    private float originalColliderSize = 2.0f; // 元のColliderサイズ（固定）
    private float originalTreasureColliderRadius = 1f; // 宝箱のColliderの半径（固定）
    private float GroundCheckerColliderOffsetY = 0f; // 地面判定のcolliderのy座標のoffset (固定)
    private float groundCheckRaycastDistance = 5f; // 地面を探すために真下に飛ばすRaycastの最大距離
    private int TreasuresortingOrder = 20; // 宝箱の表示順
    private int CoinsortingOrder = 30; // コインの表示順
    private int DropItemsortingOrder = 40; // ドロップアイテムの表示順

    // --- 外部公開プロパティ・変数 ---
    [HideInInspector]
    public Enum DropID;

    [HideInInspector]
    public int DropMoney = 0;

    [HideInInspector]
    public bool isTreasureBox = false;

    [HideInInspector]
    public bool isSkillDrop = false; // スキルドロップかどうかの判定フラグ

    [HideInInspector]
    public bool isSkillCrystalDrop = false;

    [HideInInspector]
    public SkillName DropSkillID; // ドロップするスキルID

    [HideInInspector]
    public EnemyName DropSourceEnemyID = EnemyName.None;

    // --- インスペクター設定 ---
    [Header("宝箱のスプライト設定")]
    [Tooltip("アイテムランクごとの宝箱の開閉スプライトを設定します")]
    [SerializeField]
    private List<TreasureSpriteSet> treasureSpritesByRank;

    [Header("スキルドロップのスプライト設定")]
    [Tooltip("スキルカテゴリーごとのスプライトを設定します")]
    [SerializeField]
    private List<SkillSpriteSet> skillSpritesByCategory;

    [Tooltip("どのランクにも一致しない場合の、デフォルトの『閉じている』宝箱スプライト")]
    [SerializeField]
    private Sprite defaultCloseSprite;

    [Tooltip("どのランクにも一致しない場合の、デフォルトの『開いている』宝箱スプライト")]
    [SerializeField]
    private Sprite defaultOpenSprite;

    [Header("エフェクト設定")]
    [Tooltip("スキルドロップ時に表示する子オブジェクトのエフェクト")]
    [SerializeField]
    private GameObject skillEffectObject;

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

    [Header("自動回収設定")]
    [Tooltip("AutoCoinCollect装備時に、敵ドロップを引き寄せ始める距離")]
    [SerializeField]
    private float autoCollectDistance = 8f;

    [Tooltip("AutoCoinCollect装備時に、敵ドロップを引き寄せる速度")]
    [SerializeField]
    private float autoCollectSpeed = 8f;

    // --- 内部コンポーネント参照 ---
    private SpriteRenderer spriteRenderer;
    private CircleCollider2D mycollider;
    private CapsuleCollider2D groundCheckerCollider;
    private Rigidbody2D rbody;
    private Animator animator;

    // --- 自動回収状態 ---
    private bool isEnemyAutoCollectTarget;
    private bool isReadyForAutoCollect;
    private bool isAutoCollecting;
    private Transform autoCollectTarget;

    // --- 宝箱用スプライトキャッシュ ---
    // 現在の宝箱に適用すべき開閉スプライトを保存しておく変数
    private Sprite _currentTargetCloseSprite;
    private Sprite _currentTargetOpenSprite;

    #endregion

    #region Unityライフサイクル

    /// <summary>
    /// コンポーネントの初期化を行います。必要なコンポーネントをキャッシュします。
    /// </summary>
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
    /// プールから取り出された際に状態をリセットします。
    /// </summary>
    private void OnEnable()
    {
        ResetState();
    }

    /// <summary>
    /// プールに戻る際（または非アクティブ化時）にアニメーションを停止し、バグを防ぎます。
    /// </summary>
    private void OnDisable()
    {
        // 確実にTweenを停止しないと、次に取り出した時に異常な座標移動が起きる可能性があります
        transform.DOKill();
    }

    /// <summary>
    /// 着地済みの敵ドロップを、AutoCoinCollect装備中のプレイヤーへ移動させます。
    /// </summary>
    private void FixedUpdate()
    {
        if (!CanAutoCollect())
        {
            StopAutoCollect();
            return;
        }

        Vector2 currentPosition = rbody.position;
        Vector2 targetPosition = autoCollectTarget.position;
        float autoCollectDistanceSqr = autoCollectDistance * autoCollectDistance;
        if ((targetPosition - currentPosition).sqrMagnitude > autoCollectDistanceSqr)
        {
            StopAutoCollect();
            return;
        }

        if (!isAutoCollecting)
        {
            transform.DOKill();
            rbody.bodyType = RigidbodyType2D.Kinematic;
            rbody.velocity = Vector2.zero;
            isAutoCollecting = true;
        }

        Vector2 nextPosition = Vector2.MoveTowards(
            currentPosition,
            targetPosition,
            autoCollectSpeed * Time.fixedDeltaTime
        );
        rbody.MovePosition(nextPosition);
    }

    #endregion

    #region 初期化・リセット処理

    /// <summary>
    /// オブジェクトの状態（変数やコンポーネントの設定）を初期化・リセットします。
    /// </summary>
    private void ResetState()
    {
        isTreasureBox = false;
        isSkillDrop = false;
        isSkillCrystalDrop = false;
        DropMoney = 0;
        DropID = null;
        DropSkillID = SkillName.None;
        DropSourceEnemyID = EnemyName.None;
        isEnemyAutoCollectTarget = false;
        isReadyForAutoCollect = false;
        isAutoCollecting = false;
        autoCollectTarget = null;

        // 宝箱化によって変更されたタグを元に戻す
        this.tag = GameConstants.UNTAGGED_TAG_NAME;

        // スキル用エフェクトを非表示にリセット
        if (skillEffectObject != null)
        {
            skillEffectObject.SetActive(false);
        }

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

    #endregion

    #region スプライト・種別設定処理

    /// <summary>
    /// このオブジェクトをAutoCoinCollectの対象となる敵ドロップとして設定します。
    /// </summary>
    public void SetAsEnemyAutoCollectTarget()
    {
        isEnemyAutoCollectTarget = true;
    }

    /// <summary>
    /// ドロップアイテムとしてのスプライトを設定し、配置を調整します。
    /// </summary>
    public void SetDropItemSprite()
    {
        if (animator != null)
        {
            animator.enabled = false; // ドロップアイテムのアニメーションは基本的に不要なので無効化
        }

        Sprite dropSprite = ItemDataManager.instance.GetItemSpriteByID(DropID); // アイテムの見た目（スプライト）を取得
        spriteRenderer.sprite = dropSprite; // スプライトを設定
        spriteRenderer.sortingOrder = DropItemsortingOrder; // 画像の表示順を設定

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
    /// お金（コイン）としてのスプライト・アニメーションを設定し、配置を調整します。
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
        spriteRenderer.sortingOrder = CoinsortingOrder; // 画像の表示順を設定

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

        isTreasureBox = true; // 宝箱かどうかのフラグをON
        ItemRank itemRank = ItemDataManager.instance.GetItemRankByID(DropID); // アイテムのランクを取得

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

        spriteRenderer.sortingOrder = TreasuresortingOrder; // 画像の表示順を設定
        this.tag = GameConstants.INTERACTABLE_OBJECT_TAG_NAME; // タグを変更
        mycollider.radius = originalTreasureColliderRadius; // 当たり判定のcolliderの半径を調整
        groundCheckerCollider.offset = new Vector2(0, GroundCheckerColliderOffsetY); // 地面当たり判定のcolliderのoffsetを調整

        // 宝箱も地面に配置、または落下させるために座標調整を呼び出す
        AdjustPositionToGroundSurface();
    }

    /// <summary>
    /// ドロップスキルとしてのスプライトを設定し、配置を調整します。
    /// </summary>
    /// <param name="skillID">設定するスキルID</param>
    public void SetSkillSprite(SkillName skillID)
    {
        if (animator != null)
        {
            animator.enabled = false; // ドロップアイテムのアニメーションは基本的に不要なので無効化
        }

        isSkillDrop = true;
        DropSkillID = skillID;

        // スキル用エフェクトを表示する
        if (skillEffectObject != null)
        {
            skillEffectObject.SetActive(true);
        }

        // SkillManagerからデータベース経由でカテゴリーを取得
        SkillCategory category = SkillManager.instance.GetSkillCategory(skillID);
        Sprite skillSprite = null;

        foreach (var spriteSet in skillSpritesByCategory)
        {
            if (spriteSet.category == category)
            {
                skillSprite = spriteSet.skillSprite;
                break;
            }
        }

        if (skillSprite != null)
        {
            spriteRenderer.sprite = skillSprite;
        }
        else
        {
            Debug.LogWarning(
                $"カテゴリー {category} に対応するスキルのスプライトが設定されていません"
            );
        }

        spriteRenderer.sortingOrder = DropItemsortingOrder; // 画像の表示順を設定

        // Colliderサイズを元のサイズに戻す
        if (mycollider != null)
        {
            mycollider.radius = originalColliderSize / 2;
        }

        // スプライト設定後に座標を調整（通常のドロップアイテムと同じ軌道に乗せる）
        AdjustPositionToGroundSurface();
    }

    /// <summary>
    /// このオブジェクトをスキルクリスタルとして設定します。
    /// </summary>
    public void SetSkillCrystalSprite(EnemyName sourceEnemyID)
    {
        isSkillCrystalDrop = true;
        DropSourceEnemyID = sourceEnemyID;

        if (skillEffectObject != null)
        {
            skillEffectObject.SetActive(false);
        }

        if (animator != null)
        {
            animator.enabled = true;
            animator.SetTrigger("SkillCrystalTrigger");
        }

        spriteRenderer.sortingOrder = CoinsortingOrder;

        if (mycollider != null)
        {
            mycollider.radius = originalColliderSize / 2;
        }

        AdjustPositionToGroundSurface();
    }

    #endregion

    #region 配置・アニメーション処理

    /// <summary>
    /// オブジェクトの登場処理。
    /// 地面に埋まっていれば表面にスナップし、空中にいればドロップ（落下）させます。
    /// </summary>
    private void AdjustPositionToGroundSurface()
    {
        // レイヤーが未設定の場合は何もしない
        if (groundLayer.value == 0)
        {
            // 安全のためKinematicにしてその場でホバー（宝箱以外）
            rbody.bodyType = RigidbodyType2D.Kinematic;
            rbody.velocity = Vector2.zero;
            isReadyForAutoCollect = true;
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
                isReadyForAutoCollect = true;

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

        transform.DOKill();

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

    /// <summary>
    /// 現在の状態で自動回収できるか判定し、必要ならプレイヤー参照を取得します。
    /// </summary>
    private bool CanAutoCollect()
    {
        if (
            !isEnemyAutoCollectTarget
            || !isReadyForAutoCollect
            || isTreasureBox
            || isSkillDrop
            || rbody == null
            || SkillManager.instance == null
            || !SkillManager.instance.IsSkillActive(SkillName.AutoDropItemCollect)
        )
        {
            return false;
        }

        if (autoCollectTarget == null)
        {
            GameObject playerObject = PlayerManager.instance?.PlayerGameObject;
            Heroin_move heroinMove = playerObject?.GetComponent<Heroin_move>();
            autoCollectTarget = heroinMove != null ? heroinMove.transform : null;
        }

        return autoCollectTarget != null;
    }

    /// <summary>
    /// 自動回収条件から外れた場合に移動を止め、通常のホバーへ戻します。
    /// </summary>
    private void StopAutoCollect()
    {
        if (!isAutoCollecting)
        {
            return;
        }

        isAutoCollecting = false;
        rbody.velocity = Vector2.zero;
        StartHoverAnimation();
    }

    #endregion

    #region 物理判定・獲得処理

    /// <summary>
    /// 落下後に地面と衝突した際の処理を行います。
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
            isReadyForAutoCollect = true;
            StartHoverAnimation(); // 着地後にホバーアニメーションを開始
        }
    }

    /// <summary>
    /// プレイヤーがインタラクト範囲内にいる時の処理（宝箱・アイテムの取得判定）を行います。
    /// </summary>
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (Time.timeScale > 0 && this.CompareTag(GameConstants.INTERACTABLE_OBJECT_TAG_NAME))
        {
            // プレイヤーの所得動作との兼合いで、Tagで判断する
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
    /// アイテム獲得処理を強制的に実行します。
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

        // インベントリにアイテムを保存はFungusのFlowchartで行います
        // GameManager.instance.AddAllTypeIDToInventory(DropID); //インベントにアイテムを保存
    }

    /// <summary>
    /// スプライトを表示せず、即座に獲得処理を行ってプールに戻ります。
    /// （ユニークアイテムのロスト防止用）
    /// </summary>
    public void AcquireInstantly()
    {
        // 1. 獲得処理を実行
        ForceAcquire();

        // 2. 即座にプールへ返却
        ReturnToPool();
    }

    #endregion
}
