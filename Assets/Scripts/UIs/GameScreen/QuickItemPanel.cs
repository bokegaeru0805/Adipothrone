using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuickItemPanel : MonoBehaviour
{
    private PlayerManager playerManager; // PlayerManagerの参照

    // QuickSlotUIの機能のために追加
    [System.Serializable]
    public class QuickSlotButton
    {
        public GameObject slotGameObject; // 各クイックスロットのルートGameObject

        [HideInInspector]
        public Button button;

        [HideInInspector]
        public RectTransform slotTransform; // UIのTransform

        [HideInInspector]
        public CanvasGroup borderFlash; // 枠ImageのCanvasGroupでAlpha制御

        [HideInInspector]
        public Image itemIconImage; // アイテムアイコンのImage

        [HideInInspector]
        public TextMeshProUGUI countText; // アイテム個数表示のTextMeshProUGUI

        [HideInInspector]
        public Image buttonImage; // ボタン自体のImage

        [HideInInspector]
        public Tween scaleTween;

        [HideInInspector]
        public Tween flashTween;
    }

    [SerializeField]
    private QuickSlotButton[] quickSlotButtons; // 各ボタンのUI要素とTween管理用 (GameObjectをInspectorで設定)

    [SerializeField]
    private int columns = 5;

    [SerializeField]
    private int rows = 2;

    [SerializeField]
    private HealItemDatabase healItemDatabase;

    [SerializeField]
    private Sprite selectedButtonSprite;

    [SerializeField]
    private Sprite normalButtonSprite;

    [SerializeField]
    private Sprite transparentSquare;

    [HideInInspector]
    public int currentIndex = 0;

    private List<HealItemData> healItemData = new List<HealItemData>(); //アイテムの情報
    private List<ItemEntry> quickList = new List<ItemEntry>(); //セーブデータから参照する

    [Header("アイテムの効果を表示するパネルのUI")]
    [SerializeField]
    private GameObject playerHPBar;

    [SerializeField]
    private Image playerHPHealthBarImage;

    [SerializeField]
    private GameObject playerWPBar;

    [SerializeField]
    private Image playerWPHealthBarImage;

    [Header("バフのアイコンとバーのUI")]
    [SerializeField]
    private List<BuffEffectUI> buffEffectUIList;

    [System.Serializable]
    public class BuffEffectUI
    {
        public GameObject icon;
        public GameObject barObject;
        public Image barFillImage;
    }

    private Dictionary<GameObject, (GameObject, Image)> buffUIs;
    private float baseSize = 0; // ボタンのアイテム画像のベースサイズ（初期化時に設定）
    private bool isUiPaused = false; //UIが一時停止中かどうかを管理するフラグ

    private void Awake()
    {
        if (healItemDatabase == null)
        {
            Debug.LogError("QuickItemPanelはHealItemDatabaseが設定されていません");
            return;
        }

        if (selectedButtonSprite == null || normalButtonSprite == null || transparentSquare == null)
        {
            Debug.LogError("QuickItemPanelは必要なスプライトが設定されていません");
            return;
        }

        if (quickSlotButtons == null || quickSlotButtons.Length == 0)
        {
            Debug.LogError(
                "QuickItemPanelのquickSlotButtonsが設定されていません。各スロットのGameObjectをInspectorで設定してください。"
            );
            return;
        }

        // quickSlotButtonsの各要素から必要なコンポーネントを取得
        for (int i = 0; i < quickSlotButtons.Length; i++)
        {
            if (quickSlotButtons[i].slotGameObject == null)
            {
                Debug.LogError(
                    $"quickSlotButtons[{i}]のslotGameObjectがnullです。Inspectorで設定してください。"
                );
                continue;
            }

            quickSlotButtons[i].button = quickSlotButtons[i].slotGameObject.GetComponent<Button>();
            if (quickSlotButtons[i].button == null)
            {
                Debug.LogError(
                    $"slotGameObject {quickSlotButtons[i].slotGameObject.name}にButtonコンポーネントが見つかりません。"
                );
                continue;
            }

            quickSlotButtons[i].slotTransform = quickSlotButtons[i]
                .slotGameObject.GetComponent<RectTransform>();
            if (quickSlotButtons[i].slotTransform == null)
            {
                Debug.LogError(
                    $"slotGameObject {quickSlotButtons[i].slotGameObject.name}にRectTransformコンポーネントが見つかりません。"
                );
                continue;
            }

            quickSlotButtons[i].borderFlash = quickSlotButtons[i]
                .slotGameObject.GetComponent<CanvasGroup>();
            if (quickSlotButtons[i].borderFlash == null)
            {
                quickSlotButtons[i].borderFlash = quickSlotButtons[i]
                    .slotGameObject.AddComponent<CanvasGroup>();
                Debug.LogWarning(
                    $"slotGameObject {quickSlotButtons[i].slotGameObject.name}にCanvasGroupが見つからなかったため追加しました。"
                );
            }

            // ボタン自体のImage (選択/非選択スプライト切り替え用)
            quickSlotButtons[i].buttonImage = quickSlotButtons[i]
                .slotGameObject.GetComponent<Image>();
            if (quickSlotButtons[i].buttonImage == null)
            {
                Debug.LogError(
                    $"slotGameObject {quickSlotButtons[i].slotGameObject.name}にImageコンポーネントが見つかりません。"
                );
            }

            // アイテムアイコンのImage (子オブジェクトの0番目)
            if (quickSlotButtons[i].slotGameObject.transform.childCount > 0)
            {
                quickSlotButtons[i].itemIconImage = quickSlotButtons[i]
                    .slotGameObject.transform.GetChild(0)
                    .GetComponent<Image>();
                if (quickSlotButtons[i].itemIconImage == null)
                {
                    Debug.LogWarning(
                        $"slotGameObject {quickSlotButtons[i].slotGameObject.name}の最初のRaycast Targetではない子オブジェクトにImageコンポーネントが見つかりません。"
                    );
                }
            }
            else
            {
                Debug.LogWarning(
                    $"slotGameObject {quickSlotButtons[i].slotGameObject.name}に子オブジェクトが見つかりません。アイテムアイコンのImageを取得できません。"
                );
            }

            // 個数表示のTextMeshProUGUI (子オブジェクトの1番目)
            if (quickSlotButtons[i].slotGameObject.transform.childCount > 1)
            {
                quickSlotButtons[i].countText = quickSlotButtons[i]
                    .slotGameObject.transform.GetChild(1)
                    .GetComponent<TextMeshProUGUI>();
                if (quickSlotButtons[i].countText == null)
                {
                    Debug.LogWarning(
                        $"slotGameObject {quickSlotButtons[i].slotGameObject.name}の2番目の子オブジェクトにTextMeshProUGUIコンポーネントが見つかりません。"
                    );
                }
            }
            else
            {
                Debug.LogWarning(
                    $"slotGameObject {quickSlotButtons[i].slotGameObject.name}に十分な子オブジェクトが見つかりません。個数表示のTextMeshProUGUIを取得できません。"
                );
            }
        }

        if (
            playerHPBar == null
            || playerHPHealthBarImage == null
            || playerWPBar == null
            || playerWPHealthBarImage == null
        )
        {
            Debug.LogError("QuickItemPanelは必要なUIが設定されていません");
            return;
        }

        if (buffEffectUIList == null)
        {
            Debug.LogError("QuickItemPanelは必要なエフェクトUIが設定されていません");
            return;
        }

        // リストを Dictionary に変換
        buffUIs = new();
        foreach (var ui in buffEffectUIList)
        {
            buffUIs[ui.icon] = (ui.barObject, ui.barFillImage);
        }

        // アイテム画像のベースサイズを取得
        if (quickSlotButtons.Length > 0 && quickSlotButtons[0].itemIconImage != null)
        {
            baseSize = quickSlotButtons[0].itemIconImage.GetComponent<RectTransform>().sizeDelta.x; // 横幅をベースサイズとして使用
        }
        else
        {
            Debug.LogWarning("アイテム画像のRectTransformが取得できませんでした。");
        }
    }

    private void Start()
    {
        //ボタンの情報を取得
        for (int i = 0; i < quickSlotButtons.Length; i++)
        {
            int index = i; // 参照回避のためにローカルコピーを作成
            QuickSlotButton currentSlot = quickSlotButtons[i];

            if (currentSlot.button != null)
            {
                currentSlot.button.onClick.AddListener(() => OnButtonClicked(index));
            }
            else
            {
                Debug.LogError(
                    $"ButtonコンポーネントがquickSlotButtons[{i}]でnullです。onClickイベントを追加できません。"
                );
            }

            if (currentSlot.itemIconImage != null)
            {
                currentSlot.itemIconImage.sprite = transparentSquare;
            }
            if (currentSlot.countText != null)
            {
                currentSlot.countText.text = null;
            }
        }

        // PlayerManagerの参照を取得
        playerManager = PlayerManager.instance;
        if (playerManager == null)
        {
            Debug.LogError("PlayerManagerが見つかりませんでした");
            return;
        }
        else
        {
            //アイテムスロットに登録されているアイテムのIDが変更されたときに使用
            playerManager.OnQuickSlotAssigned += HandleQuickSlotAssigned;
            //セーブデータを取得する
            quickList = GameManager.instance.savedata.QuickItemData.ownedItems;
            //アイテムの所持数が変化したときに使用
            GameManager.instance.savedata.ItemInventoryData.OnItemCountChanged +=
                ChangeAllCountTextImage;
        }

        HandleQuickSlotAssigned(); //スロットを初期化する(quickListを取得してから行う)
        ChangeAllCountTextImage(); //所持数を初期化する
        StartCoroutine(InitialChangeSelection()); //最初のボタンを選択する(フレームの終わりまで待つ)
    }

    private IEnumerator InitialChangeSelection()
    {
        yield return new WaitForEndOfFrame(); //フレームの終わりまで待つ
        ChangeSelection(0); //最初のボタンを選択する
    }

    private void OnDisable()
    {
        // ゲームがまだ開始されていない場合は何もしない
        if (!GameManager.isFirstGameSceneOpen)
            return;

        // イベント解除（メモリリーク防止）
        GameManager.instance.savedata.ItemInventoryData.OnItemCountChanged -=
            ChangeAllCountTextImage;

        if (playerManager != null)
        {
            playerManager.OnQuickSlotAssigned -= HandleQuickSlotAssigned;
        }

        // Tweenを停止して破棄
        foreach (var slotButton in quickSlotButtons)
        {
            slotButton.scaleTween?.Kill();
            slotButton.flashTween?.Kill();
        }
    }

    private void Update()
    {
        // 1. 一時停止すべきかどうかの条件をチェック
        // UIが開いている、会話中、または時間が停止している場合は一時停止
        bool shouldBePaused =
            UIManager.instance.isMenuOpen || GameManager.IsTalking || Time.timeScale <= 0f;

        // 2. 状態の切り替わりを検知して、適切な処理を一度だけ呼び出す
        if (shouldBePaused && !isUiPaused)
        {
            // ポーズ状態に入った瞬間
            isUiPaused = true;
            PauseAllAnimations();
        }
        else if (!shouldBePaused && isUiPaused)
        {
            // ポーズ状態から復帰した瞬間
            isUiPaused = false;
            ResumeAllAnimations();
        }

        // 3. UIが一時停止中なら、以降の入力処理を行わない
        if (isUiPaused)
        {
            return;
        }

        if (InputManager.instance.GetQuickItemLeft())
            Move(-1);
        if (InputManager.instance.GetQuickItemRight())
            Move(1);
        if (InputManager.instance.GetQuickItemUpDown())
            MoveVertical();
        if (InputManager.instance.GetQuickItemSelect())
            PressCurrentButton();
    }

    /// <summary>
    /// 水平方向にカーソルを移動させる（左右）
    /// </summary>
    /// <param name="horizontal">
    /// -1なら左、+1なら右に移動
    /// </param>
    public void Move(int horizontal)
    {
        // 現在の列と行を計算
        int col = currentIndex % columns;
        int row = currentIndex / columns;

        // 新しい列を計算（範囲外に出ないようClamp）
        int newCol = Mathf.Clamp(col + horizontal, 0, columns - 1);

        // 新しいインデックスを計算（rowは変わらず、colだけ変更）
        int newIndex = row * columns + newCol;

        // 新しいインデックスがボタン数内なら移動
        if (newIndex >= 0 && newIndex < quickSlotButtons.Length)
        {
            ChangeSelection(newIndex);
        }
    }

    /// <summary>
    /// 垂直方向にカーソルを移動させる（下に進む）
    /// 現在の行の下の行へ移動。最下行の場合は一番上にループする。
    /// </summary>
    public void MoveVertical()
    {
        // 現在の列と行を計算
        int col = currentIndex % columns;
        int row = currentIndex / columns;

        // 新しい行を計算（行数内でループ）
        int newRow = (row + 1) % rows;

        // 新しいインデックスを計算（rowだけ変わる）
        int newIndex = newRow * columns + col;

        // 新しいインデックスがボタン数内なら移動
        if (newIndex < quickSlotButtons.Length)
        {
            ChangeSelection(newIndex);
        }
    }

    //選択されるボタンを変更
    private void ChangeSelection(int newIndex)
    {
        // 前の選択を解除
        if (currentIndex != newIndex) // 前の選択と異なる場合のみ実行
        {
            SetButtonSelected(currentIndex, false);
        }

        currentIndex = newIndex;
        SetButtonSelected(currentIndex, true);

        int itemID = 0; //アイテムのIDを初期化
        if (
            currentIndex < quickList.Count //インデックスが範囲内
            && quickList[currentIndex] != null //アイテムが存在
            && quickList[currentIndex].count > 0 //アイテムの個数が0より大きい
        )
        {
            //アイテムのIDを取得
            itemID = quickList[currentIndex].itemID;
        }

        if (HealItemPreviewUIManager.instance != null)
        {
            //アイテムの効果を表示する
            HealItemPreviewUIManager.instance.DisplaySelectedItemEffects(
                itemID,
                playerHPBar,
                playerHPHealthBarImage,
                playerWPBar,
                playerWPHealthBarImage,
                buffUIs
            );
        }
        else
        {
            Debug.LogError("PlayerEffectUIManagerが見つかりませんでした");
        }
    }

    /// <summary>
    /// ボタンの選択状態を設定し、Tweenアニメーションを制御します。
    /// </summary>
    /// <param name="index">対象のボタンのインデックス。</param>
    /// <param name="selected">選択状態にするか否か。</param>
    private void SetButtonSelected(int index, bool selected)
    {
        if (index < 0 || index >= quickSlotButtons.Length)
            return;

        QuickSlotButton currentButton = quickSlotButtons[index];

        if (currentButton.buttonImage != null)
        {
            currentButton.buttonImage.sprite = selected ? selectedButtonSprite : normalButtonSprite;
        }
        else
        {
            Debug.LogWarning(
                $"QuickSlotButton[{index}]のbuttonImageがnullです。選択スプライトを設定できません。"
            );
        }

        // 前のTweenが残っていたら停止
        currentButton.scaleTween?.Kill();
        currentButton.flashTween?.Kill();

        if (selected)
        {
            if (currentButton.slotTransform != null)
            {
                // ゆらゆら拡大（拡大縮小を繰り返す）
                currentButton.scaleTween = currentButton
                    .slotTransform.DOScale(1.1f, 0.4f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
            else
            {
                Debug.LogWarning(
                    $"QuickSlotButton[{index}]のslotTransformがnullです。拡大アニメーションを実行できません。"
                );
            }

            if (currentButton.borderFlash != null)
            {
                // 枠を点滅（Alphaを0→1→0）
                currentButton.flashTween = currentButton
                    .borderFlash.DOFade(1f, 0.4f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
            else
            {
                Debug.LogWarning(
                    $"QuickSlotButton[{index}]のborderFlashがnullです。点滅アニメーションを実行できません。"
                );
            }
        }
        else
        {
            if (currentButton.slotTransform != null)
            {
                // 元のサイズに戻す
                currentButton.scaleTween = currentButton.slotTransform.DOScale(1f, 0.2f);
            }
            if (currentButton.borderFlash != null)
            {
                // 枠の元に戻す
                currentButton.flashTween = currentButton.borderFlash.DOFade(1f, 0.2f);
            }
        }
    }

    private void PressCurrentButton()
    {
        if (quickSlotButtons[currentIndex].button != null)
        {
            quickSlotButtons[currentIndex].button.onClick.Invoke();
        }
        else
        {
            Debug.LogWarning(
                $"QuickSlotButton[{currentIndex}]のButtonコンポーネントがnullです。クリックできません。"
            );
        }
    }

    /// <summary>
    /// ボタンがクリックされたときの処理
    /// </summary>
    private void OnButtonClicked(int id)
    {
        if (id >= quickList.Count)
        {
            SEManager.instance?.PlayUISE(SE_UI.Beep1);
            return;
        }

        if (quickList[id] != null && quickList[id].count > 0)
        {
            playerManager.UseHealItem((HealItemName)quickList[id].itemID); //アイテムを使用する処理を行う
            if (quickList[id].count <= 0)
            {
                //ボタンのアイテムの画像を暗くする
                DisableButtonImage(id);
            }
        }
        else
        {
            //アイテムの所持数が0以下の時は、選べないようにする
            SEManager.instance?.PlayUISE(SE_UI.Beep1);
        }
    }

    //即座に使用できるアイテムが入れ替えられたときに呼び出す関数
    private void HandleQuickSlotAssigned()
    {
        if (GameManager.instance.savedata != null)
        {
            healItemData = new List<HealItemData>(new HealItemData[quickSlotButtons.Length]);
            //healItemDataのリストサイズが事前にquickSlotButtons.Countになるように初期化

            for (int i = 0; i < quickList.Count; i++)
            {
                var entry = quickList[i];
                if (i >= quickSlotButtons.Length) // quickSlotButtonsの範囲チェック
                {
                    Debug.LogWarning(
                        $"quickListの要素数({quickList.Count})がquickSlotButtonsの要素数({quickSlotButtons.Length})を超えています。"
                    );
                    break;
                }

                QuickSlotButton currentSlot = quickSlotButtons[i];
                if (currentSlot.itemIconImage == null)
                    continue; // イメージがない場合はスキップ

                if (entry == null || entry.itemID == 0)
                {
                    //スプライト画像を透明化
                    currentSlot.itemIconImage.sprite = transparentSquare;
                    continue;
                }

                //アイテムの情報を取得する
                healItemData[i] = healItemDatabase.GetItemByID((HealItemName)quickList[i].itemID);
                if (healItemData[i] == null)
                {
                    continue;
                }

                //アイテムスプライトを変更
                Sprite itemSprite = healItemData[i].itemSprite;
                if (itemSprite != null)
                {
                    //アイテム選択ボタンのImageコンポーネントにアイテムのスプライトを設定
                    UIUtility.SetSpriteFitToSquare(currentSlot.itemIconImage, itemSprite, baseSize);
                }
            }

            ChangeAllCountTextImage();
        }
        else
        {
            Debug.LogError("GameManagerが存在しません");
        }
    }

    //アイテムの所持数が変化したときに呼び出す関数
    private void ChangeAllCountTextImage()
    {
        for (int i = 0; i < quickList.Count; i++)
        {
            if (i >= quickSlotButtons.Length) // quickSlotButtonsの範囲チェック
            {
                Debug.LogWarning(
                    $"quickListの要素数({quickList.Count})がquickSlotButtonsの要素数({quickSlotButtons.Length})を超えています。"
                );
                break;
            }

            QuickSlotButton currentSlot = quickSlotButtons[i];
            if (currentSlot.countText == null)
                continue; // テキストがない場合はスキップ

            //個数の文章を変更
            if (quickList[i] == null || quickList[i].itemID == 0)
            {
                //もしアイテムがない場合は、文章を初期化
                currentSlot.countText.text = null;
                continue;
            }

            //アイテムの個数の文章を変更
            currentSlot.countText.text = $"<color=#FFD700>{quickList[i].count}</color>";

            if (0 < quickList[i].count)
            {
                EnableButtonImage(i); //ボタンの画像と文章を使えるようにする
            }
            else
            {
                //ボタンの画像と文章を使えないように黒くする
                DisableButtonImage(i);
            }
        }
    }

    //すべてのアニメーションを停止し、デフォルト状態に戻すメソッド
    private void PauseAllAnimations()
    {
        // すべてのスロットをループ
        for (int i = 0; i < quickSlotButtons.Length; i++)
        {
            QuickSlotButton slot = quickSlotButtons[i];

            // 既存のTweenをすべて停止
            slot.scaleTween?.Kill();
            slot.flashTween?.Kill();

            // スケールを元のサイズ(1.0)に戻す
            if (slot.slotTransform != null)
            {
                slot.slotTransform.localScale = Vector3.one;
            }

            // 点滅枠のアルファを元に戻す（点滅していない状態）
            // ※SetButtonSelectedの実装に基づき、非選択時はAlpha=1.0に戻しています
            if (slot.borderFlash != null)
            {
                slot.borderFlash.alpha = 1.0f;
            }

            // ボタンのスプライトを通常状態のものに戻す
            if (slot.buttonImage != null)
            {
                slot.buttonImage.sprite = normalButtonSprite;
            }
        }
    }

    //アニメーションを再開するメソッド
    private void ResumeAllAnimations()
    {
        // 現在選択されているボタンのアニメーションを再開するために、
        // 選択状態を再適用する
        ChangeSelection(currentIndex);
    }

    //ボタンの画像と文章を使えるようにする
    private void EnableButtonImage(int id)
    {
        if (id < 0 || id >= quickSlotButtons.Length)
            return;
        Image image = quickSlotButtons[id].itemIconImage;
        if (image == null)
            return;

        Color originalColor = image.color;
        Color.RGBToHSV(originalColor, out float h, out float s, out float v); // RGB → HSV に変換
        float clampedV = Mathf.Clamp01(255 / 255f); // V を新しい値に設定(安全のため [0,1] に制限)
        Color newColor = Color.HSVToRGB(h, s, clampedV); //HSV → RGB に変換
        newColor.a = originalColor.a; // alpha値は元のまま保つ
        image.color = newColor;
    }

    //ボタンの画像と文章を使えないように黒くする
    private void DisableButtonImage(int id)
    {
        if (id < 0 || id >= quickSlotButtons.Length)
            return;
        Image image = quickSlotButtons[id].itemIconImage;
        if (image == null)
            return;

        Color originalColor = image.color;
        Color.RGBToHSV(originalColor, out float h, out float s, out float v); // RGB → HSV に変換
        float clampedV = Mathf.Clamp01(20 / 255f); // V を新しい値に設定(安全のため [0,1] に制限)
        Color newColor = Color.HSVToRGB(h, s, clampedV); //HSV → RGB に変換
        newColor.a = originalColor.a; // alpha値は元のまま保つ
        image.color = newColor;
    }
}
