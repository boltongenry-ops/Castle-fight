using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseUnits026(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"units026"));Directory.CreateDirectory(folder);
        MainMenu();aspectsOn=false;raceChoice=12;botRaceChoice=12;NewMatch();testHoldAuthority=true;options.fogOfWar=false;
        var rows=new JArray();var seen=new HashSet<string>();var races=new JArray();int cardCount=0;
        for(int race=0;race<15;race++){
            var authority=new HumanMatch(JObject.Parse(RaceCatalog(race)));int count=0;
            foreach(var line in authority.Lines.Where(l=>!string.IsNullOrEmpty(l.unit))){count++;if(!seen.Add(line.unit))continue;
                int kind=Array.FindIndex(match.Lines,l=>l.unit==line.unit);Check(kind>=0,"Catalog actor has an Ultimate mapping: "+line.unit);if(kind<0)continue;
                var unit=match.Spawn(0,kind,new Vector2(-150,20));bool valid=unit.maxHp>0&&!float.IsNaN(unit.maxHp);var cards=new JArray();
                foreach(string id in UnitCardAbilities(unit).Where(VisibleUnitAbility)){
                    var cfg=match.Abilities[id];string tip=UnitSkillTip(unit,id,cfg);bool readable=cfg!=null&&tip.Contains("\n")&&tip.Split('\n').Skip(1).Any(t=>!string.IsNullOrWhiteSpace(t))&&System.Text.RegularExpressions.Regex.IsMatch(tip,"[А-Яа-я]")&&!System.Text.RegularExpressions.Regex.IsMatch(tip,@"\d+[.,]\d{5,}")&&!tip.Contains("TRIGSTR_");
                    valid&=readable;cards.Add(new JObject{["id"]=id,["tip"]=tip,["readable"]=readable});cardCount++;
                }
                Check(valid,"Spawn and ability cards are defined/readable: "+line.unit);rows.Add(new JObject{["raw"]=line.unit,["cards"]=cards});match.Units.Remove(unit);
            }
            races.Add(new JObject{["index"]=race,["rows"]=count});
        }
        var wind=match.Spawn(0,Array.FindIndex(match.Lines,l=>l.unit=="o00E"),new Vector2(-150,25));
        Check(UnitCardAbilities(wind).Contains("A0C5")&&UnitSkillTip(wind,"A0C5",match.Abilities["A0C5"]).Contains("680"),"Wind charge remains visible with its actual owner-specific action");
        Check(!ContextAbilityVisible("h03P","A0C5",null)&&ContextAbilityVisible("n029","A0C5",null),"Lightning marker stays hidden and bear retains its genuine rage");
        Check(!ContextAbilityVisible("h06B","Arav",match.Abilities["Arav"])&&!ContextAbilityVisible("h06B","Awha",match.Abilities["Awha"]),"Nature spirit hides engine-only raven and harvest commands");
        string aura=ContextSkillTip("o00E","A0CR",match.Abilities["A0CR"]);Check(aura.Contains("Вражеские летуны")&&aura.Contains("35%")&&aura.Contains("700"),"Wind aura correctly describes enemy air targets, magnitude and range");
        string decay=ContextSkillTip("n02K","A0FR",match.Abilities["A0FR"]);Check(decay.Contains("25")&&!decay.Contains("10 "),"Twin decay uses configured twenty-five DPS instead of stale ten DPS text");
        Check(!ContextSkillTip("h099","A09Y",match.Abilities["A09Y"]).Contains("великан")&&ContextSkillTip("h099","A09Y",match.Abilities["A09Y"]).Contains("18"),"Tank armor has actor-neutral correct block values");
        Check(ContextSkillTip("n027","A0AH",match.Abilities["A0AH"]).Contains("70%")&&ContextSkillTip("h06B","A0BV",match.Abilities["A0BV"]).Contains("100%"),"Empty legendary and immunity cards show their actual percentages");
        // Authority checks for the two corrected elemental cards, without changing balance.
        wind.hp=wind.maxHp=10000;wind.armor=0;wind.spellResist=0;match.DamageUnit(wind,680,1,"chaos",true);Check(wind.elementalArmed&&!wind.abilities.Contains("A0C5")&&wind.abilities.Contains("A0EN"),"Actual wind mechanic changes charge into detonation at 680 damage");
        builderSelected=false;selectedUnit=wind.id;RenderMatch();controls.FocusWorld(World(wind.position),30);testHudPointer=Slot(0,0).center;yield return Shot(folder,"01-wind-card");testHudPointer=null;
        guideAuthority=new HumanMatch(JObject.Parse(RaceCatalog(3)));guideRace=3;guideEntry=Array.FindIndex(guideAuthority.Lines,l=>l.unit=="h06B");guideUnit=true;guideActorIndex=0;
        Check(guideEntry>=0&&!GuideEntryText().Contains("Добыча золота")&&!GuideEntryText().Contains("Приказ друиду"),"Guide filters the same engine-only nature spirit skills");guideAuthority=null;
        yield return ExerciseElvenTower026(folder);
        MainMenu();var report=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["failures"]=testFailures,["checks"]=testChecks,["races"]=races,["actors"]=rows,["uniqueActors"]=seen.Count,["cardCount"]=cardCount,["scope"]="Spawn/catalog/card audit for all catalog actors; targeted changed mechanics and Elven tower flight/impact, not full all-race combat or Android device QA"};File.WriteAllText(Path.Combine(folder,"units026-verification.json"),report.ToString());Debug.Log("CF_UNITS026_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" actors="+seen.Count+" cards="+cardCount);Application.Quit(testFailures==0?0:1);
    }
    IEnumerator ExerciseElvenTower026(string folder){
        MainMenu();raceChoice=11;botRaceChoice=0;aspectsOn=false;NewMatch();testHoldAuthority=true;options.fogOfWar=false;
        var at=new Vector2(-145,26);var tower=ChaosHut(0,"h014",at);foreach(var hut in match.Huts)if(hut!=tower)hut.paused=true;
        var targets=new List<HumanMatch.Unit>();
        foreach(var offset in new[]{new Vector2(8,0),new Vector2(10,5),new Vector2(12,-6),new Vector2(15,8),new Vector2(17,-7),new Vector2(19,0)})targets.Add(ElvenDummy(1,at+offset));
        targets[3].kind=Array.FindIndex(match.Lines,l=>l.unit=="h016"); // a real flying target among ground targets
        var beyondSecondary=ElvenDummy(1,at+Vector2.right*21);var beyondPrimary=ElvenDummy(1,at+Vector2.right*23);var ally=ElvenDummy(0,at+Vector2.up*8);
        match.TickBuilding(tower,0);match.TickBuilding(tower,1);var volley=match.Shots.Where(s=>s.sourceId==tower.id).ToArray();
        Check(volley.Length==5&&volley.Select(s=>s.target).Distinct().Count()==5,"Elven tower fires exactly five distinct missiles with more than five available victims");
        Check(volley.All(s=>s.target!=beyondSecondary.id&&s.target!=beyondPrimary.id&&s.target!=ally.id)&&volley.Any(s=>s.target==targets[3].id),"Barrage includes air, excludes allies and victims outside native secondary range");
        Check(match.Lines[tower.kind].buildingWeapons[0].range==22.5f&&HumanMatch.F(match.Abilities["A015"],"secondary_range")==20,"Native Elven primary range 675 and secondary range 600 are preserved");
        RenderMatch();var originalViews=volley.ToDictionary(s=>s.id,s=>projectiles[s.id].NativeObject);
        Check(originalViews.Values.All(v=>v&&v.GetComponent<ClassicProjectileModel>())&&originalViews.Values.Distinct().Count()==5,"All five flights have separate native rendered objects including billboard adapters");
        Check(volley.Count(s=>s.nightSecondary&&originalViews[s.id].GetComponent<ClassicProjectileModel>().key=="fx_8619182774d6")==4,"Each of four secondary flights uses the source SpiritOfVengeance missile");
        controls.FocusWorld(World(at+Vector2.right*9),43);for(int i=0;i<4;i++){CorruptedCall("TickShots",.05f);RenderMatch();yield return null;}
        Check(volley.All(s=>Vector3.Distance(projectiles[s.id].NativeObject.transform.position,World(at))>1),"Every rendered missile advances away from its tower origin");
        yield return Shot(folder,"02-elven-five-flights");match.Huts.Remove(tower);RenderMatch();
        Check(volley.All(s=>projectiles[s.id].NativeObject==originalViews[s.id]),"Selling the Elven tower retains all five original in-flight native objects");
        var victims=volley.Select(s=>match.Units.First(u=>u.id==s.target)).ToArray();var hp=victims.Select(u=>u.hp).ToArray();for(int i=0;i<25;i++){CorruptedCall("TickShots",.05f);RenderMatch();yield return null;}
        Check(victims.Select((u,i)=>u.hp<hp[i]).All(v=>v),"All five selected victims receive damage on missile arrival after source sale");
        Check(beyondSecondary.hp==beyondSecondary.maxHp&&beyondPrimary.hp==beyondPrimary.maxHp&&ally.hp==ally.maxHp,"Unselected and allied actors receive no barrage damage");
        match.Units.Clear();match.Shots.Clear();RenderMatch();tower=ChaosHut(0,"h014",at);var lone=ElvenDummy(1,at+Vector2.right*21);match.TickBuilding(tower,0);match.TickBuilding(tower,1);
        Check(match.Shots.Count==1&&match.Shots[0].target==lone.id,"Single primary target at 630 range is attacked without inventing extra flights");
        match.Shots.Clear();match.Units.Clear();tower.cooldown=0;tower.strikeLeft=-1;foreach(int i in Enumerable.Range(0,3))ElvenDummy(1,at+new Vector2(8+i*3,i*2));match.TickBuilding(tower,0);match.TickBuilding(tower,1);
        Check(match.Shots.Count==3&&match.Shots.Select(s=>s.target).Distinct().Count()==3,"Three available victims receive exactly three independent projectiles");
    }
}
}
