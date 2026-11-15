using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseFrontend(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"frontend-validation"));Directory.CreateDirectory(folder);
        MainMenu();yield return new WaitForSecondsRealtime(2);Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length==1,"Exactly one active audio listener in the actual player");
        Check(menuAudio&&menuAudio.clip&&menuAudio.clip.loadState==AudioDataLoadState.Loaded&&menuAudio.isPlaying&&menuAudio.volume>0,"Godot menu music loaded, playing and audible by settings");
        var samples=new float[512];bool audible=false;for(int attempt=0;attempt<40&&!audible;attempt++){menuAudio.GetOutputData(samples,0);audible=samples.Any(v=>Mathf.Abs(v)>.00001f);if(!audible){AudioListener.GetOutputData(samples,0);audible=samples.Any(v=>Mathf.Abs(v)>.00001f);}if(!audible)yield return new WaitForSecondsRealtime(.2f);}Debug.Log("CF_AUDIO_PROBE audible="+audible+" time="+menuAudio.time+" volume="+menuAudio.volume+" listener="+AudioListener.volume+" paused="+AudioListener.pause+" focused="+Application.isFocused+" dsp="+AudioSettings.dspTime);Check(audible,"Menu audio source emits nonzero PCM samples");
        Check(loadingBackground&&loadingHammer,"Generated loading background and transparent orc hammer packaged");
        Check(((JArray)progression.Catalog["challenges"]).Count==18&&((JArray)progression.Catalog["items"]).Count==7,"Nine Godot challenges plus nine new challenges and all six original shop items plus the meme pack");
        foreach(var row in (JArray)progression.Catalog["challenges"]){Check(RaceStaged(Array.IndexOf(ProgressRaces,(string)row["race"]))&&RaceStaged(Array.IndexOf(ProgressRaces,(string)row["opponent"])),"Both playable challenge races: "+row["id"]);}
        Check(progression.Coins==0,"Fresh test profile starts without free tokens");yield return Shot(folder,"01-main");
        yield return Action("main.challenges");Check(page==Page.Progression&&!progressShop,"Challenges open through main menu");yield return Shot(folder,"02-challenges");
        progressionScroll=new Vector2(0,2500);yield return Shot(folder,"03-more-challenges");progressionScroll=Vector2.zero;
        int chosenRace=raceChoice,gold=startGold;yield return Action("challenge.five_pillars");testHoldAuthority=true;
        Check(activeChallenge!=null&&matchRace==0&&matchBotRace==1&&startGold==500&&series.Rounds==1&&!aspectsOn,"Source challenge races, gold, one round and no aspects");
        match.Gold[0]=100000;match.Wood[0]=100000;match.LegendaryLimit[0]=20;
        int kind=match.BaseKinds.First(k=>match.Lines[k].upgrades.Length>0);
        for(int i=0;i<5;i++)Check(match.TryBuild(0,kind,3+i*2,15,out _),"Five-building rule accepts queued building "+i);
        Check(!match.TryBuild(0,kind,15,15,out string limit)&&limit.Contains("максимум 5"),"Sixth queued building rejected by simulation authority");
        var first=match.Huts.First();first.paid=true;first.buildLeft=0;first.started=true;
        int upgrade=match.Kind(match.Lines[kind].upgrades[0]);Check(match.Upgrade(0,first.id,upgrade,out _),"Upgrades remain allowed at the building cap");
        Check(match.Sell(0,match.Huts.Last().id)&&match.TryBuild(0,kind,15,15,out _),"Selling releases one simultaneous building slot");
        RenderMatch();yield return Shot(folder,"04-five-buildings");
        match.Surrender(1);Tick(.05f);Check(challengeReward==100&&progression.Coins==100&&progression.Completed("five_pillars"),"Actual winning result awards first completion tokens");SettleChallenge();Check(progression.Coins==100,"Repeated result callback cannot duplicate reward");yield return Shot(folder,"05-challenge-reward");
        MainMenu();Check(raceChoice==chosenRace&&startGold==gold&&activeChallenge==null,"Leaving restores normal battle settings");
        BeginChallenge(progression.Challenge("five_pillars"));testHoldAuthority=true;match.Surrender(1);Tick(.05f);Check(challengeReward==0&&progression.Coins==100,"Replay victory grants no repeated completion reward");MainMenu();
        BeginChallenge(progression.Challenge("no_legends"));testHoldAuthority=true;match.Gold[0]=match.Gold[1]=100000;match.Wood[0]=match.Wood[1]=100000;
        foreach(int team in new[]{0,1}){int legendary=Array.FindIndex(match.Lines,l=>l.slots>0);Check(match.PurchaseError(team,legendary).Contains("легендарные"),"Legendary restriction enforced for side "+team);}
        match.Surrender(0);Tick(.05f);Check(progression.Coins==100&&!progression.Completed("no_legends"),"Defeat awards no completion tokens");MainMenu();
        BeginChallenge(progression.Challenge("north_storm"));testHoldAuthority=true;match.Elapsed=721;Tick(.05f);Check(match.Winner==1&&!progression.Completed("north_storm"),"Time-limited challenge ends as a defeat when deadline expires");MainMenu();
        BeginChallenge(progression.Challenge("bone_fortress"));testHoldAuthority=true;match.CastleHp[0]=HumanMatch.CastleMax*.69f;match.Surrender(1);Tick(.05f);Check(!match.ChallengeVictoryEligible&&!progression.Completed("bone_fortress"),"Victory below required castle health grants no reward");MainMenu();
        BeginChallenge(progression.Challenge("orc_no_rescue"));testHoldAuthority=true;match.Gold[0]=match.Gold[1]=100000;
        foreach(int team in new[]{0,1})Check(!match.Rescue(team,Vector2.zero)&&!match.BuyRescue(team),"No rescue use or repurchase for side "+team);MainMenu();
        // Controlled award fixtures write only to the explicit isolated test profile.
        foreach(var row in ((JArray)progression.Catalog["challenges"]).Take(9))progression.Complete((string)row["id"],100);
        int coins=progression.Coins;Check(!progression.Buy("does-not-exist",out _)&&progression.Coins==coins,"Unknown shop item cannot debit wallet");
        yield return Action("main.challenges");yield return Action("progress.shop");Check(progressShop,"Shop opens through main menu");yield return Shot(folder,"06-shop");
        yield return Action("progress.item.frame_jade");Check(!progression.Owned("frame_jade"),"Purchase requires concrete second confirmation");
        yield return Action("progress.item.frame_jade");Check(progression.Owned("frame_jade")&&progression.Coins==coins-150,"Confirmed purchase debits exact original price");
        Check(!progression.Buy("frame_jade",out _)&&progression.Coins==coins-150,"Duplicate purchase cannot spend tokens again");
        yield return Action("progress.item.frame_jade");Check(progression.Equipped("frame")=="frame_jade","Owned frame equips through shop UI");
        Check(progression.Buy("avatar_pandaren",out _)&&progression.Equip("avatar_pandaren",true)&&ProfileAvatar(),"Original Pandaren avatar equips and resolves original emblem");
        Check(progression.Buy("aura_jade",out _)&&progression.Equip("aura_jade",true),"Original builder aura bought and equipped");yield return Shot(folder,"07-equipped");
        var reload=new PcProgressionStore(progression.Catalog,BattleLab.Arg("--pc-profile-file"));Check(reload.Coins==progression.Coins&&reload.Owned("frame_jade")&&reload.Equipped("aura")=="aura_jade"&&reload.Completed("five_pillars"),"Wallet, completed tasks, collection and equipment survive disk reload");
        MainMenu();raceChoice=0;botRaceChoice=1;roundsChoice=1;aspectsOn=false;NewMatch();testHoldAuthority=true;RenderMatch();Check(builderCosmetic&&builderCosmetic.activeSelf,"Purchased cosmetic aura displayed on builder");controls.FocusWorld(World(match.Builders[0].position),26);yield return new WaitForSecondsRealtime(1);Check(cosmeticParticles&&cosmeticParticles.particleCount>0&&cosmeticRunes.childCount==12,"Jade aura has live particles and twelve orbiting rune glyphs");yield return Shot(folder,"08-builder-aura");
        Check(progression.Buy("aura_flame",out _)&&progression.Equip("aura_flame",true),"Fire aura purchased and equipped");RenderMatch();yield return new WaitForSecondsRealtime(1);Check(cosmeticAuraId=="aura_flame"&&cosmeticParticles.particleCount>0,"Fire aura switches to rising embers in the real player");yield return Shot(folder,"09-builder-fire-aura");
        Check(progression.Equip("aura_flame",false),"Owned aura can be unequipped without losing ownership");RenderMatch();Check(!builderCosmetic.activeSelf,"Unequipped aura no longer renders");MainMenu();
        var report=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["build"]=Version,["checks"]=testChecks,["failures"]=testFailures,["sourceChallenges"]=9,["extraChallenges"]=9,["sourceShopItems"]=6};File.WriteAllText(Path.Combine(folder,"frontend-verification.json"),report.ToString());
        Debug.Log("CF_FRONTEND_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}
