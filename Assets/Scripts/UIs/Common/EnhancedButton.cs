using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Buttonクラスを継承して、機能拡張を行う
public class EnhancedButton : Button
{
    [Header("同期するテキスト")]
    [SerializeField, Tooltip("色の輝度(V)を同期させたいTextコンポーネント")]
    private TextMeshProUGUI targetText;

    private Color originalTextColor; // テキストの元の色を保存

    // Awakeの代わりに、基底クラスのAwakeと連携するOnEnableを使用
    protected override void OnEnable()
    {
        base.OnEnable(); // 親クラスのOnEnableを必ず呼び出す

        if (targetText != null)
        {
            // 最初にテキストの元の色を記憶しておく
            originalTextColor = targetText.color;
        }
    }

    /// <summary>
    /// ボタンの状態（通常、選択、押下など）が変化したときに自動で呼び出されるメソッドを上書き（override）する
    /// </summary>
    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        // まず、親クラスの元の処理を呼び出し、ボタン自体の色を変更させる
        base.DoStateTransition(state, instant);

        // targetTextまたはボタンのImage(targetGraphic)がなければ何もしない
        if (targetText == null || targetGraphic == null)
        {
            return;
        }

        Color targetColor;

        // 遷移先の状態(state)に応じて、目標となる色をButtonのcolors設定から直接取得する
        switch (state)
        {
            case SelectionState.Normal:
                targetColor = this.colors.normalColor;
                break;
            case SelectionState.Highlighted:
                targetColor = this.colors.highlightedColor;
                break;
            case SelectionState.Selected:
                targetColor = this.colors.selectedColor;
                break;
            case SelectionState.Pressed:
                targetColor = this.colors.pressedColor;
                break;
            case SelectionState.Disabled:
                targetColor = this.colors.disabledColor;
                break;
            default:
                targetColor = Color.black;
                break;
        }

        // 1. 目標の色から輝度(V)を取得
        Color.RGBToHSV(targetColor, out _, out _, out float buttonValue);

        // 2. テキストの元の色から色相(H)と彩度(S)を取得
        Color.RGBToHSV(originalTextColor, out float textHue, out float textSaturation, out _);

        // 3. テキストのH/SとボタンのVを組み合わせて、新しいテキストの色を作成
        Color newTextColor = Color.HSVToRGB(textHue, textSaturation, buttonValue);

        // 4. テキストの色を更新
        targetText.color = newTextColor;
    }
}
