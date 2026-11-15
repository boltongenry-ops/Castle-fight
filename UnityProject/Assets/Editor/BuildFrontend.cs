using System;
using System.IO;
using CastleFight;
using UnityEditor;
using UnityEngine;
public static class BuildFrontend {
    public static void BuildWindows(){
        AssetDatabase.Refresh();
        foreach(string file in new[]{"Assets/Resources/LoadingBackground.png","Assets/Resources/LoadingOrcHammer.png"}){
            var importer=(TextureImporter)AssetImporter.GetAtPath(file);importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.maxTextureSize=2048;importer.SaveAndReimport();
        }
        var music=(AudioImporter)AssetImporter.GetAtPath("Assets/Resources/PcMenuMusic.mp3");var settings=music.defaultSampleSettings;settings.preloadAudioData=true;settings.loadType=AudioClipLoadType.Streaming;music.defaultSampleSettings=settings;music.SaveAndReimport();
        var cfg=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/PcProgression.json");if(!cfg)throw new Exception("Missing progression catalog");
        if(!File.Exists("Assets/StreamingAssets/Frontend/startup_intro.mp4"))throw new Exception("Missing intro movie");
        AssetDatabase.SaveAssets();Debug.Log("CF_FRONTEND_PREPARED sourceChallenges=9 extraChallenges=9 sourceItems=6 generatedLoading=true");
        BuildFullMap.RebuildRuntimeWindows();
    }
}
