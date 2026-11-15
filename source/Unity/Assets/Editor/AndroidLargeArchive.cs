#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
// Unity's lint dependency bundles all game assets in a temporary library AAR.
// This archive can exceed 4 GiB even when the final compressed APK is smaller.
public sealed class AndroidLargeArchive : IPostGenerateGradleAndroidProject {
 public int callbackOrder=>200;
 public void OnPostGenerateGradleAndroidProject(string path){
  string file=Path.Combine(path,"build.gradle"),text=File.ReadAllText(file);
  const string marker="// Castle Fight large temporary archives";
  if(!text.Contains(marker))File.AppendAllText(file,"\n"+marker+"\ntasks.withType(org.gradle.api.tasks.bundling.Zip).configureEach { zip64 = true }\n");
 }
}
#endif
