using UnityEngine;
namespace CastleFight {
public sealed class NativeMapLoop : MonoBehaviour {
    public Animation player;public AnimationClip clip;
    void Start(){player.AddClip(clip,clip.name);player[clip.name].wrapMode=WrapMode.Loop;player.Play(clip.name);}
}
}
