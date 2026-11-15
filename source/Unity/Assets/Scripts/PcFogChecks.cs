using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseFog(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"fog-validation"));Directory.CreateDirectory(folder);
        options.fogOfWar=true;MainMenu();raceChoice=botRaceChoice=0;aspectsOn=false;roundsChoice=1;NewMatch();testHoldAuthority=true;UpdateFog(true);RenderMatch();
        Check(FogActive&&fog.Visible(HumanMatch.CastlePosition(0))&&!fog.Visible(HumanMatch.CastlePosition(1)),"Fog defaults on, own base permanently visible, enemy base unseen");
        Check(!fog.Known(Vector2.zero)&&fog.Display(Vector2.zero)==0,"Unexplored battlefield starts black");
        controls.FocusWorld(World(new Vector2(-58,0)),48);yield return Shot(folder,"01-unexplored-boundary");
        int kind=Array.FindIndex(match.Lines,l=>l.Producer);var enemy=match.Spawn(1,kind,Vector2.zero);UpdateFog(true);RenderMatch();
        Check(!CanSeeUnit(enemy)&&!units[enemy.id].gameObject.activeSelf,"Unseen enemy model hidden");selectedUnit=enemy.id;Check(SelectedUnit==null,"Hidden enemy cannot remain selected in unit card");
        SetSelectedGroup(new[]{enemy.id});Check(selectedGroup.Count==0,"Drag selection excludes unseen enemies");
        var scout=match.Spawn(0,kind,new Vector2(-12,0));UpdateFog(true);RenderMatch();Check(fog.Visible(enemy.position)&&CanSeeUnit(enemy)&&units[enemy.id].gameObject.activeSelf,"Allied living unit reveals enemy and enables selection");
        controls.FocusWorld(World(scout.position),45);yield return Shot(folder,"02-visible-scout");
        scout.hp=0;UpdateFog(true);RenderMatch();Check(!fog.Visible(Vector2.zero)&&fog.Known(Vector2.zero)&&fog.Display(Vector2.zero)==85&&!CanSeeUnit(enemy),"After scout loss, explored terrain stays dim and enemies disappear");yield return Shot(folder,"03-explored-shadow");
        match.Gold[1]=100000;match.Wood[1]=100000;Check(match.TryBuild(1,kind,6,15,out _),"Enemy building fixture placed on legal base cell");var hut=match.Huts.Last();UpdateFog(true);RenderMatch();
        Check(!fogKnownHuts.Contains(hut.id)&&!huts[hut.id].gameObject.activeSelf,"Never seen enemy building hidden on field and minimap");
        scout.hp=scout.maxHp;scout.position=scout.previous=hut.position;UpdateFog(true);RenderMatch();Check(CanInspectHut(hut)&&fogKnownHuts.Contains(hut.id),"Scout discovers enemy building");
        scout.hp=0;UpdateFog(true);RenderMatch();selectedHut=hut.id;Check(fogKnownHuts.Contains(hut.id)&&huts[hut.id].gameObject.activeSelf&&SelectedHut==null,"Known building retained under shroud without live card inspection");
        controls.FocusWorld(World(hut.position),45);yield return Shot(folder,"04-known-building");
        enemy.effects.Add(new HumanMatch.Effect{left=10,sourceTeam=0,config=new JObject{["code"]="Afae",["reveals"]=true}});UpdateFog(true);Check(FogVisible(enemy.position),"Elven Faerie Fire supplies native vision");enemy.effects.Clear();
        var orb=new HumanMatch.NorthernOrb{team=0,position=Vector2.zero};match.NorthernOrbs.Add(orb);UpdateFog(true);Check(FogVisible(Vector2.zero),"Northern winter sphere supplies native vision");match.NorthernOrbs.Clear();
        var zone=new HumanMatch.NightZone{team=0,point=Vector2.zero,age=7};match.NightZones.Add(zone);UpdateFog(true);Check(FogVisible(Vector2.zero),"Night Starfall active zone supplies native vision");zone.age=8;UpdateFog(true);Check(!FogVisible(Vector2.zero),"Expired Starfall vision ends");match.NightZones.Clear();
        Check(!match.VisionTest(0,enemy.position)&&match.VisionTest(1,enemy.position),"Manual player target validation uses fog without restricting global AI spells");
        enemy.nightInvisible=true;scout.hp=scout.maxHp;scout.position=scout.previous=enemy.position;UpdateFog(true);Check(!CanSeeUnit(enemy),"Vision alone does not reveal native invisible enemies");enemy.nightInvisible=false;
        options.fogOfWar=false;RenderMatch();Check(CanSeeUnit(enemy)&&castleObjects[1].activeSelf,"Disabling fog restores full battlefield presentation");
        options.fogOfWar=true;MainMenu();NewMatch();testHoldAuthority=true;UpdateFog(true);Check(!fog.Known(Vector2.zero)&&fogKnownHuts.Count==0,"New match clears exploration and remembered buildings");
        BeginChallenge(progression.Challenge("five_pillars"));testHoldAuthority=true;options.fogOfWar=false;UpdateFog(true);Check(FogActive,"Challenges enforce fog independently of casual setting");MainMenu();options.fogOfWar=true;
        NewMatch();testHoldAuthority=true;UpdateFog(true);controls.FocusWorld(World(new Vector2(-58,0)),48);yield return new WaitForSecondsRealtime(.5f);yield return Shot(folder,"05-fog-final");
        Check(fogScreen.width==160&&fogScreen.height==90&&fogMini.width==256&&fogMini.height==128,"Small fixed masks independent of display resolution, no fog particles");
        var reference=new FogGrid();reference.RevealBase();reference.Reveal(new Vector2(-12,0),800f/30);reference.Remember();File.WriteAllBytes(Path.Combine(folder,"vision-grid.bin"),reference.Current);File.WriteAllBytes(Path.Combine(folder,"explored-grid.bin"),reference.Explored);
        var report=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["build"]=Version,["checks"]=testChecks,["failures"]=testFailures,["projectionMilliseconds"]=fogProjectionMs,["maskMilliseconds"]=fogMaskMs,["refreshSeconds"]=.18,["cameraProjectionMaxHz"]=12};File.WriteAllText(Path.Combine(folder,"fog-verification.json"),report.ToString());
        Debug.Log("CF_FOG_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count+" projectionMs="+fogProjectionMs+" maskMs="+fogMaskMs);Application.Quit(testFailures==0?0:1);
    }
}
}

