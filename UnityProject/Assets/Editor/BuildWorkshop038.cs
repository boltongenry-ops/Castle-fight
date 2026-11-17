using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildWorkshop038 {
    const string Forge="Assets/Workshop038/Forge.png", Bundle="workshop-038";
    const string Manifest="Assets/MobileBootstrap/content.json", Content="Builds/ContentAndroid021";

    static string Pack(BuildTarget target,string folder){
        var importer=AssetImporter.GetAtPath(Forge) as TextureImporter;
        if(!importer)throw new InvalidDataException("Workshop Forge texture is missing");
        importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=1024;importer.alphaSource=TextureImporterAlphaSource.None;
        importer.SaveAndReimport();
        Directory.CreateDirectory(folder);
        var build=new AssetBundleBuild{assetBundleName=Bundle,assetNames=new[]{Forge},addressableNames=new[]{"forge"}};
        if(!BuildPipeline.BuildAssetBundles(folder,new[]{build},
            BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,target))
            throw new InvalidOperationException("Could not pack workshop "+target);
        return folder+"/"+Bundle;
    }

    public static void Windows(){
        BuildMobileContent.Restore();
        string art=Pack(BuildTarget.StandaloneWindows64,"Builds/WorkshopWindows038");
        const string folder="Builds/MapWindows";
        PlayerSettings.bundleVersion="0.20.0";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=new[]{"Assets/Scenes/CastleFightMap.unity"},target=BuildTarget.StandaloneWindows64,
            locationPathName=folder+"/CastleFightMap.exe",options=BuildOptions.BuildScriptsOnly
        });
        if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Workshop Windows build failed");
        File.Copy(art,folder+"/CastleFightMap_Data/StreamingAssets/Frontend/"+Bundle+".windows",true);
        Debug.Log("CF_WORKSHOP038_WINDOWS_PASS");
    }

    public static void Android(){
        BuildMobileContent.Restore();
        string art=Pack(BuildTarget.Android,"Builds/WorkshopAndroid038");
        string hash;
        using(var file=File.OpenRead(art))using(var sha=SHA256.Create())
            hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
        string name=hash+".cfb";
        var manifest=JObject.Parse(File.ReadAllText(Manifest));
        var bundles=(JArray)manifest["bundles"];var resources=(JArray)manifest["resources"];
        foreach(var row in bundles.Where(row=>(string)row["name"]==Bundle).ToArray())row.Remove();
        foreach(var row in resources.Where(row=>(string)row["bundle"]==Bundle).ToArray())row.Remove();
        bundles.Add(new JObject{
            ["name"]=Bundle,["file"]=name,["size"]=new FileInfo(art).Length,["sha256"]=hash,["dependencies"]=new JArray(),
            ["url"]="https://github.com/boltongenry-ops/Castle-fight/releases/download/v0.38.0/"+name
        });
        resources.Add(new JObject{["key"]="Workshop038/Forge",["asset"]="forge",["bundle"]=Bundle});
        manifest["version"]="0.38.0";manifest["minimumPlayerVersion"]="0.38.0";
        Directory.CreateDirectory(Content);
        File.Copy(art,Content+"/"+name,true);
        File.WriteAllText(Manifest,manifest.ToString());File.WriteAllText(Content+"/manifest.json",manifest.ToString());
        AssetDatabase.ImportAsset(Manifest,ImportAssetOptions.ForceSynchronousImport);
        BuildAndroidRelease.Build();
        Debug.Log("CF_WORKSHOP038_ANDROID_PASS "+hash);
    }
}
