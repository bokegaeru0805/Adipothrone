using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FastTravelSelectButton
    : MonoBehaviour,
        IItemAssignable,
        ISelectHandler,
        IDeselectHandler
{
    [Header("アニメーション対象")]
    [SerializeField]
    private RectTransform backgroundToAnimate;

    [Header("ファストトラベルの名前のTextコンポーネント")]
    [SerializeField]
    private TextMeshProUGUI fastTravelNameText; //ファストトラベルの名前を表示するTextコンポーネント

    [Header("選択/非選択時のスプライト")]
    [SerializeField]
    private Sprite selectedSprite; // 選択時に適用するスプライト

    [SerializeField]
    private Sprite deselectedSprite; // 非選択時に適用するスプライト

    [HideInInspector]
    public Enum AssignedItemID => assignedItemID; //選択されているアイテムのID
    private Enum assignedItemID; // 実際のEnum型
    private Tween selectionTween;
    private Image backgroundImage; // スプライトを適用するImageコンポーネント

    public void AssignItem(Enum itemID)
    {
        assignedItemID = itemID;
    }

    public void UpdateFastTravelName(string fastTravelName)
    {
        if (fastTravelNameText != null)
        {
            fastTravelNameText.text = fastTravelName;
        }
        else
        {
            Debug.LogError("ファストトラベルの名前のTextコンポーネントが設定されていません");
        }
    }

    private void Awake()
    {
        if (backgroundToAnimate == null)
        {
            Debug.LogError("背景のRectTransformが正しく設定されていません。");
            return;
        }

        // 背景オブジェクトからImageコンポーネントを取得
        backgroundImage = backgroundToAnimate.GetComponent<Image>();
        if (backgroundImage == null)
        {
            Debug.LogError(
                "アニメーション対象のオブジェクトにImageコンポーネントが見つかりません。",
                this
            );
            return;
        }

        if (fastTravelNameText == null)
        {
            Debug.LogError("ファストトラベルの名前のTextコンポーネントが設定されていません");
            return;
        }
        GetComponent<Button>().onClick.AddListener(SelectFastTravel);

        // 初期状態として非選択スプライトを設定
        backgroundImage.sprite = deselectedSprite;
    }

    private void SelectFastTravel()
    {
        //自分の親オブジェクトのFastPanelActiveコンポーネントを取得
        FastTravelPanelActive fastTravelPanel = GetComponentInParent<FastTravelPanelActive>();
        if (fastTravelPanel == null)
        {
            Debug.LogError("FastTravelPanelActiveコンポーネントが見つかりません");
            return;
        }
        fastTravelPanel.RequestFastTravel(assignedItemID);
    }

    // オブジェクトが非アクティブになった時にアニメーションを確実に停止させる
    private void OnDisable()
    {
        assignedItemID = null; //weaponIDを初期化する

        // 念のため、Tweenをキルしてスケールを元に戻す
        selectionTween?.Kill();
        if (backgroundToAnimate != null)
        {
            backgroundToAnimate.localScale = Vector3.one;
        }

        // 非アクティブになったら非選択スプライトに戻す
        if (backgroundImage != null)
        {
            backgroundImage.sprite = deselectedSprite;
        }
    }

    // このボタンが選択された時に呼び出されるメソッド
    public void OnSelect(BaseEventData eventData)
    {
        // 選択時のスプライトを適用
        if (backgroundImage != null)
        {
            backgroundImage.sprite = selectedSprite;
        }

        // 既存のアニメーションがあれば停止
        selectionTween?.Kill();
        // 拡大・縮小を繰り返すアニメーションを開始
        if (backgroundToAnimate != null)
        {
            selectionTween = backgroundToAnimate
                .DOScale(1.05f, 0.8f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }
    }

    // このボタンの選択が外れた時に呼び出されるメソッド
    public void OnDeselect(BaseEventData eventData)
    {
        // 非選択時のスプライトを適用
        if (backgroundImage != null)
        {
            backgroundImage.sprite = deselectedSprite;
        }

        // 実行中のアニメーションを停止
        selectionTween?.Kill();
        // スケールを元のサイズに滑らかに戻す
        if (backgroundToAnimate != null)
        {
            selectionTween = backgroundToAnimate.DOScale(1f, 0.1f).SetUpdate(true);
        }
    }
}
