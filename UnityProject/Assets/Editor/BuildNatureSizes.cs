using System;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
public static class BuildNatureSizes {
    public static void BuildWindows(){Normalize();BuildFullMap.RebuildRuntimeWindows();}
    public static void Normalize() {
        var reference=(JObject)JObject.Parse(File.ReadAllText("../nature-size-reference.json"))["models"];
        var appearance=JObject.Parse(File.ReadAllText("Assets/Resources/native-appearance.json"));var audit=new JArray();
        foreach(var item in reference.Properties())for(int team=0;team<2;team++) {
            string path=$"Assets/GeneratedReforged/{item.Name}-t{team}.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try {
                var view=root.GetComponent<PcModel>();var model=view.animatedRoot;model.transform.localScale=Vector3.one;
                foreach(var animator in model.GetComponentsInChildren<Animator>())animator.enabled=false;
                var player=model.GetComponent<Animation>();if(!player)player=model.AddComponent<Animation>();
                foreach(var clip in view.clips)player.AddClip(clip,clip.name);
                var stand=view.clips.First(c=>c.name.StartsWith("Stand",StringComparison.OrdinalIgnoreCase));
                player.Play(stand.name);player[stand.name].time=.15f;player.Sample();
                var cfg=appearance[item.Name+"_ref"];var interval=(JArray)cfg["clips"][stand.name];var surfaces=(JArray)cfg["surfaces"];
                Bounds bounds=new Bounds();bool found=false;var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();skin.BakeMesh(baked,true);var vertices=baked.vertices;
                for(int sub=0;sub<baked.subMeshCount;sub++) {
                    var surface=surfaces[sub];if(NativeAppearance.Sample((JObject)surface["geoset"],interval,150)*NativeAppearance.Sample((JObject)surface["layer"],interval,150)<.01f)continue;
                    foreach(int index in baked.GetTriangles(sub)) {
                        // BakeMesh includes the inherited skeleton scale; applying TransformPoint would apply it twice.
                        var point=skin.transform.position+skin.transform.rotation*vertices[index];if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
                    }
                }
                UnityEngine.Object.DestroyImmediate(baked);float target=(float)item.Value["height"];
                if(!found||bounds.size.y<.01f||float.IsInfinity(bounds.size.y)||float.IsNaN(bounds.size.y))throw new Exception("Invalid Nature size: "+item.Name);
                float correction=target/bounds.size.y;model.transform.localScale=Vector3.one*correction;view.godotHeight=target;view.visualScale=correction;
                player.Stop();PrefabUtility.SaveAsPrefabAsset(root,path);
                audit.Add(new JObject {["rawcode"]=item.Name,["team"]=team,["sourceHeight"]=bounds.size.y,["godotHeight"]=target,["correction"]=correction,["resultHeight"]=bounds.size.y*correction});
            } finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();File.WriteAllText("../nature-size-audit.json",new JObject{["status"]="PASS",["samples"]=audit,["pose"]="Stand at 0.15 s; visible geosets"}.ToString());Debug.Log("CF_NATURE_SIZES_PASS models="+audit.Count);
    }
}
