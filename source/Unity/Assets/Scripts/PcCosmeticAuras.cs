using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace CastleFight {
public sealed partial class PcGame {
    string cosmeticAuraId;Material cosmeticGlow;Texture2D cosmeticGlowTexture;Mesh cosmeticRuneMesh;Transform cosmeticRunes,cosmeticInner,cosmeticCrown;ParticleSystem cosmeticParticles;
    void CreateRichAura(string id){
        cosmeticAuraId=id;bool flame=id=="aura_flame";
        foreach(Transform child in builderCosmetic.transform)Destroy(child.gameObject);
        if(!cosmeticGlowTexture){cosmeticGlowTexture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Cosmetic soft light",wrapMode=TextureWrapMode.Clamp};var pixels=new Color[64*64];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float dx=(x-31.5f)/31.5f,dy=(y-31.5f)/31.5f;float glow=Mathf.Pow(Mathf.Clamp01(1-dx*dx-dy*dy),3);pixels[y*64+x]=new Color(1,1,1,glow);}cosmeticGlowTexture.SetPixels(pixels);cosmeticGlowTexture.Apply();}
        if(cosmeticGlow)Destroy(cosmeticGlow);cosmeticGlow=new Material(Shader.Find("Legacy Shaders/Particles/Additive")){name="Local cosmetic glow",mainTexture=cosmeticGlowTexture};cosmeticGlow.SetColor("_TintColor",Color.white);
        cosmeticRunes=new GameObject("Rotating cosmetic runes").transform;cosmeticRunes.SetParent(builderCosmetic.transform,false);
        if(!cosmeticRuneMesh){cosmeticRuneMesh=new Mesh{name="Cosmetic rune glyph"};cosmeticRuneMesh.vertices=new[]{new Vector3(-.09f,0,-.18f),new Vector3(-.035f,0,-.18f),new Vector3(.09f,0,.18f),new Vector3(.035f,0,.18f),new Vector3(-.13f,0,.03f),new Vector3(.12f,0,.03f),new Vector3(.12f,0,.08f),new Vector3(-.13f,0,.08f)};cosmeticRuneMesh.triangles=new[]{0,1,2,0,2,3,4,5,6,4,6,7};cosmeticRuneMesh.RecalculateNormals();}
        for(int i=0;i<12;i++){float angle=Mathf.PI*2*i/12;var rune=new GameObject("Cosmetic rune");rune.transform.SetParent(cosmeticRunes,false);rune.transform.localPosition=new Vector3(Mathf.Sin(angle)*1.36f,.015f,Mathf.Cos(angle)*1.36f);rune.transform.localRotation=Quaternion.Euler(0,angle*Mathf.Rad2Deg,0);rune.AddComponent<MeshFilter>().sharedMesh=cosmeticRuneMesh;rune.AddComponent<MeshRenderer>().sharedMaterial=builderCosmeticMaterial;}
        cosmeticInner=Ring("Counter rotating inner halo",builderCosmetic.transform).transform;cosmeticInner.localPosition=Vector3.up*.025f;cosmeticInner.localScale=Vector3.one*.81f;cosmeticInner.GetComponent<Renderer>().sharedMaterial=builderCosmeticMaterial;
        cosmeticCrown=new GameObject("Orbiting cosmetic lights").transform;cosmeticCrown.SetParent(builderCosmetic.transform,false);
        for(int i=0;i<(flame?5:3);i++){var obj=new GameObject("Soft orbit light");obj.transform.SetParent(cosmeticCrown,false);var ps=obj.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.startLifetime=.55f;main.startSpeed=0;main.startSize=flame?.24f:.17f;main.maxParticles=24;main.startColor=flame?new Color(1,.35f,.045f,.9f):new Color(.18f,1,.65f,.8f);main.simulationSpace=ParticleSystemSimulationSpace.World;var shape=ps.shape;shape.enabled=false;var emission=ps.emission;emission.rateOverTime=12;var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=cosmeticGlow;renderer.shadowCastingMode=ShadowCastingMode.Off;ps.Play();}
        var host=new GameObject(flame?"Rising embers":"Floating jade motes");host.transform.SetParent(builderCosmetic.transform,false);cosmeticParticles=host.AddComponent<ParticleSystem>();cosmeticParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var settings=cosmeticParticles.main;settings.loop=true;settings.startLifetime=new ParticleSystem.MinMaxCurve(1.1f,1.9f);settings.startSpeed=0;settings.startSize=new ParticleSystem.MinMaxCurve(flame?.11f:.06f,flame?.23f:.13f);settings.startColor=Color.white;settings.maxParticles=160;settings.simulationSpace=ParticleSystemSimulationSpace.World;
        var area=cosmeticParticles.shape;area.shapeType=ParticleSystemShapeType.Circle;area.radius=1.18f;area.radiusThickness=.3f;area.rotation=new Vector3(90,0,0);
        var emit=cosmeticParticles.emission;emit.rateOverTime=flame?55:28;
        var velocity=cosmeticParticles.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=new ParticleSystem.MinMaxCurve(-.12f,.12f);velocity.y=new ParticleSystem.MinMaxCurve(flame?.45f:.14f,flame?1.4f:.65f);velocity.z=new ParticleSystem.MinMaxCurve(-.12f,.12f);
        var colors=cosmeticParticles.colorOverLifetime;colors.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(flame?new Color(1,.72f,.15f):new Color(.3f,1,.8f),0),new GradientColorKey(flame?new Color(.8f,.09f,.015f):new Color(.04f,.5f,.28f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.85f,.15f),new GradientAlphaKey(0,1)});colors.color=gradient;
        var sizes=cosmeticParticles.sizeOverLifetime;sizes.enabled=true;sizes.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));var particleRenderer=cosmeticParticles.GetComponent<ParticleSystemRenderer>();particleRenderer.sharedMaterial=cosmeticGlow;particleRenderer.shadowCastingMode=ShadowCastingMode.Off;cosmeticParticles.Play();cosmeticParticles.Simulate(1,true,false);cosmeticParticles.Play();
    }
    void UpdateRichAura(string id){
        if(cosmeticAuraId!=id||!cosmeticParticles)CreateRichAura(id);
        float time=Time.unscaledTime;bool flame=id=="aura_flame";
        cosmeticRunes.localRotation=Quaternion.Euler(0,time*(flame?32:16),0);cosmeticInner.localRotation=Quaternion.Euler(0,-time*22,0);
        for(int i=0;i<cosmeticCrown.childCount;i++){float angle=time*(flame?1.4f:.8f)+i*Mathf.PI*2/cosmeticCrown.childCount;cosmeticCrown.GetChild(i).localPosition=new Vector3(Mathf.Sin(angle)*1.05f,.25f+(Mathf.Sin(time*1.2f+i)+1)*.4f,Mathf.Cos(angle)*1.05f);}
    }
    void DisposeRichAura(){if(cosmeticGlow)Destroy(cosmeticGlow);if(cosmeticGlowTexture)Destroy(cosmeticGlowTexture);if(cosmeticRuneMesh)Destroy(cosmeticRuneMesh);}
}
}
