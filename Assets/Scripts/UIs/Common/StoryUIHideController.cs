using System.Collections.Generic;
using Fungus;
using UnityEngine;

/// <summary>
/// FungusのStoryブロックでの会話中に、指定されたUI要素を自動的に非表示にするコンポーネント。
/// </summary>
public class StoryUIHideController : MonoBehaviour
{
    [Header("UI設定")]
    [Tooltip("Storyブロックでの会話開始時に非表示にするUI要素のリスト")]
    [SerializeField]
    private List<GameObject> targetUIs = new List<GameObject>();

    // 現在のUI非表示が「Storyブロックの会話」によるものかを追跡するフラグ
    private bool isHiddenByStoryTalk = false;

    private void OnEnable()
    {
        // GameManagerの会話状態変更イベントを購読
        GameManager.OnTalkingStateChanged += HandleTalkingStateChanged;
    }

    private void OnDisable()
    {
        // オブジェクト破棄・非アクティブ時にイベント購読を解除（メモリリーク防止）
        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
    }

    /// <summary>
    /// GameManagerから会話状態の変更通知を受け取るメソッド
    /// </summary>
    /// <param name="isTalking">会話中かどうかのフラグ</param>
    private void HandleTalkingStateChanged(bool isTalking)
    {
        if (isTalking)
        {
            // 会話開始時：実行中のブロックがStoryタイプか確認
            if (IsCurrentBlockStory())
            {
                SetTargetUIsActive(false);
                isHiddenByStoryTalk = true;
            }
        }
        else
        {
            // 会話終了時：このスクリプトがUIを隠した場合のみ再表示する
            if (isHiddenByStoryTalk)
            {
                SetTargetUIsActive(true);
                isHiddenByStoryTalk = false;
            }
        }
    }

    /// <summary>
    /// 現在実行中のFungusブロックが「Story」タイプかどうかを判定します。
    /// </summary>
    private bool IsCurrentBlockStory()
    {
        // 重いFindObjectsOfTypeを廃止し、Fungusが自動管理しているFlowchartリストを参照する
        foreach (Flowchart flowchart in Flowchart.CachedFlowcharts)
        {
            // そのFlowchartが持っているブロックのみを取得（シーン全体検索に比べて圧倒的に軽量）
            Block[] blocksInFlowchart = flowchart.GetComponents<Block>();

            foreach (Block block in blocksInFlowchart)
            {
                // 現在実行中（Executing）のブロックを探し、タイプを判定
                if (block.State == ExecutionState.Executing)
                {
                    return block.TypeOfBlock == BlockType.Story;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 登録されたUIオブジェクトの表示状態を一括で切り替えます。
    /// </summary>
    private void SetTargetUIsActive(bool isActive)
    {
        foreach (GameObject ui in targetUIs)
        {
            if (ui != null)
            {
                ui.SetActive(isActive);
            }
        }
    }
}
