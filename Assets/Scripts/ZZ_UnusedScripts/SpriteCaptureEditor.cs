// using UnityEditor;
// using UnityEngine;

// /// <summary>
// /// SpriteCaptureコンポーネントのInspectorの表示をカスタマイズするエディタ拡張クラス。
// /// </summary>
// [CustomEditor(typeof(SpriteCapture))] // このエディタがどのクラスを対象にするかを指定
// public class SpriteCaptureEditor : Editor
// {
//     /// <summary>
//     /// InspectorのGUIを描画する際にUnityから呼び出されるメソッド。
//     /// </summary>
//     public override void OnInspectorGUI()
//     {
//         // まず、元のInspectorの項目（Target Sprite RendererやRender Camera）を全て表示する
//         DrawDefaultInspector();

//         // 対象のSpriteCaptureスクリプトのインスタンスを取得
//         SpriteCapture capturer = (SpriteCapture)target;

//         // スペースを少し空けて、見た目を整える
//         EditorGUILayout.Space(10);

//         // 機能の説明をヘルプボックスで表示
//         EditorGUILayout.HelpBox(
//             "以下のボタンを押すと、現在設定されているSpriteRendererの見た目をPNGとして保存します。",
//             MessageType.Info
//         );

//         // ボタンを描画する。if文で囲むことで、ボタンが押された瞬間に中身が実行される
//         // GUILayout.Height(40) で、ボタンを大きく押しやすくしています。
//         if (GUILayout.Button("スプライトをキャプチャして保存", GUILayout.Height(40)))
//         {
//             // ボタンが押されたら、CaptureAndSaveSpriteメソッドを呼び出す
//             capturer.CaptureAndSaveSprite();
//         }
//     }
// }