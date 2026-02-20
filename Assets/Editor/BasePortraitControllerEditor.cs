using System.Linq;
using Fungus;
using Fungus.EditorUtils;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BasePortraitController), true)]
public class BasePortraitControllerEditor : Editor
{
    // Characterプロパティの参照を保持する変数
    protected SerializedProperty characterProp;

    // インスペクターが表示される際にプロパティを取得
    protected virtual void OnEnable()
    {
        characterProp = serializedObject.FindProperty("character");
    }

    // InspectorのGUIを描画する際に呼び出されるメソッド
    public override void OnInspectorGUI()
    {
        // 最新の状態でプロパティを更新
        serializedObject.Update();

        // --- Characterをドロップダウンで選択できるように描画 ---
        CommandEditor.ObjectField<Character>(
            characterProp,
            new GUIContent("Character", "担当するキャラクター"),
            new GUIContent("<None>"), // キャラクターが設定されていない場合の表示
            Character.ActiveCharacters // シーン内の全キャラクターをリストアップ
        );

        EditorGUILayout.Space();

        // --- Character以外のプロパティを標準の形式で描画する ---
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            // スクリプト自体の参照と、独自に描画した character プロパティは描画をスキップ
            if (iterator.name == "m_Script" || iterator.name == "character")
            {
                continue;
            }
            EditorGUILayout.PropertyField(iterator, true);
        }

        // ここまでの変更を適用
        serializedObject.ApplyModifiedProperties();

        // 対象のコンポーネント（BasePortraitControllerまたはその派生クラス）のインスタンスを取得
        BasePortraitController controller = (BasePortraitController)target;

        // ボタンとの間に少しスペースを空けて見やすくする
        EditorGUILayout.Space(10);

        // キャラクターが未設定の場合は警告を出して処理を止める
        if (controller.character == null)
        {
            EditorGUILayout.HelpBox(
                "Character が設定されていません。上のフィールドでキャラクターを選択してください。",
                MessageType.Warning
            );
            return;
        }

        // 読み込み対象のフォルダパスを自動構築 (character.name を使用)
        string targetFolderPath = $"Assets/Sprites/Portrait/{controller.character.name}";

        // ボタンの前に、どこから読み込むのかをインスペクターに表示
        EditorGUILayout.HelpBox(
            $"以下のフォルダからスプライトを自動で読み込みます:\n{targetFolderPath}",
            MessageType.Info
        );

        // 「Load Sprites from Folder」というラベルのボタンを作成
        if (GUILayout.Button("Load Sprites from Folder"))
        {
            // ボタンが押されたら、構築したパスを渡して読み込み処理を呼び出す
            LoadSpritesFromFolder(controller, targetFolderPath);
        }

        // ボタンの後に、補足説明をインスペクターに表示
        EditorGUILayout.LabelField(
            "※フォルダが存在しない、または画像がない場合はエラーになります。",
            EditorStyles.miniLabel
        );
    }

    /// <summary>
    /// 指定されたフォルダからスプライトを読み込み、リストに登録するメソッド
    /// </summary>
    private void LoadSpritesFromFolder(BasePortraitController controller, string folderPath)
    {
        // フォルダが存在するかチェック
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError(
                $"指定されたフォルダが見つかりません: {folderPath}\n事前にフォルダを作成し、画像を配置してください。"
            );
            return;
        }

        // 指定されたフォルダ内にある、タイプが「Sprite」のアセットのGUID（一意なID）をすべて検索
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });

        // 見つかったアセットをリストに登録する前に、既存のリストをクリア
        controller.portraitSprites.Clear();

        // 見つかった各アセットをSpriteとして読み込み、リストに追加
        foreach (string guid in guids)
        {
            // GUIDからアセットのパスを取得
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            // パスからSpriteアセットを読み込む
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

            if (sprite != null)
            {
                controller.portraitSprites.Add(sprite);
            }
        }

        // 読み込んだ後、名前順に並び替えておくと管理しやすい（任意）
        controller.portraitSprites = controller.portraitSprites.OrderBy(s => s.name).ToList();

        // controllerオブジェクトに変更があったことをUnityに通知し、変更を保存させる
        EditorUtility.SetDirty(controller);

        // 完了メッセージをコンソールに表示
        Debug.Log(
            $"【成功】{folderPath} から {controller.portraitSprites.Count} 個のスプライトをリストに登録しました。"
        );
    }
}
