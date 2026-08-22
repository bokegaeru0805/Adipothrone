using System.Collections;
using Fungus;
using NaughtyAttributes;
using UnityEngine;

[CommandInfo(
    "Camera",
    "Start Camera Shake",
    "持続的なカメラ振動を開始します。時間経過での停止や、外部Stopコマンドによる停止が可能です。"
)]
[AddComponentMenu("")]
public class StartCameraShakeCommand : Command
{
    [Tooltip("揺れの強さ（振幅）")]
    [SerializeField]
    protected float amplitude = 1.0f;

    [Tooltip("揺れの速さ（周波数）")]
    [SerializeField]
    protected float frequency = 1.0f;

    [Header("終了位置の設定")]
    [Tooltip("シェイク終了後、開始時の座標ではなく任意の座標へ移動するか")]
    [SerializeField]
    protected bool isUseCustomEndPosition = false;

    [Tooltip("シェイク終了後のX/Y座標。Z座標はシェイク開始時の値を維持します。")]
    [AllowNesting]
    [SerializeField, ShowIf(nameof(isUseCustomEndPosition))]
    protected Vector2 customEndPosition;

    [Header("停止条件の設定")]
    [Tooltip("時間経過で自動停止するかどうか")]
    [SerializeField]
    protected bool stopByTime = false;

    [Tooltip("自動停止するまでの時間（秒）")]
    [AllowNesting]
    [
        SerializeField,
        UnityEngine.Serialization.FormerlySerializedAs("duration"),
        ShowIf(nameof(stopByTime))
    ]
    protected float timeToStop = 2.0f;

    [Tooltip("時間経過で停止する際、フェードアウトにかける時間（秒）。0で即停止。")]
    [AllowNesting]
    [SerializeField, ShowIf(nameof(stopByTime))]
    protected float fadeOutTimeOnTimeStop = 0.5f;

    [Tooltip(
        "外部の「Stop Camera Shake」コマンドによる停止を受け付けるか。\n（両方がTrueの場合、先に条件を満たした方で停止します）"
    )]
    [SerializeField]
    protected bool stopByCommand = false;

    [Header("進行設定")]
    [Tooltip(
        "Trueの場合、時間による揺れ＋フェードアウトが完了するまで次のFungusコマンドに進みません。\n※手動(Command)でのみ停止させる場合は、基本的にFalseにしてください。"
    )]
    [SerializeField]
    protected bool waitUntilFinished = false;

    private Coroutine shakeCoroutine;

    public override void OnEnter()
    {
        if (MyGame.CameraControl.CameraManager.instance == null)
        {
            Continue();
            return;
        }

        // 1. 揺れを開始
        MyGame.CameraControl.CameraManager.instance.PlayContinuousShake(
            amplitude,
            frequency,
            isUseCustomEndPosition,
            customEndPosition
        );

        // 2. 時間経過での停止が有効な場合、タイマーを開始
        if (stopByTime)
        {
            shakeCoroutine = StartCoroutine(WaitAndStopCoroutine());
        }

        // 3. 次のコマンドに進むかどうかの判定
        if (waitUntilFinished && stopByTime)
        {
            // コルーチンの中でContinue()が呼ばれるのを待つ
        }
        else
        {
            // 即座に次のコマンドへ進む（並列でStopコマンドを待つ場合など）
            Continue();
        }
    }

    private IEnumerator WaitAndStopCoroutine()
    {
        // 指定時間（揺れ続ける時間）待機
        yield return new WaitForSeconds(timeToStop);

        // この時点で、外部のStopコマンドによって既に停止されていなければ、時間経過での停止を実行
        if (
            MyGame.CameraControl.CameraManager.instance != null
            && MyGame.CameraControl.CameraManager.instance.IsContinuousShakeActive
        )
        {
            MyGame.CameraControl.CameraManager.instance.StopContinuousShake(fadeOutTimeOnTimeStop);

            // WaitUntilFinishedがTrueなら、フェードアウト完了まで更に待ってから進む
            if (waitUntilFinished)
            {
                yield return new WaitForSeconds(fadeOutTimeOnTimeStop);
                Continue();
            }
        }
        else
        {
            // 既に外部コマンドで停止されていた場合は、ここでContinueは呼ばない
            // （外部Stopコマンド側で進行が行われているはずなので重複を防ぐ）
        }
    }

    public override void OnStopExecuting()
    {
        // Fungusのフロー自体が強制停止された際の安全処理
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }
        if (
            MyGame.CameraControl.CameraManager.instance != null
            && MyGame.CameraControl.CameraManager.instance.IsContinuousShakeActive
        )
        {
            MyGame.CameraControl.CameraManager.instance.StopContinuousShake(0f);
        }
        base.OnStopExecuting();
    }

    public override string GetSummary()
    {
        string stopType = stopByTime ? $"Time({timeToStop}s)" : "Manual Stop Only";
        string endPosition = isUseCustomEndPosition
            ? $"End:{customEndPosition}"
            : "End:Start Position";
        return $"Amp:{amplitude}, Freq:{frequency} | {stopType} | {endPosition}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(235, 191, 217, 255); // カメラ系コマンドの標準色（ピンク系）
    }
}
