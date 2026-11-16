using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseNerubian033(){
        string folder=BattleLab.Arg("--pc-results");Directory.CreateDirectory(folder);yield return null;MainMenu();aspectsOn=false;raceChoice=13;botRaceChoice=0;startGold=100000;NewMatch();testHoldAuthority=true;options.fogOfWar=false;match.Wood[0]=match.Wood[1]=100000;
        Exception failure=null;try{Nerubian033Scenarios.Run(JObject.Parse(RaceCatalog(13)),JObject.Parse(RaceCatalog(0)),Check);Infernal033Scenarios.Run(JObject.Parse(RaceCatalog(4)),JObject.Parse(RaceCatalog(0)),Check);}catch(Exception e){failure=e;Check(false,e.ToString());}
        if(failure==null){
            Check(ConstructionKinds().Last()==match.Kind("h008"),"Chest remains last in construction");
            var tower=QaBuild(match.Kind("hNT1"),17,14);Check(match.Upgrade(0,tower.id,match.Kind("hNT2"),out _),"Tower upgrades through ordinary purchase rules");QaSelect(tower);Check(BuildingCardAbilities(tower).Contains("ANDB"),"Two-target passive has a real icon and card description");yield return Shot(folder,"two-target-tower-card");
            match.Units.Clear();match.Shots.Clear();tower.cooldown=0;tower.strikeLeft=-1;var a=match.Spawn(1,match.Kind("h000"),tower.position+Vector2.right*8);var b=match.Spawn(1,match.Kind("h000"),tower.position+Vector2.right*10);a.hp=a.maxHp=b.hp=b.maxHp=10000;
            match.TickBuilding(tower,.05f);match.TickBuilding(tower,1f);Check(match.Shots.Count==2&&match.Shots.Select(s=>s.target).Distinct().Count()==2,"Packaged player creates two distinct native tower shots");
            RenderMatch();controls.FocusWorld(World(tower.position+Vector2.right*5),36);yield return new WaitForSeconds(.15f);yield return Shot(folder,"two-native-tower-projectiles");
            var unit=match.Spawn(0,match.Kind("hNS2"),tower.position+Vector2.down*4);selectedUnit=unit.id;selectedHut=-1;builderSelected=false;RenderMatch();yield return Shot(folder,"priest-mana-and-reworked-skills");
            Check(unit.maxMana>0&&UnitManaRegen(unit)>0,"Portrait mana and regen remain available");
        }
        MainMenu();var report=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["physicalAndroidTest"]=false};File.WriteAllText(Path.Combine(folder,"nerubian033-verification.json"),report.ToString());Debug.Log("CF_NERUBIAN033_TARGETED_"+(testFailures==0?"PASS":"FAIL"));Application.Quit(testFailures==0?0:1);
    }
}
}
