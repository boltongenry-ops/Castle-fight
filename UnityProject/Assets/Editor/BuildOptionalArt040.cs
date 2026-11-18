using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class BuildOptionalArt040 {
    const string Source="Assets/SourceAssets/ReforgedArt040";
    const string Archived="Assets/ArchivedReforged/RefFx";
    const string Generated="Assets/GeneratedReforgedArt040";
    const string BundleName="reforged-art-040";
    public static void Windows()=>Build("windows",BuildTarget.StandaloneWindows64);
    public static void Android()=>Build("android",BuildTarget.Android);
    public static void AndroidPreview(){Android();BuildAndroidRelease.Build();}
    public static void WindowsPreview(){Windows();BuildFixture();BuildOptionalModels039.PlayerWindows();}

    static void BuildFixture(){
        const string output="Builds/OptionalArt040/fixture";
        Directory.CreateDirectory(output);
        var raw=new[]{"h051","h02Z","h030"};
        var models=raw.SelectMany(id=>new[]{(id,0),(id,1)}).ToArray();
        var common=new AssetBundleBuild{
            assetBundleName="fixture-common",
            assetNames=new[]{"Assets/SourceAssets/Reforged/native-appearance-hd.json"},
            addressableNames=new[]{"reforged-appearance"}
        };
        var actors=new AssetBundleBuild{
            assetBundleName="fixture-actors",
            assetNames=models.Select(row=>$"Assets/GeneratedReforged/{row.id}-t{row.Item2}.prefab").ToArray(),
            addressableNames=models.Select(row=>ReforgedModelPack.Address(row.id,row.Item2)).ToArray()
        };
        if(!BuildPipeline.BuildAssetBundles(output,new[]{common,actors},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64))
            throw new InvalidOperationException("Unable to build Reforged test fixtures");
        var files=new JArray();
        foreach(string name in new[]{common.assetBundleName,actors.assetBundleName}){
            string path=output+"/"+name;
            files.Add(new JObject{["name"]=name,["sha256"]=FileHash(path),["size"]=new FileInfo(path).Length});
        }
        File.WriteAllText(output+"/reforged-windows.json",new JObject{["format"]=2,["platform"]="windows",["files"]=files}.ToString());
    }

    static void Build(string platform,BuildTarget target){
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var paths=new List<string>();
        var addresses=new List<string>();
        Add(Source+"/icons.json","art-icons");
        Add(Source+"/effects.json","art-effects");
        Add(Archived+"/ReforgedEffect.shader","fx-040-shader");
        Add(Archived+"/FlameParticle.shader","fx-040-flame-shader");
        var icons=JObject.Parse(File.ReadAllText(Source+"/icons.json"));
        foreach(string key in icons.Properties().Select(p=>(string)p.Value).Distinct().OrderBy(key=>key,StringComparer.Ordinal))
            Add($"{Source}/Icons/{key}.png","art-icon/"+key);
        var fx=JObject.Parse(File.ReadAllText(Source+"/effects.json"));
        var sheets=new HashSet<string>(StringComparer.Ordinal);
        foreach(string property in new[]{"actors","modelEffects"})
            foreach(var row in ((JObject)fx[property]).Properties())
                foreach(var layer in row.Value)sheets.Add((string)layer["key"]);
        foreach(var row in ((JObject)fx["specialActors"]).Properties())
            foreach(var state in ((JObject)row.Value).Properties())
                if((string)state.Value!=null)sheets.Add((string)state.Value);
        foreach(string path in Directory.GetFiles(Archived,"*.json"))sheets.Add(Path.GetFileNameWithoutExtension(path));
        foreach(string key in sheets.OrderBy(key=>key,StringComparer.Ordinal)){
            string folder=key.StartsWith("fx-",StringComparison.Ordinal)?Source+"/Effects":Archived;
            Add($"{folder}/{key}.png","fx-sheet/"+key);
            Add($"{folder}/{key}.json","fx-layout/"+key);
        }
        foreach(string key in new[]{"flame_clouds","wisp_dust"})Add($"{Archived}/{key}.png","fx-texture/"+key);
        foreach(string path in Directory.GetFiles(Archived,"*.prefab").OrderBy(path=>path,StringComparer.Ordinal))
            Add(path,"fx-model/"+Path.GetFileNameWithoutExtension(path));
        int geometry=PrepareGeometry(Add);
        string output="Builds/OptionalArt040/"+platform;
        Directory.CreateDirectory(output);
        var build=new AssetBundleBuild {assetBundleName=BundleName,assetNames=paths.ToArray(),addressableNames=addresses.ToArray()};
        var manifest=BuildPipeline.BuildAssetBundles(output,new[]{build},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,target);
        if(!manifest)throw new InvalidOperationException("Unable to build optional Reforged art for "+platform);
        string bundle=output+"/"+BundleName;
        string hash=FileHash(bundle);
        long size=new FileInfo(bundle).Length;
        File.WriteAllText(output+"/reforged-art-"+platform+".json",new JObject{
            ["format"]=1,["platform"]=platform,["sha256"]=hash,["size"]=size,
            ["iconKeys"]=icons.Count,["sheets"]=sheets.Count,["geometry"]=geometry
        }.ToString());
        Debug.Log($"CF_REFORGED_ART040_BUILD_PASS platform={platform} icons={icons.Count} sheets={sheets.Count} geometry={geometry} size={size} sha256={hash}");

        void Add(string path,string address){
            if(!File.Exists(path))throw new FileNotFoundException("Required Reforged art is missing",path);
            paths.Add(path.Replace('\\','/'));addresses.Add(address);
        }
    }

    static string FileHash(string path){
        using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    }

    static int PrepareGeometry(Action<string,string> add){
        const string index=Source+"/geometry.json";
        if(!File.Exists(index))return 0;
        var geometry=JObject.Parse(File.ReadAllText(index));
        string particlesPath=Source+"/particles.json";
        if(File.Exists(particlesPath))geometry.Merge(JObject.Parse(File.ReadAllText(particlesPath)));
        var keys=((JObject)geometry["assets"]).Properties().Select(p=>p.Name)
            .Concat((geometry["particles"] as JObject??new JObject()).Properties().Select(p=>p.Name))
            .Distinct().OrderBy(key=>key,StringComparer.Ordinal).ToArray();
        foreach(string folder in new[]{"Prefabs","Materials","Clips"})Directory.CreateDirectory(Generated+"/"+folder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var white=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ClassicProjectiles/White.asset");
        foreach(string key in keys)for(int team=0;team<2;team++){
            string prefab=$"{Generated}/Prefabs/{key}-t{team}.prefab";
            BuildClassicProjectiles.PrepareArt(geometry,key,team,Generated,white,false);
            add(prefab,$"fx-geometry/{key}/{team}");
        }
        AssetDatabase.SaveAssets();
        return keys.Length;
    }
}
