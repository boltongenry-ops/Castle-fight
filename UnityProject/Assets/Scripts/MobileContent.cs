using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace CastleFight {
// Android stores immutable, verified LZ4 bundles on disk. Nothing extracts a
// whole game archive or creates a game-sized managed byte[] during startup.
public sealed class MobileContent : MonoBehaviour {
    public TextAsset manifest;public Texture2D background,hammer;public Font font;
    public static MobileContent Current {get;private set;}
    public static bool Active=>Current!=null;
    public bool Busy {get;private set;}=true;
    readonly Dictionary<string,AssetBundle> loaded=new Dictionary<string,AssetBundle>();
    readonly Dictionary<string,JObject> files=new Dictionary<string,JObject>();
    readonly Dictionary<string,List<JObject>> resources=new Dictionary<string,List<JObject>>(StringComparer.OrdinalIgnoreCase);
    JObject index;string cache,baseUrl,status="Подготовка загрузки",error="";float progress;bool retry;
    public static string MediaRoot=>Active?Path.Combine(Current.cache,"media"):Application.streamingAssetsPath;
    public static string Media(string relative)=>Path.Combine(MediaRoot,relative.Replace('/',Path.DirectorySeparatorChar));
    public static string[] RaceResources={"","OrcStageOne","NagaStageOne","NatureStageOne","ChaosStageOne","NorthernRealms","MechCoalition","UndeadForces","CorruptedConglomerate","ElementalsUniverse","NightElves","ElvenUnion","UltimateBuilder","NerubianSwarm","PandarenOrder","CentaurKhanates",PepeContent.Pack};
    public static string[] RaceEffects={"NativeFX","NativeFX","NativeFX","NativeFX","NativeFX","NorthernFx","MechFx","UndeadFx","CorruptedFx","ElementalFx","NightFx","ElvenFx","FinalFx","FinalFx","FinalFx","NativeFX"};
    void Awake(){if(Current){Destroy(gameObject);return;}Current=this;DontDestroyOnLoad(gameObject);Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Application.backgroundLoadingPriority=ThreadPriority.Low;QualitySettings.asyncUploadTimeSlice=2;QualitySettings.asyncUploadBufferSize=8;QualitySettings.asyncUploadPersistentBuffer=false;}
    IEnumerator Start(){
        index=JObject.Parse(manifest.text);cache=Path.Combine(Application.persistentDataPath,"content-v1");Directory.CreateDirectory(cache);
        status="Проверяем ресурсы игры";yield return ContentCatalog031.Resolve(index,cache,value=>index=value);baseUrl=(string)index["baseUrl"];
        foreach(var row in ((JArray)index["bundles"]).OfType<JObject>())files.Add((string)row["name"],row);
        foreach(var row in ((JArray)index["resources"]).OfType<JObject>()){string key=(string)row["key"];if(!resources.TryGetValue(key,out var list))resources[key]=list=new List<JObject>();list.Add(row);}
        // Common is metadata/UI only; no race pack, model library, or audits.
        yield return EnsureBundles(new[]{"frontend","mapscene","builders"},"Первый запуск · поле и интерфейс");
        if(files.ContainsKey("centaur-028"))yield return EnsureBundles(new[]{"centaur-028"},"Подготовка кентавров");
        status="Открываем главное меню";progress=1;yield return null;
        var scene=SceneManager.LoadSceneAsync((string)index["scene"],LoadSceneMode.Single);yield return scene;
        var startupCamera=GetComponentInChildren<Camera>();if(startupCamera)startupCamera.enabled=false;
        Debug.Log("CF_ANDROID_CONTENT_SCENE_READY");
        // PcGame releases the loading overlay after its actual menu is ready.
    }
    public void Ready(){Busy=false;Debug.Log("CF_ANDROID_CONTENT_MENU_VISIBLE");}
    public void Begin(){Busy=true;}
    public void ReleaseRaceBundles(){
        // Keep the live scene and every dependency of the resident interface and shared content.
        var keep=new HashSet<string>();
        var roots=loaded.Keys.Where(name=>!name.StartsWith("race-",StringComparison.Ordinal)&&!name.StartsWith("fx-",StringComparison.Ordinal)).ToArray();
        foreach(string root in roots)foreach(string _ in Dependencies(root,keep)){}
        foreach(string name in loaded.Keys.Where(name=>!keep.Contains(name)).ToArray()){
            loaded[name].Unload(false);
            loaded.Remove(name);
        }
    }
    public bool Has(string key)=>resources.ContainsKey(key);
    public T Load<T>(string key) where T:UnityEngine.Object {
        if(!resources.TryGetValue(key,out var rows))return null;
        foreach(var row in rows){if(!loaded.TryGetValue((string)row["bundle"],out var bundle))continue;var obj=bundle.LoadAsset<T>((string)row["asset"]);if(obj)return obj;}return null;
    }
    public T[] LoadAll<T>(string prefix) where T:UnityEngine.Object=>resources.Keys.Where(k=>string.IsNullOrEmpty(prefix)||k.StartsWith(prefix+"/",StringComparison.OrdinalIgnoreCase)).Select(Load<T>).Where(a=>a).ToArray();
    public IEnumerator LoadAsync<T>(string key,Action<T> complete) where T:UnityEngine.Object {
        if(!resources.TryGetValue(key,out var rows)){complete(null);yield break;}
        yield return EnsureBundles(rows.Select(r=>(string)r["bundle"]),"Загрузка ресурсов");
        foreach(var row in rows){var request=loaded[(string)row["bundle"]].LoadAssetAsync<T>((string)row["asset"]);yield return request;if(request.asset is T value){complete(value);yield break;}}complete(null);
    }
    AssetBundle NamedBundle(string path){if(string.IsNullOrEmpty(path))return null;foreach(var bundle in loaded.Values)if(bundle.Contains(path))return bundle;throw new InvalidOperationException("Missing downloaded asset "+path);}
    public T LoadNamed<T>(string path) where T:UnityEngine.Object {var bundle=NamedBundle(path);return bundle?bundle.LoadAsset<T>(path):null;}
    public IEnumerator LoadNamedAsync<T>(string path,Action<T> complete) where T:UnityEngine.Object {var bundle=NamedBundle(path);if(!bundle){complete(null);yield break;}var request=bundle.LoadAssetAsync<T>(path);yield return request;complete(request.asset as T);}
    public IEnumerator EnsureRace(int race){
        yield return EnsurePrefix("ClassicMissiles","Классические снаряды");
        if(race==0||race==PepeContent.Race){yield return EnsurePrefix("NativeFX","Подготовка эффектов");yield break;}
        yield return EnsureResources(new[]{RaceResources[race]},"Подготовка расы");
        yield return EnsurePrefix(RaceEffects[race],"Подготовка эффектов");
        yield return EnsurePrefix(RaceEffects[race]+"Materials","Подготовка эффектов");
        yield return EnsurePrefix("NativeFX","Подготовка общих эффектов");
    }
    public IEnumerator CacheRaces(IEnumerable<int> races){
        var wanted=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"ClassicMissiles","NativeFX"};
        foreach(int race in races){if(race==PepeContent.Race)continue;if(race>0)wanted.Add(RaceResources[race]);wanted.Add(RaceEffects[race]);wanted.Add(RaceEffects[race]+"Materials");}
        var roots=resources.Where(p=>wanted.Any(prefix=>p.Key.Equals(prefix,StringComparison.OrdinalIgnoreCase)||p.Key.StartsWith(prefix+"/",StringComparison.OrdinalIgnoreCase))).SelectMany(p=>p.Value.Select(row=>(string)row["bundle"]));
        var seen=new HashSet<string>();var rows=roots.SelectMany(key=>Dependencies(key,seen)).Select(key=>files[key]).Where(row=>!Cached(row)).ToArray();long total=rows.Sum(row=>(long)row["size"]),done=0;
        foreach(var row in rows){yield return Download(row,"Случайные расы · загрузка на диск",done,total);done+=(long)row["size"];}
    }
    public IEnumerator EnsureResources(IEnumerable<string> keys,string message)=>EnsureBundles(keys.Where(resources.ContainsKey).SelectMany(k=>resources[k].Select(r=>(string)r["bundle"])),message);
    public IEnumerator EnsurePrefix(string prefix,string message)=>EnsureBundles(resources.Keys.Where(k=>k.StartsWith(prefix+"/",StringComparison.OrdinalIgnoreCase)).SelectMany(k=>resources[k].Select(r=>(string)r["bundle"])),message);
    IEnumerable<string> Dependencies(string name,HashSet<string> seen){if(!seen.Add(name))yield break;if(!files.TryGetValue(name,out var row))throw new InvalidDataException("Unknown content bundle "+name);foreach(string dep in ((JArray)row["dependencies"]).Values<string>())foreach(string item in Dependencies(dep,seen))yield return item;yield return name;}
    public IEnumerator EnsureBundles(IEnumerable<string> roots,string message){
        var seen=new HashSet<string>();var queue=roots.SelectMany(k=>Dependencies(k,seen)).ToArray();bool wasBusy=Busy;Busy=true;status=message;progress=0;
        long total=queue.Where(k=>!loaded.ContainsKey(k)).Sum(k=>(long)files[k]["size"]);long done=0;
        foreach(string key in queue){
            if(loaded.ContainsKey(key))continue;var row=files[key];
            yield return Download(row,message,done,total);done+=(long)row["size"];
            status=message+" · чтение с диска";
            var operation=AssetBundle.LoadFromFileAsync(CacheFile(row));yield return operation;
            while(!operation.assetBundle){
                error="Не удалось прочитать пакет. Повторите загрузку.";yield return WaitForRetry();File.Delete(CacheFile(row));File.Delete(CacheFile(row)+".ok");
                yield return Download(row,message,done-(long)row["size"],total);operation=AssetBundle.LoadFromFileAsync(CacheFile(row));yield return operation;
            }
            loaded.Add(key,operation.assetBundle);yield return null;
        }
        progress=1;Busy=wasBusy;
    }
    string CacheFile(JObject row){string hash=(string)row["sha256"];if(hash==null||hash.Length!=64||hash.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException("Invalid content hash");return Path.Combine(cache,hash+".cfb");}
    static string Hash(string path){using(var stream=File.OpenRead(path))using(var algorithm=SHA256.Create())return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    bool Cached(JObject row){string path=CacheFile(row);if(!File.Exists(path)||new FileInfo(path).Length!=(long)row["size"])return false;string stamp=(string)row["sha256"]+":"+File.GetLastWriteTimeUtc(path).Ticks;return File.Exists(path+".ok")&&File.ReadAllText(path+".ok")==stamp;}
    void Mark(JObject row){string path=CacheFile(row);File.WriteAllText(path+".ok",(string)row["sha256"]+":"+File.GetLastWriteTimeUtc(path).Ticks);}
    readonly HashSet<string> downloading=new HashSet<string>();bool bulkActive,cancelBulk;
    IEnumerable<JObject> AllFiles=>((JArray)index["bundles"]).Concat((JArray)index["streaming"]).OfType<JObject>().GroupBy(r=>(string)r["file"]).Select(g=>g.First());
    public long TotalContentBytes=>AllFiles.Sum(r=>(long)r["size"]);
    public long RemainingContentBytes=>AllFiles.Where(r=>!Cached(r)).Sum(r=>(long)r["size"]);
    public IEnumerator DownloadAll(){
        if(bulkActive)yield break;bool wasBusy=Busy;Busy=true;bulkActive=true;cancelBulk=false;long total=RemainingContentBytes,done=0;
        try{foreach(var row in AllFiles){if(cancelBulk)break;if(Cached(row))continue;yield return Download(row,"Все ресурсы · скачивание",done,total);if(cancelBulk)break;done+=(long)row["size"];yield return null;}Debug.Log("CF_CONTENT_DOWNLOAD_ALL finished="+!cancelBulk+" remaining="+RemainingContentBytes);}
        finally{bulkActive=false;Busy=wasBusy;error="";}
    }
    IEnumerator Download(JObject row,string message,long prior,long total,bool interactive=true){
        string key=(string)row["file"];while(downloading.Contains(key)){if(bulkActive&&cancelBulk)yield break;yield return null;}
        downloading.Add(key);try{yield return DownloadCore(row,message,prior,total,interactive);}finally{downloading.Remove(key);}
    }
    IEnumerator DownloadCore(JObject row,string message,long prior,long total,bool interactive=true){
        string final=CacheFile(row),partial=final+".part";long size=(long)row["size"];
        if(Cached(row))yield break;
        if(File.Exists(final)&&new FileInfo(final).Length==size){var verify=Task.Run(()=>Hash(final));while(!verify.IsCompleted){status="Проверка сохранённых ресурсов";yield return null;}if(!verify.IsFaulted&&verify.Result==(string)row["sha256"]){Mark(row);yield break;}}
        if(File.Exists(final))File.Delete(final);
        int attempt=0;
        while(true){
            long resume=File.Exists(partial)?new FileInfo(partial).Length:0;if(resume>=size){File.Delete(partial);resume=0;}
            using(var request=UnityWebRequest.Get(ContentCatalog031.AssetUrl(index,row))){
                request.downloadHandler=new DownloadHandlerFile(partial,resume>0){removeFileOnAbort=false};request.timeout=0;
                if(resume>0)request.SetRequestHeader("Range","bytes="+resume+"-");
                var transfer=request.SendWebRequest();long lastBytes=0;float lastProgress=Time.realtimeSinceStartup;
                while(!transfer.isDone){if(bulkActive&&cancelBulk){request.Abort();break;}long currentBytes=(long)request.downloadedBytes;if(currentBytes!=lastBytes){lastBytes=currentBytes;lastProgress=Time.realtimeSinceStartup;}else if(Time.realtimeSinceStartup-lastProgress>45){request.Abort();break;}long got=Math.Min(size,resume+(long)request.downloadedBytes);progress=total>0?(prior+got)/(float)total:0;status=message+"\n"+((prior+got)/1048576f).ToString("F1")+" / "+(total/1048576f).ToString("F1")+" МБ";yield return null;}
                if(bulkActive&&cancelBulk)yield break;
                if(resume>0&&request.responseCode==200){File.Delete(partial);continue;}
                if(request.result!=UnityWebRequest.Result.Success){if(++attempt<3){status="Соединение прервано · повторяем загрузку";yield return new WaitForSecondsRealtime(attempt);continue;}if(!interactive)yield break;error="Не удалось скачать ресурсы. Проверьте интернет и нажмите «Повторить».";yield return WaitForRetry();attempt=0;continue;}
            }
            status="Проверка загруженного пакета";var digest=Task.Run(()=>new FileInfo(partial).Length==size?Hash(partial):"");while(!digest.IsCompleted)yield return null;
            if(digest.IsFaulted||digest.Result!=(string)row["sha256"]){if(File.Exists(partial))File.Delete(partial);if(!interactive)yield break;error="Пакет загрузился с ошибкой. Нажмите «Повторить».";yield return WaitForRetry();continue;}
            File.Move(partial,final);Mark(row);Debug.Log("CF_CONTENT_CACHED name="+row["name"]+" bytes="+size);yield break;
        }
    }
    IEnumerator WaitForRetry(){retry=false;while(!retry)yield return null;error="";}
    public IEnumerator EnsureMedia(string relative,bool foreground=true){
        var row=((JArray)index["streaming"]).OfType<JObject>().FirstOrDefault(r=>(string)r["path"]==relative);if(row==null)yield break;
        bool wasBusy=Busy;if(foreground)Busy=true;yield return Download(row,"Загрузка оформления",0,(long)row["size"],foreground);if(!Cached(row)){if(foreground)Busy=wasBusy;yield break;}
        string destination=Media(relative);Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if(!File.Exists(destination)||new FileInfo(destination).Length!=(long)row["size"])File.Copy(CacheFile(row),destination,true);if(foreground)Busy=wasBusy;
    }
    public void DeferDownloadAll(){if(bulkActive){cancelBulk=true;retry=true;}}
    void OnGUI(){if(!Busy)return;GUI.depth=-10000;int action=ContentLoadingScreen.Draw(background,hammer,font,status,progress,error,bulkActive);if((action&1)!=0)retry=true;if((action&2)!=0)DeferDownloadAll();}

}
public static class ContentResources {
    public static T Load<T>(string key) where T:UnityEngine.Object=>PepeContent.Load<T>(key)??(MobileContent.Active?MobileContent.Current.Load<T>(key):CentaurContent.Load<T>(key)??Resources.Load<T>(key));
    public static T[] LoadAll<T>(string key) where T:UnityEngine.Object=>MobileContent.Active?MobileContent.Current.LoadAll<T>(key):Resources.LoadAll<T>(key);
}
}
