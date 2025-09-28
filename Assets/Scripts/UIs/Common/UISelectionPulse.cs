using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// EventSystemでUIが選択された際に、対象のグラフィックを点滅させて
/// 選択中であることを分かりやすく表示するコンポーネント。
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UISelectionPulse : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [Header("設定")]
    [Tooltip("trueの場合、このコンポーネントがアタッチされたオブジェクトのImageを点滅させます。")]
    [SerializeField] private bool pulseSelf = true;

    [Tooltip("自分自身と同時に点滅させたい、他のImageコンポーネントのリスト。")]
    [SerializeField] private List<Image> additionalGraphicsToPulse = new List<Image>();

    [Header("アニメーション設定")]
    [Tooltip("点滅のアニメーションが片道にかかる時間（秒）")]
    [SerializeField] private float pulseDuration = 0.5f;

    [Tooltip("明るさの最大値（元の明るさに対する倍率）")]
    [SerializeField] private float pulseBrightness = 1.2f;

    private Image myGraphic; // 自分自身のImageコンポーネント

    // 実行中のTweenアニメーションを管理するための変数
    private Tween _selfPulseTween;
    private List<Tween> _additionalTweens = new List<Tween>();

    // 点滅開始前の元の色を保存しておくための変数
    private Color _originalColor;
    private Dictionary<Image, Color> _originalAdditionalColors = new Dictionary<Image, Color>();

    private void Awake()
    {
        if (pulseSelf)
        {
            if (myGraphic == null)
            {
                myGraphic = GetComponent<Image>();
            }

            if (myGraphic == null)
            {
                Debug.LogError("点滅対象のImageコンポーネントが見つかりません。", this);
                pulseSelf = false; // 自分自身は点滅させないように設定
            }
            else
            {
                _originalColor = myGraphic.color;
            }
        }

        // 同時に点滅させるUIの元の色を保存
        foreach (var graphic in additionalGraphicsToPulse)
        {
            if (graphic != null && !_originalAdditionalColors.ContainsKey(graphic))
            {
                _originalAdditionalColors.Add(graphic, graphic.color);
            }
        }
    }

    public void OnSelect(BaseEventData eventData)
    {

        // --- 自分自身の点滅を開始 ---
        if (pulseSelf && myGraphic != null)
        {
            _selfPulseTween?.Kill();
            // StartPulseTweenメソッドを呼び出す前に、まず目標の色を計算して即座に適用
            Color targetColor = GetTargetColor(_originalColor);
            myGraphic.color = targetColor;
            // その後、元の色との間でTweenを開始
            _selfPulseTween = StartPulseTween(myGraphic, _originalColor, targetColor);
        }

        // --- 同時に点滅させるUIのアニメーションを開始 ---
        _additionalTweens.ForEach(tween => tween?.Kill());
        _additionalTweens.Clear();
        foreach (var graphic in additionalGraphicsToPulse)
        {
            if (graphic != null)
            {
                // Dictionaryから元の色を取得
                Color originalColor = _originalAdditionalColors[graphic];
                // こちらも同様に、まず目標の色を計算して即座に適用
                Color targetColor = GetTargetColor(originalColor);
                graphic.color = targetColor;
                // その後、元の色との間でTweenを開始
                _additionalTweens.Add(StartPulseTween(graphic, originalColor, targetColor));
            }
        }

    }

    public void OnDeselect(BaseEventData eventData)
    {

        // --- 自分自身の点滅を停止 ---
        if (pulseSelf && myGraphic != null)
        {
            _selfPulseTween?.Kill();
            myGraphic.color = _originalColor;
        }

        // 同時に点滅させていたUIのアニメーションを停止 ---
        _additionalTweens.ForEach(tween => tween?.Kill());
        _additionalTweens.Clear();
        foreach (var graphic in additionalGraphicsToPulse)
        {
            if (graphic != null)
            {
                graphic.color = _originalAdditionalColors[graphic];
            }
        }
    }

    /// <summary>
    /// 指定されたImageに対して点滅Tweenを開始し、そのTweenを返すヘルパーメソッド
    /// </summary>
    private Tween StartPulseTween(Image graphic, Color originalColor)
    {
        // 1. 元の色をRGBからHSV（色相, 彩度, 明度）に変換
        Color.RGBToHSV(originalColor, out float h, out float s, out float v);

        // 2. 明度(V)を pulseBrightness でスケールし、0～1の範囲に収める
        float targetV = Mathf.Clamp01(v * pulseBrightness);

        // 3. 変更したHSVを、再びRGBの色に戻す
        Color targetColor = Color.HSVToRGB(h, s, targetV);

        // 4. 元のアルファ値（透明度）を維持する
        targetColor.a = originalColor.a;

        return graphic.DOColor(targetColor, pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    /// <summary>
    /// 元の色から、指定された明るさ倍率の目標色を計算して返します。
    /// </summary>
    private Color GetTargetColor(Color originalColor)
    {
        // 1. 元の色をRGBからHSV（色相, 彩度, 明度）に変換
        Color.RGBToHSV(originalColor, out float h, out float s, out float v);

        // 2. 明度(V)を pulseBrightness でスケールし、0～1の範囲に収める
        float targetV = Mathf.Clamp01(v * pulseBrightness);

        // 3. 変更したHSVを、再びRGBの色に戻す
        Color targetColor = Color.HSVToRGB(h, s, targetV);

        // 4. 元のアルファ値（透明度）を維持する
        targetColor.a = originalColor.a;

        return targetColor;
    }

    /// <summary>
    /// 指定されたImageに対して点滅Tweenを開始し、そのTweenを返すヘルパーメソッド
    /// </summary>
    private Tween StartPulseTween(Image graphic, Color originalColor, Color targetColor)
    {
        // DOColorの引数を変更し、「元の色」へ戻るアニメーションにする
        // OnSelectで既に目標の色になっているため、ここからは元の色に戻って、また目標の色へ、という往復運動になる
        return graphic.DOColor(originalColor, pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void OnDestroy()
    {
        // 実行中のTweenを安全に停止
        _selfPulseTween?.Kill();
        _additionalTweens.ForEach(tween => tween?.Kill());
    }
}