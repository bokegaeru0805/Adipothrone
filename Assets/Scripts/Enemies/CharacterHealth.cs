using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// HPを持ち、ダメージを受けることができる全てのキャラクターの基本となる抽象クラス。
/// HPの増減、被弾時の共通エフェクトやサウンド、死亡判定の基本フローなど、
/// 敵とボスで完全に共通する機能のみを定義します。
/// </summary>
public abstract class CharacterHealth : MonoBehaviour, IDamageable, IDroppable, IDefeatable
{
    // --- プロパティ（継承先クラスから読み書き可能） ---
    public int MaxHP { get; protected set; }
    public int CurrentHP { get; protected set; }
    public bool IsDefeated { get; protected set; }

    /// <summary>
    /// HPが変動した際にUIなどに通知するためのイベント。
    /// </summary>
    public event Action<int> OnHPChanged;

    /// <summary>
    /// 派生クラスから安全にOnHPChangedイベントを発火させるためのメソッド。
    /// </summary>
    protected void InvokeHPChangedEvent()
    {
        OnHPChanged?.Invoke(CurrentHP);
    }

    /// <summary>
    /// このキャラクターのレベルを取得します。EnemyDataから参照されます。
    /// </summary>
    public int Level => enemyData != null ? enemyData.requiredLevel : 0;

    // --- Inspector設定（継承先クラスで利用） ---
    [Tooltip("キャラクターの基本データを設定します")]
    [SerializeField]
    protected EnemyData enemyData;

    // --- 内部参照（継承先クラスで利用） ---
    protected SpriteRenderer spriteRenderer;
    protected Color col;
    protected Animator animator;

    /// <summary>
    /// コンポーネントが有効になった際の初期化処理。
    /// 派生クラスで必要なコンポーネントをキャッシュする土台となります。
    /// virtual: 派生クラスでこの処理を上書き（拡張）できます。
    /// </summary>
    protected virtual void Awake()
    {
        // 描画用のコンポーネントを取得し、初期色を保存
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"{this.gameObject.name}にSpriteRendererがアタッチされていません");
        }
        else
        {
            col = spriteRenderer.color;
        }
    }

    /// <summary>
    /// ダメージ処理の全体の流れを定義するテンプレートメソッド。
    /// </summary>
    public virtual void Damage(int damage)
    {
        // 処理実行前の共通ガード節
        if (IsDefeated || Time.timeScale <= 0)
            return;

        // --- Step 1: ダメージ適用前の共通処理 ---
        TimeManager.instance.TriggerHitStop(); //ヒットストップを行う
        SEManager.instance?.PlayEnemyActionSE(SE_EnemyAction.Damage2); // 被弾SEを再生

        // --- Step 2: HPの減算 ---
        CurrentHP -= damage;

        // HPが変動したことを外部に通知する
        OnHPChanged?.Invoke(CurrentHP);

        // --- Step 3: ダメージ適用"後"の、派生クラス独自の処理を呼び出すフック ---
        OnDamageApplied();

        // --- Step 4: 共通の被弾エフェクト ---
        StartCoroutine(FadeInOut());

        // --- Step 5: 死亡判定の、派生クラス独自の処理を呼び出すフック ---
        CheckForDeath();
    }

    /// <summary>
    /// [フック] ダメージが適用された直後に呼ばれる仮想メソッド。
    /// 派生クラスはこれを上書きして、HPバーの更新など固有の処理を追加できます。
    /// </summary>
    protected virtual void OnDamageApplied()
    {
        // 基本クラスでは何もしない
    }

    /// <summary>
    /// [フック] 死亡判定を行うための仮想メソッド。
    /// 派生クラスはこれを上書きして、特別な死亡条件を追加できます。
    /// </summary>
    protected virtual void CheckForDeath()
    {
        // 基本的な死亡判定
        if (CurrentHP <= 0)
        {
            HandleDeathFlow();
        }
    }

    /// <summary>
    /// 死亡時の共通フローを管理します。HPが0になった際にDamageメソッドから呼び出されます。
    /// </summary>
    protected void HandleDeathFlow()
    {
        // 多重実行を防ぐため、一度だけ実行
        if (IsDefeated)
            return;
        IsDefeated = true;

        //討伐記録をセーブデータに反映する処理を呼び出す
        RecordDefeat();

        // 共通の死亡時処理
        this.tag = "Untagged"; // 敵として認識されなくなるようタグを変更
        DropOnDeathHandler.Drop(this); // アイテムドロップ処理を呼び出す

        // 固有の死亡演出を呼び出す（中身は継承先クラスで実装）
        OnDeath();
    }

    /// <summary>
    /// この敵が討伐されたことをセーブデータに記録します。
    /// </summary>
    private void RecordDefeat()
    {
        // GameManagerと、この敵のEnemyDataが正しく設定されているかを確認
        if (GameManager.instance != null && enemyData != null)
        {
            // GameManager経由でセーブデータにアクセスし、討伐数を1加算する
            GameManager.instance.savedata.EnemyRecordData.AddKillCount(enemyData.enemyID);
        }
        else
        {
            Debug.LogWarning("GameManagerまたはEnemyDataが見つからないため、討伐数を記録できませんでした。");
        }
    }

    /// <summary>
    /// 派生クラスで固有の死亡演出を実装するための抽象メソッド。
    /// abstract: このクラスを継承するクラスは、必ずこのメソッドを実装しなければなりません。
    /// </summary>
    protected abstract void OnDeath();

    /// <summary>
    /// 被弾時にキャラクターを短時間点滅させる共通のコルーチン。
    /// 元のスクリプトの挙動を完全に再現しています。
    /// </summary>
    protected IEnumerator FadeInOut()
    {
        if (spriteRenderer != null)
        {
            // 一度元の色に戻してから処理を開始
            col.a = 1;
            spriteRenderer.color = col;

            // 一瞬暗く（半透明に）する
            col.a -= 0.8f;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = col;

            // 元の不透明度に戻す
            col.a += 0.8f;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = col;
        }
    }

    // --- ヘルパーメソッド ---
    /// <summary>
    /// 指定時間後にこのゲームオブジェクトを非アクティブ化します。
    /// </summary>
    /// <param name="time">非アクティブ化までの待機時間</param>
    protected virtual IEnumerator DeactivateAfterTime(float time)
    {
        yield return new WaitForSeconds(time);
        this.gameObject.SetActive(false);
    }

    protected bool HasParameter(string paramName)
    {
        if (animator == null)
            return false;
        foreach (var param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 現在のHPの割合を0.0f～1.0fの範囲で取得します。
    /// UIの更新やAIの条件分岐などに使用します。
    /// </summary>
    public float NormalizedHP
    {
        get
        {
            // MaxHPが0以下の場合に、ゼロ除算エラーを防ぐためのチェック
            if (MaxHP <= 0)
            {
                return 0f;
            }

            // CurrentHPとMaxHPはどちらも整数(int)なため、
            // そのまま割り算すると小数点以下が切り捨てられてしまいます。(例: 50 / 100 = 0)
            // (float)とキャスト（型変換）することで、正しい小数点の割合(0.5)を算出します。
            return (float)CurrentHP / MaxHP;
        }
    }

    #region Interface Implementations
    // --- インターフェースの共通実装 ---
    public EnemyData GetEnemyData() => enemyData;

    public Vector3 GetDropPosition() => transform.position;

    public virtual Transform GetDropParent() => transform.parent;
    #endregion
}
