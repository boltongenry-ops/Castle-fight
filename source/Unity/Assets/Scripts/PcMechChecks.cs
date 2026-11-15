using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    void MechClear() {
        NorthClear();match.MechJobs.Clear();match.HellCurses.Clear();match.MechRepairGold[0]=match.MechRepairGold[1]=match.MechRepairWood[0]=match.MechRepairWood[1]=0;
        foreach(var line in match.Lines)line.regen=HumanMatch.F(line.stats,"hp_regen");match.Gold[0]=100000;match.Wood[0]=100000;match.Wood[1]=0;
    }
    HumanMatch.Unit MechUnit(int team,string raw,Vector2 point) {var u=NatureUnit(team,raw,point);u.castPause=10000;return u;}
    HumanMatch.Unit MechDummy(int team,Vector2 point,string raw="h000") {var u=MechUnit(team,raw,point);u.hp=u.maxHp=10000;u.spellResist=0;match.Lines[u.kind].regen=0;return u;}
    IEnumerator ExerciseMechOnly() {
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"mech-validation"));Directory.CreateDirectory(folder);
        aspectsOn=false;MainMenu();botRaceChoice=0;yield return Action("main.play");yield return Action("setup.mech");
        Check(raceChoice==6&&mechPack&&AllRaceIds[RaceOfUnity(6)]=="mechanical","Mechanical race occupies its own source menu card");yield return Shot(folder,"01-mech-selection");startGold=1000;yield return Action("setup.start");testHoldAuthority=true;
        Check(match.TeamMech[0]&&!match.TeamMech[1]&&!match.TeamHuman[0]&&match.TeamHuman[1]&&builders[0].raw=="h06P","Mech worker, Human opponent and aspect identities remain independent");
        Check(match.BaseKinds.Length==11&&mechPack.buildingIcons.Length==22&&AspectCount(6)==2,"Mech has eleven base commands, ten upgrades, a hidden mine row and two aspects");
        Check(Mathf.Abs(builders[0].animatedRoot.transform.localScale.x-.7692308f)<.0001f,"Mech builder uses the native 0.5 authored-scale correction instead of oversized custom GLB dimensions");
        Check(CommandIcon(0)==mechPack.builderIcon&&CommandIcon(6)==mechPack.buildIcon,"Mech construction and builder source icons resolve");
        Check(new[]{"A095","A099","A09B","A09D","A09E","A09F","A09K","A09N","A09O","A09Q","A09R","A09S","A09T","A10C"}.All(id=>AbilityArt(id)),"Mech mechanics and passive icons resolve for either side");
        match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;match.LegendaryLimit[0]=12;
        for(int i=0;i<match.BaseKinds.Length;i++) {
            int kind=match.BaseKinds[i];var line=match.Lines[kind];float gold=match.Gold[0];int wood=match.Wood[0];var h=QaBuild(kind,3+i%6*4,15+i/6*4);h.paused=true;RenderMatch();
            Check(h.paid&&h.spent==line.cost&&Mathf.Abs(h.income-line.cost*line.incomeRate)<.001f,"Mech base construction investment "+line.building);
            Check(match.Wood[0]==wood-line.woodCost+line.woodReward&&match.Gold[0]<=gold-line.cost+match.Income(0)+1,"Mech base construction source payment "+line.building);
            Check(huts[h.id].raw==line.building&&!huts[h.id].reforged,"Mech classic building renders "+line.building);
            if(line.Producer){h.paused=false;h.spawnLeft=.01f;Tick(.05f);h.paused=true;Check(match.Units.Any(u=>u.kind==kind),"Mech base production releases "+line.unit);}
        }
        controls.FocusWorld(World(new Vector2(-141,32)),58);yield return Shot(folder,"02-mech-buildings");
        string[] from={"h069","h06J","h05T","h05U","h05J","h05V","h05X","h05X","h05X","h09B"},to={"h100","h05R","h09P","h06Z","h09L","h097","h09J","h09I","h09B","h09H"};
        for(int i=0;i<from.Length;i++) {
            var h=match.Huts.FirstOrDefault(h=>match.Lines[h.kind].building==from[i]);if(h==null)h=QaBuild(match.Kind(from[i]),3+(i-6)*4,7);int kind=match.Kind(to[i]),spent=h.spent,wood=match.Wood[0];float gold=match.Gold[0];var line=match.Lines[kind];QaSelect(h);yield return Action("upgrade."+to[i]);h.paused=true;RenderMatch();
            Check(h.kind==kind&&h.spent==spent+line.cost,"Mech native upgrade branch "+to[i]);Check(match.Wood[0]==wood-line.woodCost+line.woodReward&&match.Gold[0]<gold-line.cost+match.Income(0)+1,"Mech upgrade charges source gold and wood "+to[i]);
            Check(huts[h.id].raw==to[i]&&Mathf.Abs(match.Interval(h)-(15+h.spent/20f))<.001f,"Mech saved upgrade model and cumulative production interval "+to[i]);
            if(line.Producer){h.paused=false;h.spawnLeft=.01f;Tick(.05f);h.paused=true;Check(match.Units.Any(u=>u.kind==kind),"Mech upgraded production releases "+line.unit);}
        }
        Check(BuildingCardAbilities(match.Huts.First(h=>match.Lines[h.kind].building=="h09T")).Contains("A09S"),"Power station card displays its native scripted aura");
        Check(BuildingCardAbilities(match.Huts.First(h=>match.Lines[h.kind].building=="h05L")).Contains("A09U"),"Energy tower card displays its native chain-lightning chance");
        Check(!BuildingCardAbilities(match.Huts.First(h=>match.Lines[h.kind].building=="h100")).Contains("A10D"),"Maintenance card hides the duplicate native helper");
        var support=match.Huts.First(h=>match.Lines[h.kind].building=="h100");int count=match.Spawned;support.paused=false;support.spawnLeft=.01f;Tick(.05f);support.paused=true;
        Check(!match.Lines[support.kind].Producer&&match.Spawned==count,"Maintenance upgrade becomes a support building without producing the old mechanic");
        Check(match.Lines[match.Kind("h05R")].cost==150&&match.Lines[match.Kind("h09P")].cost==150,"Missing native upgrade gold uses Godot choose_upgrade's 150 fallback");
        RenderMatch();QaSelect(match.Huts.First(h=>match.Lines[h.kind].building=="h09H"));controls.FocusWorld(World(new Vector2(-141,32)),59);yield return Shot(folder,"03-mech-upgrades");
        Check(mechAttachments.Keys.Count(k=>k.StartsWith("decor-"))>=20,"Native mechanical decorations coexist on upgraded and base buildings");
        MechClear();string[] roster={"h069","h05T","h09P","h05U","h06Z","h05M","h05J","h09L","h05V","h097","h05X","h09J","h09I","h09B","h09H"};
        for(int i=0;i<roster.Length;i++)MechUnit(0,roster[i],new Vector2(-165+i%5*8,23+i/5*9));RenderMatch();
        foreach(string raw in roster){var unit=match.Units.Single(u=>u.kind==match.Kind(raw));var view=units[unit.id];Check(!view.reforged&&view.godotHeight==0&&view.clips.All(c=>c),"Mech real classic model and saved clips "+match.Lines[unit.kind].unit);}
        var sapper=match.Units.Single(u=>match.Lines[u.kind].unit=="n01V");Check(!units[sapper.id].clips.Any(c=>c.name.StartsWith("Attack"))&&units[sapper.id].clips.Any(c=>c.name.StartsWith("Death")),"Classic sapper preserves its native Death art without inventing Attack clips");
        selectedUnit=match.Units.Single(u=>match.Lines[u.kind].unit=="n024").id;builderSelected=false;controls.FocusWorld(World(new Vector2(-148,31)),60);yield return Shot(folder,"04-mech-full-roster");
        yield return MechPowerChecks(folder);yield return MechPayloadChecks(folder);yield return MechShieldChecks(folder);yield return MechAspectsChecks(folder);
        MainMenu();raceChoice=0;botRaceChoice=6;aspectsOn=false;yield return Action("main.play");yield return Action("setup.start");testHoldAuthority=true;NorthTime(8);RenderMatch();
        Check(match.TeamMech[1]&&builders[1].raw=="h06P"&&match.Huts.Any(h=>h.team==1&&h.paid&&match.Lines[h.kind].building=="h05X"),"Mech bot constructs its own native tank branch with its own worker");
        var enemy=MechUnit(1,"h09H",new Vector2(-148,32));RenderMatch();selectedUnit=enemy.id;builderSelected=false;controls.FocusWorld(World(enemy.position),30);Check(AbilityArt("A09F")&&AbilityArt("A09Z"),"Enemy Mech unit card resolves its original weapon and armor passives");yield return Shot(folder,"12-mech-enemy-card");
        var sizes=new JArray(mechPack.units.Concat(mechPack.builders).Where(v=>v).Select(v=>new JObject { ["raw"]=v.GetComponent<PcModel>().raw,["scale"]=v.GetComponent<PcModel>().animatedRoot.transform.localScale.x,["clips"]=new JArray(v.GetComponent<PcModel>().clips.Select(c=>c.name)) }));
        var result=new JObject {["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["build"]=Version,["catalog"]=JObject.Parse(mechPack.catalog.text),["models"]=sizes};File.WriteAllText(Path.Combine(folder,"mech-verification.json"),result.ToString());
        Debug.Log("CF_MECH_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}
