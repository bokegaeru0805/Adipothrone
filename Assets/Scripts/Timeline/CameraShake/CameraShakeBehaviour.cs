using UnityEngine;
using UnityEngine.Playables;

public class CameraShakeBehaviour : PlayableBehaviour
{
    public float amplitude;
    public float frequency;
    public AnimationCurve intensityCurve;
}