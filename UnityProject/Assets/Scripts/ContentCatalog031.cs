using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
namespace CastleFight {
public static class ContentCatalog031 {
    public static string AssetUrl(JObject catalog,JToken row)=>(string)row["url"]??(string)catalog["baseUrl"]+"/"+(string)row["file"];
    public static bool Compatible(JObject data,string player){
        if((int?)data?["schema"]!=1||!Release031.Https((string)data["baseUrl"])||(string)data["scene"]!="Assets/Scenes/CastleFightMap.unity")return false;
        string minimum=(string)data["minimumPlayerVersion"]??(string)data["version"];if(!System.Version.TryParse(minimum,out _)||Release031.Newer(minimum,player))return false;
        if(!(data["bundles"] is JArray bundles)||!(data["resources"] is JArray resources)||!(data["streaming"] is JArray media))return false;
        var names=bundles.Select(row=>(string)row["name"]).ToArray();if(names.Distinct().Count()!=names.Length||!new[]{"frontend","mapscene","builders"}.All(names.Contains))return false;
        return bundles.Concat(media).All(row=>Release031.Https(AssetUrl(data,row))&&((string)row["sha256"]??"").Length==64&&(long?)row["size"]>0&&!((string)row["file"]??"").Contains("/")&&!((string)row["file"]??"").Contains(".."))&&resources.All(row=>names.Contains((string)row["bundle"]))&&media.All(row=>!Path.IsPathRooted((string)row["path"])&&!((string)row["path"]??"").Split('/','\\').Contains(".."));
    }
    public static IEnumerator Resolve(JObject embedded,string cache,Action<JObject> ready){
        string file=Path.Combine(cache,"catalog031.json");JObject chosen=embedded;
        if(File.Exists(file))try{var stored=JObject.Parse(File.ReadAllText(file));if(Compatible(stored,Application.version)&&!Release031.Newer((string)embedded["version"],(string)stored["version"]))chosen=stored;}catch(Exception){}
        string url=null;
        using(var release=UnityWebRequest.Get(Release031.IndexUrl)){release.timeout=5;yield return release.SendWebRequest();if(release.result==UnityWebRequest.Result.Success)try{var data=JObject.Parse(release.downloadHandler.text);if((int?)data["schema"]==1&&!Release031.Newer((string)data["android"]?["minimumPlayerVersion"],Application.version))url=(string)data["android"]?["contentManifest"];}catch(Exception){}}
        if(Release031.Https(url))using(var request=UnityWebRequest.Get(url)){request.timeout=5;yield return request.SendWebRequest();if(request.result==UnityWebRequest.Result.Success)try{var data=JObject.Parse(request.downloadHandler.text);if(Compatible(data,Application.version)&&!Release031.Newer((string)chosen["version"],(string)data["version"])){
            chosen=data;string pending=file+".pending";File.WriteAllText(pending,data.ToString());if(File.Exists(file))File.Delete(file);File.Move(pending,file);
        }}catch(Exception){}}
        ready(chosen);
    }
}
}
