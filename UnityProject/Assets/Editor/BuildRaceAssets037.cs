using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildRaceAssets037 {
    const string Bundle="race-builders";
    const string Player="Builds/MapWindows/CastleFightMap.exe";

    public static void Windows(){
        BuildMobileContent.Restore();
        var paths=new List<string>();
        var addresses=new List<string>();
        var supplement=JObject.Parse(File.ReadAllText("Assets/Centaur028/index.json"));
        for(int race=1;race<MobileContent.RaceResources.Length;race++){
            string key=MobileContent.RaceResources[race];
            string path=race==15?(string)supplement[key]:"Assets/Resources/"+key+".asset";
            string yaml=File.ReadAllText(path);
            paths.Add(PreviewPath(yaml,@"^  catalog: \{[^\r\n]*guid: ([0-9a-f]{32})",key));
            addresses.Add("catalogs/"+race);
            paths.Add(PreviewPath(yaml,@"^  builders:\r?\n  - \{[^\r\n]*guid: ([0-9a-f]{32})",key));
            addresses.Add("builders/"+race);
        }
        Debug.Log("CF_RACE_ASSETS037_PREVIEWS count="+paths.Count);
        string output="Builds/RaceBuildersWindows037";
        Directory.CreateDirectory(output);
        var definition=new AssetBundleBuild{assetBundleName=Bundle,assetNames=paths.ToArray(),addressableNames=addresses.ToArray()};
        if(!BuildPipeline.BuildAssetBundles(output,new[]{definition},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64))
            throw new InvalidOperationException("Desktop builder bundle failed");
        PlayerSettings.bundleVersion="0.20.0";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/CastleFightMap.unity"},target=BuildTarget.StandaloneWindows64,locationPathName=Player,options=BuildOptions.BuildScriptsOnly});
        if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Desktop player scripts failed");
        File.Copy(output+"/"+Bundle,"Builds/MapWindows/CastleFightMap_Data/StreamingAssets/Frontend/"+Bundle+".windows",true);
        Debug.Log("CF_RACE_ASSETS037_WINDOWS_PASS");
    }
    static string PreviewPath(string yaml,string pattern,string race){
        var reference=Regex.Match(yaml,pattern,RegexOptions.Multiline);
        string path=reference.Success?AssetDatabase.GUIDToAssetPath(reference.Groups[1].Value):"";
        if(string.IsNullOrEmpty(path))throw new InvalidDataException("Missing preview asset for "+race);
        return path;
    }
}
