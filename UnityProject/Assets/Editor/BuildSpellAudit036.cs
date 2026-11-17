using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildSpellAudit036 {
    const string Root="Assets/SpellArt036", Bundle="spell-art-036";
    static JObject Read(string path)=>JObject.Parse(File.ReadAllText(path));
    static string[] Keys= {"fx_abc46fd107a7","fx_ef87a9d564e8","fx_04df853663a3","fx_403cdcbdb657","fx_1a6e13ab3326","fx_a6d7f1b4d60a"};
    static string Prefab(string key)=>Root+"/Prefabs/"+key+"-t0.prefab";
    static void Prepare(){
        foreach(string folder in new[]{"Prefabs","Materials","Clips"})Directory.CreateDirectory(Root+"/"+folder);
        AssetDatabase.Refresh();
        string data="Assets/StreamingAssets/SourceData/";
        var source=Read(data+"effect_models.json");var visibility=Read(data+"model_visibility.json");var particles=Read(data+"model_particles.json");
        var appearance=Read("Assets/Resources/native-appearance.json");
        var meshes=new JObject();var looks=new JObject();var clips=new JObject();var emitters=new JObject();
        var white=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ClassicProjectiles/White.asset");
        foreach(string key in Keys){
            var asset=source[key];string resource=(string)asset["resource"];
            meshes[key]="Assets/SourceAssets/models_effects/"+key+".glb";
            looks[key]=(appearance[key]??visibility[resource]).DeepClone();
            clips[key]=looks[key]["clips"].DeepClone();
            if(particles[resource] is JObject raw){
                var copy=(JObject)raw.DeepClone();
                foreach(var emitter in (JArray)copy["emitters"]){
                    string path="Assets/SourceAssets/particles/"+Path.GetFileName((string)emitter["texture"]);
                    if(!AssetDatabase.LoadAssetAtPath<Texture2D>(path))throw new InvalidDataException("Missing spell particle texture "+path);
                    emitter["unityTexture"]=path;
                }
                emitters[key]=copy;
            }
        }
        var view=new JObject{["assets"]=meshes,["appearance"]=looks,["clips"]=clips,["particles"]=emitters};
        foreach(string key in Keys){
            BuildClassicProjectiles.PrepareArt(view,key,0,Root,white,true);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab(key));var renderers=prefab.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0||renderers.SelectMany(r=>r.sharedMaterials).Any(m=>!m||!m.shader||!m.mainTexture))
                throw new InvalidDataException("Invalid spell prefab "+key);
        }
        AssetDatabase.SaveAssets();Debug.Log("CF_SPELL036_PREFABS_PASS count="+Keys.Length);
    }
    static string Pack(BuildTarget target,string output){
        Directory.CreateDirectory(output);
        var assets=new[]{new AssetBundleBuild{assetBundleName=Bundle,assetNames=Keys.Select(Prefab).ToArray()}};
        if(!BuildPipeline.BuildAssetBundles(output,assets,BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,target))
            throw new Exception("Spell art bundle failed: "+target);
        return Path.Combine(output,Bundle);
    }
    public static void Windows(){
        BuildMobileContent.Restore();Prepare();string pack=Pack(BuildTarget.StandaloneWindows64,"Builds/SpellArtWindows036");
        const string output="Builds/MapWindows";
        PlayerSettings.bundleVersion="0.20.0";PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/CastleFightMap.unity"},target=BuildTarget.StandaloneWindows64,locationPathName=output+"/CastleFightMap.exe",options=BuildOptions.BuildScriptsOnly});
        if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Spell audit Windows player failed");
        File.Copy(pack,output+"/CastleFightMap_Data/StreamingAssets/Frontend/"+Bundle+".windows",true);
        Debug.Log("CF_SPELL036_WINDOWS_BUILD_PASS");
    }
    public static void Android(){
        BuildMobileContent.Restore();Prepare();string pack=Pack(BuildTarget.Android,"Builds/SpellArtAndroid036"),digest;
        using(var stream=File.OpenRead(pack))using(var hash=SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        const string manifest="Assets/MobileBootstrap/content.json",output="Builds/ContentAndroid021";
        var index=Read(manifest);var bundles=(JArray)index["bundles"];var resources=(JArray)index["resources"];
        foreach(var row in bundles.Where(r=>(string)r["name"]==Bundle).ToArray())row.Remove();
        foreach(var row in resources.Where(r=>(string)r["bundle"]==Bundle).ToArray())row.Remove();
        string file=digest+".cfb";
        bundles.Add(new JObject{["name"]=Bundle,["file"]=file,["size"]=new FileInfo(pack).Length,["sha256"]=digest,["dependencies"]=new JArray(),["url"]="https://github.com/boltongenry-ops/Castle-fight/releases/download/v0.36.0/"+file});
        foreach(string key in Keys)resources.Add(new JObject{["key"]="SpellArt036/"+key,["asset"]=Prefab(key),["bundle"]=Bundle});
        index["version"]=index["minimumPlayerVersion"]="0.36.0";
        Directory.CreateDirectory(output);File.Copy(pack,output+"/"+file,true);File.WriteAllText(manifest,index.ToString());File.WriteAllText(output+"/manifest.json",index.ToString());
        AssetDatabase.ImportAsset(manifest,ImportAssetOptions.ForceSynchronousImport);
        BuildBurst035.Android();
        Debug.Log("CF_SPELL036_ANDROID_BUILD_PASS "+digest);
    }
}
