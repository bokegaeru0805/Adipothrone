using UnityEngine;
using UnityEngine.Playables;

// データを運ぶだけのクラス
public class HeroinePlayableBehaviour : PlayableBehaviour
{
    public int bodyState;
    public int animState;
    public HeroineClip.FacingType facing;
}