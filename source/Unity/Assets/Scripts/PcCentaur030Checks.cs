using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseCentaurCombat030(string folder){
        var scenarios=Centaur030Scenarios.Run(JObject.Parse(RaceCatalog(15)),JObject.Parse(RaceCatalog(0)),Check);
        File.WriteAllText(Path.Combine(folder,"centaur030-authority.json"),scenarios.ToString());
        MainMenu();raceChoice=15;botRaceChoice=0;aspectsOn=false;startGold=100000;NewMatch();testHoldAuthority=true;options.fogOfWar=false;
        var raws=new[]{"cU01","cU02","cU03","cU04","cU05","cU06","cU07","cU0A"};int n=0;
        foreach(string raw in raws){var unit=match.Spawn(0,Array.FindIndex(match.Lines,l=>l.unit==raw),new Vector2(-145+n*4,24));unit.castPause=100000;n++;builderSelected=false;selectedHut=-1;selectedUnit=unit.id;RenderMatch();controls.FocusWorld(World(unit.position),34);
            var skills=UnitCardAbilities(unit).Where(VisibleUnitAbility).ToArray();Check(skills.All(id=>AbilityArt(id))&&skills.Any(id=>id!="CT01"),"Packaged fighter displays its individual skill icons: "+raw);yield return Shot(folder,"centaur-"+raw+"-skills");}
        var duels=new JArray();string baseline=BattleLab.Arg("--pc-centaur-baseline");
        if(File.Exists(baseline))foreach(string version in new[]{"before","after"})foreach(var pair in new[]{new[]{"cU01","hfoo"},new[]{"cU03","hrif"},new[]{"cU02","h03A"},new[]{"cU05","h03B"}}){
            var data=version=="before"?JObject.Parse(File.ReadAllText(baseline)):JObject.Parse(RaceCatalog(15));var world=new HumanMatch(data,100000,1,-1,-1,JObject.Parse(RaceCatalog(0)));typeof(HumanMatch).GetField("botAge",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(world,100000f);
            var first=world.Spawn(0,Array.FindIndex(world.Lines,l=>l.unit==pair[0]),new Vector2(-12,0));var second=world.Spawn(1,Array.FindIndex(world.Lines,l=>l.unit==pair[1]),new Vector2(12,0));first.onRoad=second.onRoad=true;float time=0;
            while(time<90&&first.hp>0&&second.hp>0){world.Step(.05f);time+=.05f;}
            duels.Add(new JObject{["version"]=version,["centaur"]=pair[0],["opponent"]=pair[1],["seconds"]=time,["centaurRemaining"]=Mathf.Max(0,first.hp),["enemyRemaining"]=Mathf.Max(0,second.hp),["thrusts"]=world.CentaurThrusts,["breaks"]=world.CentaurBreaks,["needles"]=world.CentaurNeedles});
        }
        Check(duels.Count==8,"Before/after comparison runs four focused centaur matchups without asserting universal wins");File.WriteAllText(Path.Combine(folder,"centaur030-duels.json"),duels.ToString());
    }
}
}
