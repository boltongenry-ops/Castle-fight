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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildMobileContent {
    const string Source="Assets/RemoteContentSource",Streaming="Assets/RemoteStreamingSource",Output="Builds/ContentAndroid021",Scene="Assets/Scenes/CastleFightMap.unity";
    static readonly Dictionary<string,string> owner=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    static readonly List<JObject> lookup=new List<JObject>();
    static bool RuntimeAsset(string p)=>p.StartsWith("Assets/",StringComparison.Ordinal)&&!new[]{".cs",".dll",".asmdef",".asmref",".shader",".cginc",".hlsl",".compute"}.Contains(Path.GetExtension(p).ToLowerInvariant())&&!AssetDatabase.IsValidFolder(p);
    static string[] Paths(string directory)=>AssetDatabase.FindAssets("",new[]{directory}).Select(AssetDatabase.GUIDToAssetPath).Where(RuntimeAsset).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
    static void Claim(string group,IEnumerable<string> roots){foreach(string p in AssetDatabase.GetDependencies(roots.ToArray(),true).Where(RuntimeAsset).OrderBy(p=>p,StringComparer.Ordinal))if(!owner.ContainsKey(p))owner.Add(p,group);}
    static string Sha(string p){using(var f=File.OpenRead(p))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
    static void Move(string a,string b){if(!AssetDatabase.IsValidFolder(a))return;if(AssetDatabase.IsValidFolder(b))throw new Exception("Both content source folders exist; recover before building: "+a);string error=AssetDatabase.MoveAsset(a,b);if(error!="")throw new Exception(error);}
    [MenuItem("Castle Fight/Restore Android content source folders")]
    public static void Restore(){
        // The performance-test package creates a new Resources folder while the originals are moved.
        const string resources="Assets/Resources";
        if(AssetDatabase.IsValidFolder(Source)&&Directory.Exists(resources)){
            var generated=Directory.GetFiles(resources,"*",SearchOption.AllDirectories);
            var allowed=new[]{"PerformanceTestRunInfo.json","PerformanceTestRunInfo.json.meta","PerformanceTestRunSettings.json","PerformanceTestRunSettings.json.meta"};
            if(Directory.GetDirectories(resources).Length==0&&generated.All(p=>allowed.Contains(Path.GetFileName(p)))){
                string archive="Builds/AndroidBuildTelemetry/"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");Directory.CreateDirectory(archive);
                foreach(string file in generated)File.Move(file,Path.Combine(archive,Path.GetFileName(file)));
                AssetDatabase.DeleteAsset(resources);
            }
        }
        Move(Source,resources);Move(Streaming,"Assets/StreamingAssets");AssetDatabase.Refresh();
    }
    public static void ResumePlayer(){
        string previous=PlayerSettings.bundleVersion;
        try {
            Directory.CreateDirectory("Assets/MobileBootstrap");File.Copy(Output+"/manifest.json","Assets/MobileBootstrap/content.json",true);AssetDatabase.Refresh();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var host=new GameObject("Castle Fight downloadable Android startup");var boot=host.AddComponent<MobileContent>();boot.manifest=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/MobileBootstrap/content.json");boot.background=AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"/LoadingBackground.png");boot.hammer=AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"/LoadingOrcHammer.png");
            boot.font=AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:Font",new[]{Source}).First()));if(!boot.background||!boot.hammer||!boot.font)throw new Exception("Bootstrap art missing");
            var camera=new GameObject("Startup camera").AddComponent<Camera>();camera.transform.SetParent(host.transform);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=0;EditorSceneManager.SaveScene(scene,"Assets/MobileBootstrap/Startup.unity");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.castlefight.unitymap");PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.OpenGLES3});PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;PlayerSettings.Android.forceInternetPermission=true;
            File.WriteAllText("Assets/MobileBootstrap/link.xml","<linker><assembly fullname=\"Assembly-CSharp\" preserve=\"all\"/><assembly fullname=\"UnityEngine\" preserve=\"all\"/><assembly fullname=\"UnityEngine.AnimationModule\" preserve=\"all\"/><assembly fullname=\"UnityEngine.VideoModule\" preserve=\"all\"/><assembly fullname=\"UnityEngine.ParticleSystemModule\" preserve=\"all\"/></linker>");AssetDatabase.Refresh();RebuildPlayer();
        } finally {PlayerSettings.bundleVersion=previous;Restore();AssetDatabase.SaveAssets();}
    }
    public static void RebuildPlayer(){
        if(!File.Exists(Output+"/manifest.json"))throw new Exception("Build downloadable content first");
        string previous=PlayerSettings.bundleVersion;
        try{
            Move("Assets/Resources",Source);Move("Assets/StreamingAssets",Streaming);
            PlayerSettings.bundleVersion="0.35.0";PlayerSettings.Android.bundleVersionCode=Math.Max(23,PlayerSettings.Android.bundleVersionCode);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/MobileBootstrap/Startup.unity"},target=BuildTarget.Android,locationPathName="Builds/MapAndroid/CastleFight-Android-OnlineAssets.apk",assetBundleManifestPath=File.Exists(Output+"/ContentAndroid021.manifest")?Output+"/ContentAndroid021.manifest":null,options=BuildOptions.CompressWithLz4|(Environment.GetEnvironmentVariable("CF_ANDROID_CLEAN_BURST")=="1"?BuildOptions.CleanBuildCache:BuildOptions.None)});
            if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Small Android rebuild failed: "+result.summary.result);
            Debug.Log("CF_ANDROID_CONTENT_PLAYER_PASS apkBytes="+result.summary.totalSize);
        }finally{PlayerSettings.bundleVersion=previous;Restore();AssetDatabase.SaveAssets();}
    }
    public static void Build(){
        if(AssetDatabase.IsValidFolder(Source)||AssetDatabase.IsValidFolder(Streaming))throw new Exception("A previous interrupted build needs BuildMobileContent.Restore first");
        owner.Clear();lookup.Clear();Directory.CreateDirectory(Output);
        var oldVersion=PlayerSettings.bundleVersion;int oldCode=PlayerSettings.Android.bundleVersionCode;
        try {
            Move("Assets/Resources",Source);Move("Assets/StreamingAssets",Streaming);
            var resources=Paths(Source);var packs=MobileContent.RaceResources.Skip(1).ToDictionary(n=>n,n=>AssetDatabase.LoadAssetAtPath<PcRacePack>(Source+"/"+n+".asset"));
            if(packs.Any(p=>!p.Value))throw new Exception("Race pack missing");
            var common=resources.Where(p=>{var t=AssetDatabase.GetMainAssetTypeAtPath(p);return t==typeof(TextAsset)||t==typeof(Font)||t==typeof(AudioClip)||(t==typeof(Texture2D)&&!p.Substring(Source.Length+1).Contains("/"))||p.StartsWith(Source+"/PcStickers/")||p.StartsWith(Source+"/PcLobby/");}).ToArray();
            Claim("frontend",common);
            if(AssetDatabase.IsValidFolder("Assets/ClassicProjectiles/Prefabs")){
                var missileAssets=Paths("Assets/ClassicProjectiles/Prefabs").Concat(new[]{"Assets/ClassicProjectiles/runtime.json"}).ToArray();Claim("classic-missiles",missileAssets);
                foreach(string asset in missileAssets)lookup.Add(new JObject{["key"]="ClassicMissiles/"+(asset.EndsWith("runtime.json")?"index":Path.GetFileNameWithoutExtension(asset)),["asset"]=asset});
            }

            // Builders have their own small bundle: opening setup/reference book
            // never traverses a race pack and its complete model dependency tree.
            for(int race=1;race<MobileContent.RaceResources.Length;race++){var pack=packs[MobileContent.RaceResources[race]];var builder=AssetDatabase.GetAssetPath(pack.builders[0]);Claim("builders",new[]{builder});lookup.Add(new JObject{["key"]="MobileBuilders/"+race,["asset"]=builder});var data=AssetDatabase.GetAssetPath(pack.catalog);Claim("frontend",new[]{data});lookup.Add(new JObject{["key"]="MobileCatalogs/"+race,["asset"]=data});}
            // The scene must be isolated from non-scene assets in build maps.
            foreach(string p in AssetDatabase.GetDependencies(Scene,true).Where(p=>p!=Scene&&RuntimeAsset(p)))if(!owner.ContainsKey(p))owner[p]="world";
            owner[Scene]="mapscene";
            foreach(string dir in AssetDatabase.GetSubFolders(Source).OrderBy(x=>x,StringComparer.Ordinal))Claim("fx-"+Path.GetFileName(dir).ToLowerInvariant(),Paths(dir));
            // Ultimate goes last because its cross-race art reuses prior bundles.
            foreach(int race in Enumerable.Range(1,MobileContent.RaceResources.Length-1).OrderBy(n=>n==12?99:n))Claim("race-"+race,new[]{AssetDatabase.GetAssetPath(packs[MobileContent.RaceResources[race]])});
            Claim("frontend",resources.Where(p=>!owner.ContainsKey(p)));
            foreach(string p in resources){string rel=p.Substring(Source.Length+1);lookup.Add(new JObject{["key"]=rel.Substring(0,rel.Length-Path.GetExtension(rel).Length),["asset"]=p});}
            foreach(var row in lookup)row["bundle"]=owner[(string)row["asset"]];
            // Android-only overrides leave PC texture formats and GUIDs intact.
            int textures=0;
            AssetDatabase.StartAssetEditing();
            try{foreach(string path in owner.Keys){if(!(AssetImporter.GetAtPath(path) is TextureImporter importer))continue;var settings=importer.GetPlatformTextureSettings("Android");bool ui=path.StartsWith(Source+"/")||path.Contains("/pcui/")||path.Contains("/ui/");int max=ui?1024:512;settings.name="Android";settings.overridden=true;settings.maxTextureSize=max;settings.format=TextureImporterFormat.ETC2_RGBA8;settings.textureCompression=TextureImporterCompression.Compressed;settings.compressionQuality=50;importer.SetPlatformTextureSettings(settings);importer.SaveAndReimport();textures++;}}finally{AssetDatabase.StopAssetEditing();}
            Debug.Log("CF_CONTENT_ANDROID_TEXTURES count="+textures);
            AssetDatabase.SaveAssets();
            var definitions=owner.GroupBy(x=>x.Value).OrderBy(g=>g.Key,StringComparer.Ordinal).Select(g=>new AssetBundleBuild{assetBundleName=g.Key,assetNames=g.Select(x=>x.Key).OrderBy(x=>x,StringComparer.Ordinal).ToArray()}).ToArray();
            var bundles=BuildPipeline.BuildAssetBundles(Output,definitions,BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.Android);if(!bundles)throw new Exception("Android content bundle build failed");
            var rows=new JArray();
            foreach(string name in bundles.GetAllAssetBundles()){
                string path=Output+"/"+name,sha=Sha(path),file=sha+".cfb";File.Copy(path,Output+"/"+file,true);
                rows.Add(new JObject{["name"]=name,["size"]=new FileInfo(path).Length,["sha256"]=sha,["file"]=file,["dependencies"]=new JArray(bundles.GetAllDependencies(name))});
            }
            // Frontend paints/intro remain raw files, fetched individually.
            var media=new JArray();foreach(string p in Directory.GetFiles(Streaming,"*",SearchOption.AllDirectories).Where(p=>!p.EndsWith(".meta")&&!p.Contains("SourceData"))){string relative=p.Substring(Streaming.Length+1).Replace('\\','/');if(!relative.StartsWith("Frontend/")||relative.EndsWith(".windows"))continue;string sha=Sha(p),file=sha+".cfb";File.Copy(p,Output+"/"+file,true);media.Add(new JObject{["name"]=relative,["path"]=relative,["file"]=file,["sha256"]=sha,["size"]=new FileInfo(p).Length});}
            var manifest=new JObject{["schema"]=1,["version"]="0.31.0",["baseUrl"]="https://45-15-156-101.fatmetal.net/content/android-021-c5",["scene"]=Scene,["bundles"]=rows,["resources"]=new JArray(lookup),["streaming"]=media};
            File.WriteAllText(Output+"/manifest.json",manifest.ToString());
            Directory.CreateDirectory("Assets/MobileBootstrap");File.WriteAllText("Assets/MobileBootstrap/content.json",manifest.ToString());AssetDatabase.ImportAsset("Assets/MobileBootstrap/content.json",ImportAssetOptions.ForceSynchronousImport);
            var startup=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var host=new GameObject("Castle Fight downloadable Android startup");var boot=host.AddComponent<MobileContent>();boot.manifest=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/MobileBootstrap/content.json");boot.background=AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"/LoadingBackground.png");boot.hammer=AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"/LoadingOrcHammer.png");
            boot.font=AssetDatabase.LoadAssetAtPath<Font>(owner.Keys.First(p=>AssetDatabase.GetMainAssetTypeAtPath(p)==typeof(Font)));
            if(!boot.background||!boot.hammer||!boot.font)throw new Exception("Bootstrap art missing");
            var camera=new GameObject("Startup camera").AddComponent<Camera>();camera.transform.SetParent(host.transform);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=0;
            EditorSceneManager.SaveScene(startup,"Assets/MobileBootstrap/Startup.unity");
            PlayerSettings.bundleVersion="0.31.0";PlayerSettings.Android.bundleVersionCode=Math.Max(15,oldCode+1);PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.castlefight.unitymap");PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.OpenGLES3});PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;PlayerSettings.Android.forceInternetPermission=true;
            // Keep runtime components and animation/shader types referenced only by bundles.
            File.WriteAllText("Assets/MobileBootstrap/link.xml","<linker><assembly fullname=\"Assembly-CSharp\" preserve=\"all\"/><assembly fullname=\"UnityEngine\" preserve=\"all\"/><assembly fullname=\"UnityEngine.AnimationModule\" preserve=\"all\"/><assembly fullname=\"UnityEngine.VideoModule\" preserve=\"all\"/><assembly fullname=\"UnityEngine.ParticleSystemModule\" preserve=\"all\"/></linker>");AssetDatabase.Refresh();
            Directory.CreateDirectory("Builds/MapAndroid");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/MobileBootstrap/Startup.unity"},target=BuildTarget.Android,locationPathName="Builds/MapAndroid/CastleFight-Android-OnlineAssets.apk",assetBundleManifestPath=Output+"/ContentAndroid021.manifest",options=BuildOptions.CompressWithLz4});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Downloadable Android player build failed: "+report.summary.result);
            Debug.Log("CF_ANDROID_CONTENT_BUILD_PASS apkBytes="+report.summary.totalSize+" bundles="+rows.Count+" media="+media.Count);
        }finally{PlayerSettings.bundleVersion=oldVersion;Restore();AssetDatabase.SaveAssets();}
    }
}
