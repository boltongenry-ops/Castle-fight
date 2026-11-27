using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseModels039(){
        string folder=BattleLab.Arg("--pc-results");Directory.CreateDirectory(folder);
        string relay=BattleLab.Arg("--pc-reforged-fixture");
        Check(!HdModels.Preferred&&!HdModels.Active&&!HdModels.Installed,"Old hdTextures setting does not opt in or load Reforged");
        Check(!MenuBuilder(0).GetComponent<PcModel>().reforged,"Default race preview uses classic model");
        yield return Action("main.settings");yield return Action("settings.tab.3");yield return Action("settings.models");
        yield return HdModels.Transfer(true,relay);
        Check(!HdModels.Busy&&!HdModels.Installed,"Download refused before selecting Reforged");
        yield return Action("models.reforged");
        yield return HdModels.Transfer(false,relay);
        Check(!HdModels.Installed&&!HdModels.Active,"Selecting Reforged does not download or activate it");
        yield return Shot(folder,"01-model-settings");
        yield return Action("models.back");yield return Action("settings.apply");
        Check(HdModels.Preferred&&!HdModels.Active,"Applying the option without a package stays classic");
        yield return Action("main.settings");yield return Action("settings.tab.3");yield return Action("settings.models");
        yield return HdModels.Transfer(true,relay);
        Check(HdModels.Installed&&HdModels.Active&&HdModels.Bytes==HdModels.Total,"Explicit download verifies and activates the model package");

        var archive=JObject.Parse(File.ReadAllText(BattleLab.Arg("--pc-model-index")));
        var ids=((JArray)archive["models"]).Values<string>().ToArray();
        int valid=ids.Sum(raw=>Enumerable.Range(0,2).Count(team=>HdModels.Contains(raw,team)));
        Check(ids.Length>0&&valid==ids.Length*2,"The bundle exposes all catalogued models for both teams");
        var portraitIds=((JArray)archive["portraits"]).Values<string>().ToArray();
        Check(portraitIds.Length>0&&portraitIds.All(raw=>HdActorIcon(raw)),"Every portrait listed by the optional package can be opened");
        string[] sample={"h00C","h019","h00E","h01A","hcas","h000","n00S","hrif"};
        foreach(string raw in sample)for(int team=0;team<2;team++){
            var prefab=HdModels.Model(raw,team);var model=prefab?prefab.GetComponent<PcModel>():null;
            Check(model&&model.raw==raw&&model.reforged&&model.godotHeight>0&&model.clips.Any()&&prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .All(skin=>skin.sharedMaterials.All(material=>material&&material.shader.isSupported&&material.mainTexture)),"Animated HD prefab materials / "+raw+" / "+team);
        }
        var classicIds=((JArray)archive["classicActors"]).Values<string>().ToHashSet();
        Check(Enumerable.Range(0,MobileContent.RaceResources.Length).All(race=>{
            var classic=ClassicMenuBuilder(race).GetComponent<PcModel>();
            return MenuBuilder(race).GetComponent<PcModel>().reforged!=classicIds.Contains(classic.raw);
        }),"Every race builder follows the published HD and classic roster");
        Check(classicIds.Count==12&&classicIds.All(raw=>!HdModels.Contains(raw,0)&&!HdActorIcon(raw)),
            "Only the approved author models remain in classic");
        yield return Action("models.back");yield return Action("settings.apply");
        Check((string)JObject.Parse(File.ReadAllText(settingsPath))["modelStyle"]=="reforged","Preference is saved separately from old HD effects");
        MainMenu();raceChoice=0;botRaceChoice=0;matchMode=0;NewMatch();testHoldAuthority=true;
        match.Gold[0]=1000;match.Gold[1]=0;
        int kind=match.Kind("h000");
        var hut=QaBuild(kind,7,15);var unit=match.Spawn(0,kind,match.Builders[0].position+Vector2.right*4);
        RenderMatch();
        Check(builders.All(p=>p&&p.reforged)&&castleViews.All(p=>p&&p.reforged)&&huts[hut.id].reforged&&units[unit.id].reforged,
            "Live actors, buildings and castles use Reforged on the normal render path");
        Check(CommandIcon(0)==HdActorIcon(BuilderRaw)&&GroupIcon(unit)==HdActorIcon(match.Lines[unit.kind].unit),
            "Builder shortcut and group selection show the corresponding HD portraits");
        options.fogOfWar=false;FocusBuilder();controls.FocusWorld(World(unit.position),39);
        yield return Shot(folder,"02-reforged-battle");

        page=Page.Pause;yield return Action("pause.settings");yield return Action("settings.tab.3");yield return Action("settings.models");
        yield return Action("models.classic");yield return Action("models.back");yield return Action("settings.apply");
        RenderMatch();
        Check(!HdModels.Active&&HdModels.Installed&&!huts[hut.id].reforged&&!units[unit.id].reforged&&castleObjects[0].activeSelf,
            "Classic mode restores the same live match and keeps the downloaded file");
        page=Page.Match;yield return Shot(folder,"03-classic-battle");
        HdModels.Select(true);yield return HdModels.OpenCached();
        Check(HdModels.Active,"An installed package mounts from disk without a network transfer");
        HdModels.Select(false);

        var rules=new HumanMatch(JObject.Parse(catalog.text),1000,0);
        int basic=rules.Kind("h000");
        Check(rules.Income(0)==5&&rules.IncomeIn==10&&rules.CastleHp.All(hp=>hp==5000),"Common income, interval, and standard castle HP");
        Check(rules.TryBuild(0,basic,7,15,out _)&&rules.ReservedGold(0)==rules.Lines[basic].cost,
            "Queued construction reserves its real full price");
        var constructed=rules.Huts.Single();
        Check(rules.Sell(0,constructed.id)&&rules.ReservedGold(0)==0&&rules.Gold[0]==1000,"Canceling unpaid construction releases the full reserve");
        constructed.spent=520;
        Check(rules.Interval(constructed)==41&&Mathf.Abs(HumanMatch.ArmorFactor(5)-1/1.3f)<.0001f,
            "Upgraded production uses total investment and the armor formula uses the Warcraft coefficient");
        var bow=rules.Lines.First(line=>line.weapons.Any(w=>w.type=="missile"&&w.full==0&&w.half==0&&w.quarter==0));
        var weapon=bow.weapons.First(w=>w.type=="missile"&&w.full==0&&w.half==0&&w.quarter==0);
        var converted=rules.Spawn(1,basic,new Vector2(0,12));var enemy=rules.Spawn(1,basic,new Vector2(0,-12));
        float convertedHp=converted.hp,enemyHp=enemy.hp;
        rules.Shots.Add(new HumanMatch.Shot{target=converted.id,sourceId=-123,team=0,kind=0,weapon=weapon,duration=.1f,damage=50});
        rules.Shots.Add(new HumanMatch.Shot{target=enemy.id,sourceId=-123,team=0,kind=0,weapon=weapon,duration=.1f,damage=50});
        converted.team=0;rules.Step(.2f);
        Check(converted.hp==convertedHp&&enemy.hp<enemyHp,"An in-flight shot respects the target's new team and still hits an enemy");
        Check(rules.Rescue(0,enemy.position)&&rules.RescueCharges[0]==0&&enemy.hp<=0&&converted.hp==convertedHp,
            "Rescue consumes one charge, hits enemies, and spares allies");
        yield return ExerciseFoundation(folder);

        File.WriteAllText(Path.Combine(folder,"models039-verification.json"),new JObject{
            ["status"]=testFailures==0?"PASS":"FAIL",["build"]=Version,["checks"]=testChecks,["verifiedTeamModels"]=valid
        }.ToString());
        Debug.Log("CF_MODELS039_TEST_RESULT failures="+testFailures+" checks="+testChecks.Count);
        Application.Quit(testFailures==0?0:1);
    }
}
}
