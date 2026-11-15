using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

// Code-only Android release using the existing immutable downloadable content.
public static class BuildAndroidRelease {
    public static void Build(){
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
            BuildMobileContent.RebuildPlayer();
        }finally{
            PlayerSettings.Android.keystorePass="";PlayerSettings.Android.keyaliasPass="";
            PlayerSettings.Android.useCustomKeystore=custom;
            PlayerSettings.Android.keystoreName=oldKey;PlayerSettings.Android.keyaliasName=oldAlias;
            AssetDatabase.SaveAssets();
        }
    }
}
