using System.Collections;
using Effekseer;
using UnityEngine;

/// <summary>
/// 通常敵のHPと死亡処理を管理するクラス。CharacterHealthを継承します。
/// 元のenemy_HPの全ての機能を持ち、スポナーからの初期化やオブジェクトの再利用に対応します。
/// </summary>
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
    private EffekseerEmitter destroyEffect;
    private float destroyEffectScale = 1.0f; // 死亡エフェクトの大きさ
    private const string deathAnimParam = "death"; // 死亡アニメーションのパラメータ名
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
            if (enemyData == null)
            {
                Debug.LogWarning($"{this.gameObject.name}はEnemyDataが設定されていません");
                return;
            }
            Initialize(this.enemyData);
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
        destroyEffect = enemyData.destroyeffect;
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
        // エフェクト再生
        if (destroyEffect != null)
        {
            var effect = Instantiate(destroyEffect, transform.position, Quaternion.identity);
            effect.transform.localScale = Vector3.one * destroyEffectScale;
            effect.Play();
        }

        // 物理挙動を停止
        if (rbody != null)
        {
            rbody.velocity = Vector2.zero;
            rbody.isKinematic = true;
        }

        // 元のHPバーの色をリセット
        col.a = 1;
        spriteRenderer.color = col;

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
            col.a -= 1 / (60 * fadeOutDuration);
            spriteRenderer.color = col;
        }
    }

    /// <summary>
    /// オブジェクトプーリング（再利用）のために、敵の状態を初期状態に戻します。
    /// </summary>
    public void ResetState()
    {
        IsDefeated = false; // 倒された状態をリセット
        CurrentHP = MaxHP; // HPを最大HPにリセット
        col.a = 1; // 透明度を完全に戻す
        spriteRenderer.color = col;

        if (HasParameter(deathAnimParam))
        {
            animator.SetBool(deathAnimParam, false);
        }

        if (rbody != null)
        {
            rbody.isKinematic = false; // 物理挙動を再び有効化
        }
    }

    // ドロップアイテムの親オブジェクトを返すように上書き
    public override Transform GetDropParent() => this.dropParent;
}
