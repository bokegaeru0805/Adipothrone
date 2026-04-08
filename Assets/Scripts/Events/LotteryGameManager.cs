using System;
using System.Collections;
using System.Collections.Generic;
using Fungus;
using UnityEngine;

/// <summary>
/// 宝箱くじ引きの進行管理クラス。
/// Fungusと連携し、抽選、モンティ・ホール問題の提示、結果判定を行う。
/// フラグやアイテム所持状況に応じて、景品内容や価格（プロファイル）を動的に切り替える機能を持つ。
/// </summary>
public class LotteryGameManager : MonoBehaviour
{
    #region Settings & References

    [Header("Chest Settings")]
    [SerializeField]
    private List<LotteryChestController> chests; // 使用する宝箱のリスト

    [Header("Default Profile")]
    [Tooltip("条件に一致するプロファイルがない場合に使用されるデフォルトの景品リスト")]
    [SerializeField]
    private List<LotteryItemEntry> defaultLotteryItems;

    [Tooltip("条件に一致するプロファイルがない場合に使用されるデフォルトの参加費")]
    [SerializeField]
    private int defaultEntryFee;

    [Tooltip("条件に一致するプロファイルがない場合に使用されるデフォルトのヒント代")]
    [SerializeField]
    private int defaultHintCost;

    [Header("Conditional Profiles")]
    [Tooltip(
        "条件に応じて切り替わる設定のリスト。下から順（逆順）に評価され、最初に条件を満たしたものが適用されます。"
    )]
    [SerializeField]
    private List<LotteryProfile> lotteryProfiles;

    [Header("Fungus Integration")]
    [SerializeField]
    private Flowchart lotteryFlowchart; // このミニゲーム用の会話フローチャート

    [SerializeField]
    private string entryBlockName = "Lottery_Entry"; // 受付開始用ブロック名

    [SerializeField]
    private string entryDeniedBlockName = "Lottery_EntryDenied"; // 参加拒否（金不足）用ブロック名

    [SerializeField]
    private string invalidConditionBlockName = "Lottery_InvalidCondition"; // 条件不一致（開催不可）用ブロック名

    [SerializeField]
    private string noConditionBlockName = "Lottery_NoCondition"; // 条件が一つも当てはまらなかった場合のブロック名

    [SerializeField]
    private string waitFirstSelectBlockName = "Lottery_WaitFirstSelect"; // 最初の選択待ち用ブロック名

    [SerializeField]
    private string offerBlockName = "Lottery_Offer"; // 2択提示用ブロック名

    [SerializeField]
    private string hintDeniedBlockName = "Lottery_HintDenied"; // ヒント拒否用ブロック名

    [SerializeField]
    private string waitFinalSelectBlockName = "Lottery_WaitFinalSelect"; // 最終選択待ち用ブロック名

    [SerializeField]
    private string resultWinBlockName = "Lottery_Win"; // 当たり時の店主コメント用ブロック名

    [SerializeField]
    private string resultLoseBlockName = "Lottery_Lose"; // ハズレ時の店主コメント用ブロック名

    [Header("Fungus Variables")]
    [SerializeField]
    private string variableNameSelectedChest = "SelectedChestID"; // 選択した宝箱番号

    [SerializeField]
    private string variableNameEntryFee = "EntryFee"; // 現在の参加費

    [SerializeField]
    private string variableNameHintCost = "HintCost"; // 現在のヒント代
    #endregion

    #region Internal State

    // 内部ステート
    private enum GameState
    {
        Idle, // 待機中（初期化前など）
        WaitingForEntry, // 受付中（プレイヤーからのインタラクト待ち）
        WaitingFirstSelect, // 最初の選択待ち
        WaitingOffer, // 変更するかどうかの会話中
        WaitingFinalSelect // 最終選択待ち
        ,
    }

    private GameState currentState = GameState.Idle;
    private int winningChestIndex = -1; // 当たりの宝箱インデックス
    private int firstSelectedIndex = -1; // 最初に選んだ宝箱インデックス
    private LotteryItemEntry currentRoundPrize = null; // SetupGameで決定したアイテムを保持しておくための変数
    private bool isTalking = false; // 会話状態を保存するローカル変数

    // 現在適用されている設定（プロファイル評価後に値が入る）
    private List<LotteryItemEntry> currentLotteryItems;
    private int currentEntryFee;
    private int currentHintCost;

    #endregion

    #region Unity Lifecycle Methods

    private void Start()
    {
        // 宝箱にIDを割り当てて初期化
        for (int i = 0; i < chests.Count; i++)
        {
            if (chests[i] != null)
            {
                chests[i].Initialize(this, i);
            }
        }
    }

    private void OnEnable()
    {
        StartCoroutine(DelayedInitialization());
        currentState = GameState.WaitingForEntry; // 受付状態にする
    }

    private void OnDisable()
    {
        // オブジェクトが非アクティブになったら、購読を解除（メモリリーク防止）
        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // ゲームが動作中、他の会話が実行中でなく、プレイヤーがインタラクトした場合に会話を試みる
        if (
            Time.timeScale > 0
            && !isTalking
            && InputManager.instance.GetInteract()
            && collision.gameObject.CompareTag(GameConstants.PLAYER_TAG_NAME)
        )
        {
            switch (currentState)
            {
                case GameState.WaitingForEntry:
                    HandleEntryInteraction();
                    break;

                case GameState.WaitingFirstSelect:
                    // 選択待ちの時に話しかけた場合の会話
                    if (lotteryFlowchart != null)
                    {
                        lotteryFlowchart.ExecuteBlock(waitFirstSelectBlockName);
                    }
                    break;

                case GameState.WaitingFinalSelect:
                    // 最終選択待ちの時に話しかけた場合の会話
                    if (lotteryFlowchart != null)
                    {
                        lotteryFlowchart.ExecuteBlock(waitFinalSelectBlockName);
                    }
                    break;

                default:
                    // 他の状態では無視
                    break;
            }
        }
    }

    #endregion

    #region Profile Management

    /// <summary>
    /// 現在のフラグやアイテム所持状況に基づいて、適用するプロファイルを決定します。
    /// </summary>
    /// <returns>有効な設定が見つかった（またはデフォルトが有効）場合はtrue、開催不可能な場合はfalse</returns>
    /// <summary>
    /// 現在のフラグやアイテム所持状況に基づいて、適用するプロファイルを決定します。
    /// </summary>
    /// <returns>有効な設定が見つかった（またはデフォルトが有効）場合はtrue、開催不可能な場合はfalse</returns>
    private bool UpdateActiveProfile()
    {
        // 1. プロファイルリストを下から順（新しい/進行度が高い条件）に評価
        for (int i = lotteryProfiles.Count - 1; i >= 0; i--)
        {
            var profile = lotteryProfiles[i];

            if (profile.AreConditionsMet())
            {
                // 条件に合致したプロファイルの設定を適用
                currentLotteryItems = profile.lotteryItems;
                currentEntryFee = profile.entryFee;
                currentHintCost = profile.hintCost;

                // アイテムリストが空でないか確認
                return currentLotteryItems != null && currentLotteryItems.Count > 0;
            }
        }

        // 2. どのプロファイルにも合致しない場合はデフォルト設定を使用
        currentLotteryItems = defaultLotteryItems;
        currentEntryFee = defaultEntryFee;
        currentHintCost = defaultHintCost;

        // デフォルト設定も空の場合は「開催不可」とする
        return currentLotteryItems != null && currentLotteryItems.Count > 0;
    }

    #endregion

    #region Fungus Callbacks (Public)

    /// <summary>
    /// ゲームを開始する。アイテムを抽選し、宝箱を閉じる。
    /// Fungusの会話イベント冒頭で呼び出す。
    /// </summary>
    public void SetupGame()
    {
        if (chests.Count < 2)
        {
            Debug.LogError("宝箱が少なすぎてゲームが成立しません（最低2つ必要）");
            return;
        }

        // 1. 参加費を払う（現在の設定価格を使用）
        PlayerManager.instance.ChangeMoney(-currentEntryFee);

        // 2. 当たりアイテムの抽選（重み付きランダム）を行い、保持する
        currentRoundPrize = DrawRandomItem();

        // 3. 当たりを入れる宝箱を決定
        winningChestIndex = UnityEngine.Random.Range(0, chests.Count);

        // デバッグ用ログ
        string itemName =
            currentRoundPrize != null && currentRoundPrize.itemData != null
                ? currentRoundPrize.itemData.itemName
                : "ハズレ";
        Debug.Log($"[Lottery] 当たりは宝箱 {winningChestIndex + 1} 番。中身: {itemName}");

        // 4. 全ての宝箱を閉じる（リセット）
        foreach (var chest in chests)
        {
            chest.ResetToClose();
        }

        currentState = GameState.WaitingFirstSelect;
        firstSelectedIndex = -1;
    }

    /// <summary>
    /// 「金を払って空の箱を開ける」処理。
    /// Fungusで「支払う」を選択した後に呼び出す。
    /// </summary>
    public void PayAndRevealEmpty()
    {
        // 金を支払う（現在の設定価格を使用）
        PlayerManager.instance.ChangeMoney(-currentHintCost);

        // 「当たり」でもなく、「プレイヤーが選んだ箱」でもない箱を探す
        List<int> openableIndices = new List<int>();
        for (int i = 0; i < chests.Count; i++)
        {
            if (i != winningChestIndex && i != firstSelectedIndex)
            {
                openableIndices.Add(i);
            }
        }

        if (openableIndices.Count > 0)
        {
            // ランダムに1つ選んで開ける（空であることを示す）
            int revealIndex = openableIndices[UnityEngine.Random.Range(0, openableIndices.Count)];
            chests[revealIndex].OpenVisual();
            // Debug.Log($"[Lottery] ヒント: 宝箱 {revealIndex + 1} は空です");
        }

        // 最終選択フェーズへ移行
        currentState = GameState.WaitingFinalSelect;
    }

    /// <summary>
    /// 「金を払わない」または「金がない」場合の処理。
    /// そのまま再選択フェーズへ移行する。
    /// </summary>
    public void SkipReveal()
    {
        // 安全装置: 宝箱未選択時の呼び出し防止
        if (firstSelectedIndex < 0 || firstSelectedIndex >= chests.Count)
        {
            Debug.LogError($"[LotteryError] 不正な宝箱インデックス: {firstSelectedIndex}");
            return;
        }

        // 何も開けずに最終選択へ（プレイヤーの最初の選択で確定させる）
        currentState = GameState.WaitingForEntry;
        ResolveGame(firstSelectedIndex);
    }

    #endregion

    #region Game Logic

    /// <summary>
    /// 宝箱から呼び出される選択処理
    /// </summary>
    public void OnChestSelected(int index)
    {
        if (currentState == GameState.WaitingFirstSelect)
        {
            // --- 最初の選択 ---
            firstSelectedIndex = index;
            currentState = GameState.WaitingOffer; // 会話待ち状態へロック

            // Fungusにヒント価格情報を渡す
            if (lotteryFlowchart != null)
            {
                lotteryFlowchart.SetIntegerVariable(variableNameHintCost, currentHintCost);
            }

            // 所持金チェック（現在のヒント代と比較）
            int currentMoney = PlayerManager.instance.GetPlayerIntStatus(
                PlayerStatusIntName.playerMoney
            );

            if (currentMoney < currentHintCost)
            {
                // 金が足りない場合はヒント拒否ブロックを呼ぶ
                Debug.Log("所持金不足のためヒントなし");
                if (lotteryFlowchart != null)
                {
                    lotteryFlowchart.ExecuteBlock(hintDeniedBlockName);
                }
                return;
            }

            // Fungusに選択した番号を渡す（必要なら会話で使用）
            if (lotteryFlowchart != null)
            {
                lotteryFlowchart.SetIntegerVariable(variableNameSelectedChest, index + 1); // 1始まりで表示
                // 提案ブロックを実行
                lotteryFlowchart.ExecuteBlock(offerBlockName);
            }
            else
            {
                Debug.LogError("Flowchartが設定されていません");
            }
        }
        else if (currentState == GameState.WaitingFinalSelect)
        {
            // --- 最終選択（結果発表） ---
            currentState = GameState.WaitingForEntry; // ゲーム終了状態へ
            ResolveGame(index);
        }
    }

    /// <summary>
    /// 受付時のインタラクト処理
    /// </summary>
    private void HandleEntryInteraction()
    {
        if (lotteryFlowchart == null)
        {
            Debug.LogError("Flowchartが設定されていません");
            return;
        }

        // 1. プロファイルを更新し、開催可能かチェック
        // プロファイルが無い、またはデフォルト設定も空の場合はfalseが返る
        if (!UpdateActiveProfile())
        {
            // Debug.Log(
            //     "[Lottery] 条件に合うプロファイルがなく、デフォルト設定も無効なためキャンセルします。"
            // );
            lotteryFlowchart.ExecuteBlock(invalidConditionBlockName);
            return;
        }

        // 2. Fungusに参加費情報を渡す（会話テキストで金額を表示するため）
        lotteryFlowchart.SetIntegerVariable(variableNameEntryFee, currentEntryFee);

        // 3. 所持金チェック
        int currentMoney = PlayerManager.instance.GetPlayerIntStatus(
            PlayerStatusIntName.playerMoney
        );

        if (currentMoney >= currentEntryFee)
        {
            // ゲーム開始ブロックを実行
            lotteryFlowchart.ExecuteBlock(entryBlockName);
        }
        else
        {
            // 金が足りない場合は参加拒否ブロックを呼ぶ
            lotteryFlowchart.ExecuteBlock(entryDeniedBlockName);
        }
    }

    /// <summary>
    /// 結果判定と報酬付与。
    /// TreasureFungusの実行終了を待ってから店主の会話へ繋げる。
    /// </summary>
    private void ResolveGame(int selectedIndex)
    {
        // インデックス安全チェック
        if (selectedIndex < 0 || selectedIndex >= chests.Count)
            return;

        bool isWin = (selectedIndex == winningChestIndex);

        // 選んだ宝箱を開く
        chests[selectedIndex].OpenVisual();

        // 演出として、選ばれなかった他の宝箱も全て開けてネタ晴らしする
        foreach (var chest in chests)
        {
            if (chest != null)
                chest.OpenVisual();
        }

        if (isWin)
        {
            // 当たりの場合
            if (currentRoundPrize != null && currentRoundPrize.itemData != null)
            {
                // GlobalFlowchartのTreasureBlockを実行（アイテム獲得ウィンドウ表示）
                GameManager.instance.TreasureFungus(
                    currentRoundPrize.itemData,
                    currentRoundPrize.count
                );

                // TreasureFungusによる会話（ウィンドウ）が閉じるのを待ってから、
                // 店主の「おめでとう」ブロックを再生する
                if (lotteryFlowchart != null)
                {
                    StartCoroutine(WaitTalkEndAndExecuteBlock(resultWinBlockName));
                }
            }
            else
            {
                // 設定ミスなどで中身がnullの場合はハズレ扱いへフォールバック
                HandleLose();
            }
        }
        else
        {
            // ハズレの場合
            HandleLose();
        }
    }

    /// <summary>
    /// ハズレ時の処理
    /// </summary>
    private void HandleLose()
    {
        // ハズレの場合は直接店主の「残念だったな」ブロックなどを呼ぶ
        if (lotteryFlowchart != null)
        {
            lotteryFlowchart.ExecuteBlock(resultLoseBlockName);
        }
    }

    /// <summary>
    /// 現在のリスト（currentLotteryItems）から重みに基づく抽選を行う
    /// </summary>
    private LotteryItemEntry DrawRandomItem()
    {
        if (currentLotteryItems == null || currentLotteryItems.Count == 0)
            return null;

        int totalWeight = 0;
        foreach (var entry in currentLotteryItems)
            totalWeight += entry.weight;

        int randomValue = UnityEngine.Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var entry in currentLotteryItems)
        {
            currentWeight += entry.weight;
            if (randomValue < currentWeight)
            {
                return entry;
            }
        }
        return currentLotteryItems[0]; // フォールバック
    }

    #endregion

    #region Coroutines & Helpers

    /// <summary>
    /// 全てのAwake/Startが完了するのを待ってから、初期化処理を実行するコルーチン
    /// </summary>
    private IEnumerator DelayedInitialization()
    {
        yield return new WaitForEndOfFrame();
        GameManager.OnTalkingStateChanged += HandleTalkingStateChanged;
    }

    private void HandleTalkingStateChanged(bool talkState)
    {
        isTalking = talkState;
    }

    /// <summary>
    /// 会話終了を待機して次のブロックを実行する
    /// </summary>
    private IEnumerator WaitTalkEndAndExecuteBlock(string nextBlockName)
    {
        yield return null;

        bool isTalkFinished = false;
        Action<bool> onTalkingChanged = (isTalking) =>
        {
            if (!isTalking)
                isTalkFinished = true;
        };

        GameManager.OnTalkingStateChanged += onTalkingChanged;
        yield return new WaitUntil(() => isTalkFinished);
        GameManager.OnTalkingStateChanged -= onTalkingChanged;

        yield return new WaitForSeconds(0.2f);

        if (lotteryFlowchart != null)
        {
            lotteryFlowchart.ExecuteBlock(nextBlockName);
        }
    }

    #endregion
}

#region Data Structures

/// <summary>
/// アイテム所持条件を定義するクラス
/// </summary>
[System.Serializable]
public class ItemCondition
{
    [Tooltip("所持判定を行うアイテム")]
    public BaseItemData targetItem;

    [Tooltip("必要な所持数（以上）")]
    public int requiredAmount = 1;

    [Tooltip("条件を反転するか（持っていない場合に真とする）")]
    public bool invert = false;

    /// <summary>
    /// 条件を満たしているか判定
    /// </summary>
    public bool IsMet()
    {
        if (targetItem == null)
            return true; // 設定なしは常にTrue扱い

        // GameManager経由で所持数を取得
        int currentAmount = GameManager.instance.GetAllTypeIDToAmount(targetItem);
        bool hasEnough = currentAmount >= requiredAmount;

        return invert ? !hasEnough : hasEnough;
    }
}

/// <summary>
/// くじの設定プロファイル。条件と、適用される設定のセット。
/// </summary>
[System.Serializable]
public class LotteryProfile
{
    [Header("Conditions (AND)")]
    [Tooltip("これらのフラグ条件がすべて満たされた場合に適用されます")]
    public List<FlagConditionPro> flagConditions = new List<FlagConditionPro>();

    [Tooltip("これらのアイテム所持条件がすべて満たされた場合に適用されます")]
    public List<ItemCondition> itemConditions = new List<ItemCondition>();

    [Header("Override Settings")]
    [Tooltip("この条件が満たされたときの景品リスト")]
    public List<LotteryItemEntry> lotteryItems;

    [Tooltip("この条件が満たされたときの参加費")]
    public int entryFee;

    [Tooltip("この条件が満たされたときのヒント代")]
    public int hintCost;

    /// <summary>
    /// すべての条件（フラグ＆アイテム）を満たしているか確認
    /// </summary>
    public bool AreConditionsMet()
    {
        // フラグチェック
        foreach (var flag in flagConditions)
        {
            if (!flag.IsMet())
            {
                return false;
            }
        }

        // アイテムチェック
        foreach (var item in itemConditions)
        {
            if (!item.IsMet())
            {
                return false;
            }
        }

        return true;
    }
}

#endregion
