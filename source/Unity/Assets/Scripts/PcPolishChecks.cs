using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExercisePolishOnly(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"polish-validation"));Directory.CreateDirectory(folder);
        MainMenu();aspectsOn=false;matchMode=0;raceChoice=0;botRaceChoice=0;
        yield return Action("main.play");yield return Action("setup.start");testHoldAuthority=true;RenderMatch();yield return new WaitForEndOfFrame();
        Check(portraitModel&&portraitModel.gameObject.activeSelf,"Match builder portrait exists before opening menu");
        MainMenu();yield return Action("main.play");yield return Action("setup.orc");yield return new WaitForEndOfFrame();
        Check(menuPortraitModel&&menuPortraitModel.raw=="h019"&&!portraitModel,"Menu destroys previous match portrait before rendering Orc builder");
        Check(portraitStage.GetComponentsInChildren<PcModel>().Count(m=>m.gameObject.activeInHierarchy)==1,"Exactly one active model in portrait stage");
        aspectsOn=true;roundsChoice=7;matchMode=1;yield return new WaitForEndOfFrame();
        var summary=new GUIStyle(raceSummaryStyle){wordWrap=true};Check(summary.CalcHeight(new GUIContent("До 4 побед · армия до 160\n1 спасательный удар на сторону за раунд.\nСлучайная раса для каждой стороны в каждом раунде.\nАспекты: Кровь и честь / Шаманская поддержка"),314)>52,"Long battle description exceeds viewport and requires scroll");
        raceRulesScroll=new Vector2(0,1000);yield return Shot(folder,"01-menu-orc-description-scrolled");Check(raceRulesScroll.y>0,"Battle description scroll position reaches hidden last lines");
        yield return Action("setup.night");yield return new WaitForEndOfFrame();Check(menuPortraitModel.raw=="h089"&&portraitStage.GetComponentsInChildren<PcModel>().Count(m=>m.gameObject.activeInHierarchy)==1,"Switching races keeps exactly one menu model");yield return Shot(folder,"02-menu-night-single-model");
        aspectsOn=false;matchMode=0;raceChoice=0;botRaceChoice=0;roundsChoice=1;yield return Action("setup.start");testHoldAuthority=true;RenderMatch();yield return new WaitForEndOfFrame();
        Check(portraitModel&&!menuPortraitModel,"Returning to match removes menu preview model");
        var at=new Vector2(-148,32);match.Builders[0].position=at;FocusBuilder();match.Gold[0]=10000;
        Check(RescueVisible&&match.Rescue(0,at)&&match.RescueCharges[0]==0&&!RescueVisible,"Last explosion charge is consumed and its command disappears");
        BeginBuilderSpell("rescue");Check(targetedSpell==null,"Hotkey cannot arm an exhausted explosion");yield return Shot(folder,"03-explosion-consumed");
        SelectCastle(false);yield return Action("builder.buy-rescue");FocusBuilder();Check(RescueVisible&&match.RescueCharges[0]==1,"Shop repurchase restores explosion command");yield return Shot(folder,"04-explosion-repurchased");
        ChaosClear();var a=match.Spawn(0,match.Kind("h000"),at);var b=match.Spawn(0,match.Kind("h000"),at+Vector2.right*5);var c=match.Spawn(1,match.Kind("h000"),at+Vector2.up*5);RenderMatch();controls.FocusWorld(World(at),46);yield return null;
        var pa=UnitSelectionPoint(a);var pb=UnitSelectionPoint(b);var pc=UnitSelectionPoint(c);var rect=SelectionRect((Vector2)pa-Vector2.one*15,(Vector2)pb+Vector2.one*15);rect=Rect.MinMaxRect(Mathf.Min(rect.xMin,pc.x-15),Mathf.Min(rect.yMin,pc.y-15),Mathf.Max(rect.xMax,pc.x+15),Mathf.Max(rect.yMax,pc.y+15));
        SelectScreenGroup(rect,false);Check(selectedGroup.SequenceEqual(new[]{a.id,b.id})&&!builderSelected,"Marquee selects friendly group without mixing enemy in same box");RenderMatch();Check(groupRings.Count==2,"Each selected fighter has its own ring");
        selectionStart=rect.min;selectionEnd=rect.max;selectionPending=selectionDragging=true;yield return Shot(folder,"05-selection-marquee");selectionPending=selectionDragging=false;
        SelectScreenGroup(new Rect(pc.x-8,pc.y-8,16,16),false);Check(selectedGroup.SequenceEqual(new[]{c.id}),"Enemy-only rectangle can acquire enemy target for inspection");yield return Shot(folder,"06-enemy-selection");
        SetSelectedGroup(new[]{a.id});SelectScreenGroup(new Rect(pb.x-8,pb.y-8,16,16),true);Check(selectedGroup.Count==2&&selectedGroup.Contains(a.id)&&selectedGroup.Contains(b.id),"Shift adds a second fighter to existing group");
        var many=Enumerable.Range(0,14).Select(i=>match.Spawn(0,match.Kind("h000"),at+new Vector2(i%7*2,i/7*3))).ToArray();SetSelectedGroup(many.Select(u=>u.id));Check(selectedGroup.Count==12,"Command group caps twelve fighters");many[0].hp=0;UpdateGroupRings();Check(selectedGroup.Count==11&&!selectedGroup.Contains(many[0].id),"Dead fighters leave group and selection rings");FocusBuilder();Check(selectedGroup.Count==0&&groupRings.Count==0,"F1 clears group and returns to builder");
        Check(NativeMapCamera.EdgeMotion(new Vector2(640,360),1280,720)==Vector2.zero,"Camera remains still away from screen edges");
        Check(NativeMapCamera.EdgeMotion(new Vector2(-1,360),1280,720)==Vector2.zero,"Camera does not pan when cursor is outside window");
        var mild=NativeMapCamera.EdgeMotion(new Vector2(10,360),1280,720);var strong=NativeMapCamera.EdgeMotion(new Vector2(0,360),1280,720);Check(mild.x<0&&Mathf.Abs(mild.x)<Mathf.Abs(strong.x)&&strong.x==-1,"Edge pan speed responds progressively to cursor distance");
        Check(NativeMapCamera.EdgeMotion(new Vector2(0,0),1280,720).magnitude<=1.001f,"Diagonal edge pan has no extra speed");
        MainMenu();yield return Action("main.play");raceChoice=9;botRaceChoice=9;yield return Action("setup.start");testHoldAuthority=true;ElementalClear();match.Gold[0]=match.Gold[1]=0;
        foreach(int side in new[]{0,1})foreach(string raw in new[]{"h04E","h03Z"}){var h=ChaosHut(side,raw,at+Vector2.right*side*40);h.spawnLeft=.01f;h.mana=0;}
        Check(!match.Lines.Any(l=>HumanMatch.RemovedElementalCompanion(l.unit))&&!elementalPack.Prefabs.Any(p=>HumanMatch.RemovedElementalCompanion(p.GetComponent<PcModel>().raw)),"Removed primordial types and models are absent from runtime catalog and race pack");Check(!ContentResources.LoadAll<GameObject>("").Any(p=>p.GetComponent<PcModel>() is PcModel model&&HumanMatch.RemovedElementalCompanion(model.raw)),"No removed primordial model is directly loadable from packaged Resources");Tick(.05f);Check(match.Units.Count==4&&match.Units.Count(u=>match.Lines[u.kind].unit=="h03V")==2&&match.Units.Count(u=>match.Lines[u.kind].unit=="h02Y")==2&&!match.Units.Any(u=>HumanMatch.RemovedElementalCompanion(match.Lines[u.kind].unit)),"Both teams produce only main primordial fire/water actors");
        RenderMatch();controls.FocusWorld(World(at+Vector2.right*10),58);yield return Shot(folder,"07-primordials-without-giants");
        var result=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["build"]=Version};File.WriteAllText(Path.Combine(folder,"polish-verification.json"),result.ToString());Debug.Log("CF_POLISH_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}
