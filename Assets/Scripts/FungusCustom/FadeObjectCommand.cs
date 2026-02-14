using System.Collections.Generic;
using DG.Tweening;
using Fungus;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[CommandInfo(
    "Sprite",
    "Fade Object Pro",
    "指定したオブジェクト(Sprite/UI)の透明度を徐々に変更します。\n子オブジェクトの一括操作や、透明度0時の非表示化も可能です。"
)]
[AddComponentMenu("")]
public class FadeObjectCommand : Command
{
    [BoxGroup("Target Settings")]
    [Tooltip("フェードさせる対象のGameObject")]
    [SerializeField]
    protected GameObjectData targetGameObject;

    [BoxGroup("Target Settings")]
    [Tooltip("子オブジェクトのRenderer/UIも含めてフェードするか")]
    [SerializeField]
    protected bool applyRecursively = true;

    [BoxGroup("Fade Settings")]
    [Tooltip("目標とする透明度 (0.0 ～ 1.0)")]
    [SerializeField]
    protected FloatData targetAlpha = new FloatData(0f);

    [BoxGroup("Fade Settings")]
    [Tooltip("変化にかける時間(秒)")]
    [SerializeField]
    protected FloatData duration = new FloatData(1.0f);

    [BoxGroup("Fade Settings")]
    [Tooltip("透明度が0になったとき、自動的にSetActive(false)にするか")]
    [SerializeField]
    protected bool deactivateOnZeroAlpha = true;

    [BoxGroup("Tween Settings")]
    [Tooltip("待機するかどうか")]
    [SerializeField]
    protected bool waitUntilFinished = true;

    [BoxGroup("Tween Settings")]
    [Tooltip("変化のカーブ (Linear=一定, OutQuad=ふんわり停止)")]
    [SerializeField]
    protected Ease easeType = Ease.Linear;

    public override void OnEnter()
    {
        if (targetGameObject.Value == null)
        {
            Continue();
            return;
        }

        GameObject target = targetGameObject.Value;
        float endAlpha = targetAlpha.Value;
        float time = duration.Value;

        // もし目標Alphaが0より大きいなら、フェード前に表示状態にする
        if (endAlpha > 0f && !target.activeSelf)
        {
            target.SetActive(true);
        }

        // フェード対象のコンポーネントを収集
        List<Component> fadeTargets = new List<Component>();

        // CanvasGroupがある場合は、それを優先するとパフォーマンスが良い（UIの場合）
        CanvasGroup cg = target.GetComponent<CanvasGroup>();
        if (cg != null && !applyRecursively)
        {
            fadeTargets.Add(cg);
        }
        else
        {
            // 個別のコンポーネントを探す
            if (applyRecursively)
            {
                fadeTargets.AddRange(target.GetComponentsInChildren<SpriteRenderer>());
                fadeTargets.AddRange(target.GetComponentsInChildren<Image>());
                fadeTargets.AddRange(target.GetComponentsInChildren<Text>());
                fadeTargets.AddRange(target.GetComponentsInChildren<CanvasGroup>());
                // TextMeshProがある場合
                fadeTargets.AddRange(target.GetComponentsInChildren<TMP_Text>());
            }
            else
            {
                if (target.TryGetComponent(out SpriteRenderer sr))
                    fadeTargets.Add(sr);
                if (target.TryGetComponent(out Image img))
                    fadeTargets.Add(img);
                if (target.TryGetComponent(out Text txt))
                    fadeTargets.Add(txt);
                if (target.TryGetComponent(out CanvasGroup grp))
                    fadeTargets.Add(grp);
                if (target.TryGetComponent(out TMP_Text tmp))
                    fadeTargets.Add(tmp);
            }
        }

        // 何も操作対象がなければ終了
        if (fadeTargets.Count == 0)
        {
            Continue();
            return;
        }

        // DOTweenのSequenceを作成して一括管理
        Sequence seq = DOTween.Sequence();

        foreach (var component in fadeTargets)
        {
            // 各コンポーネントに応じたTweenを追加
            if (component is SpriteRenderer sr)
                seq.Join(sr.DOFade(endAlpha, time));
            else if (component is Image img)
                seq.Join(img.DOFade(endAlpha, time));
            else if (component is Text txt)
                seq.Join(txt.DOFade(endAlpha, time));
            else if (component is CanvasGroup group)
                seq.Join(group.DOFade(endAlpha, time));
            else if (component is TMP_Text tmp)
                seq.Join(tmp.DOFade(endAlpha, time));
        }

        // イージング設定
        seq.SetEase(easeType);

        // 完了時の処理
        seq.OnComplete(() =>
        {
            // 目標が透明度0、かつ設定がONなら非表示にする
            if (Mathf.Approximately(endAlpha, 0f) && deactivateOnZeroAlpha)
            {
                target.SetActive(false);
            }

            // 待機設定がONならここで次のコマンドへ
            if (waitUntilFinished)
            {
                Continue();
            }
        });

        // 待機しない設定なら、Tween開始と同時に次へ
        if (!waitUntilFinished)
        {
            Continue();
        }
    }

    public override void OnStopExecuting()
    {
        // コマンド停止時はTweenも安全にキルする
        if (targetGameObject.Value != null)
        {
            targetGameObject.Value.transform.DOKill(true);
        }
        base.OnStopExecuting();
    }

    public override string GetSummary()
    {
        if (targetGameObject.Value == null)
            return "Error: No Target GameObject";
        return $"{targetGameObject.Value.name} -> Alpha: {targetAlpha.Value} ({duration.Value}s)";
    }

    public override Color GetButtonColor()
    {
        return new Color32(200, 200, 255, 255); // 薄い青紫
    }
}
