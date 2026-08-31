using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// キャラクター「Apothecary」専用の立ち絵コントローラー。
/// BasePortraitControllerを継承し、前髪・後ろ髪を含む立ち絵を管理します。
/// </summary>
public class ApothecaryPortraitController : BasePortraitController
{
    [Header("Apothecary Specific UI References")]
    [Tooltip("Apothecaryの前髪を表示するImage")]
    public Image frontHairImage;

    [Tooltip("Apothecaryの後ろ髪を表示するImage")]
    public Image backHairImage;

    private const string FrontHairSpriteName = "Apothecary_fronthair";
    private const string BackHairSpriteName = "Apothecary_backhair";

    /// <summary>
    /// Fungusからのリクエストを受信し、Apothecary宛てであれば立ち絵を表示します。
    /// </summary>
    public override void HandleShowRequest(string portraitString)
    {
        string[] parts = portraitString.Split('_');

        // 想定フォーマット: Apothecary_状態_表情
        if (parts.Length >= 3 && parts[0] == character.name)
        {
            string stateString = parts[1];
            string expressionString = parts.LastOrDefault();

            string bodySpriteName = $"{character.name}_{stateString}_body";
            string faceSpriteName = $"{character.name}_{stateString}_face";
            string expressionSpriteName =
                $"{character.name}_{stateString}_{expressionString}_expression";

            ShowPortrait(bodySpriteName, faceSpriteName, expressionSpriteName);
        }
    }

    /// <summary>
    /// 基本パーツに加えて、Apothecaryの前髪と後ろ髪を設定します。
    /// </summary>
    protected override void SetAllSprites(
        string bodySpriteName,
        string faceSpriteName,
        string expressionSpriteName
    )
    {
        base.SetAllSprites(bodySpriteName, faceSpriteName, expressionSpriteName);

        SetHairSprite(frontHairImage, FrontHairSpriteName, "前髪");
        SetHairSprite(backHairImage, BackHairSpriteName, "後ろ髪");
    }

    /// <summary>
    /// 明暗の切り替えを前髪と後ろ髪にも適用します。
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
    /// 基本パーツに加えて、前髪と後ろ髪も非表示にします。
    /// </summary>
    public override void HidePortrait()
    {
        base.HidePortrait();
        HideHairImages();
    }

    /// <summary>
    /// 基本状態へ戻し、前髪と後ろ髪も非表示にします。
    /// </summary>
    public override void ResetToInitialState()
    {
        base.ResetToInitialState();
        HideHairImages();
    }

    private void SetHairSprite(Image hairImage, string spriteName, string partName)
    {
        if (hairImage == null)
        {
            Debug.LogError($"{partName}用のImageが設定されていません。", this);
            return;
        }

        if (_portraitDictionary.TryGetValue(spriteName, out Sprite hairSprite))
        {
            hairImage.sprite = hairSprite;
            hairImage.enabled = true;
        }
        else
        {
            Debug.LogError($"{partName}スプライトが見つかりません: {spriteName}", this);
            hairImage.enabled = false;
        }
    }

    private void HideHairImages()
    {
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
