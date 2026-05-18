using System.Collections;
using UnityEngine;

/// <summary>
/// ブレス攻撃のエフェクト（FreezeMist）の挙動を制御するスクリプト。
/// オブジェクトプールからの生成時にサイズや放出量を動的に変更し、使用後に初期化して返却します。
/// </summary>
public class FreezeMistController : PoolableObject
{
    #region 内部データ・構造体

    /// <summary>
    /// パーティクルシステムの初期値を保存するための構造体
    /// </summary>
    private struct PSInitialParams
    {
        public Vector2 mainStartSize;
        public float mainEmission;
        public Vector2 child1StartSize;
        public float child1Radial;
        public float child2Emission;
        public float child2Radius;
    }

    #endregion

    #region プライベート変数
    private float _sizeMultiplierMin = 1.75f; // パーティクルサイズのランダム倍率の最小値
    private float _sizeMultiplierMax = 2.25f; // パーティクルサイズのランダム倍率の最大値

    // コンポーネントキャッシュ
    private ParticleSystem _mainPS;
    private ParticleSystem _childPS1;
    private ParticleSystem _childPS2;

    // 状態管理
    private PSInitialParams _initialParams;
    private bool _isInitialized = false;

    #endregion

    #region Unity ライフサイクル

    private void Awake()
    {
        _mainPS = GetComponent<ParticleSystem>();

        // 子オブジェクトのパーティクルシステムを取得
        if (transform.childCount >= 2)
        {
            _childPS1 = transform.GetChild(0).GetComponent<ParticleSystem>();
            _childPS2 = transform.GetChild(1).GetComponent<ParticleSystem>();
        }

        CaptureInitialParams();
    }

    #endregion

    #region 初期化処理

    /// <summary>
    /// ブレスの移動方向、生存時間、速度、ブレ幅を指定して挙動を開始します。
    /// 呼び出し元（ゴーレムなど）から生成時に実行されます。
    /// </summary>
    /// <param name="direction">基本となる発射方向</param>
    /// <param name="duration">生存時間（秒）</param>
    /// <param name="moveSpeed">移動速度</param>
    /// <param name="yVariance">Y軸方向のランダムなブレ幅</param>
    public void Initialize(Vector2 direction, float duration, float moveSpeed, float yVariance)
    {
        if (!_isInitialized)
        {
            CaptureInitialParams();
        }

        // 1.0～1.75倍の範囲でサイズや放出量をランダムに決定
        float multiplier = Random.Range(_sizeMultiplierMin, _sizeMultiplierMax);
        ApplyParameters(multiplier);

        // 移動と制御のコルーチンを開始
        StartCoroutine(MistRoutine(direction, duration, moveSpeed, yVariance));
    }

    /// <summary>
    /// インスペクターで設定されているパーティクルの初期値を記憶します。
    /// サイズを動的に変更した後、プールへ返却する際に元に戻すため必要になります。
    /// </summary>
    private void CaptureInitialParams()
    {
        if (_mainPS == null || _childPS1 == null || _childPS2 == null)
            return;

        _initialParams = new PSInitialParams
        {
            mainStartSize = new Vector2(
                _mainPS.main.startSizeX.constant,
                _mainPS.main.startSizeY.constant
            ),
            mainEmission = _mainPS.emission.rateOverTime.constant,
            child1StartSize = new Vector2(
                _childPS1.main.startSize.constantMin,
                _childPS1.main.startSize.constantMax
            ),
            child1Radial = _childPS1.velocityOverLifetime.radial.constant,
            child2Emission = _childPS2.emission.rateOverTime.constant,
            child2Radius = _childPS2.shape.radius,
        };

        _isInitialized = true;
    }

    #endregion

    #region パーティクル制御・移動ロジック

    /// <summary>
    /// 記憶した初期値に対して、指定された倍率（乗数）を適用します。
    /// </summary>
    /// <param name="mult">適用するサイズの倍率</param>
    private void ApplyParameters(float mult)
    {
        // 主オブジェクトのパラメータ適用
        var mainMod = _mainPS.main;
        mainMod.startSizeX = _initialParams.mainStartSize.x * mult;
        mainMod.startSizeY = _initialParams.mainStartSize.y * mult;
        var mainEmit = _mainPS.emission;
        mainEmit.rateOverTime = _initialParams.mainEmission * mult;

        // 第一子オブジェクトのパラメータ適用
        var c1Mod = _childPS1.main;
        c1Mod.startSize = new ParticleSystem.MinMaxCurve(
            _initialParams.child1StartSize.x * mult,
            _initialParams.child1StartSize.y * mult
        );
        var c1Vel = _childPS1.velocityOverLifetime;
        c1Vel.radial = _initialParams.child1Radial * mult;

        // 第二子オブジェクトのパラメータ適用
        var c2Emit = _childPS2.emission;
        c2Emit.rateOverTime = _initialParams.child2Emission * mult;
        var c2Shape = _childPS2.shape;
        c2Shape.radius = _initialParams.child2Radius * mult;
    }

    /// <summary>
    /// ブレスの実際の移動と、時間経過によるフェードアウトを制御するコルーチンです。
    /// </summary>
    private IEnumerator MistRoutine(
        Vector2 direction,
        float duration,
        float moveSpeed,
        float yVariance
    )
    {
        float timer = 0;

        // 受け取った yVariance を使用してランダムなブレを加える
        float randomY = Random.Range(-yVariance, yVariance);
        Vector3 moveDir = new Vector3(direction.x, direction.y + randomY, 0).normalized;

        _mainPS.Play();

        while (timer < duration)
        {
            // 指定された速度で進行方向へ移動
            transform.position += moveDir * moveSpeed * Time.deltaTime;
            timer += Time.deltaTime;

            // 生存時間の終了1秒前になったら、パーティクルの放出を停止して徐々に消えさせる
            if (timer > duration - 1.0f)
            {
                if (_mainPS.isPlaying)
                {
                    _mainPS.Stop();
                }
            }
            yield return null;
        }

        ResetAndReturn();
    }

    #endregion

    #region 終了・返却処理

    /// <summary>
    /// パーティクルのサイズを初期値に戻し、オブジェクトプールに返却します。
    /// </summary>
    private void ResetAndReturn()
    {
        ApplyParameters(1.0f); // 倍率1.0でサイズを元に戻す
        ReturnToPool(); // PoolableObjectの継承メソッドを呼び出して返却
    }

    #endregion
}
