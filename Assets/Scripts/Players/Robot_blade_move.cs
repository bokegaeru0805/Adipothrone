using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class Robot_blade_move : MonoBehaviour
{
    private struct CooldownEntry
    {
        // public GameObject enemy; // GameObject参照はGC対象になりうる
        public int enemyInstanceID; // 代わりにInstanceIDを使う
        public float timer;
    }

    private PlayerEffectManager playerEffectManager;
    private PlayerManager playerManager;
    public float BladeLength { get; private set; } = 0;

    [Header("Faboの剣にはKinematicなRigidbody2Dが必要")]
    [SerializeField]
    private GameObject RobotObject; //ロボットのオブジェクト

    [SerializeField]
    private GameObject RobotBladeParticle;

    [SerializeField, Tooltip("非ボスヒット時に追加再生するエフェクトの数")]
    private int subHitEffectCount = 3;

    [SerializeField, Tooltip("非ボスヒット時の追加エフェクトが散らばる半径")]
    private float subHitEffectSpawnRadius = 1.5f;

    // 敵ごとのクールタイムタイマー
    // private Dictionary<GameObject, float> enemyCooldowns = new Dictionary<GameObject, float>();
    private List<CooldownEntry> enemyCooldownsList = new List<CooldownEntry>(32);
    private int bladePower = 0; //剣そのものの攻撃力
    private float cooldownTime = 1.0f; // クールタイム（秒）
    public float attackTime { get; private set; } = 1.0f;
    public float moveTime { get; private set; } = 1.0f;
    private float wpCost = 0f; // WP消費量
    private bool rightFlag = true;
    private bool _isInBossBattle = false; // ボス戦闘中かどうか
    public bool isStarted { get; private set; } = false; //生成が完了したかどうか
    private string hitEffectPoolTag = "HitEffect1"; // ヒットエフェクトのプールタグ
    private string subHitEffectPoolTag = "HitEffect2"; // サブ弾ヒットエフェクトのプールタグ
    private Sprite sprite;
    private Vector2 newColliderOffset = Vector2.zero;
    private Vector2 newColliderSize = Vector2.zero;
    private CapsuleCollider2D capsuleCollider;
    private SpriteRenderer spriteRenderer;
    private CriWare.Assets.CriAtomSePlayer sePlayer;
    private Robot_move robotMoveScript;
    private BladeWeaponData attack;

    private void Awake()
    {
        capsuleCollider = this.gameObject.GetComponent<CapsuleCollider2D>();
        spriteRenderer = this.gameObject.GetComponent<SpriteRenderer>();
        sePlayer = this.gameObject.GetComponent<CriWare.Assets.CriAtomSePlayer>();

        if (RobotObject == null)
        {
            RobotObject = transform.parent.gameObject;
            if (RobotObject == null)
            {
                Debug.LogError("RobotObjectが設定されていません。");
            }
        }

        robotMoveScript = RobotObject.GetComponent<Robot_move>();
        if (robotMoveScript == null)
        {
            Debug.LogError("Robot_moveスクリプトが見つかりません。");
        }
    }

    /// <summary>
    /// Robot_moveから武器データを受け取り、自身のパラメータを更新する
    /// </summary>
    public void SetBladeData(BladeWeaponData data)
    {
        attack = data; // BladeWeaponDataをattack変数にキャッシュ
        if (attack != null)
        {
            sprite = attack.itemSprite;
            spriteRenderer.sprite = sprite;
            bladePower = attack.power;
            wpCost = attack.wpCost;
            cooldownTime = attack.cooldownTime;
            attackTime = attack.attackTime;
            newColliderOffset = attack.ColliderOffset;
            newColliderSize = attack.ColliderSize;

            capsuleCollider.offset = newColliderOffset;
            capsuleCollider.size = newColliderSize;
            RobotBladeParticle.GetComponent<RobotBladeParticle>().BladeLenght = attack
                .ColliderSize
                .x;
        }
        else
        {
            Debug.LogWarning($"Bladeデータがnullです。");
        }
    }

    private void Start()
    {
        playerEffectManager = PlayerEffectManager.instance;
        if (playerEffectManager == null)
        {
            Debug.LogError("PlayerEffectManagerが見つかりません。ロボットの剣の動きに影響します。");
        }

        playerManager = PlayerManager.instance;
        if (playerManager == null)
        {
            Debug.LogError("PlayerManagerが見つかりません。ロボットの剣の動きに影響します。");
        }
    }

    private void FixedUpdate()
    {
        // 初期化が完了していない場合は何もしない
        if (!GameManager.isFirstGameSceneOpen)
            return;

        // 攻撃中でない（コライダーが無効）の時だけ、向きの追従を行う（FixedUpdateで毎フレーム角度を更新する場合）
        if (!capsuleCollider.enabled)
        {
            UpdateBladeRotationWhenIdle();
        }

        // // タイマーを減らす（必要に応じてクリア）
        // foreach (var key in enemyCooldowns.Keys.ToList())
        // {
        //     enemyCooldowns[key] -= Time.fixedDeltaTime;
        //     if (enemyCooldowns[key] <= 0f)
        //     {
        //         enemyCooldowns.Remove(key);
        //     }
        // }
        for (int i = enemyCooldownsList.Count - 1; i >= 0; i--)
        {
            CooldownEntry entry = enemyCooldownsList[i]; // structなので値コピー
            entry.timer -= Time.fixedDeltaTime;

            if (entry.timer <= 0f)
            {
                enemyCooldownsList.RemoveAt(i); // 後ろからの削除は高速
            }
            else
            {
                enemyCooldownsList[i] = entry; // 時間を書き戻す
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        IDamageable hpScript = collision.GetComponent<IDamageable>();
        if (hpScript == null)
        {
            return; // IDamageableがない場合は何もしない
        }

        MonoBehaviour mb = hpScript as MonoBehaviour;
        if (mb.enabled == false)
        {
            return; // IDamageableが無効化されている場合は何もしない
        }

        // GameObject enemy = collision.gameObject;

        // // まだクールタイム中なら何もしない
        // if (enemyCooldowns.ContainsKey(enemy))
        //     return;

        // // クールタイム開始
        // enemyCooldowns[enemy] = cooldownTime;

        GameObject enemy = collision.gameObject;
        int enemyID = enemy.GetInstanceID(); // 敵のInstanceIDを取得

        // まだクールタイム中かチェック (ここがO(N)になるが、GCよりはマシ)
        bool onCooldown = false;
        foreach (var entry in enemyCooldownsList)
        {
            // if (entry.enemy == enemy) // GameObjectの比較は重い
            if (entry.enemyInstanceID == enemyID) // IDでの比較
            {
                onCooldown = true;
                break;
            }
        }
        if (onCooldown)
            return;

        // クールタイム開始
        enemyCooldownsList.Add(
            new CooldownEntry { enemyInstanceID = enemyID, timer = cooldownTime }
        );

        // 衝突点（自分のCollider上の、collisionに最も近い点）
        Vector2 contactPoint = capsuleCollider.ClosestPoint(collision.transform.position);

        // ヒットエフェクトの再生
        // 永続プール(PersistentInstance)がnullでないか確認
        if (ObjectPooler.PersistentInstance != null && !string.IsNullOrEmpty(hitEffectPoolTag))
        {
            // 衝突点（弾の位置）を取得
            Vector2 hitPosition = contactPoint;

            // ObjectPooler の永続インスタンスから、指定した「タグ」のエフェクトを呼び出す
            ObjectPooler.PersistentInstance.SpawnFromPool(
                hitEffectPoolTag, // プレハブの代わりに「タグ」を渡す
                hitPosition, // 座標
                Quaternion.identity // 回転
            );

            if (!_isInBossBattle && !string.IsNullOrEmpty(subHitEffectPoolTag))
            {
                // ボス戦闘中でなければ、指定した回数だけサブエフェクトをランダムな位置に再生
                for (int i = 0; i < subHitEffectCount; i++)
                {
                    // hitPosition の周囲（半径 subHitEffectSpawnRadius 内）にランダムな座標を生成
                    // (Random.insideUnitCircle は Vector2(x, y) を返す)
                    Vector2 randomOffset = Random.insideUnitCircle * subHitEffectSpawnRadius;
                    Vector2 spawnPosition = hitPosition + randomOffset;

                    // プールからサブエフェクトを再生
                    ObjectPooler.PersistentInstance.SpawnFromPool(
                        subHitEffectPoolTag,
                        spawnPosition, // ランダム化された座標
                        Quaternion.identity
                    );
                }
            }
        }

        //様々な効果を考慮した攻撃力を計算
        int damageSumAmount =
            playerEffectManager?.CalculateFinalAttackPower(bladePower) ?? bladePower;

        //ダメージ量を指定
        hpScript.Damage(damageSumAmount);
        sePlayer.Play(SE_EnemyAction.Damage2); //敵ダメージSEを再生

        if (wpCost > 0)
        {
            // WPを消費
            playerManager?.AddWpConsumptionBuffer(wpCost);
        }
    }

    /// <summary>
    /// Robot_moveから剣の振り状態の変更通知を受け取ったときの処理
    /// </summary>
    /// <param name="isSwinging">新しい状態</param>
    private void HandleBladeSwingingChanged(bool isSwinging)
    {
        // FixedUpdateで行っていたコライダーの切り替えを、イベント発生時に即座に行う
        if (isSwinging)
        {
            if (!capsuleCollider.enabled)
            {
                capsuleCollider.enabled = true; //当たり判定を得る
            }
        }
        else
        {
            if (capsuleCollider.enabled)
            {
                capsuleCollider.enabled = false; //当たり判定を失くす
            }

            // 非攻撃状態になった瞬間に、現在の向きに合わせて角度を更新する
            UpdateBladeRotationWhenIdle();
        }
    }

    /// <summary>
    /// 非攻撃時の剣の角度を、ロボットの向きに合わせて更新する
    /// </summary>
    private void UpdateBladeRotationWhenIdle()
    {
        if (robotMoveScript != null)
        {
            rightFlag = robotMoveScript.rightFlag;
            this.transform.rotation = Quaternion.Euler(0f, 0f, rightFlag ? -30 : 210);
        }
    }

    /// <summary>
    /// OnBossBattleStateChangedイベントを受け取ったときに実行される関数
    /// </summary>
    /// <param name="isInBattle">イベントから渡された「ボス戦中かどうか」のbool値</param>
    private void OnBossBattleStateChanged(bool isInBattle)
    {
        // 自身の変数を更新する
        _isInBossBattle = isInBattle;
    }

    private void OnEnable()
    {
        // 初期化が完了していない場合は何もしない
        if (!GameManager.isFirstGameSceneOpen)
            return;

        // robotMoveScriptがnullでないことを確認してから購読
        if (robotMoveScript != null)
        {
            // Robot_move側で定義したイベントを購読
            robotMoveScript.OnBladeSwingingChanged += HandleBladeSwingingChanged;

            // 起動時の初期状態を同期（安全のため）
            HandleBladeSwingingChanged(robotMoveScript.isBladeSwinging);
        }
        else
        {
            Debug.LogError("Robot_moveスクリプトが見つかりません。", this);
        }

        //生成が完了したフラグを立てる
        isStarted = true;
        //向きの変数を初期化
        rightFlag = robotMoveScript.rightFlag;
        //画像の角度を初期化
        this.transform.rotation = Quaternion.Euler(0f, 0f, rightFlag ? 60 : 120);
        // イベントを購読する
        GameUIManager.OnBossBattleStateChanged += OnBossBattleStateChanged;
        OnBossBattleStateChanged(GameUIManager.instance.IsInBossBattle); // 現在の状態で初期化
    }

    private void OnDisable()
    {
        // 購読解除
        if (robotMoveScript != null)
        {
            robotMoveScript.OnBladeSwingingChanged -= HandleBladeSwingingChanged;
        }
        // イベントの購読解除
        GameUIManager.OnBossBattleStateChanged -= OnBossBattleStateChanged;
        //生成が完了したフラグを下げる
        isStarted = false;
    }
}
