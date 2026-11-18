using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
[DefaultExecutionOrder(200)]
public sealed class ClassicWispBody040 : MonoBehaviour {
    // The classic Wisp MDX animates the placement of its planes.
    // Its Billboarded flags need a separate camera-facing pass after animation.
    static readonly string[] planes={"Plane02","Plane03","Plane04","Plane05","Plane12","Plane24","Plane42","Plane06","Plane08","Plane21","Plane07","Plane09","Plane22","Plane16","Plane14","Plane23","Plane26","Plane28","Plane29","Plane36","Plane40","Plane30","Plane31","Plane32","Plane33","Plane34","Plane35","Plane37","Plane38","Plane39"};
    static readonly Dictionary<string,JArray> clips=new Dictionary<string,JArray>(StringComparer.OrdinalIgnoreCase){
        ["Stand"]=new JArray(33,700),["Birth"]=new JArray(733,1100),["Death"]=new JArray(1167,3333),
        ["Stand Lumber"]=new JArray(4567,6600),["Stand Work"]=new JArray(7033,8033)
    };
    static readonly Dictionary<(Material,bool,int),Material> materials=new Dictionary<(Material,bool,int),Material>();
    static readonly Texture2D[] teamGlow=new Texture2D[2];
    const string blueGlow="iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAADFUlEQVR4nK1XAXKjMAxcO27T3t3/n9oLCdg3K0sgHMC0Oc84wOBoV9JKNgEIBUfDXm+uCno9MmFrtkc6fHvCwDF4f0S8OsIJjq9FoOe9ogcfDM+ok2H0NGAGQms47OugnE9N6BOwlR6Qf2u8nEFL8/xfRBh0Rgfu5VOqH4WAeXkWIuVVAnFNIMQ1kcJ7BQsZKHkhIfPRcS+ioISN3Dkw8zgkILwB8aLPtEAfFJTgmdcRyHegTGrLE7OokjBcBMpOba3A34H4XkmES30nJA1gAiLBvbR8Gp71kHbFOudZwQX4A4ifQLyuSQiBESiP6jmGxak8KhmueQZL+96rd1HDTvDLrzqFxIdGJdZQC/gAhJsSZzpoxoDpyLMo05z/1vs5/wS/VkAh8FvnZ00JSVjO819goj6IPGr4eeUgyVW3ktu0E/9FhLPwNPwkkUjgT00F3hbvJzUn6WAqJmAaXOWoKB2HtA3elJ1ogCSu1XOJBAkwCpdKwIPHG1BIbtIU+VLe0sDmaLQggrNoaEpEBxHI1EoB8kNT9gaAqbByZb/wnXQJQXru2Q1T1vzsAH/oMcPOq4qU4IXkkoKqDYJKBKxp0eC6JNO2966Gra8XU7C1W6pbhcUeYHMut2butOS09tz+wEFjWmJUtAmLYsu3qgmuzVYFzPtQ11ATov5RSXq7uwTarVU9ZV6ZBhonSP6qoOQV72sCE999AeXmiGiHlAjZLrmMhM1hYWZ4J+1m99pkqHbJ56SC833ASNz0+bGAr6K74KQ+Ad5PamzQtOTqvRDQZ2vD0oxIgM+WAtPIXiveFaAa5uAGI4Nk7ro36F4gxq0dc5KAklhtz2Y3nD0PFCWgf84ajUDDjICW6OyhRkoiMSxt+ECEoXsko6e+GbHO5d7aqx5IjIDk3OV9dsDP75yIyuhOPqpo32xmApx8Z0T6x7GTBHIjTN1UhFDTP+xMOHvPU48d2V46FQe33O8RjtuKhBPcfHre1kHoE2h3sKNjdtn5nmjfLbYSusNS8NPRHEK+/W0YXsTufCGd+zY8/NRrGD4RPs7wP1kHweXJovbAAAAAAElFTkSuQmCC";
    const string redGlow="iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAACXElEQVR4nMVXAW6DMAy0aWjXbf9/6lag4CnMx65WILSdNktWGwi58zk2QVXEZMO2buodc9YsVe5XF9hkv8Ma+WdLzzxsKwrpHQqlPSClhZW8BLQ3NVrbhBE8AjOYkWP8K5tQ3ZvwnwnApzB+OgVNAG0K1xgcjmvDjgCtJOcCproAZbatiBxIgWR2A5z9KiK9iIw+h+8toFsKaMg5wI/uIIF7WHx0cAbgNOg9KVBSAeAvInIWkROTUP2O2myWO0fe0TrXsDeqBDTkF7Jn8Ff3s4+Tz8mRD6rSmcmFVBEC5r1yQ0Ar0bceMQi8uZ9dlUQ5/1SdVZnMlsjzrzhJTs1mCjTIDwXOROIdqVCdpc/RY7GrqvRmM2hHa42PpKAhEici8e7/c8SZQJpb2nfkOQ0n1RmwMbsp2yqBNSKHQOTFHSTnmleV1mzZoCjXWFWchhRzEpke7GdGvn7IILR47gPm5JLPX+ueUijJFPBvJsW+HjseNlZsRLEVb7XkxNHxxIk2zpV2euc5ziogcuS98zkDPTPuJSBBJkQ6uNwA/qCH+kDgw/1CRKDU7kYkBK6kQO8LJ7rehj5wKRBg8NKZIdUIGDod1fTkAC2N0YY/nQDSAAK7FbBAYgjpGR3oSC8kyDy4X4hEBI+HlFRSgMkM4WEA9U5AQlXwCwlteGsTau1IdgzNCPXPbkQAPoUA1soxScXwfkeuR5I+nogAXCu9uxSAoaPxb+wf3KQwBkF5VIHYv7k62NZk3gKf164pEN9gW8fsEvDaGGslqRhONo9aSa0//Ta0yv2nvg2zPfv1/AWp1ywNXqtgJgAAAABJRU5ErkJggg==";
    ProjectileBillboards billboards;
    NativeMapEmitter[] emitters;
    Transform anchor;
    Camera worldCamera;
    string currentClip;
    public Vector3 Center=>anchor?anchor.position:transform.position+Vector3.up*.77f;
    internal int FacingPlanes=>billboards?.Count??0;
    internal string CurrentClip=>currentClip;

    public static bool IsWisp(string raw)=>raw=="h051"||raw=="h06B";

    void Awake(){
        var model=GetComponent<PcModel>();
        var geometry=model.animatedRoot;
        billboards=new ProjectileBillboards(geometry,planes);
        anchor=geometry.GetComponentsInChildren<Transform>(true).FirstOrDefault(node=>node.name=="Dummy01root");
        emitters=GetComponentsInChildren<NativeMapEmitter>(true);
        bool red=model.name.Contains("-t1");
        foreach(var skin in geometry.GetComponentsInChildren<SkinnedMeshRenderer>()){
            var source=skin.sharedMaterials;
            if(source.Length!=6)continue;
            foreach(int index in new[]{1,2,3,5}){
                if(!source[index])continue;
                var key=(source[index],red,index);
                if(!materials.TryGetValue(key,out var material)){
                    material=new Material(source[index]){hideFlags=HideFlags.HideAndDontSave};
                    if(index==1)material.SetColor("_Tint",new Color(.764706f,.658824f,.541176f));
                    else material.mainTexture=TeamGlow(red);
                    materials[key]=material;
                }
                source[index]=material;
            }
            skin.sharedMaterials=source;
        }
    }

    static Texture2D TeamGlow(bool red){
        int team=red?1:0;
        if(teamGlow[team])return teamGlow[team];
        var image=new Texture2D(2,2,TextureFormat.RGBA32,false){
            name=red?"WC3 TeamGlow00":"WC3 TeamGlow01",wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave
        };
        image.LoadImage(Convert.FromBase64String(red?redGlow:blueGlow),true);
        return teamGlow[team]=image;
    }

    public void SetClip(string name,bool loop){
        if(string.Equals(currentClip,name,StringComparison.OrdinalIgnoreCase)||!clips.TryGetValue(name,out var interval))return;
        currentClip=name;
        foreach(var emitter in emitters)emitter.SetClip(interval,loop);
    }

    void LateUpdate(){
        if(gameObject.layer==31)return;
        if(!worldCamera)worldCamera=Camera.main;
        billboards.Face(worldCamera);
    }

    internal float Alignment(Camera camera)=>billboards.Alignment(camera);

    public static void FacePortrait(PcModel model,Camera camera){
        if(model&&IsWisp(model.raw)&&model.TryGetComponent<ClassicWispBody040>(out var wisp))wisp.billboards.Face(camera);
    }
}
}
