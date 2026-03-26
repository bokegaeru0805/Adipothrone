using UnityEngine;

/// <summary>
/// 通常敵のHPと死亡処理を管理するクラス。CharacterHealthを継承します。
/// 元のenemy_HPの全ての機能を持ち、スポナーからの初期化やオブジェクトの再利用に対応します。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyHealth : CharacterHealth, IEnemyResettable
{
    [Header("死亡演出設定 (通常敵)")]
    [SerializeField]
    private float fadeOutDuration = 0.1f; // 消えるまでの透明化時間

    [SerializeField]
    private float deathsecond = 0.1f; // 死亡アニメーションの表示時間

    [SerializeField]
    private bool isDeathAnimActive = false; // 死亡アニメーションを行うかどうか

    [SerializeField]
    private bool isDeathHandled = true; // HP0時の自動死亡処理を行うかどうか

    // --- 内部コンポーネント参照 ---
    private bool isInitialized = false; // 外部からの初期化が完了したかを管理するフラグ
    private Rigidbody2D rbody;
    private float destroyEffectScale = 1.0f; // 死亡エフェクトの大きさ
    private const string deathAnimParam = "death"; // 死亡アニメーションのパラメータ名
    private string destroyEffectPoolTag = "DestroyEffect1"; // 死亡エフェクトのプールタグ
    private string subDestroyEffectPoolTag = "DestroyEffect2"; // サブ死亡エフェクトのプールタグ
    private int subDestroyEffectCount = 3; // サブ死亡エフェクトの生成数
    private Transform dropParent;

    /// <summary>
    /// 基本クラスのAwakeを拡張し、通常敵に必要なコンポーネントを取得、設定します。
    /// </summary>
    protected override void Awake()
    {
        // まず基本クラスのAwake処理（SpriteRendererの取得など）を実行
        base.Awake();

        // 通常敵固有のコンポーネントをキャッシュ
        rbody = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        dropParent = this.transform.parent; // ドロップアイテムの親を設定
        // if (dropParent == null)
        // {
        //     Debug.LogWarning(
        //         $"{this.gameObject.name}の親オブジェクトが設定されていません。ドロップアイテムの親が正しく設定されない可能性があります。"
        //     );
        // }
    }

    /// <summary>
    /// ゲーム開始時に、もし外部からInitializeが呼ばれていなければ、
    /// インスペクターに設定されたデータで自己初期化するフォールバック処理。
    /// </summary>
    private void Start()
    {
        if (!isInitialized)
        {
            // スポナー経由でなく、シーンに直接配置された場合の初期化
            if (enemyData != null)
            {
                Initialize(enemyData);
            }
            else if (useManualHP)
            {
                // 手動HP設定が有効な場合は、インスペクターの値を最大HPとして使用する
                MaxHP = manualMaxHP;
                CurrentHP = MaxHP;
                isInitialized = true;
            }
            else
            {
                Debug.LogWarning(
                    $"[{gameObject.name}] EnemyDataが設定されていません。手動HP設定(useManualHP)も無効です。"
                );
            }
        }
    }

    /// <summary>
    /// 敵生成スポナーなど、外部から敵のステータスを初期化するためのメソッド。
    /// </summary>
    public void Initialize(EnemyData data)
    {
        if (isInitialized)
            return; // 既に初期化済みなら何もしない

        if (data == null)
        {
            Debug.LogError($"{this.gameObject.name}に設定されるEnemyDataがnullです。");
            gameObject.SetActive(false); // エラー時は非表示にするなど
            return;
        }

        this.enemyData = data;

        // EnemyDataに基づいてステータスを設定
        MaxHP = enemyData.enemyHP;
        // destroyEffect = enemyData.destroyeffect;
        destroyEffectScale = enemyData.destroyeffectScale;

        // 状態をリセットしてHPなどを満タンにする
        ResetState();

        isInitialized = true;
    }

    /// <summary>
    /// [フックの上書き] 基本クラスの死亡判定処理を、isDeathHandledフラグを考慮するように変更します。
    /// </summary>
    protected override void CheckForDeath()
    {
        // HPが0以下 かつ 倒されていない かつ 自動死亡処理が有効 の場合のみ死亡フローへ
        if (CurrentHP <= 0 && !IsDefeated && isDeathHandled)
        {
            HandleDeathFlow();
        }
        else if (CurrentHP <= 0 && !IsDefeated)
        {
            // 自動死亡処理はしないが、倒されたフラグだけは立てておく
            IsDefeated = true;
        }
    }

    /// <summary>
    /// 通常敵固有の死亡処理。アニメーションやエフェクト再生、オブジェクトの非アクティブ化を行います。
    /// </summary>
    protected override void OnDeath()
    {
        // 死亡エフェクトの再生
        // 永続プール(PersistentInstance)がnullでないか確認
        if (ObjectPooler.PersistentInstance != null && !string.IsNullOrEmpty(destroyEffectPoolTag))
        {
            // 衝突点（弾の位置）を取得
            Vector2 hitPosition = this.transform.position;

            // ObjectPooler の永続インスタンスから、指定した「タグ」のエフェクトを呼び出す
            GameObject effect = ObjectPooler.PersistentInstance.SpawnFromPool(
                destroyEffectPoolTag, // プレハブの代わりに「タグ」を渡す
                hitPosition, // 座標
                Quaternion.identity // 回転
            );

            // エフェクトの大きさを調整
            effect.transform.localScale = Vector3.one * destroyEffectScale;

            // GameUIManager から現在のボス戦状態を取得
            bool isBossBattle = GameUIManager.instance?.IsInBossBattle ?? false;

            if (!isBossBattle && !string.IsNullOrEmpty(subDestroyEffectPoolTag))
            {
                // ボス戦闘中でなければ、指定した回数だけサブエフェクトをランダムな位置に再生
                for (int i = 0; i < subDestroyEffectCount; i++)
                {
                    // hitPosition の周囲（半径 subHitEffectSpawnRadius 内）にランダムな座標を生成
                    // (Random.insideUnitCircle は Vector2(x, y) を返す)
                    Vector2 randomOffset = Random.insideUnitCircle * destroyEffectScale;
                    Vector2 spawnPosition = hitPosition + randomOffset;

                    // プールからサブエフェクトを再生
                    ObjectPooler.PersistentInstance.SpawnFromPool(
                        subDestroyEffectPoolTag,
                        spawnPosition, // ランダム化された座標
                        Quaternion.identity
                    );
                }
            }
        }

        SEManager.instance?.PlayEnemyActionSE(SE_EnemyAction.Death1); // 死亡の効果音を鳴らす

        // 物理挙動を停止
        if (rbody != null)
        {
            rbody.velocity = Vector2.zero;
            // rbody.isKinematic = true;
        }

        // スプライトの透明度を完全にする
        ResetColor();

        // 死亡アニメーションの有無で処理を分岐
        if (isDeathAnimActive && animator != null && HasParameter(deathAnimParam))
        {
            animator.SetBool(deathAnimParam, true);
            StartCoroutine(DeactivateAfterTime(deathsecond));
        }
        else
        {
            StartCoroutine(DeactivateAfterTime(0.1f));
        }
    }

    /// <summary>
    /// HPが0になった後、徐々にフェードアウトさせるための処理。
    /// </summary>
    private void FixedUpdate()
    {
        if (Time.timeScale > 0 && CurrentHP <= 0)
        {
            SetAlpha(currentAlpha - (1.0f / (60.0f * fadeOutDuration)));
        }
    }

    /// <summary>
    /// オブジェクトプーリング（再利用）のために、敵の状態を初期状態に戻します。
    /// </summary>
    public void ResetState()
    {
        IsDefeated = false; // 倒された状態をリセット
        CurrentHP = MaxHP; // HPを最大HPにリセット
        ResetColor(); // 色と透明度を完全に戻す

        if (HasParameter(deathAnimParam))
        {
            animator.SetBool(deathAnimParam, false);
        }

        // if (rbody != null)
        // {
        //     rbody.isKinematic = false; // 物理挙動を再び有効化
        // }
    }

    // ドロップアイテムの親オブジェクトを返すように上書き
    public override Transform GetDropParent() => this.dropParent;
}
