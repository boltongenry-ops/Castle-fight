using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseBot027(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"bot027"));Directory.CreateDirectory(folder);MainMenu();
        var names=new[]{"human","orcs","naga","nature","chaos","northern","mech","undead","corrupted","elemental","night","elven","ultimate","nerubian","pandaren"};
        var watch=System.Diagnostics.Stopwatch.StartNew();var scenarios=Bot027Scenarios.Run(name=>JObject.Parse(RaceCatalog(Array.IndexOf(names,name))),Check);watch.Stop();double simulationMs=watch.Elapsed.TotalMilliseconds;
        // Native runtime view after ordinary bot construction/upgrade orders.
        raceChoice=0;botRaceChoice=7;aspectsOn=false;startGold=250;NewMatch();testHoldAuthority=true;options.fogOfWar=false;
        typeof(HumanMatch).GetField("botRandom",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(match,123456u);
        for(int i=0;i<1800;i++){match.Winner=-2;match.CastleHp[0]=match.CastleHp[1]=HumanMatch.CastleMax;match.Step(.5f);if(i%200==0)yield return null;}
        RenderMatch();builderSelected=false;selectedHut=match.Huts.First(h=>h.team==1).id;selectedUnit=-1;controls.FocusWorld(World(new Vector2(142,0)),58);yield return Shot(folder,"01-developed-undead-bot");
        Check(match.Huts.Where(h=>h.team==1).Select(h=>match.Lines[h.kind].building).Distinct().Count()>=3&&match.Upgrades>0,"Actual player renderer receives diversified upgraded undead bot buildings");
        // Measure repeated hard-bot decisions against a full visible enemy army.
        var perf=new HumanMatch(JObject.Parse(RaceCatalog(0)),10000,2);int enemy=Array.FindIndex(perf.Lines,l=>l.unit=="hfoo");for(int i=0;i<160;i++){var u=perf.Spawn(0,enemy,new Vector2(100+i%8,i/8));u.cooldown=u.castPause=10000;}
        var think=typeof(HumanMatch).GetMethod("Bot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);var times=new double[12];
        for(int i=0;i<times.Length;i++){perf.Huts.Clear();perf.Builders[1].jobs.Clear();watch.Restart();think.Invoke(perf,null);watch.Stop();times[i]=watch.Elapsed.TotalMilliseconds;}
        Check(times.Skip(1).Average()<50,"Bot purchasing decisions remain bounded with 160 observed enemies");
        MainMenu();var result=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["failures"]=testFailures,["checks"]=testChecks,["scenarios"]=scenarios,["decisionMilliseconds"]=new JArray(times),["simulationMilliseconds"]=simulationMs,["androidDeviceTest"]=false};File.WriteAllText(Path.Combine(folder,"bot027-verification.json"),result.ToString());Debug.Log("CF_BOT027_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}
