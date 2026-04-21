using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SkillDatabaseのインスペクター表示を拡張するカスタムエディタークラス。
/// カテゴリ別表示と標準リスト表示（並び替え用）の切り替え機能、
/// および新規スキルの自動検索・追加機能を提供します。
/// </summary>
[CustomEditor(typeof(SkillDatabase))]
public class SkillDatabaseEditor : BaseDatabaseEditor<SkillDatabase>
{
    [Tooltip(
        "インスペクターでの表示モードを管理するフラグ（true: カテゴリ別表示, false: 標準リスト表示）"
    )]
    private bool showAsCategories = true;

    /// <summary>
    /// ベースクラスで描画される自動追加ボタンのテキストを取得します。
    /// </summary>
    protected override string GetButtonText() => "新規スキルを自動検索・追加";

    /// <summary>
    /// 自動追加ボタンを押した際に表示される確認ダイアログのメッセージを取得します。
    /// </summary>
    protected override string GetDialogMessage() =>
        "指定フォルダから新しいスキルを検索し、カテゴリごとにリストの末尾に追加します。よろしいですか？";

    /// <summary>
    /// 新しいスキルデータの検索とリストへの追加処理を実行します。
    /// </summary>
    /// <param name="database">更新対象のスキルデータベース</param>
    protected override void ExecuteUpdate(SkillDatabase database)
    {
        // 検索対象のフォルダパス（プロジェクトの構成に合わせて変更してください）
        const string skillPath = "Assets/SkillData";
        var idCheckDict = new Dictionary<System.Enum, string>();

        // 追加前のリストの要素数を記録
        int beforeCount = database.skills.Count;

        // 基底クラスのメソッドを使用してフォルダ内を検索し、新規アセットをリストに追加
        int addedCount = ProcessTargetList(skillPath, database.skills, idCheckDict);

        // 新規要素が追加された、あるいはリストに変化（nullの削除など）があった場合はソートを実行
        if (addedCount > 0 || beforeCount != database.skills.Count)
        {
            ReorderSkillsByCategory(database, addedCount);
        }

        // 変更を保存（基底クラスのメソッド）
        SaveDatabase(database, addedCount, "スキル");
    }

    /// <summary>
    /// リスト内のスキルをカテゴリごとに整理します。
    /// 手動で並び替えた既存の順番を維持しつつ、新規追加分を各カテゴリの最後尾に配置します。
    /// </summary>
    /// <param name="database">整理対象のデータベース</param>
    /// <param name="addedCount">今回新しく追加されたスキルの数</param>
    private void ReorderSkillsByCategory(SkillDatabase database, int addedCount)
    {
        // 追加前の既存スキルと、今回新規追加されたスキルをリストに分離
        int originalCount = database.skills.Count - addedCount;
        List<SkillData> oldSkills = database.skills.GetRange(0, originalCount);
        List<SkillData> newSkills = database.skills.GetRange(originalCount, addedCount);

        List<SkillData> reorderedList = new List<SkillData>();

        // Enumに定義されたカテゴリの順番に従ってリストを再構築
        foreach (SkillCategory category in System.Enum.GetValues(typeof(SkillCategory)))
        {
            // 1. 既存のスキルを追加（FindAllは元の順番を維持するため、手動の並び順が保護される）
            reorderedList.AddRange(oldSkills.FindAll(s => s != null && s.category == category));

            // 2. 新規追加されたスキルを該当カテゴリの最後尾に追加
            reorderedList.AddRange(newSkills.FindAll(s => s != null && s.category == category));
        }

        // アサイン忘れ等でnull要素が混入している場合は、リストの最後にまとめる
        reorderedList.AddRange(database.skills.FindAll(s => s == null));

        // 整理されたリストでデータベースを上書き
        database.skills = reorderedList;
    }

    // =========================================================
    // インスペクターのカスタム描画処理
    // =========================================================
    public override void OnInspectorGUI()
    {
        // 変更の監視を開始
        serializedObject.Update();

        SkillDatabase db = (SkillDatabase)target;
        SerializedProperty skillsProp = serializedObject.FindProperty("skills");

        EditorGUILayout.Space(5);

        // --- 表示モード切り替えトグルの描画 ---
        GUIStyle toggleStyle = new GUIStyle(EditorStyles.toggle) { fontStyle = FontStyle.Bold };

        EditorGUILayout.BeginVertical("box");
        showAsCategories = EditorGUILayout.ToggleLeft(
            " カテゴリ別に表示する (オフにするとドラッグで並び替え可能)",
            showAsCategories,
            toggleStyle
        );
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // --- モード別のリスト描画 ---
        if (showAsCategories)
        {
            // 【カテゴリ表示モード】
            // カテゴリごとにヘッダーを付けて要素を描画します（ドラッグ＆ドロップ不可）
            foreach (SkillCategory category in System.Enum.GetValues(typeof(SkillCategory)))
            {
                bool hasPrintedHeader = false;

                for (int i = 0; i < db.skills.Count; i++)
                {
                    if (db.skills[i] != null && db.skills[i].category == category)
                    {
                        // 該当カテゴリの最初の要素を描画する直前にヘッダーを表示
                        if (!hasPrintedHeader)
                        {
                            EditorGUILayout.Space(10);
                            EditorGUILayout.LabelField(
                                $"【 {GetCategoryName(category)} 】",
                                EditorStyles.boldLabel
                            );
                            hasPrintedHeader = true;
                        }

                        EditorGUILayout.PropertyField(skillsProp.GetArrayElementAtIndex(i), true);
                    }
                }
            }

            // nullの要素があれば最後に「未設定」として表示
            bool hasPrintedNullHeader = false;
            for (int i = 0; i < db.skills.Count; i++)
            {
                if (db.skills[i] == null)
                {
                    if (!hasPrintedNullHeader)
                    {
                        EditorGUILayout.Space(10);
                        EditorGUILayout.LabelField("【 未設定 (Null) 】", EditorStyles.boldLabel);
                        hasPrintedNullHeader = true;
                    }
                    EditorGUILayout.PropertyField(skillsProp.GetArrayElementAtIndex(i), true);
                }
            }
        }
        else
        {
            // 【標準リストモード】
            // Unityデフォルトの配列表示（ドラッグ＆ドロップでの並び替え可能）
            EditorGUILayout.HelpBox(
                "左端の「＝」アイコンをドラッグして順番を入れ替えることができます。\n※異なるカテゴリの間に移動させても、再度「カテゴリ表示」をオンにするか、新規追加ボタンを押すと自動的にカテゴリごとに整理されます。",
                MessageType.Info
            );
            EditorGUILayout.PropertyField(skillsProp, new GUIContent("全てのスキル"), true);
        }

        // 変更を適用
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(15);

        // 基底クラス(BaseDatabaseEditor)のOnInspectorGUIを呼び出し、自動追加ボタン等を描画
        // ※Database側の skills 変数に [HideInInspector] を付けているため、リストが重複して描画されることはありません
        base.OnInspectorGUI();
    }

    /// <summary>
    /// SkillCategoryのEnum値を、エディタ表示用の日本語テキストに変換します。
    /// </summary>
    /// <param name="category">変換元のカテゴリ</param>
    /// <returns>インスペクターに表示する文字列</returns>
    private string GetCategoryName(SkillCategory category)
    {
        switch (category)
        {
            case SkillCategory.Basic:
                return "基本型";
            case SkillCategory.Exploration:
                return "探索型";
            case SkillCategory.Attack:
                return "攻撃型";
            case SkillCategory.Defense:
                return "防御型";
            case SkillCategory.Luck:
                return "幸運型";
            case SkillCategory.Item:
                return "アイテム型";
            case SkillCategory.Special:
                return "特殊型";
            default:
                return category.ToString();
        }
    }
}
