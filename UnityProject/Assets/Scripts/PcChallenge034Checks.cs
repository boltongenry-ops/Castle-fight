using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseChallenge034(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"challenge034-validation"));Directory.CreateDirectory(folder);
        Challenge034Scenarios.Run((JObject)progression.Catalog.DeepClone(),JObject.Parse(catalog.text),Path.Combine(folder,"isolated-rules-"+Guid.NewGuid().ToString("N")+".json"),Check);
        MainMenu();OpenProgression(false);yield return Shot(folder,"01-thirty-challenges");
        progressionScroll=new Vector2(0,29*154);yield return Shot(folder,"02-last-challenges");
        foreach(JObject row in (JArray)progression.Catalog["challenges"]){
            BeginChallenge(row);testHoldAuthority=true;
            Check(match!=null&&activeChallenge!=null&&matchRace==Array.IndexOf(ProgressRaces,(string)row["race"])&&matchBotRace==Array.IndexOf(ProgressRaces,(string)row["opponent"]),"Actual round opens: "+row["id"]);
            if(matchRace==12){RenderMatch();Check(ultimateRoundLoadouts[0].Length==11&&ConstructionKinds().Length==11,"Ultimate keeps eleven native drawn lines");
                match.Gold[0]=100000;match.Wood[0]=100000;foreach(int k in ConstructionKinds().Take(3)){var l=match.Lines[k];if(l.slots>0)continue;Check(RaceModel(0,k,true),"Drawn Ultimate building prefab: "+l.building);if(l.Producer)Check(RaceModel(0,k,false),"Drawn Ultimate fighter prefab: "+l.unit);}
                builderSelected=true;buildMenu=true;yield return Shot(folder,"03-ultimate-challenge");}
            MainMenu();yield return null;
        }
        int tokens=progression.Coins;progression.Complete("five_pillars",100);progression.Complete("centaur_open_steppe",110);
        OpenProgression(false);progressionScroll=Vector2.zero;yield return Shot(folder,"04-mastery-profile");
        Check(progression.Experience>0&&progression.MasteryLevel>=2,"Mastery shown after completing distinct challenges");
        var report=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["build"]=Version,["checks"]=testChecks,["failures"]=testFailures};File.WriteAllText(Path.Combine(folder,"challenge034-verification.json"),report.ToString());
        Debug.Log("CF_CHALLENGE034_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}
