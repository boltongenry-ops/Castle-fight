using System;using System.IO;using System.Linq;using System.Security.Cryptography;using UnityEditor;using UnityEditor.Build.Reporting;using UnityEngine;
public static class BuildFrontendRich {
    static string Hash(string file){
        string command="$f=[IO.File]::OpenRead('"+Path.GetFullPath(file).Replace("'","''")+"');$s=[Security.Cryptography.SHA256Cng]::new();try{[Convert]::ToBase64String($s.ComputeHash($f))}finally{$s.Dispose();$f.Dispose()}";
        var info=new System.Diagnostics.ProcessStartInfo("powershell.exe","-NoProfile -NonInteractive -Command \""+command+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var process=System.Diagnostics.Process.Start(info)){string result=process.StandardOutput.ReadToEnd(),error=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new Exception(error);return result.Trim();}
    }
    public static void RebuildWindows(){Build(true);}
    public static void RebuildPlayerOnlyWindows(){Build(false);}
    static void Build(bool package){
        const string output="Builds/MapWindows/CastleFightMap.exe";string data="Builds/MapWindows/CastleFightMap_Data/";
        if(PlayerSettings.bundleVersion!="0.20.0"||!File.Exists(data+"resources.assets")||!File.Exists("Assets/StreamingAssets/Frontend/startup_intro.mp4"))throw new Exception("Build matching 0.20.0 frontend player data first");
        var before=new[]{"resources.assets","globalgamemanagers","sharedassets0.assets"}.ToDictionary(name=>name,name=>Hash(data+name));
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/CastleFightMap.unity"},target=BuildTarget.StandaloneWindows64,locationPathName=output,options=BuildOptions.BuildScriptsOnly});
        if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Frontend runtime build failed: "+result.summary.result);
        if(before.Any(row=>Hash(data+row.Key)!=row.Value))throw new Exception("Script-only frontend build unexpectedly changed player assets");
        Debug.Log("CF_FRONTEND_SCRIPT_BUILD_PASS matchingPlayerData=true unchangedAssetHashes=true");
        if(!package)return;
        var info=new System.Diagnostics.ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+Path.GetFullPath("../PortableTools/Update-Portable.ps1")+"\" -BuildDirectory \""+Path.GetFullPath("Builds/MapWindows")+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var process=System.Diagnostics.Process.Start(info)){var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();process.WaitForExit();if(process.ExitCode!=0)throw new Exception(stdout.Result+stderr.Result);Debug.Log(stdout.Result);}
    }
}
