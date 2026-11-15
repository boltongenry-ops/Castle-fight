using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    void NorthClear() {
        match.Units.Clear();match.Huts.Clear();match.Shots.Clear();match.Corpses.Clear();match.Pulses.Clear();match.NorthernOrbs.Clear();match.NorthernWinters.Clear();match.NorthernFreezes.Clear();match.NorthernWeather=0;match.Winner=-2;match.CastleHp[0]=match.CastleHp[1]=HumanMatch.CastleMax;match.Gold[1]=0;
        foreach(var builder in match.Builders)builder.jobs.Clear();selectedUnit=selectedHut=-1;builderSelected=false;
    }
    HumanMatch.Unit NorthUnit(int team,string raw,Vector2 at) {var unit=NatureUnit(team,raw,at);unit.stun=100;return unit;}
    void NorthTime(float seconds){for(int i=0;i<Mathf.RoundToInt(seconds/.05f);i++)Tick(.05f);}
    IEnumerator ExerciseNorthernOnly() {
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"northern-validation"));Directory.CreateDirectory(folder);
        aspectsOn=false;MainMenu();botRaceChoice=0;yield return Action("main.play");yield return Action("setup.northern");
        Check(raceChoice==5&&northernPack,"The complete Northern race is selectable");yield return Shot(folder,"01-northern-selection");startGold=1000;yield return Action("setup.start");testHoldAuthority=true;RenderMatch();
        Check(match.TeamNorthern[0]&&!match.TeamNorthern[1]&&matchRace==5&&builders[0].raw=="h017"&&!builders[0].reforged,"Northern identity and classic worker are independent from Human opponent");
        Check(match.BaseKinds.Length==11&&AspectCount(5)==2&&northernPack.buildingIcons.Length==17,"Northern has eleven base commands, six upgrades and both aspects");
        Check(CommandIcon(0)==northernPack.builderIcon&&CommandIcon(6)==northernPack.buildIcon,"Northern builder and construction command use source icons");
        Check(new[]{"A03Y","A03X","A03W","A03Z","A040","A044","A045","A04I","A04J","A04L","A05C","A05E","A080","A081","A0AK","A0FL"}.All(id=>AbilityArt(id)),"All Northern spell, projectile and hostile status icons resolve");
        match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;match.LegendaryLimit[0]=8;
        for(int i=0;i<match.BaseKinds.Length;i++) {
            int kind=match.BaseKinds[i];var line=match.Lines[kind];float gold=match.Gold[0];int wood=match.Wood[0];var hut=QaBuild(kind,3+i%6*4,15+i/6*4);hut.paused=true;RenderMatch();
            Check(hut.paid&&hut.spent==line.cost&&Mathf.Abs(hut.income-line.cost*line.incomeRate)<.001f,"Northern construction economy "+line.building);
            Check(match.Wood[0]==wood+line.woodReward-line.woodCost&&match.Gold[0]<=gold-line.cost+match.Income(0)+1,"Northern construction wood and payment "+line.building);
            Check(huts[hut.id]&&huts[hut.id].raw==line.building&&!huts[hut.id].reforged,"Northern classic building renders "+line.building);
            if(line.Producer){hut.paused=false;hut.spawnLeft=.01f;Tick(.05f);hut.paused=true;Check(match.Units.Any(u=>u.kind==hut.kind&&u.hp>0),"Northern base producer actually releases "+line.unit);}
        }
        controls.FocusWorld(World(new Vector2(-141,32)),57);yield return Shot(folder,"02-northern-buildings");
        string[] from={"h048","h049","h04F","h03T","h03K","h03U"},to={"h03L","h04F","h03W","h043","h03J","h06L"};
        for(int i=0;i<from.Length;i++) {
            var hut=match.Huts.First(h=>match.Lines[h.kind].building==from[i]);int invested=hut.spent;float gold=match.Gold[0];int wood=match.Wood[0];int kind=match.Kind(to[i]);var line=match.Lines[kind];QaSelect(hut);yield return Action("upgrade."+to[i]);hut.paused=true;RenderMatch();
            Check(hut.kind==kind&&hut.spent==invested+line.cost,"Northern real upgrade keeps cumulative gold "+to[i]);
            Check(match.Wood[0]==wood-line.woodCost+line.woodReward&&match.Gold[0]<gold-line.cost+2,"Northern real upgrade charges source resources "+to[i]);
            Check(Mathf.Abs(match.Interval(hut)-(15+hut.spent/20f))<.001f&&huts[hut.id].raw==to[i],"Northern upgrade interval and saved visual "+to[i]);
            if(line.Producer){hut.paused=false;hut.spawnLeft=.01f;Tick(.05f);hut.paused=true;Check(match.Units.Any(u=>u.kind==hut.kind&&u.hp>0),"Northern upgraded producer actually releases "+line.unit);}
        }
        var legend=match.Huts.First(h=>match.Lines[h.kind].building=="h06L");Check(legend.spent==740&&Mathf.Abs(match.Interval(legend)-52)<.001f&&match.Lines[legend.kind].woodReward==145&&match.Lines[legend.kind].slots==1,"Great igloo uses 740 invested gold, 52 seconds and quarter wood reward");
        QaSelect(legend);controls.FocusWorld(World(legend.position),33);yield return Shot(folder,"03-northern-upgrades");
        NorthClear();string[] roster={"h049","h04F","h03W","h03T","h043","h03K","h03J","h03U","h06L","h03I","h03S"};
        for(int i=0;i<roster.Length;i++)NorthUnit(0,roster[i],new Vector2(-165+i%6*7,25+i/6*10));RenderMatch();
        foreach(string raw in roster) {var unit=match.Units.Single(u=>u.kind==match.Kind(raw));var model=units[unit.id];Check(!model.reforged&&model.godotHeight==0&&new[]{"Stand","Walk","Attack","Death"}.All(pose=>model.clips.Any(c=>c.name.StartsWith(pose))),"Northern actual classic clips "+match.Lines[unit.kind].unit);}
        var tusk=match.Units.Single(u=>u.kind==match.Kind("h03U"));Check(Mathf.Abs(units[tusk.id].animatedRoot.transform.localScale.x-.6f)<.0001f,"Tuskarr uses the Godot 0.6 outer scale correction");
        builderSelected=false;selectedUnit=match.Units.Single(u=>u.kind==match.Kind("h03S")).id;controls.FocusWorld(World(new Vector2(-148,30)),58);yield return Shot(folder,"04-northern-full-roster");
        Check(northernPack.Prefabs.All(p=>p.GetComponent<PcModel>().clips.All(c=>c))&&ContentResources.LoadAll<GameObject>("NorthernFx").Length==5,"Full race retains every saved animation and five available classic effects");
        NorthClear();var dragon=NorthUnit(0,"h03I",new Vector2(-148,32));var enemy=NorthUnit(1,"h000",new Vector2(-140,32));enemy.hp=enemy.maxHp=10000;dragon.stun=0;dragon.cooldown=0;
        for(int i=0;i<45&&!enemy.effects.Any(e=>e.id=="A03Y");i++)Tick(.05f);dragon.stun=100;
        Check(enemy.effects.Any(e=>e.id=="A03Y")&&Mathf.Abs(match.Stat(enemy,"speed")+.5f)<.001f&&Mathf.Abs(match.Stat(enemy,"haste")+.25f)<.001f,"Frost weapon applies fifty percent movement and twenty-five percent attack slow on impact");
        NorthClear();var tower=ChaosHut(0,"h03Q",new Vector2(-148,32));enemy=NorthUnit(1,"h000",new Vector2(-140,32));enemy.hp=enemy.maxHp=10000;for(int i=0;i<55&&!enemy.effects.Any(e=>e.id=="A03Y");i++)Tick(.05f);tower.paused=true;
        Check(enemy.effects.Any(e=>e.id=="A03Y"),"Northern tower applies its frost attack after weapon impact");
        NorthClear();var shaman=NorthUnit(0,"h03K",new Vector2(-148,32));var ally=NorthUnit(0,"h03U",new Vector2(-147,32));shaman.stun=0;shaman.mana=100;
        float armor=ally.armor;Check(match.Cast(shaman,"A03Z",ally.id)&&Mathf.Abs(match.Stat(ally,"armor")-3.5f)<.001f,"Frost Armor grants the native three-and-a-half armor");shaman.stun=100;
        enemy=NorthUnit(1,"h000",new Vector2(-145,32));enemy.stun=0;enemy.cooldown=0;ally.hp=ally.maxHp=10000;for(int i=0;i<45&&!enemy.effects.Any(e=>e.id=="A03Y");i++)Tick(.05f);enemy.stun=100;
        Check(enemy.effects.Any(e=>e.id=="A03Y"&&e.lifetime==4),"Melee hits on Frost Armor chill the attacker for four seconds");
        NorthClear();shaman=NorthUnit(0,"h03J",new Vector2(-148,32));ally=NorthUnit(0,"h03U",new Vector2(-147,32));shaman.stun=0;shaman.mana=100;Check(match.Cast(shaman,"A040",ally.id)&&match.Stat(ally,"armor")==7&&ally.effects.Single().lifetime==45,"Upgraded Frost Armor gives seven armor for forty-five seconds");shaman.stun=100;
        var ranged=NorthUnit(1,"h004",new Vector2(-140,32));ranged.stun=0;ranged.cooldown=0;ally.hp=ally.maxHp=10000;NorthTime(2);ranged.stun=100;Check(!ranged.effects.Any(e=>e.id=="A03Y"),"Ranged missiles do not trigger Frost Armor retaliation");
        NorthClear();var chains=NorthUnit(0,"h03U",new Vector2(-148,32));enemy=NorthUnit(1,"h000",new Vector2(-146,32));enemy.hp=enemy.maxHp=10000;var chance=match.Abilities["A03X"]["chance"].DeepClone();match.Abilities["A03X"]["chance"]=1;chains.stun=0;chains.cooldown=0;
        for(int i=0;i<35&&!match.Rooted(enemy);i++)Tick(.05f);chains.stun=100;match.Abilities["A03X"]["chance"]=chance;
        Check(match.Rooted(enemy)&&match.Disarmed(enemy)&&enemy.effects.Any(e=>e.id=="A03W"&&e.lifetime==7),"Tuskarr orb roots and disarms for seven seconds on a successful hit");float hp=enemy.hp;NorthTime(1.1f);Check(enemy.hp<hp-10,"Ice chains deliver their fifteen magic damage per second");
        match.ApplyEffect(enemy,"A047",0);match.ApplyEffect(enemy,"A03W",0);Check(enemy.effects.Count(e=>e.group=="northern_ice_chains")==1&&enemy.effects.Any(e=>e.id=="A047"),"Stronger chains replace weaker chains and cannot be downgraded");
        RenderMatch();selectedUnit=enemy.id;controls.FocusWorld(World(enemy.position),29);yield return Shot(folder,"05-northern-chains");
        NorthClear();var mage=NorthUnit(0,"h03S",new Vector2(-148,32));enemy=NorthUnit(1,"h000",new Vector2(-142,32));var secondary=NorthUnit(1,"h004",new Vector2(-140,32));enemy.hp=enemy.maxHp=secondary.hp=secondary.maxHp=10000;mage.stun=0;mage.mana=200;
        Check(match.Cast(mage,"A044",enemy.id)&&enemy.hp<secondary.hp&&enemy.effects.Any(e=>e.id=="A043"),"Frost Nova hits its primary harder than nearby secondary targets");mage.stun=100;Check(match.Stat(enemy,"damage")==0&&match.Stat(enemy,"haste")==-.25f,"Frost Nova slow does not turn its damage payload into a weapon bonus");
        NorthClear();var elder=NorthUnit(0,"h043",Vector2.zero);enemy=NorthUnit(1,"h000",Vector2.right*3);secondary=NorthUnit(1,"h004",Vector2.right*20);elder.stun=0;elder.mana=100;
        Check(match.Cast(elder,"A05C",enemy.id)&&enemy.effects.Any(e=>e.id=="A05B")&&secondary.effects.Any(e=>e.id=="A05B"),"Elder Wendigo howl uses its short initiation range and wide caster-centered area");elder.stun=100;
        Check(Mathf.Abs(match.Stat(enemy,"damage")+.3f)<.001f,"Wendigo howl lowers enemy attack damage by thirty percent");
        NorthClear();var giant=NorthUnit(0,"h03W",Vector2.zero);enemy=NorthUnit(1,"h000",Vector2.right*2);enemy.hp=enemy.maxHp=10000;giant.stun=0;giant.mana=100;Check(match.Cast(giant,"A05E",enemy.id)&&match.Disabled(enemy)&&enemy.hp<10000&&match.Stat(enemy,"damage")==0,"Ice Giant stomp deals damage and disables without granting weapon damage");giant.stun=100;
        NorthClear();var wendigo=NorthUnit(0,"h03T",Vector2.zero);enemy=NorthUnit(1,"h000",Vector2.right*2);secondary=NorthUnit(1,"h004",new Vector2(3,1));enemy.hp=enemy.maxHp=secondary.hp=secondary.maxHp=10000;match.Abilities["A059"]["chance"]=1;wendigo.stun=0;wendigo.cooldown=0;int pulverizes=match.PulverizeProcs;
        for(int i=0;i<40&&match.PulverizeProcs==pulverizes;i++)Tick(.05f);wendigo.stun=100;Check(match.PulverizeProcs>pulverizes&&secondary.hp<10000,"Wendigo pulverize delivers actual secondary physical damage");match.Abilities["A059"]["chance"]=.25f;
        yield return NorthernStructuresChecks(folder);
        yield return NorthernOrbsChecks(folder);
        yield return NorthernAspectsChecks(folder);
        aspectsOn=false;MainMenu();raceChoice=0;botRaceChoice=5;yield return Action("main.play");yield return Action("setup.start");testHoldAuthority=true;NorthTime(8);RenderMatch();
        Check(match.TeamNorthern[1]&&builders[1].raw=="h017"&&match.Huts.Any(h=>h.team==1&&h.paid&&match.Lines[h.kind].building=="h049"),"Northern bot uses its own worker, roster and production order");
        var hostile=NorthUnit(1,"h06L",new Vector2(-148,32));RenderMatch();selectedUnit=hostile.id;builderSelected=false;Check(AbilityArt("A0FL")&&AbilityArt("A0FM"),"Enemy Northern unit card resolves its own barrage and chain icons");controls.FocusWorld(World(hostile.position),30);yield return Shot(folder,"10-northern-enemy-card");
        var sizes=new JArray(northernPack.units.Where(v=>v).Select(v=>new JObject { ["raw"]=v.GetComponent<PcModel>().raw,["scale"]=v.GetComponent<PcModel>().animatedRoot.transform.localScale.x,["clips"]=new JArray(v.GetComponent<PcModel>().clips.Select(c=>c.name)) }));
        var result=new JObject {["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["build"]=Version,["catalog"]=JObject.Parse(northernPack.catalog.text),["models"]=sizes};File.WriteAllText(Path.Combine(folder,"northern-verification.json"),result.ToString());
        Debug.Log("CF_NORTHERN_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}
