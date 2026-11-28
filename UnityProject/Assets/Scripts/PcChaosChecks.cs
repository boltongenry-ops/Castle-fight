using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseChaosOnly() {
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"chaos-validation"));Directory.CreateDirectory(folder);aspectsOn=false;
        MainMenu();botRaceChoice=0;yield return Action("main.play");yield return Action("setup.chaos");Check(raceChoice==4&&chaosPack,"Chaos initial pack is selectable");yield return Shot(folder,"01-chaos-selection");startGold=1000;yield return Action("setup.start");testHoldAuthority=true;RenderMatch();
        Check(match.TeamChaos[0]&&!match.TeamChaos[1]&&builders[0].raw=="h00O"&&!builders[0].reforged,"Classic Chaos worker and source race identity are independent of Human opponent");Check(match.BaseKinds.Length==11&&AspectCount(4)==2,"Full Chaos catalog exposes eleven base buildings and both aspects");Check(new[]{"A012","A05V","A03R","A03S","A05W","A05Z"}.All(id=>AbilityArt(id)),"Chaos skill and aura icons resolve from its own saved pack");Check(match.Lines[match.Kind("h026")].unitName=="Зерглинг","The upgraded cage uses the Zergling source model and readable card name");match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;
        var cage=QaBuild(match.Kind("h01I"),6,16);Check(cage.paid&&cage.spent==120&&Mathf.Abs(match.Interval(cage)-21)<.01f,"Demonic cage costs 120 and produces every 21 seconds");QaSelect(cage);yield return Action("upgrade.h026");Check(cage.spent==350&&match.Lines[cage.kind].unit=="z000"&&Mathf.Abs(match.Interval(cage)-32.5f)<.01f,"Zergling upgrade costs 230 and uses cumulative investment");Check(match.Lines[cage.kind].upgrades.Contains("h06O"),"Twin Demons upgrade is available after the large cage");
        var citadel=QaBuild(match.Kind("h01D"),10,16);QaSelect(citadel);yield return Action("upgrade.h03G");Check(citadel.spent==350&&match.Lines[citadel.kind].unit=="n00Z","Chaos lord upgrade costs another 190");var dragon=QaBuild(match.Kind("h009"),14,16);var gate=QaBuild(match.Kind("h007"),18,16);Check(dragon.spent==350&&gate.spent==500,"Red dragon and infernal use source gold costs");
        NatureClear();string[] actors={"h01I","h026","h01D","h03G","h009","h007"};for(int i=0;i<actors.Length;i++)NatureUnit(0,actors[i],new Vector2(-155+i*5,31));RenderMatch();builderSelected=false;selectedHut=-1;selectedUnit=match.Units.Last(u=>u.hp>0).id;controls.FocusWorld(World(new Vector2(-141,31)),38);yield return Shot(folder,"02-chaos-classic");Check(actors.All(raw=>!units[match.Units.Last(u=>u.hp>0&&u.kind==match.Kind(raw)).id].reforged),"All six Chaos actors render classic Godot geometry and textures");
        NatureClear();var lord=NatureUnit(0,"h03G",Vector2.zero);var ally=NatureUnit(0,"h01D",Vector2.up);var enemy=NatureUnit(1,"h000",Vector2.right*3);Check(match.Cast(lord,"A03S",enemy.id)&&ally.effects.Any(e=>e.id=="A03R")&&Mathf.Abs(match.Stat(ally,"damage")-.25f)<.001f,"Chaos lord roars in enemy range and grants 25 percent damage");
        Check(lord.critChance>0&&lord.critMultiplier>1,"Chaos lord retains native critical profile");
        CheckZerglingRicochet();
        NatureClear();var walker=NatureUnit(0,"h026",Vector2.zero);ally=NatureUnit(0,"h01D",Vector2.right);enemy=NatureUnit(1,"h000",Vector2.right*2);enemy.cooldown=0;enemy.hp=enemy.maxHp=10000;ally.hp=ally.maxHp=10000;walker.stun=100;ally.stun=100;for(int i=0;i<30;i++)Tick(.05f);Check(ally.auraIds.Contains("A05V")&&Mathf.Abs(ally.auraThorns-.35f)<.001f&&enemy.hp<10000,"Zergling thorns aura reflects melee damage to the attacker");
        NatureClear();var infernal=NatureUnit(0,"h007",Vector2.zero);enemy=NatureUnit(1,"h000",Vector2.right);enemy.hp=enemy.maxHp=10000;enemy.stun=100;infernal.stun=100;for(int i=0;i<30;i++)Tick(.05f);Check(enemy.hp<9980&&enemy.auraIds.Contains("A012"),"Infernal hostile aura deals source magic damage over time");
        NatureClear();infernal=NatureUnit(0,"h007",Vector2.zero);enemy=NatureUnit(1,"h000",Vector2.right);enemy.hp=enemy.maxHp=10000;enemy.spellResist=1;enemy.stun=100;infernal.stun=100;for(int i=0;i<30;i++)Tick(.05f);Check(!enemy.auraIds.Contains("A012")&&enemy.hp==10000,"Infernal hostile aura excludes spell-immune enemies");
        yield return ChaosBuildingChecks(folder);
        yield return ChaosCompletionChecks(folder);
        MainMenu();raceChoice=0;botRaceChoice=4;yield return Action("main.play");yield return Action("setup.start");testHoldAuthority=true;for(int i=0;i<160;i++)Tick(.05f);RenderMatch();Check(match.TeamChaos[1]&&builders[1].raw=="h00O"&&match.Huts.Any(h=>h.team==1&&h.paid&&match.Lines[h.kind].building=="h01I"),"Chaos bot uses own builder and source production order");
        Check(AbilityArt("A012")&&AbilityArt("A05V"),"Enemy Chaos aura icons resolve with a Human player too");
        var report=new JObject {["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["build"]=Version};File.WriteAllText(Path.Combine(folder,"chaos-verification.json"),report.ToString());Debug.Log("CF_CHAOS_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
    void CheckZerglingRicochet(){
        ChaosClear();
        var shooter=NatureUnit(0,"h026",Vector2.zero);
        var primary=NatureUnit(1,"h000",Vector2.right*8);
        var second=NatureUnit(1,"h000",new Vector2(15,1));
        var third=NatureUnit(1,"h000",new Vector2(17,-2));
        var ally=NatureUnit(0,"h000",new Vector2(10,1));
        foreach(var unit in new[]{primary,second,third,ally}){
            unit.hp=unit.maxHp=1000;unit.armor=0;unit.stun=100;
        }
        shooter.cooldown=0;
        shooter.combatTarget=primary.id;
        for(int i=0;i<20&&!match.Shots.Any(s=>s.sourceId==shooter.id);i++)Tick(.05f);
        var initial=match.Shots.FirstOrDefault(s=>s.sourceId==shooter.id);
        Check(initial!=null&&initial.target==primary.id&&initial.duration>.1f&&primary.hp==1000,
            "Zergling launches a visible missile before damaging its first target");
        shooter.cooldown=100;
        for(int i=0;i<12&&primary.hp==1000;i++)Tick(.05f);
        var bounce=match.Shots.FirstOrDefault(s=>s.sourceId==shooter.id&&s.target==second.id);
        Check(primary.hp<1000&&second.hp==1000&&bounce!=null&&initial!=null&&
            Mathf.Abs(bounce.damage-initial.damage*.5f)<.001f,
            "Zergling hits the first target and launches a separate half-damage ricochet");
        for(int i=0;i<12;i++)Tick(.05f);
        Check(second.hp<1000&&third.hp==1000&&ally.hp==1000,
            "Zergling ricochet hits exactly two enemies and ignores the closer ally");
    }
}
}
