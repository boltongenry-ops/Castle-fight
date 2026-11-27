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
    internal static string[] ChangeNotes(JObject release){
        if(!(release?["changes_ru"] is JArray changes))return Array.Empty<string>();
        return changes.Where(item=>item.Type==JTokenType.String)
            .Select(item=>((string)item).Trim())
            .Where(line=>line.Length>0)
            .Take(24)
            .Select(line=>line.Length<=480?line:line.Substring(0,479)+"…")
            .ToArray();
    }
}
public sealed partial class PcGame {
    bool showUpdatePanel,checkingRelease,downloadingUpdate,updateCancelled,awaitInstallPermission;float updateProgress;string updateStatus="Проверка доступна через интернет",downloadedUpdate;JObject availableRelease;
    static readonly string[] InstalledChangeNotes={
        "Меню, лобби и испытания в стиле карты кампании.",
        "Новые настройки карты, моделей и HUD.",
        "Поправлены профиль и выбор расы.",
        "Плавный дым и свет в окнах замка на загрузке."
    };
    static readonly Rect UpdateNotesArea=new Rect(248,293,784,180);
    string[] publishedChangeNotes=Array.Empty<string>();
    Vector2 updateNotesScroll;
    float updateNotesHeight;
    int updateNotesFinger=-1;
    void LoadLauncherSession(){
#if UNITY_STANDALONE_WIN
        string path=BattleLab.Arg("--launcher-session");if(string.IsNullOrEmpty(path))return;
        try{
            string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CastleFight","Launcher","Sessions")+Path.DirectorySeparatorChar;
            path=Path.GetFullPath(path);if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!path.EndsWith(".json",StringComparison.OrdinalIgnoreCase)||!File.Exists(path))return;
            if(new FileInfo(path).Length>16384||DateTime.UtcNow-File.GetLastWriteTimeUtc(path)>TimeSpan.FromMinutes(5)){File.Delete(path);return;}
            var data=JObject.Parse(File.ReadAllText(path));File.Delete(path);string issuer=(string)data["endpoint"];
            if(issuer!=NetworkAccountStore.LegacyEndpoint&&issuer!=NetworkAccountStore.UnityEndpoint)return;
            networkSessionToken=(string)data["token"]??"";
            networkSessionEndpoint=issuer;
            networkGuest=(bool?)data["guest"]==true;
            networkName=(string)data["nickname"]??networkName;
            rememberedAccountName=networkGuest||string.IsNullOrEmpty(networkSessionToken)?"":(string)data["nickname"]??"";
        }catch(Exception){Debug.LogWarning("CF_LAUNCHER_SESSION_UNAVAILABLE");}
#endif
    }
    IEnumerator CheckGameRelease(bool automatic=false){
        if(checkingRelease)yield break;checkingRelease=true;updateStatus="Проверяем версию на GitHub";
        using(var request=UnityWebRequest.Get(Release031.IndexUrl)){request.timeout=10;yield return request.SendWebRequest();
            if(request.result!=UnityWebRequest.Result.Success)updateStatus="Не удалось проверить обновление. Можно продолжать играть.";
            else try{
                var data=JObject.Parse(request.downloadHandler.text);
                if((int?)data["schema"]!=1||!System.Version.TryParse((string)data["version"],out _))throw new FormatException();
                SetAvailableRelease(data);
                bool newer=Release031.Newer((string)data["version"],Version);
                updateStatus=newer?"Доступна версия "+(string)data["version"]:"Установлена актуальная версия "+Version;
                if(automatic&&newer&&page==Page.Main)showUpdatePanel=true;
            }catch(Exception){updateStatus="Не удалось прочитать сведения об обновлении.";}
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
    void SetAvailableRelease(JObject release){
        if((string)availableRelease?["android"]?["sha256"]!=(string)release?["android"]?["sha256"])
            downloadedUpdate=null;
        availableRelease=release;
        publishedChangeNotes=Release031.ChangeNotes(release);
        updateNotesScroll=Vector2.zero;
    }

    void UpdateNotesTouch(){
        if(!showUpdatePanel){updateNotesFinger=-1;return;}
        foreach(var touch in Input.touches){
            if(touch.phase==TouchPhase.Began&&UpdateNotesArea.Contains(GuiPoint(touch.position)))
                updateNotesFinger=touch.fingerId;
            if(touch.fingerId!=updateNotesFinger)continue;
            if(touch.phase==TouchPhase.Moved)updateNotesScroll.y=Mathf.Clamp(updateNotesScroll.y+touch.deltaPosition.y/guiScale,
                0,Mathf.Max(0,updateNotesHeight-UpdateNotesArea.height));
            if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled)updateNotesFinger=-1;
        }
    }

    void DrawGameUpdates(){
        if(!showUpdatePanel)return;
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.78f));
        AtlasSurface(new Rect(213,118,854,492),true);
        var remoteVersion=(string)availableRelease?["version"];
        bool newer=Release031.Newer(remoteVersion,Version);
        Rect versionBadge=new Rect(852,152,180,43);
        AtlasTile(versionBadge,newer,false);
        Text(868,153,148,41,newer?remoteVersion:Version,atlasButton);
        Text(248,146,570,52,newer?"Доступно обновление":"Обновления",atlasTitle);
        string status=newer&&updateStatus=="Доступна версия "+remoteVersion?"Установлена "+Version:updateStatus;
        Text(248,201,782,43,status,label);
        if(downloadingUpdate||checkingRelease)
            ContentLoadingScreen.DrawProgress(new Rect(248,245,784,5),downloadingUpdate?updateProgress:-1,Time.unscaledTime);
        else Accent(248,245,784);
        Text(248,255,782,37,"Что изменилось",raceTitleForUpdates??(raceTitleForUpdates=new GUIStyle(heading){fontSize=23}));
        DrawUpdateNotes(newer?publishedChangeNotes:InstalledChangeNotes);

        bool mobile=SettingsLayout035.Mobile;
        if(newer&&!downloadingUpdate)Text(248,482,782,29,mobile?"Установку подтверждает Android.":
            "Для установки откройте Castle Fight Launcher.",small);
        var primaryAction=new Rect(696,529,336,49);
        if(downloadingUpdate){
            if(Button("updates.defer",primaryAction,"Отложить загрузку"))updateCancelled=true;
        }else if(mobile&&newer&&Release031.ValidApk(availableRelease["android"])){
            if(Button("updates.install",primaryAction,downloadedUpdate!=null?"Установить обновление":"Скачать обновление",true)){
                if(downloadedUpdate!=null)OpenAndroidInstaller();else StartCoroutine(DownloadApkUpdate());
            }
        }else{
            bool enabled=GUI.enabled;GUI.enabled=enabled&&!checkingRelease;
            if(Button("updates.check",primaryAction,checkingRelease?"Проверяем…":"Проверить обновления",true))
                StartCoroutine(CheckGameRelease());
            GUI.enabled=enabled;
        }
        if(Button("updates.close",new Rect(248,529,193,49),"← Назад"))showUpdatePanel=false;
    }
    GUIStyle raceTitleForUpdates;

    void DrawUpdateNotes(string[] notes){
        var style=new GUIStyle(label){fontSize=17,wordWrap=true,richText=false,
            alignment=TextAnchor.MiddleLeft,padding=new RectOffset(12,12,4,4)};
        const float width=762;
        updateNotesHeight=0;
        foreach(string line in notes)updateNotesHeight+=Mathf.Max(43,style.CalcHeight(new GUIContent(line),width-20)+8);
        updateNotesScroll=GUI.BeginScrollView(UpdateNotesArea,updateNotesScroll,
            new Rect(0,0,width,Mathf.Max(UpdateNotesArea.height,updateNotesHeight)),false,updateNotesHeight>UpdateNotesArea.height);
        if(notes.Length==0)Text(12,5,width-24,50,"Изменения к этой версии пока не опубликованы.",label);
        float y=0;
        for(int i=0;i<notes.Length;i++){
            string line=notes[i];
            float height=Mathf.Max(43,style.CalcHeight(new GUIContent(line),width-20)+8);
            var row=new Rect(0,y,width,height-4);
            Fill(row,AtlasWell);
            Fill(new Rect(0,y+8,2,height-20),AtlasGold);
            GUI.Label(new Rect(9,y,width-20,height-4),line,style);
            y+=height;
        }
        GUI.EndScrollView();
    }
}
}
