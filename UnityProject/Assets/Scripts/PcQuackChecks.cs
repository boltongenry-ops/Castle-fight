using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    float? quackCaptureTime;

    IEnumerator ExerciseQuackalypse(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"quackalypse-validation"));
        Directory.CreateDirectory(folder);
        MainMenu();
        Check(!GetComponents<AudioSource>().Any(source=>source.isPlaying&&source.loop),
            "The main screen starts without a background track");
        yield return Action("main.challenges");
        yield return Action("progress.mythic");
        yield return Action("mythic.play."+HumanMatch.QuackTrial);
        Check(MythicRacePicker&&selectedMythicId==HumanMatch.QuackTrial,"Sixth mythic challenge has a separate menu entry");
        mythicRace=0;
        yield return Shot(folder,"01-boss-card");
        yield return Action("mythic.begin");
        testHoldAuthority=true;
        float readyUntil=Time.realtimeSinceStartup+55;
        while(versusActive&&Time.realtimeSinceStartup<readyUntil)yield return null;
        if(versusActive||match==null){
            Debug.LogError("The Quackalypse versus screen did not finish preparing the match");
            Application.Quit(1);
            yield break;
        }
        string captureFolder=BattleLab.Arg("--pc-quack-reel");
        if(captureFolder.Length>0){
            quackSceneAge=QuackOpeningEnd;
            yield return null;
            Directory.CreateDirectory(captureFolder);
            for(int frame=0;frame<320;frame++){
                if(frame==13){
                    match.Elapsed=227;
                    match.DebugQuackHitCastle();
                    Tick(.05f);
                }
                if(frame>=13)quackCaptureTime=(frame-13)/30f;
                yield return null;
                yield return new WaitForEndOfFrame();
                var shot=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(captureFolder,frame.ToString("D4")+".jpg"),shot.EncodeToJPG(90));
                Destroy(shot);
            }
            quackCaptureTime=null;
            Application.Quit();
            yield break;
        }
        Check(match?.QuackActive==true&&matchBotRace==PepeContent.Race,"The challenge starts with the player's standard race against boss Pepe");
        Check(match.Gold[0]==500&&match.Gold[1]==500&&match.Wood[0]==match.Wood[1]&&match.Huts.Count==0&&match.Units.Count==0,
            "Both sides start with 500 gold, equal wood and no army or structures");
        if(BattleLab.Arg("--pc-quack-perf")=="1"){
            yield return BenchmarkQuackalypse(folder);
            Application.Quit();
            yield break;
        }
        Check(quackKing&&quackGuards.Count(guard=>guard)==3&&quackEmoji&&quackReveal,"Opening has the Great Quack, guards and both generated textures");
        Check(!greatQuackTemplate&&quackKing&&quackKing.clips.Length>0,
            "Epochal opening uses the original Great Quack presentation");
        Check(quackCroak&&quackCroak.frequency==24000&&quackCroak.length>.5f,"Speech has the recorded croak");
        Check(quackHudFrame&&quackHudFrame.width>quackHudFrame.height*3,"The frog-medallion banner is loaded");
        Check(bossSoundtrack&&bossSoundtrack.isPlaying&&bossSoundtrack.loop&&bossSoundtrack.clip.name==EpochalBossTrack&&
            Mathf.Approximately(bossSoundtrack.volume,1),"Frozen Final Stand loops at full volume during the opening");
        quackSceneAge=4.3f;
        yield return null;
        yield return Shot(folder,"02-opening-orders");
        quackSceneAge=6.1f;
        yield return null;
        yield return Shot(folder,"03a-face-start");
        quackSceneAge=6.65f;
        yield return null;
        Check(quackScene==QuackScene.Opening&&quackFacePose&&Camera.main.projectionMatrix[1,2]==0,
            "The full-face close-up uses a held pose and a centered projection");
        yield return Shot(folder,"03-king-close");
        quackSceneAge=6.95f;
        yield return null;
        yield return Shot(folder,"03c-face-end");
        var camera=Camera.main;
        FrameQuackOpening(QuackFaceEnd-.02f);
        var closePosition=camera.transform.position;
        FrameQuackOpening(QuackFaceEnd+.02f);
        Check(Vector3.Distance(closePosition,camera.transform.position)<.1f,"The full-face shot pulls back without a cut");
        quackSceneAge=QuackSwampCue+.15f;
        yield return null;
        yield return Shot(folder,"03b-swamp-transition");
        Check(quackSwampRevealed&&quackGround.Count>0&&quackWetland&&quackWetland.activeSelf&&quackWetlandMesh.vertexCount>0,
            "The camera return reveals the swamp textures and the combined puddle and lily mesh");
        Check(!match.QuackDownpour&&quackDrizzle&&quackDownpour&&quackDrizzle!=quackDownpour,"The swamp opens with drizzle and has a separate downpour texture");
        quackSceneAge=QuackFlyoverStart+1.45f;
        yield return null;
        yield return Shot(folder,"03d-flyover");
        FrameQuackOpening(QuackOpeningEnd);
        var landing=camera.transform.position;
        var landingProjection=camera.projectionMatrix;
        var soundtrack=bossSoundtrack;
        float songTime=soundtrack?soundtrack.time:0;
        quackSceneAge=QuackOpeningEnd;
        yield return null;
        yield return null;
        Check(quackScene==QuackScene.None&&builderSelected&&Vector3.Distance(landing,camera.transform.position)<.01f&&
            landingProjection.Equals(camera.projectionMatrix),"The flyover lands at the exact gameplay camera and projection");
        yield return new WaitForSecondsRealtime(1.55f);
        Check(bossSoundtrack&&bossSoundtrack==soundtrack&&soundtrack.isPlaying&&soundtrack.time>songTime&&
            Mathf.Approximately(soundtrack.volume,WinterBattleMusicVolume),"The winter track continues under gameplay at reduced volume");
        yield return Shot(folder,"03e-builder-handoff");
        var humans=JObject.Parse(RaceCatalog(0));
        var pepe=JObject.Parse(RaceCatalog(PepeContent.Race));
        new HumanMatch(humans,500,2,-1,-1,pepe).CheckQuackRules(QuackDefinition(),Check);
        new HumanMatch(humans,500,2,-1,-1,pepe).CheckQuackRules(ExpertDefinition(QuackDefinition()),Check);
        new HumanMatch(humans,500,2,-1,-1,pepe).CheckQuackReplacementRate(QuackDefinition(),Check);
        var strategy=new HumanMatch(humans,500,2,-1,-1,pepe);
        strategy.BeginChallengeRules(QuackDefinition());
        strategy.CheckQuackOpeningEconomy(Check);
        while(strategy.Elapsed<90&&!strategy.Finished)strategy.Step(.15f);
        var roster=strategy.Huts.Where(h=>h.team==1).Select(h=>strategy.Lines[h.kind].building).ToArray();
        Check(roster.Any(id=>id=="pB31"||id=="pB12"||id=="pB13")&&roster.Any(id=>id=="pB32"||id=="pB14")&&roster.Any(id=>id=="pB05"||id=="pB18"),
            "A natural 90-second opening builds frontline, ranged support and a healer");
        match.DebugQuackProducer(0,out var producer);
        match.DebugQuackInfiltrate(producer);
        yield return null;
        Check(quackEmojiSeen==1,"The encounter shows a smile on the replacement event");
        yield return Shot(folder,"04-sabotage-emoji");
        match.DebugQuackHitCastle();
        Tick(.05f);
        Check(quackScene==QuackScene.Reversal&&match.QuackRitualPending&&!match.Finished&&!mythicGuestPrompt,
            "First lethal hit opens a cinematic and records no victory");
        quackSceneAge=.5f;
        yield return null;
        yield return Shot(folder,"05-fake-victory");
        quackSceneAge=QuackFirstCrack+.56f;
        yield return null;
        yield return Shot(folder,"05b-growing-cracks");
        quackSceneAge=QuackMeme+.38f;
        yield return null;
        yield return Shot(folder,"06-speedrunners");
        quackSceneAge=QuackCall+.3f;
        yield return null;
        yield return Shot(folder,"07-level-light");
        quackSceneAge=QuackPoolsShot+.85f;
        yield return null;
        yield return Shot(folder,"07b-night-pools");
        Check(match.QuackDownpour&&match.CastleHp[1]==match.CastleHealthLimit(1)&&match.Units.All(unit=>unit.team==1),
            "Second cinematic restores 100 percent boss HP and removes the player's field army");
        Check(match.QuackRainAlive()==0,"The Great Quacks arrive after the cinematic");
        quackSceneAge=QuackReversalEnd+.2f;
        yield return null;
        match.RescueCharges[0]=1;
        var target=match.DebugQuackSpawnAlly(new Vector2(-82,0));
        BeginBuilderSpell("rescue");
        Check(targetedSpell==null&&match.QuackRescueBackfires==1&&match.RescueCharges[0]==0,
            "Pressing rescue in phase two immediately spawns the hostile frog and consumes the charge");
        yield return Shot(folder,"08-counterblast-frog");
        for(int step=0;step<19;step++)Tick(.05f);
        Check(target.hp<=0,"The frog blast kills the player's nearby creature in the actual game");
        yield return Shot(folder,"09-counterblast");
        for(int step=0;step<48;step++)Tick(.05f);
        match.Builders[0].position=match.Builders[0].previous=HumanMatch.QuackPoolPoint(4)+Vector2.up*3;
        controls.FocusWorld(World(match.Builders[0].position),34);
        UpdateFog(true);RenderMatch();
        Check(match.QuackRainAlive()==HumanMatch.QuackRainLimit&&match.Units.Exists(u=>u.team==1&&u.noBounty&&u.noCorpse&&
            match.Lines[u.kind].unit=="pU13"&&units.TryGetValue(u.id,out var view)&&view.name.StartsWith("Great Quack",StringComparison.Ordinal)),
            "The original thirty-slot wave uses the updated Great Quack model");
        yield return Shot(folder,"09b-great-quack-wave");
        match.DebugQuackHitCastle();
        Tick(.05f);
        Check(match.Winner==0&&page==Page.Result,"Second lethal hit gives the actual victory");
        Check(bossSoundtrack&&!bossSoundtrack.isPlaying,"The challenge stops the soundtrack on victory");
        File.WriteAllText(Path.Combine(folder,"quackalypse-verification.json"),new JObject{
            ["status"]=testFailures==0?"PASS":"FAIL",["version"]=Version,["checks"]=testChecks
        }.ToString());
        Debug.Log("CF_QUACKALYPSE_CHECKS "+testChecks.Count+" failed="+testFailures);
        Application.Quit(testFailures==0?0:1);
    }

    IEnumerator BenchmarkQuackalypse(string folder){
        Application.targetFrameRate=-1;
        QualitySettings.vSyncCount=0;
        quackSceneAge=QuackOpeningEnd;
        yield return null;
        var report=new JObject{["resolution"]=Screen.width+"x"+Screen.height,["gpu"]=SystemInfo.graphicsDeviceName,
            ["cpu"]=SystemInfo.processorType,["units"]=112};
        var cases=new JArray();
        var drizzle=quackDrizzle;
        var downpour=quackDownpour;
        for(int phase=1;phase<=2;phase++){
            match.SpawnQuackBenchmarkArmy();
            FrameWinterCamera(World(new Vector2(-5,0)),53,0,52);
            RenderMatch();
            foreach(bool weather in new[]{true,false,true}){
                quackDrizzle=weather?drizzle:null;
                quackDownpour=weather?downpour:null;
                yield return new WaitForSecondsRealtime(.7f);
                var frames=new List<float>();
                yield return CollectProtocolFrames(frames,2.2f);
                var sample=ProtocolFrameStats(frames);
                sample["phase"]=phase;
                sample["weather"]=weather;
                cases.Add(sample);
            }
            quackDrizzle=drizzle;
            quackDownpour=downpour;
            if(phase==1){
                match.DebugQuackHitCastle();
                Tick(.05f);
                var cinematic=new List<float>();
                yield return CollectProtocolFrames(cinematic,QuackReversalEnd+.2f);
                report["phase_2_cinematic"]=ProtocolFrameStats(cinematic);
                match.ClearQuackBenchmarkArmy();
            }
        }
        testHoldAuthority=false;
        var live=new List<float>();
        yield return CollectProtocolFrames(live,4f);
        report["phase_2_live_battle"]=ProtocolFrameStats(live);
        report["render_comparison"]=cases;
        File.WriteAllText(Path.Combine(folder,"quackalypse-fps.json"),report.ToString());
    }
}

public sealed partial class HumanMatch {
    internal void DebugQuackProducer(int team,out Hut created){
        int kind=team==0?BaseKinds.First(k=>Lines[k].Producer):Kind("pB31");
        foreach(int y in Enumerable.Range(0,24))
        foreach(int x in Enumerable.Range(0,32))
            if(LegalCell(team,x,y)&&TryBuild(team,kind,x,y,out _)){
                created=Huts.Last();
                created.paid=true;created.buildLeft=0;
                Builders[team].jobs.Remove(created.id);
                return;
            }
        throw new InvalidOperationException("Quack fixture cannot place the producer");
    }

    internal void DebugQuackInfiltrate(Hut producer){
        quackInfiltrationRandom=new System.Random(1);
        Produce(producer);
    }

    internal Unit DebugQuackSpawnAlly(Vector2 point)=>Spawn(0,BaseKinds.First(k=>Lines[k].Producer),point);

    internal void CheckQuackReplacementRate(JObject rules,Action<bool,string> check){
        BeginChallengeRules(rules);
        DebugQuackProducer(0,out var producer);
        quackInfiltrationRandom=new System.Random(42);
        const int releases=600;
        for(int i=0;i<releases;i++){
            Produce(producer);
            Units.Clear();InvalidateArmyCounts035();RebuildIdCache033();
        }
        check(QuackInfiltrations>=releases*.45f&&QuackInfiltrations<=releases*.55f,
            $"An independent 50 percent roll on every release at the same timestamp replaced {QuackInfiltrations} of {releases}");
    }

    internal void DebugQuackHitCastle()=>ApplyCastleDamage(1,CastleHealthLimit(1)*2);

    internal void SpawnQuackBenchmarkArmy(){
        for(int side=0;side<2;side++){
            var roster=side==0?new[]{"h006","h00A"}:new[]{"pB31","pB32"};
            for(int i=0;i<56;i++){
                int kind=Kind(roster[i%roster.Length]);
                if(kind<0||!Lines[kind].Producer)kind=side==0?BaseKinds.First(k=>Lines[k].Producer):Kind("pB31");
                var point=new Vector2((side==0?-1:1)*(3+i/14*2.7f),(i%14-6.5f)*1.45f);
                Spawn(side,kind,point).onRoad=true;
            }
        }
    }

    internal void ClearQuackBenchmarkArmy(){
        Units.Clear();
        InvalidateArmyCounts035();
        RebuildIdCache033();
    }

    internal void CheckQuackOpeningEconomy(Action<bool,string> check){
        quackInfiltrationRandom=new System.Random(0);
        int kind=BaseKinds.First(k=>Lines[k].Producer&&Lines[k].woodCost==0&&Lines[k].cost<=500);
        bool queued=TryBuild(0,kind,17,12,out _);
        check(queued&&Income(0)==Income(1)&&Gold[1]==Gold[0]&&Wood[1]==Wood[0],
            "Both sides start with identical base income and pay through the standard build queue");
        bool queuedBot=false,paidBot=false,playerReleased=false;
        while(Elapsed<59.8f&&!Finished){
            Step(.05f);
            queuedBot|=Builders[1].jobs.Count>0&&Huts.Any(h=>h.team==1&&!h.paid);
            paidBot|=Huts.Any(h=>h.team==1&&h.paid&&Lines[h.kind].Producer);
            playerReleased|=Units.Any(u=>u.team==0);
        }
        check(queuedBot&&paidBot&&playerReleased&&QuackBellCount==0,
            "The bot queues and pays for producers normally; a standard player opening releases troops before the bell");
    }

    internal void CheckQuackRules(JObject rules,Action<bool,string> check){
        BeginChallengeRules(rules);
        var point=new Vector2(139,4);
        var guard=Spawn(1,Kind("pB31"),point);
        var ally=Spawn(1,Kind("pB12"),point+Vector2.right*4);
        check(Mathf.Approximately(guard.maxHp,Lines[guard.kind].hp*1.2f)&&LegendaryLimit[1]==2,
            "Boss troops have 20 percent more HP and two legendary slots");
        PepeGiveShield(guard,80,3,"PGSh");
        TickEffects(guard,3.05f);
        check(PepeShield(guard)==null&&PepeShield(ally)?.power==40,"Expired rain shield transfers half of its remaining protection");
        TickEffects(ally,3.05f);
        check(PepeShield(guard)==null,"A relayed shield expires without bouncing back");
        Elapsed=179.99f;
        TickQuackTrial();
        check(QuackBellCount==0,"The bell stays silent before minute three");
        Elapsed=180.01f;
        TickQuackTrial();
        check(QuackBellCount==1&&Units.Count==7&&Units.Count(unit=>PepeShield(unit)!=null)==2,
            "The first bell at minute three sends five mixed reinforcements with two protected guards");
        Elapsed=359.99f;
        TickQuackTrial();
        bool stayedAtOne=QuackBellCount==1;
        Elapsed=360.01f;
        TickQuackTrial();
        check(stayedAtOne&&QuackBellCount==2,"Repeating bell waves stay three minutes apart");
        DebugQuackProducer(0,out var tavern);
        DebugQuackInfiltrate(tavern);
        var invader=Units.Last();
        check(QuackInfiltrations==1&&invader.team==1&&invader.attackTarget==HutTarget(tavern)&&invader.strikeLeft>=0,
            "A stolen production slot starts the hostile swordsman's attack on its tavern immediately");
        Produce(tavern);
        check(QuackInfiltrations==2&&Units.Last().team==1,"Consecutive production events can both be replaced");
        InitBotPolicy();ObserveBot();
        check(QuackBotPriority(Kind("pB31"),null,0)>QuackBotPriority(Kind("pB06"),null,0),
            "Boss favours shield-capable infantry over early tadpole spam");
        botAir=.8f;botAirCoverage=.2f;
        check(QuackBotPriority(Kind("pB14"),null,1)>QuackBotPriority(Kind("pB12"),null,1)
            &&QuackBotPriority(Kind("pB10"),null,0)>0,"Air-heavy armies trigger guard upgrades and anti-air towers");
        int kept=Huts.Count;
        DebugQuackHitCastle();DebugQuackHitCastle();
        float elapsed=Elapsed;
        Step(.05f);
        check(!Finished&&CastleHp[1]==0&&Elapsed==elapsed,"Multiple lethal damage sources cannot skip the cinematic or finish the challenge");
        check(OpenQuackDownpour()&&CastleHp[1]==QuackCastleHealth&&Units.All(unit=>unit.team==1)&&Huts.Count==kept,
            "Intermission resets the boss castle, removes player units and preserves production buildings");
        check(QuackRainAlive()==0,"Opening downpour queues the groups without spawning them");
        for(int i=0;i<12;i++){Elapsed+=.65f;TickQuackTrial();}
        string rainBuilding=ExpertTrial?QuackKingBuilding:"pB06";
        check(QuackRainAlive()==QuackRainLimit&&Units.Count(unit=>unit.hp>0&&unit.team==1&&
            unit.noBounty&&unit.noCorpse&&Lines[unit.kind].building==rainBuilding)==QuackRainLimit,
            ExpertTrial?"Expert rain spawns thirty Great Quacks":"Epochal rain spawns thirty tadpoles");
        float rain=quackRainAt;
        Elapsed=rain-QuackRainWarning-.01f;TickQuackTrial();
        bool waited=quackRainLeft==0;
        Elapsed=rain-QuackRainWarning+.01f;TickQuackTrial();
        bool warned=quackRainLeft==2&&Mathf.Approximately(quackNextHatch,rain);
        Elapsed=rain+.01f;TickQuackTrial();
        check(waited&&warned&&quackRainLeft==1&&QuackRainAlive()==QuackRainLimit&&Mathf.Approximately(quackRainAt,rain+180),
            "Downpour repeats every three minutes, with a short warning and the same live cap");
        var near=DebugQuackSpawnAlly(new Vector2(-75,0));
        var far=DebugQuackSpawnAlly(new Vector2(-155,0));
        var opposite=Spawn(1,Kind("pB31"),near.position+Vector2.up*2);
        RescueCharges[0]=2;
        check(Rescue(0,near.position)&&QuackRescueBackfires==1&&RescueCharges[0]==1&&RescueCooldown[0]==60&&
            quackBackfireFrog!=null&&quackBackfireFrog.team==1&&near.hp>0&&!Rescue(0,near.position),
            "Phase two triggers one enemy frog with the normal charge and cooldown");
        Elapsed+=QuackBackfireDelay+.01f;
        TickQuackTrial();
        check(near.hp<=0&&far.hp>0&&opposite.hp>0&&tavern.hp>0,
            "Counterblast detonates within normal radius against player units only, preserving buildings");
        DebugQuackHitCastle();Step(.05f);
        check(Winner==0&&ChallengeVictoryEligible,"Only the rebuilt castle's defeat is eligible for the leaderboard");
    }
}
}
