using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    bool combat030Gallery;
    void DrawCombat030Gallery(){
        Fill(new Rect(294,156,692,320),new Color(.02f,.025f,.025f,.98f));Border(new Rect(294,156,692,320),Color.gray);
        GUI.Label(new Rect(310,166,600,30),"Оригинальные типы атаки и защиты Warcraft III",hudTitle);
        for(int i=0;i<AttackTypes.Length;i++){float x=312+i*94;GUI.DrawTexture(new Rect(x+16,211,44,44),CombatIcon(false,AttackTypes[i]),ScaleMode.ScaleToFit);GUI.Label(new Rect(x,260,94,24),AttackLabels[i],new GUIStyle(hudSmall){alignment=TextAnchor.MiddleCenter});}
        for(int i=0;i<ArmorTypes.Length;i++){float x=302+i*84;GUI.DrawTexture(new Rect(x+20,321,44,44),CombatIcon(true,ArmorTypes[i]),ScaleMode.ScaleToFit);GUI.Label(new Rect(x,370,84,24),ArmorLabels[i],new GUIStyle(hudSmall){fontSize=11,alignment=TextAnchor.MiddleCenter});}
    }
    IEnumerator ExerciseCombat030(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"combat030"));Directory.CreateDirectory(folder);MainMenu();aspectsOn=false;raceChoice=11;botRaceChoice=0;startGold=100000;NewMatch();testHoldAuthority=true;options.fogOfWar=false;match.Wood[0]=100000;
        foreach(string type in AttackTypes)Check(CombatIcon(false,type)&&CombatIcon(false,type).width>=32,"Native attack infocard is packaged: "+type);
        foreach(string type in ArmorTypes)Check(CombatIcon(true,type)&&CombatIcon(true,type).width>=32,"Native armor infocard is packaged: "+type);
        Check(AttackTypes.Select(t=>CombatIcon(false,t)).Distinct().Count()==6&&ArmorTypes.Select(t=>CombatIcon(true,t)).Distinct().Count()==7,"Native type illustrations differ; only WC3 spell/magic and normal/light aliases share art");
        var cells=new JArray();
        foreach(bool mobile in new[]{false,true}){
            var rects=Enumerable.Range(0,12).Select(i=>CommandSlot(i%4,i/4,mobile)).ToArray();
            Check(rects.All(r=>r.x>=960&&r.xMax<=1280&&r.y>=480&&r.yMax<=720),"All twelve command cells fit the "+(mobile?"Android":"PC")+" panel");
            Check(rects.SelectMany((r,i)=>rects.Skip(i+1).Select(other=>!r.Overlaps(other))).All(v=>v),"All twelve "+(mobile?"Android":"PC")+" command cells are separate");
            foreach(var r in rects)cells.Add(new JObject{["mobile"]=mobile,["x"]=r.x,["y"]=r.y,["width"]=r.width,["height"]=r.height});
        }
        Check(Enumerable.Range(0,6).Select(InventorySlot).All(r=>r.x>=819&&r.xMax<960&&r.y>=580&&r.yMax<720),"Six inventory icons fit their native compartments");
        var catalogTypes=new JArray();for(int race=0;race<16;race++){
            var data=new HumanMatch(JObject.Parse(RaceCatalog(race)));bool valid=data.Lines.All(l=>l.weapons.Concat(l.buildingWeapons).All(w=>CombatIcon(false,w.attack??"normal"))&&CombatIcon(true,l.armorType)&&CombatIcon(true,(string)l.buildingStats["armor_type"]??"fortified"));
            Check(valid,"Unit and building combat types resolve native art for catalog "+race);catalogTypes.Add(new JObject{["race"]=race,["valid"]=valid});
        }
        FocusBuilder();buildMenu=false;yield return Shot(folder,"01-builder-aligned");yield return Action("builder.rescue");Check(targetedSpell=="rescue","Rescue accepts its command-cell click");targetedSpell=null;
        yield return Action("builder.build");Check(buildMenu,"Construction grid remains clickable");yield return Shot(folder,"02-twelve-construction-cells");yield return Action("builder.cancel");
        for(int i=0;i<6;i++){var id=match.ItemCatalog.Properties().Where(p=>(string)p.Value["mode"]!="purchase").Select(p=>p.Name).ElementAt(i);match.Inventories[0].slots.Add(new HumanMatch.ItemSlot{id=id});}
        yield return Shot(folder,"03-six-inventory-cells");SelectCastle(false);yield return Shot(folder,"04-shop-aligned");yield return Action("shop.back");
        var tower=ChaosHut(0,"h014",new Vector2(-145,26));builderSelected=false;selectedHut=tower.id;selectedUnit=-1;RenderMatch();controls.FocusWorld(World(tower.position),40);yield return Shot(folder,"05-building-types");
        testHudPointer=new Vector2(520,640);yield return Shot(folder,"06-building-attack-table");Check(hudTipTable&&hudTip==AttackTable(match.Lines[tower.kind].buildingWeapons),"Building attack table uses the tower's own weapons");
        testHudPointer=new Vector2(666,640);yield return Shot(folder,"07-building-armor-table");Check(hudTipTable&&hudTip==ArmorTable((string)match.Lines[tower.kind].buildingStats["armor_type"]??"fortified"),"Building defense table uses actual building armor");testHudPointer=null;
        var producer=QaBuild(match.BaseKinds.First(k=>match.Lines[k].Producer),17,14);selectedHut=producer.id;RenderMatch();Check(match.Lines[producer.kind].buildingWeapons.Length==0,"Nonattacking producer does not borrow its fighter's weapon");yield return Shot(folder,"08-producer-types");
        var unit=match.Spawn(0,Array.FindIndex(match.Lines,l=>l.Producer&&l.weapons.Length>0),new Vector2(-145,28));selectedHut=-1;selectedUnit=unit.id;RenderMatch();yield return Shot(folder,"09-unit-types");
        testHudPointer=new Vector2(520,640);yield return Shot(folder,"10-unit-attack-table");Check(hudTipTable&&hudTip==AttackTable(CardWeapons(unit)),"Unit attack hover retains the actual percentage table");
        testHudPointer=new Vector2(666,640);yield return Shot(folder,"11-unit-armor-table");Check(hudTipTable&&hudTip==ArmorTable(match.Lines[unit.kind]),"Unit defense hover retains the actual percentage table");testHudPointer=null;
        var changed=CardWeapons(unit)[0].Copy();changed.attack="chaos";unit.elementalWeapons=new[]{changed};Check(CardAttacks(CardWeapons(unit)).SequenceEqual(new[]{"chaos"})&&AttackTable(CardWeapons(unit)).Contains("Атака: Хаос"),"Transformed weapon changes both infocard type and multiplier table");unit.elementalWeapons=null;
        onlineMatch=true;networkUnitCards[unit.id]=new JObject{["armor"]=17,["weapon_damage"]=new JArray(123),["state"]=new JObject()};Check(NetworkCardStat(unit.id,"armor",0)==17&&NetworkCardStat(unit.id,"damage",0)==123,"Online cards keep authoritative numbers with the native type icons");onlineMatch=false;networkUnitCards.Clear();
        combat030Gallery=true;yield return Shot(folder,"12-native-type-gallery");combat030Gallery=false;
        yield return ExerciseCentaurCombat030(folder);
        MainMenu();var report=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["failures"]=testFailures,["checks"]=testChecks,["cells"]=cells,["catalogs"]=catalogTypes,["physicalAndroidTest"]=false,["scope"]="Changed HUD geometry, native combat type art, unit/building cards, tooltips and command dispatch. No broad combat rerun."};File.WriteAllText(Path.Combine(folder,"combat030-verification.json"),report.ToString());Debug.Log("CF_COMBAT030_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}
