using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace CastleFight {
public sealed class ReforgedModelPack {
    const string Release="https://github.com/boltongenry-ops/Castle-fight/releases/download/v0.39.0/";
    const string Windows="windows", Android="android";
    static readonly ConditionalWeakTable<PcGame,ReforgedModelPack> States=new ConditionalWeakTable<PcGame,ReforgedModelPack>();
    public static ReforgedModelPack For(PcGame game)=>States.GetOrCreateValue(game);
    public static string Address(string raw,int team)=>"model/"+string.Concat(raw.Select(c=>((int)c).ToString("x2")))+"/"+team;
    public static string PortraitAddress(string raw)=>"portrait/"+string.Concat(raw.Select(c=>((int)c).ToString("x2")));
    public static string IconAddress(string name)=>"icon/"+name.ToLowerInvariant();

    readonly Dictionary<string,GameObject>[] models={new Dictionary<string,GameObject>(),new Dictionary<string,GameObject>()};
    readonly List<AssetBundle> bundles=new List<AssetBundle>();
    readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>(StringComparer.Ordinal);
    readonly HashSet<string> verifiedHashes=new HashSet<string>(StringComparer.Ordinal);
    string[] optionalProfiles=Array.Empty<string>();
    sealed class ArtPack040 {
        public const string Release="https://github.com/boltongenry-ops/Castle-fight/releases/download/v0.40.0/";
        public static readonly ConditionalWeakTable<ReforgedModelPack,ArtPack040> Instances=new ConditionalWeakTable<ReforgedModelPack,ArtPack040>();
        public AssetBundle Bundle;
        public JObject Icons,Effects;
        public bool HasDownloadFiles,ConfirmDelete;
        public readonly HashSet<string> MissingSheets=new HashSet<string>(StringComparer.Ordinal);
        public readonly Dictionary<int,string> ShotPaths=new Dictionary<int,string>();
        public readonly Dictionary<string,GameObject> Geometry=new Dictionary<string,GameObject>();
        public readonly Dictionary<string,ClassicProjectileModel> LiveEffects=new Dictionary<string,ClassicProjectileModel>();
        public readonly HashSet<string> CurrentEffects=new HashSet<string>(StringComparer.Ordinal);
        public readonly List<string> ExpiredEffects=new List<string>();
    }
    ArtPack040 Art=>ArtPack040.Instances.GetOrCreateValue(this);
    AssetBundle artBundle {get=>Art.Bundle;set=>Art.Bundle=value;}
    JObject artIcons {get=>Art.Icons;set=>Art.Icons=value;}
    JObject artEffects {get=>Art.Effects;set=>Art.Effects=value;}
    internal HashSet<string> MissingSheets=>Art.MissingSheets;
    internal Dictionary<int,string> ShotPaths=>Art.ShotPaths;
    internal Dictionary<string,GameObject> Geometry=>Art.Geometry;
    internal Dictionary<string,ClassicProjectileModel> LiveEffects=>Art.LiveEffects;
    internal HashSet<string> CurrentEffects=>Art.CurrentEffects;
    internal List<string> ExpiredEffects=>Art.ExpiredEffects;
    string cache;
    bool canceled;
    public bool Preferred,Draft,Panel;
    public bool Busy {get;private set;}
    public int Revision {get;private set;}
    public int RenderRevision;
    public long Bytes {get;private set;}
    public long Total {get;private set;}
    public string Status {get;private set;}="";
    public bool Active=>Preferred&&bundles.Count>0;
    public bool Installed=>File.Exists(IndexPath)&&!broken;
    public bool ArtInstalled=>File.Exists(ArtIndexPath);
    public bool HasCache=>Installed||ArtInstalled||Art.HasDownloadFiles;
    public bool ConfirmDelete {get=>Art.ConfirmDelete;set=>Art.ConfirmDelete=value;}
    internal JObject ArtEffects=>Active?artEffects:null;
    bool broken;
    string Platform=>Application.isMobilePlatform?Android:Windows;
    string IndexPath=>Path.Combine(cache,Platform+".json");
    string ArtIndexPath=>Path.Combine(cache,"art040-"+Platform+".json");

    public void SetCache(string settingsPath){
        cache=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsPath)),"reforged-models-v1");
        Art.HasDownloadFiles=CachedDownloads().Any();
    }

    IEnumerable<string> CachedDownloads(){
        if(!Directory.Exists(cache))return Array.Empty<string>();
        try{return Directory.GetFiles(cache).Where(path=>{
            string name=Path.GetFileName(path);
            return (name.Length==68&&name.EndsWith(".cfb",StringComparison.OrdinalIgnoreCase)||
                    name.Length==73&&name.EndsWith(".cfb.part",StringComparison.OrdinalIgnoreCase))&&
                name.Take(64).All(Uri.IsHexDigit);
        }).ToArray();}
        catch(IOException){return Array.Empty<string>();}
        catch(UnauthorizedAccessException){return Array.Empty<string>();}
    }

    public void DeleteCache(){
        if(Busy)return;
        bool wanted=Preferred;
        Select(false);
        Preferred=wanted;
        verifiedHashes.Clear();
        bool removed=Delete(IndexPath)&Delete(ArtIndexPath);
        foreach(var path in CachedDownloads())removed&=Delete(path);
        Art.HasDownloadFiles=CachedDownloads().Any();
        broken=false;
        Bytes=Total=0;
        ConfirmDelete=false;
        Status=removed?"Пакет удалён. Его можно скачать снова.":"Часть файлов не удалось удалить. Повторите попытку.";
    }

    public GameObject Model(string raw,int team){
        if(!Active||string.IsNullOrEmpty(raw))return null;
        var entries=models[team];
        if(entries.TryGetValue(raw,out var current))return current;
        var address=Address(raw,team);
        var pack=bundles.FirstOrDefault(value=>value.Contains(address));
        var prefab=pack?pack.LoadAsset<GameObject>(address):null;
        entries.Add(raw,prefab);
        return prefab;
    }

    internal bool Contains(string raw,int team)=>Active&&bundles.Any(pack=>pack.Contains(Address(raw,team)));
    internal T Asset<T>(string address) where T:UnityEngine.Object{
        if(!Active)return null;
        if(artBundle&&artBundle.Contains(address))return artBundle.LoadAsset<T>(address);
        var pack=bundles.FirstOrDefault(value=>value.Contains(address));
        return pack?pack.LoadAsset<T>(address):null;
    }
    internal Texture2D Icon(string address){
        if(!Active)return null;
        if(address.StartsWith("icon/",StringComparison.Ordinal)&&artIcons?[address.Substring(5)] is JValue alias)
            address="art-icon/"+(string)alias;
        if(!icons.TryGetValue(address,out var icon))icons[address]=icon=Asset<Texture2D>(address);
        return icon;
    }
    public void ClearPrefabs(){foreach(var entries in models)entries.Clear();}

    public void Select(bool preferred){
        if(Preferred==preferred)return;
        Preferred=preferred;
        if(!Preferred){
            Cancel();
            if(artBundle)artBundle.Unload(false);
            artBundle=null;artIcons=artEffects=null;
            MissingSheets.Clear();Geometry.Clear();
            if(bundles.Count>0){
                foreach(var pack in bundles)if(pack)pack.Unload(false);
                NativeAppearance.RemoveOptionalProfiles(optionalProfiles);optionalProfiles=Array.Empty<string>();
                bundles.Clear();icons.Clear();ClearPrefabs();Revision++;
            }
        }
    }

    public void Cancel()=>canceled=true;

    sealed class PackFile {
        public string Hash;
        public long Size;
    }
    List<PackFile> Parse(string json){
        try {
            var data=JObject.Parse(json);
            int format=(int?)data["format"]??0;
            if((string)data["platform"]!=Platform)return null;
            var rows=format==1?new JArray(data):format==2?data["files"] as JArray:null;
            if(rows==null||rows.Count==0||rows.Count>32)return null;
            var result=new List<PackFile>();
            var hashes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var row in rows){
                string hash=(string)row["sha256"];
                long size=(long?)row["size"]??0;
                if(size<=0||size>=int.MaxValue||hash==null||hash.Length!=64||hash.Any(c=>!Uri.IsHexDigit(c))||!hashes.Add(hash))return null;
                result.Add(new PackFile{Hash=hash.ToLowerInvariant(),Size=size});
            }
            return result;
        }catch(Exception e) when(e is Newtonsoft.Json.JsonException||e is ArgumentException||e is FormatException||e is OverflowException||e is InvalidOperationException){return null;}
    }
    string FileFor(PackFile file)=>Path.Combine(cache,file.Hash+".cfb");
    static long Length(string path){try{return new FileInfo(path).Length;}catch(IOException){return 0;}catch(UnauthorizedAccessException){return 0;}}
    static bool Valid(string path,PackFile index){
        if(Length(path)!=index.Size)return false;
        using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())
            return string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",""),index.Hash,StringComparison.OrdinalIgnoreCase);
    }

    public IEnumerator OpenCached(){
        if(!Preferred||Busy||!File.Exists(IndexPath))yield break;
        if(Active){yield return OpenCachedArt();yield break;}
        List<PackFile> files;
        try{files=Parse(File.ReadAllText(IndexPath));}catch(IOException){yield break;}catch(UnauthorizedAccessException){yield break;}
        if(files==null||files.Any(file=>Length(FileFor(file))!=file.Size)){broken=true;Status="Пакет отсутствует или повреждён";yield break;}
        Busy=true;canceled=false;Total=Bytes=files.Sum(file=>file.Size);
        try{yield return Open(files);if(Active)yield return OpenCachedArt();}finally{Busy=false;}
    }

    IEnumerator Open(List<PackFile> files){
        var opened=new List<AssetBundle>();
        try {
            foreach(var file in files){
                if(canceled||!Preferred)yield break;
                if(!verifiedHashes.Contains(file.Hash)){
                    Status="Проверяем пакет моделей";
                    var hash=Task.Run(()=>Valid(FileFor(file),file));
                    while(!hash.IsCompleted){if(canceled)yield break;yield return null;}
                    if(hash.IsFaulted||!hash.Result){broken=true;Status="Пакет повреждён. Скачайте его заново.";yield break;}
                    verifiedHashes.Add(file.Hash);
                }
                Status="Подключаем модели";
                var open=AssetBundle.LoadFromFileAsync(FileFor(file));yield return open;
                if(!open.assetBundle){broken=true;Status="Не удалось открыть пакет моделей";yield break;}
                opened.Add(open.assetBundle);
            }
            if(canceled||!Preferred)yield break;
            if(files.Count>1&&!InstallAppearanceProfiles(opened)){
                broken=true;Status="Каталог материалов Reforged повреждён";yield break;
            }
            bundles.AddRange(opened);opened.Clear();Revision++;Status="Модели Reforged готовы";
            Debug.Log("CF_REFORGED_MODELS_READY platform="+Platform+" size="+Total);
        } finally {foreach(var pack in opened)pack.Unload(true);}
    }

    bool InstallAppearanceProfiles(List<AssetBundle> opened){
        var common=opened.FirstOrDefault(pack=>pack.Contains("reforged-appearance"));
        var asset=common?common.LoadAsset<TextAsset>("reforged-appearance"):null;
        if(!asset)return false;
        try{optionalProfiles=NativeAppearance.AddOptionalProfiles(asset.text);return true;}
        catch(Exception e) when(e is Newtonsoft.Json.JsonException||e is ArgumentException||e is InvalidCastException||e is NullReferenceException){return false;}
    }

    public IEnumerator Download(bool explicitRequest,string localArt=null){
        if(!explicitRequest||!Draft||Busy)yield break;
        if(!Installed)yield return Transfer(true,Release);
        if(Installed&&!canceled&&!ArtInstalled){
            string source=Directory.Exists(localArt)?new Uri(Path.GetFullPath(localArt)+Path.DirectorySeparatorChar).AbsoluteUri:ArtPack040.Release;
            yield return TransferArt(source);
        }
    }

    internal IEnumerator TransferArt(string release){
        if(!Draft||Busy||!Installed||artBundle)yield break;
        Busy=true;canceled=false;Bytes=Total=0;
        try{
            using(var request=UnityWebRequest.Get(release+"reforged-art-"+Platform+".json")){
                request.timeout=20;
                var operation=request.SendWebRequest();
                while(!operation.isDone&&!canceled)yield return null;
                if(canceled){request.Abort();yield break;}
                if(request.result!=UnityWebRequest.Result.Success){Status="Дополнение HD пока недоступно";yield break;}
                string json=request.downloadHandler.text;
                var files=Parse(json);
                if(files==null||files.Count!=1){Status="Каталог HD-эффектов повреждён";yield break;}
                var file=files[0];Total=file.Size;
                yield return Fetch(file,release,0);
                if(canceled||!verifiedHashes.Contains(file.Hash))yield break;
                try{File.WriteAllText(ArtIndexPath,json);}
                catch(IOException){Status="Не удалось сохранить каталог HD-эффектов";yield break;}
                catch(UnauthorizedAccessException){Status="Не удалось сохранить каталог HD-эффектов";yield break;}
            }
            if(Active)yield return OpenCachedArt();
            else Status="HD-материалы скачаны. Нажмите «Применить».";
        }finally{Busy=false;if(canceled)Status="Загрузка остановлена. Можно продолжить позже.";}
    }

    IEnumerator OpenCachedArt(){
        if(!Active||artBundle||!ArtInstalled)yield break;
        List<PackFile> files;
        try{files=Parse(File.ReadAllText(ArtIndexPath));}
        catch(IOException){yield break;}
        catch(UnauthorizedAccessException){yield break;}
        if(files==null||files.Count!=1){Delete(ArtIndexPath);yield break;}
        var file=files[0];
        if(!verifiedHashes.Contains(file.Hash)){
            var verified=Task.Run(()=>Valid(FileFor(file),file));
            while(!verified.IsCompleted){if(canceled||!Active)yield break;yield return null;}
            if(verified.IsFaulted||!verified.Result){Delete(ArtIndexPath);yield break;}
            verifiedHashes.Add(file.Hash);
        }
        var request=AssetBundle.LoadFromFileAsync(FileFor(file));yield return request;
        var bundle=request.assetBundle;
        if(!bundle){Delete(ArtIndexPath);yield break;}
        if(canceled||!Active||artBundle){bundle.Unload(true);yield break;}
        try{
            artIcons=JObject.Parse(bundle.LoadAsset<TextAsset>("art-icons").text);
            artEffects=JObject.Parse(bundle.LoadAsset<TextAsset>("art-effects").text);
        }catch(Exception error) when(error is Newtonsoft.Json.JsonException||error is NullReferenceException){
            artIcons=artEffects=null;bundle.Unload(true);Delete(ArtIndexPath);yield break;
        }
        artBundle=bundle;icons.Clear();MissingSheets.Clear();Revision++;Status="Модели и эффекты Reforged готовы";
    }
    internal IEnumerator Transfer(bool explicitRequest,string release){
        if(!explicitRequest||!Draft||Busy||Active)yield break;
        Busy=true;canceled=false;Bytes=Total=0;Status="Проверяем пакет Reforged";
        try {
            if(!CreateCache()){Status="Не удалось создать папку для моделей";yield break;}
            string json;
            using(var request=UnityWebRequest.Get(release+"reforged-"+Platform+".json")){
                request.timeout=20;
                var task=request.SendWebRequest();
                while(!task.isDone&&!canceled)yield return null;
                if(canceled){request.Abort();yield break;}
                if(request.result!=UnityWebRequest.Result.Success){Status="Не удалось получить каталог. Повторите загрузку.";yield break;}
                json=request.downloadHandler.text;
            }
            var files=Parse(json);
            if(files==null){Status="Каталог моделей повреждён";yield break;}
            Total=files.Sum(file=>file.Size);
            long completed=0;
            foreach(var file in files){
                yield return Fetch(file,release,completed);
                if(canceled||!verifiedHashes.Contains(file.Hash))yield break;
                completed+=file.Size;
            }
            if(!SaveIndex(json)){Status="Не удалось сохранить каталог моделей";yield break;}
            broken=false;
            if(Preferred)yield return Open(files);
            else Status="Модели скачаны. Нажмите «Применить».";
        } finally{Busy=false;if(canceled)Status="Загрузка остановлена. Можно продолжить позже.";}
    }

    bool CreateCache(){try{Directory.CreateDirectory(cache);return true;}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}}
    bool SaveIndex(string json){try{File.WriteAllText(IndexPath,json);return true;}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}}
    static bool Delete(string path){try{File.Delete(path);return true;}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}}
    static bool Put(string partial,string final){try{File.Move(partial,final);return true;}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}}
    static DownloadHandlerFile FileHandler(string path,bool append){
        try{return new DownloadHandlerFile(path,append){removeFileOnAbort=false};}
        catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is InvalidOperationException){return null;}
    }

    IEnumerator Fetch(PackFile index,string release,long prior){
        string final=FileFor(index),partial=final+".part";
        Art.HasDownloadFiles=true;
        verifiedHashes.Remove(index.Hash);
        if(Length(final)==index.Size){
            var verify=Task.Run(()=>Valid(final,index));
            while(!verify.IsCompleted&&!canceled)yield return null;
            if(canceled)yield break;
            if(!verify.IsFaulted&&verify.Result){Bytes=prior+index.Size;verifiedHashes.Add(index.Hash);yield break;}
        }
        if(!Delete(final)){Status="Нет доступа к файлу моделей";yield break;}
        long start=Length(partial);
        if(start==index.Size){
            yield return VerifyPartial(index,partial,final,prior);
            if(canceled||verifiedHashes.Contains(index.Hash)||File.Exists(partial))yield break;
            start=0;
        }
        if(start>=index.Size||start>0&&Uri.TryCreate(release,UriKind.Absolute,out var uri)&&uri.IsFile){
            if(!Delete(partial)){Status="Нет доступа к файлу загрузки";yield break;}
            start=0;
        }
        for(int attempt=0;attempt<2;attempt++){
            using(var request=UnityWebRequest.Get(release+index.Hash+".cfb")){
                var handler=FileHandler(partial,start>0);
                if(handler==null){Status="Не удалось открыть файл загрузки";yield break;}
                request.downloadHandler=handler;
                if(start>0)request.SetRequestHeader("Range","bytes="+start+"-");
                Status="Скачиваем модели Reforged";
                var transfer=request.SendWebRequest();long previous=0;float progressAt=Time.realtimeSinceStartup;
                while(!transfer.isDone&&!canceled){
                    long downloaded=(long)request.downloadedBytes;
                    if(downloaded!=previous){previous=downloaded;progressAt=Time.realtimeSinceStartup;}
                    if(Time.realtimeSinceStartup-progressAt>45||downloaded>index.Size-start){request.Abort();break;}
                    Bytes=prior+Math.Min(index.Size,start+downloaded);yield return null;
                }
                if(canceled){request.Abort();yield break;}
                if(start>0&&(request.responseCode==200||request.responseCode==416||request.responseCode==206&&
                    request.GetResponseHeader("Content-Range")!=$"bytes {start}-{index.Size-1}/{index.Size}")){
                    request.Dispose();if(!Delete(partial)){Status="Нет доступа к файлу загрузки";yield break;}
                    start=0;Bytes=prior;continue;
                }
                if(request.result!=UnityWebRequest.Result.Success){Status="Соединение прервалось. Нажмите «Скачать» для продолжения.";yield break;}
            }
            yield return VerifyPartial(index,partial,final,prior);
            yield break;
        }
    }

    IEnumerator VerifyPartial(PackFile index,string partial,string final,long prior){
        Status="Проверяем скачанный файл";Bytes=prior+Length(partial);
        var verify=Task.Run(()=>Valid(partial,index));
        while(!verify.IsCompleted&&!canceled)yield return null;
        if(canceled)yield break;
        if(verify.IsFaulted||!verify.Result){Delete(partial);Status="Файл не прошёл проверку. Повторите загрузку.";yield break;}
        if(Put(partial,final))verifiedHashes.Add(index.Hash);
        else Status="Не удалось сохранить модели. Проверьте свободное место.";
    }
}
}
