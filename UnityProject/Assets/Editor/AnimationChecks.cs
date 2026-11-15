using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

public static class AnimationChecks {
    public static void Check(GameObject prefab,AnimationClip[] clips) {
        var model=UnityEngine.Object.Instantiate(prefab);
        try {
            var renderer=model.GetComponentInChildren<SkinnedMeshRenderer>();
            if(!renderer || renderer.bones.Length==0)throw new Exception("No skin: "+prefab.name);
            var records=new JArray();
            foreach(string name in new[]{"Walk","Attack","Death"}) {
                var clip=clips.First(c=>c.name.Equals(name,StringComparison.OrdinalIgnoreCase));
                var a=new Mesh();var b=new Mesh();
                clip.SampleAnimation(model,clip.length*.05f);renderer.BakeMesh(a);
                clip.SampleAnimation(model,clip.length*.55f);renderer.BakeMesh(b);
                var av=a.vertices;var bv=b.vertices;float movement=0;
                for(int k=0;k<av.Length;k++) {
                    if(float.IsNaN(bv[k].x)||float.IsInfinity(bv[k].y)||bv[k].magnitude>1000)throw new Exception("Invalid skin pose");
                    movement=Mathf.Max(movement,(av[k]-bv[k]).magnitude);
                }
                if(movement<.0001f)throw new Exception("Static animation "+prefab.name+"/"+name);
                records.Add(new JObject { ["clip"]=name,["vertices"]=av.Length,["maxVertexDelta"]=movement,["boundsHeight"]=b.bounds.size.y });
                UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);
            }
            Debug.Log("CF_SKIN_PASS "+prefab.name+" "+records.ToString(Newtonsoft.Json.Formatting.None));
        }finally{UnityEngine.Object.DestroyImmediate(model);}
    }
}
