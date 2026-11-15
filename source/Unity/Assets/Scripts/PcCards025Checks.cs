using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseCards025(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"cards025"));Directory.CreateDirectory(folder);MainMenu();raceChoice=10;botRaceChoice=7;aspectsOn=false;NewMatch();testHoldAuthority=true;options.fogOfWar=false;
        var spirit=NightUnit(0,"e00B",new Vector2(-148,29));var avenger=NightUnit(0,"e00A",new Vector2(-144,29));var vampire=NightUnit(1,"h05H",new Vector2(-140,29));
        Check(UnitSkillTip(spirit,"A07K",match.Abilities["A07K"]).Contains("14%")&&!UnitSkillTip(spirit,"A07K",match.Abilities["A07K"]).Contains("вампир"),"Spirit of Vengeance shows its actual fourteen-percent soul heal instead of vampire conversion");
        Check(UnitSkillTip(avenger,"A07K",match.Abilities["A07K"]).Contains("20%")&&UnitSkillTip(avenger,"A07K",match.Abilities["A07K"]).Contains("350"),"Greater vengeance spirit describes twenty-percent heal and native death strike");
        Check(UnitSkillTip(vampire,"A07K",match.Abilities["A07K"]).Contains("Обращение в вампира"),"Actual vampire retains its conversion card");
        foreach(string raw in new[]{"n020","n01Z","n02G","n01O","n00V"})Check(!ContextAbilityVisible(raw,"A07K",match.Abilities["A07K"]),"Shared channel ID does not invent vampire conversion on "+raw);
        Check(!ContextAbilityVisible("n01Z","A07M",null)&&ContextAbilityVisible("n01U","A07M",null),"Assassin does not inherit the mechanic's automatic repair label");
        Check(!ContextAbilityVisible("h03P","A0C5",null)&&ContextAbilityVisible("n029","A0C5",null),"Lightning and wind markers do not inherit bear transformation cards");
        Check(!UnitSkillTip(spirit,"A07B",match.Abilities["A07B"]).Contains("Горный великан"),"Shared resistance tooltip uses actor-neutral wording");
        RenderMatch();builderSelected=false;selectedUnit=spirit.id;controls.FocusWorld(World(spirit.position),30);testHudPointer=Slot(0,0).center;yield return Shot(folder,"01-spirit-passive");testHudPointer=null;
        guideRace=10;guideAuthority=new HumanMatch(JObject.Parse(RaceCatalog(10)));guideEntry=Array.FindIndex(guideAuthority.Lines,l=>l.unit=="e00B");guideActorIndex=0;guideUnit=true;
        Check(GuideEntryText().Contains("14%")&&!GuideEntryText().Contains("Обращение в вампира"),"Reference book shares the corrected spirit passive");guideAuthority=null;
        var previous=match;var towerRows=new JArray();var visited=new HashSet<string>();
        for(int race=0;race<15;race++){
            var authority=new HumanMatch(JObject.Parse(RaceCatalog(race)));match=authority;
            foreach(var line in authority.Lines.Where(l=>string.IsNullOrEmpty(l.unit)&&l.buildingWeapons.Length>0)){
                if(!visited.Add(line.building))continue;int kind=Array.IndexOf(authority.Lines,line);
                for(int i=0;i<line.buildingWeapons.Length;i++){
                    var weapon=line.buildingWeapons[i];string key=(string)classicProjectileIndex["weapons"]?[line.building+"_"+(i+1)];if(key==null)continue;
                    var shot=new HumanMatch.Shot{line=kind,sourceId=777777,weapon=weapon,team=0,kind=3};var prefab=NativeMissilePrefab(shot);
                    Check(prefab&&prefab.GetComponent<ClassicProjectileModel>()&&prefab.GetComponent<ClassicProjectileModel>().key==key,"Native tower weapon survives source removal: "+line.building+" slot"+(i+1));
                    towerRows.Add(new JObject{["raw"]=line.building,["slot"]=i+1,["key"]=key});
                }
            }
        }
        match=previous;var lantern=ChaosHut(0,"h073",new Vector2(-148,35));vampire.position=new Vector2(-138,35);match.TickBuilding(lantern,.05f);match.TickBuilding(lantern,.05f);var fired=match.Shots.FirstOrDefault(s=>s.sourceId==lantern.id);
        Check(fired!=null,"Lantern creates an authoritative ordinary weapon shot");RenderMatch();var shotView=projectiles[fired.id];var firedObject=shotView.NativeObject;Check(firedObject&&firedObject.GetComponent<ClassicProjectileModel>().key==(string)classicProjectileIndex["weapons"]?["h073_1"],"Actual Lantern flight uses the PriestMissile source model");
        match.Huts.Remove(lantern);RenderMatch();Check(shotView.NativeObject==firedObject,"Selling a tower mid-flight does not switch its projectile to a generic sphere");
        match.Shots.Clear();RenderMatch();
        var source=JObject.Parse(ProjectileBillboardData.Json);var artRows=new JArray();var keys=((JObject)classicProjectileIndex["weapons"]).Properties().Select(p=>(string)p.Value).Distinct().ToArray();
        foreach(string key in keys){
            var obj=Instantiate(ClassicProjectile(key,0),visuals.transform);obj.transform.position=World(new Vector2(-148,29))+Vector3.up*3;var model=obj.GetComponent<ClassicProjectileModel>();yield return new WaitForEndOfFrame();
            int expected=(source[key] as JArray)?.Count??0;if(expected>0)Check(model.BillboardCount>0&&model.BillboardAlignment(Camera.main)>.99f,"Original camera-facing missile surfaces: "+key);
            artRows.Add(new JObject{["key"]=key,["sourceBillboards"]=expected,["boundBillboards"]=model.BillboardCount,["alignment"]=model.BillboardAlignment(Camera.main)});Destroy(obj);
        }
        var demo=Instantiate(ClassicProjectile((string)classicProjectileIndex["weapons"]["h073_1"],0),visuals.transform);demo.transform.position=World(new Vector2(-148,29))+Vector3.up*3;var visual=demo.GetComponent<ClassicProjectileModel>();yield return null;
        var emitters=demo.GetComponentsInChildren<NativeMapEmitter>();Check(emitters.Length==2&&!emitters[0].Emitting&&emitters[1].Emitting,"Lantern Stand phase uses the original flight emitter visibility");
        bool cameraWasEnabled=controls.enabled;controls.enabled=false;var camera=Camera.main;var focus=demo.transform.position;var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;
        var angles=new[]{new Vector3(0,9,-13),new Vector3(13,8,0),new Vector3(-9,12,9)};
        for(int i=0;i<angles.Length;i++){camera.transform.position=focus+angles[i];camera.transform.LookAt(focus);yield return new WaitForEndOfFrame();Check(visual.BillboardAlignment(camera)>.99f,"Lantern glow faces camera from view "+i);yield return Shot(folder,"02-lantern-view-"+i);}
        float tail=visual.Finish();Check(tail>0&&tail<=1.5f&&emitters[0].Emitting&&!emitters[1].Emitting,"Lantern impact switches to original Death particles instead of keeping the Stand interval");
        yield return Shot(folder,"03-lantern-impact");yield return new WaitForSecondsRealtime(.6f);Check(emitters.All(e=>!e.Emitting),"Nonlooping Death emitters stop after their native interval");Destroy(demo);
        camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);controls.enabled=cameraWasEnabled;MainMenu();
        var result=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["towerWeapons"]=towerRows,["models"]=artRows,["androidDeviceTest"]=false};File.WriteAllText(Path.Combine(folder,"cards025-verification.json"),result.ToString());Debug.Log("CF_CARDS025_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}
