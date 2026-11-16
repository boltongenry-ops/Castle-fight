using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEngine;
public static class SimulationScaleAudit {
    static double[] Measure(Action action,int count){var samples=new double[count];for(int i=0;i<count;i++){var sw=Stopwatch.StartNew();action();samples[i]=sw.Elapsed.TotalMilliseconds;}return samples;}
    static JObject Row(string name,double[] samples,long allocated){var ordered=samples.OrderBy(v=>v).ToArray();return new JObject{["section"]=name,["samples"]=samples.Length,["medianMs"]=ordered[ordered.Length/2],["maxMs"]=ordered.Last(),["allocatedBytesPerSample"]=allocated>0?(JToken)(allocated/(double)samples.Length):JValue.CreateNull()};}
    public static void Run(){
        var own=JObject.Parse(File.ReadAllText("Assets/Nerubian032/pc-nerubian.json"));var enemy=JObject.Parse(File.ReadAllText("Assets/Resources/pc-human.json"));var rows=new JArray();
        foreach(int count in new[]{200,500,1000,2000})foreach(bool contact in new[]{false,true}){
            var m=new HumanMatch((JObject)own.DeepClone(),0,1,-1,-1,(JObject)enemy.DeepClone());
            typeof(HumanMatch).GetField("botAge",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(m,10000f);
            string[] ours={"hNV1","hNP2","hNS2","hNA2"},theirs={"h000","h003"};
            for(int i=0;i<count;i++){int side=i%2;float dir=side==0?-1:1;float x=dir*(contact?.5f+(i/2%16)*.5f:60+(i/2%50));var u=m.Spawn(side,m.Kind(side==0?ours[(i/2)%ours.Length]:theirs[(i/2)%theirs.Length]),new Vector2(x,((i/2)%3-1)*1.5f));u.onRoad=true;u.hp=u.maxHp=100000;}
            m.Step(.05f);m.Step(.05f);
            var sample=new JObject{["units"]=count,["layout"]=contact?"engaged_dense":"march_separated",["sections"]=new JArray()};var sections=(JArray)sample["sections"];
            long start=GC.GetAllocatedBytesForCurrentThread();var times=Measure(()=>m.Step(.05f),8);sections.Add(Row("full_simulation_step_50ms",times,GC.GetAllocatedBytesForCurrentThread()-start));
            var aura=typeof(HumanMatch).GetMethod("UpdateAuras",BindingFlags.NonPublic|BindingFlags.Instance);start=GC.GetAllocatedBytesForCurrentThread();times=Measure(()=>aura.Invoke(m,null),3);sections.Add(Row("aura_refresh",times,GC.GetAllocatedBytesForCurrentThread()-start));
            start=GC.GetAllocatedBytesForCurrentThread();times=Measure(()=>{foreach(var u in m.Units.ToArray())if(u.hp>0)m.TickUnit(u,.05f);},3);sections.Add(Row("all_unit_decisions_and_movement",times,GC.GetAllocatedBytesForCurrentThread()-start));
            rows.Add(sample);UnityEngine.Debug.Log("CF_SIMULATION_SCALE_SAMPLE units="+count+" contact="+contact+" stepMs="+sections[0]["medianMs"]);
        }
        var buildings=new JArray();
        foreach(int count in new[]{100,500,1000}){
            var m=new HumanMatch((JObject)own.DeepClone(),0,1,-1,-1,(JObject)enemy.DeepClone());typeof(HumanMatch).GetField("botAge",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(m,10000f);
            for(int i=0;i<count;i++){int side=i%2,j=i/2,k=m.Kind(side==0?"hNV1":"h000");m.Huts.Add(new HumanMatch.Hut{id=100000+i,team=side,kind=k,position=HumanMatch.Plot(side,j%32,j/32),paid=true,started=true,hp=1000,maxHp=1000,spent=m.Lines[k].cost,spawnLeft=10000});}
            m.Step(.05f);var times=Measure(()=>m.Step(.05f),8);buildings.Add(new JObject{["buildings"]=count,["fixture"]="producing buildings, no units, production not due; excludes purchase/placement cost",["step"]=Row("full_simulation_step_50ms",times,0)});
        }
        Directory.CreateDirectory("../Validation/SimulationScale033");File.WriteAllText("../Validation/SimulationScale033/editor-benchmark.json",new JObject{["runtime"]="Unity Editor Mono batchmode; simulation only, no graphics; not Android or player FPS",["allocationCounterNote"]="Zero from Mono allocation counter is treated as unavailable, not zero allocations",["stepSeconds"]=.05,["rows"]=rows,["buildingRows"]=buildings}.ToString());UnityEngine.Debug.Log("CF_SIMULATION_SCALE_AUDIT_PASS");
    }
    public static void AuditAndBuildAndroid(){Run();BuildAndroidRelease.Build();}
}
