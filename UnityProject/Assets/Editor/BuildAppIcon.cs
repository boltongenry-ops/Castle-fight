using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEditor.Build.Reporting;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
public static class BuildAppIcon {
 public static void Apply(){
  const string path="Assets/SourceAssets/Branding/CastleFight.png";
  AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
  var importer=(TextureImporter)AssetImporter.GetAtPath(path);
  importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;
  importer.textureCompression=TextureImporterCompression.Uncompressed;
  importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
  importer.alphaIsTransparency=false;importer.SaveAndReimport();
  var image=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
  if(!image)throw new Exception("Generated application icon missing");
  PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown,new[]{image});
  int count=0;
  foreach(var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android)){
   var slots=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,kind);
   foreach(var slot in slots){
    var layers=new Texture2D[slot.maxLayerCount];
    for(int i=0;i<Math.Min(2,layers.Length);i++)layers[i]=image;
    slot.SetTextures(layers);count++;
   }
   PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android,kind,slots);
  }
  AssetDatabase.SaveAssets();Debug.Log("CF_APP_ICON_CONFIGURED slots="+count);
 }
 static Dictionary<string,string> Snapshot(string root){
  string full=Path.GetFullPath(root).Replace("'","''");
  string command="$root='"+full+"';Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object {$_.Name -cmatch '^[0-9a-f]{32}$' -or $_.Name -eq 'globalgamemanagers' -or $_.Name -match '^level[0-9]+$'} | ForEach-Object {$f=[IO.File]::OpenRead($_.FullName);$s=[Security.Cryptography.SHA256Cng]::new();try{[Console]::WriteLine($_.FullName.Substring($root.Length)+'|'+[Convert]::ToBase64String($s.ComputeHash($f)))}finally{$s.Dispose();$f.Dispose()}}";
  var start=new System.Diagnostics.ProcessStartInfo("powershell.exe","-NoProfile -NonInteractive -Command \""+command+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
  using(var process=System.Diagnostics.Process.Start(start)){string result=process.StandardOutput.ReadToEnd(),error=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new Exception(error);return result.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).ToDictionary(line=>line.Substring(0,line.IndexOf('|')),line=>line.Substring(line.IndexOf('|')+1));}
 }
 public static void Android(){
  Apply();
  const string data="Library/PlayerDataCache/Android/Data";
  var before=Directory.Exists(data)?Snapshot(data):new Dictionary<string,string>();
  if(before.Count==0){BuildAndroidTouch.Build();return;}
  var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/CastleFightMap.unity"},target=BuildTarget.Android,locationPathName="Builds/MapAndroid/CastleFight-Android-Fixed-Icons.apk",options=BuildOptions.BuildScriptsOnly});
  if(result.summary.result!=BuildResult.Succeeded){Debug.LogWarning("Android scripts-only unsupported; using full player-data build");BuildAndroidTouch.Build();return;}
  var after=Snapshot(data);if(before.Count!=after.Count||before.Any(p=>!after.TryGetValue(p.Key,out string value)||value!=p.Value))throw new Exception("Android script-only build changed game assets");
  Debug.Log("CF_ANDROID_SCRIPT_DATA_UNCHANGED files="+before.Count);
  Debug.Log("CF_ANDROID_BUILD_PASS "+result.summary.totalSize);
 }
}
