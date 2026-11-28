using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace CastleFight {
public sealed partial class PcGame {
    readonly Dictionary<int,GameObject> wispBursts=new Dictionary<int,GameObject>(); Material wispMaterial,wispMeshMaterial;GameObject wispMeshPrefab;
    void RenderWispBursts(){
        if(match==null)return;
        if(!HdParticleAssets040){foreach(var go in wispBursts.Values)if(go)Destroy(go);wispBursts.Clear();return;}
        if(!wispMaterial){var shader=HdFlameShader040();var texture=HdFxTexture040("wisp_dust");if(!shader||!texture){Debug.LogError("CF_NATIVE_WISP_MISSING");return;}wispMaterial=new Material(shader);wispMaterial.mainTexture=texture;Debug.Log("CF_NATIVE_WISP_READY");}
        foreach(var p in match.Pulses.Where(x=>x.fx=="wisp"&&ShowPulse036(x))){
            Art036.rendered.Add(p.id);
            if(!wispBursts.TryGetValue(p.id,out var go)){
                go=new GameObject("Reforged WispExplode PRE2");go.transform.SetParent(visuals.transform);go.transform.position=World(p.position);wispBursts[p.id]=go;
                if(!wispMeshPrefab)wispMeshPrefab=HdFxModel040("wisp_explode");
                if(wispMeshPrefab){var mesh=Instantiate(wispMeshPrefab,go.transform).GetComponent<PcModel>();foreach(var skin in mesh.GetComponentsInChildren<SkinnedMeshRenderer>()){
                    if(!wispMeshMaterial){wispMeshMaterial=new Material(skin.sharedMaterial);wispMeshMaterial.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);wispMeshMaterial.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);wispMeshMaterial.SetInt("_ZWrite",0);wispMeshMaterial.SetFloat("_Unlit",1);wispMeshMaterial.renderQueue=(int)RenderQueue.Transparent;}
                    var materials=skin.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=wispMeshMaterial;skin.sharedMaterials=materials;
                }mesh.Play("Birth");}
                WispEmitter(go.transform,"explode",.434f,.93f,10,35.2f,25.8f,11.3f,false);
                WispEmitter(go.transform,"ring",.367f,.48f,6.667f,.001f,16.6f,5,true);
            }
            foreach(var ps in go.GetComponentsInChildren<ParticleSystem>())if(page==Page.Match){if(ps.isPaused)ps.Play();}else if(ps.isPlaying)ps.Pause();
        }
        foreach(int id in wispBursts.Keys.Where(id=>!match.Pulses.Any(p=>p.id==id&&ShowPulse036(p))).ToArray()){var go=wispBursts[id];wispBursts.Remove(id);if(go){foreach(var model in go.GetComponentsInChildren<PcModel>())model.gameObject.SetActive(false);Destroy(go,1f);}}
    }
    void WispEmitter(Transform parent,string name,float delay,float life,float speed,float first,float middle,float last,bool ring){
        var go=new GameObject(name);go.transform.SetParent(parent,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=1.267f;main.startDelay=delay;main.startLifetime=life;main.startSpeed=speed;main.startSize=1;main.maxParticles=80;main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,75)});
        var shape=ps.shape;shape.enabled=true;shape.shapeType=ring?ParticleSystemShapeType.Circle:ParticleSystemShapeType.Sphere;shape.radius=.01f;if(ring)shape.rotation=new Vector3(90,0,0);
        var c=ps.colorOverLifetime;c.enabled=true;var gradient=new Gradient();
        if(ring)gradient.SetKeys(new[]{new GradientColorKey(new Color(.25098f,.964706f,.854902f),0),new GradientColorKey(new Color(.0509804f,.721569f,.972549f),.75f),new GradientColorKey(new Color(.0470588f,.988235f,.380392f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.75f),new GradientAlphaKey(0,1)});
        else gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.701961f,.309804f,.952941f),.5f),new GradientColorKey(new Color(.0470588f,.988235f,.380392f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.5f),new GradientAlphaKey(0,1)});c.color=gradient;
        var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,first/30),new Keyframe(ring ? .75f : .5f,middle/30),new Keyframe(1,last/30)));
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=wispMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;if(ring){renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=1.5f;}ps.Play();
    }
}
}
