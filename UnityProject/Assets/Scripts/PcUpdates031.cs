using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
namespace CastleFight {
public static class Release031 {
    public const string IndexUrl="https://raw.githubusercontent.com/boltongenry-ops/Castle-fight/main/release.json";
    public static bool Newer(string remote,string local)=>System.Version.TryParse(remote,out var a)&&System.Version.TryParse(local,out var b)&&a>b;
    public static bool Https(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https";
    public static string Hash(string path){using(var file=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
    public static bool ValidApk(JToken apk)=>apk!=null&&Https((string)apk["url"])&&((string)apk["sha256"]??"").Length==64&&(long?)apk["size"]>0;
}
public sealed partial class PcGame {
    bool showUpdatePanel,checkingRelease,downloadingUpdate,updateCancelled,awaitInstallPermission;float updateProgress;string updateStatus="Проверка доступна через интернет",downloadedUpdate;JObject availableRelease;
    void LoadLauncherSession(){
#if UNITY_STANDALONE_WIN
        string path=BattleLab.Arg("--launcher-session");if(string.IsNullOrEmpty(path))return;
        try{
            string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CastleFight","Launcher","Sessions")+Path.DirectorySeparatorChar;
            path=Path.GetFullPath(path);if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!path.EndsWith(".json",StringComparison.OrdinalIgnoreCase)||!File.Exists(path))return;
            if(new FileInfo(path).Length>16384||DateTime.UtcNow-File.GetLastWriteTimeUtc(path)>TimeSpan.FromMinutes(5)){File.Delete(path);return;}
            var data=JObject.Parse(File.ReadAllText(path));File.Delete(path);if((string)data["endpoint"]!="wss://45-15-156-101.fatmetal.net/play")return;
            networkSessionToken=(string)data["token"]??"";networkSessionEndpoint=directoryEndpoint=(string)data["endpoint"];networkName=(string)data["nickname"]??networkName;
        }catch(Exception){Debug.LogWarning("CF_LAUNCHER_SESSION_UNAVAILABLE");}
#endif
    }
    IEnumerator CheckGameRelease(bool automatic=false){
        if(checkingRelease)yield break;checkingRelease=true;updateStatus="Проверяем версию на GitHub";
        using(var request=UnityWebRequest.Get(Release031.IndexUrl)){request.timeout=10;yield return request.SendWebRequest();
            if(request.result!=UnityWebRequest.Result.Success)updateStatus="Не удалось проверить обновление. Можно продолжать играть.";
            else try{var data=JObject.Parse(request.downloadHandler.text);if((int?)data["schema"]!=1)throw new FormatException();availableRelease=data;bool newer=Release031.Newer((string)data["version"],Version);updateStatus=newer?"Доступна версия "+(string)data["version"]:"Установлена актуальная версия "+Version;if(automatic&&newer&&page==Page.Main)showUpdatePanel=true;}catch(Exception){updateStatus="Не удалось прочитать сведения об обновлении.";}
        }checkingRelease=false;
    }
    static string ApkUpdatePath(){
#if UNITY_ANDROID && !UNITY_EDITOR
        using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))using(var installer=new AndroidJavaClass("com.castlefight.updates.UpdateProvider"))
            return installer.CallStatic<string>("downloadPath",activity);
#else
        return Path.Combine(Application.temporaryCachePath,"castle-fight-update.apk");
#endif
    }
    IEnumerator DownloadApkUpdate(){
        var apk=availableRelease?["android"];if(downloadingUpdate||!Release031.ValidApk(apk))yield break;
        string final=ApkUpdatePath();Directory.CreateDirectory(Path.GetDirectoryName(final));
        downloadingUpdate=true;updateCancelled=false;string partial=final+".part";long size=(long)apk["size"];string hash=(string)apk["sha256"];
        if(File.Exists(final)&&new FileInfo(final).Length==size){var verify=Task.Run(()=>Release031.Hash(final));while(!verify.IsCompleted)yield return null;if(!verify.IsFaulted&&verify.Result==hash){downloadedUpdate=final;downloadingUpdate=false;OpenAndroidInstaller();yield break;}}
        if(File.Exists(partial)&&new FileInfo(partial).Length>size)File.Delete(partial);
        bool complete=false;
        for(int attempt=0;attempt<3&&!updateCancelled;attempt++){
            long resume=File.Exists(partial)?new FileInfo(partial).Length:0;
            if(resume==size){complete=true;break;}
            using(var request=UnityWebRequest.Get((string)apk["url"])){if(resume>0)request.SetRequestHeader("Range","bytes="+resume+"-");request.downloadHandler=new DownloadHandlerFile(partial,resume>0){removeFileOnAbort=false};request.timeout=0;var operation=request.SendWebRequest();
                while(!operation.isDone){updateProgress=Mathf.Clamp01((resume+(long)request.downloadedBytes)/(float)size);updateStatus="Скачиваем обновление · "+Mathf.RoundToInt(updateProgress*100)+"%";if(updateCancelled){request.Abort();break;}yield return null;}
                if(updateCancelled)break;
                if(resume>0&&request.responseCode!=206){File.Delete(partial);continue;}
                if(request.result==UnityWebRequest.Result.Success&&File.Exists(partial)&&new FileInfo(partial).Length==size){complete=true;break;}
            }
        }
        if(complete){updateStatus="Проверяем целостность APK";var verify=Task.Run(()=>Release031.Hash(partial));while(!verify.IsCompleted)yield return null;if(verify.IsFaulted||verify.Result!=hash){File.Delete(partial);updateStatus="Проверка APK не прошла. Повторите загрузку.";}else{if(File.Exists(final))File.Delete(final);File.Move(partial,final);downloadedUpdate=final;}}
        else updateStatus=updateCancelled?"Загрузка отложена. При повторе продолжим с места остановки.":"Загрузка прервана. Нажмите «Скачать» для продолжения.";
        downloadingUpdate=false;if(downloadedUpdate==final)OpenAndroidInstaller();
    }
    void OpenAndroidInstaller(){
#if UNITY_ANDROID && !UNITY_EDITOR
        try{using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))using(var installer=new AndroidJavaClass("com.castlefight.updates.UpdateProvider")){
            string error=installer.CallStatic<string>("validate",activity,downloadedUpdate);if(error!=""){updateStatus=error;return;}
            bool allowed=installer.CallStatic<bool>("canInstall",activity);awaitInstallPermission=!allowed;
            activity.Call("runOnUiThread",new AndroidJavaRunnable(()=>{using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))using(var current=player.GetStatic<AndroidJavaObject>("currentActivity"))using(var api=new AndroidJavaClass("com.castlefight.updates.UpdateProvider"))api.CallStatic("install",current);}));
            updateStatus=allowed?"Подтвердите обновление в системном окне Android.":"Разрешите установку обновлений для Castle Fight и вернитесь в игру.";
        }}catch(Exception){updateStatus="Не удалось открыть установщик. Повторите установку кнопкой ниже.";}
#else
        updateStatus="Обновления ПК устанавливаются через Castle Fight Launcher.";
#endif
    }
    void OnApplicationFocus(bool focused){
#if UNITY_ANDROID && !UNITY_EDITOR
        if(focused&&awaitInstallPermission)try{using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))using(var installer=new AndroidJavaClass("com.castlefight.updates.UpdateProvider"))if(installer.CallStatic<bool>("canInstall",activity)){awaitInstallPermission=false;OpenAndroidInstaller();}}catch(Exception){awaitInstallPermission=false;}
#endif
    }
    void DrawGameUpdates(){
        if(!showUpdatePanel)return;Fill(new Rect(0,0,1280,720),new Color(0,0,0,.82f));Box(new Rect(300,140,680,440));Text(338,167,600,44,"ОБНОВЛЕНИЯ CASTLE FIGHT",heading);Accent(338,223,600);Text(338,244,600,72,updateStatus,label);
        Text(338,321,600,78,Application.isMobilePlatform?"Ресурсы докачиваются автоматически. Обновление приложения скачивается здесь; установку подтверждает Android. Аккаунт и сохранения сохраняются.":"Лаунчер проверяет файлы на GitHub и скачивает только изменённые. В игре используется тот же сетевой аккаунт.",small);
        if(downloadingUpdate){Fill(new Rect(338,408,600,12),new Color(.04f,.06f,.08f));Fill(new Rect(338,408,600*updateProgress,12),new Color(.75f,.58f,.24f));if(Button("updates.defer",new Rect(338,440,600,46),"Отложить загрузку"))updateCancelled=true;}
        else if(Application.isMobilePlatform&&availableRelease!=null&&Release031.Newer((string)availableRelease["version"],Version)&&Release031.ValidApk(availableRelease["android"])){
            if(Button("updates.install",new Rect(338,430,600,48),downloadedUpdate!=null?"Установить скачанное обновление":"Скачать и установить обновление",true)){if(downloadedUpdate!=null)OpenAndroidInstaller();else StartCoroutine(DownloadApkUpdate());}
        }else if(Button("updates.check",new Rect(338,430,600,48),checkingRelease?"Проверяем…":"Проверить обновления",true)&&!checkingRelease)StartCoroutine(CheckGameRelease());
        if(Button("updates.close",new Rect(338,505,600,44),"Назад"))showUpdatePanel=false;
    }
}
}
