using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
namespace CastleFight {
// Only independent numeric range tests run on workers. Eligibility, stacking,
// healing and damage stay serial and in the original source/ability/unit order.
public sealed class AuraBatch035:IDisposable {
    public struct Query {public HumanMatch.Unit source;public string id;public JToken profile;}
    [BurstCompile(FloatMode=FloatMode.Strict,FloatPrecision=FloatPrecision.Standard)]
    public struct RangeJob:IJobParallelFor {
        [ReadOnly] public NativeArray<Vector2> points,origins;
        [ReadOnly] public NativeArray<float> radii;
        [WriteOnly] public NativeArray<byte> inside;
        public int width;
        public void Execute(int i){int row=i/width;var d=points[i%width]-origins[row];inside[i]=(byte)(Mathf.Sqrt(d.x*d.x+d.y*d.y)<=radii[row]?1:0);}
    }
    public readonly List<Query> queries;
    NativeArray<byte> results;
    readonly int width;
    public bool parallel;
    public AuraBatch035(List<Query> rows,List<HumanMatch.Unit> units,bool reference){
        queries=rows;width=units.Count;
        // Small batches avoid worker scheduling/allocation overhead entirely.
        if(reference||rows.Count==0||(long)rows.Count*width<65536)return;
        var points=new NativeArray<Vector2>(width,Allocator.TempJob);var origins=new NativeArray<Vector2>(rows.Count,Allocator.TempJob);var radii=new NativeArray<float>(rows.Count,Allocator.TempJob);
        try{
            for(int i=0;i<width;i++)points[i]=units[i].position;
            for(int i=0;i<rows.Count;i++){origins[i]=rows[i].source.position;radii[i]=HumanMatch.F(rows[i].profile,"radius");}
            results=new NativeArray<byte>(checked(rows.Count*width),Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
            try{new RangeJob{points=points,origins=origins,radii=radii,inside=results,width=width}.Schedule(results.Length,128).Complete();parallel=true;}
            catch{results.Dispose();throw;}
        }finally{points.Dispose();origins.Dispose();radii.Dispose();}
    }
    public bool Contains(int row,int column,HumanMatch.Unit unit){if(parallel)return results[row*width+column]!=0;return Vector2.Distance(queries[row].source.position,unit.position)<=HumanMatch.F(queries[row].profile,"radius");}
    public void Dispose(){if(results.IsCreated)results.Dispose();}
}
}
