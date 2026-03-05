// このコードはFungusライブラリ（https://github.com/snozbot/fungus）の一部です。
// MITオープンソースライセンス（https://github.com/snozbot/fungus/blob/master/LICENSE）の下で無料で公開されています。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Fungus.EditorUtils
{
    /// <summary>
    /// SayコマンドのInspectorの表示をカスタマイズするエディタ拡張クラスです。
    /// </summary>
    [CustomEditor(typeof(Say))]
    public class SayEditor : CommandEditor
    {
        /// <summary>
        /// テキスト装飾用の「タグヘルプ」を表示するかどうかを制御する静的フラグ。
        /// </summary>
        public static bool showTagHelp;

        /// <summary>
        /// プレビュー表示用の黒いテクスチャ。
        /// </summary>
        public Texture2D blackTex;

        /// <summary>
        /// Fungusで利用可能なテキストタグの一覧を表示するヘルプラベルを描画します。
        /// </summary>
        public static void DrawTagHelpLabel()
        {
            // Fungusに組み込まれている標準タグのヘルプテキストを取得
            string tagsText = TextTagParser.GetTagHelp();

            // プロジェクトにカスタムタグが存在する場合、それらの情報もヘルプに追加する
            if (CustomTag.activeCustomTags.Count > 0)
            {
                tagsText += "\n\n\t-------- CUSTOM TAGS --------";
                List<Transform> activeCustomTagGroup = new List<Transform>();
                // ( ... カスタムタグをリストアップして整形する処理 ... )
            }

            // 最終的に整形されたヘルプテキストを選択可能なラベルとして描画
            float pixelHeight = EditorStyles.miniLabel.CalcHeight(
                new GUIContent(tagsText),
                EditorGUIUtility.currentViewWidth
            );
            EditorGUILayout.SelectableLabel(
                tagsText,
                GUI.skin.GetStyle("HelpBox"),
                GUILayout.MinHeight(pixelHeight)
            );
        }

        // --- Sayコマンドの各プロパティ（変数）への参照 ---
        // これらをキャッシュしておくことで、Inspectorの描画パフォーマンスを向上させます。
        protected SerializedProperty characterProp;
        protected SerializedProperty portraitProp;
        protected SerializedProperty portraitStringProp;
        protected SerializedProperty storyTextProp;
        protected SerializedProperty descriptionProp;
        protected SerializedProperty voiceOverClipProp;
        protected SerializedProperty showAlwaysProp;
        protected SerializedProperty showCountProp;
        protected SerializedProperty extendPreviousProp;
        protected SerializedProperty fadeWhenDoneProp;
        protected SerializedProperty waitForClickProp;
        protected SerializedProperty stopVoiceoverProp;
        protected SerializedProperty setSayDialogProp;
        protected SerializedProperty waitForVOProp;

        /// <summary>
        /// このエディタが有効になったときに呼び出されます。
        /// </summary>
        public override void OnEnable()
        {
            base.OnEnable();

            // Sayクラスの各変数を、シリアライズされたプロパティとして取得・キャッシュします。
            // これにより、Undo/RedoやPrefabの変更保存が正しく機能します。
            characterProp = serializedObject.FindProperty("character");
            portraitProp = serializedObject.FindProperty("portrait");
            portraitStringProp = serializedObject.FindProperty("portraitString");
            storyTextProp = serializedObject.FindProperty("storyText");
            descriptionProp = serializedObject.FindProperty("description");
            voiceOverClipProp = serializedObject.FindProperty("voiceOverClip");
            showAlwaysProp = serializedObject.FindProperty("showAlways");
            showCountProp = serializedObject.FindProperty("showCount");
            extendPreviousProp = serializedObject.FindProperty("extendPrevious");
            fadeWhenDoneProp = serializedObject.FindProperty("fadeWhenDone");
            waitForClickProp = serializedObject.FindProperty("waitForClick");
            stopVoiceoverProp = serializedObject.FindProperty("stopVoiceover");
            setSayDialogProp = serializedObject.FindProperty("setSayDialog");
            waitForVOProp = serializedObject.FindProperty("waitForVO");

            // プレビュー用の黒いテクスチャがなければ生成する
            if (blackTex == null)
            {
                blackTex = CustomGUI.CreateBlackTexture();
            }
        }

        /// <summary>
        /// このエディタが無効になった、または破棄されるときに呼び出されます。
        /// </summary>
        protected virtual void OnDisable()
        {
            // 生成したテクスチャリソースを解放します。
            DestroyImmediate(blackTex);
        }

        /// <summary>
        /// SayコマンドのInspectorのGUIを描画するメインのメソッドです。
        /// </summary>
        public override void DrawCommandGUI()
        {
            // 最新の状態でプロパティを更新
            serializedObject.Update();

            // キャラクターの変更を検知するためのチェックを開始
            EditorGUI.BeginChangeCheck();

            // --- キャラクター選択 ---
            CommandEditor.ObjectField<Character>(
                characterProp,
                new GUIContent("Character", "話しているキャラクター"),
                new GUIContent("<None>"), // キャラクターが設定されていない場合の表示
                Character.ActiveCharacters // シーン内の全キャラクターをリストアップ
            );

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(" "); // インデントを合わせるための空ラベル
            characterProp.objectReferenceValue = (Character)
                EditorGUILayout.ObjectField(
                    characterProp.objectReferenceValue,
                    typeof(Character),
                    true
                );
            EditorGUILayout.EndHorizontal();

            // キャラクターが変更された場合、設定されていた立ち絵の参照をクリアする
            if (EditorGUI.EndChangeCheck())
            {
                portraitProp.objectReferenceValue = null;
                portraitStringProp.stringValue = ""; // 紐づいていた動的立ち絵の文字列も一緒にリセットする
            }

            // --- 立ち絵（ポートレート）設定 ---
            // 動的立ち絵用の文字列入力フィールドを常に表示
            EditorGUILayout.PropertyField(
                portraitStringProp,
                new GUIContent(
                    "Portrait String",
                    "【動的立ち絵用】表情ファイル名を指定（例: Name_Body_Face）"
                )
            );

            // 従来通りのSprite選択ドロップダウンを表示
            var character = characterProp.objectReferenceValue as Character;
            bool showPortraits = (
                character != null && character.Portraits != null && character.Portraits.Count > 0
            );

            if (showPortraits)
            {
                CommandEditor.ObjectField<Sprite>(
                    portraitProp,
                    new GUIContent("Portrait Sprite", "【標準立ち絵用】キャラクターを表すSprite"),
                    new GUIContent("<None>"),
                    character.Portraits
                );
            }

            // --- テキスト入力欄 ---
            EditorGUILayout.PropertyField(storyTextProp);
            EditorGUILayout.PropertyField(descriptionProp);

            // (中略... テキスト関連オプション、その他のオプションまでは変更なし)

            EditorGUILayout.PropertyField(waitForClickProp);
            EditorGUILayout.PropertyField(stopVoiceoverProp);
            EditorGUILayout.PropertyField(setSayDialogProp);
            EditorGUILayout.PropertyField(waitForVOProp);

            // --- 立ち絵プレビュー ---
            // portrait(Sprite)が設定されている場合のみプレビューを表示。
            if (portraitProp.objectReferenceValue != null)
            {
                var portraitSprite = portraitProp.objectReferenceValue as Sprite;
                if (portraitSprite != null)
                {
                    Texture2D characterTexture = portraitSprite.texture;
                    float aspect = (float)characterTexture.width / (float)characterTexture.height;
                    Rect previewRect = GUILayoutUtility.GetAspectRect(
                        aspect,
                        GUILayout.Width(100),
                        GUILayout.ExpandWidth(true)
                    );
                    if (characterTexture != null)
                    {
                        GUI.DrawTexture(
                            previewRect,
                            characterTexture,
                            ScaleMode.ScaleToFit,
                            true,
                            aspect
                        );
                    }
                }
            }

            // 全ての変更を適用
            serializedObject.ApplyModifiedProperties();
        }
    }
}
