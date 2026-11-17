using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using CastleFight;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
public static class BuildClassicProjectiles {
    const string Root="Assets/ClassicProjectiles";
    static T Save<T>(T value,string path) where T:UnityEngine.Object {var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(value,path);return value;}
    public static void Prepare(){
        AssetDatabase.Refresh();Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Clips");AssetDatabase.Refresh();
        var source=JObject.Parse(File.ReadAllText(Root+"/source.json"));var white=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/White.asset");if(!white){white=new Texture2D(2,2);white.SetPixels(Enumerable.Repeat(Color.white,4).ToArray());white.Apply();white=Save(white,Root+"/White.asset");}
        var keys=((JObject)source["weapons"]).Properties().Select(p=>(string)p.Value).Distinct().ToArray();
        foreach(string key in keys)for(int team=0;team<2;team++){
            PrepareArt(source,key,team,Root,white,false);
        }
        var indexData=new JObject{["weapons"]=source["weapons"],["paths"]=source["paths"]};File.WriteAllText(Root+"/runtime.json",indexData.ToString());AssetDatabase.Refresh();AssetDatabase.SaveAssets();Debug.Log("CF_CLASSIC_PROJECTILES_PREPARED art="+keys.Length+" prefabs="+keys.Length*2);
    }
    public static void PrepareArt(JObject source,string key,int team,string output,Texture2D white,bool oneShot){
            var root=new GameObject(key+"-t"+team);var view=root.AddComponent<ClassicProjectileModel>();view.key=key;view.clips=new AnimationClip[0];
            string path=(string)source["assets"]?[key];var config=source["appearance"]?[key] as JObject;
            if(path!=null){var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset)throw new Exception("Missing projectile mesh "+path);var model=UnityEngine.Object.Instantiate(asset,root.transform);view.geometry=model;view.profile=config?.ToString();int index=0;
                foreach(var renderer in model.GetComponentsInChildren<Renderer>()){
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++,index++){
                        var surface=config?["surfaces"]?[index];var color=surface?["color"] as JArray;string blend=(string)surface?["blend"]??"alpha";bool transparent=blend=="alpha"||blend=="additive";
                        var mat=new Material(Shader.Find("CastleFight/NativeUnit")){mainTexture=materials[i]&&materials[i].mainTexture?materials[i].mainTexture:white};mat.SetColor("_Tint",color!=null?new Color((float)color[0],(float)color[1],(float)color[2],(float)color[3]):Color.white);mat.SetFloat("_Team",(bool?)surface?["team"]==true?1:0);mat.SetColor("_TeamColor",team==0?new Color(.1f,.3f,.95f):new Color(.85f,.08f,.06f));
                        mat.SetInt("_SrcBlend",(int)(transparent?BlendMode.SrcAlpha:BlendMode.One));mat.SetInt("_DstBlend",(int)(blend=="additive"?BlendMode.One:transparent?BlendMode.OneMinusSrcAlpha:BlendMode.Zero));mat.SetInt("_ZWrite",transparent?0:1);mat.SetFloat("_Unlit",blend=="additive"||(bool?)surface?["unlit"]==true?1:0);mat.SetFloat("_Cutoff",(float?)surface?["cutoff"]??.002f);mat.renderQueue=transparent?3000:2450;materials[i]=Save(mat,output+"/Materials/"+key+"-t"+team+"-"+index+".mat");
                    }renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.Off;
                }
                view.clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Select((clip,i)=>{var copy=UnityEngine.Object.Instantiate(clip);copy.legacy=true;copy.name=clip.name;copy=Save(copy,output+"/Clips/"+key+"-t"+team+"-"+i+".anim");copy.name=clip.name;EditorUtility.SetDirty(copy);return copy;}).ToArray();
            }
            if(source["particles"]?[key] is JObject profile){var clips=source["clips"]?[key] as JObject;var interval=(JArray)((oneShot?clips?["Birth"]:null)??clips?["Stand"]??clips?["Birth"]??clips?["Death"]??new JArray(0,1000));BuildProjectileParticles.Add(root,profile,interval,key+"-t"+team,output+"/Materials");}
            PrefabUtility.SaveAsPrefabAsset(root,output+"/Prefabs/"+key+"-t"+team+".prefab");UnityEngine.Object.DestroyImmediate(root);
    }
    public static void Windows(){Prepare();Build(BuildTarget.StandaloneWindows64,"Builds/ClassicMissilesWindows");}
    public static void Android(){Build(BuildTarget.Android,"Builds/ClassicMissilesAndroid");}
    static void Build(BuildTarget target,string output){Directory.CreateDirectory(output);var assets=Directory.GetFiles(Root+"/Prefabs","*.prefab").Concat(new[]{Root+"/runtime.json"}).ToArray();var result=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="classic-missiles",assetNames=assets}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,target);if(!result)throw new Exception("Classic projectile bundle failed");Debug.Log("CF_CLASSIC_PROJECTILES_BUNDLE_PASS "+target);}
}
