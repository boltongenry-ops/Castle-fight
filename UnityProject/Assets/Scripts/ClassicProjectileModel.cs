using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
[DefaultExecutionOrder(200)]
public sealed class ClassicProjectileModel:MonoBehaviour {
    public string key,profile;public GameObject geometry;public AnimationClip[] clips;
    Animation animationPlayer;NativeAppearance appearance;
    ProjectileBillboards billboards;Camera viewingCamera;bool terminal;
    static JObject sourceClips;JObject clipTimes;NativeMapEmitter[] emitters;string activeClip;
    public int BillboardCount=>billboards?.Count??0;
    public float BillboardAlignment(Camera camera)=>billboards==null?1:billboards.Alignment(camera);
    void Awake(){
        emitters=GetComponentsInChildren<NativeMapEmitter>();foreach(var emitter in emitters)emitter.density=Application.isMobilePlatform?.5f:1;
        if(sourceClips==null)sourceClips=JObject.Parse(ProjectileClipData.Json);clipTimes=sourceClips[key] as JObject;
        billboards=new ProjectileBillboards(key,geometry);
        if(geometry)foreach(var skin in geometry.GetComponentsInChildren<SkinnedMeshRenderer>())skin.updateWhenOffscreen=true;
        if(!geometry||clips==null||clips.Length==0){Play(false);return;}
        foreach(var animator in geometry.GetComponentsInChildren<Animator>())animator.enabled=false;
        animationPlayer=geometry.GetComponent<Animation>();if(!animationPlayer)animationPlayer=geometry.AddComponent<Animation>();
        animationPlayer.cullingType=AnimationCullingType.AlwaysAnimate;
        foreach(var clip in clips)animationPlayer.AddClip(clip,clip.name);
        if(!string.IsNullOrEmpty(profile)&&geometry.GetComponentsInChildren<SkinnedMeshRenderer>().Length==1){appearance=geometry.AddComponent<NativeAppearance>();appearance.ConfigureProfile(profile,animationPlayer);}
        Play(false);
    }
    public void Play(bool death){
        if(terminal&&!death)return;if(death)terminal=true;
        string phase=death?"Death":"Stand";if(activeClip==phase)return;activeClip=phase;
        var interval=clipTimes?[phase] as JArray;if(interval!=null)foreach(var emitter in emitters)emitter.SetClip(interval,!death);
        if(!animationPlayer)return;var clip=clips.FirstOrDefault(c=>c.name.StartsWith(phase,StringComparison.OrdinalIgnoreCase))??(death?null:clips.FirstOrDefault(c=>c.name.StartsWith("Birth",StringComparison.OrdinalIgnoreCase)));if(!clip)return;
        animationPlayer[clip.name].wrapMode=death?WrapMode.ClampForever:WrapMode.Loop;animationPlayer.Play(clip.name);if(appearance)appearance.SetClip(clip.name);
    }
    public void Face(Vector3 velocity){if(velocity.sqrMagnitude>.0001f)transform.rotation=Quaternion.LookRotation(velocity);}
    // A ground/target spell uses its authored one-shot animation, not the
    // looping projectile pose. The presentation lifetime is set by its pulse.
    public void PlayCast(){
        foreach(var emitter in emitters)emitter.SetClip(JArray.Parse(emitter.clipInterval),false);
        if(!animationPlayer)return;
        var clip=clips.FirstOrDefault(c=>c.name.StartsWith("Birth",StringComparison.OrdinalIgnoreCase))
            ??clips.FirstOrDefault(c=>c.name.StartsWith("Stand",StringComparison.OrdinalIgnoreCase))
            ??clips.FirstOrDefault();
        if(!clip)return;
        animationPlayer[clip.name].wrapMode=WrapMode.ClampForever;
        animationPlayer[clip.name].time=0;
        animationPlayer.Play(clip.name);
        if(appearance)appearance.SetClip(clip.name);
    }
    void LateUpdate(){if(!viewingCamera)viewingCamera=Camera.main;billboards?.Face(viewingCamera);}
    public float Finish(){
        float tail=0;
        foreach(var particle in GetComponentsInChildren<ParticleSystem>())tail=Mathf.Max(tail,particle.main.startLifetime.constantMax);
        if(clipTimes?["Death"] is JArray death){Play(true);tail+=((float)death[1]-(float)death[0])/1000;}
        else {
            foreach(var emitter in emitters)emitter.enabled=false;
            foreach(var particle in GetComponentsInChildren<ParticleSystem>())particle.Stop(false,ParticleSystemStopBehavior.StopEmitting);
            foreach(var renderer in GetComponentsInChildren<Renderer>())if(!(renderer is ParticleSystemRenderer))renderer.enabled=false;
        }
        return Mathf.Clamp(tail,0,1.5f);
    }
}
}
