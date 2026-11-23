using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

public static class BuildDedicatedCastleFight {
    public static void Linux(){
        const string scene="Assets/DedicatedCastleFight.unity";
        if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneLinux64))
            throw new InvalidOperationException("Install Unity Linux Dedicated Server Build Support.");
        if(File.Exists(scene))throw new InvalidOperationException("Use a clean dedicated build project.");
        EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene),scene);
        string output=Environment.GetEnvironmentVariable("CF_SERVER_OUTPUT")??"Builds/Dedicated/CastleFightServer.x86_64";
        try{
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{scene},
                locationPathName=output,
                target=BuildTarget.StandaloneLinux64,
                subtarget=(int)StandaloneBuildSubtarget.Server,
                options=BuildOptions.CompressWithLz4
            });
            if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Dedicated build failed.");
        }finally{AssetDatabase.DeleteAsset(scene);}
    }
}
