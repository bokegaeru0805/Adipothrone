// このコードはFungusライブラリ（https://github.com/snozbot/fungus）の一部です。
// MITオープンソースライセンス（https://github.com/snozbot/fungus/blob/master/LICENSE）の下で無料で公開されています。

using System.Collections;
using Fungus;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// ブロック内の次のコマンドを実行する前に、指定された時間待機します。
/// TimelineSkipManagerの早送り・全スキップ機能に対応しています。
/// </summary>
[CommandInfo("Flow", "Wait", "ブロック内の次のコマンドを実行する前に、指定された時間待機します。")]
[AddComponentMenu("")]
[ExecuteInEditMode]
public class Wait : Command
{
    [Tooltip("待機する時間（秒）")]
    [SerializeField]
    protected FloatData _duration = new FloatData(1);

    #region Public Methods

    public override void OnEnter()
    {
        // 目標時間が0以下の場合は即座に完了させる
        if (_duration.Value <= 0f)
        {
            OnWaitComplete();
            return;
        }

        // コルーチンを使用して待機処理を開始
        StartCoroutine(WaitRoutine());
    }

    public override string GetSummary()
    {
        return _duration.Value.ToString() + " seconds";
    }

    public override Color GetButtonColor()
    {
        return new Color32(190, 190, 220, 255);
    }

    public override bool HasReference(Variable variable)
    {
        return _duration.floatRef == variable || base.HasReference(variable);
    }

    #endregion

    #region Internal Logic

    /// <summary>
    /// 待機処理を行うコルーチン。TimelineSkipManagerの状態を監視し、時間を進めます。
    /// </summary>
    private IEnumerator WaitRoutine()
    {
        float timer = 0f;
        float duration = _duration.Value;

        while (timer < duration)
        {
            float dt = Time.unscaledDeltaTime;

            // TimelineSkipManagerが存在する場合、早送り/スキップの処理を適用
            if (TimelineSkipManager.instance != null)
            {
                if (TimelineSkipManager.instance.IsSkipping)
                {
                    // 全スキップ中は即座に待機を完了させる
                    break;
                }
                else if (TimelineSkipManager.instance.IsFastForwarding)
                {
                    // 早送り中は経過時間に倍率をかける
                    dt *= TimelineSkipManager.instance.FastForwardSpeed;
                }
            }

            timer += dt;
            yield return null; // 1フレーム待機
        }

        OnWaitComplete();
    }

    /// <summary>
    /// 待機が完了した際に呼び出されるメソッド
    /// </summary>
    protected virtual void OnWaitComplete()
    {
        Continue();
    }

    #endregion

    #region Backwards compatibility

    [HideInInspector]
    [FormerlySerializedAs("duration")]
    public float durationOLD;

    protected virtual void OnEnable()
    {
        // 古いバージョンの設定値を新しいフォーマットに引き継ぐ処理
        if (durationOLD != default(float))
        {
            _duration.Value = durationOLD;
            durationOLD = default(float);
        }
    }

    #endregion
}
