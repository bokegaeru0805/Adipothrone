using System.Collections;
using Effekseer;
using UnityEngine;

public class GhostwolfMoveController : MonoBehaviour
{
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

    [SerializeField]
    private int chargedShotDamage = 0; //扇状弾幕攻撃(溜め攻撃)の弾のダメージ量

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

    [Header("弾のプレハブ")]
    [SerializeField]
    private GameObject shoot_prefab1; //弾1のプレハブ

    [SerializeField]
    private GameObject shoot_prefab2; //弾2のプレハブ

    [SerializeField]
    private GameObject shoot_prefab3; //弾3のプレハブ

    [Header("弾幕が出るときのスプライト")]
    [SerializeField]
    private Sprite howlsprite; //弾幕が出るときのスプライト

    [Header("エフェクト")]
    [SerializeField]
    private EffekseerEmitter chargeEffect;

    [SerializeField, Tooltip("扇状弾幕攻撃(溜め攻撃)のエフェクトの大きさ")]
    private float chargeRange = 10f;

    [SerializeField]
    private EffekseerEmitter shockWaveEffect;

    [SerializeField]
    private float ChargeeffectoffsetY; //エフェクトのY座標を調整
    private int totalAttacks = 0; //攻撃した回数
    private float action_mode = 0; // 行動モードを初期化
    private float gravity = 9.81f; //重力の数値
    private float hpPercent = 100;
    private int bossMaxHP; //最大HP
    private int bossHP; //現在のHP
    private bool isFirstHPbelowHalf = false; //HPが半分以下になったかどうかのフラグ
    private Vector3 playerPos; //プレイヤーの位置を保存するための変数
    private Animator animator;
    private IDamageable hpscript;
    private Color col;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        if (
            normalShootDamage <= 0
            || rainDamage <= 0
            || flatShootDamage <= 0
            || chargedShotDamage <= 0
        )
        {
            Debug.LogError("GhostWolfに弾のダメージ量が設定されていません。");
        }

        if (shoot_prefab1 == null || shoot_prefab2 == null || shoot_prefab3 == null)
        {
            Debug.LogError("GhostWolfに弾のプレハブが設定されていません。");
        }

        if (howlsprite == null)
        {
            Debug.LogError("GhostWolfに弾幕が出るときのスプライトが設定されていません。");
        }

        if (shockWaveEffect == null || chargeEffect == null)
        {
            Debug.LogError("GhostWolfにエフェクトが設定されていません。");
        }

        spriteRenderer = this.GetComponent<SpriteRenderer>();
        animator = this.GetComponent<Animator>(); //Animatorのコンポーネントを取得
    }

    private void Start()
    {
        if (PlayerObject == null)
            PlayerObject = GameObject.FindGameObjectWithTag(GameConstants.PlayerTagName); // プレイヤーオブジェクトを探して格納

        animator.SetFloat("stay_speed", 0.250f / staySec); //stayアニメーションの時間を調整
        hpscript = this.GetComponent<IDamageable>(); //hpのscriptを取得
        bossMaxHP = hpscript.MaxHP; //最大HPを取得
        gravity = Mathf.Abs(Physics.gravity.y); //重力の大きさを取得
        col = new Color(1, 1, 0, 1); //colを初期化
        spriteRenderer.color = col; //colorコンポーネントを初期化
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
                bossHP = hpscript.CurrentHP; //現在のHPを取得
                hpPercent = ((float)bossHP / (float)bossMaxHP) * 100f; //HPの割合を取得
                int attackversion = Random.Range(0, 2);

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
                            if (attackversion == 0)
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
        GameObject newGameObject = Instantiate(shoot_prefab1) as GameObject; // 弾1のプレハブを生成
        newGameObject.transform.SetParent(this.transform); // 弾の親をこのオブジェクトに設定
        newGameObject.tag = GameConstants.DamageableEnemyTagName; //弾のタグを設定
        var script = newGameObject.GetComponent<ContactDamageController>(); //ダメージに関するスクリプトを取得
        if (script != null)
        {
            script.SetDamageAmount(normalShootDamage); //弾のダメージを設定
        }
        else
        {
            Debug.LogWarning($"EnemyStateControllerが{newGameObject.name}に見つかりませんでした。");
        }
        newGameObject.transform.position = newPos; //弾の位置を設定
        Rigidbody2D newrbody = newGameObject.GetComponent<Rigidbody2D>(); //弾のRigidbody2Dを取得
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
        if (SEManager.instance != null)
            SEManager.instance.PlayEnemyActionSE(SE_EnemyAction.Shoot2_Enemy); //攻撃の効果音を鳴らす

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
            GameObject newGameObject = Instantiate(shoot_prefab1) as GameObject; // 弾1のプレハブを生成
            newGameObject.transform.SetParent(this.transform); // 弾の親をこのオブジェクトに設定
            newGameObject.tag = GameConstants.DamageableEnemyTagName; //弾のタグを設定
            var script = newGameObject.GetComponent<ContactDamageController>(); //ダメージに関するスクリプトを取得
            if (script != null)
            {
                script.SetDamageAmount(normalShootDamage); //弾のダメージを設定
            }
            else
            {
                Debug.LogWarning(
                    $"EnemyStateControllerが{newGameObject.name}に見つかりませんでした。"
                );
            }
            newGameObject.transform.position = newPos; //弾の位置を設定
            Rigidbody2D newrbody = newGameObject.GetComponent<Rigidbody2D>(); //弾のRigidbody2Dを取得
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
            if (SEManager.instance != null)
                SEManager.instance.PlayEnemyActionSE(SE_EnemyAction.Shoot2_Enemy); //攻撃の効果音を鳴らす
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
            GameObject newGameObject = Instantiate(shoot_prefab2) as GameObject; // 弾2のプレハブを生成
            newGameObject.transform.SetParent(this.transform); // 弾の親をこのオブジェクトに設定
            newGameObject.tag = GameConstants.DamageableEnemyTagName; //弾のタグを設定
            var script = newGameObject.GetComponent<ContactDamageController>(); //ダメージに関するスクリプトを取得
            if (script != null)
            {
                script.SetDamageAmount(rainDamage); //弾のダメージを設定
            }
            else
            {
                Debug.LogWarning(
                    $"EnemyStateControllerが{newGameObject.name}に見つかりませんでした。"
                );
            }
            playerPos = PlayerObject.transform.position; //プレイヤーの座標を取得
            newGameObject.transform.position = new Vector2(
                Random.Range(playerPos.x - rainRange / 2, playerPos.x + rainRange / 2),
                ceilingHeight
            ); //弾の位置を設定
            Rigidbody2D newrbody = newGameObject.GetComponent<Rigidbody2D>(); //弾のRigidbody2Dを取得
            newrbody.gravityScale = 0; //弾の重力を消去
            newrbody.AddForce(new Vector2(0, -drop_speed), ForceMode2D.Impulse); //弾の落下速度を設定
            if (SEManager.instance != null)
                SEManager.instance.PlayFieldSE(SE_Field.WaterDrip1); //攻撃の効果音を鳴らす
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
            GameObject newGameObject = Instantiate(shoot_prefab3) as GameObject; // 弾4のプレハブを生成
            newGameObject.transform.localScale = Vector3.one * 2.5f; //弾のサイズを調整(子オブジェクトにする前に行う)
            newGameObject.transform.SetParent(this.transform); // 弾の親をこのオブジェクトに設定
            newGameObject.tag = GameConstants.DamageableEnemyTagName; //弾のタグを設定
            var script = newGameObject.GetComponent<ContactDamageController>(); //ダメージに関するスクリプトを取得
            if (script != null)
            {
                script.SetDamageAmount(flatShootDamage); //弾のダメージを設定
            }
            else
            {
                Debug.LogWarning(
                    $"EnemyStateControllerが{newGameObject.name}に見つかりませんでした。"
                );
            }
            newGameObject.transform.position = newPos; //弾の位置を設定
            Rigidbody2D newrbody = newGameObject.GetComponent<Rigidbody2D>(); //弾のRigidbody2Dを取得
            newrbody.gravityScale = 0; //弾の重力を無効化

            float targetHeight = RobotHeight - flatShootRadius * Random.Range(0, 2);
            Vector2 flatvelocity =
                new Vector2(flatshoot_offsetX - shoot_offsetX, targetHeight - newPos.y).normalized
                * flatShootSpeed; //弾の速度を計算
            newrbody.AddForce(new Vector2(flatvelocity.x, flatvelocity.y), ForceMode2D.Impulse); //弾の速度を設定
            if (SEManager.instance != null)
                SEManager.instance.PlayEnemyActionSE(SE_EnemyAction.Shoot1_Enemy); //攻撃の効果音を鳴らす
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

        float ChargeEffectStart_Sec = Attack5howlSec - 5.0f; //エフェクトの開始時間を設定
        if (ChargeEffectStart_Sec < 0)
            ChargeEffectStart_Sec = 0; //エフェクトの開始時間を設定
        yield return new WaitForSeconds(ChargeEffectStart_Sec); //エフェクトの開始まで待つ

        if (chargeEffect != null)
        {
            EffekseerEmitter chargeEffectInstance = Instantiate(chargeEffect); //エフェクトを生成
            chargeEffectInstance.transform.SetParent(this.transform); //エフェクトの親をこのオブジェクトに設定
            Vector2 chargeEffectPos = this.transform.position; //自分の座標を保存
            chargeEffectPos.y += ChargeeffectoffsetY; //エフェクトのy座標を調整
            chargeEffectInstance.transform.position = chargeEffectPos; //エフェクトの位置を指定
            chargeEffectInstance.transform.localScale = new Vector2(chargeRange, chargeRange); //エフェクトの大きさを指定
            chargeEffectInstance.Play(); //エフェクトを再生
        }

        while (spriteRenderer.sprite != howlsprite)
        {
            if (SEManager.instance != null)
            {
                if (!SEManager.instance.IsPlayingEnemyActionSE(SE_EnemyAction.ChargePower1))
                {
                    SEManager.instance.PlayEnemyActionSE(SE_EnemyAction.ChargePower1); //チャージの効果音を鳴らす
                }
            }
            yield return null; //少し待つ
        }

        SEManager.instance?.StopEnemyActionSE(SE_EnemyAction.ChargePower1); //チャージの効果音を止める
        Vector3 newPos = this.transform.position; //自分の座標を保存
        newPos.x += shoot_offsetX; //弾のx座標を調整
        newPos.y += shoot_offsetX; //弾のy座標を調整

        playerPos = PlayerObject.transform.position; //プレイヤーの座標を取得
        SEManager.instance?.PlayEnemyActionSE(SE_EnemyAction.Roar1); //咆哮の効果音を鳴らす
        GameUIManager.instance?.ShowSkillNameUI("咆哮"); //スキル名UIを表示

        if (shockWaveEffect != null)
        {
            EffekseerEmitter shockWaveEffectInstance = Instantiate(shockWaveEffect); //エフェクトを生成
            shockWaveEffectInstance.transform.SetParent(this.transform); //エフェクトの親をこのオブジェクトに設定
            shockWaveEffectInstance.transform.position = this.transform.position; //エフェクトの位置を指定
            float shockwaveRange = 2 * Mathf.Abs(newPos.x - leftBoundary); //エフェクトの大きさを取得
            shockWaveEffectInstance.transform.localScale = new Vector3(
                shockwaveRange,
                shockwaveRange,
                0
            ); //エフェクトの大きさを指定
            shockWaveEffectInstance.Play(); //エフェクトを再生
        }

        for (int i = 0; i < arcShootCount; i++)
        {
            GameObject newGameObject = Instantiate(shoot_prefab3) as GameObject; // 弾5のプレハブを生成
            newGameObject.transform.localScale = Vector3.one * 2.5f; //弾のサイズを調整(子オブジェクトにする前に行う)
            newGameObject.transform.SetParent(this.transform); // 弾の親をこのオブジェクトに設定
            newGameObject.tag = GameConstants.DamageableEnemyTagName; //弾のタグを設定
            var script = newGameObject.GetComponent<ContactDamageController>(); //ダメージに関するスクリプトを取得
            if (script != null)
            {
                script.SetDamageAmount(chargedShotDamage); //弾のダメージを設定
            }
            else
            {
                Debug.LogWarning(
                    $"EnemyStateControllerが{newGameObject.name}に見つかりませんでした。"
                );
            }
            newGameObject.transform.position = newPos; //弾の位置を設定
            Rigidbody2D newrbody = newGameObject.GetComponent<Rigidbody2D>(); //弾のRigidbody2Dを取得
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
                    Destroy(shoot);
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
                    Destroy(shoot);
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
