using Fungus;
using UnityEngine;
using UnityEngine.Playables;

[CommandInfo(
    "Timeline",
    "Stop Timeline",
    "再生中のTimelineを強制停止し、カメラ制御などを元に戻します"
)]
[AddComponentMenu("")]
public class StopTimelineCommand : Command
{
    [Tooltip("停止させたいPlayable Director")]
    [SerializeField]
    protected PlayableDirector director;

    public override void OnEnter()
    {
        if (director != null)
        {
            // Timelineを停止する
            // これにより Mixerの OnGraphStop が呼ばれ、カメラが主人公に戻ります
            director.Stop();
        }

        Continue();
    }

    public override string GetSummary()
    {
        if (director == null)
            return "Error: No Director selected";
        return director.name;
    }

    public override Color GetButtonColor()
    {
        // 停止っぽい色（赤系）
        return new Color32(235, 128, 128, 255);
    }
}
