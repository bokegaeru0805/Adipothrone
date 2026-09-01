using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 章ボス（Chapter3）の移動および攻撃パターンを管理するコントローラークラスです。
/// </summary>
public class Chapter3BossMoveController : MonoBehaviour
{
    #region 攻撃仕様概要
    /*
     * ■ 共通フロー
     * 1. 攻撃開始時にCurrentStateを更新する。
     * 2. 構えアニメーションと攻撃位置への移動を行う。
     * 3. 対応するContactDamageControllerへ、その攻撃のダメージ量を設定する。
     * 4. 攻撃アニメーションを再生する。
     * 5. Chapter3BossAnimationEventRelayがAnimation Eventを受け取り、
     *    SetAttackDamageEnabled()を介して攻撃部位のTagを切り替える。
     *      - 攻撃判定中: DamageableEnemy
     *      - 攻撃判定外: Untagged
     * 6. 攻撃後の待機を経てIdleへ戻る。Idle移行時には全攻撃判定を無効化する。
     *
     * Collider2Dは原則として常時有効にし、ダメージの有効期間はTagで管理する。
     * ResetState、OnDisable、Idle移行時にも全攻撃判定を無効化し、
     * Animation Eventが中断された場合に判定が残らないようにする。
     *
     * ■ ① 通常攻撃 (Normal Attack)
     * - 下段攻撃用の高さへ移動し、構えの後にLowAttackSlashで攻撃する。
     * - 下段攻撃後、highAttackProbabilityの確率で上段攻撃へ派生する。
     * - 派生時は待機時間を挟み、HighAttackSlashで追撃する。
     * - 下段判定: lowAttackDamageController
     * - 上段判定: highAttackDamageController
     *
     * ■ ② 突き攻撃 (Thrust Attack)
     * - 剣先とプレイヤーの位置からボス本体の移動先を逆算し、前方へ突進する。
     * - プレイヤー位置を終点にせず、剣先とボス本体が指定距離だけ反対側へ通過する。
     * - 壁際と最小接近距離を考慮して移動先を補正する。
     * - 判定: thrustDamageController
     *
     * ■ ③ 射撃攻撃 (Shoot Attack)
     * - 構えた後、ランダムな高さオフセットから指定数の弾を連射する。
     * - 弾はObjectPoolerから取得し、弾側のContactDamageControllerへダメージを設定する。
     * - 本体の攻撃部位Tagは使用しない。
     *
     * ■ ④ 後退テレポート攻撃 (Retreat Teleport Attack)
     * - 空間の広い方向へ後退しながら複数回テレポートする。
     * - 中間地点で水平攻撃を行い、Animation EventのFireWindEffect()で風攻撃を発射する。
     * - 最初の中間地点でプレイヤーが背後にいる場合は、表示・攻撃せず最終地点へ移動する。
     * - 壁際では行動可能エリア内に収まるよう位置を補正する。
     * - 水平攻撃判定: horizontalAttackDamageController
     *
     * ■ ⑤ 突進コンボ (Rush Combo)
     * - High → Upper → Highの順で3連撃を行う。
     * - 壁までの移動可能距離を3分割し、各攻撃に合わせて段階的に前進する。
     * - 各打撃の開始時に対応する攻撃判定へ切り替える。
     * - 上段判定: highAttackDamageController
     * - 切り上げ判定: upperAttackDamageController
     *
     * ■ ⑥ 幻影強襲攻撃 (Mirage Assault)
     * - プレイヤー周辺へ複数回テレポートし、出現と消失を繰り返す。
     * - 最後の出現時にLow AttackまたはHorizontal Attackで攻撃する。
     * - 最終攻撃はプレイヤーの最大HPの30%を与える割合ダメージ。
     * - 下段判定: lowAttackDamageController
     * - 水平攻撃判定: horizontalAttackDamageController
     */
    #endregion

    #region 定数・列挙型
    private const string SHOOT_BULLET_POOLTAG = "Chapter3BossShoot";
    private const string POWER_UP_SKILL_NAME = "加速";
    private const float MIRAGE_ATTACK_MAX_HP_RATIO = 0.3f;

    /// <summary>
    /// ボスの現在の状態を表す列挙型
    /// </summary>
    public enum BossState
    {
        Intro, // 登場演出中
        Idle, // 待機中（下端からの特定座標をキープ）
        LowAttacking, // 下段攻撃中
        HighAttacking, // 上段攻撃中
        ThrustAttacking, // 突き攻撃中
        ShootAttacking, // 射撃攻撃中
        RetreatTeleporting, // 後退テレポート攻撃中
        RushComboAttacking, // 突進コンボ攻撃中
        MirageAssaultAttacking, // 幻影急襲攻撃中
        PoweringUp, // HPフェーズ移行時の強化演出中
    }

    private enum AttackPattern
    {
        NormalAttack = 0,
        ThrustAttack = 1,
        ShootAttack = 2,
        RetreatTeleportAttack = 3,
        RushCombo = 4,
        MirageAssault = 5,
    }

    private enum PlayerDistanceRange
    {
        Near = 0,
        Middle = 1,
        Far = 2,
    }

    private enum AfterImageSpeedTier
    {
        Slow = 0,
        Medium = 1,
        Fast = 2,
    }

    [System.Serializable]
    private class AttackWeightSettings
    {
        [Min(0f)]
        public float normalAttackWeight = 0f;

        [Min(0f)]
        public float thrustAttackWeight = 0f;

        [Min(0f)]
        public float shootAttackWeight = 0f;

        [Min(0f)]
        public float retreatTeleportWeight = 0f;

        [Min(0f)]
        public float rushComboWeight = 0f;

        [Min(0f)]
        public float mirageAssaultWeight = 0f;

        public float GetWeight(AttackPattern attackPattern)
        {
            switch (attackPattern)
            {
                case AttackPattern.NormalAttack:
                    return normalAttackWeight;
                case AttackPattern.ThrustAttack:
                    return thrustAttackWeight;
                case AttackPattern.ShootAttack:
                    return shootAttackWeight;
                case AttackPattern.RetreatTeleportAttack:
                    return retreatTeleportWeight;
                case AttackPattern.RushCombo:
                    return rushComboWeight;
                case AttackPattern.MirageAssault:
                    return mirageAssaultWeight;
                default:
                    return 0f;
            }
        }
    }

    internal enum AttackDamageType
    {
        Low = 0,
        High = 1,
        Horizontal = 2,
        Thrust = 3,
        Upper = 4,
    }
    #endregion

    #region プロパティ
    /// <summary>
    /// ボスの現在の状態
    /// </summary>
    public BossState CurrentState { get; private set; } = BossState.Intro;

    /// <summary>
    /// エディタ上かつisDebugNoWaitがtrueの場合のみ有効化されるデバッグ判定プロパティ
    /// </summary>
    private bool IsDebugNoWaitActive
    {
        get
        {
#if UNITY_EDITOR
            return isDebugNoWait;
#else
            return false;
#endif
        }
    }
    #endregion

    #region インスペクター設定（パラメータ設定）
    [Header("デバッグ機能")]
    [Tooltip(
        "trueの場合、各種待機時間や移動演出の時間を極短にしてデバッグを容易にします（エディタ上のみ有効）"
    )]
    [SerializeField]
    private bool isDebugNoWait = false;

#if UNITY_EDITOR
    [Header("攻撃パターン固定（Editor限定）")]
    [SerializeField]
    [Tooltip("有効にすると、選択した攻撃パターンだけを行動ループで繰り返します。")]
    private bool isDebugFixedAttackPattern = false;

    [SerializeField]
    [Tooltip("デバッグ中に繰り返す攻撃パターン")]
    private AttackPattern debugAttackPattern = AttackPattern.NormalAttack;
#endif

    [Header("行動AI：距離判定")]
    [Tooltip("このX軸距離以下を近距離として扱います。")]
    [SerializeField, Min(0f)]
    private float nearDistance = 5f;

    [Tooltip("このX軸距離以下を中距離、それより遠方を遠距離として扱います。")]
    [SerializeField, Min(0f)]
    private float middleDistance = 10f;

    [Tooltip("Sceneビューへ近距離・中距離の境界を表示します。")]
    [SerializeField]
    private bool showDistanceRangeGizmos = true;

    [Header("行動AI：距離別の攻撃抽選ウェイト")]
    [SerializeField]
    private AttackWeightSettings nearAttackWeights = new AttackWeightSettings
    {
        normalAttackWeight = 1f,
        retreatTeleportWeight = 0.2f,
        rushComboWeight = 1f,
        mirageAssaultWeight = 0.45f,
    };

    [SerializeField]
    private AttackWeightSettings middleAttackWeights = new AttackWeightSettings
    {
        thrustAttackWeight = 1f,
        retreatTeleportWeight = 0.2f,
        rushComboWeight = 1f,
        mirageAssaultWeight = 0.45f,
    };

    [SerializeField]
    private AttackWeightSettings farAttackWeights = new AttackWeightSettings
    {
        thrustAttackWeight = 0.8f,
        shootAttackWeight = 1f,
    };

    [Header("行動AI：Idle中の接近")]
    [Tooltip("通常攻撃・突き攻撃・突進コンボ後のIdle中にプレイヤーへ近づく速度")]
    [SerializeField, Min(0f)]
    private float idleApproachSpeed = 2.5f;

    [Tooltip("Idle中の接近でプレイヤーとの間に維持するX軸距離")]
    [SerializeField, Min(0f)]
    private float idleApproachStopDistance = 3.5f;

    [Header("各攻撃のダメージ量")]
    [Tooltip("下段攻撃でプレイヤーに与えるダメージ量")]
    [SerializeField]
    private int lowAttackDamage = 10;

    [Tooltip("上段攻撃でプレイヤーに与えるダメージ量")]
    [SerializeField]
    private int highAttackDamage = 15;

    [Tooltip("水平攻撃でプレイヤーに与えるダメージ量")]
    [SerializeField]
    private int horizontalAttackDamage = 15;

    [Tooltip("突き攻撃でプレイヤーに与えるダメージ量")]
    [SerializeField]
    private int thrustDamage = 20;

    [Tooltip("射撃の弾がプレイヤーに与えるダメージ量")]
    [SerializeField]
    private int shootDamage = 10;

    [Tooltip("WindEffectがプレイヤーに与えるダメージ量")]
    [SerializeField]
    private int windEffectDamage = 15;

    [Tooltip("Upper攻撃でプレイヤーに与えるダメージ量")]
    [SerializeField]
    private int upperAttackDamage = 15;

    [InfoBox("幻影強襲：プレイヤーの最大HPの30%を与える割合ダメージ（設定不要）")]
    [SerializeField, ReadOnly, Tooltip("表示専用。実際のダメージは定数で最大HPの30%に固定されています")]
    private float mirageAttackMaxHPRatioDisplay = MIRAGE_ATTACK_MAX_HP_RATIO;

    [Header("エリア境界の設定")]
    [Tooltip("ボスが行動できるエリアの左端X座標")]
    [SerializeField]
    private float areaLeftBound = -10f;

    [Tooltip("ボスが行動できるエリアの右端X座標")]
    [SerializeField]
    private float areaRightBound = 10f;

    [Tooltip("ボスが行動できるエリアの下端Y座標")]
    [SerializeField]
    private float areaBottomBound = -5f;

    [Tooltip("ボスが行動できるエリアの上端Y座標")]
    [SerializeField]
    private float areaTopBound = 10f;

    [Tooltip("壁からのマージン（Retreat, Rush, Mirage等で共通使用）")]
    [SerializeField]
    private float wallMargin = 2.0f;

    [Header("Idle状態の設定")]
    [Tooltip("Idle時に下端（areaBottomBound）から維持する高さ")]
    [SerializeField]
    private float idleHeightFromBottom = 2.0f;

    [Tooltip("Idle位置に移行する際にかかる移動時間（秒）")]
    [SerializeField]
    private float idleTransitionDuration = 1.0f;

    [Header("LowAttack(下段攻撃)状態の設定")]
    [Tooltip("攻撃時に移動する下端からの高さ")]
    [SerializeField]
    private float lowAttackHeightFromBottom = 4.0f;

    [Tooltip("攻撃準備時間（秒）")]
    [SerializeField]
    private float lowAttackReadyDuration = 1.0f;

    [Tooltip("攻撃時間（秒）")]
    [SerializeField]
    private float lowAttackDuration = 0.5f;

    [Tooltip("攻撃後待機時間（秒）")]
    [SerializeField]
    private float postLowAttackWaitDuration = 1.0f;

    [Tooltip("攻撃終了後、次の行動に移るまでの待機時間（秒）")]
    [SerializeField]
    private float lowAttackNextInterval = 1.0f;

    [Tooltip("下段攻撃時に使用するContactDamageController")]
    [SerializeField]
    private ContactDamageController lowAttackDamageController;

    [Header("HighAttack(上段攻撃)状態の設定")]
    [Tooltip("LowAttack後にHighAttackを行う確率(0.0～1.0)")]
    [Range(0f, 1f)]
    [SerializeField]
    private float highAttackProbability = 0.5f;

    [Tooltip("LowAttack終了からHighAttackに移行するまでの待機時間（秒）")]
    [SerializeField]
    private float waitBeforeHighAttackDuration = 1.0f;

    [Tooltip("攻撃時に移動する下端からの高さ")]
    [SerializeField]
    private float highAttackHeightFromBottom = 6.0f;

    [Tooltip("攻撃準備時間（秒）")]
    [SerializeField]
    private float highAttackReadyDuration = 1.0f;

    [Tooltip("攻撃時間（秒）")]
    [SerializeField]
    private float highAttackDuration = 0.5f;

    [Tooltip("攻撃後待機時間（秒）")]
    [SerializeField]
    private float postHighAttackWaitDuration = 1.0f;

    [Tooltip("攻撃終了後、次の行動に移るまでの待機時間（秒）")]
    [SerializeField]
    private float highAttackNextInterval = 1.0f;

    [Tooltip("上段攻撃時に使用するContactDamageController")]
    [SerializeField]
    private ContactDamageController highAttackDamageController;

    [Header("HorizontalAttack(水平攻撃)状態の設定")]
    [Tooltip("攻撃時に移動する下端からの高さ")]
    [SerializeField]
    private float horizontalAttackHeightFromBottom = 4.0f;

    [Tooltip("攻撃時間（秒）")]
    [SerializeField]
    private float horizontalAttackDuration = 0.5f;

    [Tooltip("攻撃後待機時間（秒）")]
    [SerializeField]
    private float postHorizontalAttackWaitDuration = 1.0f;

    [Tooltip("水平攻撃時に使用するContactDamageController")]
    [SerializeField]
    private ContactDamageController horizontalAttackDamageController;

    [Header("ThrustAttack(突き攻撃)状態の設定")]
    [Tooltip("剣の先のTransform（座標逆算用）")]
    [SerializeField]
    private Transform swordTipTransform;

    [Tooltip("これ以上近づかない・後方時の突進基準となる最小距離")]
    [SerializeField]
    private float minThrustDistance = 3.0f;

    [Tooltip("プレイヤー位置を通過した後、剣先がさらに進む距離")]
    [SerializeField, Min(0f)]
    private float thrustOvershootDistance = 2.5f;

    [Tooltip("突き攻撃1回で剣先が移動できる最大距離")]
    [SerializeField, Min(0f)]
    private float thrustMaxTravelDistance = 20f;

    [Tooltip("突き攻撃終了時の剣先の高さ（areaBottomBoundからのオフセット）")]
    [SerializeField]
    private float thrustAttackHeightFromBottom = 0f;

    [Tooltip("攻撃準備時（構え）に移動する下端からの高さ")]
    [SerializeField]
    private float thrustReadyHeightFromBottom = 3.0f;

    [Tooltip("攻撃準備時間（秒）")]
    [SerializeField]
    private float thrustReadyDuration = 1.0f;

    [Tooltip("攻撃（突進）時間（秒）")]
    [SerializeField]
    private float thrustDuration = 0.4f;

    [Tooltip("攻撃後待機時間（秒）")]
    [SerializeField]
    private float postThrustWaitDuration = 1.2f;

    [Tooltip("攻撃終了後、次の行動に移るまでの待機時間（秒）")]
    [SerializeField]
    private float thrustAttackNextInterval = 1.0f;

    [Tooltip("突き攻撃時に使用するContactDamageController")]
    [SerializeField]
    private ContactDamageController thrustDamageController;

    [Tooltip("突き攻撃時に再生するエフェクト（子オブジェクト）")]
    [SerializeField]
    private ParticleSystem thrustEffect;

    [Header("ShootAttack(射撃攻撃)状態の設定")]
    [Tooltip("HPが75%より多い時に、1回の射撃攻撃で発射する弾数")]
    [SerializeField, Range(1, 5)]
    private int shootBulletCount = 3;

    [Tooltip("HPが75%以下の時に、1回の射撃攻撃で発射する弾数")]
    [SerializeField, Range(1, 5)]
    private int shootBulletCountBelow75Percent = 4;

    [Tooltip("HPが40%以下の時に、1回の射撃攻撃で発射する弾数")]
    [SerializeField, Range(1, 5)]
    private int shootBulletCountBelow40Percent = 5;

    [Tooltip("攻撃準備時間（秒）")]
    [SerializeField]
    private float shootReadyDuration = 1.0f;

    [Tooltip("攻撃の基本Y座標オフセット")]
    [SerializeField]
    private float shootBulletHeightOffset = 1.0f;

    [Tooltip("弾の速度")]
    [SerializeField]
    private float shootBulletSpeed = 10.0f;

    [Tooltip("Shoot攻撃フェーズ自体の時間（秒）")]
    [SerializeField]
    private float shootAttackDuration = 0.5f;

    [Tooltip("連射時の弾と弾の間の発射間隔（秒）")]
    [SerializeField]
    private float shootBulletInterval = 0.3f;

    [Tooltip("攻撃後待機時間（秒）")]
    [SerializeField]
    private float postShootWaitDuration = 1.0f;

    [Tooltip("攻撃終了後、次の行動に移るまでの待機時間（秒）")]
    [SerializeField]
    private float shootAttackNextInterval = 1.0f;

    [Tooltip("Shoot攻撃時に再生するエフェクト（子オブジェクト）")]
    [SerializeField]
    private ParticleSystem shootEffect;

    [Tooltip("射撃時に再生するAirBurstEffect1のAnimator（配置済みの子オブジェクト）")]
    [SerializeField]
    private Animator airBurstEffectAnimator;

    [Header("後退テレポート(RetreatTeleport)状態の設定")]
    [Tooltip("1回の後退テレポート攻撃で中間攻撃を行う回数")]
    [SerializeField, Min(1)]
    private int retreatTeleportCount = 3;

    [Tooltip("背後への指定距離")]
    [SerializeField]
    private float retreatDistance = 10f;

    [Tooltip("初期の消滅にかかる時間（秒）")]
    [SerializeField]
    private float retreatInitialFadeOutTime = 1.0f;

    [Tooltip("ホログラム出現時間（秒）")]
    [SerializeField]
    private float retreatHologramAppearTime = 0.5f;

    [Tooltip("攻撃の時間（秒）")]
    [SerializeField]
    private float retreatAttackDuration = 1.0f;

    [Tooltip("ホログラム再消滅時間（秒）")]
    [SerializeField]
    private float retreatHologramDisappearTime = 0.5f;

    [Tooltip("攻撃終了後、次の行動に移るまでの待機時間（秒）")]
    [SerializeField]
    private float retreatTeleportNextInterval = 1.0f;

    [Tooltip("予め指定する複数の地面からの高さ（areaBottomBoundからのオフセット値）")]
    [SerializeField]
    private float[] retreatHeights;

    [Tooltip(
        "ホログラム演出の対象となるSpriteRenderer（Slashエフェクト等を除外するため手動で設定）"
    )]
    [SerializeField]
    private SpriteRenderer[] hologramTargetRenderers;

    [Header("行動開始後の全身残像")]
    [Tooltip("残像へ使用するマテリアル。未設定の場合は対象SpriteRendererと同じマテリアルを使用します")]
    [SerializeField]
    private Material afterImageMaterial;

    [Tooltip("通常速度の残像数")]
    [SerializeField, Min(1)]
    private int slowAfterImageCount = 4;

    [Tooltip("通常速度の残像間隔（秒）")]
    [SerializeField, Min(0.001f)]
    private float slowAfterImageInterval = 0.045f;

    [SerializeField, HideInInspector]
    private float slowAfterImageDistanceRange = 0f;

    [Tooltip("中速時の残像数")]
    [SerializeField, Min(1)]
    private int mediumAfterImageCount = 6;

    [Tooltip("中速時の残像間隔（秒）")]
    [SerializeField, Min(0.001f)]
    private float mediumAfterImageInterval = 0.03f;

    [SerializeField, HideInInspector]
    private float mediumAfterImageDistanceRange = 0.005f;

    [Tooltip("高速時の残像数")]
    [SerializeField, Min(1)]
    private int fastAfterImageCount = 9;

    [Tooltip("高速時の残像間隔（秒）")]
    [SerializeField, Min(0.001f)]
    private float fastAfterImageInterval = 0.015f;

    [SerializeField, HideInInspector]
    private float fastAfterImageDistanceRange = 0.005f;

    [Tooltip("中速残像へ切り替えるボス本体の移動速度")]
    [SerializeField, Min(0f)]
    private float mediumAfterImageSpeed = 0.5f;

    [Tooltip("高速残像へ切り替えるボス本体の移動速度")]
    [SerializeField, Min(0f)]
    private float fastAfterImageSpeed = 7f;

    [Tooltip("速度帯の境界付近で切替が連続しないようにする戻り幅")]
    [SerializeField, Min(0f)]
    private float afterImageSpeedHysteresis = 2f;

    [Tooltip("通常移動時の残像フェード時間の倍率。テレポート残像には適用しない")]
    [SerializeField, Min(0.1f)]
    private float afterImageFadeTimeMultiplier = 2f;

    [Tooltip("HP75%より多い時の残像色")]
    [SerializeField]
    private Color highHpAfterImageColor = new Color(0.2f, 0.9f, 1f, 0.28f);

    [Tooltip("HP75%以下の残像色")]
    [SerializeField]
    private Color middleHpAfterImageColor = new Color(0.35f, 0.45f, 1f, 0.32f);

    [Tooltip("HP40%以下の残像色")]
    [SerializeField]
    private Color lowHpAfterImageColor = new Color(0.9f, 0.2f, 1f, 0.38f);

    [Tooltip("瞬間移動で消えた位置に残す静止残像の保持時間")]
    [SerializeField, Min(0f)]
    private float teleportAfterImageDuration = 0.01f;

    [Tooltip("瞬間移動で消えた位置に残す静止残像のフェード時間")]
    [SerializeField, Min(0.01f)]
    private float teleportAfterImageFadeTime = 0.06f;

    [Tooltip("瞬間移動残像の通常残像に対する透明度倍率")]
    [SerializeField, Min(0f)]
    private float teleportAfterImageAlphaMultiplier = 1.25f;

    [Header("WindEffect(後退テレポート時)の設定")]
    [Tooltip("発射するWindEffectのプレハブ")]
    [SerializeField]
    private GameObject windEffectPrefab;

    [Tooltip("WindEffectのオブジェクトプール初期サイズ")]
    [SerializeField]
    private int windEffectPoolSize = 5;

    [Tooltip("WindEffectの移動速度（1秒間に進む距離）")]
    [SerializeField]
    private float windEffectSpeed = 20.0f;

    [Header("RushComboAttack(突進コンボ攻撃)状態の設定")]
    [Tooltip("1回で進む距離")]
    [SerializeField]
    private float advanceDistancePerHit = 3.0f;

    [Tooltip("Y座標の高さ（areaBottomBoundからのオフセット）")]
    [SerializeField]
    private float advanceHeightFromBottom = 0.0f;

    [Tooltip("準備時間（秒）")]
    [SerializeField]
    private float advanceReadyDuration = 1.0f;

    [Tooltip("1回ごとの攻撃時間（秒）")]
    [SerializeField]
    private float advanceAttackDuration = 0.5f;

    [Tooltip("1回ごとの待機・インターバル時間（秒）")]
    [SerializeField]
    private float advanceWaitDuration = 0.3f;

    [Tooltip("攻撃時間のうち、移動に使う時間の割合（0.0～1.0）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float advanceMoveTimeRatio = 0.2f;

    [Tooltip("突進コンボの1区間に保証する最低移動時間（秒）")]
    [SerializeField, Min(0.01f)]
    private float comboMinimumMoveDuration = 0.15f;

    [Tooltip("攻撃終了後、次の行動に移るまでの待機時間（秒）")]
    [SerializeField]
    private float rushComboNextInterval = 1.0f;

    [Tooltip("Upper攻撃時に使用するContactDamageController")]
    [SerializeField]
    private ContactDamageController upperAttackDamageController;

    [Header("MirageAssault(幻影急襲)状態の設定")]
    [Tooltip("初期の消滅にかかる時間（秒）")]
    [SerializeField]
    private float mirageInitialFadeOutTime = 0.5f;

    [Tooltip("最小テレポート回数")]
    [SerializeField]
    private int mirageMinTeleportCount = 3;

    [Tooltip("最大テレポート回数")]
    [SerializeField]
    private int mirageMaxTeleportCount = 5;

    [Tooltip("プレイヤーからの最小距離")]
    [SerializeField]
    private float mirageMinDistanceFromPlayer = 3.0f;

    [Tooltip("プレイヤーからの最大距離")]
    [SerializeField]
    private float mirageMaxDistanceFromPlayer = 8.0f;

    [Tooltip("地面からの最小高さ（areaBottomBoundからのオフセット）")]
    [SerializeField]
    private float mirageMinHeightFromBottom = 1.0f;

    [Tooltip("地面からの最大高さ（areaBottomBoundからのオフセット）")]
    [SerializeField]
    private float mirageMaxHeightFromBottom = 6.0f;

    [Tooltip("ホログラムが現れるまでの時間（秒）")]
    [SerializeField]
    private float mirageAppearTime = 0.3f;

    [Tooltip("ホログラムとして留まる中間時間（秒）")]
    [SerializeField]
    private float mirageStayTime = 0.4f;

    [Tooltip("ホログラムが消えるまでの時間（秒）")]
    [SerializeField]
    private float mirageDisappearTime = 0.3f;

    [Tooltip("消えてから次の場所に現れるまでの間隔（秒）")]
    [SerializeField]
    private float mirageIntervalTime = 0.2f;

    [Tooltip("ホログラムの最大透明度（0.0～1.0）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float mirageMaxAlpha = 0.6f;

    [Tooltip("最終攻撃時のプレイヤーからの距離")]
    [SerializeField]
    private float mirageFinalAttackDistance = 2.0f;

    [Tooltip("最終攻撃でLowAttackを選択する確率(0.0～1.0)")]
    [Range(0f, 1f)]
    [SerializeField]
    private float mirageLowAttackProbability = 0.5f;

    [Tooltip("幻影急襲の最終攻撃終了後の専用待機時間（秒）")]
    [SerializeField]
    private float miragePostWaitDuration = 1.0f;

    [Tooltip("攻撃終了後、次の行動に移るまでの待機時間（秒）")]
    [SerializeField]
    private float mirageAssaultNextInterval = 1.0f;

    [Header("召喚演出の設定")]
    [
        SerializeField,
        Tooltip(
            "魔法陣と表示マスクをまとめたルート。ボスの子の場合のみ、演出中は切り離して地面に固定します。"
        )
    ]
    private Transform summonEffectRoot;

    [SerializeField, Tooltip("召喚時に展開する魔法陣のコントローラー")]
    private MagicCircleController summonMagicCircleController;

    [SerializeField, Tooltip("魔法陣より下側を隠すためのSpriteMask")]
    private SpriteMask summonRevealMask;

    [
        SerializeField,
        Tooltip(
            "召喚時にSpriteMaskを適用する本体のSpriteRenderer。未設定時はホログラム対象を使用します。"
        )
    ]
    private SpriteRenderer[] summonTargetRenderers;

    [SerializeField, Tooltip("SummonEffectRootを基準にした、ボス本体の出現X座標オフセット")]
    private float summonBodyOffsetX = 0f;

    [SerializeField, Tooltip("SummonEffectRootを基準にした、出現前のボス本体の高さ")]
    private float summonBodyStartHeightFromRoot = -6f;

    [SerializeField, Tooltip("召喚エフェクトを表示してからボスが上昇を始めるまでの時間（秒）")]
    private float summonCircleOpenDuration = 0.4f;

    [SerializeField, Tooltip("ボスが地面から上昇する時間（秒）")]
    private float summonRiseDuration = 1.8f;

    [SerializeField, Tooltip("上昇完了後に魔法陣が消える時間（秒）")]
    private float summonCircleCloseDuration = 0.5f;
    #endregion

    #region 内部管理変数・ハッシュ
    // 内部管理用変数
    private Animator _animator;
    private CharacterHealth _characterHealth;
    private Coroutine _actionLoopCoroutine;
    private Coroutine _summonAppearanceCoroutine;
    private Tween _moveTween;
    private Transform _playerTransform;
    private readonly Dictionary<Sprite, Mesh> _summonGizmoMeshCache =
        new Dictionary<Sprite, Mesh>();
    private bool _isFacingRight = false; // 現在右を向いているかどうかのフラグ（デフォルト左向き）
    private float _currentNextInterval = 1.0f; // 攻撃終了後の次の行動までの待機時間を管理する変数
    private AttackPattern _lastAttackPattern;
    private int _consecutiveAttackCount;
    private bool _hasLastAttackPattern;
    private bool _hasTriggered75PercentPowerUp;
    private bool _hasTriggered40PercentPowerUp;
    private int _currentPowerUpHpPhase;
    private int _unlockedAfterImageHpPhase;
    private readonly List<GameObject> _teleportAfterImageObjects = new List<GameObject>();
    private readonly Dictionary<SpriteRenderer, Queue<Chapter3BossSkinnedAfterImage>> _poseAfterImagePools =
        new Dictionary<SpriteRenderer, Queue<Chapter3BossSkinnedAfterImage>>();
    private readonly Dictionary<Chapter3BossSkinnedAfterImage, SpriteRenderer> _poseAfterImageOwners =
        new Dictionary<Chapter3BossSkinnedAfterImage, SpriteRenderer>();
    private readonly List<Chapter3BossSkinnedAfterImage> _activePoseAfterImages =
        new List<Chapter3BossSkinnedAfterImage>();
    private readonly Dictionary<SpriteRenderer, Vector3> _previousAfterImageRendererPositions =
        new Dictionary<SpriteRenderer, Vector3>();
    private AfterImageSpeedTier _currentAfterImageSpeedTier = AfterImageSpeedTier.Slow;
    private Vector3 _previousAfterImagePosition;
    private bool _isAfterImageInitialized;
    private bool _isAfterImageEnabled;
    private bool _isDynamicAfterImageSuspended;
    private bool _isTeleportAfterImageSequenceActive;
    private float _slowAfterImageElapsedTime;

    private static readonly AttackPattern[] AttackPatterns =
    {
        AttackPattern.NormalAttack,
        AttackPattern.ThrustAttack,
        AttackPattern.ShootAttack,
        AttackPattern.RetreatTeleportAttack,
        AttackPattern.RushCombo,
        AttackPattern.MirageAssault,
    };

    // オブジェクトプール用キュー
    private Queue<GameObject> _windEffectPool;

    // Animatorパラメータの事前キャッシュ
    private readonly int _idleStateHash = Animator.StringToHash("Chapter3Boss_Idle");
    private readonly int _powerUpStateHash = Animator.StringToHash("Chapter3Boss_PowerUp");
    private readonly int _powerUpTriggerHash = Animator.StringToHash("PowerUpTrigger");

    // LowAttack用ハッシュ
    private readonly int _lowAttackReadyTriggerHash = Animator.StringToHash(
        "LowAttackReadyTrigger"
    );
    private readonly int _lowAttackTriggerHash = Animator.StringToHash("LowAttackTrigger");
    private readonly int _lowAttackReadySpeedHash = Animator.StringToHash("LowAttackReadySpeed");
    private readonly int _lowAttackSpeedHash = Animator.StringToHash("LowAttackSpeed");

    // HighAttack用ハッシュ
    private readonly int _normalHighAttackReadyTriggerHash = Animator.StringToHash(
        "NormalHighAttackReadyTrigger"
    );
    private readonly int _comboHighAttackReadyTriggerHash = Animator.StringToHash(
        "ComboHighAttackReadyTrigger"
    );
    private readonly int _normalHighAttackTriggerHash = Animator.StringToHash(
        "NormalHighAttackTrigger"
    );
    private readonly int _comboHighAttackTriggerHash = Animator.StringToHash(
        "ComboHighAttackTrigger"
    );
    private readonly int _highAttackReadySpeedHash = Animator.StringToHash("HighAttackReadySpeed");
    private readonly int _highAttackSpeedHash = Animator.StringToHash("HighAttackSpeed");

    // ThrustAttack用ハッシュ
    private readonly int _thrustReadyTriggerHash = Animator.StringToHash(
        "ThrustAttackReadyTrigger"
    );
    private readonly int _thrustTriggerHash = Animator.StringToHash("ThrustAttackTrigger");
    private readonly int _thrustReadySpeedHash = Animator.StringToHash("ThrustAttackReadySpeed");
    private readonly int _thrustSpeedHash = Animator.StringToHash("ThrustAttackSpeed");

    // ShootAttack用ハッシュ
    private readonly int _shootReadyTriggerHash = Animator.StringToHash("ShootAttackReadyTrigger");
    private readonly int _shootTriggerHash = Animator.StringToHash("ShootAttackTrigger");
    private readonly int _shootReadySpeedHash = Animator.StringToHash("ShootAttackReadySpeed");
    private readonly int _shootSpeedHash = Animator.StringToHash("ShootAttackSpeed");
    private readonly int _airBurstTriggerHash = Animator.StringToHash("AirBurst1Trigger");
    private readonly int _airBurstSpeedHash = Animator.StringToHash("AirBurst1Speed");

    // HorizontalAttack用ハッシュ
    private readonly int _horizontalAttackReadyTriggerHash = Animator.StringToHash(
        "HorizontalAttackReadyTrigger"
    );
    private readonly int _horizontalAttackTriggerHash = Animator.StringToHash(
        "HorizontalAttackTrigger"
    );
    private readonly int _horizontalAttackReadySpeedHash = Animator.StringToHash(
        "HorizontalAttackReadySpeed"
    );
    private readonly int _horizontalAttackSpeedHash = Animator.StringToHash(
        "HorizontalAttackSpeed"
    );

    // UpperAttack用ハッシュ (RushComboAttack内で使用)
    private readonly int _upperAttackTriggerHash = Animator.StringToHash("UpperAttackTrigger");
    private readonly int _upperAttackSpeedHash = Animator.StringToHash("UpperAttackSpeed");
    private readonly int _spriteMaskStencilCompHash = Shader.PropertyToID("_SpriteMaskStencilComp");
    #endregion

    #region Unity ライフサイクル
    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _characterHealth = GetComponent<CharacterHealth>();
        if (_characterHealth == null)
            _characterHealth = GetComponentInParent<CharacterHealth>();

        if (_characterHealth == null)
        {
            Debug.LogError(
                $"{name}: HPフェーズ判定に必要なCharacterHealthを取得できませんでした。",
                this
            );
        }
    }

    private void Start()
    {
        ResetState();
    }

    private void LateUpdate()
    {
        UpdateAfterImageEffects();
    }

    private void OnDisable()
    {
        DisableAllAttackDamage();
        SetAfterImageEffectsEnabled(false);
        ClearTeleportAfterImages();
    }

    private void OnDestroy()
    {
        ClearTeleportAfterImages();
        DestroyPoseAfterImagePoolObjects();

        if (_moveTween != null && _moveTween.IsActive())
        {
            _moveTween.Kill();
        }

        if (_summonAppearanceCoroutine != null)
        {
            StopCoroutine(_summonAppearanceCoroutine);
        }

        foreach (Mesh gizmoMesh in _summonGizmoMeshCache.Values)
        {
            if (gizmoMesh == null)
                continue;

            if (Application.isPlaying)
                Destroy(gizmoMesh);
            else
                DestroyImmediate(gizmoMesh);
        }
        _summonGizmoMeshCache.Clear();
    }

    private void OnDrawGizmos()
    {
        Vector3 center = new Vector3(
            (areaLeftBound + areaRightBound) / 2f,
            (areaTopBound + areaBottomBound) / 2f,
            transform.position.z
        );
        Vector3 size = new Vector3(
            areaRightBound - areaLeftBound,
            areaTopBound - areaBottomBound,
            0.1f
        );

        Gizmos.color = new Color(1f, 0f, 0f, 0.05f);
        Gizmos.DrawCube(center, size);

        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawWireCube(center, size);

        float drawLeft = areaLeftBound;
        float drawRight = areaRightBound;

        // Idle状態のキープ位置（青線）
        Gizmos.color = Color.blue;
        float idleY = areaBottomBound + idleHeightFromBottom;
        Gizmos.DrawLine(
            new Vector3(drawLeft, idleY, transform.position.z),
            new Vector3(drawRight, idleY, transform.position.z)
        );

        // LowAttack時の位置（オレンジ線）
        Gizmos.color = new Color(1f, 0.5f, 0f);
        float lowAttackY = areaBottomBound + lowAttackHeightFromBottom;
        Gizmos.DrawLine(
            new Vector3(drawLeft, lowAttackY, transform.position.z),
            new Vector3(drawRight, lowAttackY, transform.position.z)
        );

        // HighAttack時の位置（マゼンタ線）
        Gizmos.color = Color.magenta;
        float highAttackY = areaBottomBound + highAttackHeightFromBottom;
        Gizmos.DrawLine(
            new Vector3(drawLeft, highAttackY, transform.position.z),
            new Vector3(drawRight, highAttackY, transform.position.z)
        );

        // ThrustAttackの準備位置（白線）
        Gizmos.color = Color.white;
        float thrustReadyY = areaBottomBound + thrustReadyHeightFromBottom;
        Gizmos.DrawLine(
            new Vector3(drawLeft, thrustReadyY, transform.position.z),
            new Vector3(drawRight, thrustReadyY, transform.position.z)
        );

        DrawSummonAppearanceGizmos();
        DrawDistanceRangeGizmos();
    }

    /// <summary>
    /// SummonEffectRootを基準に、召喚開始位置とIdle位置の本体シルエットを表示します。
    /// </summary>
    private void DrawSummonAppearanceGizmos()
    {
        if (summonEffectRoot == null)
            return;

        Vector3 startPosition = GetSummonStartPosition();
        Vector3 finalPosition = GetSummonFinalPosition();
        Vector3 rootPosition = summonEffectRoot.position;
        rootPosition.z = transform.position.z;
        Vector3 offsetCorner = new Vector3(startPosition.x, rootPosition.y, rootPosition.z);

        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(rootPosition, 0.25f);
        Gizmos.DrawLine(rootPosition, offsetCorner);
        Gizmos.DrawLine(offsetCorner, startPosition);

        DrawSummonTargetSilhouette(startPosition, new Color(1f, 0.25f, 0.8f, 0.3f));
        DrawSummonTargetSilhouette(finalPosition, new Color(0.1f, 1f, 1f, 0.3f));

        Gizmos.color = new Color(0.1f, 1f, 1f, 0.7f);
        Gizmos.DrawLine(startPosition, finalPosition);
    }

    private void DrawDistanceRangeGizmos()
    {
        if (!showDistanceRangeGizmos)
            return;

        float safeNearDistance = Mathf.Max(0f, nearDistance);
        float safeMiddleDistance = Mathf.Max(safeNearDistance, middleDistance);
        float bottomY = areaBottomBound;
        float topY = areaTopBound;

        Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.9f);
        Gizmos.DrawLine(
            new Vector3(transform.position.x - safeNearDistance, bottomY, transform.position.z),
            new Vector3(transform.position.x - safeNearDistance, topY, transform.position.z)
        );
        Gizmos.DrawLine(
            new Vector3(transform.position.x + safeNearDistance, bottomY, transform.position.z),
            new Vector3(transform.position.x + safeNearDistance, topY, transform.position.z)
        );

        Gizmos.color = new Color(0.1f, 0.9f, 1f, 0.9f);
        Gizmos.DrawLine(
            new Vector3(transform.position.x - safeMiddleDistance, bottomY, transform.position.z),
            new Vector3(transform.position.x - safeMiddleDistance, topY, transform.position.z)
        );
        Gizmos.DrawLine(
            new Vector3(transform.position.x + safeMiddleDistance, bottomY, transform.position.z),
            new Vector3(transform.position.x + safeMiddleDistance, topY, transform.position.z)
        );
    }

    private void DrawSummonTargetSilhouette(Vector3 bossPosition, Color color)
    {
        SpriteRenderer[] targetRenderers =
            summonTargetRenderers != null && summonTargetRenderers.Length > 0
                ? summonTargetRenderers
                : hologramTargetRenderers;
        if (targetRenderers == null)
            return;

        Vector3 positionOffset = bossPosition - transform.position;
        Gizmos.color = color;

        foreach (SpriteRenderer targetRenderer in targetRenderers)
        {
            if (targetRenderer == null || targetRenderer.sprite == null)
                continue;

            Mesh spriteMesh = GetOrCreateSummonGizmoMesh(targetRenderer.sprite);
            if (spriteMesh == null)
                continue;

            Vector3 rendererScale = targetRenderer.transform.lossyScale;
            if (targetRenderer.flipX)
                rendererScale.x *= -1f;
            if (targetRenderer.flipY)
                rendererScale.y *= -1f;

            Gizmos.DrawMesh(
                spriteMesh,
                targetRenderer.transform.position + positionOffset,
                targetRenderer.transform.rotation,
                rendererScale
            );
        }
    }

    private Mesh GetOrCreateSummonGizmoMesh(Sprite sprite)
    {
        if (sprite == null)
            return null;

        if (_summonGizmoMeshCache.TryGetValue(sprite, out Mesh cachedMesh))
            return cachedMesh;

        Vector2[] spriteVertices = sprite.vertices;
        ushort[] spriteTriangles = sprite.triangles;
        if (spriteVertices == null || spriteVertices.Length < 3)
            return null;
        if (spriteTriangles == null || spriteTriangles.Length < 3)
            return null;

        Vector3[] meshVertices = new Vector3[spriteVertices.Length];
        for (int i = 0; i < spriteVertices.Length; i++)
        {
            meshVertices[i] = spriteVertices[i];
        }

        int[] meshTriangles = new int[spriteTriangles.Length];
        for (int i = 0; i < spriteTriangles.Length; i++)
        {
            meshTriangles[i] = spriteTriangles[i];
        }

        Mesh mesh = new Mesh
        {
            name = $"{sprite.name}_SummonGizmoMesh",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = meshVertices,
            triangles = meshTriangles,
        };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        _summonGizmoMeshCache.Add(sprite, mesh);
        return mesh;
    }

    private Vector3 GetSummonStartPosition()
    {
        Vector3 rootPosition =
            summonEffectRoot != null ? summonEffectRoot.position : transform.position;
        return new Vector3(
            rootPosition.x + summonBodyOffsetX,
            rootPosition.y + summonBodyStartHeightFromRoot,
            transform.position.z
        );
    }

    private Vector3 GetSummonFinalPosition()
    {
        float finalX =
            summonEffectRoot != null
                ? summonEffectRoot.position.x + summonBodyOffsetX
                : transform.position.x;
        return new Vector3(finalX, areaBottomBound + idleHeightFromBottom, transform.position.z);
    }
    #endregion

    #region 初期化・状態管理
    /// <summary>
    /// ボスの状態をリセットします。召喚演出と行動ループは開始しません。
    /// </summary>
    public void ResetState()
    {
        InitializeWindEffectPool();

        DisableAllAttackDamage();
        _hasLastAttackPattern = false;
        _consecutiveAttackCount = 0;
        _hasTriggered75PercentPowerUp = false;
        _hasTriggered40PercentPowerUp = false;
        _currentPowerUpHpPhase = 0;
        _unlockedAfterImageHpPhase = 0;
        SetAfterImageEffectsEnabled(false);
        ClearTeleportAfterImages();

        if (_actionLoopCoroutine != null)
        {
            StopCoroutine(_actionLoopCoroutine);
            _actionLoopCoroutine = null;
        }

        if (_moveTween != null && _moveTween.IsActive())
        {
            _moveTween.Kill();
        }

        CurrentState = BossState.Intro;
        SetDamageAreaAlpha(0f);
    }

    /// <summary>
    /// 攻撃中断や状態リセット時に、すべての接触ダメージを確実に無効化します。
    /// </summary>
    private void DisableAllAttackDamage()
    {
        DisableAttackDamage(lowAttackDamageController);
        DisableAttackDamage(highAttackDamageController);
        DisableAttackDamage(horizontalAttackDamageController);
        DisableAttackDamage(thrustDamageController);
        DisableAttackDamage(upperAttackDamageController);
    }

    /// <summary>
    /// Animation Event Relayから指定された攻撃部位の接触ダメージを切り替えます。
    /// </summary>
    internal void SetAttackDamageEnabled(AttackDamageType attackType, bool isEnabled)
    {
        ContactDamageController damageController = GetAttackDamageController(attackType);
        if (!isEnabled)
        {
            DisableAttackDamage(damageController);
            return;
        }

        DisableAllAttackDamage();
        damageController?.EnableContactDamage();
    }

    private ContactDamageController GetAttackDamageController(AttackDamageType attackType)
    {
        switch (attackType)
        {
            case AttackDamageType.Low:
                return lowAttackDamageController;
            case AttackDamageType.High:
                return highAttackDamageController;
            case AttackDamageType.Horizontal:
                return horizontalAttackDamageController;
            case AttackDamageType.Thrust:
                return thrustDamageController;
            case AttackDamageType.Upper:
                return upperAttackDamageController;
            default:
                Debug.LogWarning($"未対応の攻撃判定種別です: {attackType}", this);
                return null;
        }
    }

    private static void DisableAttackDamage(ContactDamageController damageController)
    {
        if (damageController != null)
        {
            damageController.DisableContactDamage();
        }
    }

    /// <summary>
    /// WindEffect用のオブジェクトプールを初期化します。
    /// </summary>
    private void InitializeWindEffectPool()
    {
        _windEffectPool = new Queue<GameObject>();

        if (windEffectPrefab == null)
        {
            Debug.LogWarning("WindEffectのプレハブが設定されていません。");
            return;
        }

        // 弾がボスの移動に影響されないよう、ルート階層（親なし）に生成する
        for (int i = 0; i < windEffectPoolSize; i++)
        {
            GameObject effect = Instantiate(windEffectPrefab);
            effect.SetActive(false);
            _windEffectPool.Enqueue(effect);
        }
    }

    /// <summary>
    /// プールからWindEffectを取得します。足りない場合は追加生成します。
    /// </summary>
    private GameObject GetWindEffectFromPool()
    {
        if (windEffectPrefab == null)
            return null;

        foreach (GameObject effect in _windEffectPool)
        {
            if (!effect.activeInHierarchy)
            {
                return effect;
            }
        }

        GameObject newEffect = Instantiate(windEffectPrefab);
        newEffect.SetActive(false);
        _windEffectPool.Enqueue(newEffect);
        return newEffect;
    }

    /// <summary>
    /// プレイヤーのTransform参照を最新の状態に更新します。
    /// </summary>
    private void UpdatePlayerTransformReference()
    {
        if (_playerTransform == null)
        {
            if (PlayerManager.instance != null && PlayerManager.instance.PlayerGameObject != null)
            {
                _playerTransform = PlayerManager.instance.PlayerGameObject.transform;
            }
            else
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag(
                    GameConstants.PLAYER_TAG_NAME
                );
                if (playerObj != null)
                    _playerTransform = playerObj.transform;
            }
        }
    }

    private bool IsPlayerInFront()
    {
        UpdatePlayerTransformReference();
        if (_playerTransform == null)
            return false;

        float facingDirection = _isFacingRight ? 1f : -1f;
        float forwardDistance =
            (_playerTransform.position.x - transform.position.x) * facingDirection;
        return forwardDistance > 0f;
    }

    /// <summary>
    /// ボスの左右の向きをRotation（Y軸回転）ベースで更新し、クラス内の向きフラグを保持します。
    /// </summary>
    public void UpdateFacingDirection(bool isFacingRight)
    {
        _isFacingRight = isFacingRight;

        if (_isFacingRight)
        {
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }
        else
        {
            transform.rotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// 正規化（1秒）されたアニメーションを指定時間で再生するための速度を設定します。
    /// </summary>
    private void SetAnimatorSpeed(int speedParamHash, float duration)
    {
        if (_animator == null)
            return;

        float safeDuration = Mathf.Max(0.1f, duration);
        float speed = 1.0f / safeDuration;

        _animator.SetFloat(speedParamHash, speed);
    }

    /// <summary>
    /// 攻撃判定エリアのSpriteRendererの透明度を設定します。
    /// </summary>
    private void SetDamageAreaAlpha(float alpha)
    {
        if (lowAttackDamageController != null)
        {
            SpriteRenderer lowRenderer = lowAttackDamageController.GetComponent<SpriteRenderer>();
            if (lowRenderer != null)
            {
                Color color = lowRenderer.color;
                color.a = alpha;
                lowRenderer.color = color;
            }
        }

        if (highAttackDamageController != null)
        {
            SpriteRenderer highRenderer = highAttackDamageController.GetComponent<SpriteRenderer>();
            if (highRenderer != null)
            {
                Color color = highRenderer.color;
                color.a = alpha;
                highRenderer.color = color;
            }
        }

        if (thrustDamageController != null)
        {
            SpriteRenderer thrustRenderer = thrustDamageController.GetComponent<SpriteRenderer>();
            if (thrustRenderer != null)
            {
                Color color = thrustRenderer.color;
                color.a = alpha;
                thrustRenderer.color = color;
            }
        }
    }
    #endregion

    #region 全身残像
    private void InitializeAfterImageEffects()
    {
        if (_isAfterImageInitialized)
            return;

        _isAfterImageInitialized = true;
        if (hologramTargetRenderers == null)
            return;

        foreach (SpriteRenderer targetRenderer in hologramTargetRenderers)
        {
            if (targetRenderer == null)
                continue;

            _previousAfterImageRendererPositions[targetRenderer] =
                targetRenderer.transform.position;

            if (!_poseAfterImagePools.ContainsKey(targetRenderer))
            {
                _poseAfterImagePools.Add(
                    targetRenderer,
                    new Queue<Chapter3BossSkinnedAfterImage>()
                );
            }
        }
    }

    private void SetAfterImageEffectsEnabled(bool isEnabled)
    {
        if (isEnabled)
            InitializeAfterImageEffects();

        _isAfterImageEnabled = isEnabled && _isAfterImageInitialized;
        _isDynamicAfterImageSuspended = false;
        _isTeleportAfterImageSequenceActive = false;
        _previousAfterImagePosition = transform.position;
        ResetAfterImageRendererPositions();
        _slowAfterImageElapsedTime = 0f;

        if (!_isAfterImageEnabled)
        {
            ClearActivePoseAfterImages();
            return;
        }

        ApplyAfterImageSpeedTier(AfterImageSpeedTier.Slow, true);
    }

    private void UpdateAfterImageEffects()
    {
        if (!_isAfterImageEnabled)
            return;

        Vector3 currentPosition = transform.position;
        if (_isDynamicAfterImageSuspended || Time.deltaTime <= 0f)
        {
            _previousAfterImagePosition = currentPosition;
            ResetAfterImageRendererPositions();
            return;
        }

        float moveSpeed = Vector3.Distance(currentPosition, _previousAfterImagePosition) / Time.deltaTime;
        _previousAfterImagePosition = currentPosition;
        moveSpeed = Mathf.Max(moveSpeed, GetAndUpdateFastestRendererSpeed());

        float mediumEnterSpeed = Mathf.Max(0f, mediumAfterImageSpeed);
        float mediumExitSpeed = Mathf.Max(0f, mediumEnterSpeed - afterImageSpeedHysteresis);
        float fastEnterSpeed = Mathf.Max(mediumEnterSpeed, fastAfterImageSpeed);
        float fastExitSpeed = Mathf.Max(mediumEnterSpeed, fastEnterSpeed - afterImageSpeedHysteresis);

        AfterImageSpeedTier nextTier = _currentAfterImageSpeedTier;
        switch (_currentAfterImageSpeedTier)
        {
            case AfterImageSpeedTier.Slow:
                if (moveSpeed >= fastEnterSpeed)
                    nextTier = AfterImageSpeedTier.Fast;
                else if (moveSpeed >= mediumEnterSpeed)
                    nextTier = AfterImageSpeedTier.Medium;
                break;
            case AfterImageSpeedTier.Medium:
                if (moveSpeed >= fastEnterSpeed)
                    nextTier = AfterImageSpeedTier.Fast;
                else if (moveSpeed < mediumExitSpeed)
                    nextTier = AfterImageSpeedTier.Slow;
                break;
            case AfterImageSpeedTier.Fast:
                if (moveSpeed < fastExitSpeed)
                {
                    nextTier =
                        moveSpeed < mediumExitSpeed
                            ? AfterImageSpeedTier.Slow
                            : AfterImageSpeedTier.Medium;
                }
                break;
        }

        ApplyAfterImageSpeedTier(nextTier, false);
        UpdatePoseAfterImageEmission();
    }

    private float GetAndUpdateFastestRendererSpeed()
    {
        if (hologramTargetRenderers == null || Time.deltaTime <= 0f)
            return 0f;

        float fastestSpeed = 0f;
        foreach (SpriteRenderer targetRenderer in hologramTargetRenderers)
        {
            if (targetRenderer == null)
                continue;

            Vector3 currentPosition = targetRenderer.transform.position;
            if (_previousAfterImageRendererPositions.TryGetValue(targetRenderer, out Vector3 previousPosition))
            {
                float rendererSpeed = Vector3.Distance(currentPosition, previousPosition) / Time.deltaTime;
                fastestSpeed = Mathf.Max(fastestSpeed, rendererSpeed);
            }
            _previousAfterImageRendererPositions[targetRenderer] = currentPosition;
        }

        return fastestSpeed;
    }

    private void ResetAfterImageRendererPositions()
    {
        if (hologramTargetRenderers == null)
            return;

        foreach (SpriteRenderer targetRenderer in hologramTargetRenderers)
        {
            if (targetRenderer != null)
            {
                _previousAfterImageRendererPositions[targetRenderer] =
                    targetRenderer.transform.position;
            }
        }
    }

    private void ApplyAfterImageSpeedTier(AfterImageSpeedTier speedTier, bool forceUpdate)
    {
        if (!forceUpdate && speedTier == _currentAfterImageSpeedTier)
            return;

        _currentAfterImageSpeedTier = speedTier;
    }

    private void UpdatePoseAfterImageEmission()
    {
        GetCurrentAfterImageSettings(out int count, out float interval, out float alphaScale);
        float safeInterval = Mathf.Max(0.001f, interval);
        _slowAfterImageElapsedTime += Time.deltaTime;
        if (_slowAfterImageElapsedTime < safeInterval)
            return;

        // 低FPS時も同一フレームの残像をまとめて生成せず、全身を1組だけ記録する。
        _slowAfterImageElapsedTime %= safeInterval;
        EmitPoseAfterImageBatch(count, safeInterval, alphaScale);
    }

    private void GetCurrentAfterImageSettings(
        out int count,
        out float interval,
        out float alphaScale
    )
    {
        switch (_currentAfterImageSpeedTier)
        {
            case AfterImageSpeedTier.Fast:
                count = fastAfterImageCount;
                interval = fastAfterImageInterval;
                alphaScale = 0.85f;
                return;
            case AfterImageSpeedTier.Medium:
                count = mediumAfterImageCount;
                interval = mediumAfterImageInterval;
                alphaScale = 0.7f;
                return;
            default:
                count = slowAfterImageCount;
                interval = slowAfterImageInterval;
                alphaScale = 0.55f;
                return;
        }
    }

    private void EmitPoseAfterImageBatch(int count, float interval, float alphaScale)
    {
        if (hologramTargetRenderers == null)
            return;

        Color color = GetCurrentAfterImageColor();
        color.a = Mathf.Clamp01(color.a * alphaScale);
        float holdTime = interval;
        float fadeTime =
            interval * Mathf.Max(1, count) * Mathf.Max(0.1f, afterImageFadeTimeMultiplier);

        // Chapter3BossはネストされたPSDの複数SpriteRendererで全身を構成する。
        // Animator評価後の同一LateUpdateで全部位を記録し、1ポーズとして同期させる。
        foreach (SpriteRenderer targetRenderer in hologramTargetRenderers)
        {
            if (
                targetRenderer == null
                || !targetRenderer.enabled
                || targetRenderer.sprite == null
            )
                continue;

            Chapter3BossSkinnedAfterImage afterImage = GetPoseAfterImage(targetRenderer);
            afterImage.gameObject.SetActive(true);
            afterImage.Initialize(
                targetRenderer,
                afterImageMaterial != null ? afterImageMaterial : targetRenderer.sharedMaterial,
                color,
                holdTime,
                fadeTime,
                ReleasePoseAfterImage
            );
            _activePoseAfterImages.Add(afterImage);
        }
    }

    private Chapter3BossSkinnedAfterImage GetPoseAfterImage(SpriteRenderer targetRenderer)
    {
        Queue<Chapter3BossSkinnedAfterImage> pool = _poseAfterImagePools[targetRenderer];
        if (pool.Count > 0)
            return pool.Dequeue();

        GameObject afterImageObject = new GameObject(
            $"{targetRenderer.gameObject.name}_PoseAfterImage"
        );
        afterImageObject.layer = targetRenderer.gameObject.layer;
        Chapter3BossSkinnedAfterImage afterImage =
            afterImageObject.AddComponent<Chapter3BossSkinnedAfterImage>();
        _poseAfterImageOwners.Add(afterImage, targetRenderer);
        return afterImage;
    }

    private void ReleasePoseAfterImage(Chapter3BossSkinnedAfterImage afterImage)
    {
        if (afterImage == null)
            return;

        _activePoseAfterImages.Remove(afterImage);
        if (
            _poseAfterImageOwners.TryGetValue(afterImage, out SpriteRenderer owner)
            && owner != null
            && _poseAfterImagePools.TryGetValue(owner, out Queue<Chapter3BossSkinnedAfterImage> pool)
        )
        {
            afterImage.gameObject.SetActive(false);
            pool.Enqueue(afterImage);
            return;
        }

        Destroy(afterImage.gameObject);
    }

    private void ClearActivePoseAfterImages()
    {
        for (int i = _activePoseAfterImages.Count - 1; i >= 0; i--)
        {
            Chapter3BossSkinnedAfterImage afterImage = _activePoseAfterImages[i];
            _activePoseAfterImages.RemoveAt(i);
            if (afterImage == null)
                continue;

            if (
                _poseAfterImageOwners.TryGetValue(afterImage, out SpriteRenderer owner)
                && owner != null
                && _poseAfterImagePools.TryGetValue(owner, out Queue<Chapter3BossSkinnedAfterImage> pool)
            )
            {
                afterImage.gameObject.SetActive(false);
                pool.Enqueue(afterImage);
            }
            else
            {
                Destroy(afterImage.gameObject);
            }
        }
    }

    private void DestroyPoseAfterImagePoolObjects()
    {
        foreach (Chapter3BossSkinnedAfterImage afterImage in _poseAfterImageOwners.Keys)
        {
            if (afterImage != null)
                Destroy(afterImage.gameObject);
        }

        _activePoseAfterImages.Clear();
        _poseAfterImageOwners.Clear();
        _poseAfterImagePools.Clear();
    }

    private int GetAfterImageHpPhase()
    {
        float normalizedHP = GetNormalizedHP();
        int hpPhase;
        if (normalizedHP <= 0.4f)
            hpPhase = 2;
        else if (normalizedHP <= 0.75f)
            hpPhase = 1;
        else
            hpPhase = 0;

        // 色のフェーズ変更は、PowerUpのAnimation Eventで「加速」が表示された後に解禁する。
        return Mathf.Min(hpPhase, _unlockedAfterImageHpPhase);
    }

    private Color GetCurrentAfterImageColor()
    {
        switch (GetAfterImageHpPhase())
        {
            case 2:
                return lowHpAfterImageColor;
            case 1:
                return middleHpAfterImageColor;
            default:
                return highHpAfterImageColor;
        }
    }

    private void BeginTeleportAfterImage()
    {
        if (!_isAfterImageEnabled)
            return;

        // 通常残像を移動前の位置に長く残さず、消失地点の短い1組へ置き換える。
        ClearActivePoseAfterImages();
        CreateTeleportDepartureAfterImages();
        _isTeleportAfterImageSequenceActive = true;
        _isDynamicAfterImageSuspended = true;
        _slowAfterImageElapsedTime = 0f;
    }

    private void ResumeDynamicAfterImage()
    {
        if (!_isAfterImageEnabled)
            return;

        _previousAfterImagePosition = transform.position;
        ResetAfterImageRendererPositions();
        _slowAfterImageElapsedTime = 0f;
        _isDynamicAfterImageSuspended = _isTeleportAfterImageSequenceActive;
    }

    private void EndTeleportAfterImageSequence()
    {
        _isTeleportAfterImageSequenceActive = false;
        ResumeDynamicAfterImage();
    }

    private void CreateTeleportDepartureAfterImages()
    {
        if (hologramTargetRenderers == null)
            return;

        // 連続テレポート時は前回分を残さず、消失地点に短い残像を1組だけ表示する。
        ClearTeleportAfterImages();

        Color color = GetCurrentAfterImageColor();
        color.a = Mathf.Clamp01(color.a * teleportAfterImageAlphaMultiplier);
        // テレポートでは消失直後の一瞬だけ残し、通常残像のような長い軌跡にはしない。
        float duration = Mathf.Min(Mathf.Max(0f, teleportAfterImageDuration), 0.001f);
        float fadeTime = Mathf.Min(Mathf.Max(0.01f, teleportAfterImageFadeTime), 0.035f);

        foreach (SpriteRenderer targetRenderer in hologramTargetRenderers)
        {
            if (targetRenderer == null || targetRenderer.sprite == null)
                continue;

            GameObject afterImageObject = new GameObject(
                $"{targetRenderer.gameObject.name}_TeleportAfterImage"
            );
            afterImageObject.layer = targetRenderer.gameObject.layer;
            Chapter3BossSkinnedAfterImage afterImage =
                afterImageObject.AddComponent<Chapter3BossSkinnedAfterImage>();
            afterImage.Initialize(
                targetRenderer,
                afterImageMaterial != null ? afterImageMaterial : targetRenderer.sharedMaterial,
                color,
                duration,
                fadeTime,
                ReleaseTeleportAfterImage
            );
            _teleportAfterImageObjects.Add(afterImageObject);
        }
    }

    private void ReleaseTeleportAfterImage(Chapter3BossSkinnedAfterImage afterImage)
    {
        if (afterImage == null)
            return;

        GameObject afterImageObject = afterImage.gameObject;
        _teleportAfterImageObjects.Remove(afterImageObject);
        Destroy(afterImageObject);
    }

    private void ClearTeleportAfterImages()
    {
        foreach (GameObject afterImageObject in _teleportAfterImageObjects)
        {
            if (afterImageObject != null)
                Destroy(afterImageObject);
        }
        _teleportAfterImageObjects.Clear();
    }
    #endregion

    #region メインループ・状態推移
    /// <summary>
    /// Fungusから呼び出し、魔法陣からボスが徐々に現れる召喚演出を開始します。
    /// 行動ループは開始しないため、演出後にStartActionLoopを別途呼び出してください。
    /// </summary>
    public void PlaySummonAppearance()
    {
        if (_summonAppearanceCoroutine != null)
        {
            StopCoroutine(_summonAppearanceCoroutine);
        }

        _summonAppearanceCoroutine = StartCoroutine(SummonAppearanceSequence());
    }

    private IEnumerator SummonAppearanceSequence()
    {
        CurrentState = BossState.Intro;

        if (_actionLoopCoroutine != null)
        {
            StopCoroutine(_actionLoopCoroutine);
            _actionLoopCoroutine = null;
        }

        if (_moveTween != null && _moveTween.IsActive())
        {
            _moveTween.Kill();
        }

        // Animatorの上書きに負けないタイミングで全攻撃判定の透明度を0に初期化
        SetDamageAreaAlpha(0f);

        if (_animator != null)
        {
            _animator.CrossFadeInFixedTime(_idleStateHash, 0f);
        }

        SpriteRenderer[] targetRenderers =
            summonTargetRenderers != null && summonTargetRenderers.Length > 0
                ? summonTargetRenderers
                : hologramTargetRenderers;
        var originalStencilComparisons = new Dictionary<Material, float>();

        if (targetRenderers != null)
        {
            foreach (SpriteRenderer targetRenderer in targetRenderers)
            {
                if (targetRenderer == null)
                    continue;

                targetRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

                if (targetRenderer.material != null)
                {
                    Material targetMaterial = targetRenderer.material;
                    targetMaterial.EnableKeyword("_HOLOGRAM_ON");
                    targetMaterial.SetFloat("_HologramBlend", 1f);

                    if (targetMaterial.HasProperty(_spriteMaskStencilCompHash))
                    {
                        originalStencilComparisons[targetMaterial] = targetMaterial.GetFloat(
                            _spriteMaskStencilCompHash
                        );
                        targetMaterial.SetFloat(
                            _spriteMaskStencilCompHash,
                            (float)UnityEngine.Rendering.CompareFunction.LessEqual
                        );
                    }
                }

                Color color = targetRenderer.color;
                color.a = 1f;
                targetRenderer.color = color;
            }
        }

        Transform originalEffectParent = null;
        int originalEffectSiblingIndex = 0;
        Vector3 originalEffectLocalPosition = Vector3.zero;
        Quaternion originalEffectLocalRotation = Quaternion.identity;
        Vector3 originalEffectLocalScale = Vector3.one;
        bool isEffectRootChildOfBoss = false;

        if (summonEffectRoot != null)
        {
            summonEffectRoot.gameObject.SetActive(true);
            isEffectRootChildOfBoss = summonEffectRoot.IsChildOf(transform);

            if (isEffectRootChildOfBoss)
            {
                originalEffectParent = summonEffectRoot.parent;
                originalEffectSiblingIndex = summonEffectRoot.GetSiblingIndex();
                originalEffectLocalPosition = summonEffectRoot.localPosition;
                originalEffectLocalRotation = summonEffectRoot.localRotation;
                originalEffectLocalScale = summonEffectRoot.localScale;
                summonEffectRoot.SetParent(null, true);
            }
        }

        if (summonRevealMask != null)
        {
            summonRevealMask.enabled = true;
        }

        float openDuration = Mathf.Max(0f, summonCircleOpenDuration);
        if (openDuration > 0f)
        {
            yield return new WaitForSeconds(openDuration);
        }

        Vector3 startPosition = GetSummonStartPosition();
        Vector3 finalPosition = GetSummonFinalPosition();
        transform.position = startPosition;

        float riseDuration = Mathf.Max(0f, summonRiseDuration);
        Sequence riseSequence = DOTween.Sequence();
        riseSequence.Join(transform.DOMove(finalPosition, riseDuration).SetEase(Ease.OutCubic));

        if (targetRenderers != null)
        {
            foreach (SpriteRenderer targetRenderer in targetRenderers)
            {
                if (targetRenderer != null && targetRenderer.material != null)
                {
                    riseSequence.Join(
                        targetRenderer.material.DOFloat(0f, "_HologramBlend", riseDuration)
                    );
                }
            }
        }

        yield return riseSequence.WaitForCompletion();

        transform.position = finalPosition;

        if (targetRenderers != null)
        {
            foreach (SpriteRenderer targetRenderer in targetRenderers)
            {
                if (targetRenderer == null)
                    continue;

                targetRenderer.material?.DisableKeyword("_HOLOGRAM_ON");
                if (
                    targetRenderer.material != null
                    && originalStencilComparisons.TryGetValue(
                        targetRenderer.material,
                        out float stencilComparison
                    )
                )
                {
                    targetRenderer.material.SetFloat(_spriteMaskStencilCompHash, stencilComparison);
                }
                targetRenderer.maskInteraction = SpriteMaskInteraction.None;
            }
        }

        if (summonRevealMask != null)
        {
            summonRevealMask.enabled = false;
        }

        float closeDuration = Mathf.Max(0f, summonCircleCloseDuration);
        if (summonMagicCircleController != null)
        {
            summonMagicCircleController.ChangeScaleXY(
                Vector2.zero,
                closeDuration,
                null,
                Ease.InBack
            );
        }

        if (closeDuration > 0f)
        {
            yield return new WaitForSeconds(closeDuration);
        }

        if (summonEffectRoot != null)
        {
            summonEffectRoot.gameObject.SetActive(false);

            if (isEffectRootChildOfBoss)
            {
                summonEffectRoot.SetParent(originalEffectParent, false);
                summonEffectRoot.SetSiblingIndex(originalEffectSiblingIndex);
                summonEffectRoot.localPosition = originalEffectLocalPosition;
                summonEffectRoot.localRotation = originalEffectLocalRotation;
                summonEffectRoot.localScale = originalEffectLocalScale;
            }
        }

        CurrentState = BossState.Idle;
        _summonAppearanceCoroutine = null;
    }

    /// <summary>
    /// メイン行動ループのコルーチンを開始します。
    /// </summary>
    public void StartActionLoop()
    {
        if (_actionLoopCoroutine != null)
        {
            StopCoroutine(_actionLoopCoroutine);
        }

        _actionLoopCoroutine = StartCoroutine(ActionLoopSequence());

        if (_characterHealth is BossHealth bossHealth)
        {
            bossHealth.InitializeBossSpecifics();
        }

        SetAfterImageEffectsEnabled(true);
    }

    /// <summary>
    /// 登場 -> 攻撃方法選択 -> 待機 -> 攻撃方法選択 を繰り返すメインループです。
    /// </summary>
    private IEnumerator ActionLoopSequence()
    {
        while (true)
        {
            // HPフェーズ移行は、Editor用の固定攻撃を含むすべての攻撃選択より優先する。
            // 一度に両方の閾値を下回った場合も、75%演出を先に消化し、次のループで40%演出を行う。
            if (TryConsumePendingPowerUp())
            {
                yield return StartCoroutine(PerformPowerUp());
                yield return StartCoroutine(TransitionToIdle());
                continue;
            }

            AttackPattern attackPattern = SelectNextAttackPattern();
            yield return StartCoroutine(ExecuteAttackPattern(attackPattern));

            // 攻撃中に閾値を下回った場合は、通常のIdle移行・待機より先にPowerUpを行う。
            if (TryConsumePendingPowerUp())
            {
                yield return StartCoroutine(PerformPowerUp());
                yield return StartCoroutine(TransitionToIdle());
                continue;
            }

            // 3. 待機状態（Idle）への移行
            yield return StartCoroutine(TransitionToIdle());

            // 4. 次の行動ループまでのインターバル待機
            // 固定値ではなく、直前の攻撃でセットされた_currentNextIntervalを使用する
            float waitTime = GetCombatDuration(_currentNextInterval);
            yield return StartCoroutine(PerformIdleWait(attackPattern, waitTime));
        }
    }

    /// <summary>
    /// HP75%未満、40%未満の順に、未再生のPowerUp演出を1件だけ予約消化します。
    /// </summary>
    private bool TryConsumePendingPowerUp()
    {
        float normalizedHP = GetNormalizedHP();

        if (!_hasTriggered75PercentPowerUp && normalizedHP < 0.75f)
        {
            _hasTriggered75PercentPowerUp = true;
            _currentPowerUpHpPhase = 1;
            return true;
        }

        if (!_hasTriggered40PercentPowerUp && normalizedHP < 0.4f)
        {
            _hasTriggered40PercentPowerUp = true;
            _currentPowerUpHpPhase = 2;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 攻撃判定を停止してPowerUpアニメーションを1回再生します。
    /// </summary>
    private IEnumerator PerformPowerUp()
    {
        DisableAllAttackDamage();
        CurrentState = BossState.PoweringUp;

        if (_animator == null)
            yield break;

        _animator.SetTrigger(_powerUpTriggerHash);

        // Triggerが評価され、PowerUpステートへ遷移するまで待つ。
        yield return null;
        while (
            _animator.GetCurrentAnimatorStateInfo(0).shortNameHash != _powerUpStateHash
            && (
                !_animator.IsInTransition(0)
                || _animator.GetNextAnimatorStateInfo(0).shortNameHash != _powerUpStateHash
            )
        )
        {
            yield return null;
        }

        // 遷移完了後、非ループのPowerUpアニメーションが1周するまで待つ。
        while (
            _animator.IsInTransition(0)
            || _animator.GetCurrentAnimatorStateInfo(0).shortNameHash != _powerUpStateHash
            || _animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f
        )
        {
            yield return null;
        }
    }

    /// <summary>
    /// PowerUpアニメーションのAnimation Eventからスキル名UIを表示します。
    /// </summary>
    internal void ShowPowerUpSkillNameUI()
    {
        if (CurrentState != BossState.PoweringUp)
            return;

        GameUIManager.instance?.ShowSkillNameUI(POWER_UP_SKILL_NAME);
        _unlockedAfterImageHpPhase = Mathf.Max(
            _unlockedAfterImageHpPhase,
            _currentPowerUpHpPhase
        );
    }

    /// <summary>
    /// 次に実行する攻撃パターンを決定します。
    /// Editor上で攻撃固定が有効な場合のみ、デバッグ指定を優先します。
    /// </summary>
    private AttackPattern SelectNextAttackPattern()
    {
#if UNITY_EDITOR
        if (isDebugFixedAttackPattern)
        {
            return debugAttackPattern;
        }
#endif

        PlayerDistanceRange distanceRange = GetPlayerDistanceRange();
        AttackPattern selectedAttack = SelectWeightedAttack(distanceRange);
        RecordSelectedAttack(selectedAttack);
        return selectedAttack;
    }

    private PlayerDistanceRange GetPlayerDistanceRange()
    {
        UpdatePlayerTransformReference();
        if (_playerTransform == null)
            return PlayerDistanceRange.Far;

        float distanceX = Mathf.Abs(_playerTransform.position.x - transform.position.x);
        float safeNearDistance = Mathf.Max(0f, nearDistance);
        float safeMiddleDistance = Mathf.Max(safeNearDistance, middleDistance);

        if (distanceX <= safeNearDistance)
            return PlayerDistanceRange.Near;
        if (distanceX <= safeMiddleDistance)
            return PlayerDistanceRange.Middle;
        return PlayerDistanceRange.Far;
    }

    private AttackPattern SelectWeightedAttack(PlayerDistanceRange distanceRange)
    {
        AttackWeightSettings weights = GetAttackWeights(distanceRange);
        float totalWeight = 0f;

        foreach (AttackPattern attackPattern in AttackPatterns)
        {
            totalWeight += GetAvailableAttackWeight(attackPattern, distanceRange, weights);
        }

        if (totalWeight <= 0f)
        {
            AttackPattern fallbackAttack = GetFallbackAttack(distanceRange);
            Debug.LogWarning(
                $"{name}: {distanceRange}の攻撃候補がありません。{fallbackAttack}を使用します。",
                this
            );
            return fallbackAttack;
        }

        float randomValue = Random.Range(0f, totalWeight);
        foreach (AttackPattern attackPattern in AttackPatterns)
        {
            float weight = GetAvailableAttackWeight(attackPattern, distanceRange, weights);
            if (weight <= 0f)
                continue;

            randomValue -= weight;
            if (randomValue <= 0f)
                return attackPattern;
        }

        return GetFallbackAttack(distanceRange);
    }

    private AttackWeightSettings GetAttackWeights(PlayerDistanceRange distanceRange)
    {
        switch (distanceRange)
        {
            case PlayerDistanceRange.Near:
                return nearAttackWeights;
            case PlayerDistanceRange.Middle:
                return middleAttackWeights;
            case PlayerDistanceRange.Far:
                return farAttackWeights;
            default:
                return farAttackWeights;
        }
    }

    private float GetAvailableAttackWeight(
        AttackPattern attackPattern,
        PlayerDistanceRange distanceRange,
        AttackWeightSettings weights
    )
    {
        if (weights == null)
            return 0f;

        if (!IsAttackAllowedForDistance(attackPattern, distanceRange))
            return 0f;

        if (
            _hasLastAttackPattern
            && _consecutiveAttackCount >= 2
            && attackPattern == _lastAttackPattern
        )
            return 0f;

        if (
            attackPattern == AttackPattern.MirageAssault
            && (GetNormalizedHP() >= 0.4f || distanceRange == PlayerDistanceRange.Far)
        )
        {
            return 0f;
        }

        return Mathf.Max(0f, weights.GetWeight(attackPattern));
    }

    private static bool IsAttackAllowedForDistance(
        AttackPattern attackPattern,
        PlayerDistanceRange distanceRange
    )
    {
        switch (distanceRange)
        {
            case PlayerDistanceRange.Near:
                return attackPattern == AttackPattern.NormalAttack
                    || attackPattern == AttackPattern.RetreatTeleportAttack
                    || attackPattern == AttackPattern.RushCombo
                    || attackPattern == AttackPattern.MirageAssault;
            case PlayerDistanceRange.Middle:
                return attackPattern == AttackPattern.ThrustAttack
                    || attackPattern == AttackPattern.RetreatTeleportAttack
                    || attackPattern == AttackPattern.RushCombo
                    || attackPattern == AttackPattern.MirageAssault;
            case PlayerDistanceRange.Far:
                return attackPattern == AttackPattern.ShootAttack
                    || attackPattern == AttackPattern.ThrustAttack;
            default:
                return false;
        }
    }

    private AttackPattern GetFallbackAttack(PlayerDistanceRange distanceRange)
    {
        AttackPattern primaryAttack =
            distanceRange == PlayerDistanceRange.Near ? AttackPattern.NormalAttack
            : distanceRange == PlayerDistanceRange.Middle ? AttackPattern.ThrustAttack
            : AttackPattern.ShootAttack;

        if (
            !_hasLastAttackPattern
            || _consecutiveAttackCount < 2
            || primaryAttack != _lastAttackPattern
        )
        {
            return primaryAttack;
        }

        switch (distanceRange)
        {
            case PlayerDistanceRange.Near:
                return AttackPattern.RushCombo;
            case PlayerDistanceRange.Middle:
                return AttackPattern.RushCombo;
            case PlayerDistanceRange.Far:
                return AttackPattern.ThrustAttack;
            default:
                return AttackPattern.ThrustAttack;
        }
    }

    private void RecordSelectedAttack(AttackPattern attackPattern)
    {
        if (_hasLastAttackPattern && attackPattern == _lastAttackPattern)
        {
            _consecutiveAttackCount++;
            return;
        }

        _lastAttackPattern = attackPattern;
        _consecutiveAttackCount = 1;
        _hasLastAttackPattern = true;
    }

    private float GetNormalizedHP()
    {
        if (_characterHealth == null || _characterHealth.MaxHP <= 0)
            return 1f;

        return _characterHealth.NormalizedHP;
    }

    private float GetCurrentCombatTimeScale()
    {
        float normalizedHP = GetNormalizedHP();
        if (normalizedHP <= 0.4f)
            return 0.8f;
        if (normalizedHP <= 0.75f)
            return 0.9f;
        return 1f;
    }

    private float GetCombatDuration(float baseDuration, float minimumDuration = 0.1f)
    {
        if (IsDebugNoWaitActive)
            return minimumDuration;

        return Mathf.Max(minimumDuration, baseDuration * GetCurrentCombatTimeScale());
    }

    private IEnumerator PerformIdleWait(AttackPattern previousAttack, float waitTime)
    {
        bool shouldApproach =
            previousAttack == AttackPattern.NormalAttack
            || previousAttack == AttackPattern.ThrustAttack
            || previousAttack == AttackPattern.RushCombo;

        float elapsedTime = 0f;
        while (elapsedTime < waitTime)
        {
            if (shouldApproach)
                MoveTowardsPlayerDuringIdle();

            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    private void MoveTowardsPlayerDuringIdle()
    {
        UpdatePlayerTransformReference();
        if (_playerTransform == null || idleApproachSpeed <= 0f)
            return;

        float deltaX = _playerTransform.position.x - transform.position.x;
        float distanceX = Mathf.Abs(deltaX);
        float stopDistance = Mathf.Max(0f, idleApproachStopDistance);
        if (distanceX <= stopDistance)
            return;

        UpdateFacingDirection(deltaX > 0f);

        float targetX = _playerTransform.position.x - Mathf.Sign(deltaX) * stopDistance;
        float minimumX = areaLeftBound + wallMargin;
        float maximumX = areaRightBound - wallMargin;
        targetX = Mathf.Clamp(targetX, minimumX, maximumX);

        float nextX = Mathf.MoveTowards(
            transform.position.x,
            targetX,
            idleApproachSpeed * Time.deltaTime
        );
        transform.position = new Vector3(nextX, transform.position.y, transform.position.z);
    }

    /// <summary>
    /// 選択された攻撃パターンを実行します。
    /// </summary>
    private IEnumerator ExecuteAttackPattern(AttackPattern attackPattern)
    {
        switch (attackPattern)
        {
            case AttackPattern.NormalAttack:
                yield return StartCoroutine(PerformNormalAttack());
                break;
            case AttackPattern.ThrustAttack:
                yield return StartCoroutine(PerformThrustAttack());
                break;
            case AttackPattern.ShootAttack:
                yield return StartCoroutine(PerformShootAttack(GetCurrentShootBulletCount()));
                break;
            case AttackPattern.RetreatTeleportAttack:
                yield return StartCoroutine(PerformRetreatTeleport(retreatTeleportCount));
                break;
            case AttackPattern.RushCombo:
                yield return StartCoroutine(PerformRushComboAttack());
                break;
            case AttackPattern.MirageAssault:
                yield return StartCoroutine(PerformMirageAssault());
                break;
            default:
                Debug.LogWarning($"未対応の攻撃パターンです: {attackPattern}", this);
                break;
        }
    }

    /// <summary>
    /// 射撃開始時のHPフェーズに応じた発射数を取得します。
    /// </summary>
    private int GetCurrentShootBulletCount()
    {
        float normalizedHP = GetNormalizedHP();
        if (normalizedHP <= 0.4f)
            return Mathf.Max(1, shootBulletCountBelow40Percent);
        if (normalizedHP <= 0.75f)
            return Mathf.Max(1, shootBulletCountBelow75Percent);

        return Mathf.Max(1, shootBulletCount);
    }

    /// <summary>
    /// 下段攻撃を行い、確率判定に成功し、かつプレイヤーが前方にいる場合は上段攻撃へ派生します。
    /// </summary>
    private IEnumerator PerformNormalAttack()
    {
        bool willDoHighAttack = Random.value <= highAttackProbability;

        // 上段攻撃へ派生する場合、下段攻撃側の攻撃後待機をスキップする。
        yield return StartCoroutine(PerformLowAttack(willDoHighAttack));

        if (!willDoHighAttack)
            yield break;

        // 下段攻撃中にプレイヤーが背後へ回った場合は、上段攻撃へ派生せず終了する。
        if (!IsPlayerInFront())
            yield break;

        float beforeHighWait = GetCombatDuration(waitBeforeHighAttackDuration);
        yield return new WaitForSeconds(beforeHighWait);

        yield return StartCoroutine(PerformHighAttack());
    }

    /// <summary>
    /// Idle状態に移行し、下端から一定の座標をキープするようにスムーズに移動します。
    /// </summary>
    private IEnumerator TransitionToIdle()
    {
        DisableAllAttackDamage();
        CurrentState = BossState.Idle;

        // 下端からの目標Y座標を計算（X座標は現在の位置を維持）
        float targetY = areaBottomBound + idleHeightFromBottom;
        float duration = GetCombatDuration(idleTransitionDuration);

        if (_animator != null)
        {
            // 浮上にかかる移動時間（duration）と同じ秒数をかけて、徐々にIdleアニメーション状態へ遷移させます
            _animator.CrossFadeInFixedTime(_idleStateHash, duration);
        }

        // DOTweenを使用してスムーズに移動
        _moveTween = transform.DOMoveY(targetY, duration).SetEase(Ease.InOutQuad);
        yield return _moveTween.WaitForCompletion();
    }
    #endregion

    #region 攻撃パターン①：通常攻撃・下段 (Low Attack)
    /// <summary>
    /// LowAttack（下段攻撃）の一連のアクションを実行します。
    /// </summary>
    private IEnumerator PerformLowAttack(bool skipPostWait)
    {
        CurrentState = BossState.LowAttacking;

        float readyDuration = GetCombatDuration(lowAttackReadyDuration);
        float attackDur = GetCombatDuration(lowAttackDuration);
        float postWait = GetCombatDuration(postLowAttackWaitDuration);

        // --- 1. 準備フェーズ ---
        if (_animator != null)
        {
            SetAnimatorSpeed(_lowAttackReadySpeedHash, readyDuration);
            _animator.SetTrigger(_lowAttackReadyTriggerHash);
        }

        float targetY = areaBottomBound + lowAttackHeightFromBottom;
        _moveTween = transform.DOMoveY(targetY, readyDuration).SetEase(Ease.InOutQuad);
        yield return _moveTween.WaitForCompletion();

        // --- 2. 攻撃フェーズ ---
        if (lowAttackDamageController != null)
        {
            lowAttackDamageController.SetNormalDamage(lowAttackDamage);
        }

        if (_animator != null)
        {
            SetAnimatorSpeed(_lowAttackSpeedHash, attackDur);
            _animator.SetTrigger(_lowAttackTriggerHash);
        }

        // エフェクトを再生（子オブジェクトのまま）
        if (shootEffect != null)
        {
            shootEffect.Stop(); // 連射時に最初から再生されるよう一度停止する
            shootEffect.Play();
        }

        yield return new WaitForSeconds(attackDur);

        // --- 3. 攻撃後待機（リカバリー）フェーズ ---
        if (!skipPostWait)
        {
            yield return new WaitForSeconds(postWait);
        }

        _currentNextInterval = lowAttackNextInterval;
    }
    #endregion

    #region 攻撃パターン①：通常攻撃・上段派生 (High Attack)
    /// <summary>
    /// HighAttack（上段攻撃）の一連のアクションを実行します。
    /// </summary>
    private IEnumerator PerformHighAttack()
    {
        CurrentState = BossState.HighAttacking;

        float readyDuration = GetCombatDuration(highAttackReadyDuration);
        float attackDur = GetCombatDuration(highAttackDuration);
        float postWait = GetCombatDuration(postHighAttackWaitDuration);

        // --- 1. 準備フェーズ ---
        if (_animator != null)
        {
            SetAnimatorSpeed(_highAttackReadySpeedHash, readyDuration);
            _animator.SetTrigger(_normalHighAttackReadyTriggerHash);
        }

        float targetY = areaBottomBound + highAttackHeightFromBottom;
        _moveTween = transform.DOMoveY(targetY, readyDuration).SetEase(Ease.InOutQuad);
        yield return _moveTween.WaitForCompletion();

        // --- 2. 攻撃フェーズ ---
        if (highAttackDamageController != null)
        {
            highAttackDamageController.SetNormalDamage(highAttackDamage);
        }

        if (_animator != null)
        {
            SetAnimatorSpeed(_highAttackSpeedHash, attackDur);
            _animator.SetTrigger(_normalHighAttackTriggerHash);
        }

        if (shootEffect != null)
        {
            shootEffect.Stop();
            shootEffect.Play();
        }

        yield return new WaitForSeconds(attackDur);

        // --- 3. 攻撃後待機（リカバリー）フェーズ ---
        yield return new WaitForSeconds(postWait);

        _currentNextInterval = highAttackNextInterval;
    }
    #endregion

    #region 攻撃パターン②：突き攻撃 (Thrust Attack)
    /// <summary>
    /// ThrustAttack（突き攻撃）の一連のアクションを実行します。
    /// 剣先がプレイヤーに確実に当たるように、ボス本体の移動目標座標を逆算して突進します。
    /// </summary>
    private IEnumerator PerformThrustAttack()
    {
        CurrentState = BossState.ThrustAttacking;

        float readyDuration = GetCombatDuration(thrustReadyDuration);
        float attackDur = GetCombatDuration(thrustDuration);
        float postWait = GetCombatDuration(postThrustWaitDuration);

        // --- 1. 準備フェーズ ---
        if (_animator != null)
        {
            SetAnimatorSpeed(_thrustReadySpeedHash, readyDuration);
            _animator.SetTrigger(_thrustReadyTriggerHash);
        }

        // 突き攻撃準備のための特定高さへ移動（X座標は現在のまま維持）
        float readyY = areaBottomBound + thrustReadyHeightFromBottom;
        _moveTween = transform.DOMoveY(readyY, readyDuration).SetEase(Ease.InOutQuad);
        yield return _moveTween.WaitForCompletion();

        // --- 2. 構え終了後の目標座標計算フェーズ ---
        UpdatePlayerTransformReference();

        // 現在のボスの向きを判定（右向きなら1、左向きなら-1）
        int facingDir = _isFacingRight ? 1 : -1;

        // 剣先の現在のX座標を取得（未設定の場合はボス本体の座標を代用）
        float currentSwordTipX =
            swordTipTransform != null ? swordTipTransform.position.x : transform.position.x;

        // プレイヤーの目標X座標を決定（プレイヤーが未取得の場合は剣先から前方の最小距離先を仮置き）
        float playerX =
            _playerTransform != null
                ? _playerTransform.position.x
                : currentSwordTipX + (facingDir * minThrustDistance);

        // 剣先の前方方向を基準とした、プレイヤーとの水平距離差を計算
        float forwardDistance = (playerX - currentSwordTipX) * facingDir;
        float safeMinThrustDistance = Mathf.Max(0f, minThrustDistance);
        float safeOvershootDistance = Mathf.Max(0f, thrustOvershootDistance);
        float safeMaxTravelDistance = Mathf.Max(
            safeMinThrustDistance,
            thrustMaxTravelDistance
        );

        // プレイヤー位置を終点にせず、その先まで剣先を通過させる。
        // プレイヤーが近すぎる、または背後にいる場合も、現在の向きへ最低距離分は突進する。
        float desiredTravelDistance = Mathf.Max(
            safeMinThrustDistance,
            forwardDistance + safeOvershootDistance
        );
        float actualTravelDistance = Mathf.Min(desiredTravelDistance, safeMaxTravelDistance);
        float targetSwordTipX = currentSwordTipX + (facingDir * actualTravelDistance);

        // 剣の先（swordTipTransform）とボス本体の現在位置のオフセット（ズレ）を計算
        Vector3 swordOffset = Vector3.zero;
        if (swordTipTransform != null)
        {
            swordOffset = swordTipTransform.position - transform.position;
        }

        // 剣先が目標座標へ到達するように、本体の目標座標を逆算する。
        Vector3 targetBossPosition;
        targetBossPosition.x = targetSwordTipX - swordOffset.x;
        targetBossPosition.y =
            areaBottomBound + thrustAttackHeightFromBottom - swordOffset.y;
        targetBossPosition.z = transform.position.z;

        // ボス本体が行動可能エリアを越えないように制限する。
        float minimumBossX = areaLeftBound + wallMargin;
        float maximumBossX = areaRightBound - wallMargin;
        targetBossPosition.x = Mathf.Clamp(targetBossPosition.x, minimumBossX, maximumBossX);

        // --- 3. 攻撃（突進）フェーズ ---
        if (thrustDamageController != null)
        {
            thrustDamageController.SetNormalDamage(thrustDamage);
        }

        if (_animator != null)
        {
            SetAnimatorSpeed(_thrustSpeedHash, attackDur);
            _animator.SetTrigger(_thrustTriggerHash);
        }

        // エフェクトの切り離しと再生に必要なローカルTransform情報を保存する変数
        Vector3 effectOriginalLocalPos = Vector3.zero;
        Quaternion effectOriginalLocalRot = Quaternion.identity;
        Vector3 effectOriginalLocalScale = Vector3.one;

        if (thrustEffect != null)
        {
            // 親に戻すときのために元のローカル座標・回転・スケールを記録
            effectOriginalLocalPos = thrustEffect.transform.localPosition;
            effectOriginalLocalRot = thrustEffect.transform.localRotation;
            effectOriginalLocalScale = thrustEffect.transform.localScale;

            Vector3 moveDirection = targetBossPosition - transform.position;
            if (moveDirection.sqrMagnitude > 0.001f) // 念のため移動距離がゼロでないか確認
            {
                // 現在地から目標地点への角度を算出し、Z軸回転に適用
                float effectAngle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                thrustEffect.transform.rotation = Quaternion.Euler(0f, 0f, effectAngle);
            }
            else
            {
                // 移動距離がほぼ無い場合のフォールバック（左右の向きのみ合わせる）
                thrustEffect.transform.rotation = _isFacingRight
                    ? Quaternion.Euler(0f, 0f, 0f)
                    : Quaternion.Euler(0f, 180f, 0f);
            }

            // ボスの移動に追従しないよう、ワールド空間へ一時的に切り離す
            thrustEffect.transform.SetParent(null);

            // まさに突進を開始する瞬間に、その場でエフェクトを再生
            thrustEffect.Play();
        }

        // プレイヤー付近で停止・減速せず、最高速に近い状態で通過する。
        _moveTween = transform.DOMove(targetBossPosition, attackDur).SetEase(Ease.InOutCubic);
        yield return new WaitForSeconds(attackDur);

        // --- 4. 攻撃後待機（リカバリー）フェーズ ---
        yield return new WaitForSeconds(postWait);

        // --- 5. 終了処理（エフェクトを親に戻す） ---
        if (thrustEffect != null)
        {
            thrustEffect.Stop(); // 次回の使用に向けて念のため停止

            // 再びボスの子オブジェクトに戻し、記録しておいたローカルTransformを復元する
            thrustEffect.transform.SetParent(transform);
            thrustEffect.transform.localPosition = effectOriginalLocalPos;
            thrustEffect.transform.localRotation = effectOriginalLocalRot;
            thrustEffect.transform.localScale = effectOriginalLocalScale;
        }

        _currentNextInterval = thrustAttackNextInterval;
    }
    #endregion

    #region 攻撃パターン③：射撃攻撃 (Shoot Attack)
    /// <summary>
    /// ShootAttack（射撃攻撃）の一連のアクションを実行します。
    /// 指定された数の弾を、ランダムに生成した高さのオフセットから連射します。
    /// </summary>
    /// <param name="shootCount">発射する弾の最大個数</param>
    private IEnumerator PerformShootAttack(int shootCount)
    {
        CurrentState = BossState.ShootAttacking;

        float readyDuration = GetCombatDuration(shootReadyDuration);
        float attackDur = GetCombatDuration(shootAttackDuration);
        float bulletInterval = GetCombatDuration(shootBulletInterval);
        float postWait = GetCombatDuration(postShootWaitDuration);

        // --- 1. 準備フェーズ ---
        if (_animator != null)
        {
            SetAnimatorSpeed(_shootReadySpeedHash, readyDuration);
            _animator.SetTrigger(_shootReadyTriggerHash);
        }

        yield return new WaitForSeconds(readyDuration);

        // --- 2. オフセットリストの作成と抽出 ---
        List<float> allOffsets = new List<float>
        {
            -2f * shootBulletHeightOffset,
            -1f * shootBulletHeightOffset,
            0f,
            1f * shootBulletHeightOffset,
            2f * shootBulletHeightOffset,
        };

        // リストをランダムにシャッフルし、射撃パターンを多様化させる
        for (int i = 0; i < allOffsets.Count; i++)
        {
            int randomIndex = Random.Range(i, allOffsets.Count);
            float temp = allOffsets[i];
            allOffsets[i] = allOffsets[randomIndex];
            allOffsets[randomIndex] = temp;
        }

        int actualCount = Mathf.Min(shootCount, allOffsets.Count);
        List<float> targetYOffsets = allOffsets.GetRange(0, actualCount);

        // --- 3. 発射ループ ---
        UpdatePlayerTransformReference();
        int facingDir = _isFacingRight ? 1 : -1;

        foreach (float yOffset in targetYOffsets)
        {
            UpdatePlayerTransformReference();

            float currentSwordTipX =
                swordTipTransform != null ? swordTipTransform.position.x : transform.position.x;

            // 懐判定（プレイヤーがボスの背後に回った場合は射撃を中断する）
            bool shouldFire = true;
            if (_playerTransform != null)
            {
                float forwardDistance =
                    (_playerTransform.position.x - currentSwordTipX) * facingDir;
                if (forwardDistance <= 0f)
                {
                    shouldFire = false;
                }
            }

            // 懐に入られていた場合は、これ以降の射撃処理と postWait をすべてスキップして即座にコルーチンを抜ける
            if (!shouldFire)
            {
                _currentNextInterval = shootAttackNextInterval;
                yield break;
            }

            // アニメーションの再生速度には「攻撃時間（shootAttackDuration）」を指定
            if (_animator != null)
            {
                SetAnimatorSpeed(_shootSpeedHash, attackDur);
                _animator.SetTrigger(_shootTriggerHash);
            }

            // エフェクトを再生（子オブジェクトのまま）
            if (shootEffect != null)
            {
                shootEffect.Stop(); // 連射時に最初から再生されるよう一度停止する
                shootEffect.Play();
            }

            PlayAirBurstEffect(attackDur + bulletInterval);

            // 弾の発射
            FireShootBullet(yOffset, facingDir, currentSwordTipX);

            yield return new WaitForSeconds(attackDur); // 攻撃フェーズ自体の時間を待機

            // 次の弾を発射するまでの待機には「発射間隔（shootBulletInterval）」を使用
            yield return new WaitForSeconds(bulletInterval);
        }

        // --- 4. 攻撃後待機（リカバリー）フェーズ ---
        yield return new WaitForSeconds(postWait);

        _currentNextInterval = shootAttackNextInterval;
    }

    /// <summary>
    /// 次弾までに1秒基準のAirBurstアニメーションが完了するよう速度を調整し、再生します。
    /// </summary>
    private void PlayAirBurstEffect(float actualShotInterval)
    {
        if (airBurstEffectAnimator == null)
            return;

        float safeShotInterval = Mathf.Max(0.01f, actualShotInterval);
        airBurstEffectAnimator.SetFloat(_airBurstSpeedHash, 1f / safeShotInterval);
        airBurstEffectAnimator.gameObject.SetActive(true);
        airBurstEffectAnimator.SetTrigger(_airBurstTriggerHash);
    }

    /// <summary>
    /// Shoot攻撃用の弾を生成・発射します。
    /// </summary>
    /// <param name="yOffset">プレイヤーに対するY座標オフセット</param>
    /// <param name="facingDir">現在のボスの向き（1 or -1）</param>
    /// <param name="startX">弾の生成X座標（剣先）</param>
    private void FireShootBullet(float yOffset, int facingDir, float startX)
    {
        // 発射位置の決定（Y座標も剣先を基準にする）
        float startY =
            swordTipTransform != null ? swordTipTransform.position.y : transform.position.y;
        Vector3 spawnPos = new Vector3(startX, startY, 0f);

        // ターゲット位置の計算
        Vector3 targetPos = Vector3.zero;
        if (_playerTransform != null)
        {
            targetPos = _playerTransform.position + new Vector3(0f, yOffset, 0f);
        }
        else
        {
            // プレイヤーがいない場合は前方へ飛ばす
            targetPos = spawnPos + new Vector3(facingDir * 10f, yOffset, 0f);
        }

        // プールから弾を取得して生成
        GameObject bullet = ObjectPooler.SceneInstance.SpawnFromPool(
            SHOOT_BULLET_POOLTAG,
            spawnPos,
            Quaternion.identity
        );

        if (bullet != null)
        {
            // 進行方向ベクトル
            Vector2 direction = (targetPos - spawnPos).normalized;

            // 弾の回転角度設定
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, angle);

            // 速度の適用
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = direction * shootBulletSpeed;
            }

            // 攻撃力の設定
            var damageController = bullet.GetComponent<ContactDamageController>();
            if (damageController != null)
            {
                damageController.SetNormalDamage(shootDamage);
            }
        }
    }
    #endregion

    #region 攻撃パターン④：後退テレポート攻撃 (Retreat Teleport)
    /// <summary>
    /// 後退しながら瞬間移動し、中間地点で攻撃を行う一連のアクションを実行します。
    /// 広い空間がある方向へ後退し、壁際に到達した場合はエリア内に留まるよう補正します。
    /// </summary>
    /// <param name="teleportCount">中間地点で攻撃を行う回数</param>
    private IEnumerator PerformRetreatTeleport(int teleportCount)
    {
        CurrentState = BossState.RetreatTeleporting;

        float initialFadeTime = GetCombatDuration(retreatInitialFadeOutTime);
        float appearTime = GetCombatDuration(retreatHologramAppearTime);
        float attackDur = GetCombatDuration(retreatAttackDuration);
        float disappearTime = GetCombatDuration(retreatHologramDisappearTime);

        // --- 1. 広い方向の判定と向きの固定 ---
        float distToLeft = transform.position.x - areaLeftBound;
        float distToRight = areaRightBound - transform.position.x;

        // 端からの距離が遠い方（広い空間がある方）を選ぶ
        bool retreatToRight = distToRight >= distToLeft;

        // 後退方向とは「逆」を常に向き続けるように固定する（右に逃げるなら左向き）
        UpdateFacingDirection(!retreatToRight);

        // --- 2. 最終移動X座標の計算 ---
        float startX = transform.position.x;
        float finalX;

        // 規定の距離分だけ後退した座標を計算し、エリアの境界にマージンを加えた位置を越えないよう Clamp (Min/Max) を行います。
        if (retreatToRight)
        {
            float targetX = startX + retreatDistance;
            float wallLimitX = areaRightBound - wallMargin;
            // 右へ進むので、値が小さい（エリア内に収まる）方を採用
            finalX = Mathf.Min(targetX, wallLimitX);
        }
        else
        {
            float targetX = startX - retreatDistance;
            float wallLimitX = areaLeftBound + wallMargin;
            // 左へ進むので、値が大きい（エリア内に収まる）方を採用
            finalX = Mathf.Max(targetX, wallLimitX);
        }

        // --- 3. 最初の消滅演出 ---
        Sequence fadeOutSeq = DOTween.Sequence();
        foreach (var renderer in hologramTargetRenderers)
        {
            if (renderer != null && renderer.material != null)
            {
                Material mat = renderer.material;
                mat.EnableKeyword("_HOLOGRAM_ON");
                mat.SetFloat("_HologramBlend", 1.0f);

                fadeOutSeq.Join(renderer.DOFade(0f, initialFadeTime));
            }
        }
        yield return fadeOutSeq.SetEase(Ease.OutCubic).WaitForCompletion();
        BeginTeleportAfterImage();

        // --- 4. 瞬間移動と攻撃のループ ---
        // 攻撃回数 + 1(最後の出現用) で等分することで、1回目の出現を1区画先から開始する
        float stepX = (finalX - startX) / (teleportCount + 1);

        for (int i = 1; i <= teleportCount; i++)
        {
            // 座標の決定
            float currentTargetX = startX + stepX * i;
            float currentHeight = 0f;
            if (retreatHeights != null && retreatHeights.Length > 0)
            {
                currentHeight = retreatHeights[Random.Range(0, retreatHeights.Length)];
            }
            float currentTargetY = areaBottomBound + currentHeight;

            transform.position = new Vector3(currentTargetX, currentTargetY, transform.position.z);

            // 最初の攻撃地点へ透明なまま移動した時点で、プレイヤーが背後なら攻撃を中断する。
            // この後の最終出現処理へ進むため、ここでは表示や攻撃アニメーションを開始しない。
            if (i == 1 && !IsPlayerInFront())
            {
                DisableAllAttackDamage();
                break;
            }

            ResumeDynamicAfterImage();

            // 出現に合わせたアニメーション (Ready)
            if (_animator != null)
            {
                SetAnimatorSpeed(_horizontalAttackReadySpeedHash, appearTime);
                _animator.SetTrigger(_horizontalAttackReadyTriggerHash);
            }

            // ホログラムによる出現演出
            Sequence appearSeq = DOTween.Sequence();
            foreach (var renderer in hologramTargetRenderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    Material mat = renderer.material;
                    mat.EnableKeyword("_HOLOGRAM_ON");
                    mat.SetFloat("_HologramBlend", 1.0f);

                    // 透明度をパッと実体に戻す
                    Color c = renderer.color;
                    c.a = 1f;
                    renderer.color = c;

                    // ホログラムから実体へとブレンドさせる
                    appearSeq.Join(mat.DOFloat(0f, "_HologramBlend", appearTime));
                }
            }

            yield return appearSeq.WaitForCompletion();

            // 実体化後は念のためキーワードを無効化
            foreach (var renderer in hologramTargetRenderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    renderer.material.DisableKeyword("_HOLOGRAM_ON");
                }
            }

            // 攻撃実行と待機
            if (_animator != null)
            {
                SetAnimatorSpeed(_horizontalAttackSpeedHash, attackDur);
                _animator.SetTrigger(_horizontalAttackTriggerHash);
            }

            if (shootEffect != null)
            {
                shootEffect.Stop();
                shootEffect.Play();
            }

            yield return new WaitForSeconds(attackDur);

            // 再びホログラム演出で消滅
            Sequence disappearSeq = DOTween.Sequence();
            foreach (var renderer in hologramTargetRenderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    Material mat = renderer.material;
                    mat.EnableKeyword("_HOLOGRAM_ON");

                    disappearSeq.Join(mat.DOFloat(1.0f, "_HologramBlend", disappearTime));
                    disappearSeq.Join(renderer.DOFade(0f, disappearTime));
                }
            }
            yield return disappearSeq.SetEase(Ease.OutCubic).WaitForCompletion();
            BeginTeleportAfterImage();
        }

        // --- 5. 最後の出現（攻撃なしでIdleへ戻る） ---
        transform.position = new Vector3(
            finalX,
            areaBottomBound + idleHeightFromBottom,
            transform.position.z
        );
        ResumeDynamicAfterImage();

        Sequence finalAppearSeq = DOTween.Sequence();
        foreach (var renderer in hologramTargetRenderers)
        {
            if (renderer != null && renderer.material != null)
            {
                Material mat = renderer.material;
                mat.EnableKeyword("_HOLOGRAM_ON");
                mat.SetFloat("_HologramBlend", 1.0f);

                Color c = renderer.color;
                c.a = 1f;
                renderer.color = c;

                finalAppearSeq.Join(mat.DOFloat(0f, "_HologramBlend", appearTime));
            }
        }

        // 最終出現時はそのままIdleへ滑らかにクロスフェード
        if (_animator != null)
        {
            _animator.CrossFadeInFixedTime(_idleStateHash, appearTime);
        }

        yield return finalAppearSeq.WaitForCompletion();

        // 最終クリーンアップ
        foreach (var renderer in hologramTargetRenderers)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.DisableKeyword("_HOLOGRAM_ON");
            }
        }

        EndTeleportAfterImageSequence();
        CurrentState = BossState.Idle;
        _currentNextInterval = retreatTeleportNextInterval;
    }

    /// <summary>
    /// アニメーションイベントから呼び出され、WindEffectを発射します。
    /// （後退テレポート攻撃中にのみ発動）
    /// </summary>
    public void FireWindEffect()
    {
        if (CurrentState != BossState.RetreatTeleporting)
        {
            return; // 現在の状態が後退瞬間移動中でない場合は発射しない
        }

        GameObject effectObj = GetWindEffectFromPool();
        if (effectObj == null)
            return;

        Chapter3BossWindEffect windEffect = effectObj.GetComponent<Chapter3BossWindEffect>();
        if (windEffect == null)
        {
            Debug.LogWarning(
                "WindEffectプレハブにChapter3BossWindEffectスクリプトがアタッチされていません。"
            );
            return;
        }

        // 発射位置（X, Y両方ともSwordTipの座標にする。未設定ならボス本体）
        Vector3 spawnPos =
            swordTipTransform != null ? swordTipTransform.position : transform.position;

        // 目標となる端のX座標を、現在ボスが向いている方向から決定
        float targetX = _isFacingRight ? areaRightBound : areaLeftBound;

        // 速度と距離から移動時間(Duration)を逆算する
        float distance = Mathf.Abs(targetX - spawnPos.x);

        // 距離 ÷ 速度 ＝ 到達にかかる時間
        float calculatedDuration = distance / Mathf.Max(0.1f, windEffectSpeed); // ゼロ除算防止

        // エフェクトのセットアップと発射
        windEffect.Setup(
            startPos: spawnPos,
            targetX: targetX,
            duration: calculatedDuration,
            damage: windEffectDamage,
            isFacingRight: _isFacingRight
        );
    }
    #endregion

    #region 攻撃パターン⑤：突進コンボ (Rush Combo)
    /// <summary>
    /// 前進しながら3連撃（High → Upper → High）を叩き込む一連のアクションを実行します。
    /// 移動距離が壁を超える場合は補正し、その距離を3等分してステップ前進を行います。
    /// </summary>
    private IEnumerator PerformRushComboAttack()
    {
        CurrentState = BossState.RushComboAttacking;

        float readyDur = GetCombatDuration(advanceReadyDuration);
        float attackDur = GetCombatDuration(advanceAttackDuration);
        float waitDur = GetCombatDuration(advanceWaitDuration);

        // 移動にかかる実際の時間（攻撃時間 × 指定割合）。
        // 攻撃時間を越えない範囲で最低移動時間を保証し、瞬間移動と判定抜けを抑える。
        float minimumMoveDuration = Mathf.Min(
            Mathf.Max(0.01f, comboMinimumMoveDuration),
            attackDur
        );
        float moveDur = Mathf.Clamp(
            attackDur * advanceMoveTimeRatio,
            minimumMoveDuration,
            attackDur
        );

        // --- 1. 進行方向の決定と目標座標の計算 ---
        UpdatePlayerTransformReference();

        // プレイヤーの位置を元に向きを更新する（プレイヤーがいない場合は現在の向きを維持）
        if (_playerTransform != null)
        {
            UpdateFacingDirection(_playerTransform.position.x > transform.position.x);
        }
        int facingDir = _isFacingRight ? 1 : -1;

        float startX = transform.position.x;
        // 理論上の最終移動座標（3回分の前進距離）
        float theoreticalFinalX = startX + (facingDir * advanceDistancePerHit * 3);
        float finalX;

        // 壁からのマージンを考慮し、近い方（壁を越えない位置）を最終座標として決定
        if (_isFacingRight)
        {
            float wallLimitX = areaRightBound - wallMargin;
            finalX = Mathf.Min(theoreticalFinalX, wallLimitX);
        }
        else
        {
            float wallLimitX = areaLeftBound + wallMargin;
            finalX = Mathf.Max(theoreticalFinalX, wallLimitX);
        }

        // 3等分した移動距離（1回あたりの実際のステップ幅）
        float stepX = (finalX - startX) / 3f;
        float targetY = areaBottomBound + advanceHeightFromBottom;

        // --- 2. 準備フェーズ ---
        if (_animator != null)
        {
            SetAnimatorSpeed(_highAttackReadySpeedHash, readyDur);
            ResetRushComboAttackTriggers();
            _animator.SetTrigger(_comboHighAttackReadyTriggerHash);

            // TriggerをAnimatorに消費させ、Ready_High_Comboへの遷移を確定してから移動を開始する。
            yield return null;
        }

        // 指定の高さへ移動しながら待機
        _moveTween = transform.DOMoveY(targetY, readyDur).SetEase(Ease.InOutQuad);
        yield return _moveTween.WaitForCompletion();

        // --- 3. 攻撃フェーズ（3連撃） ---

        // 【1撃目：HighAttack】
        float targetX1 = startX + stepX * 1;

        if (highAttackDamageController != null)
            highAttackDamageController.SetNormalDamage(highAttackDamage);
        if (_animator != null)
        {
            SetAnimatorSpeed(_highAttackSpeedHash, attackDur);
            ResetRushComboAttackTriggers();
            _animator.SetTrigger(_comboHighAttackTriggerHash);
        }
        if (shootEffect != null)
        {
            shootEffect.Stop();
            shootEffect.Play();
        }

        yield return StartCoroutine(
            PerformRushComboStepMovement(
                targetX1,
                moveDur,
                attackDur,
                highAttackDamageController
            )
        );
        yield return new WaitForSeconds(waitDur); // インターバル待機

        // 【2撃目：UpperAttack】
        float targetX2 = startX + stepX * 2;

        if (upperAttackDamageController != null)
            upperAttackDamageController.SetNormalDamage(upperAttackDamage);
        if (_animator != null)
        {
            SetAnimatorSpeed(_upperAttackSpeedHash, attackDur);
            ResetRushComboAttackTriggers();
            _animator.SetTrigger(_upperAttackTriggerHash);
        }
        if (shootEffect != null)
        {
            shootEffect.Stop();
            shootEffect.Play();
        }

        yield return StartCoroutine(
            PerformRushComboStepMovement(
                targetX2,
                moveDur,
                attackDur,
                upperAttackDamageController
            )
        );
        yield return new WaitForSeconds(waitDur);

        // 【3撃目：HighAttack】
        float targetX3 = startX + stepX * 3; // (ほぼfinalXと一致)

        if (highAttackDamageController != null)
            highAttackDamageController.SetNormalDamage(highAttackDamage);
        if (_animator != null)
        {
            SetAnimatorSpeed(_highAttackSpeedHash, attackDur);
            ResetRushComboAttackTriggers();
            _animator.SetTrigger(_comboHighAttackTriggerHash);
        }
        if (shootEffect != null)
        {
            shootEffect.Stop();
            shootEffect.Play();
        }

        yield return StartCoroutine(
            PerformRushComboStepMovement(
                targetX3,
                moveDur,
                attackDur,
                highAttackDamageController
            )
        );

        // 3撃目の後のリカバリー（インターバル）待機
        yield return new WaitForSeconds(waitDur);

        _currentNextInterval = rushComboNextInterval;
    }

    /// <summary>
    /// 突進コンボ用のAny State Triggerを毎回初期化し、前回の連撃から残った遷移要求を防ぎます。
    /// </summary>
    private void ResetRushComboAttackTriggers()
    {
        _animator.ResetTrigger(_comboHighAttackReadyTriggerHash);
        _animator.ResetTrigger(_comboHighAttackTriggerHash);
        _animator.ResetTrigger(_upperAttackTriggerHash);
    }

    private IEnumerator PerformRushComboStepMovement(
        float targetX,
        float moveDuration,
        float attackDuration,
        ContactDamageController damageController
    )
    {
        _moveTween = transform.DOMoveX(targetX, moveDuration).SetEase(Ease.OutQuad);
        yield return _moveTween.WaitForCompletion();

        float remainingAttackTime = Mathf.Max(0f, attackDuration - moveDuration);
        if (remainingAttackTime > 0f)
            yield return new WaitForSeconds(remainingAttackTime);

        DisableAttackDamage(damageController);
    }
    #endregion

    #region 攻撃パターン⑥：幻影強襲攻撃 (Mirage Assault)
    /// <summary>
    /// プレイヤーの周囲をテレポートで撹乱し、最終的にLowAttackまたはHorizontalAttackで奇襲するアクションを実行します。
    /// 最終攻撃の種別は確率（mirageLowAttackProbability）で分岐します。
    /// </summary>
    private IEnumerator PerformMirageAssault()
    {
        CurrentState = BossState.MirageAssaultAttacking;
        UpdatePlayerTransformReference();

        // 最終攻撃の種別を確率で決定
        bool isLowAttack = Random.value <= mirageLowAttackProbability;

        // テレポート回数の決定（構えフェーズと最終攻撃フェーズがあるため、最低2回以上にする）
        int teleportCount = Random.Range(mirageMinTeleportCount, mirageMaxTeleportCount + 1);
        if (teleportCount < 2)
            teleportCount = 2;

        float initialFadeTime = GetCombatDuration(mirageInitialFadeOutTime);

        // 初期の消滅演出
        Sequence initialFadeSeq = DOTween.Sequence();
        foreach (var renderer in hologramTargetRenderers)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.EnableKeyword("_HOLOGRAM_ON");
                renderer.material.SetFloat("_HologramBlend", 1.0f);
                initialFadeSeq.Join(renderer.DOFade(0f, initialFadeTime));
            }
        }
        yield return initialFadeSeq.SetEase(Ease.OutCubic).WaitForCompletion();
        BeginTeleportAfterImage();

        // 瞬間移動ループ
        for (int i = 0; i < teleportCount; i++)
        {
            bool isFinalAttack = (i == teleportCount - 1);
            bool isReadyPhase = (i == teleportCount - 2);

            UpdatePlayerTransformReference();
            float playerX =
                _playerTransform != null ? _playerTransform.position.x : transform.position.x;

            float targetX = 0f;
            float targetY = 0f;

            // X座標の決定（プレイヤーの左右どちらに出現するかをランダムに決定）
            float dir = Random.value > 0.5f ? 1f : -1f;

            if (isFinalAttack)
            {
                // 最終攻撃の座標計算（規定の距離と高さ）
                targetX = playerX + (dir * mirageFinalAttackDistance);

                // 壁際補正（エリア外にめり込む場合は反対側に配置する）
                if (targetX < areaLeftBound + wallMargin)
                {
                    targetX = playerX + mirageFinalAttackDistance;
                }
                else if (targetX > areaRightBound - wallMargin)
                {
                    targetX = playerX - mirageFinalAttackDistance;
                }

                // 決定された攻撃方法に基づいて高さを適用
                targetY =
                    areaBottomBound
                    + (isLowAttack ? lowAttackHeightFromBottom : horizontalAttackHeightFromBottom);
            }
            else
            {
                // 通常テレポートおよび構えフェーズの座標計算（ランダムな距離と高さ）
                float dist = Random.Range(mirageMinDistanceFromPlayer, mirageMaxDistanceFromPlayer);
                targetX = playerX + (dir * dist);

                // 壁際補正
                if (targetX < areaLeftBound + wallMargin)
                {
                    targetX = playerX + dist;
                }
                else if (targetX > areaRightBound - wallMargin)
                {
                    targetX = playerX - dist;
                }

                targetY =
                    areaBottomBound
                    + Random.Range(mirageMinHeightFromBottom, mirageMaxHeightFromBottom);
            }

            // 座標の適用と向きの固定（常にプレイヤー側を向く）
            transform.position = new Vector3(targetX, targetY, transform.position.z);
            UpdateFacingDirection(playerX > transform.position.x);
            ResumeDynamicAfterImage();

            if (isFinalAttack)
            {
                // --- 最終攻撃フェーズ ---

                // ホログラムを解除し、透明度を1に戻して実体化する
                foreach (var renderer in hologramTargetRenderers)
                {
                    if (renderer != null && renderer.material != null)
                    {
                        renderer.material.DisableKeyword("_HOLOGRAM_ON");
                        Color c = renderer.color;
                        c.a = 1f;
                        renderer.color = c;
                    }
                }

                // 決定していた攻撃を実行
                if (isLowAttack)
                {
                    float finalAttackDuration = GetCombatDuration(lowAttackDuration);
                    float postWaitDuration = GetCombatDuration(miragePostWaitDuration);

                    if (lowAttackDamageController != null)
                        lowAttackDamageController.SetMaxHPRatioDamage(
                            MIRAGE_ATTACK_MAX_HP_RATIO
                        );
                    if (_animator != null)
                    {
                        SetAnimatorSpeed(_lowAttackSpeedHash, finalAttackDuration);
                        _animator.SetTrigger(_lowAttackTriggerHash);
                    }
                    yield return new WaitForSeconds(finalAttackDuration);
                    yield return new WaitForSeconds(postWaitDuration);
                }
                else
                {
                    float finalAttackDuration = GetCombatDuration(horizontalAttackDuration);
                    float postWaitDuration = GetCombatDuration(miragePostWaitDuration);

                    if (horizontalAttackDamageController != null)
                        horizontalAttackDamageController.SetMaxHPRatioDamage(
                            MIRAGE_ATTACK_MAX_HP_RATIO
                        );
                    if (_animator != null)
                    {
                        SetAnimatorSpeed(_horizontalAttackSpeedHash, finalAttackDuration);
                        _animator.SetTrigger(_horizontalAttackTriggerHash);
                    }
                    yield return new WaitForSeconds(finalAttackDuration);
                    yield return new WaitForSeconds(postWaitDuration);
                }
            }
            else if (isReadyPhase)
            {
                // --- 構えフェーズ ---

                float appearDuration = GetCombatDuration(mirageAppearTime, 0.05f);
                float stayDuration = GetCombatDuration(mirageStayTime, 0.05f);
                float disappearDuration = GetCombatDuration(mirageDisappearTime, 0.05f);
                float totalReadyTime = appearDuration + stayDuration + disappearDuration;

                // 構えアニメーションの再生（決定した最終攻撃に合わせて構えを変更）
                if (_animator != null)
                {
                    if (isLowAttack)
                    {
                        SetAnimatorSpeed(_lowAttackReadySpeedHash, totalReadyTime);
                        _animator.SetTrigger(_lowAttackReadyTriggerHash);
                    }
                    else
                    {
                        SetAnimatorSpeed(_horizontalAttackReadySpeedHash, totalReadyTime);
                        _animator.SetTrigger(_horizontalAttackReadyTriggerHash);
                    }
                }

                // 構え中のホログラム演出（現れる → 留まる → 消える）
                Sequence readySeq = DOTween.Sequence();
                foreach (var renderer in hologramTargetRenderers)
                {
                    if (renderer != null && renderer.material != null)
                    {
                        renderer.material.EnableKeyword("_HOLOGRAM_ON");
                        renderer.material.SetFloat("_HologramBlend", 1.0f);

                        readySeq.Insert(
                            0f,
                            renderer.DOFade(mirageMaxAlpha, appearDuration).SetEase(Ease.OutQuad)
                        );
                        readySeq.Insert(
                            appearDuration + stayDuration,
                            renderer.DOFade(0f, disappearDuration).SetEase(Ease.InQuad)
                        );
                    }
                }
                yield return readySeq.WaitForCompletion();
                BeginTeleportAfterImage();

                yield return new WaitForSeconds(GetCombatDuration(mirageIntervalTime));
            }
            else
            {
                // --- 通常テレポートフェーズ ---

                if (_animator != null)
                {
                    _animator.CrossFadeInFixedTime(_idleStateHash, 0f);
                }

                Sequence teleSeq = DOTween.Sequence();
                float appearDuration = GetCombatDuration(mirageAppearTime, 0.05f);
                float stayDuration = GetCombatDuration(mirageStayTime, 0.05f);
                float disappearDuration = GetCombatDuration(mirageDisappearTime, 0.05f);
                foreach (var renderer in hologramTargetRenderers)
                {
                    if (renderer != null && renderer.material != null)
                    {
                        renderer.material.EnableKeyword("_HOLOGRAM_ON");
                        renderer.material.SetFloat("_HologramBlend", 1.0f);

                        teleSeq.Insert(
                            0f,
                            renderer.DOFade(mirageMaxAlpha, appearDuration).SetEase(Ease.OutQuad)
                        );
                        teleSeq.Insert(
                            appearDuration + stayDuration,
                            renderer.DOFade(0f, disappearDuration).SetEase(Ease.InQuad)
                        );
                    }
                }
                yield return teleSeq.WaitForCompletion();
                BeginTeleportAfterImage();

                yield return new WaitForSeconds(GetCombatDuration(mirageIntervalTime));
            }
        }

        EndTeleportAfterImageSequence();
        _currentNextInterval = mirageAssaultNextInterval;
    }
    #endregion
}
