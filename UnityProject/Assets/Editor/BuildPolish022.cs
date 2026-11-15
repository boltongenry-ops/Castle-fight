using System;using System.IO;using System.Linq;using UnityEditor;using UnityEngine;
public static class BuildPolish022 {
    public static void Windows(){
        BuildClassicProjectiles.Windows();Directory.CreateDirectory("Assets/StreamingAssets/Frontend");File.Copy("Builds/ClassicMissilesWindows/classic-missiles","Assets/StreamingAssets/Frontend/classic-missiles.windows",true);AssetDatabase.Refresh();
        BuildFrontendRich.RebuildPlayerOnlyWindows();
        // Scripts-only rebuilds retain data; explicitly synchronize new raw media.
        string dest="Builds/MapWindows/CastleFightMap_Data/StreamingAssets/Frontend";Directory.CreateDirectory(dest+"/MemeAudio");File.Copy("Assets/StreamingAssets/Frontend/classic-missiles.windows",dest+"/classic-missiles.windows",true);foreach(var path in Directory.GetFiles("Assets/StreamingAssets/Frontend/MemeAudio","*.mp3"))File.Copy(path,dest+"/MemeAudio/"+Path.GetFileName(path),true);
        Debug.Log("CF_POLISH022_WINDOWS_PASS");
    }
    public static void AndroidContent(){
        var paths=AssetDatabase.GetDependencies(Directory.GetFiles("Assets/ClassicProjectiles/Prefabs","*.prefab"),true);AssetDatabase.StartAssetEditing();try{foreach(string path in paths){if(!(AssetImporter.GetAtPath(path) is TextureImporter importer))continue;var cfg=importer.GetPlatformTextureSettings("Android");cfg.name="Android";cfg.overridden=true;cfg.maxTextureSize=512;cfg.format=TextureImporterFormat.ETC2_RGBA8;importer.SetPlatformTextureSettings(cfg);importer.SaveAndReimport();}}finally{AssetDatabase.StopAssetEditing();}
        BuildClassicProjectiles.Android();Debug.Log("CF_POLISH022_ANDROID_CONTENT_PASS");
    }
}
