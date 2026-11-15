using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseNerubian032(){
        string folder=BattleLab.Arg("--pc-results");Directory.CreateDirectory(folder);yield return null;
        MainMenu();aspectsOn=false;startGold=100000;raceChoice=botRaceChoice=13;NewMatch();testHoldAuthority=true;options.fogOfWar=false;match.Wood[0]=match.Wood[1]=100000;
        var before=JObject.Parse(File.ReadAllText(BattleLab.Arg("--pc-nerub-before")));var current=JObject.Parse(RaceCatalog(13));
        Check(JToken.DeepEquals(before["abilities"],current["abilities"]),"All reworked Nerubian abilities unchanged");
        foreach(var row in before["lines"]){var revised=current["lines"].First(l=>(string)l["building"]==(string)row["building"]);Check(JToken.DeepEquals(row["stats"],revised["stats"]),"Fighter stats and weapons preserved / "+row["building"]);}
        string[] expected={"hNV1","hNW1","hNP1","hNS1","hNA1","hNB1","hNC1","hNL1","hNT1","h008"};
        Check(ConstructionKinds().Select(k=>match.Lines[k].building).SequenceEqual(expected),"Exact menu order: five producers, three specials, tower and final chest");
        Check(ConstructionKinds().Select(k=>match.Lines[k].hotkey).Distinct().Count()==10,"Every build hotkey unique");
        foreach(int k in ConstructionKinds()){
            var line=match.Lines[k];Check(MatchBuildingIcon(k)&&RaceModel(0,k,true)&&RaceModel(1,k,true),"Both teams building art/model / "+line.building);
            if(line.building!="h008")Check(MatchBuildingIcon(k).name=="Building-"+line.building,"Construction uses actual building thumbnail / "+line.building);
        }
        FocusBuilder();buildMenu=true;yield return Shot(folder,"nerubian-ordered-build-menu");
        Check(!match.TryBuild(0,match.Kind("hNO1"),17,14,out _),"Direct Matriarch construction rejected");
        var hut=QaBuild(match.Kind("hNS1"),17,14);QaSelect(hut);yield return Action("upgrade.hNS2");
        Check(hut.kind==match.Kind("hNS2"),"Cocoon Shrine upgrades to Temple of Depths by actual UI");
        match.Wood[0]=0;Check(!match.Upgrade(0,hut.id,match.Kind("hNO1"),out _),"Matriarch still requires lumber");match.Wood[0]=100000;
        yield return Action("upgrade.hNO1");
        Check(hut.kind==match.Kind("hNO1")&&hut.spent==520&&Mathf.Abs(match.Interval(hut)-41)<.01f,"Matriarch UI upgrade preserves 520 total gold and 41-second production");
        Check(match.LegendaryUsed(0)==1&&match.PurchaseError(0,match.Kind("hNL1")).Length>0,"Matriarch and legendary tomb share legendary limit");
        Check(match.Lines[hut.kind].cost==150&&match.Lines[hut.kind].woodCost==300,"Final upgrade is 150 gold and original 300 wood");
        yield return Shot(folder,"matriarch-upgraded-building");
        int n=match.Units.Count;hut.spawnLeft=0;match.Step(.05f);Check(match.Units.Skip(n).Any(u=>match.Lines[u.kind].unit=="nN0D"),"Upgraded building actually produces the Matriarch");
        Check(match.Sell(0,hut.id)&&match.LegendaryUsed(0)==0,"Selling upgraded Matriarch frees legendary limit");
        MainMenu();OpenGuide();GuideChooseRace(13);int legend=Array.FindIndex(guideAuthority.Lines,l=>l.building=="hNO1");GuideSelectEntry(legend);
        Check(GuideInvestment(legend)==520&&GuideIcon(legend).name=="Building-hNO1","Guide preserves cumulative cost and building icon");yield return Shot(folder,"nerubian-matriarch-guide");
        MainMenu();raceChoice=12;botRaceChoice=0;NewMatch();testHoldAuthority=true;var ultimate=JObject.Parse(RaceCatalog(12));
        Check(!ultimate["ultimateSlots"].SelectMany(s=>(JArray)s["groups"]).Any(g=>((JArray)g["buildings"]).Any(b=>(string)b=="hNO1")),"Ultimate cannot draw Matriarch directly");
        Check(ultimate["lines"].First(l=>(string)l["building"]=="hNS2")["upgrades"].Any(v=>(string)v=="hNO1"),"Ultimate retains Matriarch dependency chain");
        var draw=new UltimateLoadout((JArray)ultimate["ultimateSlots"],count=>0);var chosen=draw.Draw();Check(draw.Valid(new JArray(chosen)),"Ultimate has no empty groups after removing direct Matriarch draw");
        MainMenu();raceChoice=13;botRaceChoice=0;NewMatch();testHoldAuthority=true;options.fogOfWar=false;
        var caster=match.Spawn(0,match.Kind("hNS1"),new Vector2(-145,24));caster.mana=55;selectedUnit=caster.id;selectedHut=-1;builderSelected=false;
        RenderMatch();controls.FocusWorld(World(caster.position),30);yield return new WaitForSeconds(.3f);
        Check(GroupIcon(caster).name=="Unit-nN09","Group selection retains fighter portrait separate from building icon");
        Check(caster.maxMana>0&&caster.mana==55&&UnitManaRegen(caster)>0,"Mana and real regeneration remain in the selected unit card");yield return Shot(folder,"unit-hp-only-and-card-mana");
        var view=units[caster.id];var screen=Camera.main.WorldToScreenPoint(view.transform.position+Vector3.up*FinalMarkerHeight(view.raw));
        var worldHealth=new JObject{["x"]=screen.x,["y"]=Screen.height-screen.y,["width"]=Screen.width,["height"]=Screen.height,["guiScale"]=guiScale};
        MainMenu();var report=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["failures"]=testFailures,["checks"]=testChecks,["unitHealthScreen"]=worldHealth,["scope"]="Nerubian construction/art/upgrade chain, preserved fighter mechanics and unit world HP-only presentation; no broad combat rerun",["physicalAndroidTest"]=false};
        File.WriteAllText(Path.Combine(folder,"nerubian032-verification.json"),report.ToString());Debug.Log("CF_NERUBIAN032_TARGETED_"+(testFailures==0?"PASS":"FAIL"));Application.Quit(testFailures==0?0:1);
    }
}
}

