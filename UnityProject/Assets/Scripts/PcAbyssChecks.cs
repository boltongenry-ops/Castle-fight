using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseAbyssTide(){
        var folder=BattleLab.Arg("--pc-results",Application.persistentDataPath);
        Directory.CreateDirectory(folder);
        OpenProgression(false);
        yield return Action("progress.mythic");
        yield return Action("challenge."+HumanMatch.AbyssTrial);
        Check(selectedMythicId==HumanMatch.AbyssTrial,"The fifth challenge has its own leaderboard key");
        yield return Action("mythic.play."+HumanMatch.AbyssTrial);
        yield return Action("mythic.race.0");
        yield return Shot(folder,"01-abyss-card");
        Check(abyssPainting&&abyssPainting.width>=1400,"Boss-specific cover art loaded");
        yield return Action("mythic.begin");
        yield return RecoveryWait(()=>abyssIntro&&!versusActive,"The VS screen leads to the flood cinematic",50);
        if(abyssIntro&&abyss!=null){
            Check(abyss.queen&&!FogActive&&controls.InputBlocked,"Queen is on screen immediately and the battle is paused");
            Check(bossSoundtrack&&bossSoundtrack.clip&&bossSoundtrack.clip.name==EpochalBossTrack&&
                bossSoundtrack.isPlaying&&bossSoundtrack.loop&&Mathf.Approximately(bossSoundtrack.volume,1),
                "The cinematic plays the original Eternal Winter soundtrack at full volume");
            yield return RecoveryWait(()=>abyssIntroTime>=2.05f&&abyssIntro,"The portal opens before the wave",10);
            Check(abyss.portal.root.activeSelf&&abyss.portal.changed==0&&abyss.columns==0,"A dry shot shows the open portal and ordinary actors");
            yield return Shot(folder,"02-abyss-portal");
            yield return RecoveryWait(()=>abyssIntroTime>=4.6f&&abyssIntro,"The flood front crosses the map",10);
            Check(abyss.portal.jetRenderer.enabled&&abyss.portal.changed>0&&abyss.portal.changed<abyss.portal.witnesses.Count&&
                abyss.front.enabled,"The outflow reaches actors in sequence");
            yield return Shot(folder,"03-abyss-transformation");
            yield return RecoveryWait(()=>abyssIntroTime>=6.15f&&abyssIntro,"The last guard is reached",10);
            Check(abyss.portal.changed==abyss.portal.witnesses.Count&&abyss.portal.witnesses.Select(actor=>actor.crossedAt).Distinct().Count()==4,
                "Every actor mutates at a distinct wave-contact time");
            Check(abyssSkins!=null&&abyssSkins.Length==8&&abyssSkins.All(texture=>texture)&&abyssSkinMaterials.Count>=4,
                "The original UVs use the new boss-only textures");
            var visualCheck=new[]{"n00J","n00X","n00H","n004","n007","n00S","n01P","n00W"};
            foreach(var raw in visualCheck){
                int kind=Array.FindIndex(match.Lines,line=>line.unit==raw);
                var prefab=kind>=0?RaceModel(1,kind,false):null;
                var view=prefab?Instantiate(prefab,abyss.actors.transform).GetComponent<PcModel>():null;
                Check(view&&ApplyAbyssSkin(view),"Applied the edited atlas to "+raw);
                if(view)Destroy(view.gameObject);
            }
            yield return Action("abyss.skip");
            testHoldAuthority=true;
            Check(!abyssIntro&&FogActive&&abyss.columns==AbyssColumns&&!abyss.front.enabled&&match.Elapsed<.5f,
                "Skip finishes the flood, restores fog and returns control to the builder");
            yield return new WaitForSecondsRealtime(1.55f);
            Check(bossSoundtrack&&bossSoundtrack.isPlaying&&bossSoundtrack.loop&&
                Mathf.Abs(bossSoundtrack.volume-WinterBattleMusicVolume)<.025f,
                "The same track continues looping and fades into the battle background");
            Check(abyss.hudFrame&&abyss.crest,"Bespoke HUD frame and transformed models are available");
            Check(match.CastleHealthLimit(1)==HumanMatch.AbyssCastleHealth&&match.Huts.Count==4,
                "The stronger castle, two defenses and two early producers are ready");
            yield return Shot(folder,"04-abyss-battle");
            match.Elapsed=HumanMatch.AbyssFirstUndertow-3;
            RenderMatch();
            Check(abyss.raid.enabled,"The whirlpool visibly telegraphs the flank raid");
            controls.FocusWorld(World(match.AbyssRaidPoint),40);
            yield return Shot(folder,"05-abyss-whirlpool");
            var hut=new HumanMatch.Hut{id=300001,team=0,kind=match.Kind("h000"),position=HumanMatch.Plot(0,6,15),paid=true,started=true,maxHp=900,hp=600};
            match.Huts.Add(hut);
            builderSelected=selectedCastle=false;
            selectedHut=hut.id;
            RenderMatch();
            controls.FocusWorld(World(hut.position),40);
            yield return Shot(folder,"06-repair-command");
            yield return Action("building.repair");
            Check(match.Builders[0].repairing&&match.Builders[0].repairBuilding==hut.id,"Clicking the repair icon orders work on the selected barracks");
        }
        var humans=JObject.Parse(RaceCatalog(0));
        var naga=JObject.Parse(RaceCatalog(2));
        new HumanMatch(humans,620,2,-1,-1,naga).CheckAbyssRules(AbyssDefinition(),Check);
        new HumanMatch(humans,620,2,-1,-1,naga).CheckBuildingRepair(Check);
        foreach(int race in AllRaceUnity){
            var catalog=JObject.Parse(RaceCatalog(race));
            var sim=new HumanMatch(catalog,620,2,-1,-1,naga);
            sim.BeginChallengeRules(AbyssDefinition());
            sim.CheckAbyssRace(race,Check);
            yield return null;
        }
        MainMenu();
        Check(abyss==null&&!abyssIntro,"Leaving releases the mesh, water, actors and audio");
        File.WriteAllText(Path.Combine(folder,"abyss-checks.json"),new JObject{["checks"]=testChecks,["failures"]=testFailures}.ToString());
        Debug.Log("CF_ABYSS_RESULT failures="+testFailures);
        Application.Quit(testFailures==0?0:1);
    }
}

public sealed partial class HumanMatch {
    internal void CheckAbyssRules(JObject definition,Action<bool,string> check){
        int footman=Kind("h000"),guard=Kind("h00F"),flyer=Kind("h01Y"),siege=Kind("h01B");
        var soldier=Spawn(0,footman,Vector2.zero);
        var weapon=Lines[footman].weapons.First();
        float baseSpeed=MoveSpeed(soldier);
        check(WeaponMissChance(soldier,weapon)==0,"Normal matches have no water miss");
        BeginChallengeRules(definition);
        check(Mathf.Approximately(MoveSpeed(soldier),baseSpeed*.75f)&&Mathf.Approximately(WeaponMissChance(soldier,weapon),.10f),
            "Water slows ground units by 25% and adds exactly 10% miss");
        soldier.position=new Vector2(AbyssWorldEast+1,0);
        check(Mathf.Approximately(MoveSpeed(soldier),baseSpeed)&&WeaponMissChance(soldier,weapon)==0,
            "Units outside the flooded bounds have no penalty");
        soldier.position=Vector2.zero;
        var flying=Spawn(0,flyer,Vector2.zero);
        var boss=Spawn(1,guard,new Vector2(4,0));
        check(!AbyssWaterAffects(flying)&&WeaponMissChance(flying,weapon)==0,"Air units fly above the flood");
        check(AbyssDeepForm(boss)&&!AbyssWaterAffects(boss)&&Mathf.Approximately(AttackSpeed(boss),1.25f)&&
            Mathf.Approximately(boss.maxHp,Lines[boss.kind].hp*1.28f)&&AbyssWeaponDamage(boss)==1.20f,
            "Boss units get reinforced health, weapon damage and haste");
        soldier.hp=soldier.maxHp=5000;
        boss.hp=boss.maxHp*.5f;
        TickAbyssPanzer(boss);
        float remaining=soldier.hp;
        TickAbyssPanzer(boss);
        check(remaining<5000&&soldier.hp==remaining&&boss.effects.Count(e=>e.id=="mythic_abyss_frenzy")==1,
            "Melee shell shatters, damages nearby ground foes and grants haste once");

        soldier.position=BattleCastlePosition(1)+Vector2.left*(CastleCollision+1);
        int firstMiss=ChaosMisses;
        for(int i=0;i<160;i++){BeginAttack(soldier,weapon,-1);Release(soldier);}
        check(ChaosMisses-firstMiss>=8&&ChaosMisses-firstMiss<=25,"Release rolls the water miss in normal melee combat");

        soldier.position=abyssOracle.position+Vector2.left*2;
        var second=Spawn(0,footman,abyssOracle.position+Vector2.left*3);
        var third=Spawn(0,footman,abyssOracle.position+Vector2.left*4);
        abyssOracle.mana=100;
        TickNagaBuilding(abyssOracle,.05f);
        check(AbyssBondCount==2&&soldier.team==1&&second.team==1&&third.team==0,"Oracle takes the two nearest eligible soldiers");
        abyssOracle.hp=0;
        TickAbyssTide();
        check(AbyssBondCount==0&&soldier.team==0&&second.team==0,"Destroying the Oracle returns both soldiers");
        abyssOracle.hp=1000;
        abyssOracle.mana=100;
        abyssOracle.spellTimers.Clear();
        Elapsed=21;
        TickNagaBuilding(abyssOracle,.05f);
        check(AbyssBondCount==2,"The Oracle can use its ability again");
        Elapsed=34;
        TickAbyssTide();
        check(AbyssBondCount==0&&soldier.team==0&&second.team==0,"Surviving soldiers also return when the spell expires");

        var waveTarget=Spawn(0,footman,new Vector2(-16,0));
        waveTarget.hp=waveTarget.maxHp=5000;
        ActiveNagaWaves.Add(new NagaWave{team=1,origin=Vector2.zero,direction=Vector2.left,turnAt=25});
        TickNagaWaves(1);
        float outgoing=waveTarget.hp;
        TickNagaWaves(1);
        check(outgoing<5000&&waveTarget.hp<outgoing&&ActiveNagaWaves.Count==0,"Tide hits on both legs and retires after returning");

        var turtle=Spawn(1,siege,CastlePosition(0)+Vector2.right*(CastleCollision+8));
        TryLaunchAbyssHunter(turtle,-1);
        var hunter=Units.LastOrDefault(AbyssSeaHunter);
        check(hunter!=null&&hunter.hp==320&&WeaponFor(soldier,hunter)!=null,"The siege creature is a real target with 320 HP");
        if(hunter!=null){
            float castle=CastleHp[0];
            TickAbyssHunter(hunter,1);
            TickAbyssHunter(hunter,1);
            check(hunter.hp==0&&CastleHp[0]<castle&&!AbyssSeaHunter(hunter),"An unopposed creature reaches and damages the castle once");
            turtle.spellTimers.Clear();
            TryLaunchAbyssHunter(turtle,-1);
            var intercepted=Units.LastOrDefault(AbyssSeaHunter);
            if(intercepted!=null)DamageUnit(intercepted,10000,0,"siege",false);
            check(intercepted!=null&&intercepted.hp<=0&&!AbyssSeaHunter(intercepted),"Intercepting the creature removes its siege attack");
        }
        Elapsed=AbyssFirstUndertow-4;
        var north=AbyssRaidPoint;
        TickAbyssTide();
        check(AbyssRaidWarning&&AbyssRaids==0,"A whirlpool warns before the first raid");
        Elapsed=AbyssFirstUndertow;
        int spawned=Spawned;
        TickAbyssTide();
        check(AbyssRaids==1&&Spawned==spawned+6&&Mathf.Approximately(AbyssRaidPoint.y+north.y,2*CastlePosition(0).y)&&
            !AbyssRaidWarning&&AbyssRaidIn==AbyssUndertowInterval,"Six units emerge at 5:15, then the next raid switches sides");
        CastleHp[1]=AbyssCastleHealth*.5f;
        TickAbyssTide();
        check(AbyssSecondPhase&&AbyssRaidIn==AbyssRageInterval,"Half castle health makes raids more frequent");
        Elapsed+=AbyssRageInterval;
        spawned=Spawned;
        TickAbyssTide();
        check(Spawned==spawned+7&&Lines[Units.Last().kind].unit=="n01P","Second-phase raids bring an ancient Hydra");

        var siren=Spawn(1,Kind("h01Z"),Vector2.zero);
        var wyvern=Spawn(1,flyer,Vector2.zero);
        var hydra=Spawn(1,Kind("h05F"),Vector2.zero);
        var firstEnemy=Spawn(0,footman,Vector2.right*4);
        var nextEnemy=Spawn(0,footman,Vector2.right*5);
        var lastEnemy=Spawn(0,footman,Vector2.right*6);
        siren.attackTarget=wyvern.attackTarget=hydra.attackTarget=firstEnemy.id;
        AbyssWeaponReleased(siren,Lines[siren.kind].weapons[0]);
        check(Silenced(firstEnemy)&&Silenced(nextEnemy)&&Silenced(lastEnemy),"Siren's lullaby silences nearby opponents");
        int shots=Shots.Count;
        AbyssWeaponReleased(wyvern,Lines[wyvern.kind].weapons[0]);
        AbyssWeaponReleased(hydra,Lines[hydra.kind].weapons[0]);
        AbyssWeaponReleased(hydra,Lines[hydra.kind].weapons[0]);
        check(Shots.Count==shots+3&&Shots.Skip(shots).All(shot=>shot.target!=firstEnemy.id),"The flyer and Hydra fire one and two extra shots on cooldown");
    }

    internal void CheckAbyssRace(int race,Action<bool,string> check){
        int kind=Array.FindIndex(Lines,l=>l.Producer&&teamBuildings[0].Contains(l.building)&&l.speed>0&&
            !((bool?)l.stats["invulnerable"]??false)&&!((bool?)l.stats["flying"]??false));
        if(TeamUltimate[0]&&kind<0)kind=Kind("h00F");
        if(kind<0){check(false,"Race has a ground roster for flood coverage: "+race);return;}
        var unit=Spawn(0,kind,Vector2.zero);
        check(AbyssWaterAffects(unit)&&!AbyssDeepForm(unit),"Player ground roster stays subject to the flood: "+race);
    }

    internal void CheckBuildingRepair(Action<bool,string> check){
        var barracks=PlaceChallengeStructure("h000",Plot(0,6,15),100);
        var tower=PlaceChallengeStructure("h00N",Plot(0,6,17),100);
        barracks.team=tower.team=0;
        var builder=Builders[0];
        check(!RepairBuilding(0,barracks.id),"Full structures have no repair order");
        barracks.hp=85;
        tower.hp=75;
        check(!RepairBuilding(1,barracks.id)&&RepairBuilding(0,barracks.id),"Only the owner can order building repair");
        builder.position=barracks.position+new Vector2(0,-Mathf.Sign(barracks.position.y)*2.4f);
        TickBuilder(builder,1);
        check(barracks.hp==100&&tower.hp==75&&!builder.repairing,"Repair restores 15 HP/s and stops at full health");
        check(RepairBuilding(0,tower.id),"A damaged tower can be repaired just like a barracks");
        builder.position=tower.position+new Vector2(0,-Mathf.Sign(tower.position.y)*2.4f);
        TickBuilder(builder,1);
        check(tower.hp==90&&builder.repairing,"The worker keeps repairing the tower over time");
        check(MoveBuilder(0,builder.position)&&!builder.repairing,"Moving the worker cancels repair");
        check(RepairBuilding(0,tower.id),"Repair can be resumed after moving");
        tower.hp=0;
        TickBuilder(builder,1);
        check(!builder.repairing&&!RepairBuilding(0,tower.id)&&tower.hp==0,"Destroyed structures never restart repair");
    }
}
}
