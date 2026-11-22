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
            if(!await importer.InstantiateMainSceneAsync(model.transform))throw new InvalidDataException("mesh");
            model.transform.localRotation=Quaternion.Euler(0,90,0);
            if(role==8)model.transform.localScale=Vector3.one*1.12f;
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
            fighters[role,team]=root;
        }
    }
}
}
