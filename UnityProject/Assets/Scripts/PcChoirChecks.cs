using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseChoir(string folder){
        MainMenu();OpenProgression(false);yield return Action("progress.mythic");
        yield return Action("challenge."+HumanMatch.ChoirTrial);
        yield return Action("mythic.play."+HumanMatch.ChoirTrial);
        yield return Action("mythic.race.0");yield return Action("mythic.begin");
        yield return RecoveryWait(()=>choirIntro&&!versusActive,"Choir cinematic and assets ready",45);
        Check(match.FuneralChoir&&choirQueen&&choirGolem&&choirClips.All(c=>c),"Lich, flesh golem, ritual audio and four music regions loaded");
        Check(!FogActive&&FogVisible(HumanMatch.CastlePosition(1)),"Ritual visible beyond player sight");
        Check(!match.EternalWinter,"No winter rules in the undead challenge");
        Time.captureFramerate=24;
        int frame=0;bool coilSeen=false,faultSeen=false,mistSeen=false;
        while(choirIntro&&frame<360){
            yield return new WaitForEndOfFrame();
            mistSeen|=choirDeath&&spectralMats.Count>0&&spectralPower>.6f&&graveMist&&graveMist.particleCount>0;
            coilSeen|=sacrificeOrb&&sacrificeOrb.activeSelf&&sacrificeMissile.key=="fx_e67f4eb60198";
            faultSeen|=choirDeath&&choirFaults.Count>0&&choirFaults[0].positionCount>=5;
            var screen=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder,"intro-"+frame.ToString("D3")+".jpg"),screen.EncodeToJPG(83));
            Destroy(screen);frame++;
        }
        Check(mistSeen,"Spectral necropolis fades in with living mist after sacrifice");
        Check(coilSeen&&faultSeen&&choirFaults.Count==0,"Warcraft Death Coil and expanding post-sacrifice fissures");
        Check(choirDeath&&choirHasLighting&&winterOverrides.Count>0&&choirTrack==0,"Sacrifice darkens the map and finishes ground corruption before the builder");
        Check(FogActive&&!FogVisible(HumanMatch.CastlePosition(1)),"Enemy keep hidden again on return to builder");
        yield return Shot(folder,"battle-bell");
        Time.captureFramerate=0;
        Check(!choirIntro&&page==Page.Match,"Cinematic hands control to the builder");
        testHoldAuthority=true;
        match.Elapsed=HumanMatch.ChoirPreparation+.12f;match.TickChoir();
        match.Elapsed+=3.85f;UpdateChoirBattle();
        Check(choirMusic.clip==choirClips[1]&&Mathf.Abs(choirMusic.time-3.85f)<.15f,"First whisper follows soundtrack position");
        yield return Shot(folder,"whisper-first");
        match.Elapsed+=HumanMatch.ChoirPass;match.TickChoir();
        match.CastleHp[1]=2499;match.Elapsed+=8*HumanMatch.ChoirBar+.12f;match.TickChoir();
        match.Elapsed+=6.48f;UpdateChoirBattle();
        Check(choirMusic.clip==choirClips[2]&&Mathf.Abs(choirMusic.time-6.48f)<.15f,"Second whisper follows soundtrack position");
        yield return Shot(folder,"whisper-second");
        match.Elapsed+=1.3f;match.TickChoir();UpdateChoirBattle();
        Check(graveFogZones.Count==3&&Mathf.Approximately(graveFogZones[0].transform.GetChild(0).localScale.x,40),"Each grave receives a 600 WC3 radius mist zone");
        testHoldAuthority=false;
        var sim=new HumanMatch(JObject.Parse(RaceCatalog(0)),500,2,-1,-1,JObject.Parse(RaceCatalog(7)));
        sim.BeginChallengeRules(ChoirDefinition());
        sim.PlayerVersusPlayer=true;
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var resolve=typeof(HumanMatch).GetMethod("ResolveAttack",flags);
        var tickShots=typeof(HumanMatch).GetMethod("TickShots",flags);
        foreach(string type in new[]{"normal","missile","artillery"}){
            int missed=0;
            var w=new HumanMatch.Weapon{type=type,attack="siege",structure=true,ground=true,full=2};
            for(int k=0;k<600;k++){
                sim.CastleHp[1]=5000;
                if(type=="normal")resolve.Invoke(sim,new object[]{0,0,-1,20f,w,0f,true,false});
                else{
                    sim.Shots.Clear();
                    sim.Shots.Add(new HumanMatch.Shot{team=0,line=0,sourceId=0,target=-1,weapon=w,damage=20,end=HumanMatch.CastlePosition(1),duration=.1f,kind=3});
                    tickShots.Invoke(sim,new object[]{.2f});
                }
                if(sim.CastleHp[1]==5000)missed++;
            }
            Check(missed>=80&&missed<=165,"Spectral castle evades "+type+" "+missed+"/600");
        }
        sim.Shots.Clear();sim.CastleHp[1]=5000;
        var ally=sim.Spawn(0,sim.Kind("h000"),Vector2.zero);
        var boss=sim.Spawn(1,sim.Kind("u002"),Vector2.right*80);
        var before=ally.hp;var enemyHp=boss.hp;
        sim.Elapsed=239.99f;sim.TickChoir();
        Check(sim.Movement==HumanMatch.ChoirMovement.Verse&&ally.hp==before&&!sim.ChoirRevealed,"Four full minutes of preparation");
        sim.Elapsed=240;sim.TickChoir();
        Check(sim.Movement==HumanMatch.ChoirMovement.First,"Uprising begins at 4:00");
        sim.Elapsed+=8*HumanMatch.ChoirBar+.01f;sim.TickChoir();
        Check(Mathf.Abs(ally.hp-before*.94f)<.01f&&boss.hp==enemyHp,"Tithe takes 6 percent only from the player");

        Check(sim.Movement==HumanMatch.ChoirMovement.First&&sim.GraveSites.Count==1&&sim.Units.Any(sim.IsCrawler),"First chorus spawns a crawler by a fighter");
        sim.Elapsed+=HumanMatch.ChoirPass;sim.TickChoir();
        sim.CastleHp[1]=2499;sim.Elapsed+=8*HumanMatch.ChoirBar+.1f;sim.TickChoir();
        sim.Elapsed+=HumanMatch.ChoirLead+.1f;sim.TickChoir();
        Check(sim.Movement==HumanMatch.ChoirMovement.Second&&sim.GraveSites.Count==3&&sim.GraveSites.All(p=>Vector2.Distance(p,HumanMatch.CastlePosition(0))<14),"Second chorus breaks ground near the player keep");
        var saved=sim.Spawn(0,sim.Kind("h000"),Vector2.left*8);
        sim.Elapsed+=HumanMatch.ChoirPass;sim.TickChoir();
        var ghost=sim.Units.Single(u=>u.id==saved.combatTarget);
        sim.DamageUnit(ghost,ghost.hp,0,execute:true);
        sim.Elapsed+=12.2f;sim.TickChoir();
        Check(saved.hp>0&&ally.hp<=0&&sim.Units.Any(u=>u.team==1&&sim.Lines[u.kind].unit=="u002"&&u.position==ally.position),"Killing a banshee saves its victim; the other becomes a hostile skeleton");
        MainMenu();Check(!choirMusic&&!choirQueen&&choirClips.All(c=>!c),"Leaving releases the encounter");
        Debug.Log("CF_CHOIR_TEST failures="+testFailures+" frames="+frame);Application.Quit(testFailures==0?0:1);
    }
}
}
