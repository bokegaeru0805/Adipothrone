using Fungus;
using UnityEngine;

/// <summary>
/// スキップ実行中にFungus（会話）を制御するクラス。
/// SkipSystemManagerから制御されます。
/// </summary>
public class FungusSkipController : MonoBehaviour
{
    public static FungusSkipController instance { get; private set; }
    private bool isSkipAllowed = false;

    private void Awake()
    {
        if (instance == null)
            instance = this;
    }

    private void Start()
    {
        FungusCustomSignals.OnTalkBlockIsSkippable += HandleBlockIsSkippable;
    }

    private void OnDestroy()
    {
        FungusCustomSignals.OnTalkBlockIsSkippable -= HandleBlockIsSkippable;
    }

    /// <summary>
    /// 現在アクティブなWriterを探し、強制的に文字送りをする
    /// </summary>
    public void UpdateSkipProcessing()
    {
        // シーン上の全てのWriterを探す（キャッシュ推奨だが、FungusはWriterを生成・破棄することがあるため動的取得）
        // ※重い場合はSayDialogのシングルトン管理などを検討してください
        var writers = FindObjectsOfType<CustomWriter>();

        foreach (var writer in writers)
        {
            if (writer.IsWriting || writer.IsWaitingForInput)
            {
                writer.ForceAdvance();
            }
        }
    }

    /// <summary>
    /// 現在実行中の全てのブロックを確認し、
    /// 「スキップ不可」に設定されているブロックが1つでもあれば false を返す
    /// </summary>
    public bool IsSkipAllowed()
    {
        return isSkipAllowed;
    }

    /// <summary>
    /// 現在のブロックがスキップ可能かどうかを受け取るハンドラー
    /// </summary>
    /// <param name="isSkippable">スキップ可能かどうかのフラグ</param>
    private void HandleBlockIsSkippable(bool isSkippable)
    {
        isSkipAllowed = isSkippable;
    }
}
