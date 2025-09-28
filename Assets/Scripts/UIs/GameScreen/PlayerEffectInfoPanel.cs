using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PlayerEffectInfoPanel : MonoBehaviour
{
    [Header("バフアイコン画像")]
    [SerializeField]
    private List<StatusEffectIcon> iconImageList;

    [Header("バフバーオブジェクトと画像")]
    [SerializeField]
    private List<StatusEffectBar> buffBarList;

    [System.Serializable]
    public class StatusEffectIcon
    {
        public StatusEffectType type;
        public Image iconImage;
    }

    [System.Serializable]
    public class StatusEffectBar
    {
        public StatusEffectType type;
        public GameObject barObject;
        public Image barFillImage;
    }

    private Dictionary<StatusEffectType, Image> iconImages;
    private Dictionary<StatusEffectType, (GameObject, Image)> buffBars;

    /// <summary>
    /// 現在点滅中のアニメーション（Tween）を管理するための辞書
    /// </summary>
    private Dictionary<StatusEffectType, Tween> blinkingTweens = new();

    private void Awake()
    {
        if (iconImageList == null || iconImageList.Count != 4)
        {
            Debug.LogError("アイコン画像リストが設定されていません");
            return;
        }

        if (buffBarList == null || buffBarList.Count != 4)
        {
            Debug.LogError("バフバーリストが設定されていません");
            return;
        }

        // リストを Dictionary に変換
        iconImages = new();
        foreach (var icon in iconImageList)
        {
            iconImages[icon.type] = icon.iconImage;
        }

        buffBars = new();
        foreach (var bar in buffBarList)
        {
            buffBars[bar.type] = (bar.barObject, bar.barFillImage);
        }
    }

    private void Update()
    {
        if (HealItemPreviewUIManager.instance == null)
        {
            return;
        }
        // 毎フレーム呼び出し（または必要に応じて呼ぶ）
        HealItemPreviewUIManager.instance.DisplayPlayerStatusEffect(
            iconImages,
            buffBars,
            out var expirationFlags
        );

        // バフが切れそうな時にアイコンを点滅させる
        foreach (var pair in expirationFlags)
        {
            StatusEffectType type = pair.Key;
            bool isExpiring = pair.Value;
            Image icon = iconImages[type];

            if (isExpiring)
            {
                // 点滅が必要だが、まだ点滅アニメーションが開始されていない場合
                if (!blinkingTweens.ContainsKey(type))
                {
                    // アイコンのアルファ値を0.3まで下げて、元に戻すアニメーションを無限ループさせる
                    Tween tween = icon.DOFade(0.3f, 0.5f)
                        .SetLoops(-1, LoopType.Yoyo) // Yoyoで行ったり来たりさせる
                        .SetEase(Ease.InOutSine); // 滑らかなイージング

                    // 管理リストに追加
                    blinkingTweens[type] = tween;
                }
            }
            else
            {
                // 点滅が不要だが、現在点滅中の場合
                if (blinkingTweens.TryGetValue(type, out Tween tween))
                {
                    // アニメーションを停止し、色を元に戻す
                    tween.Kill();
                    icon.color = new Color(icon.color.r, icon.color.g, icon.color.b, 1f); // アルファを1に戻す

                    // 管理リストから削除
                    blinkingTweens.Remove(type);
                }
            }
        }
    }

    /// <summary>
    /// このパネルが非表示になる際に、実行中のすべてのアニメーションを停止する
    /// </summary>
    private void OnDisable()
    {
        foreach (var pair in blinkingTweens)
        {
            // アニメーションをキル（停止）
            pair.Value.Kill();
            // 対応するアイコンのアルファ値を元に戻す
            if (iconImages.TryGetValue(pair.Key, out Image icon))
            {
                icon.color = new Color(icon.color.r, icon.color.g, icon.color.b, 1f);
            }
        }
        // 管理リストをクリア
        blinkingTweens.Clear();
    }
}
