using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseLastProtocol(){
        var folder=BattleLab.Arg("--pc-results",Application.persistentDataPath);
        bool perf=BattleLab.Arg("--pc-protocol-perf")=="1";
        Directory.CreateDirectory(folder);
        OpenProgression(false);
        yield return Action("progress.mythic");
        yield return Action("challenge."+HumanMatch.ProtocolTrial);
        Check(selectedMythicId==HumanMatch.ProtocolTrial,"A separate leaderboard is selected");
        yield return Action("mythic.play."+HumanMatch.ProtocolTrial);
        yield return Action("mythic.race.0");
        yield return Action("protocol.aspect");
        yield return Shot(folder,"01-race-picker");
        Check(protocolPainting&&protocolPainting.width>=1280,"Dedicated battle key art is loaded from the player");
        yield return Action("mythic.begin");
        yield return RecoveryWait(()=>protocolIntro&&!versusActive,"VS loads the boss and routes to the intro",50);
        if(match!=null&&match.LastProtocol){
            Check(protocolActor&&protocolWires.All(w=>w)&&protocolCircles.Count==9&&!FogActive,"Intro shows the boss, powered wires and mine positions");
            Check(protocolReskinnedMaterials>0&&protocolArmorAtlas&&protocolChassisAtlas,"Both custom Terminator atlases bind to the cinematic model");
            Check(bossSoundtrack&&bossSoundtrack.clip&&bossSoundtrack.clip.name==ProtocolBossTrack&&bossSoundtrack.clip.length>89&&
                bossSoundtrack.isPlaying&&bossSoundtrack.loop&&bossSoundtrack.volume>.99f,"Krang’s Battle starts at cinematic volume and loops");
            var introFrames=new List<float>();
            if(perf){Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;}
            while(protocolIntroAge<5&&protocolIntro){
                if(perf&&protocolIntroAge>=.7f)introFrames.Add(Time.unscaledDeltaTime*1000);
                yield return null;
            }
            Check(protocolGroundMesh&&protocolGroundProgress>0&&protocolGroundProgress<1,"The floor spreads across the field during the intro");
            yield return Shot(folder,"02-cinematic");
            yield return Action("protocol.skip");
            Check(!protocolIntro&&FogActive&&match.Elapsed<.5f&&match.Aspects[0]==0,"Returning to the builder restores fog and applies the chosen aspect");
            Check(protocolGroundProgress==1&&protocolGroundTint.All(color=>color.a==255),"Skipping completes the floor transition");
            Check(protocolHudFrame&&protocolHudLight,"The Technodrome HUD loads the bespoke frame and station lights");
            testHoldAuthority=true;
            yield return Shot(folder,"03-battle-hud");
            yield return new WaitForSecondsRealtime(1.65f);
            Check(bossSoundtrack&&bossSoundtrack.isPlaying&&Mathf.Abs(bossSoundtrack.volume-ProtocolBattleMusicVolume)<.025f,
                "The soundtrack continues at reduced volume when control returns");
            match.ProtocolVisualProbe(false);
            match.Builders[0].position=match.Builders[0].previous=new Vector2(135,10);
            controls.FocusWorld(World(new Vector2(136,0)),50);
            UpdateFog(true);
            RenderProtocol();
            Check(protocolRail.enabled&&!protocolBeam.enabled,"The amber warning precedes the electric discharge");
            yield return Shot(folder,"04-warning");
            match.ProtocolVisualProbe(true);
            RenderProtocol();
            Check(protocolBeam.enabled&&protocolArcs.All(arc=>arc.enabled),"The cannon shows its bright core and branching arcs");
            yield return Shot(folder,"05-cannon");
            if(perf)yield return BenchmarkLastProtocol(folder,introFrames);
        }
        var bot=JObject.Parse(RaceCatalog(6));
        for(int race=0;race<AllRaceNames.Length;race++){
            Check(RaceStaged(race),"Race available for the trial "+race);
            if(!RaceStaged(race))continue;
            var catalog=JObject.Parse(RaceCatalog(race));
            int count=race==12?3:Mathf.Max(1,AspectCount(race));
            for(int variant=0;variant<count;variant++){
                var chosen=catalog;
                if(race==12){
                    var random=new System.Random(2026+variant);
                    var draw=new UltimateLoadout((JArray)catalog["ultimateSlots"],n=>random.Next(n));
                    chosen=draw.Playable(catalog,draw.Draw());
                }
                var sim=new HumanMatch(chosen,600,2,count>1&&race!=12?variant:-1,-1,bot);
                sim.BeginChallengeRules(ProtocolDefinition());
                sim.CheckProtocolRace(race,variant,Check);
            }
            yield return null;
        }
        foreach(int race in new[]{0,2,7,9,15}){
            var sim=new HumanMatch(JObject.Parse(RaceCatalog(race)),600,2,-1,-1,bot);
            sim.BeginChallengeRules(ProtocolDefinition());
            sim.CheckProtocolExceptions(race,Check);
        }
        MainMenu();Check(!protocolProps&&!protocolIntro&&!protocolGroundMesh&&!protocolArmorAtlas,"Leaving cleans up the effects, terrain and atlas overrides");
        File.WriteAllText(Path.Combine(folder,"last-protocol-checks.json"),new JObject{["checks"]=testChecks,["failures"]=testFailures}.ToString());
        Debug.Log("CF_PROTOCOL_RESULT failures="+testFailures);
        Application.Quit(testFailures==0?0:1);
    }

    IEnumerator BenchmarkLastProtocol(string folder,List<float> introFrames){
        var samples=new JObject{["resolution"]=Screen.width+"x"+Screen.height,["graphics"]=SystemInfo.graphicsDeviceName,
            ["processor"]=SystemInfo.processorType,["intro"]=ProtocolFrameStats(introFrames)};
        var kinds=new[]{match.Kind("h000"),match.Kind("h05T")};
        for(int team=0;team<2;team++)for(int i=0;i<56;i++){
            var position=new Vector2(130+(team==0?-1:1)*(2+i/14*2.7f),((i%14)-6.5f)*1.65f);
            match.Spawn(team,kinds[team],position);
        }
        RenderMatch();
        samples["units"]=112;
        var cases=new JArray();
        foreach(bool enabled in new[]{true,false,false,true}){
            protocolProps.SetActive(enabled);
            yield return new WaitForSecondsRealtime(.6f);
            var frames=new List<float>();
            yield return CollectProtocolFrames(frames,1.8f);
            var sample=ProtocolFrameStats(frames);
            sample["protocol_vfx"]=enabled;
            cases.Add(sample);
        }
        samples["frozen_frame_comparison"]=cases;
        protocolProps.SetActive(true);
        testHoldAuthority=false;
        yield return new WaitForSecondsRealtime(.4f);
        var battle=new List<float>();
        yield return CollectProtocolFrames(battle,3);
        samples["battle_with_simulation"]=ProtocolFrameStats(battle);
        File.WriteAllText(Path.Combine(folder,"last-protocol-fps.json"),samples.ToString());
        testHoldAuthority=true;
    }

    static IEnumerator CollectProtocolFrames(List<float> frames,float seconds){
        float until=Time.realtimeSinceStartup+seconds;
        while(Time.realtimeSinceStartup<until){
            yield return null;
            frames.Add(Time.unscaledDeltaTime*1000);
        }
    }

    static JObject ProtocolFrameStats(List<float> frames){
        frames.Sort();
        float P(float n)=>frames.Count==0?0:frames[Mathf.Clamp(Mathf.CeilToInt(frames.Count*n)-1,0,frames.Count-1)];
        return new JObject{["frames"]=frames.Count,["p50_ms"]=P(.5f),["p95_ms"]=P(.95f),
            ["mean_fps"]=frames.Count==0?0:1000/frames.Average()};
    }
}

public sealed partial class HumanMatch {
    internal void ProtocolVisualProbe(bool discharge){
        if(discharge){
            Elapsed+=ProtocolWarning+.05f;
            TickLastProtocol();
        }else{
            foreach(var station in protocolStations)DamageHut(station,100000,"siege",0);
            DamageCastle(1,100000,"siege");
            Step(.05f);
        }
    }

    internal void CheckProtocolRace(int race,int variant,Action<bool,string> check){
        string label="Race "+race+" / "+variant+" · ";
        PlayerVersusPlayer=true;
        var candidates=BaseKinds.Where(i=>Lines[i].Producer&&Lines[i].weapons.Any(w=>w.structure)).ToArray();
        check(candidates.Length>0&&ProtocolStationsUp==2&&Huts.Count(h=>Lines[h.kind].building=="h05L")==2,label+"siege options and both road stations");
        if(candidates.Length==0)return;
        var ranged=candidates.OrderByDescending(i=>Lines[i].range).First();
        var siege=Spawn(0,ranged,CastlePosition(1)+Vector2.left*3);
        var weapon=WeaponFor(siege,null,true);
        float maximum=CastleHp[1],gate=maximum*.4f;
        var yard=protocolStations[0];
        var from=yard.position-Vector2.right*12;
        var meleeKind=candidates.OrderBy(i=>Lines[i].range).First();
        var front=Spawn(0,meleeKind,from);
        TickUnit(front,.05f);
        check(front.combatTarget==HutTarget(yard)||front.castLeft>=0,label+"normal orders acquire the first station");
        float expected=Multiplier(weapon.attack,CastleArmorType)*ArmorFactor(CastleArmorFor(1))*.30f*100;
        DamageCastle(1,100,weapon.attack);
        check(Mathf.Abs(maximum-CastleHp[1]-expected)<.02f,label+"both stations apply ordinary damage reduction");
        var roster=Enumerable.Range(0,Lines.Length).Where(i=>PlayerHasLine(i)&&
            Lines[i].Producer&&Lines[i].weapons.Any(w=>w.structure)).ToArray();
        foreach(int kind in roster){
            var attacker=Spawn(0,kind,siege.position);
            foreach(var attack in Lines[kind].weapons.Where(w=>w.structure))
                ResolveAttack(attacker.id,0,-1,100000,attack,0,true);
        }
        check(CastleHp[1]==gate&&!ProtocolSecondStage,label+"the full available siege roster stops at the protected phase");
        ResolveAttack(siege.id,0,-1,100000,weapon,0,false);
        ApplyCastleDamage(1,100000);
        check(CastleHp[1]==gate&&!ProtocolSecondStage,label+"weapon and bonus damage cannot skip stations");
        check(((string)Lines[yard.kind].buildingStats["unit_types"]).Contains("Mechanical"),label+"station is a mechanical structure");
        DamageHut(yard,100000,"siege",0);
        check(ProtocolStationsUp==1&&!ProtocolSecondStage&&!RepairCastle(1),label+"destroyed station stays offline and boss cannot reset the HP phase");
        DamageHut(protocolStations[1],100000,"siege",0);
        TickLastProtocol();
        Elapsed+=2;
        var shot=AddShot(0,siege.kind,siege.id,-1,siege.position,CastlePosition(1),100000,weapon,0,3);
        TickShots(shot.duration+.05f);
        check(ProtocolSecondStage&&ProtocolWarningActive&&CastleHp[1]==gate,label+"phase transition survives in-flight high damage");
        var inside=Spawn(0,meleeKind,new Vector2(ProtocolBeamStart+4,0));
        var outside=Spawn(0,meleeKind,inside.position+Vector2.up*(ProtocolBeamHalfWidth+2));
        float insideHp=inside.hp,outsideHp=outside.hp;
        Elapsed+=1.02f;
        TickLastProtocol();
        check(ProtocolGatesOpen&&inside.hp<insideHp&&outside.hp==outsideHp,label+"cannon hits marked strip and opens a timed window");
        float before=CastleHp[1];
        DamageCastle(1,100,weapon.attack);
        float opened=before-CastleHp[1];
        Elapsed+=ProtocolVulnerable+.05f;
        before=CastleHp[1];DamageCastle(1,100,weapon.attack);
        check(!ProtocolGatesOpen&&opened>before-CastleHp[1],label+"window improves siege damage and closes again");

        var mine=ProtocolMines.First(m=>!m.spent&&m.detonatesAt<0);
        var bait=Spawn(0,meleeKind,mine.position);
        bool air=Flying(bait);
        float baitHp=bait.hp;
        Elapsed+=.2f;TickLastProtocol();
        Elapsed+=.4f;TickLastProtocol();
        check(air?!mine.spent&&bait.hp==baitHp:mine.spent&&bait.hp<baitHp,label+"mines distinguish ground from airborne targets");
    }

    internal void CheckProtocolExceptions(int race,Action<bool,string> check){
        PlayerVersusPlayer=true;
        var station=protocolStations[0];
        switch(race){
            case 2:
                var naga=Spawn(0,BaseKinds.First(i=>Lines[i].Producer),ProtocolStationPoint(1));
                naga.spellBorn=true;
                var w=WeaponFor(naga,null,true);
                check(w!=null,"Spawned Naga behind the stations can contribute to the siege");
                if(w!=null)ResolveAttack(naga.id,0,-1,100000,w,0,true);
                check(Mathf.Abs(CastleHp[1]-CastleHealthLimit(1)*.4f)<.01f&&!ProtocolSecondStage,
                    "A unit appearing behind both stations cannot skip the protection · HP "+CastleHp[1]);
                break;
            case 7:
                var undead=Spawn(0,Array.FindIndex(Lines,l=>l.unit=="u00D"),station.position);
                float hp=station.hp;
                UndeadStructureHit(undead,HutTarget(station),0);
                check(station.hp==hp&&ProtocolStationsUp==2,"Undead building destruction cannot one-shot a mechanical station");
                break;
            case 9:
                var elemental=Spawn(0,Array.FindIndex(Lines,l=>Strings(l.stats["abilities"]).Contains("A0DW")),station.position);
                ApplyCastleDamage(1,float.MaxValue);
                ElementalStructureHit(elemental,-1,20);
                check(Mathf.Abs(CastleHp[1]-CastleHealthLimit(1)*.4f)<.01f,
                    "Elemental demolition applies the boss floor through the common health path · HP "+CastleHp[1]);
                break;
            case 0:
            case 15:
                int kind=BaseKinds.Where(i=>Lines[i].Producer&&Lines[i].weapons.Any(w=>w.structure)&&
                    !UnitConfig035(i).flying).OrderBy(i=>Lines[i].range).First();
                for(int i=0;i<48;i++)Spawn(0,kind,station.position+new Vector2(-18-i%8,(i%13-6)*1.3f));
                for(int step=0;step<520&&station.hp>0;step++)Step(.05f);
                check(station.hp<=0,"Ground formation clears the station with 48 infantry, race "+race);
                break;
        }
    }
}
}
