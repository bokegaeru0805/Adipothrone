using System.Collections;
using MyGame.CameraControl;
using UnityEngine;

[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class GhostwolfMoveController : MonoBehaviour
{
    private const string POOL_TAG_SHOOT1 = "TutorialStageShoot1"; // 弾1のプールタグ
    private const string POOL_TAG_SHOOT2 = "RainBullet"; // 弾2のプールタグ
    private const string POOL_TAG_SHOOT3 = "TutorialStageShoot2"; // 弾3のプールタグ

    private GameObject PlayerObject; //ターゲットオブジェクトを定義

    [Header("行動範囲のパラメータ")]
    [SerializeField]
    private float leftBoundary; //行動範囲の左端

    [SerializeField]
    private float rightBoundary; //行動範囲の左端

    [SerializeField]
    private float ExistBottom; //弾が存在できる一番下の座標

    [Header("弾のダメージ量")]
    [SerializeField]
    private int normalShootDamage = 0; //通常の弾のダメージ量

    [SerializeField]
    private int rainDamage = 0; //降雨の弾のダメージ量

    [SerializeField]
    private int flatShootDamage = 0; //地面に平行に動く弾のダメージ量

    [Header("弾のパラメータ")]
    [SerializeField, Tooltip("弾が上昇する最大の高さ")]
    private float maxHeightoffset; //弾が上昇する最大の高さ

    [SerializeField, Tooltip("弾が降ってくる天井の高さ")]
    private float ceilingHeight; //弾が降ってくる天井の高さ

    [SerializeField]
    private float RobotHeight; //Robotの通常の高さ

    [SerializeField]
    private float shoot_offsetX;

    [SerializeField]
    private float shoot_offsetY;

    [SerializeField]
    private float flatshoot_offsetX;

    [SerializeField]
    private float rainRange; //降雨の攻撃の範囲

    [SerializeField]
    private int DropTimesMin; //降雨の回数の最小値

    [SerializeField]
    private int DropTimesMax; //降雨の回数の最大値

    [SerializeField]
    private float DropFallTime; //攻撃3の降雨が地面にたどり着くまでの時間

    [SerializeField]
    private float flatShootSpeed; //攻撃４の地面と平行な弾の速度

    [SerializeField]
    private float flatShootIntervalMin; //攻撃４の弾の間隔の最小値

    [SerializeField]
    private float flatShootIntervalMax; //攻撃４の弾の間隔の最大値

    [SerializeField]
    private float flatShootRadius; //攻撃４の弾の動く半径の値

    [SerializeField]
    private int arcShootCount; //攻撃５の弾の個数

    [SerializeField]
    private float arcShootSpeed; //攻撃５の弾の速度

    [Header("アニメーションのパラメータ")]
    [SerializeField]
    private float staySec; //stay時のアニメーションの早さ

    [SerializeField, Tooltip("単発放物線攻撃の咆哮のアニメーションの長さ")]
    private float Attack1howlSec; //攻撃1の時の咆哮のアニメーションの長さ

    [SerializeField, Tooltip("連続放物線攻撃の咆哮のアニメーションの長さ")]
    private float Attack2howlSec; //攻撃2の時の咆哮のアニメーションの長さ

    [SerializeField, Tooltip("降雨攻撃の咆哮のアニメーションの長さ")]
    private float Attack3howlSec; //攻撃3の時の咆哮のアニメーションの長さ

    [SerializeField, Tooltip("平行弾攻撃の咆哮のアニメーションの長さ")]
    private float Attack4howlSec; //攻撃4の時の咆哮のアニメーションの長さ

    [SerializeField, Tooltip("扇状弾幕攻撃(溜め攻撃)の咆哮のアニメーションの長さ")]
    private float Attack5howlSec; //攻撃5の時の咆哮のアニメーションの長さ

    [Header("攻撃の待機時間")]
    [SerializeField, Tooltip("単発放物線攻撃")]
    private float Attack1wait_Sec; //攻撃1の後の待機時間の長さ

    [SerializeField, Tooltip("連続放物線攻撃")]
    private float Attack2wait_Sec; //攻撃2の後の待機時間の長さ

    [SerializeField, Tooltip("降雨攻撃")]
    private float Attack3wait_Sec; //攻撃3の後の待機時間の長さ

    [SerializeField, Tooltip("平行弾攻撃")]
    private float Attack4wait_Sec; //攻撃4の後の待機時間の長さ

    [SerializeField, Tooltip("扇状弾幕攻撃(溜め攻撃)")]
    private float Attack5wait_Sec; //攻撃5の後の待機時間の長さ

    [Header("弾幕が出るときのスプライト")]
    [SerializeField]
    private Sprite howlsprite; //弾幕が出るときのスプライト

    [Header("エフェクト")]
    [SerializeField]
    private ChargeEffect_Master chargeEffect; //チャージエフェクト

    [SerializeField]
    private BurstEffect_Master burstEffect; //衝撃波エフェクト

    private int totalAttacks = 0; //攻撃した回数
    private float action_mode = 0; // 行動モードを初期化
    private float gravity = 9.81f; //重力の数値
    private bool isFirstHPbelowHalf = false; //HPが半分以下になったかどうかのフラグ
    private Vector3 playerPos; //プレイヤーの位置を保存するための変数
    private Animator animator;
    private BossHealth hpscript;
    private SpriteRenderer spriteRenderer;
    private CriWare.Assets.CriAtomSePlayer _sePlayer;

    private void Awake()
    {
        if (normalShootDamage <= 0 || rainDamage <= 0 || flatShootDamage <= 0)
        {
            Debug.LogError("GhostWolfに弾のダメージ量が設定されていません。", this);
        }

        if (howlsprite == null)
        {
            Debug.LogError("GhostWolfに弾幕が出るときのスプライトが設定されていません。", this);
        }

        if (chargeEffect == null || burstEffect == null)
        {
            Debug.LogError("GhostWolfにエフェクトが設定されていません。", this);
        }

        spriteRenderer = this.GetComponent<SpriteRenderer>();
        animator = this.GetComponent<Animator>();
        _sePlayer = this.GetComponent<CriWare.Assets.CriAtomSePlayer>();
    }

    private void Start()
    {
        if (PlayerObject == null)
            PlayerObject = GameObject.FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME); // プレイヤーオブジェクトを探して格納

        animator.SetFloat("stay_speed", 0.250f / staySec); //stayアニメーションの時間を調整
        hpscript = this.GetComponent<BossHealth>(); //hpのscriptを取得
        hpscript.InitializeBossSpecifics(); //ボス固有の初期化を実行
        gravity = Mathf.Abs(Physics.gravity.y); //重力の大きさを取得
        action_mode = 0; //行動モードを0に設定
    }

    private void FixedUpdate()
    {
        // 敵の動きがポーズされているかどうかを確認
        if (TimeManager.instance.isEnemyMovePaused)
        {
            return;
        }

        switch (action_mode)
        {
            case 0:
                float hpPercent = hpscript.NormalizedHP * 100; //HPの割合を取得
                int attackversion = Random.Range(0, 4); //攻撃パターンのバージョンをランダムに決定(0~3)

                if (!isFirstHPbelowHalf && hpPercent < 50)
                {
                    isFirstHPbelowHalf = true; //初めてHPが半分以下になった
                    action_mode = 5; //扇状弾幕攻撃(溜め攻撃)
                    break;
                }

                switch (hpPercent)
                {
                    case >= 70:
                        if (attackversion == 0)
                        {
                            action_mode = 3; //上空からの降雨の弾
                        }
                        else
                        {
                            action_mode = 1; //1発の放物線を描く弾
                        }
                        break;
                    case >= 40:
                        if (attackversion == 0)
                        {
                            action_mode = 4; //地面に平行に動くHPのある弾
                        }
                        else
                        {
                            action_mode = 2; //3発の放物線を描く弾
                        }
                        break;
                    default:
                        if (totalAttacks >= 3)
                        {
                            action_mode = 5;
                            totalAttacks = -1;
                        }
                        else
                        {
                            if (attackversion == 0 || attackversion == 1)
                            {
                                action_mode = 4; //地面に平行に動くHPのある弾
                            }
                            else
                            {
                                action_mode = 2; //3発の放物線を描く弾
                            }
                        }
                        break;
                }
                totalAttacks += 1;
                break;
            case 1:
                StartCoroutine(Attack1()); //1発の放物線を描く弾
                action_mode = -1;
                break;
            case 2:
                StartCoroutine(Attack2()); //3発の放物線を描く弾
                action_mode = -1;
                break;
            case 3:
                StartCoroutine(Attack3()); //上空からの降雨の弾
                action_mode = -1;
                break;
            case 4:
                StartCoroutine(Attack4()); //地面に平行に動くHPのある弾
                action_mode = -1;
                break;
            case 5:
                StartCoroutine(Attack5()); //円弧状に発射されるHPのある弾
                action_mode = -1;
                break;
        }
    }

    private IEnumerator Attack1()
    {
        animator.SetFloat("howl_speed", 0.583f / Attack1howlSec); //howlアニメーションの時間を調整
        animator.SetTrigger("howl");
        animator.SetBool("isFinishHowl", false);

        yield return new WaitUntil(() => spriteRenderer.sprite == howlsprite); //特定のスプライトになるまで待つ

        Vector3 newPos = this.transform.position; //自分の座標を保存
        newPos.x += shoot_offsetX; //弾のx座標を調整
        newPos.y += shoot_offsetX; //弾のy座標を調整
        GameObject newGameObject = ObjectPooler.SceneInstance.SpawnFromPool(
            POOL_TAG_SHOOT1,
            newPos,
            Quaternion.identity
        ); // 弾1をプールから生成
        newGameObject.tag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME; //弾のタグを設定
        var script = newGameObject.GetComponent<ContactDamageController>(); //ダメージに関するスクリプトを取得
        if (script != null)
        {
            script.SetNormalDamage(normalShootDamage); //弾のダメージを設定
        }
        else
        {
            Debug.LogWarning($"EnemyStateControllerが{newGameObject.name}に見つかりませんでした。");
        }
        var newrbody = newGameObject.GetComponent<Rigidbody2D>(); //弾のRigidbody2Dを取得
        newrbody.gravityScale = 1; //弾の重力を初期化
        float targetPointX = Random.Range(leftBoundary, rightBoundary); //着弾地点を設定
        playerPos = PlayerObject.transform.position; //プレイヤーの座標を取得
        float vx =
            (playerPos.x - newPos.x)
            * Mathf.Sqrt(2 * gravity)
            * (-Mathf.Sqrt(maxHeightoffset) + Mathf.Sqrt(newPos.y - ExistBottom + maxHeightoffset))
            / (2 * (newPos.y - ExistBottom));
        float vy = Mathf.Sqrt(2 * gravity * maxHeightoffset);
        newrbody.AddForce(new Vector2(vx, vy), ForceMode2D.Impulse); //弾の速度を設定
        _sePlayer.Play(SE_EnemyAction.Shoot2_Enemy); //攻撃の効果音を鳴らす
        CameraManager.instance?.PlayCustomShake(1.0f, 3.0f, 0.3f); // カメラシェイクを再生

        StartCoroutine(DestroyShoot(newGameObject));

        animator.SetBool("isFinishHowl", true);
        StartCoroutine(MoveStart(Attack1wait_Sec));
    }

    private IEnumerator Attack2()
    {
        Vector3 newPos = this.transform.position; //自分の座標を保存
        newPos.x += shoot_offsetX; //弾のx座標を調整
        newPos.y += shoot_offsetX; //弾のy座標を調整
        animator.SetFloat("howl_speed", 0.583f / Attack2howlSec); //howlアニメーションの時間を調整

        animator.SetTrigger("howl");
        animator.SetBool("isFinishHowl", false);

        yield return new WaitUntil(() => spriteRenderer.sprite == howlsprite);

        for (int i = 0; i < 3; i++)
        {
            GameObject newGameObject = ObjectPooler.SceneInstance.SpawnFromPool(
                POOL_TAG_SHOOT1,
                newPos,
                Quaternion.identity
            ); // 弾1をプールから生成
            newGameObject.tag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME; //弾のタグを設定
            var script = newGameObject.GetComponent<ContactDamageController>(); //ダメージに関するスクリプトを取得
            if (script != null)
            {
                script.SetNormalDamage(normalShootDamage); //弾のダメージを設定
            }
            else
            {
                Debug.LogWarning(
                    $"EnemyStateControllerが{newGameObject.name}に見つかりませんでした。"
                );
            }
            var newrbody = newGameObject.GetComponent<Rigidbody2D>(); //弾のRigidbody2Dを取得
            newrbody.gravityScale = 1; //弾の重力を初期化
            float targetPointX = Random.Range(leftBoundary, rightBoundary); //着弾地点を設定
            playerPos = PlayerObject.transform.position; //プレイヤーの座標を取得
            float vx =
                (
                    (playerPos.x - newPos.x)
                    * Mathf.Sqrt(2 * gravity)
                    * (
                        -Mathf.Sqrt(maxHeightoffset)
                        + Mathf.Sqrt(newPos.y - ExistBottom + maxHeightoffset)
                    )
                ) / (2 * (newPos.y - ExistBottom));
            float vy = Mathf.Sqrt(2 * gravity * maxHeightoffset);
            newrbody.AddForce(new Vector2(vx, vy), ForceMode2D.Impulse); //弾の速度を設定
            _sePlayer.Play(SE_EnemyAction.Shoot2_Enemy); //攻撃の効果音を鳴らす
            CameraManager.instance?.PlayCustomShake(1.0f, 3.0f, 0.3f); // カメラシェイクを再生
            StartCoroutine(DestroyShoot(newGameObject));
            yield return new WaitForSeconds(Random.Range(0.5f, 1)); //次の攻撃までの時間を設定
        }

        animator.SetBool("isFinishHowl", true);
        StartCoroutine(MoveStart(Attack2wait_Sec));
    }

    private IEnumerator Attack3()
    {
        animator.SetFloat("howl_speed", 0.583f / Attack3howlSec); //howlアニメーションの時間を調整
        animator.SetTrigger("howl");
        animator.SetBool("isFinishHowl", false);

        yield return new WaitUntil(() => spriteRenderer.sprite == howlsprite);

        int droptimes = Random.Range(DropTimesMin, DropTimesMax + 1); //降雨の回数を設定
        Vector3 newPos = this.transform.position; //自分の座標を保存
        float drop_speed = (ceilingHeight - ExistBottom) / DropFallTime; //雨の速さを指定

        for (int i = 0; i < droptimes; i++)
        {
            playerPos = PlayerObject.transform.position; //プレイヤーの座標を取得
            Vector2 spawnPos = new Vector2(
                Random.Range(playerPos.x - rainRange / 2, playerPos.x + rainRange / 2),
                ceilingHeight
            ); //弾の生成位置を計算

            GameObject newGameObject = ObjectPooler.SceneInstance.SpawnFromPool(
                POOL_TAG_SHOOT2,
                spawnPos,
                Quaternion.identity
            ); // 弾2をプールから生成
            newGameObject.tag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME; //弾のタグを設定
            var script = newGameObject.GetComponent<ContactDamageController>(); //ダメージに関するスクリプトを取得
            if (script != null)
            {
                script.SetNormalDamage(rainDamage); //弾のダメージを設定
            }
            else
            {
                Debug.LogWarning(
                    $"EnemyStateControllerが{newGameObject.name}に見つかりませんでした。"
                );
            }
            var newrbody = newGameObject.GetComponent<Rigidbody2D>(); //弾のRigidbody2Dを取得
            newrbody.gravityScale = 0; //弾の重力を消去
            newrbody.AddForce(new Vector2(0, -drop_speed), ForceMode2D.Impulse); //弾の落下速度を設定
            _sePlayer.Play(SE_Field.WaterDrip1); //攻撃の効果音を鳴らす
            StartCoroutine(DestroyShoot(newGameObject));
            yield return new WaitForSeconds(Random.Range(0.5f, 0.75f)); //次の降雨までの時間を設定
        }

        animator.SetBool("isFinishHowl", true);
        StartCoroutine(MoveStart(Attack3wait_Sec));
    }

    private IEnumerator Attack4()
    {
        animator.SetFloat("howl_speed", 0.583f / Attack4howlSec); //howlアニメーションの時間を調整
        animator.SetTrigger("howl");
        animator.SetBool("isFinishHowl", false);

        yield return new WaitUntil(() => spriteRenderer.sprite == howlsprite);

        int shoottimes = Random.Range(3, 6); //弾の発射の回数を設定
        Vector3 newPos = this.transform.position; //自分の座標を保存
        newPos.x += shoot_offsetX; //弾のx座標を調整
        newPos.y += shoot_offsetX; //弾のy座標を調整

        for (int i = 0; i < shoottimes; i++)
        {
            GameObject newGameObject = ObjectPooler.SceneInstance.SpawnFromPool(
                POOL_TAG_SHOOT3,
                newPos,
                Quaternion.identity
            ); // 弾をプールから生成
            newGameObject.transform.localScale = Vector3.one * 2.5f; //弾のサイズを調整(子オブジェクトにする前に行う)
            newGameObject.tag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME; //弾のタグを設定
            var script = newGameObject.GetComponent<ContactDamageController>(); //ダメージに関するスクリプトを取得
            if (script != null)
            {
                script.SetNormalDamage(flatShootDamage); //弾のダメージを設定
            }
            else
            {
                Debug.LogWarning(
                    $"EnemyStateControllerが{newGameObject.name}に見つかりませんでした。"
                );
            }
            var newrbody = newGameObject.GetComponent<Rigidbody2D>(); //弾のRigidbody2Dを取得
            newrbody.gravityScale = 0; //弾の重力を無効化

            float targetHeight = RobotHeight - flatShootRadius * Random.Range(0, 2);
            Vector2 flatvelocity =
                new Vector2(flatshoot_offsetX - shoot_offsetX, targetHeight - newPos.y).normalized
                * flatShootSpeed; //弾の速度を計算
            newrbody.AddForce(new Vector2(flatvelocity.x, flatvelocity.y), ForceMode2D.Impulse); //弾の速度を設定
            _sePlayer.Play(SE_EnemyAction.Shoot1_Enemy); //攻撃の効果音を鳴らす
            CameraManager.instance?.PlayCustomShake(1.5f, 2.0f, 0.3f); // カメラシェイクを再生
            StartCoroutine(DestroyFlatShoot(newGameObject, targetHeight));
            yield return new WaitForSeconds(
                Random.Range(flatShootIntervalMin, flatShootIntervalMax)
            ); //次の弾までの時間を設定
        }

        animator.SetBool("isFinishHowl", true);
        StartCoroutine(MoveStart(Attack4wait_Sec));
    }

    private IEnumerator Attack5()
    {
        animator.SetFloat("howl_speed", 0.583f / Attack5howlSec); //howlアニメーションの時間を調整
        animator.SetTrigger("howl");
        animator.SetBool("isFinishHowl", false);

        if (chargeEffect != null)
        {
            chargeEffect.SetDuration(Attack5howlSec * 0.885f); //チャージエフェクトの持続時間を設定
            CameraManager.instance?.PlayCustomShake(0.5f, 10.0f, Attack5howlSec * 0.885f); // カメラシェイクを再生
            chargeEffect.PlayEffect();
        }

        while (spriteRenderer.sprite != howlsprite)
        {
            yield return null; //少し待つ
        }

        _sePlayer.Stop(); //チャージの効果音を止める
        Vector3 newPos = this.transform.position; //自分の座標を保存
        newPos.x += shoot_offsetX; //弾のx座標を調整
        newPos.y += shoot_offsetX; //弾のy座標を調整

        playerPos = PlayerObject.transform.position; //プレイヤーの座標を取得
        CameraManager.instance?.PlayCustomShake(2.0f, 2.0f, 3f); // カメラシェイクを再生
        _sePlayer.Play(SE_EnemyAction.Roar1); //咆哮の効果音を鳴らす
        GameUIManager.instance?.ShowSkillNameUI("咆哮"); //スキル名UIを表示

        if (burstEffect != null)
        {
            burstEffect.PlayEffect();
        }

        for (int i = 0; i < arcShootCount; i++)
        {
            GameObject newGameObject = ObjectPooler.SceneInstance.SpawnFromPool(
                POOL_TAG_SHOOT3,
                newPos,
                Quaternion.identity
            ); // 弾5をプールから生成
            newGameObject.transform.localScale = Vector3.one * 2.5f; //弾のサイズを調整(子オブジェクトにする前に行う)
            newGameObject.tag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME; //弾のタグを設定
            var script = newGameObject.GetComponent<ContactDamageController>(); //ダメージに関するスクリプトを取得
            if (script != null)
            {
                script.SetCurrentHPRatioDamage(0.9f); //弾のダメージを設定
            }
            else
            {
                Debug.LogWarning(
                    $"EnemyStateControllerが{newGameObject.name}に見つかりませんでした。"
                );
            }
            var newrbody = newGameObject.GetComponent<Rigidbody2D>(); //弾のRigidbody2Dを取得
            newrbody.gravityScale = 0; //弾の重力を無効化

            float angleDeg =
                (playerPos.x - newPos.x <= 0 ? 110 : -70) + 140f * i / (arcShootCount - 1); // 発射角度を計算
            float angleRad = angleDeg * Mathf.Deg2Rad;
            newrbody.AddForce(
                new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)).normalized * arcShootSpeed,
                ForceMode2D.Impulse
            ); //弾の速度を設定
            StartCoroutine(DestroyShoot(newGameObject));
        }

        animator.SetBool("isFinishHowl", true);
        StartCoroutine(MoveStart(Attack5wait_Sec));
    }

    IEnumerator MoveStart(float wait_Sec)
    {
        yield return new WaitForSeconds(wait_Sec); //wait_Sec待つ
        action_mode = 0;
    }

    private IEnumerator DestroyShoot(GameObject shoot)
    {
        if (shoot == null)
            yield break;
        Rigidbody2D prefab_rbody = shoot.GetComponent<Rigidbody2D>(); //Rigidbody2Dコンポーネントを取得

        while (true)
        {
            if (shoot == null)
                yield break;

            //敵の動きがポーズされていないか確認
            if (!TimeManager.instance.isEnemyMovePaused)
            {
                if (prefab_rbody != null && !prefab_rbody.simulated)
                    prefab_rbody.simulated = true; //物理挙動を再起動する

                Vector3 pos = shoot.transform.position;
                if (pos.y < ExistBottom || pos.x < leftBoundary || pos.x > rightBoundary)
                {
                    var poolObj = shoot.GetComponent<PoolableObject>();
                    if (poolObj != null)
                    {
                        poolObj.ReturnToPool(); // プールに返却する
                    }
                    yield break;
                }

                float vx = prefab_rbody.velocity.x; //速度のx成分を取得
                float vy = prefab_rbody.velocity.y; //速度のy成分を取得
                shoot.transform.eulerAngles = new Vector3(
                    0,
                    0,
                    Mathf.Atan2(vy, vx) * Mathf.Rad2Deg
                ); //向きを設定
            }
            else
            {
                if (prefab_rbody != null)
                    prefab_rbody.simulated = false; //物理挙動を止める
            }

            yield return null; //1フレーム待って次のフレームで再評価する（フリーズ防止）
        }
    }

    private IEnumerator DestroyFlatShoot(GameObject shoot, float shootHeight)
    {
        if (shoot == null)
            yield break;
        Rigidbody2D prefab_rbody = shoot.GetComponent<Rigidbody2D>(); //Rigidbody2Dコンポーネントを取得

        while (true)
        {
            if (shoot == null)
                yield break;

            //敵が動きがポーズされていないか確認
            if (!TimeManager.instance.isEnemyMovePaused)
            {
                if (prefab_rbody != null && !prefab_rbody.simulated)
                    prefab_rbody.simulated = true; //物理挙動を再起動する

                Vector3 pos = shoot.transform.position;
                if (pos.y < ExistBottom || pos.x < leftBoundary || pos.x > rightBoundary)
                {
                    var poolObj = shoot.GetComponent<PoolableObject>();
                    if (poolObj != null)
                    {
                        poolObj.ReturnToPool(); // プールに返却する
                    }
                    yield break;
                }

                float vx = prefab_rbody.velocity.x; //速度のx成分を取得

                if (pos.y <= shootHeight)
                {
                    prefab_rbody.velocity = new Vector2(Mathf.Sign(vx) * flatShootSpeed, 0);
                }

                vx = prefab_rbody.velocity.x; //速度のx成分を取得
                float vy = prefab_rbody.velocity.y; //速度のy成分を取得
                shoot.transform.eulerAngles = new Vector3(
                    0,
                    0,
                    Mathf.Atan2(vy, vx) * Mathf.Rad2Deg
                ); //向きを設定
            }
            else
            {
                if (prefab_rbody != null)
                    prefab_rbody.simulated = false; //物理挙動を止める
            }

            yield return null; //1フレーム待って次のフレームで再評価する（フリーズ防止）
        }
    }

    private void OnDestroy()
    {
        ObjectPooler.SceneInstance?.ReturnAllToPool();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 center2 = new Vector3(
            (leftBoundary + rightBoundary) / 2f,
            (20 + ExistBottom) / 2f,
            0f
        );
        Vector3 size2 = new Vector3(Mathf.Abs(leftBoundary - rightBoundary), 20, 0f);
        Gizmos.DrawWireCube(center2, size2);
    }
}
