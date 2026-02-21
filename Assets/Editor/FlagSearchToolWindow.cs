using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーン内のすべてのオブジェクトを走査し、特定のフラグ（Enum）を使用しているコンポーネントを検索するエディタ拡張ツール。
/// Fungusの独自コマンドや、カスタムクラス（FlagConditionProなど）内で文字列として保存されているフラグの検知にも対応しています。
/// </summary>
public class FlagSearchToolWindow : EditorWindow
{
    #region Constants & Settings

    [Tooltip(
        "検索対象となるEnumの型名。FlagData.csに新しいフラグのカテゴリを追加した場合は、ここにも追記してください。"
    )]
    private readonly string[] targetEnumTypes = new string[]
    {
        "KeyID",
        "PrologueTriggeredEvent",
        "PrologueCountedEvent",
        "Chapter1TriggeredEvent",
        "Chapter1CountedEvent",
        "Chapter2TriggeredEvent",
        "Chapter2CountedEvent",
        "TutorialEvent",
    };

    // --- 履歴保存用のキー名と設定値 ---
    private const string PREFS_KEY_HISTORY = "FlagSearchTool_History";
    private const string PREFS_KEY_LAST_CATEGORY = "FlagSearchTool_LastCategory";
    private const string PREFS_KEY_LAST_FLAG = "FlagSearchTool_LastFlag";
    private const int MAX_HISTORY_COUNT = 5; // 保持する履歴の最大件数
    #endregion

    #region Private Fields

    // --- 検索UIの状態 ---
    private int selectedTypeIndex = 0; // 選択中のカテゴリ（Enum型）のインデックス
    private int selectedFlagIndex = 0; // 選択中のフラグ（Enum値）のインデックス
    private string[] currentFlagNames = new string[0]; // 現在選択中のカテゴリに属するフラグ名のリスト

    // --- 検索結果・履歴データ ---
    private List<SearchResult> searchResults = new List<SearchResult>();
    private List<string> searchHistory = new List<string>(); // "Category:Flag" の形式で保存
    private bool hasSearched = false; // 検索が一度でも実行されたか
    private Vector2 scrollPosition; // 検索結果一覧のスクロール位置
    #endregion

    #region Inner Classes

    /// <summary>
    /// 検索結果の1件分のデータを保持するクラス。
    /// </summary>
    private class SearchResult
    {
        public GameObject TargetObject; // フラグが設定されているGameObject（クリックでPingするため）
        public string ComponentName; // フラグが設定されているコンポーネント（スクリプト）名
        public string PropertyPath; // コンポーネント内での変数名、または詳細情報（Block名など）

        public SearchResult(GameObject targetObject, string componentName, string propertyPath)
        {
            TargetObject = targetObject;
            ComponentName = componentName;
            PropertyPath = propertyPath;
        }
    }

    #endregion

    #region Editor Window Lifecycle

    [MenuItem("Tools/Flag Search Tool")]
    public static void ShowWindow()
    {
        GetWindow<FlagSearchToolWindow>("Flag Searcher");
    }

    private void OnEnable()
    {
        // 1. Enumのリストを初期化
        UpdateFlagNames();

        // 2. 過去の検索履歴をロード
        LoadHistory();

        // 3. 前回ウィンドウを閉じた時の選択状態（カテゴリとフラグ）を復元
        string lastCategory = EditorPrefs.GetString(PREFS_KEY_LAST_CATEGORY, "");
        string lastFlag = EditorPrefs.GetString(PREFS_KEY_LAST_FLAG, "");

        if (!string.IsNullOrEmpty(lastCategory))
        {
            int catIndex = Array.IndexOf(targetEnumTypes, lastCategory);
            if (catIndex >= 0)
            {
                selectedTypeIndex = catIndex;
                UpdateFlagNames(); // 復元したカテゴリに合わせてフラグ一覧を更新
            }
        }

        if (!string.IsNullOrEmpty(lastFlag))
        {
            int flagIndex = Array.IndexOf(currentFlagNames, lastFlag);
            if (flagIndex >= 0)
            {
                selectedFlagIndex = flagIndex;
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("フラグ使用箇所 検索ツール", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // --- 1. フラグカテゴリの選択 ---
        EditorGUI.BeginChangeCheck();
        selectedTypeIndex = EditorGUILayout.Popup(
            "Flag Category",
            selectedTypeIndex,
            targetEnumTypes
        );
        if (EditorGUI.EndChangeCheck())
        {
            // カテゴリが変更されたらフラグ一覧を更新し、選択状態をリセットして保存
            UpdateFlagNames();
            selectedFlagIndex = 0;

            EditorPrefs.SetString(PREFS_KEY_LAST_CATEGORY, targetEnumTypes[selectedTypeIndex]);
            if (currentFlagNames.Length > 0)
            {
                EditorPrefs.SetString(PREFS_KEY_LAST_FLAG, currentFlagNames[selectedFlagIndex]);
            }
        }

        // --- 2. 具体的なフラグの選択 ---
        if (currentFlagNames.Length > 0)
        {
            EditorGUI.BeginChangeCheck();
            selectedFlagIndex = EditorGUILayout.Popup(
                "Target Flag",
                selectedFlagIndex,
                currentFlagNames
            );
            if (EditorGUI.EndChangeCheck())
            {
                // フラグが変更されたら選択状態を保存
                EditorPrefs.SetString(PREFS_KEY_LAST_FLAG, currentFlagNames[selectedFlagIndex]);
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "このカテゴリにはフラグが定義されていないか、コンパイルエラーが発生しています。",
                MessageType.Warning
            );
        }

        EditorGUILayout.Space();

        // --- 3. 検索実行ボタン ---
        if (GUILayout.Button("シーン内を検索 (Search in Scene)", GUILayout.Height(30)))
        {
            ExecuteSearch();
        }

        EditorGUILayout.Space();

        // --- 4. 最近の検索履歴 UI ---
        if (searchHistory.Count > 0)
        {
            EditorGUILayout.LabelField("最近の検索履歴 (クリックで再検索)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            foreach (var history in searchHistory)
            {
                string[] parts = history.Split(':');
                if (parts.Length == 2)
                {
                    string cat = parts[0];
                    string flag = parts[1];

                    // ボタンの幅を抑えるため、カテゴリ名の一部（TriggeredEvent等）を省略して表示
                    string shortCat = cat.Replace("TriggeredEvent", "").Replace("CountedEvent", "");
                    if (string.IsNullOrEmpty(shortCat))
                        shortCat = cat;

                    if (GUILayout.Button($"{shortCat} : {flag}", EditorStyles.miniButton))
                    {
                        ExecuteFromHistory(cat, flag);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        DrawHorizontalLine();

        // --- 5. 検索結果の表示 UI ---
        if (hasSearched)
        {
            GUILayout.Label($"検索結果: {searchResults.Count} 件", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (searchResults.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "現在のシーンにこのフラグを使用しているオブジェクトは見つかりませんでした。",
                    MessageType.Info
                );
            }
            else
            {
                foreach (var result in searchResults)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.BeginHorizontal();

                    // オブジェクトへの参照（クリックでHierarchy上で選択・Pingされる）
                    EditorGUILayout.ObjectField(
                        result.TargetObject,
                        typeof(GameObject),
                        true,
                        GUILayout.Width(200)
                    );

                    // 該当コンポーネント名
                    EditorGUILayout.LabelField(
                        $"[{result.ComponentName}]",
                        EditorStyles.boldLabel,
                        GUILayout.Width(150)
                    );

                    // 詳細情報（変数名やブロック名）
                    EditorGUILayout.LabelField($"変数名: {result.PropertyPath}");

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    #endregion

    #region Search Logic

    /// <summary>
    /// 現在選択されているカテゴリ（Enum型）をリフレクションで解析し、属するフラグ名の一覧を更新します。
    /// </summary>
    private void UpdateFlagNames()
    {
        string typeName = targetEnumTypes[selectedTypeIndex];

        // 基本的な名前空間（Assembly-CSharp）からTypeを取得
        Type enumType = Type.GetType(typeName + ", Assembly-CSharp");

        if (enumType != null && enumType.IsEnum)
        {
            currentFlagNames = Enum.GetNames(enumType);
        }
        else
        {
            // 万が一見つからない場合は、すべてのアセンブリから検索するフォールバック
            var type = AppDomain
                .CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == typeName && t.IsEnum);

            if (type != null)
            {
                currentFlagNames = Enum.GetNames(type);
            }
            else
            {
                currentFlagNames = new string[0];
            }
        }
    }

    /// <summary>
    /// 現在のシーン内すべてのオブジェクトとコンポーネントを走査し、ターゲットのフラグが使用されている箇所をリストアップします。
    /// </summary>
    private void ExecuteSearch()
    {
        searchResults.Clear();
        hasSearched = true;

        if (currentFlagNames.Length == 0)
            return;

        string targetTypeName = targetEnumTypes[selectedTypeIndex];
        string targetFlagName = currentFlagNames[selectedFlagIndex];

        // --- 履歴の保存 ---
        string historyItem = $"{targetTypeName}:{targetFlagName}";
        searchHistory.Remove(historyItem); // 既に同じ履歴があれば一旦削除して最新にする
        searchHistory.Insert(0, historyItem); // 先頭に追加
        if (searchHistory.Count > MAX_HISTORY_COUNT)
        {
            searchHistory.RemoveAt(searchHistory.Count - 1); // 上限を超えたら古いものを削除
        }
        SaveHistory();

        // --- シーン走査開始 ---
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (GameObject root in rootObjects)
        {
            // 子階層も含めて、非アクティブなオブジェクトのコンポーネントもすべて取得
            Component[] components = root.GetComponentsInChildren<Component>(true);

            foreach (Component comp in components)
            {
                if (comp == null)
                    continue;

                SerializedObject so = new SerializedObject(comp);

                // =========================================================
                // 特殊ケース: SetGameBoolFlagCommand の正確な検索処理
                // (Inspectorで隠れている別の章のフラグ変数を誤検知するのを防ぐ)
                // =========================================================
                if (comp.GetType().Name == "SetGameBoolFlagCommand")
                {
                    SerializedProperty categoryProp = so.FindProperty("category");
                    if (categoryProp != null)
                    {
                        int categoryIndex = categoryProp.enumValueIndex;
                        string activeFlagPropName = "";
                        string expectedEnumTypeName = "";

                        // FlagCategory Enumの順番と一致させる
                        switch (categoryIndex)
                        {
                            case 0: // Tutorial
                                activeFlagPropName = "tutorialFlag";
                                expectedEnumTypeName = "TutorialEvent";
                                break;
                            case 1: // Prologue
                                activeFlagPropName = "prologueFlag";
                                expectedEnumTypeName = "PrologueTriggeredEvent";
                                break;
                            case 2: // Chapter1
                                activeFlagPropName = "chapter1Flag";
                                expectedEnumTypeName = "Chapter1TriggeredEvent";
                                break;
                            case 3: // Chapter2
                                activeFlagPropName = "chapter2Flag";
                                expectedEnumTypeName = "Chapter2TriggeredEvent";
                                break;
                        }

                        // 現在検索しているカテゴリと、コマンドで実際に選択されているカテゴリが一致している場合のみ判定
                        if (expectedEnumTypeName == targetTypeName)
                        {
                            SerializedProperty flagProp = so.FindProperty(activeFlagPropName);
                            if (
                                flagProp != null
                                && flagProp.enumValueIndex >= 0
                                && flagProp.enumValueIndex < flagProp.enumNames.Length
                            )
                            {
                                if (flagProp.enumNames[flagProp.enumValueIndex] == targetFlagName)
                                {
                                    // このコマンドがフラグをTrue/Falseどちらに変更しようとしているか取得
                                    bool valueToSet = so.FindProperty("valueToSet").boolValue;
                                    string displayStr = $"コマンド (値を [{valueToSet}] に設定)";

                                    // FungusのBlock名を取得して末尾に追加
                                    displayStr += GetFungusBlockName(comp);

                                    searchResults.Add(
                                        new SearchResult(
                                            comp.gameObject,
                                            comp.GetType().Name,
                                            displayStr
                                        )
                                    );
                                }
                            }
                        }
                    }
                    // 専用処理を行ったため、このコンポーネントの汎用プロパティ走査はスキップ
                    continue;
                }

                // =========================================================
                // 通常ケース: 汎用プロパティ走査
                // =========================================================
                SerializedProperty prop = so.GetIterator();
                bool enterChildren = true;

                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = true;

                    // パターン1: 従来のEnumとして直接定義されている場合のチェック（Unity標準機能や他のアセット用）
                    if (prop.propertyType == SerializedPropertyType.Enum)
                    {
                        if (prop.type.Contains(targetTypeName))
                        {
                            if (
                                prop.enumValueIndex >= 0
                                && prop.enumValueIndex < prop.enumNames.Length
                            )
                            {
                                string currentEnumName = prop.enumNames[prop.enumValueIndex];
                                if (currentEnumName == targetFlagName)
                                {
                                    string displayPath =
                                        prop.propertyPath + GetFungusBlockName(comp);
                                    searchResults.Add(
                                        new SearchResult(
                                            comp.gameObject,
                                            comp.GetType().Name,
                                            displayPath
                                        )
                                    );
                                }
                            }
                        }
                    }
                    // パターン2: FlagConditionPro のように、クラス内に文字列(enumValueName)として保存されている場合のチェック
                    else if (
                        prop.propertyType == SerializedPropertyType.String
                        && prop.name == "enumValueName"
                    )
                    {
                        // 同じ階層（同じクラス内）にある enumTypeName の値を取得する
                        string basePath = prop.propertyPath.Substring(
                            0,
                            prop.propertyPath.LastIndexOf("enumValueName")
                        );
                        SerializedProperty typeProp = so.FindProperty(basePath + "enumTypeName");

                        if (
                            typeProp != null
                            && typeProp.propertyType == SerializedPropertyType.String
                        )
                        {
                            string savedTypeName = typeProp.stringValue;
                            string savedValueName = prop.stringValue;

                            // 保存されている型名にターゲットの型名が含まれており、かつフラグ名が一致しているかチェック
                            if (
                                !string.IsNullOrEmpty(savedTypeName)
                                && savedTypeName.Contains(targetTypeName)
                            )
                            {
                                if (savedValueName == targetFlagName)
                                {
                                    // 表示名が「...Array.data[0].」などで終わらないように見やすく整形
                                    string displayPath = basePath.TrimEnd('.');
                                    if (string.IsNullOrEmpty(displayPath))
                                        displayPath = "条件リスト内";

                                    displayPath += GetFungusBlockName(comp);

                                    searchResults.Add(
                                        new SearchResult(
                                            comp.gameObject,
                                            comp.GetType().Name,
                                            displayPath
                                        )
                                    );
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region History Management

    /// <summary>
    /// EditorPrefsから検索履歴（文字列リスト）を読み込みます。
    /// </summary>
    private void LoadHistory()
    {
        string saved = EditorPrefs.GetString(PREFS_KEY_HISTORY, "");
        if (!string.IsNullOrEmpty(saved))
        {
            searchHistory = new List<string>(saved.Split(','));
        }
    }

    /// <summary>
    /// 現在の検索履歴をEditorPrefsへ保存します。
    /// </summary>
    private void SaveHistory()
    {
        EditorPrefs.SetString(PREFS_KEY_HISTORY, string.Join(",", searchHistory));
    }

    /// <summary>
    /// 履歴ボタンが押された際に、UIの選択状態を書き換えて即座に検索を実行します。
    /// </summary>
    private void ExecuteFromHistory(string targetCategory, string targetFlag)
    {
        // 1. カテゴリのインデックスを探してセット
        int catIndex = Array.IndexOf(targetEnumTypes, targetCategory);
        if (catIndex >= 0)
        {
            selectedTypeIndex = catIndex;
            UpdateFlagNames(); // そのカテゴリに属するフラグ一覧を更新

            // 2. フラグのインデックスを探してセット
            int flagIndex = Array.IndexOf(currentFlagNames, targetFlag);
            if (flagIndex >= 0)
            {
                selectedFlagIndex = flagIndex;

                // 履歴から検索した場合も「最後に選択した状態」として保存
                EditorPrefs.SetString(PREFS_KEY_LAST_CATEGORY, targetEnumTypes[selectedTypeIndex]);
                EditorPrefs.SetString(PREFS_KEY_LAST_FLAG, currentFlagNames[selectedFlagIndex]);

                // 3. 検索実行
                ExecuteSearch();

                // 意図しない入力やエラーを防ぐため、UIからフォーカスを外す
                GUI.FocusControl(null);
            }
        }
    }

    #endregion

    #region UI & Utility Helpers

    /// <summary>
    /// インスペクターの区切り線を描画します。
    /// </summary>
    private void DrawHorizontalLine()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        rect.height = 1;
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        EditorGUILayout.Space();
    }

    /// <summary>
    /// 対象のコンポーネントがFungusのコマンドである場合、その親のBlock名を取得して返します。
    /// </summary>
    private string GetFungusBlockName(Component comp)
    {
        Fungus.Command command = comp as Fungus.Command;

        if (command != null && command.ParentBlock != null)
        {
            return $" (Block: {command.ParentBlock.BlockName})";
        }
        return "";
    }

    #endregion
}
