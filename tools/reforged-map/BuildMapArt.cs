using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
public static class BuildMapArt {
    public static void Bake() {
        string input=Environment.GetEnvironmentVariable("CF_MAP_INPUT");
        string output=Environment.GetEnvironmentVariable("CF_MAP_OUTPUT");
        string dir="Assets/MapArt";
        Directory.CreateDirectory(dir);
        var files=new List<string>();
        var addresses=new List<string>();
        void Add(string name,string file){addresses.Add(name);files.Add(file);}
        var index=new JObject();
        foreach(string key in File.ReadAllLines(Path.Combine(input,"catalog.txt"))){
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();
            var triangles=new List<int[]>();var textures=new JArray();
            foreach(JObject part in JArray.Parse(File.ReadAllText(Path.Combine(input,key+".json")))){
                int start=vertices.Count;
                foreach(var v in part["points"])vertices.Add(new Vector3((float)v[0],(float)v[1],(float)v[2]));
                foreach(var n in part["normals"])normals.Add(new Vector3((float)n[0],(float)n[1],(float)n[2]));
                foreach(var p in part["uv"])uv.Add(new Vector2((float)p[0],(float)p[1]));
                var ids=part["indices"].Values<int>().Select(i=>i+start).ToArray();
                for(int i=0;i<ids.Length;i+=3){var tmp=ids[i];ids[i]=ids[i+2];ids[i+2]=tmp;}
                triangles.Add(ids);textures.Add((string)part["texture"]);
            }
            var mesh=new Mesh{name=key};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.subMeshCount=triangles.Count;
            for(int i=0;i<triangles.Count;i++)mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateBounds();string path=dir+"/"+key+".asset";
            if(!File.Exists(path))AssetDatabase.CreateAsset(mesh,path);
            else {EditorUtility.CopySerialized(mesh,AssetDatabase.LoadAssetAtPath<Mesh>(path));UnityEngine.Object.DestroyImmediate(mesh);}
            Add(key,path);index[key]=textures;
        }
        foreach(var tex in index.Properties().SelectMany(p=>p.Value.Values<string>()).Concat(Enumerable.Range(0,9).Select(i=>"ground"+i)).Distinct()){
            string path=dir+"/"+tex+".png";File.Copy(Path.Combine(input,tex+".png"),path,true);Add(tex,path);
        }
        string catalog=dir+"/catalog.json";File.WriteAllText(catalog,index.ToString());Add("catalog",catalog);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        var target=EditorUserBuildSettings.activeBuildTarget;
        foreach(string path in files.Where(s=>s.EndsWith(".png"))){
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.mipmapEnabled=true;imp.wrapMode=path.Contains("/ground")?TextureWrapMode.Clamp:TextureWrapMode.Repeat;
            imp.alphaSource=TextureImporterAlphaSource.FromInput;imp.filterMode=FilterMode.Trilinear;imp.anisoLevel=2;
            var settings=imp.GetPlatformTextureSettings(target==BuildTarget.Android?"Android":"Standalone");settings.overridden=true;
            settings.maxTextureSize=target==BuildTarget.Android?1024:2048;
            settings.format=target==BuildTarget.Android?TextureImporterFormat.ETC2_RGBA8:TextureImporterFormat.DXT5;
            imp.SetPlatformTextureSettings(settings);imp.SaveAndReimport();
        }
        string platform=target==BuildTarget.Android?"android":"windows";
        Directory.CreateDirectory(output);
        var bundle=new AssetBundleBuild{assetBundleName="map-"+platform+".cfb",assetNames=files.ToArray(),addressableNames=addresses.ToArray()};
        if(!BuildPipeline.BuildAssetBundles(output,new[]{bundle},BuildAssetBundleOptions.ChunkBasedCompression,target))throw new IOException("Map pack build failed");
        Debug.Log("MAP_PACK_BUILT "+platform+" "+index.Count);
    }
}
