using System.Linq;
using DG.Tweening;
using Fungus;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// キャラクター「Fill」専用の立ち絵コントローラー。
/// BasePortraitControllerを継承し、前髪・後ろ髪の管理と、体形状態に応じた表情の分岐処理を行います。
/// </summary>
public class FillPortraitController : BasePortraitController
{
    [Header("Fill Specific UI References")]
    [Tooltip("Fillの前髪を表示するImage")]
    public Image frontHairImage;

    [Tooltip("Fillの後ろ髪を表示するImage")]
    public Image backHairImage;

    // 前髪・後ろ髪は1種類で固定のため、検索用のスプライト名を定数化
    private const string FrontHairSpriteName = "Fill_frontHair";
    private const string BackHairSpriteName = "Fill_backHair";

    /// <summary>
    /// Fungusからのリクエストを受信し、Fill宛ての命令であればスプライト名を組み立てて表示します。
    /// </summary>
    public override void HandleShowRequest(string portraitString)
    {
        if (currentBlockType != BlockType.Story)
        {
            return;
        }

        // 文字列を '_' で分割。想定フォーマット: "Fill_状態_表情" (例: Fill_normal_Smile)
        string[] parts = portraitString.Split('_');

        // 配列の要素数が足りているか、そして自分(Fill)宛てのリクエストかを確認
        if (parts.Length >= 3 && parts[0] == character.name)
        {
            string stateString = parts[1]; // "normal", "armed1", "armed2" のいずれか
            string expressionString = parts.LastOrDefault(); // "Smile", "Angry" など

            // 胴体と顔のスプライト名を組み立てる
            string bodySpriteName = $"{character.name}_{stateString}_body";
            string faceSpriteName = $"{character.name}_{stateString}_face";

            // 状態に応じて使用する表情のタイプ（normal か obese）を決定する
            string exprType = (stateString == "armed2") ? "obese" : "normal";

            // 表情のスプライト名を組み立てる
            string expressionSpriteName =
                $"{character.name}_expression_{exprType}_{expressionString}";

            // 基底クラスの表示メソッドを呼び出す
            ShowPortrait(bodySpriteName, faceSpriteName, expressionSpriteName);
        }
    }

    /// <summary>
    /// 各Imageコンポーネントにスプライトを割り当てるメソッドのオーバーライド。
    /// Fill特有の前髪と後ろ髪の設定を追加します。
    /// </summary>
    protected override void SetAllSprites(
        string bodySpriteName,
        string faceSpriteName,
        string expressionSpriteName
    )
    {
        // 基底クラスの処理（胴体、顔、表情の設定）を実行
        base.SetAllSprites(bodySpriteName, faceSpriteName, expressionSpriteName);

        // --- 前髪の設定 ---
        if (_portraitDictionary.TryGetValue(FrontHairSpriteName, out Sprite frontHairSprite))
        {
            frontHairImage.sprite = frontHairSprite;
            frontHairImage.enabled = true;
        }
        else
        {
            Debug.LogError($"前髪スプライトが見つかりません: {FrontHairSpriteName}");
            frontHairImage.enabled = false;
        }

        // --- 後ろ髪の設定 ---
        if (_portraitDictionary.TryGetValue(BackHairSpriteName, out Sprite backHairSprite))
        {
            backHairImage.sprite = backHairSprite;
            backHairImage.enabled = true;
        }
        else
        {
            Debug.LogError($"後ろ髪スプライトが見つかりません: {BackHairSpriteName}");
            backHairImage.enabled = false;
        }
    }

    /// <summary>
    /// 明暗が切り替わる際のTween処理のオーバーライド。前髪と後ろ髪にも適用します。
    /// </summary>
    public override void SetPortraitColorTween(Color targetColor, float duration)
    {
        base.SetPortraitColorTween(targetColor, duration);

        if (frontHairImage != null)
        {
            frontHairImage.DOColor(targetColor, duration).SetUpdate(true);
        }
        if (backHairImage != null)
        {
            backHairImage.DOColor(targetColor, duration).SetUpdate(true);
        }
    }

    /// <summary>
    /// 立ち絵を即座に非表示にするメソッドのオーバーライド。前髪と後ろ髪も非表示にします。
    /// </summary>
    public override void HidePortrait()
    {
        base.HidePortrait();

        if (frontHairImage != null)
        {
            frontHairImage.enabled = false;
        }
        if (backHairImage != null)
        {
            backHairImage.enabled = false;
        }
    }
}
