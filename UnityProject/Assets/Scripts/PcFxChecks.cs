using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseReforgedFx(string folder){
        MainMenu();yield return Action("main.play");yield return Action("setup.orc");startGold=1000;options.hdTextures=true;yield return Action("setup.start");testHoldAuthority=true;match.Gold[1]=0;
        foreach(var line in match.Lines){line.speed=0;line.regen=0;}
        var drum=match.Spawn(0,match.Kind("h035"),new Vector2(-160,32));var armor=match.Spawn(0,match.Kind("h036"),new Vector2(-154,32));var target=match.Spawn(0,match.Kind("h029"),new Vector2(-149,32));foreach(var u in match.Units){u.onRoad=true;u.cooldown=10000;u.pose="Stand";}
        match.ApplyEffect(target,"A02M");match.ApplyEffect(target,"A038",1);for(int i=0;i<12;i++)Tick(.05f);RenderMatch();builderSelected=false;selectedUnit=target.id;controls.FocusWorld(World(new Vector2(-154,32)),30);yield return new WaitForSecondsRealtime(.3f);
        Check(reforgedFx.Count==52&&reforgedFx.Values.All(a=>a.texture&&a.material.shader.isSupported),"original Reforged effect sheets and supported shader are loaded");
        Check(reforgedFx["general_aura"].info.Loops&&target.auraArmor>0&&target.auraDamage>0&&target.auraIds.Contains("A034")&&target.auraIds.Contains("Aakb"),"armor and war drums keep their source aura IDs and share the source buff effect");
        var drumStart=drum.position;drum.position+=new Vector2(100,0);for(int i=0;i<12;i++)Tick(.05f);Check(!target.auraIds.Contains("Aakb")&&target.auraIds.Contains("A034"),"aura visual state leaves with its source while other aura remains");drum.position=drumStart;for(int i=0;i<12;i++)Tick(.05f);RenderMatch();
        Check(target.effects.Any(e=>e.id=="A02M")&&target.effects.Any(e=>e.id=="A038")&&reforgedFx.ContainsKey("innerfire")&&reforgedFx.ContainsKey("poison"),"inner fire and poison use source visual states");yield return Shot(folder,"81-reforged-auras");
        target.hp-=20;match.Heal(target,10);RenderMatch();Check(match.Pulses.Any(p=>p.text.StartsWith("+"))&&reforgedFx.ContainsKey("heal"),"heal triggers Reforged effect");yield return Shot(folder,"82-reforged-heal");
        Check(reforgedFx.ContainsKey("acid_missile")&&reforgedFx.ContainsKey("steal_missile")&&reforgedFx.ContainsKey("steal_caster")&&reforgedFx.ContainsKey("catapult_missile")&&reforgedFx.ContainsKey("stomp"),"original projectile and spell impact effects are loaded");
        Check(new[]{"axe_missile","wyvern_missile","poison_arrow","illidan_missile","guard_missile","net_missile","mortar_missile","catapult_missile","cannon_missile","gryphon_missile"}.All(n=>ContentResources.Load<GameObject>("RefFx/proj_"+n)),"all ten Reforged projectile models load");
        Check(ContentResources.Load<GameObject>("RefFx/net")!=null,"original Reforged animated net prefab is available");
        target.stun=2;target.stasisLeft=2;match.ApplyEffect(target,"A030",1);match.Fires.Add(new HumanMatch.Fire{id=999001,team=1,ability="A02K",center=target.position,nextFull=9999,nextTail=9999});RenderMatch();yield return new WaitForSecondsRealtime(.3f);
        Check(nativeFlames.Count==1&&flameMaterial&&flameMaterial.shader.isSupported&&reforgedFx.ContainsKey("stun")&&reforgedFx.ContainsKey("incinerate")&&reforgedFx.ContainsKey("devotion")&&target.stasisLeft>0&&reforgedFx.ContainsKey("acid"),"original flame emitter and target debuffs render");yield return Shot(folder,"83-reforged-flame-debuffs");
        match.Fires[0].center=target.position+new Vector2(6,0);RenderMatch();yield return new WaitForSecondsRealtime(1f);
        Check(nativeFlames.Values.First().particleCount>0,"fire visibly emits native particles");yield return Shot(folder,"87-reforged-fire");
        match.Fires.Clear();RenderMatch();Check(nativeFlames.Count==0,"expired flame stops emitting");
        match.Shots.Add(new HumanMatch.Shot{id=999002,team=0,kind=4,start=target.position+Vector2.left*5,end=target.position,duration=.3f,age=.15f,hit=true,startHeight=1.5f,endHeight=1.5f});RenderMatch();
        Check(reforgedFx.ContainsKey("lightning_missile")&&reforgedFx.ContainsKey("boltimpact")&&projectiles.ContainsKey(999002),"chain lightning uses original Reforged missile and impact layers");yield return Shot(folder,"88-reforged-lightning");match.Shots.RemoveAll(s=>s.id==999002);RenderMatch();
        match.PulseAt(target.position,5,Color.cyan,"Магический взрыв",1.267f,"wisp");RenderMatch();yield return new WaitForSecondsRealtime(.65f);
        Check(wispBursts.Count==1&&wispMaterial&&wispMaterial.shader.isSupported&&wispBursts.Values.First().GetComponentsInChildren<ParticleSystem>().Length==2&&wispBursts.Values.First().GetComponentsInChildren<PcModel>().Any(m=>m.raw=="fx_wisp_explode"&&m.clips.Any(c=>c.name.StartsWith("Birth")))&&wispMeshMaterial&&wispMeshMaterial.GetInt("_DstBlend")==10,"Wisp explosion uses original texture and particle profiles");yield return Shot(folder,"86-reforged-wisp");
        var expiredWisp=wispBursts.Values.First();match.Pulses.RemoveAll(p=>p.fx=="wisp");RenderMatch();Check(wispBursts.Count==0&&!expiredWisp.GetComponentInChildren<PcModel>(true).gameObject.activeSelf,"expired Wisp mesh stops while its last particles fade");
        options.hdTextures=true;RenderMatch();Check(reforgedFx.Count>0,"Reforged effects remain available");options.hdTextures=true;
        Debug.Log("CF_REFORGED_FX_PLAYER_PASS");
    }
}
}
