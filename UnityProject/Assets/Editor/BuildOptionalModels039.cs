using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildOptionalModels039 {
    const string Generated="Assets/GeneratedReforged";
    const string Vortex="Assets/ArchivedReforged/RefFx/";
    public static void Windows()=>Build("windows",BuildTarget.StandaloneWindows64);
    public static void WindowsRelease(){Windows();PlayerWindows();}
    public static void PrepareWindowsRelease(){
        FitOptionalModels039.PrepareAvailable();
        EditorUtility.UnloadUnusedAssetsImmediate();
        WindowsRelease();
    }
    public static void RetryWindowsRelease(){
        FitOptionalModels039.RetryAvailable();
        EditorUtility.UnloadUnusedAssetsImmediate();
        WindowsRelease();
    }
    public static void FitWideWindowsRelease(){
        FitOptionalModels039.FitWideModels();
        EditorUtility.UnloadUnusedAssetsImmediate();
        WindowsRelease();
    }
    public static void Android(){Build("android",BuildTarget.Android);BuildAndroidRelease.Build();}

    public static void PlayerWindows(){
        VerifiedModelIds(ReadModelArchive());
        BuildMobileContent.Restore();
        PlayerSettings.bundleVersion="0.20.0";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=new[]{"Assets/Scenes/CastleFightMap.unity"},target=BuildTarget.StandaloneWindows64,
            locationPathName="Builds/MapWindows/CastleFightMap.exe",options=BuildOptions.BuildScriptsOnly
        });
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Windows model settings build failed");
    }

    static void Build(string platform,BuildTarget target){
        var archive=ReadModelArchive();
        var modelIds=VerifiedModelIds(archive);
        string[] classicIds=ClassicFallbackActors();
        string[] portraitIds=RequiredActors().Except(classicIds,StringComparer.Ordinal).ToArray();
        var missingPortraits=portraitIds.Where(raw=>!File.Exists(PortraitPath(raw))).ToArray();
        if(missingPortraits.Length!=0)
            throw new InvalidDataException("Run FitOptionalModels039.PrepareAvailable with graphics; missing Reforged portraits: "+string.Join(", ",missingPortraits));
        var unprepared=modelIds.Where(raw=>!File.Exists($"{Generated}/{raw}-t0.prefab")||!File.Exists($"{Generated}/{raw}-t1.prefab")).ToArray();
        if(unprepared.Length!=0)throw new InvalidDataException("Missing fitted Reforged prefabs: "+string.Join(", ",unprepared));
        string folder="Builds/OptionalModels039/"+platform;
        Directory.CreateDirectory(folder);
        var paths=new List<string>();var addresses=new List<string>();
        foreach(string raw in modelIds)for(int team=0;team<2;team++){
            string path=$"{Generated}/{raw}-t{team}.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var model=prefab?prefab.GetComponent<PcModel>():null;
            if(!model||!model.reforged||model.raw!=raw||!model.animatedRoot||model.godotHeight<=0||model.clips.Length==0||model.clips.Any(clip=>!clip||!clip.legacy))
                throw new InvalidDataException("Invalid archived Reforged model "+path);
            var skins=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if(skins.Length==0||skins.Any(skin=>!skin.sharedMesh||skin.sharedMaterials.Any(m=>!m||!m.mainTexture||m.shader.name!="CastleFight/ReforgedUnit")))
                throw new InvalidDataException("Invalid archived Reforged material "+path);
            paths.Add(path);addresses.Add(ReforgedModelPack.Address(raw,team));
        }
        const string appearancePath="Assets/SourceAssets/Reforged/native-appearance-hd.json";
        var appearance=new JObject();
        foreach(var p in archive.Properties())appearance[p.Name+"_ref"]=p.Value["appearance"].DeepClone();
        File.WriteAllText(appearancePath,appearance.ToString(Newtonsoft.Json.Formatting.None));
        AssetDatabase.ImportAsset(appearancePath,ImportAssetOptions.ForceSynchronousImport);
        var builds=new List<AssetBundleBuild>{new AssetBundleBuild{
            assetBundleName="reforged-shared",assetNames=new[]{"Assets/Shaders/ReforgedUnit.shader",appearancePath,
                Vortex+"tornado.png",Vortex+"tornado.json",Vortex+"ReforgedEffect.shader"},
            addressableNames=new[]{"reforged-shader", "reforged-appearance", "vortex-texture", "vortex-animation", "vortex-shader"}
        }};
        var iconPaths=Directory.GetFiles("Assets/ArchivedReforged/ReforgedIcons","*.png")
            .Select(path=>path.Replace('\\','/')).OrderBy(path=>path,StringComparer.Ordinal).ToArray();
        builds.Add(new AssetBundleBuild{
            assetBundleName="reforged-icons",
            assetNames=iconPaths.Concat(portraitIds.Select(PortraitPath)).ToArray(),
            addressableNames=iconPaths.Select(path=>ReforgedModelPack.IconAddress(Path.GetFileNameWithoutExtension(path)))
                .Concat(portraitIds.Select(ReforgedModelPack.PortraitAddress)).ToArray()
        });
        // Assign shared textures explicitly so Unity does not duplicate them in each model bundle.
        var textureParts=AssetDatabase.GetDependencies(paths.ToArray(),true)
            .Where(path=>path.StartsWith("Assets/SourceAssets/Reforged/",StringComparison.Ordinal)&&path.EndsWith(".png",StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).OrderBy(path=>path,StringComparer.Ordinal).ToArray();
        const int texturesPerBundle=256;
        for(int i=0;i<textureParts.Length;i+=texturesPerBundle)
            builds.Add(new AssetBundleBuild{
                assetBundleName="reforged-textures-"+(i/texturesPerBundle+1),
                assetNames=textureParts.Skip(i).Take(texturesPerBundle).ToArray()
            });
        const int entriesPerBundle=48;
        for(int i=0;i<paths.Count;i+=entriesPerBundle){
            builds.Add(new AssetBundleBuild{assetBundleName="reforged-models-"+(i/entriesPerBundle+1),
                assetNames=paths.Skip(i).Take(entriesPerBundle).ToArray(),addressableNames=addresses.Skip(i).Take(entriesPerBundle).ToArray()});
        }
        var manifest=BuildPipeline.BuildAssetBundles(folder,builds.ToArray(),
            BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,target);
        if(!manifest)
            throw new InvalidOperationException("Building optional models failed for "+platform);
        var files=new JArray();var entered=new HashSet<string>(StringComparer.Ordinal);
        foreach(var build in builds)IndexBundle(build.assetBundleName);
        File.WriteAllText(folder+"/reforged-"+platform+".json",new JObject{
            ["format"]=2,["platform"]=platform,["files"]=files,["models"]=new JArray(modelIds),
            ["particleActors"]=new JArray("n02E"),["portraits"]=new JArray(portraitIds),["classicActors"]=new JArray(classicIds)
        }.ToString());
        Debug.Log("CF_OPTIONAL_MODELS039_PACK_PASS platform="+platform+" pairs="+modelIds.Length+" bundles="+files.Count);

        void IndexBundle(string name){
            if(!entered.Add(name))return;
            foreach(string dep in manifest.GetDirectDependencies(name))IndexBundle(dep);
            string file=folder+"/"+name;
            long size=new FileInfo(file).Length;
            if(size>=int.MaxValue)throw new InvalidDataException("Optional bundle exceeds the release asset size limit: "+name);
            string hash;
            using(var stream=File.OpenRead(file))using(var sha=SHA256.Create())
                hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            files.Add(new JObject{["name"]=name,["sha256"]=hash,["size"]=size});
        }
    }

    static JObject ReadModelArchive()=>JObject.Parse(File.ReadAllText("Assets/SourceAssets/Reforged/manifest.json"));
    static string PortraitPath(string raw)=>"Assets/SourceAssets/Reforged/Portraits/"+raw+".png";
    static IEnumerable<string> RequiredActors()=>JArray.Parse(File.ReadAllText("Assets/SourceAssets/Reforged/required-unit-models.json")).Values<string>();
    static string[] ClassicFallbackActors()=>JArray.Parse(File.ReadAllText("Assets/SourceAssets/Reforged/classic-fallback-models.json")).Values<string>().ToArray();

    static string[] VerifiedModelIds(JObject archive){
        var modelIds=archive.Properties().Select(row=>row.Name).Where(raw=>raw.Length==4)
            .OrderBy(raw=>raw,StringComparer.Ordinal).ToArray();
        var required=RequiredActors();
        var classic=ClassicFallbackActors();
        if(!File.Exists(Vortex+"tornado.png")||!File.Exists(Vortex+"tornado.json")||!File.Exists(Vortex+"ReforgedEffect.shader"))
            throw new InvalidDataException("Missing Reforged vortex particle files");
        string[] missing=required.Except(modelIds.Concat(new[]{"n02E"}),StringComparer.Ordinal).ToArray();
        if(!new HashSet<string>(classic,StringComparer.Ordinal).SetEquals(missing))
            throw new InvalidDataException("Unreviewed Reforged coverage change. Missing: "+string.Join(", ",missing));
        return modelIds;
    }
}
