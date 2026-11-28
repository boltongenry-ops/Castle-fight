using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseStructure031(){
        string folder=BattleLab.Arg("--pc-results");Directory.CreateDirectory(folder);
        Check(networkSessionToken=="launcher031-fixture"&&!File.Exists(BattleLab.Arg("--launcher-session")),"Launcher hands off its session once and removes the private file without connecting during QA");
        Check(Release031.Newer("0.32.0","0.31.0")&&!Release031.Newer("0.31.0","0.31.0")&&!Release031.Newer("bad","0.31.0"),"Update versions reject malformed values and prevent needless reinstall");
        Check(!Release031.ValidApk(new JObject{["url"]="http://example.invalid/app.apk",["size"]=1,["sha256"]=new string('a',64)}),"Android update metadata requires HTTPS and a checksum");
        var updateCatalog=JObject.Parse(File.ReadAllText(BattleLab.Arg("--pc-update-catalog")));Check(ContentCatalog031.Compatible(updateCatalog,"0.31.0"),"Current immutable content catalog is compatible");updateCatalog["minimumPlayerVersion"]="99.0.0";Check(!ContentCatalog031.Compatible(updateCatalog,"0.31.0"),"An older APK never loads content requiring newer code");
        MainMenu();checkingRelease=true;OpenSettings();yield return Action("settings.tab.3");yield return Action("settings.updates");checkingRelease=false;Check(showUpdatePanel,"Settings opens a distinct update panel");yield return Shot(folder,"updates-panel");yield return Action("updates.close");MainMenu();aspectsOn=false;startGold=100000;
        for(int race=0;race<16;race++)if(race!=12){
            raceChoice=botRaceChoice=race;NewMatch();testHoldAuthority=true;options.fogOfWar=false;match.Wood[0]=100000;FocusBuilder();buildMenu=true;
            var order=ConstructionKinds();Check(order.Select(k=>ConstructionRole(match.Lines[k])).SequenceEqual(order.Select(k=>ConstructionRole(match.Lines[k])).OrderBy(k=>k)),"Construction groups: producers, specials, tower, chest / "+race);
            if(order.Any(k=>match.Lines[k].building=="h008"))Check(match.Lines[order.Last()].building=="h008","Golden chest is last / "+race);
            Check(order.All(k=>MatchBuildingIcon(k)&&RaceModel(0,k,true)&&RaceModel(1,k,true)),"Both teams retain all building models and icons / "+race);
            if(order.Length>11){constructionPage=0;Check(ConstructionPageKinds().Length==10,"Nature page reserves navigation and cancel cells");yield return Action("builder.next-page");Check(constructionPage==1&&ConstructionPageKinds().Contains(order.Last()),"Nature second page exposes the formerly obscured chest");yield return Shot(folder,"nature-second-page");}
        }
        MainMenu();raceChoice=12;botRaceChoice=0;NewMatch();testHoldAuthority=true;
        var canonical=JObject.Parse(RaceCatalog(12));var draw031=new UltimateLoadout((JArray)canonical["ultimateSlots"],n=>n-1);var ids031=draw031.Draw();var playable031=draw031.Playable(canonical,ids031);var world031=new HumanMatch(playable031,100000);
        Check(ids031.Contains("pK01")&&!ids031.Contains("pL01")&&world031.Lines[world031.Kind("pH02")].upgrades.Contains("pL01")&&world031.Lines[world031.Kind("pL01")].cost==315,"Ultimate Panda draw uses legendary barrel and the adjusted complete brew upgrade chain");
        Check(world031.Lines[world031.Kind("pK01")].cost==550&&HumanMatch.F(world031.Abilities["APKg"],"power")==180,"Ultimate shares Panda prices and actual legendary spell data");
        foreach(int race in new[]{14,15}){
            MainMenu();raceChoice=botRaceChoice=race;NewMatch();testHoldAuthority=true;options.fogOfWar=false;match.Wood[0]=match.Wood[1]=100000;
            string root=race==14?"pH01":"c002",middle=race==14?"pH02":"c012",legend=race==14?"pL01":"c00A",special=race==14?"pK01":"c01B";
            Check(!match.BaseKinds.Contains(match.Kind(legend))&&match.Lines[match.Kind(middle)].upgrades.Contains(legend),"Legendary fighter only through the proper family / "+race);
            Check(!match.TryBuild(0,match.Kind(legend),17,14,out _),"Direct legendary construction rejected / "+race);
            Check(match.Lines.Where(l=>string.IsNullOrEmpty(l.unit)&&l.cost>0).All(l=>l.woodCost>0),"Every tower and special, including upgrades, requires wood / "+race);
            int tower=match.Kind(race==14?"pT01":"c008");match.Wood[0]=0;Check(!match.TryBuild(0,tower,17,14,out var noWood)&&noWood.Contains("дерева"),"Real authority rejects a tower without lumber / "+race);match.Wood[0]=100000;
            FocusBuilder();buildMenu=true;yield return Shot(folder,"race-"+race+"-ordered-buildings");
            var h=QaBuild(match.Kind(root),17,14);QaSelect(h);yield return Action("upgrade."+middle);yield return Action("upgrade."+legend);
            Check(match.Lines[h.kind].building==legend&&h.spent==(race==14?720:885)&&Mathf.Abs(match.Interval(h)-(race==14?51:59.25f))<.01f,"Cumulative legendary investment and its production interval / "+race);
            Check(match.LegendaryUsed(0)==1&&match.PurchaseError(0,match.Kind(special)).Contains("легендарного"),"Fighter and legendary special share the same limited slot / "+race);
            Check(match.Sell(0,h.id)&&match.LegendaryUsed(0)==0,"Sale releases legendary slot / "+race);
            var specialHut=QaBuild(match.Kind(race==14?"pK01":"c00B"),17,14);QaSelect(specialHut);if(race==15)yield return Action("upgrade.c01B");
            Check(match.LegendaryUsed(0)==1&&match.Lines[specialHut.kind].slots==1,"Legendary special reserves a real slot / "+race);
            string spell=race==14?"APKg":"CT15";specialHut.mana=0;var low=SpellState(spell,null,specialHut);Check(low.active&&low.manaWait>0&&low.regen>0,"Spell HUD shows real mana accumulation / "+race);RenderMatch();controls.FocusWorld(World(specialHut.position),40);yield return Shot(folder,"race-"+race+"-mana-accumulation");
            specialHut.mana=HumanMatch.F(match.Lines[specialHut.kind].buildingStats,"max_mana");specialHut.spellTimers[spell]=6;var cd=SpellState(spell,null,specialHut);Check(cd.left==6&&cd.Wait==6,"Spell HUD shows authoritative cooldown / "+race);yield return Shot(folder,"race-"+race+"-spell-cooldown");yield return Action("building.ability."+spell);Check(spellDetailsId==spell,"Spell details open by tapping the icon / "+race);yield return Shot(folder,"race-"+race+"-spell-details");yield return Action("spell.details.close");
            specialHut.spellTimers.Clear();specialHut.mana=100;var target=match.Spawn(1,match.Kind(race==14?"pB01":"c001"),specialHut.position+Vector2.right*4);target.hp=target.maxHp=10000;target.spellResist=0;target.armor=0;
            if(race==15)typeof(HumanMatch).GetField("centaurTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(match,0f);
            CorruptedCall(race==14?"TickFinalBuilding":"TickCentaur",race==14?(object)specialHut:(object).25f);
            if(race==14){Check(match.PandaKegs.Count==1&&specialHut.mana==60,"Legendary barrel releases a real keg and spends forty mana");CorruptedCall("TickFinal",.15f);Check(Mathf.Abs(10000-target.hp+match.PandaStagger(target)-180)<.01f,"Legendary keg really deals 180 magic damage");}
            else {Check(match.CentaurSands.Count==1&&specialHut.mana==55&&target.hp==9994,"Legendary sand deals real bounded damage and spends forty-five mana");var before=target.hp;match.CentaurSands.Add(new HumanMatch.CentaurSand{team=0,rank=2,point=target.position});CorruptedCall("TickCentaur",.25f);Check(Mathf.Abs(before-target.hp-6)<.001f,"Overlapping legendary fields do not multiply damage");}
            MainMenu();OpenGuide();GuideChooseRace(race);int k=Array.FindIndex(guideAuthority.Lines,l=>l.building==legend);GuideSelectEntry(k);Check(GuideInvestment(k)==(race==14?720:885),"Guide follows complete legendary upgrade investment / "+race);yield return Shot(folder,"race-"+race+"-legend-guide");
            int chest=Array.FindIndex(guideAuthority.Lines,l=>l.building=="h008");GuideSelectEntry(chest);Check(guideEntry==chest&&GuideIcon(chest),"Shared chest is illustrated in the reference book / "+race);
        }
        MainMenu();raceChoice=15;botRaceChoice=0;NewMatch();testHoldAuthority=true;var witch=match.Spawn(0,match.Kind("c005"),new Vector2(-145,24));witch.mana=5;QaSelect(ChaosHut(0,"c009",new Vector2(-155,24)));selectedHut=-1;selectedUnit=witch.id;builderSelected=false;RenderMatch();controls.FocusWorld(World(witch.position),30);
        Check(SpellState("CT23",witch).active&&SpellState("CT23",witch).manaWait>0,"Custom instant witch spell has a visible mana timer");yield return Shot(folder,"unit-blue-mana-bar");witch.spellTimers["CT23"]=7;Check(SpellState("CT23",witch).left==7,"Unit icon uses its own cooldown dictionary");yield return Shot(folder,"unit-spell-cooldown");
        witch.abilities.Add("CT01");Check(!SpellState("CT01",witch).active,"Passive Charge is never labelled as an autocast");
        onlineMatch=true;networkSpellStamp=Time.unscaledTime;networkUnitCards[witch.id]=new JObject{["spell_ui"]=new JObject{["regen"]=3.5,["cooldowns"]=new JObject{["CT23"]=4},["totals"]=new JObject{["CT23"]=8},["casting"]="",["blocked"]=""}};Check(Mathf.Abs(UnitManaRegen(witch)-3.5f)<.001f&&SpellState("CT23",witch).left>3.9f,"Network spell display uses authoritative server telemetry");onlineMatch=false;networkUnitCards.Clear();
        MainMenu();var report=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["failures"]=testFailures,["checks"]=testChecks,["physicalAndroidTest"]=false,["scope"]="Construction order, two corrected race families, legendary-slot and lumber authority, mana and cooldown presentation; no broad unrelated combat rerun."};File.WriteAllText(Path.Combine(folder,"structure031-verification.json"),report.ToString());Debug.Log("CF_STRUCTURE031_TARGETED_"+(testFailures==0?"PASS":"FAIL"));Application.Quit(testFailures==0?0:1);
    }
}
}
