using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseSpellAudit036(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"spell-audit036"));
        Directory.CreateDirectory(folder);
        MainMenu();raceChoice=botRaceChoice=0;aspectsOn=false;NewMatch();testHoldAuthority=true;options.fogOfWar=true;
        int kind=match.Kind("h000");match.Gold[0]=10000;
        Select(kind);var p=HumanMatch.Plot(0,7,15);testPreviewCell=new Vector2Int(7,15);UpdatePlacement();testPreviewCell=null;
        Check(!Place(-1,0)&&chosen==kind&&buildMenu,"Invalid building tap keeps selection for retry");
        Check(Place(7,15)&&chosen<0&&!buildMenu&&builderSelected&&!hoverValid&&!preview.activeSelf&&!localGrid.activeSelf,"One accepted placement closes build menu and ghost immediately");
        Check(!Place(9,15)&&match.Huts.Count==1,"A second legal tile does not queue a duplicate");
        Select(kind);Check(!Place(7,15)&&chosen==kind&&match.Huts.Count==1,"Occupied tile keeps the order unsent and permits retry");chosen=-1;buildMenu=false;
        var hut=match.Huts.First();match.Builders[0].position=match.Builders[0].previous=p;
        for(int i=0;i<400&&!hut.paid;i++)match.Step(.05f);
        Check(hut.paid&&match.Huts.Count(h=>h.team==0)==1,"The originally placed building completes normally");
        var ground=HumanMatch.Plot(0,12,17);controls.FocusWorld(World(ground),40);yield return null;
        touchPointer=Camera.main.WorldToScreenPoint(World(ground));explicitTouchPointer=true;
        try{HandleWorldTap();}finally{explicitTouchPointer=false;}
        Check(builderSelected&&Vector2.Distance(match.Builders[0].goal,ground)<2&&match.Huts.Count(h=>h.team==0)==1,"The next world tap follows the mobile movement path");
        Select(kind);onlineMatch=true;onlineStarted=false;
        Check(!Place(9,15)&&chosen==kind&&buildMenu,"A blocked network send leaves the placement available");
        onlineMatch=false;chosen=-1;buildMenu=false;

        match.Pulses.Clear();var blinkFrom=match.Builders[0].position;
        Check(match.Blink(0,p+new Vector2(4,4)),"Actual Blink emits a departure and an arrival");
        match.PulseAt(p,7,Color.cyan,"",1.267f,"wisp");
        match.PulseAt(p+Vector2.left*5,2,Color.yellow,"Воскрешение");
        match.PulseAt(p+Vector2.right*5,2,Color.yellow,"",1f,"resurrect_caster");
        match.PulseAt(p+Vector2.up*5,1,Color.green,"+25",.8f,"heal_direct");
        match.PulseAt(p+Vector2.down*5,1,Color.cyan,"Fallback",1f,"elemental:unknown:effect");
        match.PulseAt(Vector2.zero,5,Color.cyan,"Hidden",1.267f,"wisp");
        UpdateFog(true);RenderMatch();
        var visiblePulses=match.Pulses.Where(x=>x.text!="Fallback"&&x.text!="Hidden").ToArray();
        Check(visiblePulses.Length==6&&visiblePulses.All(x=>Art036.casts.TryGetValue(x.id,out var view)&&view&&view.geometry&&view.clips.Any(c=>c.name.StartsWith("Birth")||c.name.StartsWith("Stand"))&&!humanEffects.ContainsKey(x.id)),"Six classic cast prefabs play their source one-shots without duplicate rings");
        Check(match.Pulses.Single(x=>x.text=="Скачок").position==blinkFrom,"Blink departure remains at the point of origin");
        int fallback=match.Pulses.Single(x=>x.text=="Fallback").id,hidden=match.Pulses.Single(x=>x.text=="Hidden").id;
        Check(humanEffects.TryGetValue(fallback,out var ring)&&ring.gameObject.activeSelf,"Missing named effect falls back to visible ring");
        Check(!Art036.casts.ContainsKey(hidden)&&!humanEffects.ContainsKey(hidden),"An unseen cast creates neither mesh nor ring");
        Check(Art036.casts.Values.All(v=>v.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m&&m.shader&&m.shader.isSupported&&m.mainTexture))),"Active native spell materials support the standalone renderer");
        controls.FocusWorld(World(p),38);yield return new WaitForSecondsRealtime(.3f);yield return Shot(folder,"classic-spells");
        match.Pulses.Clear();RenderMatch();Check(Art036.casts.Count==0&&humanEffects.Count==0,"Expired spell visuals are removed");

        var enemy=match.Spawn(1,kind,p+Vector2.up*5);var snare=new HumanMatch.Effect{id="A06H",left=3,root=true};
        var roots=new HumanMatch.Effect{id="A08A",left=3,root=true};
        enemy.effects.Add(roots);RenderMatch();Check(!netViews.ContainsKey(enemy.id),"Entangling Roots does not draw an orc cage");
        enemy.effects.Add(snare);RenderMatch();Check(netViews.ContainsKey(enemy.id),"Ensnare retains its visible cage");
        enemy.nightInvisible=true;RenderMatch();Check(!units[enemy.id].gameObject.activeSelf&&!netViews.ContainsKey(enemy.id),"An invisible fighter cannot be exposed by its net");
        enemy.nightInvisible=false;var old=units[enemy.id];enemy.team=0;RenderMatch();
        Check(units[enemy.id]!=old&&!old.gameObject.activeSelf&&unitViewTeams[enemy.id]==0,"Converted fighter immediately uses the new team's model");
        old=units[enemy.id];enemy.kind=match.Kind("h003");RenderMatch();
        Check(units[enemy.id]!=old&&!old.gameObject.activeSelf&&Art036.unitKinds[enemy.id]==enemy.kind,"Changing a fighter's form replaces the previous model under the same id");
        var missing=match.Spawn(0,kind,p+Vector2.right*8);missing.kind=match.Kind("h001");RenderMatch();Check(!units.ContainsKey(missing.id),"Missing actor prefab is safely skipped");match.Units.Remove(missing);
        var beam=new HumanMatch.Shot{kind=4,start=p,end=Vector2.zero,sourceId=enemy.id,target=-1};
        Check(!ShowBeam036(beam),"A beam with an endpoint in the fog stays hidden");
        var oldHut=huts[hut.id];hut.team=1;RenderMatch();
        Check(huts[hut.id]!=oldHut&&!oldHut.gameObject.activeSelf&&Art036.hutTeams[hut.id]==1,"Captured building immediately uses the new team's model");
        var report=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["version"]=Version,["checks"]=testChecks,["failures"]=testFailures};
        File.WriteAllText(Path.Combine(folder,"spell-audit036-verification.json"),report.ToString());
        Debug.Log("CF_SPELL036_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);
        Application.Quit(testFailures==0?0:1);
    }
}
}
