using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Unity.Profiling;
namespace CastleFight {
public static class Optimization035Checks {
    static object Field(object value,string key)=>value.GetType().GetField(key,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);
    static void Set(object value,string key,object item)=>value.GetType().GetField(key,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(value,item);
    static object Call(object value,string key,params object[] args)=>value.GetType().GetMethod(key,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(value,args);
    static JObject ReadCatalog(string assets,string name){string p=name=="nerubian"?"Nerubian032/pc-nerubian.json":name=="chaos"?"Nerubian033/pc-chaos.json":name=="pandaren"?"Structure031/pc-pandaren.json":"Resources/pc-"+name+".json";return JObject.Parse(File.ReadAllText(Path.Combine(assets,p)));}
    public static void Run(string assets,string baselinePath,string folder,Action<bool,string> check,bool benchmark=true){
        Directory.CreateDirectory(folder);
        var controls=new List<Rect>();for(int i=0;i<10;i++)controls.Add(SettingsLayout035.Control(i));controls.AddRange(new[]{SettingsLayout035.Downloads,SettingsLayout035.Updates,SettingsLayout035.Cancel,SettingsLayout035.Apply});
        for(int i=0;i<controls.Count;i++)for(int j=i+1;j<controls.Count;j++)check(!controls[i].Overlaps(controls[j]),"Settings controls separated "+i+"/"+j);
        check(SettingsLayout035.Downloads.yMax+18<=SettingsLayout035.Cancel.yMin&&SettingsLayout035.Apply.yMax<=680,"Resource buttons have an independent footer and panel margin");
        var source=new List<int>{1,2,3};using(var outer=SimulationBuffers035.Take(source)){source.Clear();source.Add(9);using(var inner=SimulationBuffers035.Take(source)){int sum=0;foreach(int v in inner)sum+=v;check(sum==9,"Nested snapshot sees its own source");}int oldSum=0;foreach(int v in outer)oldSum+=v;check(oldSum==6,"Nested callback cannot overwrite outer snapshot");}
        var config=new JObject{["flying"]=false,["mana"]=1,["unit_types"]="Organic"};var typed=SimulationConfig035.Get(config);config["flying"]=true;config["mana"]=7;config["unit_types"]="Mechanical";check(typed.flying&&typed.mana==7&&!typed.organic,"Typed configuration invalidated by actual JObject edits");
        var actor035=new HumanMatch.Unit();actor035.abilities.Add("one");var catalog035=new JObject{["one"]=new JObject{["kind"]="wind_walk"},["two"]=new JObject{["kind"]="heal"}};var abilityView=AbilityView035.Get(actor035,catalog035);check(abilityView.wind!=null&&abilityView.active.Count==0,"Passive wind profile excluded from autocast work");actor035.abilities.Clear();actor035.abilities.Add("two");abilityView=AbilityView035.Get(actor035,catalog035);check(abilityView.wind==null&&abilityView.active.Single()=="two","Same-count ability replacement invalidates active view");catalog035["two"]["kind"]="wind_walk";abilityView=AbilityView035.Get(actor035,catalog035);check(abilityView.wind!=null&&abilityView.active.Count==0,"Ability kind mutation refreshes active view");
        Scheduler(check);
        var baseline=Assembly.Load(File.ReadAllBytes(Path.GetFullPath(baselinePath)));var oldType=baseline.GetType("CastleFight.HumanMatch",true);
        string[] names={"human","orcs","naga","nature","chaos","northern","mech","undead","corrupted","elemental","night","elven","ultimate","nerubian","pandaren","centaur"};
        var parity=new JArray();
        for(int race=0;race<names.Length;race++){
            var own=ReadCatalog(assets,names[race]);var enemy=ReadCatalog(assets,names[(race+1)%names.Length]);
            var current=new HumanMatch((JObject)own.DeepClone(),100000,1,-1,-1,(JObject)enemy.DeepClone());
            var old=Activator.CreateInstance(oldType,new object[]{own.DeepClone(),100000,1,-1,-1,enemy.DeepClone()});
            SimulationState035.Get(current).auditGround=true;Seed(current);Seed(old);Populate(current,32,true);Populate(old,32,true);
            bool equal=true;string difference="";int at=0;
            for(int tick=0;tick<240;tick++){current.Step(.05f);Call(old,"Step",.05f);if(tick%10==0||tick==239){var a=Snapshot(current);var b=Snapshot(old);if(!JToken.DeepEquals(a,b)){equal=false;at=tick;difference=FirstDifference(a,b);File.WriteAllText(Path.Combine(folder,"difference-"+names[race]+".json"),new JObject{["current"]=a,["released034"]=b}.ToString());break;}}}
            parity.Add(new JObject{["race"]=names[race],["equal"]=equal,["tick"]=at,["difference"]=difference});UnityEngine.Debug.Log("CF_OPT035_PARITY "+names[race]+" equal="+equal);check(equal,"Released 0.34 authority parity "+names[race]+(equal?"":" tick="+at+" "+difference));
        }
        File.WriteAllText(Path.Combine(folder,"released034-parity.json"),parity.ToString());
        BurstParity(check);
        var performance=benchmark?Benchmark(assets,oldType):new JObject{["note"]="Editor validation only; benchmark runs in actual player"};File.WriteAllText(Path.Combine(folder,"simulation-benchmark.json"),performance.ToString());
    }
    static void Seed(object m){Set(m,"botAge",100000f);Set(m,"botRandom",(uint)35034);}
    static void Populate(object m,int total,bool dense){
        var lines=(Array)Field(m,"Lines");var eligible=new List<int>();for(int k=0;k<lines.Length;k++){var l=lines.GetValue(k);if(!string.IsNullOrEmpty((string)Field(l,"unit"))&&(string)Field(l,"category")!="visual"&&!HumanMatch.RemovedElementalCompanion((string)Field(l,"unit")))eligible.Add(k);}
        for(int i=0;i<total;i++){int side=i%2;int k=eligible[(i/2)%eligible.Count];float x=dense?(side==0?-1:1)*(.5f+(i/2%12)*1.2f):(side==0?-1:1)*(55+i/2%100);var u=Call(m,"Spawn",side,k,new Vector2(x,(i/24%3-1)*1.5f),false,null);Set(u,"onRoad",true);Set(u,"hp",100000f);Set(u,"maxHp",100000f);}
        if(!dense)return;
        var huts=(IList)Field(m,"Huts");var hutType=m.GetType().GetNestedType("Hut");int id=100000;
        for(int side=0;side<2;side++){int added=0;for(int k=0;k<lines.Length&&added<5;k++){var l=lines.GetValue(k);if(!string.IsNullOrEmpty((string)Field(l,"unit")))continue;var h=Activator.CreateInstance(hutType);Set(h,"id",id++);Set(h,"team",side);Set(h,"kind",k);Set(h,"paid",true);Set(h,"started",true);Set(h,"hp",100000f);Set(h,"maxHp",100000f);Set(h,"position",new Vector2(side==0?-16:16,added*4-8));Set(h,"mana",1000f);huts.Add(h);added++;}}
    }
    static readonly string[] snapshotFields={"Units","Huts","Gold","Wood","Shots","Corpses","Winner","Kills","Spawned","SpellsCast","Pulses","CastleHp","randomState"};
    sealed class VectorConverter:JsonConverter {
        public override bool CanConvert(Type type)=>type==typeof(Vector2)||type==typeof(Color);
        public override void WriteJson(JsonWriter writer,object value,JsonSerializer serializer){writer.WriteStartObject();if(value is Vector2 v){writer.WritePropertyName("x");writer.WriteValue(v.x);writer.WritePropertyName("y");writer.WriteValue(v.y);}else{var c=(Color)value;writer.WritePropertyName("r");writer.WriteValue(c.r);writer.WritePropertyName("g");writer.WriteValue(c.g);writer.WritePropertyName("b");writer.WriteValue(c.b);writer.WritePropertyName("a");writer.WriteValue(c.a);}writer.WriteEndObject();}
        public override object ReadJson(JsonReader reader,Type type,object existing,JsonSerializer serializer)=>throw new NotSupportedException();
    }
    static JObject Snapshot(object m){var serializer=new JsonSerializer{ReferenceLoopHandling=Newtonsoft.Json.ReferenceLoopHandling.Ignore};serializer.Converters.Add(new VectorConverter());var result=new JObject();foreach(string key in snapshotFields)result[key]=JToken.FromObject(Field(m,key),serializer);return result;}
    static string FirstDifference(JToken a,JToken b){if(a is JObject obj&&b is JObject other){foreach(var p in obj.Properties())if(!JToken.DeepEquals(p.Value,other[p.Name]))return p.Name+"."+FirstDifference(p.Value,other[p.Name]);}if(a is JArray array&&b is JArray arrayB){if(array.Count!=arrayB.Count)return "Count "+array.Count+" / "+arrayB.Count;for(int i=0;i<array.Count;i++)if(!JToken.DeepEquals(array[i],arrayB[i]))return "["+i+"]."+FirstDifference(array[i],arrayB[i]);}return a+" / "+b;}
    sealed class Event {public int id;public float left;}
    static void Scheduler(Action<bool,string> check){
        var pending=new List<Event>();var queue=new DeadlineQueue035<Event>((e,l)=>e.left=l,.00001f);var random=new System.Random(35);int id=0;bool equal=true;
        for(int tick=0;tick<1000;tick++){
            if(tick%3==0){float left=(float)random.NextDouble()*3;var e=new Event{id=id++,left=left};pending.Add(e);queue.Add(new Event{id=e.id,left=left},left);}
            float dt=new[]{.016f,.05f,.1f,.2f}[tick/73%4];var expected=new List<int>();foreach(var e in pending){e.left-=dt;if(e.left<=.00001f)expected.Add(e.id);}pending.RemoveAll(e=>e.left<=.00001f);
            var actual=queue.Advance(dt);if(!expected.SequenceEqual(actual.Select(e=>e.id)))equal=false;
        }
        check(equal,"Deadline queue matches float countdown and creation order over 1000 variable-dt steps");
        var zero=new DeadlineQueue035<Event>((e,l)=>e.left=l,.00001f);zero.Add(new Event{id=1},0);var ready=zero.Advance(.05f);check(ready.Count==1,"Zero-delay work runs at next queue phase");zero.Add(new Event{id=2},0);check(zero.Advance(.05f).Single().id==2,"Callback-created zero delay does not run in the same phase");
    }
    static void BurstParity(Action<bool,string> check){
        var densityUnits=new List<HumanMatch.Unit>();for(int i=0;i<400;i++)densityUnits.Add(new HumanMatch.Unit{position=new Vector2(i%30-15,i/30-7)});foreach(float radius in new[]{0f,1f,6.7f,100f})check(RadiusDensity035.Densest(densityUnits,radius)==RadiusDensity035.Densest(densityUnits,radius,true),"Burst/scalar density center and stable ties radius="+radius);
        var units=new List<HumanMatch.Unit>();for(int i=0;i<513;i++)units.Add(new HumanMatch.Unit{position=new Vector2(i%32-16,i/32-8)});
        var queries=new List<AuraBatch035.Query>();for(int i=0;i<130;i++)queries.Add(new AuraBatch035.Query{source=units[i],profile=new JObject{["radius"]=i%19}});
        bool equal=true;using(var a=new AuraBatch035(queries,units,false))using(var b=new AuraBatch035(queries,units,true)){check(a.parallel,"Large aura range batch executes IJobParallelFor");for(int r=0;r<queries.Count;r++)for(int c=0;c<units.Count;c++)if(a.Contains(r,c,units[c])!=b.Contains(r,c,units[c]))equal=false;}check(equal,"66690 Burst/scalar aura range results match exactly, including boundaries");
    }
    static JObject Benchmark(string assets,Type oldType){
        var rows=new JArray();var own=ReadCatalog(assets,"nerubian");var enemy=ReadCatalog(assets,"human");
        foreach(int total in (BattleLab.Arg("--pc-bench-size")=="320"?new[]{320}:new[]{320,1000,2000}))foreach(bool dense in new[]{false,true}){
            var next=new HumanMatch((JObject)own.DeepClone(),0,1,-1,-1,(JObject)enemy.DeepClone());var old=Activator.CreateInstance(oldType,new object[]{own.DeepClone(),0,1,-1,-1,enemy.DeepClone()});Seed(next);Seed(old);Populate(next,total,dense);Populate(old,total,dense);
            var stepOld=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),old,oldType.GetMethod("Step"));Action<float> stepNew=next.Step;
            for(int i=0;i<4;i++){stepOld(.05f);stepNew(.05f);}var before=new List<double>();var after=new List<double>();
            for(int i=0;i<24;i++){var sw=Stopwatch.StartNew();if(i%2==0){stepOld(.05f);before.Add(sw.Elapsed.TotalMilliseconds);sw.Restart();stepNew(.05f);after.Add(sw.Elapsed.TotalMilliseconds);}else{stepNew(.05f);after.Add(sw.Elapsed.TotalMilliseconds);sw.Restart();stepOld(.05f);before.Add(sw.Elapsed.TotalMilliseconds);}}
            stepOld(.05f);SimulationPhase035.Enabled=true;stepNew(.05f);var phases=SimulationPhase035.Result();SimulationPhase035.Enabled=false;
            rows.Add(new JObject{["units"]=total,["layout"]=dense?"dense_contact":"separated_march",["released034"]=Percentiles(before),["candidate035"]=Percentiles(after),["candidatePhaseMs"]=phases,["releasedAllocation"]=Allocation(()=>stepOld(.05f)),["candidateAllocation"]=Allocation(()=>stepNew(.05f))});UnityEngine.Debug.Log("CF_OPT035_BENCH units="+total+" dense="+dense+" before="+Percentiles(before)+" after="+Percentiles(after));
        }
        var producerRows=new JArray();
        foreach(int producers in new[]{100,500,1000})foreach(bool blocked in new[]{false,true}){
            var next=new HumanMatch((JObject)own.DeepClone(),0,1,-1,-1,(JObject)enemy.DeepClone());var old=Activator.CreateInstance(oldType,new object[]{own.DeepClone(),0,1,-1,-1,enemy.DeepClone()});Seed(next);Seed(old);if(blocked){Populate(next,320,false);Populate(old,320,false);}
            AddProducers(next,producers,blocked);AddProducers(old,producers,blocked);var stepOld=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),old,oldType.GetMethod("Step"));Action<float> stepNew=next.Step;stepOld(.05f);stepNew(.05f);var before=new List<double>();var after=new List<double>();
            for(int i=0;i<12;i++){var sw=Stopwatch.StartNew();stepOld(.05f);before.Add(sw.Elapsed.TotalMilliseconds);sw.Restart();stepNew(.05f);after.Add(sw.Elapsed.TotalMilliseconds);}
            producerRows.Add(new JObject{["producers"]=producers,["fullArmy"]=blocked,["released034"]=Percentiles(before),["candidate035"]=Percentiles(after)});
        }
        return new JObject{["runtime"]=Application.isEditor?"Unity Editor; not phone FPS":"Actual Windows Mono player; simulation only, not phone FPS",["pairedAlternatingSamples"]=24,["rows"]=rows,["producerRows"]=producerRows};
    }
    static JObject Allocation(Action action){
        long before=GC.GetAllocatedBytesForCurrentThread();
        var recorder=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC.Alloc",262144,ProfilerRecorderOptions.CollectOnlyOnCurrentThread|ProfilerRecorderOptions.StartImmediately);
        action();recorder.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;var result=new JObject{["managedThreadBytes"]=bytes>0?(JToken)bytes:JValue.CreateNull(),["profilerValid"]=recorder.Valid};
        if(recorder.Valid){var samples=new List<ProfilerRecorderSample>();recorder.CopyTo(samples);long total=0;foreach(var sample in samples)total+=sample.Value;result["profilerRecordedBytes"]=total>0?(JToken)total:JValue.CreateNull();result["sampleCount"]=samples.Count;result["capacityReached"]=recorder.Count==recorder.Capacity;}recorder.Dispose();return result;
    }
    static void AddProducers(object m,int count,bool due){var lines=(Array)Field(m,"Lines");int k=0;for(int i=0;i<lines.Length;i++)if(!string.IsNullOrEmpty((string)Field(lines.GetValue(i),"unit"))&&((string[])Field(lines.GetValue(i),"abilities")).Length==0){k=i;break;}var huts=(IList)Field(m,"Huts");var type=m.GetType().GetNestedType("Hut");for(int i=0;i<count;i++){var h=Activator.CreateInstance(type);Set(h,"id",1000000+i);Set(h,"team",i%2);Set(h,"kind",k);Set(h,"paid",true);Set(h,"started",true);Set(h,"hp",1000f);Set(h,"maxHp",1000f);Set(h,"spawnLeft",due?0f:10000f);Set(h,"spent",100);Set(h,"position",new Vector2((i%2==0?-1:1)*(100+i%32),i/32));huts.Add(h);}}
    static JObject Percentiles(List<double> values){values.Sort();double P(double p)=>values[Math.Min(values.Count-1,(int)Math.Ceiling(p*values.Count)-1)];return new JObject{["p50Ms"]=P(.5),["p95Ms"]=P(.95),["p99Ms"]=P(.99)};}
}
public sealed partial class PcGame {
    IEnumerator ExerciseOptimization035(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"optimization035"));Directory.CreateDirectory(folder);
        Optimization035Checks.Run(BattleLab.Arg("--pc-assets"),BattleLab.Arg("--pc-reference"),folder,Check);
        SettingsLayout035.PreviewMobile=true;MainMenu();OpenSettings();yield return Shot(folder,"settings035");
        Check(SettingsLayout035.Downloads.yMax<SettingsLayout035.Apply.yMin,"Actual settings use separated resource and action rows");yield return Action("settings.softfog");yield return Action("settings.cancel");Check(page==Page.Main,"Settings cancellation remains usable");
        File.WriteAllText(Path.Combine(folder,"optimization035-verification.json"),new JObject{["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["version"]=Version}.ToString());UnityEngine.Debug.Log("CF_OPT035_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}
