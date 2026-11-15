using System;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
public static class BuildCorruptedParticles {
    static T Save<T>(T value,string path) where T:UnityEngine.Object {var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing){EditorUtility.CopySerialized(value,existing);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(existing);return existing;}AssetDatabase.CreateAsset(value,path);return value;}
    public static void Add(GameObject instance,JObject profile,JArray interval,string key) {
        float scale=(float?)profile["scale"]??1f/30;int i=0;
        foreach(JObject e in (JArray)profile["emitters"]??new JArray()) {
            var obj=new GameObject("Native emitter "+i);string bone=(string)e["bone"];var parent=instance.transform;
            if(!string.IsNullOrEmpty(bone))parent=instance.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name==bone)??parent;
            obj.transform.SetParent(parent,false);var offset=(JArray)e["bone_offset"];var p=(JArray)e["position"];
            obj.transform.localPosition=parent!=instance.transform&&offset!=null?new Vector3(-(float)offset[0],(float)offset[1],(float)offset[2]):new Vector3(-(float)p[0],(float)p[2],-(float)p[1])*scale;
            // Unity box emitters launch along local +Z; native WC3 emitters use +Y.
            obj.transform.localRotation=Quaternion.Euler(-90,0,0);
            var ps=obj.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.startLifetime=Mathf.Max(.05f,(float)e["lifetime"]);main.loop=true;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            float speed=(float)e["speed"]*scale,variation=(float)e["variation"];main.startSpeed=new ParticleSystem.MinMaxCurve(speed*(1-variation),speed*(1+variation));main.startSize=1;
            float maxRate=(float)e["rate"];foreach(var r in (JArray)(e["rate_track"]?["values"])??new JArray())maxRate=Mathf.Max(maxRate,(float)r);
            main.maxParticles=Mathf.Clamp(Mathf.RoundToInt(maxRate*(float)e["lifetime"]),1,256);
            var force=ps.forceOverLifetime;force.enabled=true;force.space=ParticleSystemSimulationSpace.Local;force.z=-(float)e["gravity"]*scale;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3((float)e["width"]*scale,(float)e["length"]*scale,.001f);shape.rotation=Vector3.zero;shape.randomDirectionAmount=Mathf.Clamp01((float)e["latitude"]/180);
            var colors=(JArray)e["colors"];var alphas=(JArray)e["alphas"];var sizes=(JArray)e["scales"];var ck=new GradientColorKey[3];var ak=new GradientAlphaKey[3];var curve=new AnimationCurve();float mid=Mathf.Clamp((float)e["middle"],.01f,.99f);
            for(int k=0;k<3;k++){float t=k==0?0:k==1?mid:1;var c=(JArray)colors[k];ck[k]=new GradientColorKey(new Color((float)c[0],(float)c[1],(float)c[2]),t);ak[k]=new GradientAlphaKey((float)alphas[k]/255,t);curve.AddKey(t,(float)sizes[k]*scale);}
            var gradient=new Gradient();gradient.SetKeys(ck,ak);var color=ps.colorOverLifetime;color.enabled=true;color.color=gradient;var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,curve);
            var atlas=ps.textureSheetAnimation;atlas.enabled=true;atlas.numTilesX=Mathf.Max(1,(int)e["columns"]);atlas.numTilesY=Mathf.Max(1,(int)e["rows"]);atlas.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));
            int filter=(int)e["filter"];string shader=filter==1?"Legacy Shaders/Particles/Additive":filter==2||filter==3?"Legacy Shaders/Particles/Multiply":"Legacy Shaders/Particles/Alpha Blended";
            var mat=new Material(Shader.Find(shader)){mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>((string)e["unityTexture"])};mat=Save(mat,$"Assets/Resources/CorruptedFxMaterials/{key}-particle{i}.mat");
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;
            var controller=obj.AddComponent<NativeMapEmitter>();controller.density=1;controller.emitter=ps;controller.sourceProfile=e.ToString(Newtonsoft.Json.Formatting.None);controller.clipInterval=interval.ToString(Newtonsoft.Json.Formatting.None);i++;
        }
    }
}
