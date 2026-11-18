using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
// Native flag 8 rotates a bone's sprite plane towards the camera after animation.
public sealed class ProjectileBillboards {
    struct Binding {public Transform bone;public Vector3 normal;}
    static Dictionary<string,string[]> source;
    static readonly Dictionary<Mesh,Dictionary<int,Vector3>> normals=new Dictionary<Mesh,Dictionary<int,Vector3>>();
    readonly List<Binding> bindings=new List<Binding>();
    public int Count=>bindings.Count;
    public ProjectileBillboards(string key,GameObject geometry):this(geometry,Names(key)){}
    static string[] Names(string key){
        if(source==null)source=JObject.Parse(ProjectileBillboardData.Json).Properties().ToDictionary(p=>p.Name,p=>((JArray)p.Value).Select(v=>(string)v).ToArray());
        return source.TryGetValue(key,out var names)?names:null;
    }
    internal ProjectileBillboards(GameObject geometry,string[] names){
        if(!geometry||names==null)return;
        var wanted=new HashSet<string>(names);var seen=new HashSet<Transform>();
        foreach(var skin in geometry.GetComponentsInChildren<SkinnedMeshRenderer>()){
            var mesh=skin.sharedMesh;if(!mesh)continue;
            if(!normals.TryGetValue(mesh,out var lookup)){
                lookup=new Dictionary<int,Vector3>();normals[mesh]=lookup;
                var weights=mesh.boneWeights;var vectors=mesh.normals;var bind=mesh.bindposes;
                for(int i=0;i<weights.Length&&i<vectors.Length;i++){
                    var w=weights[i];int bone=w.boneIndex0;float best=w.weight0;
                    if(w.weight1>best){bone=w.boneIndex1;best=w.weight1;}if(w.weight2>best){bone=w.boneIndex2;best=w.weight2;}if(w.weight3>best){bone=w.boneIndex3;best=w.weight3;}
                    if(best<=0||bone>=bind.Length||lookup.ContainsKey(bone)||vectors[i].sqrMagnitude<.0001f)continue;
                    lookup[bone]=bind[bone].inverse.transpose.MultiplyVector(vectors[i]).normalized;
                }
            }
            for(int i=0;i<skin.bones.Length;i++)if(skin.bones[i]&&wanted.Contains(skin.bones[i].name)&&lookup.TryGetValue(i,out var normal)&&seen.Add(skin.bones[i]))bindings.Add(new Binding{bone=skin.bones[i],normal=normal});
        }
    }
    public void Face(Camera camera){
        if(!camera)return;var toward=-camera.transform.forward;
        foreach(var binding in bindings)if(binding.bone){var actual=binding.bone.TransformDirection(binding.normal);binding.bone.rotation=Quaternion.FromToRotation(actual,toward)*binding.bone.rotation;}
    }
    public float Alignment(Camera camera){float minimum=1;foreach(var binding in bindings)if(binding.bone)minimum=Mathf.Min(minimum,Mathf.Abs(Vector3.Dot(binding.bone.TransformDirection(binding.normal).normalized,-camera.transform.forward)));return minimum;}
}
}
