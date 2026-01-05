using UnityEngine;
using UnityEngine.Playables;

public class SePlayableBehaviour : PlayableBehaviour
{
    public System.Enum cue;
    
    // オプションデータ
    public bool overrideVolume;
    public float volume;
    public bool overridePitch;
    public float pitch;

    public bool hasPlayed = false;

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        hasPlayed = false;
    }
}