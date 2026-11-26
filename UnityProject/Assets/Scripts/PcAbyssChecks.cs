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
        if(mythicBoardLoading)yield return RecoveryWait(()=>!mythicBoardLoading,"The test leaderboard responds",12);
        mythicLeaderboard=new JArray(new JObject{["place"]=1,["nickname"]="Первый игрок",["race"]=2,["time_ms"]=543000},
            new JObject{["place"]=2,["nickname"]="Второй игрок",["race"]=0,["time_ms"]=585000});
        yield return Shot(folder,"00-leaderboard-crown");
        Check(mythicCrown&&mythicCrown.width==48,"A first-place crown is drawn in the standings");
        mythicLeaderboard=new JArray();
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
            Check(match.CastleHealthLimit(1)==HumanMatch.AbyssCastleHealth&&match.Huts.Count==0&&match.Units.Count==0,
                "The boss opens with an empty base and no army");
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
            yield return CheckPortraitCamera(hut);
        }
        var humans=JObject.Parse(RaceCatalog(0));
        var naga=JObject.Parse(RaceCatalog(2));
        new HumanMatch(humans,620,2,-1,-1,naga).CheckAbyssRules(AbyssDefinition(),Check);
        new HumanMatch(humans,620,2,-1,-1,naga).CheckBuildingRepair(Check);
        var ordinary=new HumanMatch((JObject)humans.DeepClone(),620,2,-1,-1,(JObject)naga.DeepClone());
        var abyssOpening=new HumanMatch((JObject)humans.DeepClone(),620,2,-1,-1,(JObject)naga.DeepClone());
        abyssOpening.BeginChallengeRules(AbyssDefinition());
        abyssOpening.CheckAbyssOpening(ordinary,Check);
        var undead=JObject.Parse(RaceCatalog(7));
        var plague=new HumanMatch(undead,620,2,-1,-1,(JObject)humans.DeepClone());
        foreach(string id in new[]{"A01L","A05P","A05T"}){
            var cfg=plague.Abilities[id];
            Check((string)cfg["name"]=="Чума"&&(string)cfg["profile"]["display"]=="Чума"&&
                ((string)cfg["icon"]).EndsWith("PASBTNPlagueCloud.blp")&&
                RaceSkillTip(id,cfg).StartsWith("Чума\n"),"Plague name, tooltip and icon source: "+id);
        }
        var elemental=JObject.Parse(RaceCatalog(9));
        new HumanMatch(elemental,620,2,-1,-1,(JObject)humans.DeepClone()).CheckLightningRange(Check);
        new HumanMatch((JObject)elemental.DeepClone(),620,2,-1,-1,(JObject)humans.DeepClone()).CheckLavaSplit(Check);
        foreach(int race in AllRaceUnity){
            var catalog=JObject.Parse(RaceCatalog(race));
            var sim=new HumanMatch(catalog,620,2,-1,-1,naga);
            sim.BeginChallengeRules(AbyssDefinition());
            sim.CheckAbyssRace(race,Check);
            yield return null;
        }
        MainMenu();
        Check(abyss==null&&!abyssIntro,"Leaving releases the mesh, water, actors and audio");
        yield return CheckChallengeProfile(folder);
        File.WriteAllText(Path.Combine(folder,"abyss-checks.json"),new JObject{["checks"]=testChecks,["failures"]=testFailures}.ToString());
        Debug.Log("CF_ABYSS_RESULT failures="+testFailures);
        Application.Quit(testFailures==0?0:1);
    }

    IEnumerator CheckChallengeProfile(string folder){
        yield return Action("main.profile");
        Check(page==Page.Profile&&ProfileChallenges.Length==47,"The main menu opens a guest profile with all 47 challenges");
        progression.Complete("five_pillars",562);
        progression.Complete(HumanMatch.AbyssTrial,788);
        Check(IsProfileChallengeComplete("five_pillars")&&IsProfileChallengeComplete(HumanMatch.AbyssTrial),
            "Existing local ordinary and mythic wins both unlock a medal");
        yield return Shot(folder,"07-profile-guest");
        yield return Action("account-profile.all");
        yield return Action("account-profile.page.next");
        Check(showAllProfileChallenges&&profileMedalPage==1,"The full challenge collection pages without a scroll gesture");
        yield return Action("account-profile.earned");
        directoryReady=true;
        yield return Action("account-profile.login");
        Check(showNetworkProfile,"Sign-in opens from the main-menu profile");
        yield return Action("profile.close");
        if(!string.IsNullOrEmpty(MythicLeaderboardUrl())){
            networkProfile=new JObject{["id"]="tester",["name"]="Профиль проверки",["wins"]=11};
            networkSessionToken="qa-tester";networkSessionEndpoint=NetworkAccountStore.UnityEndpoint;
            yield return Action("account-profile.sync");
            yield return RecoveryWait(()=>!medalSyncBusy,"Profile achievement publication completes",12);
            Check(IsPublishedMedal("five_pillars")&&IsPublishedMedal(HumanMatch.AbyssTrial),"The local collection is published to the account");
            ShareChallengeVictory("siege_prism");
            yield return RecoveryWait(()=>!medalSyncBusy,"New signed-in wins publish automatically",12);
            Check(IsPublishedMedal("siege_prism")&&!progression.Completed("siege_prism"),"Account progress from the service displays independently of the local save");
            yield return Shot(folder,"08-profile-account");
            page=Page.Network;socialTab=1;socialSupported=true;socialViewed="visitor";
            socialPlayer=new JObject{["id"]="visitor",["name"]="Другой игрок"};
            yield return Action("player.medals");
            yield return RecoveryWait(()=>!peerMedalsLoading,"Another player's medals load through the public endpoint",12);
            Check(!peerMedalsError&&medalPeer=="visitor"&&peerMedals.Contains("siege_lanterns")&&!peerMedals.Contains("five_pillars"),
                "Other profiles show their own public medals only");
            yield return Shot(folder,"09-profile-peer");
            StopDirectory();networkSessionToken="";networkSessionEndpoint="";ProfileAuthChanged();
        }
        MainMenu();
    }

    IEnumerator CheckPortraitCamera(HumanMatch.Hut hut){
        var offset=new Vector2(18,-17);
        bool Centered(Vector2 target){
            var camera=Camera.main;
            var forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
            var position=World(target);
            var relative=Vector3.ProjectOnPlane(position-camera.transform.position,Vector3.up).normalized;
            return Vector3.Dot(forward,relative)>.999f;
        }
        void StartAway(Vector2 target)=>controls.FocusWorld(World(target+offset),38);
        StartAway(hut.position);
        yield return Action("portrait.focus");
        Check(Centered(hut.position)&&selectedHut==hut.id,"The building portrait centers on that building");
        var builder=match.Builders[0];
        FocusBuilder();
        StartAway(builder.position);
        yield return Action("portrait.focus");
        Check(Centered(builder.position)&&builderSelected,"The builder portrait centers on the builder");
        SelectCastle(false);
        StartAway(match.BattleCastlePosition(0));
        yield return Action("portrait.focus");
        Check(Centered(match.BattleCastlePosition(0))&&selectedCastle,"The castle portrait centers on the keep");
        var first=match.Spawn(0,match.Kind("h000"),builder.position+new Vector2(2,2));
        var second=match.Spawn(0,match.Kind("h000"),builder.position+new Vector2(15,5));
        selectedCastle=false;selectedUnit=first.id;selectedHut=-1;
        StartAway(first.position);
        yield return Action("portrait.focus");
        Check(Centered(first.position)&&selectedUnit==first.id,"The unit portrait centers on the selected fighter");
        selectedGroup.Add(first.id);selectedGroup.Add(second.id);
        StartAway(second.position);
        yield return Action("group.unit."+second.id);
        Check(Centered(second.position)&&selectedUnit==second.id&&selectedGroup.Count==2,
            "A group portrait centers on its own fighter while keeping the group");
        ClearGroupSelection();
    }
}

public sealed partial class HumanMatch {
    internal void CheckAbyssOpening(HumanMatch ordinary,Action<bool,string> check){
        botRandom=ordinary.botRandom=0x260B97;
        for(int i=0;i<700;i++){ordinary.Step(.05f);Step(.05f);}
        var normalFirst=ordinary.Huts.FirstOrDefault(hut=>hut.team==1&&hut.paid);
        var bossFirst=Huts.FirstOrDefault(hut=>hut.team==1&&hut.paid);
        check(normalFirst!=null&&bossFirst!=null&&ordinary.Lines[normalFirst.kind].building==Lines[bossFirst.kind].building&&
            Mathf.Approximately(ordinary.Interval(normalFirst),Interval(bossFirst))&&AbyssRaids==0,
            "The first bot building and production interval match a normal Naga game");
    }

    internal void CheckLavaSplit(Action<bool,string> check){
        int shrine=Kind("h045"),flame=Kind("h046"),prime=Kind("h04E");
        check(Lines[shrine].upgrades.SequenceEqual(new[]{"h046"})&&Lines[flame].upgrades.SequenceEqual(new[]{"h04E"})&&
            Lines[prime].upgrades.Length==0&&Lines[prime].unit=="h03V","Native fire line has exactly two upgrades");
        var altar=new Hut{team=0,kind=flame,position=new Vector2(-125,10),hp=1000,maxHp=1000,paid=true,started=true};
        Produce(altar);
        var elder=Units[0];
        check(Lines[elder.kind].unit=="h030"&&elder.elementalLavaGeneration==1,"Second altar production creates a charged-capable elder");
        var enemy=Spawn(1,Kind("h000"),elder.position+Vector2.right*4);
        enemy.maxHp=enemy.hp=100000;
        var weapon=Lines[elder.kind].weapons[0];
        void Fire(){
            BeginAttack(elder,weapon,enemy.id);
            Release(elder);
        }
        Fire();
        check(Shots.Count==1&&elder.elementalLavaHits==0,"Releasing a projectile does not charge the split yet");
        enemy.team=0;TickShots(1);enemy.team=1;
        check(elder.elementalLavaHits==0&&enemy.hp==enemy.maxHp,"A projectile that cannot damage its target adds no charge");
        for(int hit=0;hit<14;hit++){Fire();TickShots(1);}
        check(elder.elementalLavaHits==14&&ElementalJobs.Count==0,"Fourteen actual missile impacts charge the elder but do not split");
        Fire();TickShots(1);elder.hp=300;
        check(elder.elementalLavaPending&&ElementalJobs.Count==1,"The fifteenth damaging missile queues one split");
        TickElemental(1.95f);
        check(Units.Count==2,"The new fighter waits the native two seconds");
        TickElemental(.05f);
        var child=Units.Last();
        check(ElementalSplits==1&&elder.hp==600&&elder.elementalLavaHits==0&&Lines[child.kind].unit=="h02Z"&&
            child.hp==child.maxHp&&child.spellBorn&&child.elementalLavaGeneration==0&&child.elementalPose=="Birth",
            "A real missile split heals the parent and produces a full-health nonsplitting fighter with a birth animation");
        for(int hit=0;hit<31;hit++){Fire();TickShots(1);if(elder.elementalLavaPending)TickElemental(2);}
        check(ElementalSplits==2&&elder.elementalLavaGeneration==LavaFinalGeneration,"Weapon impacts cannot create a third child");
        var direct=Spawn(0,elder.kind,elder.position);
        check(direct.elementalLavaGeneration==1,"Directly spawned elders retain the passive");
    }

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
        check(AbyssDeepForm(boss)&&!AbyssWaterAffects(boss)&&Mathf.Approximately(AttackSpeed(boss),1.15f)&&
            Mathf.Approximately(boss.maxHp,Lines[boss.kind].hp*1.15f)&&AbyssWeaponDamage(boss)==1.10f,
            "Boss units retain smaller bonuses to health, weapon damage and haste");
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

        var abyssOracle=PlaceChallengeStructure("h00M",CastlePosition(1)+new Vector2(-19,12),1500);
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
        check(AbyssRaids==1&&Spawned==spawned+4&&Mathf.Approximately(AbyssRaidPoint.y+north.y,2*CastlePosition(0).y)&&
            !AbyssRaidWarning&&AbyssRaidIn==AbyssUndertowInterval,"Four units emerge at 6:30, then the next raid switches sides");
        CastleHp[1]=AbyssCastleHealth*.5f;
        TickAbyssTide();
        check(AbyssSecondPhase&&AbyssRaidIn==AbyssUndertowInterval&&AbyssRaids==1,"Half health keeps the scheduled raid time");
        Elapsed+=AbyssUndertowInterval;
        spawned=Spawned;
        TickAbyssTide();
        check(Spawned==spawned+5&&Lines[Units.Last().kind].unit=="n01P"&&AbyssRaidIn==AbyssRageInterval,
            "Second-phase raids bring an ancient Hydra and use the shorter interval");

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

    internal void CheckLightningRange(Action<bool,string> check){
        int lesser=Kind("h042"),greater=Kind("h044"),human=Kind("h000");
        var razor=Spawn(0,lesser,Vector2.zero);
        var stronger=Spawn(0,greater,Vector2.left*30);
        var target=Spawn(1,human,Vector2.right*11);
        var justInside=Spawn(1,human,target.position+Vector2.up*(124.9f/30));
        var justOutside=Spawn(1,human,target.position+Vector2.up*(125.1f/30));
        check(Mathf.Approximately(Lines[lesser].weapons[0].range*30,350)&&Mathf.Approximately(Lines[greater].weapons[0].range*30,400),
            "The two Razor ranks have native weapon ranges of 350 and 400");
        float primary=target.hp,inside=justInside.hp,outside=justOutside.hp;
        ElementalOrb(razor,target,Abilities["A0CJ"]);
        check(target.hp<primary&&justInside.hp<inside&&justOutside.hp==outside,
            "Razor's additional strike stays within 125 units of the primary target");
        justInside.hp=0;
        ElementalOrb(razor,target,Abilities["A0CJ"]);
        check(justOutside.hp==outside,"An empty jump slot cannot select a target past the radius");
        var attacker=Spawn(1,human,stronger.position+Vector2.up*(600.1f/30));
        attacker.pendingWeapon=Lines[human].weapons[0];
        int shotCount=Shots.Count;
        check(!ElementalRetaliate(attacker,stronger,0)&&Shots.Count==shotCount,"Retaliation cannot reach a target beyond 600");
        attacker.position=stronger.position+Vector2.up*20;
        check(ElementalRetaliate(attacker,stronger,0)&&Shots.Count==shotCount+1,"Retaliation reaches the 600 boundary");
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
