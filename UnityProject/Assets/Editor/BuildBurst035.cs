using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
// Persist an explicit Android AOT configuration so an empty cached native-plugin
// result cannot be reused after adding Jobs to an existing project.
public static class BuildBurst035 {
 public static void Android(){
  BuildMobileContent.Restore();
  var module=typeof(EditorApplication).Assembly.GetType("Unity.Burst.Editor.BurstPlatformAotSettings");
  if(module==null)foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){module=assembly.GetType("Unity.Burst.Editor.BurstPlatformAotSettings");if(module!=null)break;}
  if(module==null)throw new Exception("Burst AOT settings unavailable");
  var flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  var settings=module.GetMethod("GetOrCreateSettings",flags).Invoke(null,new object[]{(BuildTarget?)BuildTarget.Android,true});
  Debug.Log("CF_BURST035_ANDROID_SETTINGS_BEFORE "+EditorJsonUtility.ToJson((UnityEngine.Object)settings));
  module.GetField("EnableBurstCompilation",flags).SetValue(settings,true);module.GetField("EnableOptimisations",flags).SetValue(settings,true);
  module.GetMethod("Save",flags).Invoke(settings,new object[]{(BuildTarget?)BuildTarget.Android});
  string root=Environment.GetEnvironmentVariable("CF_ANDROID_TOOLS_ROOT")??Path.Combine(EditorApplication.applicationContentsPath,"PlaybackEngines/AndroidPlayer");
  Environment.SetEnvironmentVariable("ANDROID_NDK_ROOT",Path.Combine(root,"NDK"));
  Environment.SetEnvironmentVariable("CF_ANDROID_CLEAN_BURST","1");
  BuildAndroidRelease.Build();
  Debug.Log("CF_BURST035_ANDROID_BUILD_PASS");
 }
}
