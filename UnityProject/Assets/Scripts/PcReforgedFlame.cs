using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace CastleFight {
public sealed partial class PcGame {
    readonly Dictionary<int,ParticleSystem> nativeFlames=new Dictionary<int,ParticleSystem>();Material flameMaterial;
    void RenderNativeFlame(){
        if(match==null)return;
        if(!options.hdTextures){foreach(var p in nativeFlames.Values)if(p)Destroy(p.gameObject);nativeFlames.Clear();return;}
        if(!flameMaterial){var shader=Shader.Find("CastleFight/ReforgedFlame");var tex=ContentResources.Load<Texture2D>("RefFx/flame_clouds");if(!shader||!tex){Debug.LogError("CF_NATIVE_FLAME_MISSING");return;}flameMaterial=new Material(shader);flameMaterial.mainTexture=tex;Debug.Log("CF_NATIVE_FLAME_READY");}
        foreach(var fire in match.Fires){if(!nativeFlames.TryGetValue(fire.id,out var ps)){
            var go=new GameObject("Reforged FlameStrike PRE2");go.transform.SetParent(visuals.transform);ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=true;main.playOnAwake=false;main.startLifetime=1.2f;main.startSpeed=new ParticleSystem.MinMaxCurve(2f,3.33f);main.startSize=1;main.maxParticles=64;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;emission.rateOverTime=40;var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=10;shape.radius=.42f;shape.rotation=new Vector3(-90,0,0);
            var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(0,.913726f,.321569f),0),new GradientColorKey(new Color(1,.478431f,.0117647f),.5f),new GradientColorKey(new Color(.894118f,0,0),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.5f),new GradientAlphaKey(0,1)});color.color=gradient;
            var size=ps.sizeOverLifetime;size.enabled=true;var curve=new AnimationCurve(new Keyframe(0,26.9f/30),new Keyframe(.5f,16.6f/30),new Keyframe(1,7.401f/30));size.size=new ParticleSystem.MinMaxCurve(1,curve);
            var sheet=ps.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=8;sheet.numTilesY=8;sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,.5f));
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=flameMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;nativeFlames[fire.id]=ps;ps.Play();
        }ps.transform.position=World(fire.center)+Vector3.up*.2f;if(page==Page.Match){if(ps.isPaused)ps.Play();}else if(ps.isPlaying)ps.Pause();}
        foreach(int id in nativeFlames.Keys.Where(id=>!match.Fires.Any(f=>f.id==id)).ToArray()){var ps=nativeFlames[id];nativeFlames.Remove(id);if(ps){ps.Stop(false,ParticleSystemStopBehavior.StopEmitting);Destroy(ps.gameObject,1.3f);}}
    }
}
}
