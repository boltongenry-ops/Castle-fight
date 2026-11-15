using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
public static class BuildAndroidTouch {
 public static void Build() {
  PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
  PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
  PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.castlefight.unitymap");
  PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
  PlayerSettings.Android.forceInternetPermission=true;
  PlayerSettings.Android.bundleVersionCode=Math.Max(PlayerSettings.Android.bundleVersionCode,4);
  Directory.CreateDirectory("Builds/MapAndroid");
  var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/Scenes/CastleFightMap.unity"},target=BuildTarget.Android,locationPathName="Builds/MapAndroid/CastleFight-Android-Fixed-Icons.apk",options=BuildOptions.None });
  if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Android build failed: "+report.summary.result);
  Debug.Log("CF_ANDROID_BUILD_PASS "+report.summary.totalSize);
 }
}
