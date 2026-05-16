#if UNITY_EDITOR
using CriWare.Assets;
using UnityEditor;

// CriAtomSePlayerコンポーネントのインスペクター表示を上書きします
[CustomEditor(typeof(CriAtomSePlayer))]
public class CriAtomSePlayerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // base.OnInspectorGUI() を呼ばないことで、親クラスを含めたすべての項目を描画しません
        // これによりインスペクターには何も表示されなくなります
    }
}
#endif // UNITY_EDITOR

/*#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace CriWare.Assets
{
    /// <summary>
    /// CriAtomSePlayer コンポーネントのカスタムインスペクターです。
    /// インスペクターの見た目を変更し、エディタ上でのプレビュー機能を提供します。
    /// </summary>
    [CustomEditor(typeof(CriAtomSePlayer))]
    public class CriAtomSePlayerEditor : CriAtomSourceBaseEditor
    {
        // プレビュー再生用に一時的に生成するGameObjectの名前
        private const string PreviewObjectName = "[CRIWARE SE Preview Player]";
        
        /// <summary>
        /// キュー参照に関するGUI（インスペクターの上部）を描画します。
        /// </summary>
        protected override void InspectorCueReferenceGUI()
        {
            // シリアライズされたオブジェクトの最新の状態を取得します。
            serializedObject.Update();
            
            // --- インスペクターの表示をカスタマイズ ---
            
            // 共通ACBアセットを表示しますが、編集はできないようにします。
            // これにより、どのACBアセットが使われているかをインスペクターで確認できます。
            GUI.enabled = false; // これ以降に描画されるGUIを操作不可（グレーアウト）にする
            EditorGUILayout.ObjectField("Common SE Acb Asset", CriAtomSePlayer.CommonAcbAsset, typeof(CriAtomAcbAsset), false);
            GUI.enabled = true; // GUIを再び操作可能に戻す

            // Cue Nameの入力フィールドのみを表示します。
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_cueName"), new GUIContent("Cue Name"));
            
            // インスペクターで行われた変更をオブジェクトに適用します。
            serializedObject.ApplyModifiedProperties();
        }
        
        /// <summary>
        /// プレビュー再生用のGUI（Play/Stopボタン）を描画します。
        /// </summary>
        protected override void InspectorPreviewGUI()
        {
            GUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Preview", GUILayout.MaxWidth(EditorGUIUtility.labelWidth - 5));

                // Playボタンが押された時の処理
                if (GUILayout.Button("Play", GUILayout.MaxWidth(60)))
                {
                    // 以前のプレビューが残っていれば停止します。
                    StopPreview();

                    var sourceComponent = (CriAtomSePlayer)source;
                    var commonAcb = CriAtomSePlayer.CommonAcbAsset; // 共通アセットを取得
                    
                    // 共通アセットとキュー名が両方設定されている場合のみ再生を実行します。
                    if(commonAcb != null && !string.IsNullOrEmpty(sourceComponent.cueName))
                    {
                        // プレビュー再生専用の非表示GameObjectをシーンに一時的に生成します。
                        var previewObject = new GameObject(PreviewObjectName);
                        // このオブジェクトがシーンに保存されず、ヒエラルキーにも表示されないように設定します。
                        previewObject.hideFlags = HideFlags.HideAndDontSave;
                        
                        var tempSource = previewObject.AddComponent<CriAtomSePlayer>();
                        // tempSourceは自動で共通アセットを読むため、キュー名の設定だけでOKです。
                        tempSource.cueName = sourceComponent.cueName;
                        tempSource.Play();
                    }
                }
                
                // Stopボタンが押された時の処理
                if (GUILayout.Button("Stop", GUILayout.MaxWidth(60)))
                {
                    StopPreview();
                }
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// プレビュー再生を停止します。
        /// </summary>
        private void StopPreview()
        {
            // シーン内からプレビュー用のオブジェクトを探します。
            var previewObject = GameObject.Find(PreviewObjectName);
            if (previewObject != null)
            {
                // 見つけたら即座に（エディタモードで安全に）破棄します。
                DestroyImmediate(previewObject);
            }
        }
        
        /// <summary>
        /// インスペクターが非表示になるなど、このエディタが無効になったときに呼び出されます。
        /// </summary>
        private void OnDisable()
        {
            // 音が鳴りっぱなしになるのを防ぐため、プレビュー再生を停止します。
            StopPreview();
        }
    }
}

#endif // UNITY_EDITOR
*/
