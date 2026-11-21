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
using Object=UnityEngine.Object;

namespace CastleFight {
internal sealed class ReforgedMap {
    static readonly ConditionalWeakTable<PcGame,ReforgedMap> States=new ConditionalWeakTable<PcGame,ReforgedMap>();
    internal static ReforgedMap For(PcGame owner)=>States.GetOrCreateValue(owner);
    const string BaseUrl="https://github.com/boltongenry-ops/Castle-fight/releases/download/map-art-1/";
    static string FileName=>Application.platform==RuntimePlatform.Android?"map-android.cfb":"map-windows.cfb";
    static string Digest=>Application.platform==RuntimePlatform.Android?MapPackHashes.Android:MapPackHashes.Windows;
    string path;
    internal bool Preferred,Draft,Panel,Busy,Confirm;
    internal string Status="";
    internal float Progress;
    internal bool Installed=>File.Exists(path);
    UnityWebRequest request;
    AssetBundle bundle;
    readonly List<GameObject> additions=new List<GameObject>();
    readonly List<Renderer> hidden=new List<Renderer>();
    readonly Dictionary<Renderer,Material> ground=new Dictionary<Renderer,Material>();
    readonly List<Material> materials=new List<Material>();
    internal void Init(string settings){path=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settings)),"reforged-map-v1",FileName);}
    internal void Abort(){request?.Abort();}
    static string Hash(string file){using(var stream=File.OpenRead(file))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    internal IEnumerator Download(NativeMapCamera camera){
        if(Busy||Installed)yield break;
        Busy=true;Progress=0;Status="Загрузка карты…";
        string part=path+".part";
        try{
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using(request=UnityWebRequest.Get(BaseUrl+FileName)){
                request.downloadHandler=new DownloadHandlerFile(part){removeFileOnAbort=true};
                var operation=request.SendWebRequest();
                while(!operation.isDone){Progress=request.downloadProgress;yield return null;}
                if(request.result!=UnityWebRequest.Result.Success){Status="Загрузка прервана. Можно повторить.";yield break;}
            }
            request=null;Status="Проверка файлов…";var task=Task.Run(()=>Hash(part));while(!task.IsCompleted)yield return null;
            if(task.IsFaulted||task.Result!=Digest){Status="Файл не прошёл проверку. Повторите загрузку.";yield break;}
            File.Move(part,path);Status="Карта скачана";
        }finally{request=null;Busy=false;if(File.Exists(part))File.Delete(part);}
        if(Preferred)yield return Apply(camera);
    }
    internal void Remove(){
        if(Busy)return;
        Restore();Confirm=false;
        try{if(File.Exists(path))File.Delete(path);Status="Пакет карты удалён";}
        catch(IOException){Status="Не удалось удалить пакет карты";}
    }
    internal void Restore(){
        foreach(var obj in additions)if(obj){obj.SetActive(false);Object.Destroy(obj);}
        foreach(var r in hidden)if(r)r.enabled=true;
        foreach(var item in ground)if(item.Key)item.Key.sharedMaterial=item.Value;
        foreach(var mat in materials)if(mat)Object.Destroy(mat);
        additions.Clear();hidden.Clear();ground.Clear();materials.Clear();
        if(bundle)bundle.Unload(true);bundle=null;
    }
    sealed class Prop {
        internal Mesh Mesh;internal Material[] Materials;internal bool Sized;
        internal Vector3 Scale,Offset;internal Quaternion Rotation=Quaternion.identity;
    }
    static void Fit(Prop prop,Transform parent,MeshRenderer[] originals,bool wall){
        bool first=true;var box=new Bounds();
        foreach(var r in originals){
            var mesh=r.GetComponent<MeshFilter>();if(!mesh||!mesh.sharedMesh)continue;
            var b=mesh.sharedMesh.bounds;var matrix=parent.worldToLocalMatrix*r.localToWorldMatrix;
            for(int i=0;i<8;i++){
                var p=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                if(first){box=new Bounds(p,Vector3.zero);first=false;}else box.Encapsulate(p);
            }
        }
        if(first)return;
        var hd=prop.Mesh.bounds;
        prop.Scale=Vector3.one*(box.size.magnitude/Mathf.Max(.001f,hd.size.magnitude));
        if(wall){
            // Both wall orientations exist as distinct MDX files. Match the placed classic footprint.
            bool turn=(hd.size.x>hd.size.z)!=(box.size.x>box.size.z);
            prop.Rotation=turn?Quaternion.Euler(0,90,0):Quaternion.identity;
            var size=turn?new Vector3(box.size.z,box.size.y,box.size.x):box.size;
            prop.Scale=new Vector3(size.x/Mathf.Max(.001f,hd.size.x),size.y/Mathf.Max(.001f,hd.size.y),size.z/Mathf.Max(.001f,hd.size.z));
        }
        var baseCenter=new Vector3(hd.center.x,hd.min.y,hd.center.z);
        prop.Offset=new Vector3(box.center.x,box.min.y,box.center.z)-prop.Rotation*Vector3.Scale(prop.Scale,baseCenter);
        prop.Sized=true;
    }
    internal IEnumerator Apply(NativeMapCamera camera){
        if(Busy)yield break;
        if(!Preferred){Restore();if(Installed)Status="Карта скачана";yield break;}
        if(bundle||!Installed||!camera||!camera.sceneryRoot)yield break;
        Busy=true;Progress=0;Status="Подготовка карты…";bool complete=false;
        try{
            var check=Task.Run(()=>Hash(path));while(!check.IsCompleted)yield return null;
            if(check.IsFaulted||check.Result!=Digest){Status="Пакет повреждён. Удалите и скачайте его снова.";yield break;}
            var open=AssetBundle.LoadFromFileAsync(path);yield return open;bundle=open.assetBundle;
            if(!bundle){Status="Не удалось открыть пакет карты";yield break;}
            var load=bundle.LoadAllAssetsAsync();yield return load;
            var scenery=camera.sceneryRoot.transform;var terrain=scenery.parent.Cast<Transform>()
                .Where(t=>t.name.StartsWith("Terrain",StringComparison.Ordinal))
                .SelectMany(t=>t.GetComponentsInChildren<MeshRenderer>(true))
                .Where(r=>r.name.StartsWith("Ground ",StringComparison.Ordinal)).ToArray();
            if(terrain.Length==0){Status="Геометрия карты недоступна";yield break;}
            foreach(var r in terrain){
                if(!int.TryParse(r.name.Substring(7),out int number))continue;
                var texture=bundle.LoadAsset<Texture2D>("ground"+number%9);if(!texture)throw new InvalidDataException(r.name);
                var original=r.sharedMaterial;var mat=new Material(original){mainTexture=texture};materials.Add(mat);
                ground.Add(r,original);r.sharedMaterial=mat;
            }
            var index=JObject.Parse(bundle.LoadAsset<TextAsset>("catalog").text);
            var props=new Dictionary<string,Prop>();var skins=new Dictionary<string,Material>();
            var shader=Shader.Find("CastleFight/NativeMapProp");
            foreach(var entry in index.Properties()){
                var prop=new Prop{Mesh=bundle.LoadAsset<Mesh>(entry.Name)};
                prop.Materials=entry.Value.Values<string>().Select(key=>{
                    if(!skins.TryGetValue(key,out var mat)){
                        mat=new Material(shader){mainTexture=bundle.LoadAsset<Texture2D>(key),enableInstancing=true};skins[key]=mat;materials.Add(mat);
                    }
                    return mat;
                }).ToArray();props.Add(entry.Name,prop);
            }
            foreach(var placement in scenery.GetComponentsInChildren<NativePlacement>(true)){
                if(!props.TryGetValue(placement.modelKey??"",out var p)||!p.Mesh)continue;
                var originals=placement.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled).ToArray();
                if(originals.Length==0)continue;
                if(!p.Sized)Fit(p,placement.transform,originals,placement.modelKey.Contains("ruinswall"));
                if(!p.Sized)continue;
                var obj=new GameObject("Reforged "+placement.modelKey);additions.Add(obj);obj.transform.SetParent(placement.transform,false);
                obj.transform.localPosition=p.Offset;obj.transform.localRotation=p.Rotation;obj.transform.localScale=p.Scale;
                obj.AddComponent<MeshFilter>().sharedMesh=p.Mesh;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterials=p.Materials;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                foreach(var r in originals){r.enabled=false;hidden.Add(r);}
                if(additions.Count%64==0)yield return null;
            }
            complete=true;Debug.Log("CF_MAP_REFORGED layers="+ground.Count+" props="+additions.Count);Status="Карта Reforged включена";
        }finally{Busy=false;if(!complete)Restore();}
    }
}
}
