using Fungus;
using NaughtyAttributes;
using UnityEngine;

public abstract class EventCGCommandBase : Command
{
    [BoxGroup("Event CG")]
    [Tooltip("表示する一枚絵")]
    [SerializeField]
    protected Sprite eventCG;

    [BoxGroup("Layout")]
    [Tooltip("一枚絵の表示サイズ")]
    [SerializeField]
    protected EventCGSizeMode sizeMode = EventCGSizeMode.FitScreen;

    [BoxGroup("Layout")]
    [ShowIf("UsesCustomSize")]
    [Tooltip("Canvas上での幅と高さ")]
    [SerializeField]
    [AllowNesting]
    protected Vector2 customSize = new Vector2(1280f, 720f);

    [BoxGroup("Layout")]
    [ShowIf("UsesNativeScale")]
    [MinValue(0.01f)]
    [Tooltip("画像の元サイズに掛ける倍率")]
    [SerializeField]
    [AllowNesting]
    protected float nativeScale = 1f;

    [BoxGroup("Layout")]
    [Tooltip("画面中央を基準にした表示位置のオフセット")]
    [SerializeField]
    protected Vector2 positionOffset = Vector2.zero;

    protected EventCGDisplayOptions GetDisplayOptions()
    {
        return new EventCGDisplayOptions
        {
            SizeMode = sizeMode,
            CustomSize = customSize,
            NativeScale = nativeScale,
            PositionOffset = positionOffset,
        };
    }

    protected bool UsesCustomSize()
    {
        return sizeMode == EventCGSizeMode.CustomSize;
    }

    protected bool UsesNativeScale()
    {
        return sizeMode == EventCGSizeMode.NativeScale;
    }

    protected string GetEventCGSummary(string prefix)
    {
        if (eventCG == null)
        {
            return "Error: EventCG未設定";
        }

        string sizeSummary =
            sizeMode == EventCGSizeMode.NativeScale ? $"×{nativeScale:0.##}" : sizeMode.ToString();
        return $"{prefix}: {eventCG.name} [{sizeSummary}]";
    }

    public override Color GetButtonColor()
    {
        return new Color32(120, 145, 225, 255);
    }
}
