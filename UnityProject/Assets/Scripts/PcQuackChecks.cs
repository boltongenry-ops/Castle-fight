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
        yield return Action("main.challenges");
        yield return Action("progress.mythic");
        yield return Action("mythic.play."+HumanMatch.QuackTrial);
        Check(MythicRacePicker&&selectedMythicId==HumanMatch.QuackTrial,"Sixth mythic challenge has a separate menu entry");
        mythicRace=0;
        yield return Shot(folder,"01-boss-card");
        yield return Action("mythic.begin");
        testHoldAuthority=true;
        string captureFolder=BattleLab.Arg("--pc-quack-reel");
        if(captureFolder.Length>0){
            quackSceneAge=9;
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
        Check(quackCroak&&quackCroak.frequency==24000&&quackCroak.length>.5f,"Speech has the recorded croak");
        Check(quackHudFrame&&quackHudFrame.width>quackHudFrame.height*3,"The frog-medallion banner is loaded");
        quackSceneAge=4.3f;
        yield return null;
        yield return Shot(folder,"02-opening-orders");
        quackSceneAge=6.65f;
        yield return null;
        Check(QuackEyeShot&&QuackEyeBandBottom-QuackEyeBandTop<=125,"The opening cuts to the narrow eyes-only shot");
        yield return Shot(folder,"03-king-close");
        quackSceneAge=7.75f;
        yield return null;
        yield return Shot(folder,"03b-swamp-transition");
        Check(quackSwampRevealed&&quackGround.Count>0&&quackWetland&&quackWetland.activeSelf&&quackWetlandMesh.vertexCount>0,
            "The camera return reveals the swamp textures and the combined puddle and lily mesh");
        Check(!match.QuackDownpour&&quackDrizzle&&quackDownpour&&quackDrizzle!=quackDownpour,"The swamp opens with drizzle and has a separate downpour texture");
        quackSceneAge=9;
        yield return null;
        var humans=JObject.Parse(RaceCatalog(0));
        var pepe=JObject.Parse(RaceCatalog(PepeContent.Race));
        new HumanMatch(humans,500,2,-1,-1,pepe).CheckQuackRules(QuackDefinition(),Check);
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
        Check(match.QuackRainAlive()==0,"Tadpoles stay offscreen until after the cinematic");
        quackSceneAge=QuackReversalEnd+.2f;
        yield return null;
        match.DebugQuackHitCastle();
        Tick(.05f);
        Check(match.Winner==0&&page==Page.Result,"Second lethal hit gives the actual victory");
        File.WriteAllText(Path.Combine(folder,"quackalypse-verification.json"),new JObject{
            ["status"]=testFailures==0?"PASS":"FAIL",["version"]=Version,["checks"]=testChecks
        }.ToString());
        Debug.Log("CF_QUACKALYPSE_CHECKS "+testChecks.Count+" failed="+testFailures);
        Application.Quit(testFailures==0?0:1);
    }

    IEnumerator BenchmarkQuackalypse(string folder){
        Application.targetFrameRate=-1;
        QualitySettings.vSyncCount=0;
        quackSceneAge=9;
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
        Elapsed=quackInfiltrationAt+.05f;
        Produce(producer);
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
        while(Elapsed<60.2f&&!Finished)Step(.05f);
        check(QuackBellCount==1,"The first bonus wave triggers at one minute");
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
        Elapsed=60.01f;
        TickQuackTrial();
        check(QuackBellCount==1&&Units.Count==7&&Units.Count(unit=>PepeShield(unit)!=null)==2,
            "The first bell sends five mixed reinforcements with two protected guards");
        DebugQuackProducer(0,out var tavern);
        DebugQuackInfiltrate(tavern);
        var invader=Units.Last();
        check(QuackInfiltrations==1&&invader.team==1&&invader.attackTarget==HutTarget(tavern)&&invader.strikeLeft>=0,
            "A stolen production slot starts the hostile swordsman's attack on its tavern immediately");
        Produce(tavern);
        check(QuackInfiltrations==1&&Units.Last().team==0,"Only one spawn is replaced in the current window");
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
        check(QuackRainAlive()==QuackRainLimit,"The initial downpour reaches 30 live tadpoles");
        Elapsed+=80;TickQuackTrial();Elapsed+=5;TickQuackTrial();
        check(QuackRainAlive()==QuackRainLimit,"Repeat waves respect the live tadpole cap");
        DebugQuackHitCastle();Step(.05f);
        check(Winner==0&&ChallengeVictoryEligible,"Only the rebuilt castle's defeat is eligible for the leaderboard");
    }
}
}
