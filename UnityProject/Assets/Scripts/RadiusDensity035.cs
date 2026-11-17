using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
namespace CastleFight {
public static class RadiusDensity035 {
    [BurstCompile(FloatMode=FloatMode.Strict,FloatPrecision=FloatPrecision.Standard)]
    public struct DensityJob:IJobParallelFor {
        [ReadOnly] public NativeArray<Vector2> points;
        [WriteOnly] public NativeArray<int> counts;
        public float radius;
        public void Execute(int index){int count=0;var center=points[index];for(int i=0;i<points.Length;i++){var delta=points[i]-center;if(Mathf.Sqrt(delta.x*delta.x+delta.y*delta.y)<=radius)count++;}counts[index]=count;}
    }
    public static int Densest(IList<HumanMatch.Unit> units,float radius,bool scalar=false){
        int best=0,bestCount=-1;
        if(scalar||units.Count<256){
            for(int i=0;i<units.Count;i++){int count=0;for(int j=0;j<units.Count;j++)if(Vector2.Distance(units[i].position,units[j].position)<=radius)count++;if(count>bestCount){best=i;bestCount=count;}}return best;
        }
        var points=new NativeArray<Vector2>(units.Count,Allocator.TempJob);var counts=new NativeArray<int>(units.Count,Allocator.TempJob);
        try{for(int i=0;i<units.Count;i++)points[i]=units[i].position;new DensityJob{points=points,counts=counts,radius=radius}.Schedule(units.Count,16).Complete();for(int i=0;i<units.Count;i++)if(counts[i]>bestCount){best=i;bestCount=counts[i];}return best;}
        finally{points.Dispose();counts.Dispose();}
    }
}
public sealed partial class HumanMatch {
    // Target selection must consume this sequence before applying effects. All
    // uses materialize/sort or choose a target before any damage callback.
    System.Collections.Generic.IEnumerable<Unit> RadiusUnits035(Vector2 center,float radius){
        if(!UnitIndexReady033()||SimulationState035.Get(this).reference)return Units;
        return RadiusEntries035(SimulationState035.Get(this).targets.WithinOrdered(center,Mathf.Max(0,radius)+.00001f));
    }
    static System.Collections.Generic.IEnumerable<Unit> RadiusEntries035(List<UnitIndex033.Entry> entries){for(int i=0;i<entries.Count;i++)yield return entries[i].unit;}
}
}
