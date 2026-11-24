using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

// Android release with immutable content and an optional rebuilt compatibility supplement.
public static class BuildAndroidRelease {
    public static void BuildUpdate(){PrepareContent();Build();}
    public static void PrepareContent(){
        BuildMobileContent.Restore();
        BuildNerubian033.Validate();
        const string output="Builds/CentaurAndroid028";
        Directory.CreateDirectory(output);
        var registry=JObject.Parse(File.ReadAllText("Assets/Centaur028/index.json"));
        var assets=registry.Properties().Select(p=>(string)p.Value).Concat(new[]{"Assets/Centaur028/index.json"}).Distinct().ToArray();
        var built=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="centaur-028",assetNames=assets}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.Android);
        if(!built)throw new Exception("Android supplement build failed");
        string source=output+"/centaur-028",hash;
        using(var stream=File.OpenRead(source))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        const string content="Builds/ContentAndroid021";
        Directory.CreateDirectory(content);
        var catalog=JObject.Parse(File.ReadAllText("Assets/MobileBootstrap/content.json"));
        var bundle=((JArray)catalog["bundles"]).Single(b=>(string)b["name"]=="centaur-028");
        bundle["file"]=hash+".cfb";bundle["sha256"]=hash;bundle["size"]=new FileInfo(source).Length;
        bundle["url"]="https://github.com/boltongenry-ops/Castle-fight/releases/download/v0.33.0/"+hash+".cfb";
        File.Copy(source,content+"/"+hash+".cfb",true);
        foreach(var entry in registry.Properties()){
            var row=((JArray)catalog["resources"]).FirstOrDefault(r=>(string)r["key"]==entry.Name);
            if(row==null)((JArray)catalog["resources"]).Add(new JObject{["key"]=entry.Name,["asset"]=entry.Value.DeepClone(),["bundle"]="centaur-028"});
            else{row["asset"]=entry.Value.DeepClone();row["bundle"]="centaur-028";}
        }
        catalog["version"]="0.33.0";catalog["minimumPlayerVersion"]="0.33.0";
        if(!ContentCatalog031.Compatible(catalog,"0.33.0")||ContentCatalog031.Compatible(catalog,"0.32.1"))throw new Exception("Android content compatibility check failed");
        string url=(string)bundle["url"];
        if(ContentCatalog031.AssetUrl(catalog,bundle)!=url)throw new Exception("Supplement URL override failed");
        var unchanged=((JArray)catalog["bundles"]).First(b=>b["url"]==null);
        if(ContentCatalog031.AssetUrl(catalog,unchanged)!=(string)catalog["baseUrl"]+"/"+(string)unchanged["file"])throw new Exception("Legacy content URL fallback failed");
        bundle["url"]="http://example.invalid/content.cfb";
        if(ContentCatalog031.Compatible(catalog,"0.33.0"))throw new Exception("Insecure asset URL accepted");
        bundle["url"]=url;
        Debug.Log("CF_CONTENT_URL_PASS override=verified legacyFallback=verified http=rejected");
        File.WriteAllText(content+"/manifest.json",catalog.ToString());
        File.WriteAllText("Assets/MobileBootstrap/content.json",catalog.ToString());
        AssetDatabase.ImportAsset("Assets/MobileBootstrap/content.json",ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("CF_ANDROID_SUPPLEMENT_PASS version=0.33.0 sha256="+hash);
    }
    public static void Build()=>BuildSigned(null,0);
    public static void BuildUnity045()=>BuildSigned("0.45.0",42);
    public static void BuildBattleSocial0451()=>BuildSigned("0.45.1",43);
    public static void BuildMythic046()=>BuildSigned("0.46.0",44);
    public static void BuildSquad0442()=>BuildSigned("0.44.2",41);
    public static void BuildHelp0441()=>BuildSigned("0.44.1",40);
    public static void BuildClan044()=>BuildSigned("0.44.0",39);
    public static void BuildBlackHandPreview()=>BuildSigned("0.43.0",38);
    static void BuildSigned(string version,int code){
        string key=Environment.GetEnvironmentVariable("CF_KEYSTORE_PATH");
        string password=Environment.GetEnvironmentVariable("CF_KEYSTORE_PASSWORD");
        if(string.IsNullOrEmpty(key)||!File.Exists(key)||string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Set CF_KEYSTORE_PATH and CF_KEYSTORE_PASSWORD outside the repository.");
        string root=Environment.GetEnvironmentVariable("CF_ANDROID_TOOLS_ROOT")??Path.Combine(EditorApplication.applicationContentsPath,"PlaybackEngines/AndroidPlayer");
        AndroidExternalToolsSettings.jdkRootPath=Path.Combine(root,"OpenJDK");
        AndroidExternalToolsSettings.sdkRootPath=Path.Combine(root,"SDK");
        AndroidExternalToolsSettings.ndkRootPath=Path.Combine(root,"NDK");
        const string manifest="Builds/ContentAndroid021/manifest.json";
        if(!File.Exists(manifest)){
            Directory.CreateDirectory(Path.GetDirectoryName(manifest));
            File.Copy("Assets/MobileBootstrap/content.json",manifest);
        }
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.castlefight.unitymap");
        bool custom=PlayerSettings.Android.useCustomKeystore;
        string oldKey=PlayerSettings.Android.keystoreName,oldAlias=PlayerSettings.Android.keyaliasName;
        try{
            PlayerSettings.Android.useCustomKeystore=true;
            PlayerSettings.Android.keystoreName=key;
            PlayerSettings.Android.keyaliasName=Environment.GetEnvironmentVariable("CF_KEY_ALIAS")??"androiddebugkey";
            PlayerSettings.Android.keystorePass=password;
            PlayerSettings.Android.keyaliasPass=password;
            if(version==null)BuildMobileContent.RebuildPlayer();
            else BuildMobileContent.RebuildPlayerForRelease(version,code);
        }finally{
            PlayerSettings.Android.keystorePass="";PlayerSettings.Android.keyaliasPass="";
            PlayerSettings.Android.useCustomKeystore=custom;
            PlayerSettings.Android.keystoreName=oldKey;PlayerSettings.Android.keyaliasName=oldAlias;
            AssetDatabase.SaveAssets();
        }
    }
}
