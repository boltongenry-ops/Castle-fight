using System.Collections;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseNagaOnly() {
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"naga-validation"));Directory.CreateDirectory(folder);
        yield return ExerciseNaga(folder);
        var result=new JObject {["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["build"]=Version};
        File.WriteAllText(Path.Combine(folder,"naga-verification.json"),result.ToString());Debug.Log("CF_NAGA_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
    IEnumerator ExerciseNaga(string folder) {
        MainMenu();botRaceChoice=0;yield return Action("main.play");yield return Action("setup.naga");yield return Shot(folder,"93-naga-selection");
        Check(raceChoice==2&&nagaPack&&botRaceChoice==0,"Naga first stage can be selected against Humans");
        startGold=1000;options.hdTextures=true;yield return Action("setup.start");testHoldAuthority=true;RenderMatch();
        Check(builders[0].raw=="h00E"&&builders[0].reforged&&builders[1].raw=="h00C","mixed match uses Naga worker and Human worker models");
        Check(match.BaseKinds.Length==11&&match.Kind("h020")>=0&&match.Kind("h008")>=0&&match.Kind("h022")>=0,"Naga catalog exposes Murloc camp, spawning grounds, snake shrine and chest");
        Check(match.Aspects[0]<0&&match.PurchaseError(0,match.Kind("h000"))=="Постройка другой расы","unimplemented Naga aspects are disabled and enemy construction rejected");
        match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;var hut=QaBuild(match.Kind("h020"),7,15);RenderMatch();
        Check(hut.paid&&huts[hut.id].raw=="h020"&&huts[hut.id].reforged&&hut.spent==170,"Naga builder completes the original camp");
        Check(orcDecorModels.ContainsKey(hut.id+"/naga_roots/0")&&orcDecorModels[hut.id+"/naga_roots/0"].raw=="fx_naga_roots","Murloc camp uses the original Reforged root decoration");
        hut.spawnLeft=.01f;int before=match.ArmyCount(0);Tick(.05f);Check(match.ArmyCount(0)==before+2&&match.Units.Count(u=>u.team==0&&match.Lines[u.kind].unit=="n00I")==2,"Murloc camp produces a pair on each production cycle");
        QaSelect(hut);yield return Action("upgrade.h021");Check(hut.spent==350&&Mathf.Abs(hut.spawnLeft-32.5f)<.01f&&match.Lines[hut.kind].unit=="n00J","first Murloc upgrade uses cumulative investment and interval");
        QaSelect(hut);yield return Action("upgrade.h022");RenderMatch();Check(hut.spent==540&&Mathf.Abs(hut.spawnLeft-42)<.01f&&match.Lines[hut.kind].unit=="n00K"&&huts[hut.id].raw=="h022","final Murloc upgrade preserves cumulative cost and model");
        Check(orcDecorModels.ContainsKey(hut.id+"/naga_roots/0")&&orcDecorModels.ContainsKey(hut.id+"/naga_coral/0")&&reforgedFx.ContainsKey("naga_decor_crystal"),"Murloc fortress displays Reforged roots, coral and crystal");controls.FocusWorld(World(hut.position),35);yield return new WaitForSecondsRealtime(.15f);yield return Shot(folder,"103-naga-building-decor");
        foreach(var u in match.Units)u.cooldown=10000;
        for(int i=0;i<3;i++){var u=match.Spawn(0,match.Kind(new[]{"h020","h021","h022"}[i]),new Vector2(-158+i*4,35));u.onRoad=true;u.cooldown=10000;u.pose="Stand";}
        RenderMatch();builderSelected=false;selectedHut=-1;selectedUnit=match.Units.Last().id;controls.FocusWorld(World(new Vector2(-154,34)),30);yield return new WaitForSecondsRealtime(.3f);yield return Shot(folder,"94-naga-reforged");
        Check(portraitModel&&portraitModel.raw=="n00K"&&Vector3.Distance(portraitCamera.transform.position,portraitModel.BodyBounds().center)>2,"Murloc portrait uses a wider body framing");
        Check(units.Values.Any(v=>v.raw=="n00I"&&v.reforged)&&units.Values.Any(v=>v.raw=="n00J"&&v.reforged)&&units.Values.Any(v=>v.raw=="n00K"&&v.reforged),"all three Murloc models render in Reforged mode");
        Check(AbilityArt("A061")&&AbilityArt("A025")&&Mathf.Abs(SelectedUnit.critChance-.2f)<.001f&&Mathf.Abs(SelectedUnit.evasion-.15f)<.001f,"final Murloc receives its source icons critical and evasion");
        options.hdTextures=true;Apply(options,false);RenderMatch();yield return new WaitForSecondsRealtime(.2f);yield return Shot(folder,"95-naga-classic");Check(units.Values.All(v=>v.reforged)&&builders.All(v=>v.reforged)&&huts.Values.All(v=>v.reforged),"Naga and Human actors keep Reforged models");
        options.hdTextures=true;Apply(options,false);RenderMatch();foreach(var u in match.Units)u.hp=0;for(int i=0;i<100;i++)Tick(.05f);
        var attacker=match.Spawn(0,match.Kind("h022"),new Vector2(-1,0));var target=match.Spawn(1,match.Kind("h000"),new Vector2(1,0));attacker.onRoad=target.onRoad=true;target.maxHp=target.hp=100000;target.maxMana=target.mana=100;target.manaRegen=0;target.cooldown=10000;
        for(int i=0;i<200&&match.FeedbackHits==0;i++)Tick(.05f);Check(match.FeedbackHits>0&&target.mana<=75.001f&&target.hp<100000,"Murloc mana feedback occurs on an actual enemy hit");
        foreach(var u in match.Units)u.hp=0;for(int i=0;i<80;i++)Tick(.05f);match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;
        var grounds=QaBuild(match.Kind("h00F"),13,15);QaSelect(grounds);yield return Action("upgrade.h032");RenderMatch();
        Check(grounds.spent==450&&match.Lines[grounds.kind].unit=="n00X"&&huts[grounds.id].raw=="h032"&&Mathf.Abs(grounds.spawnLeft-37.5f)<.01f,"Royal spawning grounds upgrades with original costs and production interval");
        Check(reforgedFx.ContainsKey("naga_decor_immolation")&&BuildingEffectAnchor(grounds,huts[grounds.id],2)&&BuildingEffectAnchor(grounds,huts[grounds.id],3),"Royal spawning grounds has all three Reforged flame attachments");
        var shrine=QaBuild(match.Kind("h01Y"),18,15);Check(match.Lines[shrine.kind].unit=="n00H"&&shrine.spent==230,"Naga shrine produces the original flying serpent");
        var guard=match.Spawn(0,match.Kind("h032"),new Vector2(-154,35));var serpent=match.Spawn(0,match.Kind("h01Y"),new Vector2(-148,35));guard.onRoad=serpent.onRoad=true;guard.cooldown=serpent.cooldown=10000;RenderMatch();
        Check(match.Flying(serpent)&&!match.Flying(guard)&&units[serpent.id].reforged&&units[guard.id].reforged,"Royal guard and flying serpent have their native movement profiles and HD models");
        builderSelected=false;selectedHut=-1;selectedUnit=guard.id;controls.FocusWorld(World(new Vector2(-151,35)),32);yield return new WaitForSecondsRealtime(.3f);yield return Shot(folder,"96-naga-guard-serpent");
        options.hdTextures=true;Apply(options,false);RenderMatch();Check(units[serpent.id].reforged&&units[guard.id].reforged,"guard and serpent keep Reforged models");options.hdTextures=true;Apply(options,false);
        foreach(var u in match.Units)u.hp=0;for(int i=0;i<80;i++)Tick(.05f);match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;
        int woodBefore=match.Wood[0];var well=QaBuild(match.Kind("h00J"),21,15);Check(match.Lines[well.kind].unit=="n004"&&match.Wood[0]-woodBefore==175&&match.Lines[well.kind].upgrades.Contains("h05F"),"swamp well grants normal wood and offers Hydra upgrade");
        woodBefore=match.Wood[0];var pond=QaBuild(match.Kind("h01B"),25,15);Check(match.Lines[pond.kind].unit=="n007"&&match.Wood[0]-woodBefore==210&&Mathf.Abs(pond.income-4.76f)<.01f,"siege turtle pond grants 75 percent wood and siege income");
        var turtle=match.Spawn(0,match.Kind("h01B"),new Vector2(-1,0));turtle.onRoad=true;turtle.cooldown=10000;turtle.hp=turtle.maxHp=100000;float beforeHealth=turtle.hp;float expected=100*.8f*match.Multiplier("chaos",match.Lines[turtle.kind].armorType)*HumanMatch.ArmorFactor(turtle.armor);match.DamageUnit(turtle,100,1,"chaos",false);Check(Mathf.Abs((beforeHealth-turtle.hp)-expected)<.05f,"turtle shell reduces physical damage by 20 percent before armor");
        var enemy=match.Spawn(1,match.Kind("h000"),new Vector2(1,0));enemy.onRoad=true;enemy.hp=enemy.maxHp=100000;enemy.cooldown=0;for(int i=0;i<120&&match.SpikedHits==0;i++)Tick(.05f);Check(match.SpikedHits>0&&enemy.hp<100000,"turtle reflects a successful melee hit");
        foreach(var u in match.Units)u.hp=0;for(int i=0;i<80;i++)Tick(.05f);var snap=match.Spawn(0,match.Kind("h00J"),new Vector2(-2,0));var victim=match.Spawn(1,match.Kind("h000"),new Vector2(2,0));snap.onRoad=victim.onRoad=true;victim.hp=victim.maxHp=100000;victim.cooldown=10000;for(int i=0;i<300&&!victim.effects.Any(e=>e.id=="A00C");i++)Tick(.05f);Check(victim.effects.Any(e=>e.id=="A00C")&&Mathf.Abs(match.Stat(victim,"speed")+.35f)<.001f&&Mathf.Abs(match.Stat(victim,"haste")+.35f)<.001f,"Snapdragon projectile applies poison damage and both slows on impact");
        snap.cooldown=10000;var turtleView=match.Spawn(0,match.Kind("h01B"),new Vector2(-154,35));snap.position=snap.previous=new Vector2(-160,35);snap.pose=turtleView.pose="Stand";turtleView.onRoad=true;turtleView.cooldown=10000;RenderMatch();builderSelected=false;selectedHut=-1;selectedUnit=turtleView.id;controls.FocusWorld(World(new Vector2(-157,35)),34);yield return new WaitForSecondsRealtime(.3f);yield return Shot(folder,"97-naga-turtle-snapdragon");Check(units[turtleView.id].reforged&&units[snap.id].reforged&&AbilityArt("A01C")&&AbilityArt("A00C"),"siege turtle and Snapdragon render their Reforged models and ability icons");
        options.hdTextures=true;Apply(options,false);RenderMatch();Check(units[turtleView.id].reforged&&units[snap.id].reforged,"new Naga units keep Reforged models");options.hdTextures=true;Apply(options,false);
        foreach(var u in match.Units)u.hp=0;for(int i=0;i<80;i++)Tick(.05f);match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;
        var coral=QaBuild(match.Kind("h00G"),3,19);var pyramid=QaBuild(match.Kind("h00I"),8,19);Check(coral.paid&&pyramid.paid&&coral.spent==250&&pyramid.spent==360,"coral statue and ancient pyramid can be constructed at source costs");
        var ally=match.Spawn(0,match.Kind("h020"),coral.position+new Vector2(1,0));ally.onRoad=true;ally.cooldown=10000;ally.hp=ally.maxHp-100;float hp0=ally.hp;for(int i=0;i<20;i++)Tick(.05f);Check(ally.hp-hp0>=7.4f,"coral statue heals nearby allied units");
        var quakeTarget=match.Spawn(1,match.Kind("h000"),new Vector2(0,0));quakeTarget.onRoad=true;quakeTarget.hp=quakeTarget.maxHp=100000;quakeTarget.cooldown=10000;pyramid.mana=10;for(int i=0;i<40&&match.NagaQuakes==0;i++)Tick(.05f);Check(match.NagaQuakes>0&&quakeTarget.hp<100000&&quakeTarget.effects.Any(e=>e.id=="A00B")&&Mathf.Abs(match.Stat(quakeTarget,"damage"))<.001f&&Mathf.Abs(match.Stat(quakeTarget,"speed")+.5f)<.001f,"ancient pyramid damages and slows a real ground enemy");
        RenderMatch();Check(unitModels.Length==0&&buildingModels.Length==0&&builderModels.Length==0&&nagaPack.units.Length==0&&orcPack.units.Length==0&&castleObjects.All(c=>c.GetComponent<PcModel>()&&c.GetComponent<PcModel>().reforged),"the player has only Reforged race and castle models");
        controls.FocusWorld(World(pyramid.position),38);yield return new WaitForSecondsRealtime(.2f);yield return Shot(folder,"98-naga-specials");
        foreach(var u in match.Units)u.hp=0;for(int i=0;i<80;i++)Tick(.05f);match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;
        var oracle=QaBuild(match.Kind("h00M"),13,19);Check(oracle.paid&&oracle.spent==310,"Siren oracle builds with original gold cost");
        var captured=match.Spawn(1,match.Kind("h000"),oracle.position+new Vector2(4,0));captured.onRoad=true;captured.stun=10;captured.cooldown=10000;oracle.mana=16;
        for(int i=0;i<30&&match.NagaControls==0;i++)Tick(.05f);RenderMatch();
        Check(match.NagaControls==1&&captured.team==0&&units[captured.id].raw==match.Lines[captured.kind].unit&&unitViewTeams[captured.id]==0,"oracle captures enemy and mixed-race view preserves original model with new team color");
        Check(match.Pulses.Any(p=>p.fx=="naga_charm")&&reforgedFx.ContainsKey("naga_charm"),"oracle success plays original Reforged Naga charm effect");controls.FocusWorld(World(captured.position),30);yield return new WaitForSecondsRealtime(.15f);yield return Shot(folder,"101-naga-charm");
        var shielded=match.Spawn(1,match.Kind("h000"),oracle.position+new Vector2(6,0));shielded.onRoad=true;shielded.stun=10;shielded.cooldown=10000;shielded.hp=shielded.maxHp-30;
        match.Grant(shielded,"A10A");match.Grant(shielded,"A09L");shielded.abilityLevels["A10A"]=2;shielded.abilityLevels["A09L"]=2;oracle.mana=16;oracle.spellTimers["A00O"]=0;Tick(.05f);
        Check(shielded.team==1&&shielded.abilityLevels["A10A"]==1&&shielded.abilities.Contains("A10B")&&Mathf.Abs(shielded.hp-shielded.maxHp)<.1f,"oracle consumes higher-level shield as Godot and restores target health");
        oracle.mana=16;oracle.spellTimers["A00O"]=0;Tick(.05f);Check(shielded.team==1&&!shielded.abilities.Contains("A10A")&&!shielded.abilities.Contains("A09L"),"oracle removes depleted shield and remains blocked for that cast");
        oracle.mana=16;oracle.spellTimers["A00O"]=0;Tick(.05f);Check(shielded.team==0,"oracle converts target after its shield is spent");shielded.hp=0;oracle.paused=true;pyramid.paused=true;
        var guardTower=QaBuild(match.Kind("h00N"),18,19);Check(guardTower.paid&&guardTower.spent==750&&match.Lines[guardTower.kind].slots==1&&Mathf.Abs(guardTower.income-5.25f)<.01f,"Tidal guardian consumes a legendary slot and uses tower income");
        var first=match.Spawn(1,match.Kind("h000"),guardTower.position+new Vector2(8,0));var second=match.Spawn(1,match.Kind("h000"),guardTower.position+new Vector2(16,0));
        foreach(var u in new[]{first,second}){u.hp=u.maxHp=100000;u.stun=10;u.onRoad=true;u.cooldown=10000;}
        var waveFriend=match.Spawn(0,match.Kind("h020"),guardTower.position+new Vector2(11,0));var waveAir=match.Spawn(1,match.Kind("h01Y"),guardTower.position+new Vector2(13,0));foreach(var u in new[]{waveFriend,waveAir}){u.hp=u.maxHp=100000;u.stun=10;u.onRoad=true;u.cooldown=10000;}
        for(int i=0;i<45&&(first.hp==100000||second.hp==100000);i++)Tick(.05f);
        Check(match.NagaWaves>0&&first.hp<100000&&second.hp<100000,"tidal wave travels forward and damages multiple ground enemies");
        Check(waveFriend.hp==100000&&waveAir.hp==100000&&match.ActiveNagaWaves.Count>0&&match.Pulses.Any(p=>p.fx=="naga_wave_impact"),"tidal wave spares allies and flying units and shows Reforged hit effect");
        Check(reforgedFx.ContainsKey("naga_wave")&&reforgedFx["naga_wave"].info.Loops,"traveling tsunami uses the original Reforged particle atlas");RenderMatch();controls.FocusWorld(World(guardTower.position+new Vector2(14,0)),45);yield return new WaitForSecondsRealtime(.15f);yield return Shot(folder,"102-naga-wave");
        RenderMatch();controls.FocusWorld(World(guardTower.position),40);yield return new WaitForSecondsRealtime(.2f);yield return Shot(folder,"99-naga-oracle-tidal");

        foreach(var u in match.Units)u.hp=0;for(int i=0;i<80;i++)Tick(.05f);match.Sell(0,guardTower.id);match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;
        var sirenFountain=QaBuild(match.Kind("h01Z"),23,19);Check(sirenFountain.paid&&match.Lines[sirenFountain.kind].unit=="n00S"&&sirenFountain.spent==350,"Naga fountain produces the source Siren");
        int oldWood=match.Wood[0];QaSelect(well);yield return Action("upgrade.h05F");
        Check(match.Lines[well.kind].unit=="n01P"&&well.spent==900&&match.Wood[0]==oldWood-819&&match.LegendaryUsed(0)==1,"Hydra upgrade uses cumulative gold, legendary slot and quarter wood reward");
        foreach(var h in match.Huts)h.paused=true;
        var hydra=match.Spawn(0,match.Kind("h05F"),new Vector2(-10,0));hydra.onRoad=true;hydra.cooldown=10000;match.DamageUnit(hydra,hydra.hp+1,1,"chaos",true,true);Tick(.05f);
        var minors=match.Units.Where(u=>u.hp>0&&match.Lines[u.kind].unit=="n01Q").ToArray();Check(minors.Length==2&&minors.All(u=>u.summoned),"ancient Hydra splits into two summoned smaller Hydras");
        foreach(var u in minors)match.DamageUnit(u,u.hp+1,1,"chaos",true,true);Tick(.05f);
        Check(match.Units.Count(u=>u.hp>0&&match.Lines[u.kind].unit=="n01R")==4,"smaller Hydras split into four young Hydras");
        foreach(var u in match.Units)u.hp=0;for(int i=0;i<80;i++)Tick(.05f);
        var siren=match.Spawn(0,match.Kind("h01Z"),new Vector2(-3,0));var purgeTarget=match.Spawn(1,match.Kind("h000"),new Vector2(2,0));siren.onRoad=purgeTarget.onRoad=true;siren.cooldown=purgeTarget.cooldown=10000;purgeTarget.hp=purgeTarget.maxHp=100000;purgeTarget.stun=10;match.ApplyEffect(purgeTarget,"A00C",0);
        Check(match.Cast(siren,"A02U",purgeTarget.id)&&purgeTarget.effects.Any(e=>e.id=="A02T")&&!purgeTarget.effects.Any(e=>e.id=="A00C")&&match.Rooted(purgeTarget),"Siren purge removes dispellable poison and roots target");Check(match.Pulses.Any(p=>p.fx=="naga_purge")&&reforgedFx.ContainsKey("naga_purge"),"Siren purge uses the Reforged effect");
        siren.spellTimers["A02U"]=10;for(int i=0;i<25;i++)Tick(.05f);var lobsters=match.Units.Where(u=>u.hp>0&&match.Lines[u.kind].unit=="n00W").ToArray();
        Check(lobsters.Length==2&&lobsters.All(u=>u.summoned&&u.summonLeft>40&&Mathf.Abs(u.critChance-.1f)<.001f),"Siren summons two timed lobstroks with their critical strike");
        int reduced=0,unblocked=0;var shell=lobsters[0];shell.armor=0;shell.maxHp=100000;for(int i=0;i<40;i++){shell.hp=shell.maxHp;float loss=match.DamageUnit(shell,100,1,"chaos",false);float shellExpected=100*match.Multiplier("chaos",match.Lines[shell.kind].armorType);if(Mathf.Abs(loss-shellExpected)<.1f)unblocked++;if(Mathf.Abs(loss-Mathf.Min(shellExpected,Mathf.Max(5,shellExpected-35)))<.1f)reduced++;}Check(reduced>0&&unblocked>0,"Lobstrok hardened shell can block weapon damage without blocking every hit");
        RenderMatch();controls.FocusWorld(World(siren.position),38);yield return new WaitForSecondsRealtime(.2f);yield return Shot(folder,"100-naga-siren-hydra");
        MainMenu();raceChoice=2;botRaceChoice=0;aspectsOn=true;yield return Action("main.play");yield return Action("setup.naga");yield return Action("setup.start");testHoldAuthority=true;Check(aspectWaiting&&match.Aspects[0]<0,"Naga aspect picker waits for the player");yield return Action("aspect.choose0");Check(!aspectWaiting&&match.Aspects[0]==0,"Naga match accepts the ocean depths aspect");
        var depths=match.Spawn(0,match.Kind("h020"),new Vector2(-1,0));depths.onRoad=true;depths.stun=5;depths.hp=depths.maxHp-100;float depthsHp=depths.hp;float depthSpeed=match.MoveSpeed(depths);for(int i=0;i<20;i++)Tick(.05f);
        Check(match.TeamNaga[0]&&match.Aspects[0]==0&&depths.hp-depthsHp>=3.9f&&Mathf.Abs(depthSpeed-Mathf.Clamp(match.Lines[depths.kind].speed-20f/30,5,13.333333f))<.01f,"Ocean depths adds regeneration and flat movement penalty");
        MainMenu();raceChoice=2;botRaceChoice=0;yield return Action("main.play");yield return Action("setup.naga");yield return Action("setup.start");testHoldAuthority=true;Check(aspectWaiting,"Naga aspect picker opens for the new round");yield return Action("aspect.choose1");Check(match.Aspects[0]==1,"Naga match accepts the sea creatures aspect");match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;
        var seaCoral=QaBuild(match.Kind("h00G"),7,19);seaCoral.seaTimer=.01f;Tick(.05f);var sea=match.Units.FirstOrDefault(u=>u.hp>0&&u.seaSummon);
        Check(sea!=null&&sea.summoned&&sea.noBounty&&sea.maxHp==250&&sea.attackOverride==20&&sea.abilities.Count==0&&sea.summonLeft>29,"Sea creatures aspect creates a temporary simplified no-bounty summon");
        for(int i=0;i<6;i++){seaCoral.seaTimer=.01f;Tick(.05f);}Check(match.Units.Count(u=>u.hp>0&&u.team==0&&u.seaSummon)==6,"Sea creatures aspect respects six summon cap");
        float enemyGold=match.Gold[1];match.DamageUnit(sea,sea.hp+1,1,"chaos",true,true);Check(match.Gold[1]==enemyGold,"Sea creatures grant no bounty when killed");
        var expiring=match.Units.First(u=>u.hp>0&&u.seaSummon);expiring.summonLeft=.01f;Tick(.05f);Check(expiring.hp<=0&&expiring.deathProcessed,"Sea creatures expire after their lifetime");
        aspectsOn=false;

        MainMenu();raceChoice=0;botRaceChoice=2;yield return Action("main.play");yield return Action("setup.human");yield return Action("setup.start");testHoldAuthority=true;RenderMatch();
        Check(builders[0].raw=="h00C"&&builders[1].raw=="h00E"&&match.Kind("h020")>=0,"Human versus Naga bot uses correct models and catalogs");
        for(int i=0;i<90;i++)Tick(.05f);Check(match.Huts.Any(h=>h.team==1&&match.Lines[h.kind].building=="h020"),"Naga bot starts building a Murloc camp");
        raceChoice=0;botRaceChoice=-1;Debug.Log("CF_NAGA_PLAYER_PASS stage=6 models=Reforged");
    }
}
}
