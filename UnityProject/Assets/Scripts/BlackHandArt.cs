using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace CastleFight {
internal static class BlackHandArt {
    static readonly string[] names={"Footman","Sergeant","Captain","SiegeEngine","Crossbow","HeavyCrossbow","Warlock","Pikeman","Halberdier","Inquisitor","Headsman"};
    static readonly int[][] roleIndices={new[]{0},new[]{1},new[]{2},new[]{11,12,13,14},new[]{3},new[]{4},new[]{9},new[]{5},new[]{6},new[]{10},new[]{7,8}};
    static readonly GameObject[,] fighters=new GameObject[15,2];
    static readonly List<GltfImport> imports=new List<GltfImport>();
    static GameObject library;
    internal static bool Ready {get;private set;}
    public static GameObject Fighter(int row,int team)=>row>=0&&row<fighters.GetLength(0)?fighters[row,team]:null;
    public static IEnumerator Open() {
        if(library){while(!Ready)yield return null;yield break;}
        library=new GameObject("Black Legion model templates");
        library.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(library);
        for(int row=0;row<names.Length;row++){
            byte[] data=ReadAsset(names[row]);
            if(data==null)continue;
            var task=Import(row,data);while(!task.IsCompleted)yield return null;
            if(task.IsFaulted)Debug.LogWarning("Black Legion "+names[row]+": "+task.Exception.GetBaseException().Message);
        }
        yield return BlackHandBuildings.Open(library.transform);
        Ready=true;
    }
    static GameObject builderLibrary;
    static readonly GameObject[] builders=new GameObject[2];
    internal static GameObject Builder(GameObject human,int team) {
        if(builders[team])return builders[team];
        var texture=BlackHandUi.BuilderSkin();
        if(!human||!texture)return human;
        if(!builderLibrary){
            builderLibrary=new GameObject("Black Hand builder templates");
            builderLibrary.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(builderLibrary);
        }
        var builder=UnityEngine.Object.Instantiate(human,builderLibrary.transform,false);
        builder.name="Black Hand Builder "+team;
        foreach(var skin in builder.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
            var materials=skin.sharedMaterials;
            for(int i=0;i<materials.Length;i++){
                var material=new Material(materials[i]);
                material.mainTexture=texture;
                if(material.HasProperty("_TeamColor"))material.SetColor("_TeamColor",new Color(.065f,.06f,.055f));
                if(material.HasProperty("_HDPresent"))material.SetFloat("_HDPresent",0);
                materials[i]=material;
            }
            skin.sharedMaterials=materials;
        }
        builders[team]=builder;
        return builder;
    }

    internal static byte[] ReadAsset(string key) {
        var asset=Resources.Load<TextAsset>("BlackHand/"+key);
        if(asset) {
            byte[] bytes=asset.bytes;
            Resources.UnloadAsset(asset);
            return bytes;
        }
        if(Application.isMobilePlatform)return null;
        string path=MobileContent.Media("Frontend/BlackHand/"+key+".bytes");
        return File.Exists(path)?File.ReadAllBytes(path):null;
    }
    internal static Shader SurfaceShader() {
        if(Application.isMobilePlatform) {
            var mobile=Resources.Load<Shader>("BlackHand/ClanSurface");
            if(mobile)return mobile;
        }
        return Shader.Find("Standard")??Shader.Find("Unlit/Texture");
    }

    static readonly Material[] ammoMaterials=new Material[2];

    static Material AmmoMaterial(bool bubble) {
        int slot=bubble?1:0;
        if(ammoMaterials[slot])return ammoMaterials[slot];
        const int size=32;
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,true){wrapMode=TextureWrapMode.Clamp};
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            float radius=new Vector2((x+.5f)/16-1,(y+.5f)/16-1).magnitude;
            float alpha=bubble?Mathf.Clamp01(1-Mathf.Abs(radius-.72f)*6)+.22f*Mathf.Clamp01(1-radius):
                Mathf.Pow(Mathf.Clamp01(1-radius),1.3f);
            texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(alpha)));
        }
        texture.Apply(true,true);
        return ammoMaterials[slot]=new Material(Shader.Find("Sprites/Default")){mainTexture=texture};
    }

    static void MountAmmoFx(Transform model,int role) {
        // Object01 is the socket at the end of Arm1/Arm2/Arm3 on the siege rig.
        var socket=model.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Object01");
        bool venom=role==12, fire=role==13;
        for(int layer=0;layer<2;layer++){
            bool accent=layer==1;
            var obj=new GameObject(accent?"Ammunition motes":"Ammunition plume");
            obj.transform.SetParent(socket,false);
            var ps=obj.AddComponent<ParticleSystem>();
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;
            main.loop=true;main.prewarm=true;main.maxParticles=accent?8:18;
            main.simulationSpace=ParticleSystemSimulationSpace.Local;
            main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            main.startLifetime=accent?.75f:1.05f;main.startSpeed=0;
            main.startSize=accent?(fire?.04f:.09f):(fire?.26f:.32f);
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.075f;
            var emit=ps.emission;emit.rateOverTime=accent?7:16;
            var drift=ps.velocityOverLifetime;drift.enabled=true;drift.space=ParticleSystemSimulationSpace.World;
            drift.x=new ParticleSystem.MinMaxCurve(-.12f,.12f);
            drift.y=new ParticleSystem.MinMaxCurve(accent?.55f:.38f,fire?1.3f:.78f);
            drift.z=new ParticleSystem.MinMaxCurve(-.12f,.12f);
            var tint=ps.colorOverLifetime;tint.enabled=true;
            var gradient=new Gradient();
            Color start=venom?new Color(.72f,1,.12f):fire?new Color(1,.94f,.28f):new Color(.67f,.26f,1);
            Color end=venom?new Color(.16f,.52f,.03f):fire?new Color(1,.19f,.015f):new Color(.075f,.025f,.11f);
            gradient.SetKeys(new[]{new GradientColorKey(start,0),new GradientColorKey(end,.72f)},
                new[]{new GradientAlphaKey(.88f,0),new GradientAlphaKey(.8f,.42f),new GradientAlphaKey(0,1)});
            tint.color=gradient;
            var renderer=obj.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=AmmoMaterial(venom&&accent);
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
    }

    static async Task Import(int row,byte[] data) {
        var importer=new GltfImport();
        if(!await importer.LoadGltfBinary(data,importSettings:new ImportSettings{AnimationMethod=AnimationMethod.Legacy})){
            importer.Dispose();throw new InvalidDataException("GLB");
        }
        imports.Add(importer);
        var json=Newtonsoft.Json.Linq.JObject.Parse(System.Text.Encoding.UTF8.GetString(data,20,BitConverter.ToInt32(data,12)));
        var slots=json["meshes"][0]["primitives"].Select(primitive=>(int)json["materials"][(int)primitive["material"]]["pbrMetallicRoughness"]["baseColorTexture"]["index"]).ToArray();
        var palette=new Dictionary<(int,int),Material>();
        var shader=SurfaceShader();
        foreach(int role in roleIndices[row])for(int team=0;team<2;team++){
            var root=new GameObject("Black Legion "+names[row]+" "+team);
            root.transform.SetParent(library.transform,false);
            var model=new GameObject("Model");
            model.transform.SetParent(root.transform,false);
            // The GLB's Facing node already aligns the rig with PcModel.Face.
            if(!await importer.InstantiateMainSceneAsync(model.transform))throw new InvalidDataException("mesh");
            float scale = role >= 11 ? 3.0f : role == 6 ? 4.2f : role == 5 ? 3.7f : role == 8 ? 1.12f : 1f;
            model.transform.localScale = Vector3.one * scale;
            var view=root.AddComponent<PcModel>();
            view.raw="bU"+role.ToString("00");view.appearanceKey="blackhand";
            view.animatedRoot=model;view.clips=importer.GetAnimationClips();
            int primitiveIndex=0;
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
                var materials=skin.sharedMaterials;
                for(int k=0;k<materials.Length;k++){
                    int index=slots[primitiveIndex++];
                    if(!palette.TryGetValue((team,index),out var material)){
                    material=new Material(shader){name=names[row]+" "+index,mainTexture=importer.GetTexture(index)};
                    material.color=index==1?(team==0?new Color(.73f,.79f,.98f):new Color(.99f,.74f,.69f)):Color.white;
                    palette[(team,index)]=material;
                    }
                    materials[k]=material;
                }
                skin.sharedMaterials=materials;
            }
            if(primitiveIndex!=slots.Length)throw new InvalidDataException("Submesh count mismatch");
            if(!view.clips.Any(c=>c.name=="Death")||!model.GetComponentInChildren<SkinnedMeshRenderer>(true))throw new InvalidDataException("rig or death animation");
            if(role>=12 && role<=14)MountAmmoFx(model.transform,role);
            fighters[role,team]=root;
        }
    }
}
}
