using UnityEngine;
using UnityEditor; // Unityエディタの機能を扱うために必要
using System.Linq; // リストの並び替え(OrderBy)で使用

// HeroinPortraitControllerのInspector表示をカスタマイズするクラス
[CustomEditor(typeof(HeroinPortraitController))]
public class HeroinPortraitControllerEditor : Editor
{
    // InspectorのGUIを描画する際に呼び出されるメソッド
    public override void OnInspectorGUI()
    {
        // まず、デフォルトのInspector表示を描画する
        DrawDefaultInspector();

        //対象のコンポーネント（HeroinPortraitController）のインスタンスを取得
        HeroinPortraitController controller = (HeroinPortraitController)target;

        // ボタンとの間に少しスペースを空けて見やすくする
        EditorGUILayout.Space(10);

        // 「Load Sprites from Folder」というラベルのボタンを作成
        if (GUILayout.Button("Load Sprites from Folder"))
        {
            // ボタンが押されたら、スプライトを読み込む処理を呼び出す
            LoadSpritesFromFolder(controller);
        }
    }

    /// <summary>
    /// 指定されたフォルダからスプライトを読み込み、リストに登録するメソッド
    /// </summary>
    private void LoadSpritesFromFolder(HeroinPortraitController controller)
    {
        // 読み込み対象のフォルダパス（ご自身のプロジェクトに合わせて変更可能）
        const string folderPath = "Assets/Sprites/Portrait/HeroinPortrait";

        // フォルダが存在するかチェック
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError($"指定されたフォルダが見つかりません: {folderPath}");
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
        Debug.Log($"【成功】{folderPath} から {controller.portraitSprites.Count} 個のスプライトをリストに登録しました。");
    }
}