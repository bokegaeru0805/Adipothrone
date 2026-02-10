using System;
using System.Collections;
using System.Collections.Generic;
using Fungus;
using UnityEngine;

/// <summary>
/// 宝箱くじ引きの進行管理クラス。
/// Fungusと連携し、抽選、モンティ・ホール問題の提示、結果判定を行う。
/// TreasureFungus（アイテム獲得演出）と店主の会話の連続実行を制御する。
/// </summary>
public class LotteryGameManager : MonoBehaviour
{
    #region Settings & References

    [Header("Settings")]
    [SerializeField]
    private List<LotteryChestController> chests; // 使用する宝箱のリスト

    [SerializeField]
    private List<LotteryItemEntry> lotteryItems; // 抽選対象アイテムリスト

    [SerializeField]
    private int entryFee; //一回のゲームの参加費

    [SerializeField]
    private int hintCost; // ハズレを開けるために必要な金額

    [Header("Fungus Integration")]
    [SerializeField]
    private Flowchart lotteryFlowchart; // このミニゲーム用の会話フローチャート

    [SerializeField]
    private string entryBlockName = "Lottery_Entry"; // 受付開始用ブロック名

    [SerializeField]
    private string entryDeniedBlockName = "Lottery_EntryDenied"; //参加拒否用ブロック名

    [SerializeField]
    private string waitFirstSelectBlockName = "Lottery_WaitFirstSelect"; // 最初の選択待ち用ブロック名

    [SerializeField]
    private string offerBlockName = "Lottery_Offer"; // 2択提示用ブロック名

    [SerializeField]
    private string hintDeniedBlockName = "Lottery_HintDenied"; //ヒント拒否用ブロック名

    [SerializeField]
    private string waitFinalSelectBlockName = "Lottery_WaitFinalSelect"; // 最終選択待ち用ブロック名

    [SerializeField]
    private string resultWinBlockName = "Lottery_Win"; // 当たり時の店主コメント用ブロック名

    [SerializeField]
    private string resultLoseBlockName = "Lottery_Lose"; // ハズレ時の店主コメント用ブロック名

    [SerializeField]
    private string variableNameSelectedChest = "SelectedChestID"; // Fungusに渡す選択した宝箱番号の変数名
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
        // ※このスクリプトがアタッチされているオブジェクトにCollider2Dが必要です
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
                    if (lotteryFlowchart != null)
                    {
                        lotteryFlowchart.ExecuteBlock(waitFirstSelectBlockName);
                    }
                    break;
                case GameState.WaitingFinalSelect:
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
        // 1. 参加費を払う
        PlayerManager.instance.ChangeMoney(-entryFee);

        // 2. 当たりアイテムの抽選（重み付きランダム）を行い、保持する
        currentRoundPrize = DrawRandomItem();

        // 3. 当たりを入れる宝箱を決定
        winningChestIndex = UnityEngine.Random.Range(0, chests.Count);

        // デバッグ用ログ
        string itemName =
            currentRoundPrize != null && currentRoundPrize.itemData != null
                ? currentRoundPrize.itemData.itemName
                : "ハズレ";
        Debug.Log($"[Lottery] 当たりは宝箱 {winningChestIndex} 番。中身: {itemName}");

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
        // 金を支払う
        PlayerManager.instance.ChangeMoney(-hintCost);

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
            Debug.Log($"[Lottery] ヒント: 宝箱 {revealIndex} は空です");
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

            // 所持金チェック
            int currentMoney = PlayerManager.instance.GetPlayerIntStatus(
                PlayerStatusIntName.playerMoney
            );

            if (currentMoney < hintCost)
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
                lotteryFlowchart.SetIntegerVariable(variableNameSelectedChest, index + 1); // 1始まりで渡す
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
        int currentMoney = PlayerManager.instance.GetPlayerIntStatus(
            PlayerStatusIntName.playerMoney
        );

        if (currentMoney >= entryFee)
        {
            // ゲーム開始ブロックを実行
            if (lotteryFlowchart != null)
            {
                lotteryFlowchart.ExecuteBlock(entryBlockName);
            }
            else
            {
                Debug.LogError("Flowchartが設定されていません");
            }
        }
        else
        {
            // 金が足りない場合は参加拒否ブロックを呼ぶ
            if (lotteryFlowchart != null)
            {
                lotteryFlowchart.ExecuteBlock(entryDeniedBlockName);
            }
        }
    }

    /// <summary>
    /// 結果判定と報酬付与。
    /// TreasureFungusの実行終了を待ってから店主の会話へ繋げる。
    /// </summary>
    private void ResolveGame(int selectedIndex)
    {
        bool isWin = (selectedIndex == winningChestIndex);

        // 選んだ宝箱を開く
        chests[selectedIndex].OpenVisual();

        // 演出として、選ばれなかった他の宝箱も全て開けてネタ晴らしする
        foreach (var chest in chests)
        {
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
    /// 重みに基づく抽選
    /// </summary>
    private LotteryItemEntry DrawRandomItem()
    {
        if (lotteryItems == null || lotteryItems.Count == 0)
            return null;

        int totalWeight = 0;
        foreach (var entry in lotteryItems)
            totalWeight += entry.weight;

        int randomValue = UnityEngine.Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var entry in lotteryItems)
        {
            currentWeight += entry.weight;
            if (randomValue < currentWeight)
            {
                return entry;
            }
        }
        return lotteryItems[0]; // フォールバック
    }

    #endregion

    #region Coroutines & Helpers

    /// <summary>
    /// 全てのAwake/Startが完了するのを待ってから、初期化処理を実行するコルーチン
    /// </summary>
    private IEnumerator DelayedInitialization()
    {
        // 最初のフレームの描画が終わるまで待つ
        // これにより、全てのシングルトンが確実に初期化されている状態になる
        yield return new WaitForEndOfFrame();

        // イベントを購読する
        GameManager.OnTalkingStateChanged += HandleTalkingStateChanged;
    }

    /// <summary>
    /// GameManagerから会話状態の変更通知を受け取る
    /// </summary>
    private void HandleTalkingStateChanged(bool talkState)
    {
        isTalking = talkState;
    }

    /// <summary>
    /// 会話状態（GameManager.isTalking）が終了するのを監視し、
    /// 終了したタイミングで次のFungusブロックを実行するコルーチン。
    /// TalkEndCommandが実行され、制御が戻ってきた後に店主の会話を始めるために使用。
    /// </summary>
    private IEnumerator WaitTalkEndAndExecuteBlock(string nextBlockName)
    {
        // GameManager.TreasureFungus実行直後だと、Fungusが動き出してisTalkingがtrueになるまで
        // 1フレーム程度のラグがある可能性があるため、念のため少し待つ
        yield return null;

        // GameManagerのイベントを利用して会話終了を待機する
        bool isTalkFinished = false;

        // 会話状態変更イベントのリスナーを定義
        Action<bool> onTalkingChanged = (isTalking) =>
        {
            // 会話が終わった（isTalking == false）ならフラグを立てる
            if (!isTalking)
            {
                isTalkFinished = true;
            }
        };

        // イベント登録
        GameManager.OnTalkingStateChanged += onTalkingChanged;

        // 会話が終わるまで待機
        // TreasureFungusの最後のTalkEndCommandが実行されるとisTalkingがfalseになり、ここを抜ける
        yield return new WaitUntil(() => isTalkFinished);

        // イベント解除
        GameManager.OnTalkingStateChanged -= onTalkingChanged;

        // 少しだけ間を空けて（演出的な余韻）、次のブロックを実行
        yield return new WaitForSeconds(0.2f);

        // 店主のリアクションブロック実行
        lotteryFlowchart.ExecuteBlock(nextBlockName);
    }

    #endregion
}
