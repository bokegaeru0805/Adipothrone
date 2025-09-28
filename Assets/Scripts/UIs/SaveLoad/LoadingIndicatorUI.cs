using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SaveLoadManagerのロード状態を監視し、「Now Loading」UIの表示とアニメーションを制御します。
/// </summary>
public class LoadingIndicatorUI : MonoBehaviour
{
    private RectTransform rectTransform;
    private Tween loadingTween;

    // ★ 表示を制御するImageコンポーネントへの参照
    private Image loadingImage;

    private void Awake()
    {
        // 自身のコンポーネントをキャッシュ
        rectTransform = GetComponent<RectTransform>();
        loadingImage = GetComponent<Image>(); //Imageコンポーネントを取得

        if (loadingImage == null)
        {
            Debug.LogError("Imageコンポーネントが見つかりません。", this);
            enabled = false; // このスクリプト自体を無効化
            return;
        }

        //GameObjectではなく、Imageコンポーネントを無効にして非表示にする
        loadingImage.enabled = false;
    }

    private void OnEnable()
    {
        // SaveLoadManagerのロード状態変化イベントを購読
        // (AwakeでGameObjectが無効にされないため、このメソッドは正しく呼ばれる)
        SaveLoadManager.OnLoadingStateChanged += HandleLoadingStateChanged;
    }

    private void OnDisable()
    {
        // オブジェクトが無効になる際に、必ずイベントの購読を解除
        SaveLoadManager.OnLoadingStateChanged -= HandleLoadingStateChanged;

        // 念のためアニメーションも停止
        StopAnimation();
    }

    /// <summary>
    /// ロード状態が変化したときに呼び出されるメソッド
    /// </summary>
    private void HandleLoadingStateChanged(bool isLoading)
    {
        if (isLoading)
        {
            // ロード中なら、Imageを表示してアニメーションを開始
            loadingImage.enabled = true; // 表示をImage.enabledで制御
            StartAnimation();
        }
        else
        {
            // ロード中でないなら、アニメーションを停止してImageを非表示
            StopAnimation();
            loadingImage.enabled = false; // 非表示をImage.enabledで制御
        }
    }

    /// <summary>
    /// 拡大縮小アニメーションを開始します。
    /// </summary>
    private void StartAnimation()
    {
        loadingTween?.Kill();

        loadingTween = rectTransform
            .DOScale(1.5f, 0.2f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    /// <summary>
    /// アニメーションを停止し、スケールを元に戻します。
    /// </summary>
    private void StopAnimation()
    {
        loadingTween?.Kill();
        rectTransform.localScale = Vector3.one;
    }
}
