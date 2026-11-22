using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;

namespace CastleFightLauncher {
public sealed class FileRow {public string path,sha256;public long size,offset,compressedSize;public int method;}
public sealed class WindowsRelease {public string url,sha256,entryPoint,version;public long size;public List<FileRow> files;}
public sealed class Release {
    public int schema;public string version;public WindowsRelease windows;
    public string WindowsVersion(){return windows.version??version;}
}
public sealed class UpdateResult {public int checkedFiles,changedFiles;public long networkBytes;}
public sealed class UpdateCore {
    public const string ManifestUrl="https://raw.githubusercontent.com/boltongenry-ops/Castle-fight/main/release.json";
    public readonly string Root;readonly bool fixture;
    static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=4*1024*1024};
    public UpdateCore(string root,bool testFixture=false){Root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;fixture=testFixture;}
    public static string Hash(string path){using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    public string Target(string path){
        if(string.IsNullOrWhiteSpace(path)||Path.IsPathRooted(path)||path.Contains(":")||path.Split('/','\\').Any(p=>p==".."||p=="."||p.StartsWith(".",StringComparison.Ordinal)))throw new InvalidDataException("Недопустимый путь обновления");
        string result=Path.GetFullPath(Path.Combine(Root,path.Replace('/',Path.DirectorySeparatorChar)));if(!result.StartsWith(Root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Путь вне папки игры");return result;
    }
    public Release Parse(string text){var r=Json.Deserialize<Release>(text);Validate(r);return r;}
    public void Validate(Release r){
        Uri uri;Version v;if(r==null||r.schema!=1||!Version.TryParse(r.version,out v)||r.windows==null||r.windows.files==null||r.windows.files.Count<1||r.windows.files.Count>5000||!Uri.TryCreate(r.windows.url,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&!(fixture&&uri.IsLoopback&&uri.Scheme=="http")))throw new InvalidDataException("Некорректный каталог версии");
        if(!Version.TryParse(r.WindowsVersion(),out v))throw new InvalidDataException("Некорректная версия Windows");
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in r.windows.files){Target(row.path);if(!paths.Add(row.path)||row.size<0||row.size>12L*1024*1024*1024||row.offset<0||row.compressedSize<0||row.offset>r.windows.size-row.compressedSize||row.sha256==null||row.sha256.Length!=64||!row.sha256.All(Uri.IsHexDigit)||row.method!=0&&row.method!=8)throw new InvalidDataException("Некорректный файл обновления");}
        if(!paths.Contains(r.windows.entryPoint)||r.windows.entryPoint!="CastleFightMap.exe")throw new InvalidDataException("Не найден исполняемый файл игры");
    }
    public Release Fetch(){
        var request=(HttpWebRequest)WebRequest.Create(ManifestUrl+"?t="+DateTime.UtcNow.Ticks);request.Timeout=12000;request.ReadWriteTimeout=12000;request.UserAgent="CastleFightLauncher/1.0";
        using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())){string value=reader.ReadToEnd();if(value.Length>4*1024*1024)throw new InvalidDataException("Каталог слишком большой");return Parse(value);}
    }
    bool Matches(string path,FileRow row){return File.Exists(path)&&new FileInfo(path).Length==row.size&&Hash(path)==row.sha256;}
    public Release Installed(){string file=Path.Combine(Root,".installed.json");return File.Exists(file)?Parse(File.ReadAllText(file)):null;}
    public UpdateResult Apply(Release release,Action<string,float> progress,CancellationToken cancel){
        Validate(release);Directory.CreateDirectory(Root);string state=Path.Combine(Root,".updater");Directory.CreateDirectory(state);
        using(var gate=new FileStream(Path.Combine(state,"update.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
            Release installedRelease=null;try{installedRelease=Installed();}catch(Exception){}if(installedRelease!=null&&new Version(release.WindowsVersion())<new Version(installedRelease.WindowsVersion()))throw new InvalidOperationException("Установленная версия новее каталога обновлений");
            var result=new UpdateResult();var missing=new List<FileRow>();int count=release.windows.files.Count;
            foreach(var row in release.windows.files){cancel.ThrowIfCancellationRequested();progress("Проверяем файлы · "+(++result.checkedFiles)+" / "+count,.2f*result.checkedFiles/count);if(!Matches(Target(row.path),row))missing.Add(row);}
            result.changedFiles=missing.Count;long total=missing.Sum(r=>r.compressedSize),done=0;
            string download=Path.Combine(state,"downloads"),stage=Path.Combine(state,"staged");Directory.CreateDirectory(download);Directory.CreateDirectory(stage);
            foreach(var row in missing){
                cancel.ThrowIfCancellationRequested();string prepared=Path.Combine(stage,row.sha256);if(!Matches(prepared,row)){
                    string fragment=Path.Combine(download,row.sha256+"-"+row.offset+"-"+row.compressedSize+".part");
                    long received=Download(release.windows,row,fragment,cancel,delegate(long bytes){progress("Загружаем "+row.path+" · "+((done+bytes)/1048576)+" / "+(total/1048576)+" МБ",.2f+(total>0?.7f*(done+bytes)/total:0));});result.networkBytes+=received;
                    string pending=prepared+".pending";
                    using(var input=File.OpenRead(fragment))using(var output=File.Create(pending)){
                        if(row.method==8)using(var decoder=new DeflateStream(input,CompressionMode.Decompress,true))Copy(decoder,output,row.size,cancel);else Copy(input,output,row.size,cancel);
                    }
                    if(!Matches(pending,row)){File.Delete(pending);File.Delete(fragment);throw new InvalidDataException("Проверка файла не прошла: "+row.path);}
                    if(File.Exists(prepared))File.Delete(prepared);File.Move(pending,prepared);File.Delete(fragment);
                }done+=row.compressedSize;
            }
            cancel.ThrowIfCancellationRequested();var installed=new List<string>();string backup=Path.Combine(state,"rollback-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);
            try{
                int i=0;foreach(var row in missing){string target=Target(row.path);Directory.CreateDirectory(Path.GetDirectoryName(target));string saved=Path.Combine(backup,(i++).ToString());
                    string incoming=target+".updating";File.Copy(Path.Combine(stage,row.sha256),incoming,true);if(File.Exists(target))File.Replace(incoming,target,saved);else File.Move(incoming,target);installed.Add(row.path);progress("Устанавливаем обновление",.9f+.09f*i/Math.Max(1,missing.Count));}
                string current=Path.Combine(Root,".installed.json"),pending=current+".pending";File.WriteAllText(pending,Json.Serialize(release));if(File.Exists(current))File.Replace(pending,current,null);else File.Move(pending,current);
            }catch{
                for(int i=installed.Count-1;i>=0;i--){string target=Target(installed[i]),saved=Path.Combine(backup,i.ToString());if(File.Exists(saved)){if(File.Exists(target))File.Replace(saved,target,null);else File.Move(saved,target);}else if(File.Exists(target))File.Delete(target);}throw;
            }
            // This directory is created above, never derived from remote paths.
            try{Directory.Delete(backup,true);Directory.Delete(stage,true);}catch(IOException){}catch(UnauthorizedAccessException){}progress("Готово · версия "+release.WindowsVersion(),1);return result;
        }
    }
    static void Copy(Stream input,Stream output,long maximum,CancellationToken cancel){byte[] buffer=new byte[128*1024];long count=0;int n;while((n=input.Read(buffer,0,buffer.Length))>0){cancel.ThrowIfCancellationRequested();count+=n;if(count>maximum)throw new InvalidDataException("Размер файла превышен");output.Write(buffer,0,n);}if(count!=maximum)throw new InvalidDataException("Неполный файл обновления");}
    long Download(WindowsRelease release,FileRow row,string destination,CancellationToken cancel,Action<long> report){
        if(row.compressedSize==0){File.WriteAllBytes(destination,new byte[0]);return 0;}long network=0;
        for(int attempt=0;attempt<3;attempt++){
            long have=File.Exists(destination)?new FileInfo(destination).Length:0;if(have>row.compressedSize){File.Delete(destination);have=0;}if(have==row.compressedSize)return network;
            var request=(HttpWebRequest)WebRequest.Create(release.url);request.Timeout=30000;request.ReadWriteTimeout=30000;request.UserAgent="CastleFightLauncher/1.0";request.AddRange(row.offset+have,row.offset+row.compressedSize-1);
            using(cancel.Register(request.Abort))try{using(var response=(HttpWebResponse)request.GetResponse()){
                string expected="bytes "+(row.offset+have)+"-"+(row.offset+row.compressedSize-1)+"/"+release.size;
                if(response.StatusCode!=HttpStatusCode.PartialContent||response.Headers["Content-Range"]!=expected)throw new InvalidDataException("Сервер не поддержал загрузку выбранного файла");
                using(var input=response.GetResponseStream())using(var output=new FileStream(destination,FileMode.Append,FileAccess.Write,FileShare.None)){byte[] bytes=new byte[128*1024];int n;while((n=input.Read(bytes,0,bytes.Length))>0){cancel.ThrowIfCancellationRequested();have+=n;network+=n;if(have>row.compressedSize)throw new InvalidDataException("Некорректный диапазон обновления");output.Write(bytes,0,n);report(have);}}
                if(have==row.compressedSize)return network;
            }}catch(WebException){cancel.ThrowIfCancellationRequested();if(attempt==2)throw;}
        }throw new IOException("Загрузка прервана. Повторите проверку.");
    }
}
}
