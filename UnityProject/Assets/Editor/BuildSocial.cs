using System;using System.IO;using UnityEditor;using UnityEditor.Build.Reporting;using UnityEngine;
public static class BuildSocial {
    public static void BuildPlayerOnlyWindows(){
        AssetDatabase.Refresh();
        foreach(string name in new[]{"rank-emblems.png","castle-fight-logo.png","startup_intro.mp4"})if(!File.Exists("Assets/StreamingAssets/Frontend/"+name))throw new Exception("Missing frontend asset "+name);
        PlayerSettings.bundleVersion="0.20.0";AssetDatabase.SaveAssets();
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/CastleFightMap.unity"},target=BuildTarget.StandaloneWindows64,locationPathName="Builds/MapWindows/CastleFightMap.exe",options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Social player build failed");Debug.Log("CF_SOCIAL_FULL_PLAYER_BUILD_PASS version=0.20.0 classicAssetsPreserved=true");
    }
}
