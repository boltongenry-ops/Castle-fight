using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseCentaur028(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"centaur028"));Directory.CreateDirectory(folder);MainMenu();
        var scenarios=Centaur028Scenarios.Run(JObject.Parse(RaceCatalog(15)),JObject.Parse(RaceCatalog(0)),Check);
        Check(centaurPack&&AllRaceNames.Length==16&&RaceStaged(15)&&networkRaceNames.Length==18,"Sixteenth race is available offline and listed in the network picker");
        raceChoice=15;botRaceChoice=0;aspectsOn=false;page=Page.Setup;yield return Shot(folder,"01-centaur-setup");
        startGold=100000;NewMatch();testHoldAuthority=true;options.fogOfWar=false;match.Wood[0]=100000;
        foreach(int kind in match.BaseKinds){int i=Array.IndexOf(match.BaseKinds,kind);var h=QaBuild(kind,17+i%4*4,12+i/4*3);h.paused=true;RenderMatch();Check(huts[h.id].raw==match.Lines[kind].building,"Packaged classic building "+match.Lines[kind].building);}
        var actorKinds=Enumerable.Range(0,match.Lines.Length).Where(k=>(match.Lines[k].unit??"").StartsWith("cU")).ToArray();
        for(int i=0;i<actorKinds.Length;i++){var u=match.Spawn(0,actorKinds[i],new Vector2(-130+i%5*6,5+i/5*6));u.castPause=100000;RenderMatch();var view=units[u.id];Check(view.raw==match.Lines[u.kind].unit&&view.ClassicHeight>2&&view.ClassicHeight<8&&view.clips.Any(c=>c.name.StartsWith("Walk")),"Packaged actor size and walking animation "+view.raw);Check(view.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(s=>s.sharedMaterials).All(m=>m&&m.shader.isSupported&&m.mainTexture),"Packaged actor materials "+view.raw);}
        builderSelected=false;selectedUnit=match.Units.First(u=>match.Lines[u.kind].unit=="cU05").id;selectedHut=-1;controls.FocusWorld(World(new Vector2(-122,20)),52);yield return Shot(folder,"02-centaur-army");
        selectedUnit=-1;builderSelected=true;buildMenu=false;yield return Shot(folder,"03a-centaur-builder");yield return Action("builder.build");Check(buildMenu,"Builder construction button accepts a real UI click");yield return Shot(folder,"03-centaur-build-menu");Check(!match.Abilities.ContainsKey("CT08")&&match.BaseKinds.Length==11,"Builder has eleven structure icons and no removed gathering command");yield return Action("builder.cancel");Check(!buildMenu,"Construction menu closes through its ordinary cancel button");
        var towerKind=match.Kind("c00B");var totemHut=match.Huts.First(h=>h.kind==towerKind);Check(huts[totemHut.id].raw=="c00B","Special totem renders as its own building model");
        MainMenu();OpenGuide();GuideChooseRace(15);GuideSelectEntry(4,true);yield return new WaitForSecondsRealtime(.2f);yield return Shot(folder,"04-centaur-guide");Check(guideModel&&guideModel.raw=="cU03"&&guideDescription.Contains("Натиск"),"Guide shows the imported actor and its actual skill description");
        MainMenu();raceChoice=15;botRaceChoice=0;NewMatch();testHoldAuthority=true;options.fogOfWar=false;var axe=match.Spawn(0,match.Kind("c003"),new Vector2(-147,24));var target=match.Spawn(1,Array.FindIndex(match.Lines,l=>l.unit=="hfoo"),axe.position+Vector2.right*12);axe.onRoad=target.onRoad=true;target.castPause=100000;for(int i=0;i<12;i++)Tick(.05f);RenderMatch();var shot=match.Shots.FirstOrDefault(s=>s.weapon?.missile=="centaur:axe");Check(shot!=null&&NativeMissilePrefab(shot)&&NativeMissilePrefab(shot).GetComponent<PcModel>().raw=="cAxe","Ranged axe uses the author's own imported projectile");controls.FocusWorld(World(axe.position+Vector2.right*6),30);yield return Shot(folder,"05-centaur-axe-flight");
        MainMenu();var report=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["failures"]=testFailures,["checks"]=testChecks,["scenarios"]=scenarios,["physicalAndroidTest"]=false,["fullBalanceCertification"]=false};File.WriteAllText(Path.Combine(folder,"centaur028-verification.json"),report.ToString());Debug.Log("CF_CENTAUR028_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}
