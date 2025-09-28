// このコードはFungusライブラリ（https://github.com/snozbot/fungus）の一部です。
// MITオープンソースライセンス（https://github.com/snozbot/fungus/blob/master/LICENSE）の下で無料で公開されています。

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Fungus
{
    /// <summary>
    /// Blockの実行状態を定義します。
    /// </summary>
    public enum ExecutionState
    {
        /// <summary> コマンドを実行していない待機状態 </summary>
        Idle,

        /// <summary> コマンドを実行中の状態 </summary>
        Executing,
    }

    /// <summary>
    /// ブロックの種類を定義します。エディタでの色分けなどに使用します。
    /// </summary>
    public enum BlockType
    {
        [Tooltip("通常のブロックタイプ")]
        Default = 0,

        [Tooltip("物語の主要な流れを示すストーリーブロック")]
        Story = 1,

        [Tooltip("NPCとの会話に関連するブロック")]
        NPC = 2,
    }

    /// <summary>
    /// Fungusのコマンドを順番に格納するためのコンテナ（入れ物）です。
    /// Flowchartウィンドウに表示される各「ブロック」の実体となります。
    /// </summary>
    [ExecuteInEditMode] // この属性により、Unityエディタの非再生中でもスクリプトの一部が動作します。
    [RequireComponent(typeof(Flowchart))] // このコンポーネントは、必ずFlowchartコンポーネントと同じGameObjectにアタッチされている必要があります。
    [AddComponentMenu("")] // 「Component」メニューに表示されないようにします。（BlockはFlowchartウィンドウから生成するため）
    public class Block : Node
    {
        [SerializeField]
        protected int itemId = -1; // Flowchart内で一意に識別するためのID。-1は無効なIDを示します。

        [Tooltip("ブロックの種類を設定します。エディタでの色分けなどに使用されます。")]
        [SerializeField]
        protected BlockType blockType = BlockType.Default;

        [FormerlySerializedAs("sequenceName")] // 以前のバージョンとの互換性のため、古い変数名("sequenceName")からでもデータを読み込めるようにする属性です。
        [Tooltip("Flowchartウィンドウに表示されるブロックの名前です。")]
        [SerializeField]
        protected string blockName = "New Block";

        [TextArea(2, 5)]
        [Tooltip("ブロックノードの下に表示される説明文です。開発者向けのメモとして使います。")]
        [SerializeField]
        protected string description = "";

        [Tooltip(
            "設定した場合、特定のイベント発生時にこのブロックを実行するイベントハンドラです。"
        )]
        [SerializeField]
        protected EventHandler eventHandler;

        // このブロックが保持するコマンドのリスト。Inspector上で順番に並んでいます。
        [SerializeField]
        protected List<Command> commandList = new List<Command>();

        // 現在の実行状態（待機中か、実行中か）を保持します。
        protected ExecutionState executionState;

        // 現在実行中のコマンドへの参照。実行中でなければnullになります。
        protected Command activeCommand;

        // 実行が完了したときに呼び出されるコールバック（Action）を一時的に保持します。
        protected Action lastOnCompleteAction;

        /// <summary>
        /// 現在のコマンドが実行される「前」に実行されていたコマンドのインデックス番号。
        /// Ifコマンドなどで前のコマンドを参照するために使われます。
        /// -1の場合は、前に実行されたコマンドがないことを示します。
        /// </summary>
        protected int previousActiveCommandIndex = -1;

        /// <summary>
        /// 直前に実行されたコマンドのインデックス番号を取得します。
        /// </summary>
        public int PreviousActiveCommandIndex
        {
            get { return previousActiveCommandIndex; }
        }

        // 次にジャンプ（移動）するコマンドのインデックス番号。-1はジャンプしないことを意味します。
        // Continueメソッドなどによって設定されます。
        protected int jumpToCommandIndex = -1;

        // このブロックが実行された回数をカウントします。
        protected int executionCount;

        // 実行に必要な情報（各コマンドのインデックスなど）が設定済みかどうかを示すフラグ。
        protected bool executionInfoSet = false;

        /// <summary>
        /// trueに設定すると、次にこのブロックが実行される際に、
        /// Flowchartウィンドウでこのブロックが自動的に選択されるのを抑制します。
        /// 主にイベントハンドラによって使用され、エディタ上でのみ影響します。
        /// </summary>
        public bool SuppressNextAutoSelection { get; set; }

        // このブロックが実行される際に、常に自動選択を抑制するかどうか。
        [SerializeField]
        bool suppressAllAutoSelections = false;

        /// <summary>
        /// オブジェクトが生成またはロードされたときに呼び出されます。
        /// </summary>
        protected virtual void Awake()
        {
            // 実行時情報を設定します。
            SetExecutionInfo();
        }

        /// <summary>
        /// 実行制御に使われるコマンドのメタデータ（付随情報）を設定します。
        /// </summary>
        protected virtual void SetExecutionInfo()
        {
            // リスト内の各コマンドに、親であるこのブロックへの参照と、
            // リスト内でのインデックス番号を教えます。
            int index = 0;
            for (int i = 0; i < commandList.Count; i++)
            {
                var command = commandList[i];
                if (command == null)
                {
                    continue;
                }
                command.ParentBlock = this;
                command.CommandIndex = index++;
            }

            // 全てのコマンドが正しいインデントレベル（字下げ）になっているか確認・更新します。
            // 通常はエディタ上で行われますが、実行時に動的にコマンドが追加された場合などに備えて、ここでも実行します。
            UpdateIndentLevels();

            executionInfoSet = true;
        }

#if UNITY_EDITOR
        // Unityエディタ上では、再生中にユーザーがコマンドの順序を変更する可能性があるため、
        // 毎フレーム、コマンドのインデックス番号を更新し続けます。
        // ゲームのビルド版ではこの処理は不要なため、コンパイルから除外されます。
        protected virtual void Update()
        {
            int index = 0;
            for (int i = 0; i < commandList.Count; i++)
            {
                var command = commandList[i];
                if (command == null) // nullの項目は後で自動的に削除されます
                {
                    continue;
                }
                command.CommandIndex = index++;
            }
        }
#endif

        // Flowchartウィンドウの描画を高速化するための、エディタ専用の状態キャッシュ
        public bool IsSelected { get; set; } // このブロックが選択されているかどうかのキャッシュ

        public enum FilteredState
        {
            Full,
            Partial,
            None,
        } // フィルタリング状態の定義

        public FilteredState FilterState { get; set; } // フィルタリングされているかどうかのキャッシュ
        public bool IsControlSelected { get; set; } // フロー制御コマンド（Ifなど）の一部として選択されているかのキャッシュ
        #region Public members (他のスクリプトからアクセス可能な公開メンバー)

        /// <summary>
        /// このブロックの種類（Default, Storyなど）を取得します。
        /// </summary>
        public virtual BlockType TypeOfBlock
        {
            get { return blockType; }
        }

        /// <summary>
        /// Blockの現在の実行状態（待機中か、実行中か）。
        /// </summary>
        public virtual ExecutionState State
        {
            get { return executionState; }
        }

        /// <summary>
        /// Flowchart内でのBlockの一意なID。
        /// </summary>
        public virtual int ItemId
        {
            get { return itemId; }
            set { itemId = value; }
        }

        /// <summary>
        /// Flowchartウィンドウに表示されるブロックの名前。
        /// </summary>
        public virtual string BlockName
        {
            get { return blockName; }
            set { blockName = value; }
        }

        /// <summary>
        /// ブロックノードの下に表示される説明文。
        /// </summary>
        public virtual string Description
        {
            get { return description; }
        }

        /// <summary>
        /// 特定のイベント発生時にこのブロックを実行するイベントハンドラ。
        /// </summary>
        public virtual EventHandler _EventHandler
        {
            get { return eventHandler; }
            set { eventHandler = value; }
        }

        /// <summary>
        /// 現在実行中のコマンド。実行中でなければnullです。
        /// </summary>
        public virtual Command ActiveCommand
        {
            get { return activeCommand; }
        }

        /// <summary>
        /// Blockの実行中アイコンをフェードさせるためのタイマー。
        /// </summary>
        public virtual float ExecutingIconTimer { get; set; }

        /// <summary>
        /// このブロックに含まれるコマンドのリスト。
        /// </summary>
        public virtual List<Command> CommandList
        {
            get { return commandList; }
        }

        /// <summary>
        /// 次に実行するコマンドのインデックス番号を外部から設定します。
        /// 主にジャンプ系のコマンドで使われます。
        /// </summary>
        public virtual int JumpToCommandIndex
        {
            set { jumpToCommandIndex = value; }
        }

        /// <summary>
        /// このBlockが所属する親のFlowchartを返します。
        /// </summary>
        public virtual Flowchart GetFlowchart()
        {
            return GetComponent<Flowchart>();
        }

        /// <summary>
        /// このBlockが現在コマンドを実行中かどうかを返します。
        /// </summary>
        public virtual bool IsExecuting()
        {
            return (executionState == ExecutionState.Executing);
        }

        /// <summary>
        /// このBlockが実行された回数を返します。
        /// </summary>
        public virtual int GetExecutionCount()
        {
            return executionCount;
        }

        /// <summary>
        /// このBlock内の全コマンドを実行するコルーチンを開始します。
        /// 各Blockは同時に1つしか実行できません。
        /// </summary>
        public virtual void StartExecution()
        {
            StartCoroutine(Execute());
        }

        /// <summary>
        /// このBlock内の全コマンドを順番に実行するコルーチン。
        /// 各Blockは同時に1つしか実行できません。
        /// </summary>
        /// <param name="commandIndex">実行を開始するコマンドのインデックス番号</param>
        /// <param name="onComplete">全てのコマンドの実行が完了したときに呼び出される処理</param>
        public virtual IEnumerator Execute(int commandIndex = 0, Action onComplete = null)
        {
            // すでに実行中の場合は、警告を出して処理を中断
            if (executionState != ExecutionState.Idle)
            {
                Debug.LogWarning(BlockName + " はすでに実行中のため、新たに実行できません。");
                yield break;
            }

            // 完了時コールバックを保存
            lastOnCompleteAction = onComplete;

            // 実行時情報が未設定なら設定する
            if (!executionInfoSet)
            {
                SetExecutionInfo();
            }

            executionCount++;
            var executionCountAtStart = executionCount; // 実行開始時のカウントを記録

            var flowchart = GetFlowchart();
            executionState = ExecutionState.Executing;
            BlockSignals.DoBlockStart(this); // ブロック開始のシグナル（イベント）を発行

            bool suppressSelectionChanges = false;

#if UNITY_EDITOR
            // エディタ上でのみ、実行中のブロックと最初のコマンドを自動選択する
            if (suppressAllAutoSelections || SuppressNextAutoSelection)
            {
                SuppressNextAutoSelection = false;
                suppressSelectionChanges = true;
            }
            else
            {
                flowchart.SelectedBlock = this;
                if (commandList.Count > 0)
                {
                    flowchart.ClearSelectedCommands();
                    flowchart.AddSelectedCommand(commandList[0]);
                }
            }
#endif

            jumpToCommandIndex = commandIndex;

            int i = 0;
            // メインの実行ループ
            while (true)
            {
                // 実行中のコマンドがContinue()を呼び出すと jumpToCommandIndex が設定される
                if (jumpToCommandIndex > -1)
                {
                    i = jumpToCommandIndex;
                    jumpToCommandIndex = -1;
                }

                // 無効化されているコマンド、コメント、ラベルはスキップする
                while (
                    i < commandList.Count
                    && (
                        !commandList[i].enabled
                        || commandList[i].GetType() == typeof(Comment)
                        || commandList[i].GetType() == typeof(Label)
                    )
                )
                {
                    i = commandList[i].CommandIndex + 1;
                }

                // 実行するコマンドがもうなければループを抜ける
                if (i >= commandList.Count)
                {
                    break;
                }

                // 直前に実行されたコマンドのインデックスを保存（If, Else If などで必要）
                if (activeCommand == null)
                {
                    previousActiveCommandIndex = -1;
                }
                else
                {
                    previousActiveCommandIndex = activeCommand.CommandIndex;
                }

                var command = commandList[i];
                activeCommand = command; // 現在実行中のコマンドとして設定

                if (flowchart.IsActive() && !suppressSelectionChanges)
                {
                    // 特定の状況で、実行中のコマンドをエディタ上で自動選択する
                    if (
                        (flowchart.SelectedCommands.Count == 0 && i == 0)
                        || (
                            flowchart.SelectedCommands.Count == 1
                            && flowchart.SelectedCommands[0].CommandIndex
                                == previousActiveCommandIndex
                        )
                    )
                    {
                        flowchart.ClearSelectedCommands();
                        flowchart.AddSelectedCommand(commandList[i]);
                    }
                }

                command.IsExecuting = true;
                // 実行中アイコンのタイマーを設定。コマンドがすぐに終了する場合にも備える。
                command.ExecutingIconTimer =
                    Time.realtimeSinceStartup + FungusConstants.ExecutingIconFadeTime;
                BlockSignals.DoCommandExecute(this, command, i, commandList.Count); // コマンド実行のシグナルを発行
#if UNITY_EDITOR
                // エディタ上ではtry-catchで囲み、エラー発生時に詳細な情報をログに出力する
                try
                {
                    command.Execute();
                }
                catch (Exception)
                {
                    Debug.LogError(
                        "エラーが発生したコマンドの場所: " + command.GetLocationIdentifier()
                    );
                    throw; // エラーを再スロー
                }
#else
                // ビルド版ではtry-catchのオーバーヘッドを避ける
                command.Execute();
#endif

                // 実行中のコマンドがContinue()を呼び出し、jumpToCommandIndexが設定されるまで待機
                while (jumpToCommandIndex == -1)
                {
                    yield return null;
                }

#if UNITY_EDITOR
                // デバッグ用に、コマンド間のステップ実行に一時停止時間を設ける機能
                if (flowchart.StepPause > 0f)
                {
                    yield return new WaitForSecondsRealtime(flowchart.StepPause);
                }
#endif

                command.IsExecuting = false;
            }

            // 実行が完了し、かつ途中で停止されていない場合
            if (State == ExecutionState.Executing && executionCountAtStart == executionCount)
            {
                ReturnToIdle(); // 待機状態に戻す
            }
        }

        /// <summary>
        /// ブロックの実行状態を待機中（Idle）に戻し、後処理を行います。
        /// </summary>
        private void ReturnToIdle()
        {
            executionState = ExecutionState.Idle;
            activeCommand = null;
            BlockSignals.DoBlockEnd(this); // ブロック終了のシグナルを発行

            if (lastOnCompleteAction != null)
            {
                lastOnCompleteAction();
            }
            lastOnCompleteAction = null;
        }

        /// <summary>
        /// このBlockで実行中のコマンドを停止します。
        /// </summary>
        public virtual void Stop()
        {
            // 実行中のコマンドがあれば、即座に停止処理を呼び出す
            if (activeCommand != null)
            {
                activeCommand.IsExecuting = false;
                activeCommand.OnStopExecuting();
            }

            // 実行ループを次のフレームで強制的に終了させる
            jumpToCommandIndex = int.MaxValue;

            // 次のフレームを待たずに、このフレームで即座に待機状態に戻す
            ReturnToIdle();
        }

        /// <summary>
        /// このBlockに接続されている全てのBlockのリストを返します。
        /// </summary>
        public virtual List<Block> GetConnectedBlocks()
        {
            var connectedBlocks = new List<Block>();
            GetConnectedBlocks(ref connectedBlocks);
            return connectedBlocks;
        }

        /// <summary>
        /// 接続されているBlockを再帰的に探索し、リストに追加します。
        /// </summary>
        public virtual void GetConnectedBlocks(ref List<Block> connectedBlocks)
        {
            for (int i = 0; i < commandList.Count; i++)
            {
                var command = commandList[i];
                if (command != null)
                {
                    command.GetConnectedBlocks(ref connectedBlocks);
                }
            }
        }

        /// <summary>
        /// 直前に実行されたコマンドの型（クラス名）を返します。
        /// </summary>
        public virtual System.Type GetPreviousActiveCommandType()
        {
            if (previousActiveCommandIndex >= 0 && previousActiveCommandIndex < commandList.Count)
            {
                return commandList[previousActiveCommandIndex].GetType();
            }

            return null;
        }

        /// <summary>
        /// 直前に実行されたコマンドのインデントレベルを返します。
        /// </summary>
        public virtual int GetPreviousActiveCommandIndent()
        {
            if (previousActiveCommandIndex >= 0 && previousActiveCommandIndex < commandList.Count)
            {
                return commandList[previousActiveCommandIndex].IndentLevel;
            }

            return -1;
        }

        /// <summary>
        /// 直前に実行されたコマンドのインスタンスを返します。
        /// </summary>
        public virtual Command GetPreviousActiveCommand()
        {
            if (previousActiveCommandIndex >= 0 && previousActiveCommandIndex < commandList.Count)
            {
                return commandList[previousActiveCommandIndex];
            }

            return null;
        }

        /// <summary>
        /// リスト内の全コマンドのインデントレベルを再計算します。
        /// If, Else, Loopなどのブロック構造を正しく表示するために使われます。
        /// </summary>
        public virtual void UpdateIndentLevels()
        {
            int indentLevel = 0;
            for (int i = 0; i < commandList.Count; i++)
            {
                var command = commandList[i];
                if (command == null)
                {
                    continue;
                }

                // Endコマンドなど、ブロックを閉じるコマンドの場合
                if (command.CloseBlock())
                {
                    indentLevel--;
                }

                // インデントレベルが負の値になることは許可しない
                indentLevel = Mathf.Max(indentLevel, 0);
                command.IndentLevel = indentLevel;

                // Ifコマンドなど、新しいブロックを開くコマンドの場合
                if (command.OpenBlock())
                {
                    indentLevel++;
                }
            }
        }

        /// <summary>
        /// 指定されたキーに一致するLabelコマンドのインデックス番号を返します。見つからない場合は-1を返します。
        /// </summary>
        public virtual int GetLabelIndex(string labelKey)
        {
            if (labelKey.Length == 0)
            {
                return -1;
            }

            for (int i = 0; i < commandList.Count; i++)
            {
                var command = commandList[i];
                var labelCommand = command as Label;
                if (labelCommand != null && String.Compare(labelCommand.Key, labelKey, true) == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion
    }
}
