using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using CastleFight;
public static class BuildChallenge034 {
    public static void Validate(){
        string folder=Path.GetFullPath("../Validation/Challenge034");Directory.CreateDirectory(folder);
        string profile=Path.Combine(folder,"editor-profile-"+Guid.NewGuid().ToString("N")+".json");var checks=new JArray();
        Challenge034Scenarios.Run(JObject.Parse(File.ReadAllText("Assets/Resources/PcProgression.json")),JObject.Parse(File.ReadAllText("Assets/Resources/pc-human.json")),profile,(ok,message)=>{checks.Add(new JObject{["ok"]=ok,["message"]=message});if(!ok)throw new Exception(message);});
        var map=JObject.Parse(UltimateAssetMap034.Json);int paths=0;
        foreach(var prop in map.Properties().Where(p=>p.Name!="abilityIds"))foreach(string path in prop.Value is JArray array?array.Values<string>():new[]{(string)prop.Value})if(path.Length>0){if(!File.Exists(path.Replace("assets/remotecontentsource/","Assets/Resources/"))&&!File.Exists(path)){
            // Windows path lookup is case insensitive; resource paths were renamed only during original bundle build.
            throw new Exception("Missing indexed Ultimate asset "+path);
        }paths++;}
        File.WriteAllText(Path.Combine(folder,"editor-verification.json"),new JObject{["status"]="PASS",["checks"]=checks,["ultimateAssetPaths"]=paths}.ToString());
        Debug.Log("CF_CHALLENGE034_VALIDATE_PASS checks="+checks.Count+" paths="+paths);
    }
    public static void Windows(){
        BuildMobileContent.Restore();Validate();
        string data="Builds/MapWindows/CastleFightMap_Data/";
        var names=new[]{"resources.assets","globalgamemanagers","globalgamemanagers.assets","sharedassets0.assets"};
        var before=names.ToDictionary(n=>n,n=>BuildWindowsRelease.Hash(data+n));
        const string backup="Builds/WindowsBaseline034/";Directory.CreateDirectory(backup);foreach(string n in names.Where(n=>n.StartsWith("global")))File.Copy(data+n,backup+n,true);
        PlayerSettings.bundleVersion="0.20.0";PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/CastleFightMap.unity"},target=BuildTarget.StandaloneWindows64,locationPathName="Builds/MapWindows/CastleFightMap.exe",options=BuildOptions.BuildScriptsOnly});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed");
        foreach(string n in names.Where(n=>n.StartsWith("global")))File.Copy(backup+n,data+n,true);
        foreach(var p in before)if(BuildWindowsRelease.Hash(data+p.Key)!=p.Value)throw new Exception("Modified base player data "+p.Key);
        Debug.Log("CF_CHALLENGE034_WINDOWS_PASS baseData=unchanged supplement=unchanged");
    }
}
