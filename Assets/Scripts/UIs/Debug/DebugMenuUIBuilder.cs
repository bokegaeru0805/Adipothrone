using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// DebugMenuManager が使用するUGUIを実行時に構築する。
/// </summary>
public sealed class DebugMenuUIBuilder
{
    public sealed class View
    {
        public GameObject Root;
        public TMP_InputField HpInput;
        public TMP_InputField WpInput;
        public TMP_InputField MoneyInput;
        public TMP_InputField LevelInput;
        public TMP_InputField PositionInput;
        public TMP_InputField ItemAmountInput;
        public TMP_InputField TimeScaleInput;
        public TMP_InputField MouseDamagePercentInput;
        public Toggle EventAreaToggle;
        public Toggle MouseDamageToggle;
        public Toggle PlayerInvincibleToggle;
        public TextMeshProUGUI SceneText;
        public TextMeshProUGUI FpsText;
        public TextMeshProUGUI StatusText;
        public Button CloseButton;
        public Button RefreshButton;
        public Button ApplyHpButton;
        public Button ApplyWpButton;
        public Button ApplyMoneyButton;
        public Button ApplyLevelButton;
        public Button ApplyPositionButton;
        public Button GiveAllKeyItemsButton;
        public Button GiveAllHealItemsButton;
        public Button GiveAllStatusEnhanceItemsButton;
        public Button GiveAllMaterialItemsButton;
        public Button GiveAllWeaponsButton;
        public Button GiveAllRecipeItemsButton;
        public Button UnlockAllSkillsButton;
        public Button UnlockAllEnemyDropItemsButton;
        public Button ApplyTimeScaleButton;
        public Button ApplyMouseDamagePercentButton;
        public Button ResetDebugSettingsButton;
        public readonly List<Button> TimeScalePresetButtons = new List<Button>();
        public readonly List<Button> TabButtons = new List<Button>();
        public readonly List<GameObject> TabPanels = new List<GameObject>();
    }

    private static readonly Color BackdropColor = new Color32(4, 8, 13, 224);
    private static readonly Color WindowColor = new Color32(18, 25, 34, 255);
    private static readonly Color PanelColor = new Color32(25, 35, 47, 255);
    private static readonly Color FieldColor = new Color32(10, 17, 24, 255);
    private static readonly Color AccentColor = new Color32(41, 210, 214, 255);
    private static readonly Color WarningColor = new Color32(226, 166, 54, 255);
    private static readonly Color TextColor = new Color32(225, 235, 241, 255);
    private static readonly Color MutedTextColor = new Color32(139, 160, 174, 255);

    private readonly Transform _parent;
    private readonly TMP_FontAsset _font;

    public DebugMenuUIBuilder(Transform parent, TMP_FontAsset font = null)
    {
        _parent = parent;
        _font = font != null ? font : TMP_Settings.defaultFontAsset;
    }

    public View Build()
    {
        var view = new View();
        GameObject root = CreateUIObject("RuntimeDebugUI", _parent);
        Stretch(root.GetComponent<RectTransform>());
        view.Root = root;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32000;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        GameObject backdrop = CreateImage("Backdrop", root.transform, BackdropColor);
        Stretch(backdrop.GetComponent<RectTransform>());

        GameObject window = CreateImage("Window", root.transform, WindowColor);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.08f, 0.07f);
        windowRect.anchorMax = new Vector2(0.92f, 0.93f);
        windowRect.offsetMin = Vector2.zero;
        windowRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup windowLayout = window.AddComponent<VerticalLayoutGroup>();
        windowLayout.padding = new RectOffset(18, 18, 16, 14);
        windowLayout.spacing = 10f;
        windowLayout.childControlWidth = true;
        windowLayout.childControlHeight = true;
        windowLayout.childForceExpandWidth = true;
        windowLayout.childForceExpandHeight = false;

        BuildHeader(window.transform, view);
        BuildTabs(window.transform, view);
        BuildContent(window.transform, view);
        BuildStatusBar(window.transform, view);
        SelectTab(view, 0);

        root.SetActive(false);
        return view;
    }

    public static void SelectTab(View view, int selectedIndex)
    {
        for (int i = 0; i < view.TabPanels.Count; i++)
        {
            bool isSelected = i == selectedIndex;
            view.TabPanels[i].SetActive(isSelected);
            ColorBlock colors = view.TabButtons[i].colors;
            colors.normalColor = isSelected ? AccentColor : PanelColor;
            colors.selectedColor = colors.normalColor;
            view.TabButtons[i].colors = colors;
        }

        if (EventSystem.current != null && selectedIndex < view.TabButtons.Count)
            EventSystem.current.SetSelectedGameObject(view.TabButtons[selectedIndex].gameObject);
    }

    private void BuildHeader(Transform parent, View view)
    {
        GameObject header = CreateHorizontalGroup("Header", parent, 10f, 64f);
        CreateText("Title", header.transform, "デバッグメニュー", 32f, AccentColor, FontStyles.Bold)
            .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        view.SceneText = CreateText("Scene", header.transform, "シーン: -", 20f, MutedTextColor);
        SetPreferredWidth(view.SceneText.gameObject, 320f);
        view.FpsText = CreateText("FPS", header.transform, "FPS: --", 20f, AccentColor);
        SetPreferredWidth(view.FpsText.gameObject, 130f);
        view.CloseButton = CreateButton("Close", header.transform, "閉じる [F2]", 170f, PanelColor);
    }

    private void BuildTabs(Transform parent, View view)
    {
        GameObject tabs = CreateHorizontalGroup("Tabs", parent, 8f, 48f);
        string[] tabNames = { "プレイヤー", "アイテム", "ワールド", "システム" };
        for (int i = 0; i < tabNames.Length; i++)
        {
            int tabIndex = i;
            Button button = CreateButton(tabNames[i] + "Tab", tabs.transform, tabNames[i], 0f, PanelColor);
            button.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            button.onClick.AddListener(() => SelectTab(view, tabIndex));
            view.TabButtons.Add(button);
        }
    }

    private void BuildContent(Transform parent, View view)
    {
        GameObject viewport = CreateImage("ContentViewport", parent, PanelColor);
        viewport.AddComponent<RectMask2D>();
        LayoutElement viewportLayout = viewport.AddComponent<LayoutElement>();
        viewportLayout.flexibleHeight = 1f;
        viewportLayout.minHeight = 300f;

        for (int i = 0; i < 4; i++)
        {
            GameObject panel = CreateUIObject("TabPanel" + i, viewport.transform);
            Stretch(panel.GetComponent<RectTransform>());
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 18);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            view.TabPanels.Add(panel);
        }

        BuildPlayerPanel(view.TabPanels[0].transform, view);
        BuildInventoryPanel(view.TabPanels[1].transform, view);
        BuildWorldPanel(view.TabPanels[2].transform, view);
        BuildSystemPanel(view.TabPanels[3].transform, view);
    }

    private void BuildPlayerPanel(Transform parent, View view)
    {
        CreateSectionTitle(parent, "プレイヤーステータス");
        view.HpInput = CreateValueRow(parent, "現在HP", "整数を入力", out view.ApplyHpButton);
        view.WpInput = CreateValueRow(parent, "現在WP", "整数を入力", out view.ApplyWpButton);
        view.MoneyInput = CreateValueRow(parent, "所持金", "整数を入力", out view.ApplyMoneyButton);
        view.LevelInput = CreateValueRow(parent, "レベル", "整数を入力", out view.ApplyLevelButton);
        view.PositionInput = CreateValueRow(parent, "座標", "例: 10.5, 20.0", out view.ApplyPositionButton, "移動");
        view.RefreshButton = CreateButton("Refresh", parent, "現在値を再取得", 0f, FieldColor);
        view.RefreshButton.gameObject.GetComponent<LayoutElement>().preferredHeight = 48f;
    }

    private void BuildInventoryPanel(Transform parent, View view)
    {
        CreateSectionTitle(parent, "アイテム一括操作");
        view.ItemAmountInput = CreateInputField("ItemAmount", parent, "アイテムごとの付与個数");
        view.ItemAmountInput.gameObject.GetComponent<LayoutElement>().preferredHeight = 48f;

        GameObject gridObject = CreateUIObject("InventoryGrid", parent);
        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(390f, 58f);
        grid.spacing = new Vector2(12f, 12f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperCenter;
        gridObject.AddComponent<LayoutElement>().preferredHeight = 268f;

        view.GiveAllHealItemsButton = CreateButton("GiveHeal", gridObject.transform, "全回復アイテムを付与", 0f, WarningColor);
        view.GiveAllStatusEnhanceItemsButton = CreateButton("GiveEnhance", gridObject.transform, "全強化アイテムを付与", 0f, WarningColor);
        view.GiveAllMaterialItemsButton = CreateButton("GiveMaterial", gridObject.transform, "全素材アイテムを付与", 0f, WarningColor);
        view.GiveAllKeyItemsButton = CreateButton("GiveKey", gridObject.transform, "全キーアイテムを付与", 0f, WarningColor);
        view.GiveAllRecipeItemsButton = CreateButton("GiveRecipe", gridObject.transform, "全レシピを付与", 0f, WarningColor);
        view.GiveAllWeaponsButton = CreateButton("GiveWeapon", gridObject.transform, "全武器を付与", 0f, WarningColor);
        view.UnlockAllSkillsButton = CreateButton("UnlockSkills", gridObject.transform, "全スキルを解放", 0f, WarningColor);
        view.UnlockAllEnemyDropItemsButton = CreateButton(
            "UnlockEnemyDropItems",
            gridObject.transform,
            "全敵情報・ドロップを解放",
            0f,
            WarningColor
        );
    }

    private void BuildWorldPanel(Transform parent, View view)
    {
        CreateSectionTitle(parent, "ワールド表示");
        view.EventAreaToggle = CreateToggle("EventAreaToggle", parent, "フィールドイベントエリアを表示");
        CreateText(
            "WorldHint",
            parent,
            "設定はDebugSettings.es3へ保存され、配置済みのBaseFieldEventへ通知されます。",
            19f,
            MutedTextColor
        );
    }

    private void BuildSystemPanel(Transform parent, View view)
    {
        CreateSectionTitle(parent, "実行環境");
        view.TimeScaleInput = CreateValueRow(parent, "ゲーム速度", "0.1 ～ 10.0", out view.ApplyTimeScaleButton);

        GameObject presets = CreateHorizontalGroup("TimeScalePresets", parent, 8f, 52f);
        string[] labels = { "0.25x", "0.5x", "1x", "2x", "4x" };
        foreach (string label in labels)
        {
            Button button = CreateButton("Preset" + label, presets.transform, label, 0f, FieldColor);
            button.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            view.TimeScalePresetButtons.Add(button);
        }

        CreateText(
            "SystemHint",
            parent,
            $"Unity {Application.unityVersion}    Development Build: {(Debug.isDebugBuild ? "有効" : "無効")}",
            20f,
            MutedTextColor
        );

        CreateSectionTitle(parent, "戦闘テスト");
        view.MouseDamageToggle = CreateToggle("MouseDamageToggle", parent, "クリックで敵・破壊可能オブジェクトにダメージ");
        view.MouseDamagePercentInput = CreateValueRow(
            parent,
            "クリックダメージ",
            "最大HPに対する割合（0 ～ 100）",
            out view.ApplyMouseDamagePercentButton,
            "%を設定"
        );
        view.PlayerInvincibleToggle = CreateToggle("PlayerInvincibleToggle", parent, "プレイヤー無敵");
        view.ResetDebugSettingsButton = CreateButton(
            "ResetDebugSettings",
            parent,
            "デバッグ設定を初期値へ戻す",
            0f,
            FieldColor
        );
        view.ResetDebugSettingsButton.gameObject.GetComponent<LayoutElement>().preferredHeight = 52f;
    }

    private void BuildStatusBar(Transform parent, View view)
    {
        GameObject status = CreateImage("StatusBar", parent, FieldColor);
        status.AddComponent<LayoutElement>().preferredHeight = 42f;
        HorizontalLayoutGroup layout = status.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 6, 6);
        layout.childAlignment = TextAnchor.MiddleLeft;
        view.StatusText = CreateText("Status", status.transform, "準備完了", 18f, MutedTextColor);
    }

    private TMP_InputField CreateValueRow(
        Transform parent,
        string label,
        string placeholder,
        out Button applyButton,
        string buttonLabel = "適用"
    )
    {
        GameObject row = CreateHorizontalGroup(label.Replace(" ", string.Empty) + "Row", parent, 10f, 52f);
        TextMeshProUGUI labelText = CreateText("Label", row.transform, label, 20f, TextColor);
        SetPreferredWidth(labelText.gameObject, 220f);
        TMP_InputField input = CreateInputField(label.Replace(" ", string.Empty) + "Input", row.transform, placeholder);
        input.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
        applyButton = CreateButton("Apply", row.transform, buttonLabel, 150f, AccentColor);
        return input;
    }

    private TMP_InputField CreateInputField(string name, Transform parent, string placeholderText)
    {
        GameObject fieldObject = CreateImage(name, parent, FieldColor);
        LayoutElement fieldLayout = fieldObject.AddComponent<LayoutElement>();
        fieldLayout.minWidth = 180f;
        fieldLayout.preferredHeight = 48f;

        TMP_InputField input = fieldObject.AddComponent<TMP_InputField>();
        input.transition = Selectable.Transition.ColorTint;
        input.colors = CreateColorBlock(FieldColor, AccentColor);

        GameObject textArea = CreateUIObject("Text Area", fieldObject.transform);
        RectTransform areaRect = textArea.GetComponent<RectTransform>();
        Stretch(areaRect);
        areaRect.offsetMin = new Vector2(12f, 5f);
        areaRect.offsetMax = new Vector2(-12f, -5f);
        textArea.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = CreateText("Placeholder", textArea.transform, placeholderText, 19f, MutedTextColor);
        Stretch(placeholder.rectTransform);
        placeholder.fontStyle = FontStyles.Italic;

        TextMeshProUGUI text = CreateText("Text", textArea.transform, string.Empty, 21f, TextColor);
        Stretch(text.rectTransform);
        text.enableWordWrapping = false;

        input.textViewport = areaRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }

    private Toggle CreateToggle(string name, Transform parent, string label)
    {
        GameObject row = CreateHorizontalGroup(name, parent, 12f, 54f);
        Toggle toggle = row.AddComponent<Toggle>();

        GameObject background = CreateImage("Background", row.transform, FieldColor);
        SetPreferredWidth(background, 44f);
        background.GetComponent<LayoutElement>().preferredHeight = 44f;

        GameObject checkmark = CreateImage("Checkmark", background.transform, AccentColor);
        RectTransform checkRect = checkmark.GetComponent<RectTransform>();
        Stretch(checkRect);
        checkRect.offsetMin = new Vector2(8f, 8f);
        checkRect.offsetMax = new Vector2(-8f, -8f);

        TextMeshProUGUI text = CreateText("Label", row.transform, label, 20f, TextColor);
        text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        toggle.targetGraphic = background.GetComponent<Image>();
        toggle.graphic = checkmark.GetComponent<Image>();
        return toggle;
    }

    private Button CreateButton(string name, Transform parent, string label, float width, Color color)
    {
        GameObject buttonObject = CreateImage(name, parent, color);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.colors = CreateColorBlock(color, Color.Lerp(color, Color.white, 0.25f));

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        if (width > 0f)
        {
            layout.minWidth = width;
            layout.preferredWidth = width;
        }

        TextMeshProUGUI text = CreateText("Label", buttonObject.transform, label, 19f, TextColor, FontStyles.Bold);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private void CreateSectionTitle(Transform parent, string title)
    {
        TextMeshProUGUI text = CreateText("SectionTitle", parent, title, 25f, AccentColor, FontStyles.Bold);
        text.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
    }

    private TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string value,
        float fontSize,
        Color color,
        FontStyles style = FontStyles.Normal
    )
    {
        GameObject textObject = CreateUIObject(name, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (_font != null)
            text.font = _font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateHorizontalGroup(string name, Transform parent, float spacing, float height)
    {
        GameObject group = CreateUIObject(name, parent);
        HorizontalLayoutGroup layout = group.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;
        group.AddComponent<LayoutElement>().preferredHeight = height;
        return group;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject result = CreateUIObject(name, parent);
        Image image = result.AddComponent<Image>();
        image.color = color;
        return result;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var result = new GameObject(name, typeof(RectTransform));
        result.layer = 5;
        result.transform.SetParent(parent, false);
        return result;
    }

    private static ColorBlock CreateColorBlock(Color normal, Color highlighted)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.selectedColor = highlighted;
        colors.pressedColor = Color.Lerp(normal, Color.black, 0.25f);
        colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.35f);
        colors.colorMultiplier = 1f;
        return colors;
    }

    private static void SetPreferredWidth(GameObject target, float width)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout == null)
            layout = target.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = width;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
