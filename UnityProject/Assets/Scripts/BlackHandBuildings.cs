using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GLTFast;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace CastleFight {
internal static class BlackHandBuildings {
    const int Count = 20;
    const int IconSize = 128;
    static readonly GameObject[,] models = new GameObject[Count,2];
    static readonly Texture2D[] icons = new Texture2D[Count];
    static readonly List<GltfImport> imports = new List<GltfImport>();
    static readonly Color[] workshopColors = {
        new Color(.71f,.77f,.83f), new Color(.54f,.85f,.49f),
        new Color(1f,.59f,.31f), new Color(.77f,.55f,.91f)
    };

    public static GameObject Model(int row,int team) => row>=0 && row<Count ? models[row,team] : null;
    public static Texture2D Icon(int row) => row>=0 && row<Count ? icons[row] : null;

    public static IEnumerator Open(Transform parent) {
        for(int row=0;row<Count;row++) {
            if(row>=12&&row<=14)continue;
            string key="Buildings/bH"+row.ToString("00");
            byte[] data=BlackHandArt.ReadAsset(key);
            if(data==null)continue;
            var task=Import(row,data,parent);
            while(!task.IsCompleted)yield return null;
            if(task.IsFaulted)Debug.LogWarning(key+": "+task.Exception.GetBaseException().Message);
        }
        LoadIcons();
    }

    static async Task Import(int row,byte[] data,Transform parent) {
        var importer = new GltfImport();
        if(!await importer.LoadGltfBinary(data,importSettings:new ImportSettings {AnimationMethod=AnimationMethod.Legacy})) {
            importer.Dispose();
            throw new InvalidDataException("GLB");
        }
        imports.Add(importer);
        int jsonSize=BitConverter.ToInt32(data,12);
        var document=JObject.Parse(System.Text.Encoding.UTF8.GetString(data,20,jsonSize));
        var slots=document["meshes"][0]["primitives"].Select(p=>document["materials"][(int)p["material"]]).ToArray();
        var shader=BlackHandArt.SurfaceShader();
        var clips=importer.GetAnimationClips()??Array.Empty<AnimationClip>();
        var stand=clips.FirstOrDefault(clip=>clip.name=="Stand");
        var source=new GameObject("Building import");
        source.transform.position=new Vector3(0,-1000,0);
        Mesh mesh;
        try {
            if(!await importer.InstantiateMainSceneAsync(source.transform))throw new InvalidDataException("Building geometry");
            stand?.SampleAnimation(source,0);
            mesh=Bake(source.transform,slots.Length);
            Anchor(mesh,row);
        } finally { UnityEngine.Object.Destroy(source); }

        int last=row==11?14:row;
        for(int variant=row;variant<=last;variant++) {
            var materials=new Material[slots.Length];
            for(int slot=0;slot<slots.Length;slot++) {
                var descriptor=slots[slot];
                string texture=(string)descriptor["name"];
                int index=(int)descriptor["pbrMetallicRoughness"]["baseColorTexture"]["index"];
                var material=new Material(shader){name=texture,mainTexture=importer.GetTexture(index)};
                if((string)descriptor["alphaMode"]=="MASK") {
                    material.SetOverrideTag("RenderType","TransparentCutout");
                    if(material.HasProperty("_Mode"))material.SetFloat("_Mode",1);
                    material.SetFloat("_Cutoff",.45f);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.renderQueue=(int)RenderQueue.AlphaTest;
                }
                if(texture=="cw_gilneas_buildings.blp")
                    material.color=row==11?workshopColors[variant-row]:new Color(.77f,.79f,.83f);
                materials[slot]=material;
            }
            for(int team=0;team<2;team++) {
                string raw=row==19?"h008":"bH"+variant.ToString("00");
                var root=new GameObject("Black Hand building "+raw+" team "+team);
                root.transform.SetParent(parent,false);
                var body=new GameObject("Model");
                body.transform.SetParent(root.transform,false);
                body.AddComponent<MeshFilter>().sharedMesh=mesh;
                body.AddComponent<MeshRenderer>().sharedMaterials=materials;
                var view=root.AddComponent<PcModel>();
                view.raw=raw;
                view.appearanceKey="blackhand";
                view.animatedRoot=body;
                view.clips=Array.Empty<AnimationClip>();
                models[variant,team]=root;
            }
        }
    }

    static Mesh Bake(Transform source,int slotCount) {
        var parts=new List<CombineInstance>(slotCount);
        var temporary=new List<Mesh>();
        try {
            foreach(var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                var baked=new Mesh();
                temporary.Add(baked);
                skin.BakeMesh(baked);
                Matrix4x4 transform=source.worldToLocalMatrix*skin.transform.localToWorldMatrix;
                for(int slot=0;slot<baked.subMeshCount;slot++)
                    parts.Add(new CombineInstance {mesh=baked,subMeshIndex=slot,transform=transform});
            }
            if(parts.Count!=slotCount)throw new InvalidDataException("Building material slots");
            var mesh=new Mesh {indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(parts.ToArray(),false,true);
            return mesh;
        } finally {
            foreach(var mesh in temporary)UnityEngine.Object.Destroy(mesh);
        }
    }

    static void Anchor(Mesh mesh,int row) {
        Vector3[] vertices=mesh.vertices;
        var bounds=new Bounds(vertices[0],Vector3.zero);
        for(int sub=0;sub<mesh.subMeshCount;sub++)foreach(int vertex in mesh.GetIndices(sub))
            bounds.Encapsulate(vertices[vertex]);
        if(bounds.size.y<=.01f)throw new InvalidDataException("Empty building bounds");
        float span=row==19?2.7f:row==15||row==18?4.3f:5.7f;
        float roof=row==15||row==18?7.0f:row==19?2.2f:6.3f;
        float scale=Mathf.Min(span/Mathf.Max(.05f,bounds.size.x,bounds.size.z),roof/bounds.size.y);
        var origin=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        for(int i=0;i<vertices.Length;i++)vertices[i]=(vertices[i]-origin)*scale;
        mesh.vertices=vertices;
        mesh.bounds=new Bounds((bounds.center-origin)*scale,bounds.size*scale);
    }

    static void LoadIcons() {
        byte[] data=BlackHandArt.ReadAsset("Buildings/Icons");
        if(data==null)return;
        var atlas=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try {
            if(!atlas.LoadImage(data)||atlas.width!=5*IconSize||atlas.height!=4*IconSize)return;
            for(int row=0;row<Count;row++) {
                var icon=new Texture2D(IconSize,IconSize,TextureFormat.RGBA32,false);
                icon.SetPixels(atlas.GetPixels(row%5*IconSize,(3-row/5)*IconSize,IconSize,IconSize));
                icon.Apply(false,true);
                icon.name="BlackHandBuildingIcon"+row;
                icons[row]=icon;
            }
        } finally { UnityEngine.Object.Destroy(atlas); }
    }
}
}
