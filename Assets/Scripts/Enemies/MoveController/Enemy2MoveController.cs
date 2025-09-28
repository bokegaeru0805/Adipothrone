using System.Collections;
using UnityEngine;

public class Enemy2MoveController : MonoBehaviour, IEnemyResettable
{
    [Header("敵のタイプ")]
    [SerializeField]
    private EnemyVariant variantType = EnemyVariant.None; //敵の種類を設定

    [Header("設定項目")]
    [SerializeField]
    private Transform playerTransform; //playerのTransformを設定

    [SerializeField]
    private EnemyActivator activator = null; // 親のEnemyActivatorコンポーネント

    [Header("移動・攻撃の基本設定")]
    [SerializeField]
    private float speedX = 4.0f; // 移動速度を設定

    [SerializeField]
    private float dashspeedX = 9.0f; // 走る移動速度を設定

    [SerializeField]
    private float lowerBound = 0f; // 自分の一番下の座標

    [SerializeField, Tooltip("索敵距離")]
    private float detectionRange = 4; //索敵距離

    [SerializeField, Tooltip("攻撃距離")]
    private float attackRange = 1.5f; //攻撃距離

    [SerializeField, Tooltip("待機時間")]
    private float wait_Sec = 3; //待機時間

    [SerializeField]
    private float offsetX = 1.4f; //自分に対しての弾のx座標の差分

    [SerializeField]
    private float offsetY = 1.9f; //自分に対しての弾のy座標の差分

    [SerializeField]
    private float shoot1_throwX = 3; //弾の速さ

    [Header("必要ならば設定")]
    [SerializeField]
    private float leftBound = 0; //行動範囲の左端

    [SerializeField]
    private float rightBound = 0; //行動範囲の右端

    [Header("スプライト設定")]
    [SerializeField]
    private Sprite normalSprite; //通常の画像のスプライト

    [SerializeField]
    private Sprite chaseSprite; //追跡用の画像のスプライト

    [Header("弾のプレハブ")]
    [SerializeField]
    private GameObject shoot_prefab; //弾のプレハブ

    // 敵の種類を定義
    private enum EnemyVariant
    {
        None = 0,
        TutorialStage = 1,
    }

    private float vx = 0; //x方向の移動速度
    private float PositionY = 0; //自分のy座標
    private float ExistBottom = 0; //弾が存在出来る一番下の座標
    private float UpperBoundOffset = -1.0f; //上端のオフセット
    private int shootDamage = 0; //弾のダメージ量
    private bool rightFlag = false; //右向きかどうかのフラグ
    private Vector2 pos = Vector2.zero; //自分の現在の座標
    private Rigidbody2D rbody; // Rigidbody2Dコンポーネント
    private SpriteRenderer spriteRenderer; // 自分のSpriteRenderer
    private EnemyHealth enemyHP;

    private void Awake()
    {
        switch (variantType)
        {
            case EnemyVariant.TutorialStage:
                shootDamage = 14;
                break;
            default:
                Debug.LogError($"{this.name}のEnemyVariantが設定されていません。");
                break;
        }

        if (normalSprite == null || chaseSprite == null || shoot_prefab == null)
        {
            Debug.LogError($"{this.name}のスプライトまたは弾のプレハブが設定されていません。");
        }

        if (lowerBound == 0)
        {
            Debug.LogError($"{this.name}のlowerBoundが設定されていません。");
        }

        if (activator == null)
        {
            activator = GetComponentInParent<EnemyActivator>();
            if (activator == null)
            {
                Debug.LogWarning(
                    $"{this.name}の親にEnemyActivatorが見つかりませんでした。移動範囲の自動設定は行いません。"
                );
            }
        }

        if (playerTransform == null)
        {
            playerTransform = GameObject
                .FindGameObjectWithTag(GameConstants.PlayerTagName)
                ?.transform;
            if (playerTransform == null)
            {
                Debug.LogError(
                    $"{this.name}のplayerTransformが設定されていません。Playerオブジェクトを見つけてください。"
                );
                return; // Playerが見つからない場合は処理を中止
            }
        }

        enemyHP = this.GetComponent<EnemyHealth>();
        {
            if (enemyHP == null)
            {
                Debug.LogError($"{this.gameObject.name}にEnemyHealthコンポーネントがありません。");
                return;
            }
        }

        rbody = this.GetComponent<Rigidbody2D>();
        spriteRenderer = this.GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (!GameManager.isFirstGameSceneOpen)
            return; // ゲームが開始されていない場合は何もしない

        //アクティベーターから座標に関する情報を取得
        if (activator != null)
        {
            var activatorCollider = activator.GetComponent<Collider2D>();
            if (activatorCollider != null)
            {
                if (leftBound == 0)
                {
                    leftBound = activatorCollider.bounds.min.x; // アクティベーターの左端を取得
                }

                if (rightBound == 0)
                {
                    rightBound = activatorCollider.bounds.max.x; // アクティベーターの右端を取得
                }

                ExistBottom = activatorCollider.bounds.min.y; // アクティベーターの下端を取得
            }
        }

        ResetState(); // 敵の状態をリセット
    }

    /// <summary>
    /// 敵の状態をリセットするメソッド
    /// </summary>
    public void ResetState()
    {
        if (enemyHP != null)
        {
            // 自分のHPをリセット
            enemyHP.ResetState();
        }
        else
        {
            Debug.LogWarning($"{this.gameObject.name}にenemy_HPコンポーネントがありません。");
        }

        float upperBound = lowerBound + GameConstants.RobotJumpPeakHeight + UpperBoundOffset; //自分の一番上の座標を設定
        PositionY = Random.Range(lowerBound, upperBound); //自分のy座標をランダムに設定

        rightFlag = Random.value < 0.5f; // ランダムに左右を決定（trueなら右、falseなら左）
        //物理挙動の初期化
        if (rbody != null)
        {
            vx = rightFlag ? speedX : -speedX; // speedXの符号を変えて左右へ
            rbody.velocity = new Vector2(vx, 0); // 初速を設定
            rbody.simulated = true; // 物理挙動を再起動
            rbody.constraints = RigidbodyConstraints2D.FreezeRotation; // 回転を停止する
        }

        // スプライトの初期化
        spriteRenderer.sprite = normalSprite; //通常の画像に変更
        spriteRenderer.flipX = rightFlag; //スプライトの向きを設定
        this.tag = GameConstants.ImmuneEnemyTagName; // タグを初期化

        wait_Sec = 0.7f; //待機時間を設定
        this.transform.position = new Vector2(
            UnityEngine.Random.Range(leftBound, rightBound),
            PositionY
        ); //自分の初期座標を決定
    }

    private void FixedUpdate()
    {
        // 敵の動きがポーズされていないなら
        if (!TimeManager.instance.isEnemyMovePaused)
        {
            if (rbody != null && !rbody.simulated)
                rbody.simulated = true; //物理挙動を再起動する

            pos = this.transform.position; //自分の現在座標を取得
            Vector3 dir = playerTransform.position - this.transform.position; //自分とプレイヤーの現在座標の差を取得

            if (
                dir.x * (rightFlag ? 1 : -1) <= detectionRange
                && 0 <= dir.x * (rightFlag ? 1 : -1)
                && vx != 0
            )
            { //自分の前の感知範囲内にプレイヤーがいて、自分が動いているとき
                vx = dashspeedX * Mathf.Sign(vx); //速度をダッシュの速度にする
                spriteRenderer.sprite = chaseSprite; //追跡用の画像に変更
            }

            if ((pos.x <= leftBound && vx <= 0) || (rightBound <= pos.x && 0 <= vx))
            { //端についたとき
                vx = speedX * (rightFlag ? -1 : 1); //速度を反転させる
                rightFlag = !rightFlag; //右向きかどうかのフラグを逆転させる
                spriteRenderer.sprite = normalSprite; //通常の画像に変更
                spriteRenderer.flipX = rightFlag; //スプライトの向きを逆にする
            }
            else if (
                dir.x * (rightFlag ? 1 : -1) <= attackRange
                && 0 <= dir.x * (rightFlag ? 1 : -1)
                && vx != 0
            )
            { //自分の目の前の攻撃範囲内にプレイヤーがいて、自分が動いているとき
                vx = 0; //速度を０にする
                spriteRenderer.sprite = normalSprite; //通常の画像に変更
                Vector3 newPos = this.transform.position; //自分の座標を保存
                newPos.x += (rightFlag ? 1 : -1) * offsetX; //弾のx座標を指定
                newPos.y += offsetY; //弾のx座標を指定
                newPos.z = -5; //弾のz座標を指定
                GameObject newGameObject = Instantiate(shoot_prefab) as GameObject; // 弾のプレハブを生成
                newGameObject.transform.position = newPos; //弾の位置を設定
                var script = newGameObject.GetComponent<ContactDamageController>(); //ダメージに関するスクリプトを取得
                if (script != null)
                {
                    script.SetDamageAmount(shootDamage); //弾のダメージを設定
                }
                else
                {
                    Debug.LogWarning(
                        $"EnemyStateControllerが{newGameObject.name}に見つかりませんでした。"
                    );
                }
                Rigidbody2D newrbody = newGameObject.GetComponent<Rigidbody2D>(); //弾のRigidbody2Dを取得
                newrbody.gravityScale = 1; //重力の大きさを初期化
                newrbody.AddForce(
                    new Vector2((rightFlag ? 1 : -1) * shoot1_throwX, 0),
                    ForceMode2D.Impulse
                ); //弾1の速度
                StartCoroutine(DestroyFlatShoot(newGameObject));
                StartCoroutine(MoveStart());
            }

            rbody.velocity = new Vector2(vx, rbody.velocity.y); //速度を設定
        }
        else
        {
            if (rbody != null && rbody.simulated)
                rbody.simulated = false; //物理挙動を停止する
        }
    }

    IEnumerator MoveStart()
    {
        yield return new WaitForSeconds(wait_Sec); //wait_Sec秒待つ
        rightFlag = !rightFlag; //右向きかどうかのフラグを逆転させる
        vx = rightFlag ? speedX : -speedX; // speedXの符号を変えて左右へ
        spriteRenderer.flipX = rightFlag; //スプライトの向きを設定
    }

    private IEnumerator DestroyFlatShoot(GameObject shoot)
    {
        if (shoot == null)
            yield break;
        Rigidbody2D prefab_rbody = shoot.GetComponent<Rigidbody2D>(); //Rigidbody2Dコンポーネントを取得

        while (true)
        {
            if (shoot == null)
                yield break;

            //敵の動きがポーズされているなら、Rigidbody2Dを無効化する
            if (TimeManager.instance.isEnemyMovePaused)
            {
                if (prefab_rbody != null && prefab_rbody.simulated)
                    prefab_rbody.simulated = false;
            }
            else
            {
                if (prefab_rbody != null && !prefab_rbody.simulated)
                    prefab_rbody.simulated = true;

                Vector3 pos = shoot.transform.position;
                if (pos.y < ExistBottom)
                {
                    Destroy(shoot);
                    yield break;
                }

                float vx = prefab_rbody.velocity.x;
                float vy = prefab_rbody.velocity.y;
                shoot.transform.eulerAngles = new Vector3(
                    0,
                    0,
                    Mathf.Atan2(vy, vx) * Mathf.Rad2Deg
                );
            }

            yield return null; //1フレーム待って次のフレームで再評価する（フリーズ防止）
        }
    }

    /// <summary>
    /// 指定された親オブジェクトの子要素のうち、OutlineControllerを持たないものを全て削除します。
    /// </summary>
    /// <param name="parent">子を削除したい親オブジェクトのTransform</param>
    public void DestroyAllChildrenExceptOutline(Transform parent)
    {
        // foreachループで全ての子オブジェクトを順番にチェック
        foreach (Transform child in parent)
        {
            // 子オブジェクトがOutlineControllerコンポーネントを持っているか確認
            if (child.GetComponent<OutlineController>() == null)
            {
                // 持っていない場合（＝Outlineオブジェクトではない場合）のみ削除
                Destroy(child.gameObject);
            }
            // 持っている場合は何もしないので、安全にスキップされる
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (leftBound == 0 || rightBound == 0)
        {
            return; // 左右の境界が設定されていない場合は何もしない
        }

        // 四角形の中心座標
        Vector3 center = new Vector3(
            (leftBound + rightBound) / 2f,
            transform.position.y + 1.5f, // Y座標は1.5f上に設定
            transform.position.z
        );

        // 四角形のサイズ
        Vector3 size = new Vector3(
            rightBound - leftBound,
            3f, // 高さ（Y軸）は3に設定
            0.1f // 厚み（Z軸）は薄く
        );

        // ----- 塗りつぶし：オレンジの半透明 -----
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f); // RGBA（オレンジ・半透明）
        Gizmos.DrawCube(center, size);

        // ----- 枠線：赤 -----
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center, size);
    }
}
