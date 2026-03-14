using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class TutorialGolemMoveController : MonoBehaviour, IEnemyResettable
{
    [Header("Flowchart設定")]
    [SerializeField]
    private Fungus.Flowchart flowchart = null;

    [Header("プレイヤーの情報")]
    [SerializeField]
    private Transform playerTransform = null;

    [Header("その他の設定")]
    [SerializeField]
    private float DetectLeft; //プレイヤーを感知する左端

    [SerializeField]
    private float DetectRight; //プレイヤーを感知する右端

    [SerializeField]
    private float ExistLeft; // 攻撃が存在できる一番左の座標

    [SerializeField]
    private float Interval; //攻撃間隔

    [SerializeField]
    private float timeToEdge = 1; //弾が端にたどり着くまでの時間

    [SerializeField]
    private float offsetX;

    [SerializeField]
    private float offsetX_2;

    [SerializeField]
    private float offsetY;

    [SerializeField]
    private float GroundY; //地面の高さ

    [SerializeField]
    private float RobotHeight; //Robotの通常の高さ

    [SerializeField]
    private float flatShootRadius; //攻撃４の地面と平行な弾の速度

    [Header("弾のプレハブ")]
    [SerializeField]
    private GameObject shoot_prefab; // 攻撃のプレハブ

    [Header("死亡後のオブジェクト")]
    [SerializeField]
    private GameObject AfterDeathObject; //死亡後のオブジェクト

    [Header("スプライト設定")]
    [SerializeField]
    private Sprite AttackSprite; //攻撃時のスプライト

    [SerializeField]
    private Sprite DeathSprite; //死亡判定時のスプライト
    private float flatvelocity;
    private int ShootDamage = 16; //弾のダメージ量
    private bool isAttacking = false; //攻撃コルーチンが起こっているかどうかのフラグ
    private bool isDead = false; //死亡しているかどうかのフラグ
    private Vector3 PlayerPosition;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private CharacterHealth enemyHealth;
    private CriWare.Assets.CriAtomSePlayer sePlayer;

    private void Awake()
    {
        if (flowchart == null)
        {
            Debug.LogError("TutorialGolemにFlowchartが設定されていません。");
        }

        if (shoot_prefab == null)
        {
            Debug.LogError("TutorialGolemにshoot_prefabが設定されていません。");
        }

        if (AfterDeathObject == null)
        {
            Debug.LogError("TutorialGolemにAfterDeathObjectが設定されていません。");
        }

        if (AttackSprite == null || DeathSprite == null)
        {
            Debug.LogError("TutorialGolemにAttackSpriteまたはDeathSpriteが設定されていません。");
        }

        spriteRenderer = this.GetComponent<SpriteRenderer>();
        animator = this.GetComponent<Animator>();
        sePlayer = this.GetComponent<CriWare.Assets.CriAtomSePlayer>();
        enemyHealth = this.GetComponent<CharacterHealth>();
        if (enemyHealth == null)
        {
            Debug.LogError(
                "TutorialGolemにCharacterHealthコンポーネントがアタッチされていません。"
            );
        }
    }

    private void Start()
    {
        bool? isDefeated = FlagManager.instance?.GetBoolFlag(
            PrologueTriggeredEvent.DefeatTutorialGolem
        );

        if (isDefeated == true)
        {
            isDead = true;
            this.tag = "Untagged"; //enemyのtagを外す
            Destroy(this.gameObject); //自分のゲームオブジェクト消去
            AfterDeathObject.SetActive(true); //死亡後のオブジェクトを表示させる
            return;
        }
        else
        {
            if (playerTransform == null)
            {
                playerTransform = GameObject
                    .FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME)
                    .transform; //PlayerのTransformを取得
            }

            isDead = false;
            AfterDeathObject.SetActive(false); //死亡後のオブジェクトを非表示させる
            isAttacking = false;
            flatvelocity = Mathf.Abs(
                (ExistLeft - (this.transform.position.x + offsetX)) / timeToEdge
            ); //弾の速度を計算
        }

        StartAttackWhenReady();
    }

    /// <summary>
    /// 初期化（Start）が完了するのを待ってから、攻撃コルーチンを開始する
    /// </summary>
    private void StartAttackWhenReady()
    {
        // --- ここから下は、Start()完了後に実行されることが保証される ---

        // OnEnable時に、もし死骸が表示されていたら非表示にする
        // (Start()のフラグチェックで「生」と判定された後での念押し)
        if (AfterDeathObject.activeSelf)
        {
            AfterDeathObject.SetActive(false);
        }

        // 攻撃コルーチンが多重起動しないようにフラグをチェックして開始
        if (!isAttacking)
        {
            StartCoroutine(Attack());
        }
    }

    /// <summary>
    /// 親オブジェクトから有効化された際に状態をリセットし、行動を再開します。
    /// </summary>
    public void ResetState()
    {
        // 既に死亡している場合は何もしない
        if (!isDead)
        {
            StartAttackWhenReady();
        }
    }

    private void OnEnable()
    {
        //イベントを購読
        if (enemyHealth != null)
        {
            // 既に死亡している（またはStartで破棄が決定した）場合は、イベントを購読しない
            if (!isDead)
            {
                enemyHealth.OnHPChanged += CheckHP;
            }
        }
    }

    private void OnDisable()
    {
        isAttacking = false; //攻撃コルーチンが起こっているフラグを下げる

        //イベントの購読を解除
        if (enemyHealth != null)
        {
            enemyHealth.OnHPChanged -= CheckHP;
        }

        // オブジェクト無効化時に、実行中の全コルーチンを強制停止する
        // これがないと、isAttackingフラグだけがfalseになり、コルーチンが暴走する可能性がある
        StopAllCoroutines();
    }

    private IEnumerator Attack()
    {
        isAttacking = true; //攻撃コルーチンが起こっているフラグを立てる

        while (true)
        {
            if (playerTransform == null)
            {
                // プレイヤーが見つからなければ、次のフレームで再試行
                yield return null;
                continue;
            }

            PlayerPosition = playerTransform.position;

            if (DetectLeft < PlayerPosition.x && PlayerPosition.x < DetectRight)
            {
                yield return new WaitForSeconds(Interval); //攻撃間隔を待機

                // コルーチン側はアニメーションの再生指示だけを行う
                animator.SetTrigger("Attack"); //攻撃アニメーションを起動

                // アニメーションが確実に終わるまでの余裕時間を待機する（連射防止用）
                // ※ ここはアニメーションの長さに合わせて調整してください
                yield return new WaitForSeconds(1.0f);
            }
            yield return null; // 条件を満たさなくても、必ずフレームを待つ！
        }
    }

    /// <summary>
    /// 攻撃アニメーションの特定のフレーム（Animation Event）から呼び出されます
    /// </summary>
    public void ExecuteShootEvent()
    {
        sePlayer.Play(SE_EnemyAction.Shoot1_Enemy); //効果音を鳴らす

        float targetHeight = (GroundY + RobotHeight) + flatShootRadius * Random.Range(-1, 1); //弾の高さをランダムに設定(-1か0)
        Vector2 spawnPos = new Vector2(
            this.transform.position.x + offsetX,
            this.transform.position.y + offsetY
        );
        GameObject shoot = Instantiate(shoot_prefab, spawnPos, Quaternion.identity); //弾を生成
        var script = shoot.GetComponent<ContactDamageController>(); //ダメージに関するスクリプトを取得
        if (script != null)
        {
            script.SetNormalDamage(ShootDamage); //弾のダメージを設定
        }
        else
        {
            Debug.LogWarning($"EnemyStateControllerが{shoot.name}に見つかりませんでした。");
        }
        shoot.transform.localScale = Vector3.one * 2.5f; //弾のサイズを調整
        shoot.transform.SetParent(this.transform); // 弾の親をこのオブジェクトに設定

        var newrbody = shoot.GetComponent<Rigidbody2D>(); //弾のRigidbody2Dを取得
        newrbody.gravityScale = 0; //弾の重力を消去
        Vector2 shootDirection = new Vector2(offsetX_2 - offsetX, targetHeight - spawnPos.y);
        newrbody.AddForce(
            new Vector2(shootDirection.x, shootDirection.y).normalized * flatvelocity,
            ForceMode2D.Impulse
        ); //弾の速度を設定

        StartCoroutine(DestroyShoot(shoot, targetHeight, newrbody));
    }

    private IEnumerator DestroyShoot(GameObject shoot, float targetHeight, Rigidbody2D prefab_rbody)
    {
        while (shoot != null)
        {
            Vector3 pos = shoot.transform.position;
            if (pos.x < ExistLeft)
            {
                Destroy(shoot);
                yield break;
            }

            float vx = prefab_rbody.velocity.x; //速度のx成分を取得
            if (Mathf.Abs(pos.y - targetHeight) <= 0.1f)
            {
                prefab_rbody.velocity = new Vector2(Mathf.Sign(vx) * flatvelocity, 0);
            }

            vx = prefab_rbody.velocity.x; //速度のx成分を取得
            float vy = prefab_rbody.velocity.y; //速度のy成分を取得
            shoot.transform.eulerAngles = new Vector3(0, 0, Mathf.Atan2(vy, vx) * Mathf.Rad2Deg); //向きを設定
            yield return null; //フレームを待つ
        }
    }

    private void CheckHP(int currentHP)
    {
        if (currentHP <= 0 && !isDead)
        {
            StartCoroutine(Death());
        }
    }

    private IEnumerator Death()
    {
        isDead = true; //死亡しているフラグを立てる
        this.tag = "Untagged"; //enemyのtagを外す
        animator.SetBool("death", true); //死亡アニメーションを行う
        sePlayer.Play(SE_Field.SmallBomb); //効果音を鳴らす
        yield return new WaitUntil(() => spriteRenderer.sprite == DeathSprite); //特定のスプライトになるまで待機する
        animator.enabled = false; //Animatorを無効化
        FlagManager.instance.SetBoolFlag(PrologueTriggeredEvent.DefeatTutorialGolem, true); //チュートリアルゴーレムを倒したフラグを立てる
        Destroy(this.gameObject); //このオブジェクトを消す
        AfterDeathObject.SetActive(true); //死亡後のオブジェクトを表示させる
        FungusHelper.ExecuteBlock(flowchart, "TutorialGolemDefeat"); //Fungusのブロックを実行
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 center1 = new Vector3((DetectLeft + DetectRight) / 2f, (-94.5f + -99.5f) / 2f, 0f);
        Vector3 size1 = new Vector3(
            Mathf.Abs(DetectRight - DetectLeft),
            Mathf.Abs(-94.5f - -99.5f),
            0f
        );
        Gizmos.DrawWireCube(center1, size1);

        Gizmos.color = Color.red;
        Vector3 center2 = new Vector3((ExistLeft + DetectRight) / 2f, (-94.5f + -99.5f) / 2f, 0f);
        Vector3 size2 = new Vector3(
            Mathf.Abs(DetectRight - ExistLeft),
            Mathf.Abs(-94.5f - -99.5f),
            0f
        );
        Gizmos.DrawWireCube(center2, size2);
    }
}
