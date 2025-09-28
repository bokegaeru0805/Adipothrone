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
    /// BlockコンポーネントのInspectorの表示をカスタマイズするエディタ拡張クラスです。
    /// Flowchartウィンドウでブロックが選択された際の、コマンドリストの表示や操作を担当します。
    /// </summary>
    [CustomEditor(typeof(Block))]
    public class BlockEditor : Editor
    {
        /// <summary>
        /// 前のGUI更新フレームで要求されたコマンド操作（コピー、ペースト、削除など）を一時的に保持するリスト。
        /// ReorderableListのGUIエラーを避けるため、操作を次のフレームで実行する「遅延実行」に使われます。
        /// </summary>
        public static List<Action> actionList = new List<Action>();

        /// <summary>
        /// 選択中のブロックのデータが古くなった（更新が必要な）場合にtrueになります。
        /// </summary>
        public static bool SelectedBlockDataStale { get; set; }

        // Inspectorで使うアイコンのテクスチャ
        protected Texture2D upIcon;
        protected Texture2D downIcon;
        protected Texture2D addIcon;
        protected Texture2D duplicateIcon;
        protected Texture2D deleteIcon;

        // コマンドリストの表示と操作を管理するためのアダプタークラス。
        private CommandListAdaptor commandListAdaptor;

        // シリアライズされたcommandListプロパティへの参照。Undo/RedoやPrefabの変更保存に必要です。
        private SerializedProperty commandListProperty;

        // イベントハンドラとコマンドの追加ポップアップメニューの表示位置を記憶します。
        private Rect lastEventPopupPos,
            lastCMDpopupPos;

        // このブロックを呼び出す可能性のあるオブジェクトの情報をキャッシュする文字列。
        private string callersString;

        // "Callers"（呼び出し元）の折りたたみメニューが開いているかどうか。
        private bool callersFoldout;

        /// <summary>
        /// このエディタが有効になったときに呼び出されます。
        /// </summary>
        protected virtual void OnEnable()
        {
            // プレイモードを終了する際に、シリアライズされたオブジェクトがnullになることがあるため、
            // 例外が発生しても処理を続行しないように保護しています。
            try
            {
                if (serializedObject == null)
                    return;
            }
            catch (Exception)
            {
                return;
            }

            // Fungus Editor用のリソースからアイコン画像を読み込みます。
            upIcon = FungusEditorResources.Up;
            downIcon = FungusEditorResources.Down;
            addIcon = FungusEditorResources.Add;
            duplicateIcon = FungusEditorResources.Duplicate;
            deleteIcon = FungusEditorResources.Delete;

            // Blockクラスの "commandList" という名前の変数をシリアライズされたプロパティとして取得します。
            commandListProperty = serializedObject.FindProperty("commandList");

            // コマンドリストを描画・操作するためのアダプターを初期化します。
            commandListAdaptor = new CommandListAdaptor(target as Block, commandListProperty);
        }

        /// <summary>
        /// このブロックを呼び出す可能性のあるオブジェクト（IBlockCaller）の情報を検索し、
        /// 文字列としてキャッシュします。毎フレーム検索すると重いため、初回のみ実行します。
        /// </summary>
        protected void CacheCallerString()
        {
            if (!string.IsNullOrEmpty(callersString))
                return;

            var targetBlock = target as Block;

            // シーン全体からIBlockCallerインターフェースを持つコンポーネントを探し、
            // このブロックを呼び出す可能性があるものだけを抽出します。
            var callers = FindObjectsOfType<MonoBehaviour>()
                .Where(x => x is IBlockCaller)
                .Select(x => x as IBlockCaller)
                .Where(x => x.MayCallBlock(targetBlock))
                .Select(x => x.GetLocationIdentifier())
                .ToArray();

            if (callers != null && callers.Length > 0)
                callersString = string.Join("\n", callers); // 見つかった場合は改行で連結
            else
                callersString = "None"; // 見つからなかった場合
        }

        /// <summary>
        /// Inspectorの上部にブロック名を表示・編集するUIを描画します。
        /// </summary>
        public virtual void DrawBlockName(Flowchart flowchart)
        {
            serializedObject.Update(); // 最新の状態でプロパティを更新

            SerializedProperty blockNameProperty = serializedObject.FindProperty("blockName");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Block Name"), EditorStyles.largeLabel);
            EditorGUI.BeginChangeCheck(); // これ以降のGUIに変更があったか監視を開始
            blockNameProperty.stringValue = EditorGUILayout.TextField(
                blockNameProperty.stringValue
            );
            if (EditorGUI.EndChangeCheck()) // もし変更があったら
            {
                // ブロック名がFlowchart内で一意（ユニーク）になるように自動で調整します。
                var block = target as Block;
                string uniqueName = flowchart.GetUniqueBlockKey(
                    blockNameProperty.stringValue,
                    block
                );
                if (uniqueName != block.BlockName)
                {
                    blockNameProperty.stringValue = uniqueName;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties(); // 変更を適用
        }

        /// <summary>
        /// ブロックのInspectorのメインGUIを描画します。
        /// </summary>
        public virtual void DrawBlockGUI(Flowchart flowchart)
        {
            serializedObject.Update();

            var block = target as Block;

            // GUIのレイアウトイベント時に、キューに溜まっていた操作（コピー、ペースト、削除など）を実行します。
            // ReorderableListの仕様上、リストの描画と変更を同じフレームで行うとGUIエラーが発生するため、
            // このように操作を1フレーム遅延させています。
            if (Event.current.type == EventType.Layout)
            {
                foreach (Action action in actionList)
                {
                    if (action != null)
                    {
                        action();
                    }
                }
                actionList.Clear();
            }

            EditorGUI.BeginChangeCheck();

            // このブロックが現在選択されている場合にのみ、詳細設定を表示します。
            if (block == flowchart.SelectedBlock)
            {

                // --- ブロックタイプの選択UI ---
                EditorGUILayout.PropertyField(serializedObject.FindProperty("blockType"));
                
                // ブロックのカスタムカラー設定
                SerializedProperty useCustomTintProp = serializedObject.FindProperty(
                    "useCustomTint"
                );
                SerializedProperty tintProp = serializedObject.FindProperty("tint");
                EditorGUILayout.BeginHorizontal();
                useCustomTintProp.boolValue = GUILayout.Toggle(
                    useCustomTintProp.boolValue,
                    " Custom Tint"
                );
                if (useCustomTintProp.boolValue)
                {
                    EditorGUILayout.PropertyField(tintProp, GUIContent.none);
                }
                EditorGUILayout.EndHorizontal();

                // 説明文の表示
                SerializedProperty descriptionProp = serializedObject.FindProperty("description");
                EditorGUILayout.PropertyField(descriptionProp);

                // 自動選択の抑制設定
                SerializedProperty suppressProp = serializedObject.FindProperty(
                    "suppressAllAutoSelections"
                );
                EditorGUILayout.PropertyField(suppressProp);

                // 呼び出し元（Callers）の折りたたみ表示
                EditorGUI.indentLevel++;
                if (callersFoldout = EditorGUILayout.Foldout(callersFoldout, "Callers"))
                {
                    CacheCallerString();
                    GUI.enabled = false; // 編集不可にする
                    EditorGUILayout.TextArea(callersString);
                    GUI.enabled = true; // 編集不可を解除
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.Space();

                // イベントハンドラ（Execute On Event）のGUIを描画
                DrawEventHandlerGUI(flowchart);

                // コマンドのインデントレベルを更新
                block.UpdateIndentLevels();

                // 各コマンドに親ブロックへの参照が設定されていることを確認
                foreach (var command in block.CommandList)
                {
                    if (command == null) // コマンドがnullの場合は後でリストから削除されます
                    {
                        continue;
                    }
                    command.ParentBlock = block;
                }

                // コマンドリスト本体を描画
                commandListAdaptor.DrawCommandList();

                // 本来であれば EventType.contextClick で右クリックを検知すべきですが、
                // FungusのGUI構造ではうまく動作しないため、ボタンの右クリックを直接検知する回避策をとっています。
                if (Event.current.type == EventType.MouseUp && Event.current.button == 1)
                {
                    ShowContextMenu(); // 右クリックメニューを表示
                    Event.current.Use(); // 他のGUI要素がこのイベントを処理しないようにする
                }

                // テキストフィールドに入力中でない場合にのみ、キーボードショートカットを処理します。
                if (GUIUtility.keyboardControl == 0)
                {
                    Event e = Event.current;

                    // コピー（Ctrl+C）のショートカット
                    if (e.type == EventType.ValidateCommand && e.commandName == "Copy")
                    {
                        if (flowchart.SelectedCommands.Count > 0)
                        {
                            e.Use();
                        }
                    }
                    if (e.type == EventType.ExecuteCommand && e.commandName == "Copy")
                    {
                        actionList.Add(Copy); // 遅延実行リストにコピー処理を追加
                        e.Use();
                    }

                    // カット（Ctrl+X）のショートカット
                    if (e.type == EventType.ValidateCommand && e.commandName == "Cut")
                    {
                        if (flowchart.SelectedCommands.Count > 0)
                        {
                            e.Use();
                        }
                    }
                    if (e.type == EventType.ExecuteCommand && e.commandName == "Cut")
                    {
                        actionList.Add(Cut);
                        e.Use();
                    }

                    // ペースト（Ctrl+V）のショートカット
                    if (e.type == EventType.ValidateCommand && e.commandName == "Paste")
                    {
                        CommandCopyBuffer commandCopyBuffer = CommandCopyBuffer.GetInstance();
                        if (commandCopyBuffer.HasCommands())
                        {
                            e.Use();
                        }
                    }
                    if (e.type == EventType.ExecuteCommand && e.commandName == "Paste")
                    {
                        actionList.Add(Paste);
                        e.Use();
                    }

                    // 複製（Ctrl+D）のショートカット
                    if (e.type == EventType.ValidateCommand && e.commandName == "Duplicate")
                    {
                        if (flowchart.SelectedCommands.Count > 0)
                        {
                            e.Use();
                        }
                    }
                    if (e.type == EventType.ExecuteCommand && e.commandName == "Duplicate")
                    {
                        actionList.Add(Copy);
                        actionList.Add(Paste);
                        e.Use();
                    }

                    // 削除（Delete）のショートカット
                    if (e.type == EventType.ValidateCommand && e.commandName == "Delete")
                    {
                        if (flowchart.SelectedCommands.Count > 0)
                        {
                            e.Use();
                        }
                    }
                    if (e.type == EventType.ExecuteCommand && e.commandName == "Delete")
                    {
                        actionList.Add(Delete);
                        e.Use();
                    }

                    // 全選択（Ctrl+A）のショートカット
                    if (e.type == EventType.ValidateCommand && e.commandName == "SelectAll")
                    {
                        e.Use();
                    }
                    if (e.type == EventType.ExecuteCommand && e.commandName == "SelectAll")
                    {
                        actionList.Add(SelectAll);
                        e.Use();
                    }
                }
            }

            // コマンドのクラスが削除・リネームされた場合などに発生する、リスト内のnullエントリを削除します。
            for (int i = commandListProperty.arraySize - 1; i >= 0; --i)
            {
                SerializedProperty commandProperty = commandListProperty.GetArrayElementAtIndex(i);
                if (commandProperty.objectReferenceValue == null)
                {
                    commandListProperty.DeleteArrayElementAtIndex(i);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                // データが変更されたことを通知
                SelectedBlockDataStale = true;
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// コマンドリストの下部に表示される、追加・削除・複製などのボタンツールバーを描画します。
        /// </summary>
        public virtual void DrawButtonToolbar()
        {
            GUILayout.BeginHorizontal();

            // PageUpキーで前のコマンドを選択するショートカット
            if (
                (Event.current.type == EventType.KeyDown)
                && (Event.current.keyCode == KeyCode.PageUp)
            )
            {
                SelectPrevious();
                GUI.FocusControl("dummycontrol"); // テキストフィールドからフォーカスを外す
                Event.current.Use();
            }
            // PageDownキーで次のコマンドを選択するショートカット
            if (
                (Event.current.type == EventType.KeyDown)
                && (Event.current.keyCode == KeyCode.PageDown)
            )
            {
                SelectNext();
                GUI.FocusControl("dummycontrol");
                Event.current.Use();
            }

            // 上へボタン
            if (GUILayout.Button(upIcon))
            {
                SelectPrevious();
            }

            // 下へボタン
            if (GUILayout.Button(downIcon))
            {
                SelectNext();
            }

            GUILayout.FlexibleSpace(); // ボタンを左右に寄せるためのスペース

            // コマンド追加メニューの表示位置を計算・保持
            var pos = EditorGUILayout.GetControlRect(false, 0, EditorStyles.objectField);
            if (pos.x != 0)
            {
                lastCMDpopupPos = pos;
                lastCMDpopupPos.x += EditorGUIUtility.labelWidth;
                lastCMDpopupPos.y += EditorGUIUtility.singleLineHeight * 2;
            }

            // 追加ボタン
            if (GUILayout.Button(addIcon))
            {
                // 現在のウィンドウサイズに合わせて、コマンド選択メニューの表示サイズを計算
                int h = Screen.height;
                if (EditorWindow.focusedWindow != null)
                    h = (int)EditorWindow.focusedWindow.position.height;
                else if (EditorWindow.mouseOverWindow != null)
                    h = (int)EditorWindow.mouseOverWindow.position.height;

                CommandSelectorPopupWindowContent.ShowCommandMenu(
                    lastCMDpopupPos,
                    "",
                    target as Block,
                    (int)(EditorGUIUtility.currentViewWidth),
                    (int)(h - lastCMDpopupPos.y)
                );
            }

            // 複製ボタン
            if (GUILayout.Button(duplicateIcon))
            {
                Copy();
                Paste();
            }

            // 削除ボタン
            if (GUILayout.Button(deleteIcon))
            {
                Delete();
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// イベントハンドラ（Execute On Event）を選択・設定するためのGUIを描画します。
        /// </summary>
        protected virtual void DrawEventHandlerGUI(Flowchart flowchart)
        {
            Block block = target as Block;
            System.Type currentType = null;
            if (block._EventHandler != null)
            {
                currentType = block._EventHandler.GetType();
            }

            // 現在設定されているイベントハンドラの名前を取得
            string currentHandlerName = "<None>";
            if (currentType != null)
            {
                EventHandlerInfoAttribute info = EventHandlerEditor.GetEventHandlerInfo(
                    currentType
                );
                if (info != null)
                {
                    currentHandlerName = info.EventHandlerName;
                }
            }

            // イベントハンドラ選択メニューの表示位置を計算・保持
            var pos = EditorGUILayout.GetControlRect(true, 0, EditorStyles.objectField);
            if (pos.x != 0)
            {
                lastEventPopupPos = pos;
                lastEventPopupPos.x += EditorGUIUtility.labelWidth;
                lastEventPopupPos.y += EditorGUIUtility.singleLineHeight;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Execute On Event"));
            // ドロップダウンボタンを表示
            if (
                EditorGUILayout.DropdownButton(
                    new GUIContent(currentHandlerName),
                    FocusType.Passive
                )
            )
            {
                // ボタンが押されたら、イベントハンドラ選択ポップアップを表示
                EventSelectorPopupWindowContent.DoEventHandlerPopUp(
                    lastEventPopupPos,
                    currentHandlerName,
                    block,
                    (int)(EditorGUIUtility.currentViewWidth - lastEventPopupPos.x),
                    200
                );
            }
            EditorGUILayout.EndHorizontal();

            // イベントハンドラが設定されている場合、その詳細設定GUIを描画
            if (block._EventHandler != null)
            {
                // 一時的にイベントハンドラ用のエディタを作成して描画
                EventHandlerEditor eventHandlerEditor =
                    Editor.CreateEditor(block._EventHandler) as EventHandlerEditor;
                if (eventHandlerEditor != null)
                {
                    EditorGUI.BeginChangeCheck();
                    eventHandlerEditor.DrawInspectorGUI();
                    if (EditorGUI.EndChangeCheck())
                    {
                        SelectedBlockDataStale = true;
                    }
                    DestroyImmediate(eventHandlerEditor); // 描画が終わったらすぐに破棄
                }
            }
        }

        /// <summary>
        /// Flowchart内のBlockをリスト表示するドロップダウンメニューを作成します。
        /// 他のエディタスクリプト（CallEditorなど）から利用されるヘルパーメソッドです。
        /// </summary>
        public static void BlockField(
            SerializedProperty property,
            GUIContent label,
            GUIContent nullLabel,
            Flowchart flowchart
        )
        {
            if (flowchart == null)
            {
                return;
            }

            var block = property.objectReferenceValue as Block;

            // Flowchart内のBlockを名前順でリストアップ
            List<GUIContent> blockNames = new List<GUIContent>();
            int selectedIndex = 0;
            blockNames.Add(nullLabel);
            var blocks = flowchart.GetComponents<Block>();
            blocks = blocks.OrderBy(x => x.BlockName).ToArray();

            for (int i = 0; i < blocks.Length; ++i)
            {
                blockNames.Add(new GUIContent(blocks[i].BlockName));

                if (block == blocks[i])
                {
                    selectedIndex = i + 1;
                }
            }

            // ドロップダウン（Popup）を表示
            selectedIndex = EditorGUILayout.Popup(label, selectedIndex, blockNames.ToArray());
            if (selectedIndex == 0)
            {
                block = null; // "None"が選択された
            }
            else
            {
                block = blocks[selectedIndex - 1];
            }

            property.objectReferenceValue = block;
        }

        /// <summary>
        /// Flowchart内のBlockをリスト表示するドロップダウンメニューを作成します。（Rect指定版）
        /// </summary>
        public static Block BlockField(
            Rect position,
            GUIContent nullLabel,
            Flowchart flowchart,
            Block block
        )
        {
            if (flowchart == null)
            {
                return null;
            }

            Block result = block;

            // Flowchart内のBlockを名前順でリストアップ
            List<GUIContent> blockNames = new List<GUIContent>();
            int selectedIndex = 0;
            blockNames.Add(nullLabel);
            Block[] blocks = flowchart.GetComponents<Block>();
            blocks = blocks.OrderBy(x => x.BlockName).ToArray();

            for (int i = 0; i < blocks.Length; ++i)
            {
                blockNames.Add(new GUIContent(blocks[i].BlockName));

                if (block == blocks[i])
                {
                    selectedIndex = i + 1;
                }
            }

            selectedIndex = EditorGUI.Popup(position, selectedIndex, blockNames.ToArray());
            if (selectedIndex == 0)
            {
                result = null; // "None"が選択された
            }
            else
            {
                result = blocks[selectedIndex - 1];
            }

            return result;
        }

        /// <summary>
        /// 右クリックで表示されるコンテキストメニューを作成・表示します。
        /// </summary>
        public virtual void ShowContextMenu()
        {
            var block = target as Block;
            var flowchart = (Flowchart)block.GetFlowchart();

            if (flowchart == null)
            {
                return;
            }

            // 現在の選択状態に応じて、メニュー項目を有効化/無効化する
            bool showCut = false;
            bool showCopy = false;
            bool showDelete = false;
            bool showPaste = false;
            bool showPlay = false;

            if (flowchart.SelectedCommands.Count > 0)
            {
                showCut = true;
                showCopy = true;
                showDelete = true;
                // 「ここから再生」は1つのコマンドが選択されている時だけ有効
                if (flowchart.SelectedCommands.Count == 1 && Application.isPlaying)
                {
                    showPlay = true;
                }
            }

            // コピー用のバッファにコマンドがあれば、ペーストを有効化
            CommandCopyBuffer commandCopyBuffer = CommandCopyBuffer.GetInstance();
            if (commandCopyBuffer.HasCommands())
            {
                showPaste = true;
            }

            GenericMenu commandMenu = new GenericMenu();

            // 各メニュー項目を追加
            if (showCut)
            {
                commandMenu.AddItem(new GUIContent("Cut"), false, Cut);
            }
            else
            {
                commandMenu.AddDisabledItem(new GUIContent("Cut"));
            }

            if (showCopy)
            {
                commandMenu.AddItem(new GUIContent("Copy"), false, Copy);
            }
            else
            {
                commandMenu.AddDisabledItem(new GUIContent("Copy"));
            }

            if (showPaste)
            {
                commandMenu.AddItem(new GUIContent("Paste"), false, Paste);
            }
            else
            {
                commandMenu.AddDisabledItem(new GUIContent("Paste"));
            }

            if (showDelete)
            {
                commandMenu.AddItem(new GUIContent("Delete"), false, Delete);
            }
            else
            {
                commandMenu.AddDisabledItem(new GUIContent("Delete"));
            }

            // プレイモード中のみ表示されるメニュー
            if (showPlay)
            {
                commandMenu.AddItem(new GUIContent("Play from selected"), false, PlayCommand);
                commandMenu.AddItem(new GUIContent("Stop all and play"), false, StopAllPlayCommand);
            }

            commandMenu.AddSeparator("");

            commandMenu.AddItem(new GUIContent("Select All"), false, SelectAll);
            commandMenu.AddItem(new GUIContent("Select None"), false, SelectNone);

            commandMenu.ShowAsContext();
        }

        // --- 以下、コンテキストメニューやショートカットで呼び出される各処理 ---

        protected void SelectAll()
        {
            var block = target as Block;
            var flowchart = (Flowchart)block.GetFlowchart();

            if (flowchart == null || flowchart.SelectedBlock == null)
            {
                return;
            }

            flowchart.ClearSelectedCommands();
            Undo.RecordObject(flowchart, "Select All"); // Undo（元に戻す）履歴に記録
            foreach (Command command in flowchart.SelectedBlock.CommandList)
            {
                flowchart.AddSelectedCommand(command);
            }

            Repaint(); // Inspectorを再描画
        }

        protected void SelectNone()
        {
            var block = target as Block;
            var flowchart = (Flowchart)block.GetFlowchart();

            if (flowchart == null || flowchart.SelectedBlock == null)
            {
                return;
            }

            Undo.RecordObject(flowchart, "Select None");
            flowchart.ClearSelectedCommands();

            Repaint();
        }

        protected void Cut()
        {
            Copy();
            Delete();
        }

        protected void Copy()
        {
            var block = target as Block;
            var flowchart = (Flowchart)block.GetFlowchart();

            if (flowchart == null || flowchart.SelectedBlock == null)
            {
                return;
            }

            CommandCopyBuffer commandCopyBuffer = CommandCopyBuffer.GetInstance();
            commandCopyBuffer.Clear();

            // 選択された各コマンドをループ処理
            foreach (Command command in flowchart.SelectedBlock.CommandList)
            {
                if (flowchart.SelectedCommands.Contains(command))
                {
                    // リフレクションを使って、コマンドの全シリアライズ対象フィールドをディープコピーする
                    var type = command.GetType();
                    Command newCommand =
                        Undo.AddComponent(commandCopyBuffer.gameObject, type) as Command;
                    var fields = type.GetFields(
                        BindingFlags.Instance
                            | BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.FlattenHierarchy
                    );
                    foreach (var field in fields)
                    {
                        bool copy = field.IsPublic;

                        var attributes = field.GetCustomAttributes(typeof(SerializeField), true);
                        if (attributes.Length > 0)
                        {
                            copy = true;
                        }

                        if (copy)
                        {
                            field.SetValue(newCommand, field.GetValue(command));
                        }
                    }
                }
            }
        }

        protected void Paste()
        {
            var block = target as Block;
            var flowchart = (Flowchart)block.GetFlowchart();

            if (flowchart == null || flowchart.SelectedBlock == null)
            {
                return;
            }

            CommandCopyBuffer commandCopyBuffer = CommandCopyBuffer.GetInstance();

            // ペーストする位置を決定（最後に選択したコマンドの後、またはリストの末尾）
            int pasteIndex = flowchart.SelectedBlock.CommandList.Count;
            if (flowchart.SelectedCommands.Count > 0)
            {
                for (int i = 0; i < flowchart.SelectedBlock.CommandList.Count; ++i)
                {
                    Command command = flowchart.SelectedBlock.CommandList[i];
                    foreach (Command selectedCommand in flowchart.SelectedCommands)
                    {
                        if (command == selectedCommand)
                        {
                            pasteIndex = i + 1;
                        }
                    }
                }
            }

            // バッファ内の各コマンドをループ処理
            foreach (Command command in commandCopyBuffer.GetCommands())
            {
                // Unityエディタ標準のコピー＆ペースト機能を利用して、プロパティをディープコピーする
                if (ComponentUtility.CopyComponent(command))
                {
                    if (ComponentUtility.PasteComponentAsNew(flowchart.gameObject))
                    {
                        Command[] commands = flowchart.GetComponents<Command>();
                        Command pastedCommand = commands.Last<Command>();
                        if (pastedCommand != null)
                        {
                            pastedCommand.ItemId = flowchart.NextItemId();
                            flowchart.SelectedBlock.CommandList.Insert(pasteIndex++, pastedCommand);
                        }
                    }

                    // ユーザーが手動で他のオブジェクトにペーストするのを防ぐ
                    ComponentUtility.CopyComponent(flowchart.transform);
                }
            }

            // Prefabインスタンスに変更を記録
            PrefabUtility.RecordPrefabInstancePropertyModifications(block);

            Repaint();
        }

        protected void Delete()
        {
            var block = target as Block;
            var flowchart = (Flowchart)block.GetFlowchart();

            if (flowchart == null || flowchart.SelectedBlock == null)
            {
                return;
            }
            int lastSelectedIndex = 0;
            // リストを末尾からループして、安全に要素を削除
            for (int i = flowchart.SelectedBlock.CommandList.Count - 1; i >= 0; --i)
            {
                Command command = flowchart.SelectedBlock.CommandList[i];
                foreach (Command selectedCommand in flowchart.SelectedCommands)
                {
                    if (command == selectedCommand)
                    {
                        command.OnCommandRemoved(block);

                        // Undo履歴のために、Destroyとリストからの削除を別々に記録
                        Undo.DestroyObjectImmediate(command);
                        Undo.RecordObject((Block)flowchart.SelectedBlock, "Delete");
                        flowchart.SelectedBlock.CommandList.RemoveAt(i);

                        lastSelectedIndex = i;
                        break;
                    }
                }
            }

            Undo.RecordObject(flowchart, "Delete");
            flowchart.ClearSelectedCommands();

            // 削除後、次のコマンドを自動で選択状態にする
            if (lastSelectedIndex < flowchart.SelectedBlock.CommandList.Count)
            {
                var nextCommand = flowchart.SelectedBlock.CommandList[lastSelectedIndex];
                block.GetFlowchart().AddSelectedCommand(nextCommand);
            }

            Repaint();
        }

        /// <summary>
        /// 選択中のコマンドからBlockの実行を開始します。
        /// </summary>
        protected void PlayCommand()
        {
            var targetBlock = target as Block;
            var flowchart = (Flowchart)targetBlock.GetFlowchart();
            Command command = flowchart.SelectedCommands[0];
            if (targetBlock.IsExecuting())
            {
                // Blockが既に実行中の場合、一度停止してから少し待って、
                // 選択したコマンドから再開する
                targetBlock.Stop();
                flowchart.StartCoroutine(
                    RunBlock(flowchart, targetBlock, command.CommandIndex, 0.2f)
                );
            }
            else
            {
                // Blockが実行中でなければ、すぐに実行を開始
                flowchart.ExecuteBlock(targetBlock, command.CommandIndex);
            }
        }

        /// <summary>
        /// 全てのBlockを停止してから、選択中のコマンドから実行を開始します。
        /// </summary>
        protected void StopAllPlayCommand()
        {
            var targetBlock = target as Block;
            var flowchart = (Flowchart)targetBlock.GetFlowchart();
            Command command = flowchart.SelectedCommands[0];

            flowchart.StopAllBlocks();
            flowchart.StartCoroutine(RunBlock(flowchart, targetBlock, command.CommandIndex, 0.2f));
        }

        /// <summary>
        /// 指定された遅延時間後にBlockを実行するヘルパーコルーチン。
        /// </summary>
        protected IEnumerator RunBlock(
            Flowchart flowchart,
            Block targetBlock,
            int commandIndex,
            float delay
        )
        {
            yield return new WaitForSecondsRealtime(delay);
            flowchart.ExecuteBlock(targetBlock, commandIndex);
        }

        /// <summary>
        /// 一つ前のコマンドを選択状態にします。
        /// </summary>
        protected void SelectPrevious()
        {
            var block = target as Block;
            var flowchart = (Flowchart)block.GetFlowchart();

            int firstSelectedIndex = flowchart.SelectedBlock.CommandList.Count;
            bool firstSelectedCommandFound = false;
            if (flowchart.SelectedCommands.Count > 0)
            {
                // 現在選択されているコマンドのうち、最も上にあるものを探す
                for (int i = 0; i < flowchart.SelectedBlock.CommandList.Count; i++)
                {
                    Command commandInBlock = flowchart.SelectedBlock.CommandList[i];
                    foreach (Command selectedCommand in flowchart.SelectedCommands)
                    {
                        if (commandInBlock == selectedCommand)
                        {
                            if (!firstSelectedCommandFound)
                            {
                                firstSelectedIndex = i;
                                firstSelectedCommandFound = true;
                                break;
                            }
                        }
                    }
                    if (firstSelectedCommandFound)
                    {
                        break;
                    }
                }
            }
            // 最も上のコマンドがリストの先頭でなければ、その一つ上を選択
            if (firstSelectedIndex > 0)
            {
                flowchart.ClearSelectedCommands();
                flowchart.AddSelectedCommand(
                    flowchart.SelectedBlock.CommandList[firstSelectedIndex - 1]
                );
            }

            Repaint();
        }

        /// <summary>
        /// 一つ後のコマンドを選択状態にします。
        /// </summary>
        protected void SelectNext()
        {
            var block = target as Block;
            var flowchart = (Flowchart)block.GetFlowchart();

            int lastSelectedIndex = -1;
            if (flowchart.SelectedCommands.Count > 0)
            {
                // 現在選択されているコマンドのうち、最も下にあるものを探す
                for (int i = 0; i < flowchart.SelectedBlock.CommandList.Count; i++)
                {
                    Command commandInBlock = flowchart.SelectedBlock.CommandList[i];
                    foreach (Command selectedCommand in flowchart.SelectedCommands)
                    {
                        if (commandInBlock == selectedCommand)
                        {
                            lastSelectedIndex = i;
                        }
                    }
                }
            }
            // 最も下のコマンドがリストの末尾でなければ、その一つ下を選択
            if (lastSelectedIndex < flowchart.SelectedBlock.CommandList.Count - 1)
            {
                flowchart.ClearSelectedCommands();
                flowchart.AddSelectedCommand(
                    flowchart.SelectedBlock.CommandList[lastSelectedIndex + 1]
                );
            }

            Repaint();
        }

        /// <summary>
        /// コマンド追加メニューに表示するための、フィルタリングおよび優先度処理済みのコマンド情報リストを返します。
        /// </summary>
        public static List<
            KeyValuePair<System.Type, CommandInfoAttribute>
        > GetFilteredCommandInfoAttribute(List<System.Type> menuTypes)
        {
            Dictionary<string, KeyValuePair<System.Type, CommandInfoAttribute>> filteredAttributes =
                new Dictionary<string, KeyValuePair<System.Type, CommandInfoAttribute>>();

            foreach (System.Type type in menuTypes)
            {
                object[] attributes = type.GetCustomAttributes(false);
                foreach (object obj in attributes)
                {
                    CommandInfoAttribute infoAttr = obj as CommandInfoAttribute;
                    if (infoAttr != null)
                    {
                        string dictionaryName = string.Format(
                            "{0}/{1}",
                            infoAttr.Category,
                            infoAttr.CommandName
                        );

                        // 同じ名前のコマンドが複数存在する場合、Priorityが高い方を優先する
                        int existingItemPriority = -1;
                        if (filteredAttributes.ContainsKey(dictionaryName))
                        {
                            existingItemPriority = filteredAttributes[dictionaryName]
                                .Value
                                .Priority;
                        }

                        if (infoAttr.Priority > existingItemPriority)
                        {
                            KeyValuePair<System.Type, CommandInfoAttribute> keyValuePair =
                                new KeyValuePair<System.Type, CommandInfoAttribute>(type, infoAttr);
                            filteredAttributes[dictionaryName] = keyValuePair;
                        }
                    }
                }
            }
            return filteredAttributes.Values.ToList<
                KeyValuePair<System.Type, CommandInfoAttribute>
            >();
        }

        /// <summary>
        /// コマンド情報リストをカテゴリ名→コマンド名の順でソートするための比較デリゲート。
        /// </summary>
        public static int CompareCommandAttributes(
            KeyValuePair<System.Type, CommandInfoAttribute> x,
            KeyValuePair<System.Type, CommandInfoAttribute> y
        )
        {
            int compare = (x.Value.Category.CompareTo(y.Value.Category));
            if (compare == 0)
            {
                compare = (x.Value.CommandName.CompareTo(y.Value.CommandName));
            }
            return compare;
        }
    }
}
