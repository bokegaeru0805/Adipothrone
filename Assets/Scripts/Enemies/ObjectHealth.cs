using System.Collections;
using UnityEngine;

/// <summary>
/// 破壊可能なオブジェクト（岩、木など）のHPと破壊処理を管理するクラス。
/// CharacterHealthを継承し、共通のダメージ処理などを利用しつつ、
/// EnemyDataがなくてもHPを設定できるなど、オブジェクト固有の初期化処理を持ちます。
/// </summary>
public class ObjectHealth : CharacterHealth
{
    [Header("オブジェクト固有設定")]
    [Tooltip("破壊時のフェードアウト時間")]
    [SerializeField]
    private float fadeOutDuration = 0.1f;

    [Header("EnemyDataがない場合のフォールバック設定")]
    [Tooltip("EnemyDataが未設定の場合、この値が最大HPになります")]
    [SerializeField]
    private int objectMaxHP = 0;

    [Tooltip("EnemyDataが未設定の場合、この値がドロップするお金になります")]
    [SerializeField]
    private int dropMoney = 0;

    // --- 内部参照 ---
    private bool isActivated = false; // 初期化が完了したかどうかのフラグ
    private Rigidbody2D rbody;

    /// <summary>
    /// オブジェクト固有の初期化処理。
    /// EnemyDataがない場合のフォールバック機能を持つため、基本クラスのAwakeは使わず、
    /// 完全にこのメソッドで処理を上書き（override）します。
    /// </summary>
    protected override void Awake()
    {
        // 基本クラスのAwakeは呼び出さず、ここから全て記述する
        isActivated = false;
        rbody = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>(); // spriteRendererは基本クラスの変数
        col = spriteRenderer.color;                 // colも基本クラスの変数

        // --- HPとドロップ金額の設定 ---
        // EnemyDataが設定されている場合
        if (enemyData != null)
        {
            MaxHP = enemyData.enemyHP;
            // TODO: ドロップ金額は現在使われていないが、必要ならここで設定
            // dropMoney = enemyData.dropMoney; 
        }
        // EnemyDataがなく、独自のHPが設定されている場合
        else if (objectMaxHP > 0)
        {
            MaxHP = objectMaxHP;
        }
        // どちらも設定されていない場合は警告を出す
        else
        {
            Debug.LogWarning($"{this.gameObject.name}はEnemyDataまたはobjectMaxHPが設定されていません");
        }
        
        CurrentHP = MaxHP; // 現在HPを最大HPに設定
        isActivated = true;  // 初期化完了
    }

    /// <summary>
    /// 基本クラスから継承した、オブジェクト固有の死亡処理。
    /// 元のHandleDeath()メソッドのロジックをここに記述します。
    /// </summary>
    protected override void OnDeath()
    {
        // Rigidbodyの物理処理を停止
        if (rbody != null)
        {
            rbody.velocity = Vector2.zero;
            rbody.isKinematic = true;
        }

        // 破壊時に色を元に戻す
        col.a = 1;
        spriteRenderer.color = col;

        // 指定時間後にGameObjectを完全に消去する
        Destroy(gameObject, fadeOutDuration);
    }
    
    /// <summary>
    /// HPが0になった後、徐々にフェードアウトさせるための処理。
    /// </summary>
    private void FixedUpdate()
    {
        // HPが0以下で、かつ初期化が完了している場合にフェードアウトを実行
        // isActivatedのチェックは元のスクリプトでは!isActivatedだったが、意図を汲んで修正
        if (Time.timeScale > 0 && CurrentHP <= 0 && isActivated)
        {
            col.a -= 1 / (60 * fadeOutDuration);
            spriteRenderer.color = col;
        }
    }

    #region 独自機能
    /// <summary>
    /// このオブジェクトの最大HPを外部から設定します。現在HPも新しい最大HPに合わせて全回復します。
    /// </summary>
    /// <param name="newMaxHP">新しい最大HPの値</param>
    public void SetMaxHP(int newMaxHP)
    {
        // 不正な値（0以下）が設定されないように値を検証
        if (newMaxHP <= 0)
        {
            Debug.LogWarning(
                "最大HPには1以上の値を設定してください。自動的に1に補正します。",
                this
            );
            newMaxHP = 1;
        }

        MaxHP = newMaxHP;
        CurrentHP = MaxHP; // 現在HPも最大値にリセット（全回復）
    }
    #endregion
}