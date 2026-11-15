using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildWindowsRelease {
    static string Hash(string file){
        if(Application.platform==RuntimePlatform.LinuxEditor){
            var start=new System.Diagnostics.ProcessStartInfo("sha256sum","-- \""+Path.GetFullPath(file)+"\""){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var process=System.Diagnostics.Process.Start(start)){
                string output=process.StandardOutput.ReadToEnd();process.WaitForExit();
                if(process.ExitCode!=0||output.Length<64||!output.Substring(0,64).All(Uri.IsHexDigit))throw new Exception("Native SHA-256 verification failed");
                return output.Substring(0,64);
            }
        }
        using(var stream=File.OpenRead(file))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    }
    public static void Build(){
        BuildMobileContent.Restore();
        var own=JObject.Parse(File.ReadAllText("Assets/Nerubian032/pc-nerubian.json"));
        var human=JObject.Parse(File.ReadAllText("Assets/Resources/pc-human.json"));
        int checks=0;FinalRaceScenarios.PoisonRefresh(own,human,(ok,message)=>{if(!ok)throw new Exception(message);checks++;});
        Debug.Log("CF_POISON_RELEASE_PASS checks="+checks);
        var index=JObject.Parse(File.ReadAllText("Assets/Centaur028/index.json"));
        var assets=index.Properties().Select(p=>(string)p.Value).Concat(new[]{"Assets/Centaur028/index.json"}).Distinct().ToArray();
        foreach(string asset in assets)if(!AssetDatabase.LoadMainAssetAtPath(asset))throw new Exception("Missing supplemental asset "+asset);
        const string supplement="Builds/CentaurWindows028";Directory.CreateDirectory(supplement);
        var bundle=BuildPipeline.BuildAssetBundles(supplement,new[]{new AssetBundleBuild{assetBundleName="centaur-028",assetNames=assets}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64);
        if(!bundle)throw new Exception("Windows supplemental content build failed");
        File.Copy(supplement+"/centaur-028","Assets/StreamingAssets/Frontend/centaur-028.windows",true);AssetDatabase.Refresh();
        bool scriptsOnly=Environment.GetEnvironmentVariable("CF_WINDOWS_SCRIPT_ONLY")=="1";
        string data="Builds/MapWindows/CastleFightMap_Data/";
        var before=new Dictionary<string,string>();
        if(scriptsOnly)foreach(string name in new[]{"resources.assets","globalgamemanagers","globalgamemanagers.assets","sharedassets0.assets"}){
            before[name]=Hash(data+name);
            if(name.StartsWith("globalgamemanagers",StringComparison.Ordinal)){
                Directory.CreateDirectory("Builds/WindowsBaseline");File.Copy(data+name,"Builds/WindowsBaseline/"+name,true);
            }
        }
        // Published player data retains its original 0.20 metadata; the game UI version is PcGame.Version.
        PlayerSettings.bundleVersion=scriptsOnly?"0.20.0":"0.32.1";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        const string output="Builds/MapWindows/CastleFightMap.exe";Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/CastleFightMap.unity"},target=BuildTarget.StandaloneWindows64,locationPathName=output,options=scriptsOnly?BuildOptions.BuildScriptsOnly:BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows release build failed: "+report.summary.result);
        // Unity can regenerate resource indices against the source checkout during a code-only build.
        // Keep the published indices paired with the unchanged published player data.
        if(scriptsOnly)foreach(string name in new[]{"globalgamemanagers","globalgamemanagers.assets"})
            File.Copy("Builds/WindowsBaseline/"+name,data+name,true);
        foreach(var row in before)if(Hash(data+row.Key)!=row.Value)throw new Exception("Scripts-only build changed base data: "+row.Key);
        File.Copy(supplement+"/centaur-028",data+"StreamingAssets/Frontend/centaur-028.windows",true);
        Debug.Log("CF_WINDOWS_RELEASE_PASS version=0.32.1 bytes="+report.summary.totalSize);
    }
}
