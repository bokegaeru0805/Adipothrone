using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 様々な波形を用いて「揺らぎ（Flicker）」の数値を生成し、外部コンポーネントに渡す汎用クラス。
/// 光の強さ、オブジェクトの透明度、サイズ、位置など、数値を変化させたいあらゆる要素に適用できます。
/// </summary>
public class FlickerGenerator : MonoBehaviour
{
    #region 列挙型 (Enum)

    /// <summary>
    /// 揺らぎの波形（アルゴリズム）の種類
    /// </summary>
    public enum WaveformType
    {
        PerlinNoise, // 炎や風のような滑らかで自然なランダム揺らぎ
        SineWave, // 魔法の石や機械のような規則的な脈動
        RandomBlink // 壊れかけの蛍光灯のような不規則な点滅（ステップ）
        ,
    }

    /// <summary>
    /// ベース値に対する揺らぎの適用方法
    /// </summary>
    public enum ApplyMode
    {
        Add, // ベース値に揺らぎを「加算」する（例：基準位置 ± 1m）
        Multiply // ベース値に揺らぎを「乗算」する（例：基準スケール × 0.8〜1.2倍）
        ,
    }

    #endregion

    #region インスペクター設定

    [Header("基本設定")]
    [Tooltip("揺らぎの基準となるベース値")]
    public float baseValue = 1.0f;

    [Tooltip("揺らぎの速さ（大きいほど速く変化する）")]
    public float speed = 5.0f;

    [Tooltip("揺らぎの振れ幅（影響力の強さ）")]
    public float amplitude = 0.2f;

    [Header("詳細設定")]
    [Tooltip("揺らぎのアルゴリズム（自然な揺れか、規則的な脈動かなど）")]
    public WaveformType waveformType = WaveformType.PerlinNoise;

    [Tooltip("ベース値に対して揺らぎを「足す」か「掛ける」か")]
    public ApplyMode applyMode = ApplyMode.Add;

    [Header("出力イベント")]
    [Tooltip(
        "計算された最終的な数値を外部のコンポーネントに渡すためのイベント。インスペクターの「+」ボタンからLightのIntensityなどを登録してください。"
    )]
    public UnityEvent<float> OnValueChanged;

    #endregion

    #region 内部変数

    private float noiseOffset; // 複数のオブジェクトで動きが被らないようにするためのランダムな開始位置
    private float currentBlinkTarget; // RandomBlink用の現在のターゲット値
    private float blinkTimer; // RandomBlink用の更新タイマー
    #endregion

    #region Unityライフサイクル

    private void Start()
    {
        // オブジェクトごとに揺らぎのタイミングをずらすためのランダム値
        noiseOffset = Random.Range(0f, 1000f);

        // RandomBlink用の初期化
        currentBlinkTarget = GetRandomNormalizedValue();
    }

    private void Update()
    {
        // 1. 波形アルゴリズムに基づいて、-1.0 〜 1.0 の範囲の「生ノイズ値」を取得
        float rawNoise = GetRawNoiseValue();

        // 2. 振れ幅を掛け合わせる
        float fluctuation = rawNoise * amplitude;

        // 3. 指定された計算モードでベース値に適用する
        float finalValue = CalculateFinalValue(fluctuation);

        // 4. 計算結果を UnityEvent 経由で外部コンポーネントに送る
        if (OnValueChanged != null)
        {
            OnValueChanged.Invoke(finalValue);
        }
    }

    #endregion

    #region 計算ロジック

    /// <summary>
    /// 選択された波形タイプに基づいて、-1.0 から 1.0 の範囲のノイズ値を計算します。
    /// </summary>
    private float GetRawNoiseValue()
    {
        float time = Time.time * speed + noiseOffset;

        switch (waveformType)
        {
            case WaveformType.PerlinNoise:
                // PerlinNoiseは 0.0〜1.0 を返すため、-1.0〜1.0 に変換する
                float perlin = Mathf.PerlinNoise(time, 0f);
                return (perlin - 0.5f) * 2f;

            case WaveformType.SineWave:
                // Sinは標準で -1.0〜1.0 を返す
                return Mathf.Sin(time);

            case WaveformType.RandomBlink:
                // 一定時間ごとにランダムな値を切り替える（カクカクした動き）
                blinkTimer += Time.deltaTime * speed;
                // タイマーが1を超えたら新しいターゲット値を設定
                if (blinkTimer >= 1f)
                {
                    currentBlinkTarget = GetRandomNormalizedValue();
                    blinkTimer = 0f;
                }
                return currentBlinkTarget;

            default:
                return 0f;
        }
    }

    /// <summary>
    /// 生の揺らぎ値をベース値に適用して、最終的な数値を計算します。
    /// </summary>
    private float CalculateFinalValue(float fluctuation)
    {
        switch (applyMode)
        {
            case ApplyMode.Add:
                // 加算：ベース値 + 揺らぎ （例： 10 + 2 = 12）
                return baseValue + fluctuation;

            case ApplyMode.Multiply:
                // 乗算：ベース値 * (1 + 揺らぎ) （例： 10 * (1 + 0.2) = 12）
                return baseValue * (1f + fluctuation);

            default:
                return baseValue;
        }
    }

    /// <summary>
    /// -1.0 から 1.0 の範囲のランダムな値を取得します。
    /// </summary>
    private float GetRandomNormalizedValue()
    {
        return Random.Range(-1f, 1f);
    }

    #endregion
}
