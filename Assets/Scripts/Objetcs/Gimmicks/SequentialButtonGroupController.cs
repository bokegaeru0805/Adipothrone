using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 指定された順番で押すボタン群の進行状態と表示を管理します。
/// </summary>
public class SequentialButtonGroupController : MonoBehaviour, IEnemyResettable
{
    private const int SpriteCount = 12;
    private const int QuestionSpriteIndex = 10;
    private const int NullSpriteIndex = 11;
    private const int MaxButtonCount = 9;

    [Serializable]
    private class ButtonEntry
    {
        [Tooltip("この順番で押すボタン")]
        public SequentialButton button;

        [Tooltip("数字の代わりにQuestionスプライトを表示するか")]
        public bool isQuestion;
    }

    [Header("クリア条件")]
    [SerializeField]
    [Tooltip("全ボタン成功時にONにするKeyID。ResetState時の停止判定にも使用します")]
    private KeyID completionKeyID;

    [Header("ボタン登録（上から1～9の正解順）")]
    [SerializeField]
    private List<ButtonEntry> buttons = new List<ButtonEntry>();

    [Header("スプライト一覧（0～9、Question、Nullの順）")]
    [SerializeField]
    [Tooltip("OFF状態。要素0～9=数字、10=Question、11=Null")]
    private Sprite[] offSprites = new Sprite[SpriteCount];

    [SerializeField]
    [Tooltip("ON状態。要素0～9=数字、10=Question、11=Null")]
    private Sprite[] onSprites = new Sprite[SpriteCount];

    [SerializeField]
    [Tooltip("条件達成済みの停止状態で使用するNullスプライト")]
    private Sprite stoppedNullSprite;

    [Header("判定設定")]
    [SerializeField]
    [Tooltip("壁越し（GroundLayer）の攻撃ヒットを無効にするか")]
    private bool preventWallPenetration = false;

    [Header("イベント")]
    [SerializeField]
    [Tooltip("最後まで正しい順番で押されたときに一度だけ実行します")]
    private UnityEvent onCompleted;

    private int nextButtonIndex;
    private bool isStopped;

    private void Awake()
    {
        SynchronizeButtons(true);
    }

    private IEnumerator Start()
    {
        // Persistent Manager群の初期化後に保存済みKeyIDを反映する。
        yield return new WaitForEndOfFrame();
        ResetState();
    }

    /// <summary>
    /// 子ボタンから押下を通知します。
    /// </summary>
    public void NotifyButtonPressed(SequentialButton pressedButton)
    {
        if (isStopped || pressedButton == null || buttons.Count == 0)
            return;

        if (nextButtonIndex >= buttons.Count || buttons[nextButtonIndex].button != pressedButton)
        {
            Debug.Log($"[{nameof(SequentialButtonGroupController)}] 順番ミス。最初からやり直します", this);
            ResetProgress();
            return;
        }

        pressedButton.SetPushed(true);
        nextButtonIndex++;
        Debug.Log($"[{nameof(SequentialButtonGroupController)}] {nextButtonIndex}番目のボタン成功", this);

        if (nextButtonIndex < buttons.Count)
            return;

        Debug.Log($"[{nameof(SequentialButtonGroupController)}] 全ボタン成功", this);
        FlagManager.instance?.SetKeyOpened(completionKeyID, true);
        SetStoppedState();
        onCompleted?.Invoke();
    }

    /// <summary>
    /// KeyID達成済みなら停止し、未達成なら最初から入力できる状態へ戻します。
    /// </summary>
    public void ResetState()
    {
        bool isCompleted = FlagManager.instance != null
            && FlagManager.instance.GetKeyOpened(completionKeyID);

        if (isCompleted)
        {
            SetStoppedState();
            return;
        }

        ResetProgress();
    }

    private void ResetProgress()
    {
        nextButtonIndex = 0;
        isStopped = false;

        foreach (ButtonEntry entry in buttons)
        {
            entry?.button?.SetPushed(false);
        }
    }

    private void SetStoppedState()
    {
        isStopped = true;

        foreach (ButtonEntry entry in buttons)
        {
            entry?.button?.SetStopped(stoppedNullSprite);
        }
    }

    private void SynchronizeButtons(bool shouldUpdateVisual)
    {
        int count = Mathf.Min(buttons.Count, MaxButtonCount);
        for (int i = 0; i < count; i++)
        {
            ButtonEntry entry = buttons[i];
            if (entry == null || entry.button == null)
                continue;

            int spriteIndex = entry.isQuestion ? QuestionSpriteIndex : i + 1;
            entry.button.Configure(
                this,
                GetSprite(offSprites, spriteIndex),
                GetSprite(onSprites, spriteIndex),
                preventWallPenetration,
                shouldUpdateVisual
            );
        }
    }

    private static Sprite GetSprite(Sprite[] sprites, int index)
    {
        if (sprites == null || index < 0 || index >= sprites.Length)
            return null;

        return sprites[index] ?? (sprites.Length > NullSpriteIndex ? sprites[NullSpriteIndex] : null);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResizeSpriteArray(ref offSprites);
        ResizeSpriteArray(ref onSprites);

        if (buttons.Count > MaxButtonCount)
        {
            buttons.RemoveRange(MaxButtonCount, buttons.Count - MaxButtonCount);
            Debug.LogWarning($"[{nameof(SequentialButtonGroupController)}] 登録可能なボタンは最大{MaxButtonCount}個です", this);
        }

        // OnValidate中のSpriteRenderer変更はUnity内部のSendMessage警告を発生させるため、
        // ここでは参照と設定値の同期だけを行う。
        SynchronizeButtons(false);
    }

    private static void ResizeSpriteArray(ref Sprite[] sprites)
    {
        if (sprites == null)
        {
            sprites = new Sprite[SpriteCount];
            return;
        }

        if (sprites.Length != SpriteCount)
            Array.Resize(ref sprites, SpriteCount);
    }
#endif
}
