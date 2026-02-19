using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC "Fill" の攻撃を制御するクラス
/// ターゲットの移動を予測した偏差射撃や、特定のボス行動に対する迎撃を行います。
/// </summary>
public class FillAttackController : MonoBehaviour
{
    #region 定義 (Definitions)
    private const string BULLET_POOL_TAG = "FillBullet";

    /// <summary>
    /// Fillの形態
    /// </summary>
    public enum FillFormType
    {
        Normal,
        Armed1,
        Armed2,
    }

    /// <summary>
    /// 形態ごとの攻撃パラメータをまとめた構造体
    /// </summary>
    [System.Serializable]
    public struct FormAttackSettings
    {
        [Tooltip("弾の攻撃力")]
        public int damage;

        [Tooltip("弾の移動速度")]
        public float bulletSpeed;

        [Tooltip("攻撃と攻撃の間の待機時間の最小値（秒）")]
        public float attackIntervalMin;

        [Tooltip("攻撃と攻撃の間の待機時間の最大値（秒）")]
        public float attackIntervalMax;

        [Tooltip(
            "射撃精度（ゆらぎ）：発射角度の最大ブレ幅（度）。\n"
                + "0を指定すると、予測した未来位置へ正確に発射します（必中）。\n"
                + "例えば「5」を指定した場合、本来の軌道から-5度〜+5度の範囲でランダムにズレて発射され、人間らしい狙いのブレを表現できます。"
        )]
        public float accuracySpreadAngle;
    }
    #endregion

    #region パラメータ設定 (Settings)

    /// <summary>
    /// 現在の形態を外部から取得・設定するためのプロパティ
    /// 変更すると、次の攻撃から即座に新しい形態のパラメータが適用されます。
    /// </summary>
    public FillFormType CurrentForm
    {
        get => currentForm;
        set => currentForm = value;
    }

    [Tooltip("Normal形態の設定")]
    [SerializeField]
    private FormAttackSettings normalSettings = new FormAttackSettings
    {
        damage = 0,
        bulletSpeed = 0f,
        attackIntervalMin = 0f,
        attackIntervalMax = 0f,
        accuracySpreadAngle = 0f, // ブレが大きい
    };

    [Tooltip("Armed1形態の設定")]
    [SerializeField]
    private FormAttackSettings armed1Settings = new FormAttackSettings
    {
        damage = 10,
        bulletSpeed = 15f,
        attackIntervalMin = 1.5f,
        attackIntervalMax = 3f,
        accuracySpreadAngle = 2f, // 少しブレる
    };

    [Tooltip("Armed2形態の設定")]
    [SerializeField]
    private FormAttackSettings armed2Settings = new FormAttackSettings
    {
        damage = 20,
        bulletSpeed = 20f,
        attackIntervalMin = 1f,
        attackIntervalMax = 2f,
        accuracySpreadAngle = 0f, // 必中
    };

    [Header("攻撃動作の設定")]
    [Tooltip("攻撃を開始する前の「Pray(祈り)」モーションにかける時間（秒）")]
    [SerializeField]
    private float prayDurationBeforeAttack = 2.0f;

    [Tooltip("弾を発射した後、元の姿勢に戻るまでの余韻の時間（秒）")]
    [SerializeField]
    private float prayDurationAfterAttack = 0.5f;

    [Tooltip("キャラクターの基準位置から、実際に弾が生成される位置へのオフセット")]
    [SerializeField]
    private Vector3 spawnOffset = new Vector3(0, 1.0f, 0);

    [Tooltip("降雨攻撃を迎撃する際の基準となる高さ（FillのY座標からの上方向へのオフセット）")]
    [SerializeField]
    private float interceptOffsetY = 5.0f;

    [Tooltip("迎撃する高さのランダムな揺れ幅（±この値だけ迎撃ラインが上下にブレます）")]
    [SerializeField]
    private float interceptYSpread = 1.0f;
    #endregion

    #region 内部変数 (Internal Variables)
    private FillFormType currentForm = FillFormType.Normal;
    private GameObject targetObj;
    private Animator animator;

    // コルーチン管理用
    private Coroutine attackCoroutine;
    private Coroutine antiRainCoroutine;

    // 偏差射撃のためのターゲット速度計算用
    private Vector3 targetPreviousPosition;
    private Vector3 targetVelocity;

    // 特殊連携用（特定のボス関連）
    private DesertTempleBossMoveController targetBossController;
    private Queue<GameObject> rainBulletsQueue = new Queue<GameObject>();

    // アニメーターのパラメータ名
    private readonly string isPrayingParam = "isPraying";
    #endregion

    #region Unityライフサイクル (Unity Lifecycle)
    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning(
                "FillAttackController: Animatorがアタッチされていません。攻撃モーションが再生されません。",
                this
            );
        }
    }

    private void Update()
    {
        // ターゲットが存在し、アクティブな場合は毎フレーム移動速度を計算する
        if (targetObj != null && targetObj.activeInHierarchy)
        {
            // 疑似的な速度の算出: (現在の座標 - 1フレーム前の座標) / 経過時間
            targetVelocity =
                (targetObj.transform.position - targetPreviousPosition) / Time.deltaTime;
            targetPreviousPosition = targetObj.transform.position;
        }
        else if (attackCoroutine != null)
        {
            // 攻撃中にも関わらずターゲットがDestroyされた、または非表示になった場合は攻撃を強制終了
            StopAttack();
        }
    }

    private void OnDestroy()
    {
        // オブジェクトが破棄される際は、メモリリークやエラーを防ぐためにイベント購読を必ず解除する
        if (targetBossController != null)
        {
            targetBossController.OnStateChanged -= HandleBossStateChanged;
            targetBossController.OnRainBulletFired -= HandleRainBulletFired;
        }
    }
    #endregion

    #region パブリックメソッド (Public Methods)
    /// <summary>
    /// ターゲットを指定して攻撃シーケンスを開始します。
    /// 外部の索敵スクリプトなどから呼ばれることを想定しています。
    /// </summary>
    /// <param name="target">攻撃対象のGameObject</param>
    public void StartAttack(GameObject target)
    {
        if (target == null)
            return;

        // すでに別のターゲットを攻撃中なら、進行中の処理をリセットする
        StopAttack();

        targetObj = target;
        targetPreviousPosition = targetObj.transform.position;
        targetVelocity = Vector3.zero;
        rainBulletsQueue.Clear();

        // ターゲットが特定のボスだった場合の連携処理セットアップ
        targetBossController = targetObj.GetComponent<DesertTempleBossMoveController>();
        if (targetBossController != null)
        {
            // ボスの状態変化と、降雨弾の生成イベントを監視
            targetBossController.OnStateChanged += HandleBossStateChanged;
            targetBossController.OnRainBulletFired += HandleRainBulletFired;
        }

        // 攻撃のメインループを起動
        attackCoroutine = StartCoroutine(AttackLoop());
    }

    /// <summary>
    /// 現在の攻撃を完全に終了し、アニメーションや各種状態をリセットします。
    /// </summary>
    public void StopAttack()
    {
        // 各種コルーチンを停止
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        if (antiRainCoroutine != null)
        {
            StopCoroutine(antiRainCoroutine);
            antiRainCoroutine = null;
        }

        // アニメーションを通常状態に戻す
        if (animator != null)
        {
            animator.SetBool(isPrayingParam, false);
        }

        // ボスのイベント購読を解除
        if (targetBossController != null)
        {
            targetBossController.OnStateChanged -= HandleBossStateChanged;
            targetBossController.OnRainBulletFired -= HandleRainBulletFired;
            targetBossController = null;
        }

        targetObj = null;
    }

    /// <summary>
    /// Fungusなどの外部ツールから、文字列で形態を設定するためのメソッドです。
    /// "Normal", "Armed1", "Armed2" のいずれかを指定します。
    /// </summary>
    /// <param name="formName">設定したい形態の名前</param>
    public void SetFormTypeByString(string formName)
    {
        // 文字列からEnumへの変換を試みる
        if (System.Enum.TryParse(formName, out FillFormType parsedForm))
        {
            CurrentForm = parsedForm;
        }
        else
        {
            Debug.LogWarning(
                $"FillAttackController: '{formName}' という形態は見つかりませんでした。"
            );
        }
    }

    /// <summary>
    /// Fungusなどの外部ツールから、整数（インデックス）で形態を設定するためのメソッドです。
    /// 0: Normal, 1: Armed1, 2: Armed2
    /// </summary>
    /// <param name="formIndex">設定したい形態のインデックス番号</param>
    public void SetFormTypeByInt(int formIndex)
    {
        // 指定された整数がEnumに定義されているか確認
        if (System.Enum.IsDefined(typeof(FillFormType), formIndex))
        {
            CurrentForm = (FillFormType)formIndex;
        }
        else
        {
            Debug.LogWarning(
                $"FillAttackController: インデックス '{formIndex}' に該当する形態はありません。"
            );
        }
    }

    #endregion

    #region 通常攻撃ロジック (Attack Logic)
    /// <summary>
    /// 現在の形態に応じた設定パラメーターを取得します。
    /// </summary>
    private FormAttackSettings GetCurrentSettings()
    {
        switch (currentForm)
        {
            case FillFormType.Armed1:
                return armed1Settings;
            case FillFormType.Armed2:
                return armed2Settings;
            case FillFormType.Normal:
            default:
                return normalSettings;
        }
    }

    /// <summary>
    /// 通常攻撃のメインループ（対象が生存している限り繰り返す）
    /// </summary>
    private IEnumerator AttackLoop()
    {
        while (true)
        {
            // 攻撃開始時点での形態設定を取得
            FormAttackSettings settings = GetCurrentSettings();

            // 1. 次の攻撃までのインターバル待機 (設定された最小〜最大の間でランダム)
            float waitTime = Random.Range(settings.attackIntervalMin, settings.attackIntervalMax);
            yield return new WaitForSeconds(waitTime);

            // 2. 予備動作 (Prayモーション開始)
            if (animator != null)
                animator.SetBool(isPrayingParam, true);

            // 3. 発射までのタメ時間を待機
            yield return new WaitForSeconds(prayDurationBeforeAttack);

            // 待機中にターゲットが消滅していないか最終確認
            if (targetObj != null && targetObj.activeInHierarchy)
            {
                // 4. 弾を発射 (ターゲットの未来位置を計算して撃つ)
                FireBulletAt(
                    targetObj.transform.position,
                    targetVelocity,
                    settings.bulletSpeed,
                    settings.accuracySpreadAngle,
                    settings.damage
                );
            }

            // 5. 発射後の余韻待機 (硬直時間)
            yield return new WaitForSeconds(prayDurationAfterAttack);

            // 6. 祈りモーション終了、次の攻撃インターバルへ
            if (animator != null)
                animator.SetBool(isPrayingParam, false);
        }
    }

    /// <summary>
    /// 目標の現在位置と速度から未来位置を予測し、指定のブレ幅（精度）を加えて弾を発射します。
    /// </summary>
    /// <param name="targetPos">ターゲットの現在位置</param>
    /// <param name="targetVel">ターゲットの現在の速度ベクトル</param>
    /// <param name="bulletSpeed">発射する弾の速度</param>
    /// <param name="spreadAngle">射撃のブレ幅（度）</param>
    /// <param name="damage">弾の攻撃力</param>
    private void FireBulletAt(
        Vector3 targetPos,
        Vector3 targetVel,
        float bulletSpeed,
        float spreadAngle,
        int damage
    )
    {
        Vector3 spawnPos = transform.position + spawnOffset;

        // --- 偏差射撃 (予測撃ち) の計算 ---
        // 自分からターゲットまでの直線距離を求める
        float distance = Vector3.Distance(spawnPos, targetPos);

        // 弾がターゲットの距離まで到達するのにかかるおおよその時間を算出
        float timeToHit = distance / bulletSpeed;

        // 弾が到達する頃にターゲットがどこにいるかを予測する (現在位置 + 速度 × 到達時間)
        Vector3 predictedTargetPos = targetPos + (targetVel * timeToHit);

        // 発射方向の基準ベクトルを算出 (予測位置へ向かう正規化ベクトル)
        Vector3 direction = (predictedTargetPos - spawnPos).normalized;

        // --- ゆらぎ（精度）の適用 ---
        if (spreadAngle > 0f)
        {
            // 指定された角度の範囲（-spreadAngle 〜 +spreadAngle）でランダムな角度を決定
            float randomAngle = Random.Range(-spreadAngle, spreadAngle);
            // Z軸を中心にベクトルを回転させ、意図的に射線をズラす
            direction = Quaternion.Euler(0, 0, randomAngle) * direction;
        }

        // ObjectPoolerを利用して弾を生成
        GameObject bullet = ObjectPooler.SceneInstance.SpawnFromPool(
            BULLET_POOL_TAG,
            spawnPos,
            Quaternion.identity
        );

        if (bullet != null)
        { // --- FillBulletコンポーネントに攻撃力をセット ---
            FillBullet fillBullet = bullet.GetComponent<FillBullet>();
            if (fillBullet != null)
            {
                fillBullet.Setup(damage);
            }

            // 弾の画像の向きを進行方向に合わせる (2D前提の角度計算)
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, angle);

            // Rigidbody2Dを取得し、計算した方向と速度を物理エンジンに渡す
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = direction * bulletSpeed;
            }
        }
    }
    #endregion

    #region ボス特殊連携アクション (Boss Specific Interactions)
    /// <summary>
    /// ボスが降雨の弾を生成した際に呼ばれるイベントコールバック
    /// </summary>
    /// <param name="rainBullet">ボスが生成した雨の弾オブジェクト</param>
    private void HandleRainBulletFired(GameObject rainBullet)
    {
        // 迎撃モードのコルーチン(antiRainCoroutine)が動いている時だけキューに追加する
        // （迎撃を行わないと判定された場合は、無視することでメモリを節約します）
        if (antiRainCoroutine != null && rainBullet != null)
        {
            rainBulletsQueue.Enqueue(rainBullet);
        }
    }

    /// <summary>
    /// ボスの状態が変化した際に呼ばれるイベントコールバック
    /// </summary>
    /// <param name="state">ボスの新しい状態</param>
    private void HandleBossStateChanged(DesertTempleBossMoveController.DesertTempleBossState state)
    {
        // ボスが降雨攻撃状態に切り替わった場合
        if (state == DesertTempleBossMoveController.DesertTempleBossState.RainAttacking)
        {
            // --- 追加：条件判定（今回は例として1/5の確率でアタリ） ---
            // Random.Range(0, 5) は 0, 1, 2, 3, 4 のいずれかを返します
            bool shouldIntercept = (Random.Range(0, 5) == 0);

            if (shouldIntercept)
            {
                // 【迎撃を行う場合】
                // 通常のボス本体への攻撃を中断する
                if (attackCoroutine != null)
                    StopCoroutine(attackCoroutine);

                // 迎撃モードのコルーチンを起動
                if (antiRainCoroutine != null)
                    StopCoroutine(antiRainCoroutine);

                rainBulletsQueue.Clear();
                antiRainCoroutine = StartCoroutine(AntiRainAttackSequence());
            }
            else
            {
                // 【迎撃を行わない場合】
                // 何もせず、ボス本体への通常攻撃（attackCoroutine）をそのまま継続します。
                // キューへの追加も、上の HandleRainBulletFired の制限により自動的に弾かれます。
            }
        }
        // 降雨攻撃が終わり、別の状態に移行した場合
        else
        {
            // 迎撃モード中だった場合のみ、終了して通常攻撃に戻す
            if (antiRainCoroutine != null)
            {
                // 迎撃モードを終了し、キューに残った弾情報をリセット
                StopCoroutine(antiRainCoroutine);
                antiRainCoroutine = null;
                rainBulletsQueue.Clear();

                // アニメーションを戻し、ボス本体への通常攻撃を再開する
                if (animator != null)
                    animator.SetBool(isPrayingParam, false);

                attackCoroutine = StartCoroutine(AttackLoop());
            }
        }
    }

    /// <summary>
    /// ボスの降雨攻撃中、キューに溜まった弾を次々と迎撃（撃ち落とす）シーケンス
    /// </summary>
    private IEnumerator AntiRainAttackSequence()
    {
        // 迎撃中はずっとPrayモーションを維持する
        if (animator != null)
            animator.SetBool(isPrayingParam, true);

        FormAttackSettings settings = GetCurrentSettings();

        // ボスの状態が「RainAttacking」である間は、ループを維持して迎撃し続ける
        while (
            targetBossController != null
            && targetBossController.CurrentState
                == DesertTempleBossMoveController.DesertTempleBossState.RainAttacking
        )
        {
            // キューに迎撃対象の弾が存在する限り撃ち続ける
            while (rainBulletsQueue.Count > 0)
            {
                // 一番古い弾を取り出す
                GameObject rainBullet = rainBulletsQueue.Dequeue();

                // 弾がまだシーン内に存在し、アクティブな場合のみ迎撃を実行
                if (rainBullet != null && rainBullet.activeInHierarchy)
                {
                    // 落下してくる雨の弾の速度を取得 (偏差射撃の計算に使用)
                    Vector3 bulletVelocity = Vector3.zero;
                    Rigidbody2D rb = rainBullet.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        bulletVelocity = rb.velocity;
                    }

                    // 迎撃ライン（特定のY座標）で交差するように計算して発射する専用メソッドを呼ぶ
                    FireInterceptBullet(rainBullet, bulletVelocity, settings.damage);

                    // 弾を1発撃ち落とした後、わずかにクールタイムを挟んで次を狙う
                    // (ボスの雨の生成間隔に合わせて数値を微調整するとより自然になります)
                    yield return new WaitForSeconds(0.1f);
                }
            }

            // キューが空の場合は、次の弾が降ってくるまで1フレーム待機
            yield return null;
        }

        // ボスが降雨攻撃を終え、ループを抜けたらモーションを解除
        if (animator != null)
            animator.SetBool(isPrayingParam, false);
    }

    /// <summary>
    /// 降雨攻撃の弾が「指定したY座標」に到達する時間を逆算し、
    /// そこへ向かってピッタリ到達するように自弾の速度と向きを計算して発射します。
    /// </summary>
    /// <param name="targetBullet">迎撃対象の雨の弾</param>
    /// <param name="targetVelocity">雨の弾の落下速度ベクトル</param>
    /// <param name="damage">迎撃弾の攻撃力</param>
    private void FireInterceptBullet(GameObject targetBullet, Vector3 targetVelocity, int damage)
    {
        // ターゲットが無効、またはY方向の速度がほぼ0の場合は計算できないため中断
        if (
            targetBullet == null
            || !targetBullet.activeInHierarchy
            || Mathf.Abs(targetVelocity.y) < 0.01f
        )
            return;

        Vector3 spawnPos = transform.position + spawnOffset;

        // 1. 迎撃したいY座標を決定 (FillのY座標 + 指定オフセット ± 揺れ幅)
        float targetY =
            transform.position.y
            + interceptOffsetY
            + Random.Range(-interceptYSpread, interceptYSpread);

        // 2. 雨の弾が targetY に到達するまでの時間 t を計算
        // 時間 = (目標距離) / (速度)
        float t = (targetY - targetBullet.transform.position.y) / targetVelocity.y;

        // 3. 安全装置: 計算された時間が 0 以下の場合の処理
        // （既に迎撃ラインより下まで落ちてしまっている場合など）
        if (t <= 0f)
        {
            // 時間計算が破綻した場合は、通常の偏差射撃（速さ固定）に切り替えて悪あがきをする
            FormAttackSettings settings = GetCurrentSettings();
            FireBulletAt(
                targetBullet.transform.position,
                targetVelocity,
                settings.bulletSpeed * 1.5f,
                settings.accuracySpreadAngle,
                settings.damage
            );
            return;
        }

        // 4. 時間 t 後の雨の弾の座標 (X, Y) ＝ 衝突予測地点 を計算
        Vector3 interceptPos = targetBullet.transform.position + (targetVelocity * t);

        // 5. Fillの弾が 時間 t で interceptPos に到達するための「必要な速度ベクトル」を計算
        // 速度ベクトル = (目標地点 - 発射地点) / かかる時間
        Vector3 requiredVelocity = (interceptPos - spawnPos) / t;

        // --- 弾の生成と発射 ---
        GameObject bullet = ObjectPooler.SceneInstance.SpawnFromPool(
            BULLET_POOL_TAG,
            spawnPos,
            Quaternion.identity
        );

        if (bullet != null)
        {
            // --- FillBulletコンポーネントに攻撃力をセット ---
            FillBullet fillBullet = bullet.GetComponent<FillBullet>();
            if (fillBullet != null)
            {
                fillBullet.Setup(damage);
            }

            // 弾の向きを計算した進行方向に合わせる
            float angle = Mathf.Atan2(requiredVelocity.y, requiredVelocity.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, angle);

            // 物理エンジンに計算した速度ベクトルをそのまま適用する（速さも自動的に変わる）
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = requiredVelocity;
            }
        }
    }
    #endregion

    #region デバッグ表示 (Gizmos)
    /// <summary>
    /// エディター上でオブジェクトを選択した際に、弾の発射位置をGizmosで可視化します。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 弾の発射予定位置を計算
        Vector3 spawnPos = transform.position + spawnOffset;

        // 赤色のワイヤーフレームの球体を描画
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(spawnPos, 0.3f);

        // --- 降雨攻撃の迎撃ラインの描画 ---
        // 迎撃ラインの基準となる高さを計算
        float baseInterceptY = transform.position.y + interceptOffsetY;
        Vector3 interceptCenter = new Vector3(
            transform.position.x,
            baseInterceptY,
            transform.position.z
        );

        // 迎撃範囲のブレ幅（± interceptYSpread）を可視化する半透明の緑色のボックスを描画
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        // 横幅は仮に 10.0f とし、高さはブレ幅の2倍（上下の合計）に設定
        Vector3 boxSize = new Vector3(10.0f, interceptYSpread * 2.0f, 0.1f);
        Gizmos.DrawCube(interceptCenter, boxSize);

        // 基準となる迎撃ラインの中心を明確にするため、濃い緑色で直線を引く
        Gizmos.color = Color.green;
        Gizmos.DrawLine(
            interceptCenter + new Vector3(-5.0f, 0f, 0f),
            interceptCenter + new Vector3(5.0f, 0f, 0f)
        );
    }
    #endregion
}
