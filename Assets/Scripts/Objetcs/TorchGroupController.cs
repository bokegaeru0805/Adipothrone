using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 複数のトーチをグループとして管理し、一斉または連続して状態を変化させるコントローラー。
/// </summary>
public class TorchGroupController : MonoBehaviour
{
    [Header("制御対象のトーチ")]
    [Tooltip("このコントローラーが管理するトーチのリスト")]
    [SerializeField]
    private List<TorchController> torchesToControl = new List<TorchController>();

    [Header("連続点灯の設定")]
    [Tooltip("トーチを一つずつ状態変化させる際の間隔（秒）")]
    [SerializeField]
    private float delayBetweenTorches = 0.5f;

    [Header("イベント")]
    [Tooltip("全てのトーチの状態変化が完了した後に呼び出されるイベント")]
    [SerializeField]
    private List<UnityEvent> onSequenceComplete = new List<UnityEvent>();

    // --- UnityEventから呼び出すための公開メソッド群 ---

    #region --- 一斉に状態を変化させるメソッド ---

    public void TurnAllOff() => SetStateForAll(TorchController.TorchState.Off);

    public void TurnAllRed() => SetStateForAll(TorchController.TorchState.Red);

    public void TurnAllBlue() => SetStateForAll(TorchController.TorchState.Blue);

    #endregion

    #region --- 連続して状態を変化させるメソッド ---

    public void TurnAllOffSequentially() => StartSequence(TorchController.TorchState.Off);

    public void TurnAllRedSequentially() => StartSequence(TorchController.TorchState.Red);

    public void TurnAllBlueSequentially() => StartSequence(TorchController.TorchState.Blue);

    #endregion


    // --- 内部処理用のプライベートメソッド群 ---

    /// <summary>
    /// 全てのトーチの状態を一度に設定します。
    /// </summary>
    private void SetStateForAll(TorchController.TorchState newState)
    {
        // 実行中のシーケンスがあれば停止
        StopAllCoroutines();

        foreach (var torch in torchesToControl)
        {
            torch.SetTorchState(newState);
        }

        // 処理完了イベントを発行
        foreach (var onComplete in onSequenceComplete)
        {
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// 連続で状態を変化させるコルーチンを開始します。
    /// </summary>
    private void StartSequence(TorchController.TorchState newState)
    {
        // 実行中のシーケンスがあれば停止させてから、新しいシーケンスを開始
        StopAllCoroutines();
        StartCoroutine(SequenceCoroutine(newState));
    }

    /// <summary>
    /// 実際に連続で状態を変化させる処理を行うコルーチン。
    /// </summary>
    private IEnumerator SequenceCoroutine(TorchController.TorchState newState)
    {
        foreach (var torch in torchesToControl)
        {
            torch.SetTorchState(newState);
            yield return new WaitForSeconds(delayBetweenTorches);
        }

        // 処理完了イベントを発行
        foreach (var onComplete in onSequenceComplete)
        {
            onComplete?.Invoke();
        }
    }
}
