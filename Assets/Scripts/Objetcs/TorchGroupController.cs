using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 複数のトーチをグループとして管理し、一斉または連続して状態を変化させるコントローラー。
/// Unity Eventから確実に呼び出せるように、引数なしの公開メソッドを用意しています。
/// </summary>
public class TorchGroupController : MonoBehaviour
{
    #region Inspector Settings

    [Header("制御対象のトーチ")]
    [Tooltip("このコントローラーが管理するトーチのリスト")]
    [SerializeField]
    private List<TorchController> torchesToControl = new List<TorchController>();

    [Header("演出設定")]
    [Tooltip("トーチを一つずつ状態変化させる（Sequentially）際の間隔（秒）")]
    [SerializeField]
    private float delayBetweenTorches = 0.5f;

    [Tooltip("グループで状態を変化させる際、各トーチの着火・消火SEを鳴らすか")]
    [SerializeField]
    private bool playSEOnChange = true;

    [Header("イベント")]
    [Tooltip("全てのトーチの状態変化が完了した後に呼び出されるイベント")]
    [SerializeField]
    private List<UnityEvent> onSequenceComplete = new List<UnityEvent>();

    #endregion

    #region Unity Event Public Methods (引数なし)

    // --- 一斉変化 (All) ---
    public void TurnAllOff() => ChangeStateAll(TorchController.TorchState.Off);

    public void TurnAllRed() => ChangeStateAll(TorchController.TorchState.Red);

    public void TurnAllBlue() => ChangeStateAll(TorchController.TorchState.Blue);

    // --- 連続変化 (Sequentially) ---
    public void TurnAllOffSequentially() => ChangeStateSequentially(TorchController.TorchState.Off);

    public void TurnAllRedSequentially() => ChangeStateSequentially(TorchController.TorchState.Red);

    public void TurnAllBlueSequentially() =>
        ChangeStateSequentially(TorchController.TorchState.Blue);

    #endregion

    #region Internal Logic

    /// <summary>
    /// リスト内の全てのトーチを一斉に指定した状態に変更します。
    /// </summary>
    private void ChangeStateAll(TorchController.TorchState newState)
    {
        StopAllCoroutines();

        foreach (var torch in torchesToControl)
        {
            torch.SetTorchState(newState, playSEOnChange);
        }

        InvokeCompleteEvents();
    }

    /// <summary>
    /// リスト内のトーチを、指定した間隔で順番に指定した状態に変更します。
    /// </summary>
    private void ChangeStateSequentially(TorchController.TorchState newState)
    {
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
            torch.SetTorchState(newState, playSEOnChange);
            yield return new WaitForSeconds(delayBetweenTorches);
        }

        InvokeCompleteEvents();
    }

    /// <summary>
    /// 登録されている完了イベントをすべて発火させます。
    /// </summary>
    private void InvokeCompleteEvents()
    {
        foreach (var onComplete in onSequenceComplete)
        {
            onComplete?.Invoke();
        }
    }

    #endregion
}
