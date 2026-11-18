using System;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed class PcModel : MonoBehaviour {
    public bool reforged,particleOnly;public string appearanceKey;public GameObject animatedRoot;public string raw,animationProperties;public AnimationClip[] clips;public float godotHeight,visualScale=1;
    Animation player;NativeAppearance appearance;string pose;int previousAttack=-1;
    void Awake() {
        if(particleOnly)return;
        foreach(var animator in GetComponentsInChildren<Animator>())animator.enabled=false;
        player=animatedRoot.GetComponent<Animation>();if(!player)player=animatedRoot.AddComponent<Animation>();
        foreach(var clip in clips)player.AddClip(clip,clip.name);
        player.cullingType=reforged?AnimationCullingType.BasedOnRenderers:AnimationCullingType.AlwaysAnimate;
        if(reforged)foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>())skin.updateWhenOffscreen=false;
        appearance=animatedRoot.AddComponent<NativeAppearance>();appearance.Configure(string.IsNullOrEmpty(appearanceKey)?raw:appearanceKey,player);Play("Stand");
    }
    public void Play(string requested,float speed=1,int attackSerial=0) {
        if(particleOnly)return;var clip=Choose(requested);
        if(!clip)return;
        player[clip.name].speed=speed;
        bool once=requested.StartsWith("Attack")||requested.StartsWith("Spell")||requested.StartsWith("Birth"),terminal=requested.StartsWith("Death");
        if(pose==requested&&(!once||attackSerial==previousAttack))return;
        previousAttack=attackSerial;pose=requested;player[clip.name].wrapMode=terminal||once?WrapMode.ClampForever:WrapMode.Loop;
        if(once)player[clip.name].time=0;
        if(player.isPlaying)player.CrossFade(clip.name,.12f);else player.Play(clip.name);appearance.SetClip(clip.name);
    }
    static string[] Tokens(string value)=>(value??"").ToLowerInvariant().Split(new[]{' ', '-',','},StringSplitOptions.RemoveEmptyEntries);
    AnimationClip Choose(string requested) {
        var required=Tokens(requested);var properties=Tokens(animationProperties);
        return clips.Select(c=>new {clip=c,parts=Tokens(c.name)}).Where(v=>required.All(v.parts.Contains))
            .OrderByDescending(v=>properties.Count(v.parts.Contains)*100-v.parts.Count(p=>!int.TryParse(p,out _)&&!required.Contains(p)&&!properties.Contains(p))*10).Select(v=>v.clip).FirstOrDefault()
            ??clips.FirstOrDefault(c=>c.name.StartsWith(requested.Split(' ')[0],StringComparison.OrdinalIgnoreCase));
    }
    public void SamplePose(){if(player)player.Sample();}
    public AnimationClip StandClip()=>Choose("Stand");
    public float ClassicHeight=>reforged&&godotHeight>0?godotHeight:appearance?appearance.AuthoredHeight*animatedRoot.transform.localScale.x:0;
    public bool HeadBounds(out Bounds result) {
        result=new Bounds();bool found=false;
        foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>()) {
            var headBones=skin.bones.Select((b,i)=>new {b,i}).Where(v=>v.b&&(v.b.name.ToLowerInvariant().Contains("head")||v.b.name.ToLowerInvariant().Contains("hd_anim"))&&!v.b.name.ToLowerInvariant().Contains("mount")&&!v.b.name.ToLowerInvariant().StartsWith("ride_")&&!v.b.name.ToLowerInvariant().Contains("horse")&&!v.b.name.ToLowerInvariant().Contains("gryphon")&&!v.b.name.ToLowerInvariant().Contains("ref")&&!v.b.name.ToLowerInvariant().Contains("over")).Select(v=>v.i).ToHashSet();
            if(headBones.Count==0)continue;var weights=skin.sharedMesh.boneWeights;var baked=new Mesh();skin.BakeMesh(baked,godotHeight>0);var vertices=baked.vertices;
            for(int i=0;i<vertices.Length&&i<weights.Length;i++) {var w=weights[i];float influence=(headBones.Contains(w.boneIndex0)?w.weight0:0)+(headBones.Contains(w.boneIndex1)?w.weight1:0)+(headBones.Contains(w.boneIndex2)?w.weight2:0)+(headBones.Contains(w.boneIndex3)?w.weight3:0);if(influence<.4f)continue;var point=BoundsVertex(skin,vertices[i]);if(!found){result=new Bounds(point,Vector3.zero);found=true;}else result.Encapsulate(point);}
            if(Application.isPlaying)Destroy(baked);else DestroyImmediate(baked);
        }
        return found;
    }
    public Bounds BodyBounds() {
        bool found=false;var result=new Bounds(transform.position,Vector3.zero);
        foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.enabled) {
            var baked=new Mesh();skin.BakeMesh(baked,godotHeight>0);var vertices=baked.vertices;var mats=skin.sharedMaterials;
            for(int s=0;s<baked.subMeshCount;s++) {
                if(s<mats.Length&&mats[s].GetInt("_DstBlend")!=0)continue;if(godotHeight>0&&appearance&&!appearance.SurfaceVisible(s))continue;
                foreach(int index in baked.GetTriangles(s)){var point=BoundsVertex(skin,vertices[index]);if(!found){result=new Bounds(point,Vector3.zero);found=true;}else result.Encapsulate(point);}
            }
            Destroy(baked);
        }
        return found?result:GetComponentInChildren<SkinnedMeshRenderer>().bounds;
    }
    Vector3 BoundsVertex(SkinnedMeshRenderer skin,Vector3 point)=>reforged&&godotHeight>0?
        skin.transform.TransformPoint(point):skin.transform.position+skin.transform.rotation*point;
    public void Face(Vector2 direction,bool instant=false) {
        if(direction.sqrMagnitude<.001f)return;
        // The source Godot actor faces local +Z; glTFast preserves that axis.
        var rotation=Quaternion.LookRotation(new Vector3(direction.x,0,-direction.y));
        transform.rotation=instant?rotation:Quaternion.RotateTowards(transform.rotation,rotation,720*Time.unscaledDeltaTime);
    }
}
}
