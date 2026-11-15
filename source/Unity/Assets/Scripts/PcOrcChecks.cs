using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseOrcs(string folder) {
        MainMenu();yield return Action("main.play");yield return Action("setup.orc");yield return Shot(folder,"24-orc-selection");
        startGold=1000;options.hdTextures=true;yield return Action("setup.start");testHoldAuthority=true;
        Check(OrcsInMatch&&match.BaseKinds.Length==11&&match.Lines.Count(l=>l.Producer)==14,"Orc selection opens the complete Orc race catalog race catalog");
        RenderMatch();controls.Focus("river");var oldCamera=controls.transform.position;builderSelected=false;selectedCastle=true;
        yield return Action("hud.builder");Check(builderSelected&&!selectedCastle&&Vector3.Distance(oldCamera,controls.transform.position)>50&&CommandIcon(0)==HdIcon(orcPack.builderIcon),"Orc builder shortcut displays Peon icon selects worker and focuses camera");
        yield return new WaitForSecondsRealtime(.2f);yield return Shot(folder,"85-orc-builder-shortcut");yield return Shot(folder,"25-orc-builder");Debug.Log("CF_BUILDER_SHORTCUT_PLAYER_PASS");
        Check(builders.All(b=>b&&b.reforged&&b.raw=="h019"),"both teams have a Reforged Peon builder");
        match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;for(int i=0;i<5;i++)QaBuild(match.BaseKinds[i],4+i*5,15);
        RenderMatch();buildMenu=true;controls.FocusWorld(World(new Vector2(-161,30)),58);yield return new WaitForSecondsRealtime(.2f);yield return Shot(folder,"26-orc-buildings");
        Check(huts.Values.All(v=>v.reforged)&&huts.Count==5,"Orc buildings use their native Reforged geometry");
        buildMenu=false;foreach(int k in match.BaseKinds.Take(5).Where(k=>match.Lines[k].Producer))for(int t=0;t<2;t++){var u=match.Spawn(t,k,new Vector2(-166+k*8,32+t*7));u.onRoad=true;u.cooldown=1000;u.pose="Stand";}
        RenderMatch();builderSelected=false;selectedUnit=match.Units.First(u=>u.team==0).id;controls.FocusWorld(World(new Vector2(-162,36)),35);
        yield return new WaitForSecondsRealtime(.3f);yield return Shot(folder,"27-orc-reforged");
        Check(units.Values.All(v=>v.reforged)&&units.Values.Select(v=>v.raw).Distinct().Count()==4,"Grunts Kodos Trolls and Shamans use actual Reforged models");
        foreach(var view in units.Values)Check(view.clips.Any(c=>c.name.StartsWith("Walk"))&&view.clips.Any(c=>c.name.StartsWith("Attack"))&&view.clips.Any(c=>c.name.StartsWith("Death")),"Orc unit has native movement attack and death clips "+view.raw);
        options.hdTextures=true;Apply(options,false);RenderMatch();yield return new WaitForSecondsRealtime(.3f);yield return Shot(folder,"28-orc-classic");
        Check(units.Values.All(v=>v.reforged)&&builders.All(v=>v.reforged)&&huts.Values.All(v=>v.reforged),"Orc Reforged geometry remains active for every actor type");
        options.hdTextures=true;Apply(options,false);RenderMatch();selectedUnit=match.Units.First(u=>u.team==0&&match.Lines[u.kind].unit=="o007").id;
        yield return new WaitForSecondsRealtime(.3f);yield return Shot(folder,"29-orc-kodo-portrait");
        var hall=match.Huts.First(h=>h.team==0&&match.Lines[h.kind].building=="h029");QaSelect(hall);yield return Action("upgrade.h02U");RenderMatch();
        Check(match.Lines[hall.kind].building=="h02U"&&hall.spent==210&&huts[hall.id].raw=="h02U","Warrior hall upgrade changes production and Reforged building geometry");
        controls.FocusWorld(World(hall.position),35);yield return new WaitForSecondsRealtime(.2f);yield return Shot(folder,"31-orc-warrior-hall");
        for(int t=0;t<2;t++){var u=match.Spawn(t,match.Kind("h02U"),new Vector2(-162,32+t*7));u.onRoad=true;u.cooldown=1000;u.pose="Stand";}
        RenderMatch();builderSelected=false;selectedHut=-1;selectedUnit=match.Units.First(u=>u.team==0&&match.Lines[u.kind].unit=="o004").id;
        yield return new WaitForSecondsRealtime(.2f);yield return Shot(folder,"32-orc-warrior-reforged");
        Check(AbilityArt("A02V")!=null&&SelectedUnit.critChance==.25f,"Warrior critical ability has a visible source icon and exact parameters");
        QaSelect(hall);yield return Action("upgrade.h031");RenderMatch();controls.FocusWorld(World(hall.position),35);
        Check(match.Lines[hall.kind].building=="h031"&&hall.spent==450&&huts[hall.id].raw=="h031","Chieftain hall upgrade preserves all investments and replaces its view");
        yield return new WaitForSecondsRealtime(.2f);yield return Shot(folder,"33-orc-chieftain-hall");
        for(int t=0;t<2;t++){var u=match.Spawn(t,match.Kind("h031"),new Vector2(-155,32+t*7));u.onRoad=true;u.cooldown=1000;u.pose="Stand";}
        RenderMatch();builderSelected=false;selectedHut=-1;selectedUnit=match.Units.First(u=>u.team==0&&match.Lines[u.kind].unit=="o005").id;controls.FocusWorld(World(new Vector2(-162,36)),35);
        yield return new WaitForSecondsRealtime(.2f);yield return Shot(folder,"34-orc-chieftain-reforged");
        Check(AbilityArt("A02W")&&AbilityArt("A02P")&&SelectedUnit.critChance==.3f&&units.Values.Select(v=>v.raw).Distinct().Count()==6,"all six instantiated Orc unit types and both Chieftain passive icons are present");
        options.hdTextures=true;Apply(options,false);RenderMatch();yield return new WaitForSecondsRealtime(.2f);yield return Shot(folder,"36-orc-upgrades-classic");
        Check(units.Values.All(v=>v.reforged)&&units.Values.Select(v=>v.raw).Distinct().Count()==6,"Reforged geometry is available for every instantiated Orc unit");
        options.hdTextures=true;Apply(options,false);RenderMatch();foreach(var u in match.Units)u.hp=0;for(int i=0;i<70;i++)Tick(.05f);
        var leader=match.Spawn(0,match.Kind("h031"),new Vector2(-2,0));leader.onRoad=true;
        for(int i=0;i<3;i++){var enemy=match.Spawn(1,0,new Vector2(2,(i-1)*2));enemy.onRoad=true;enemy.hp=enemy.maxHp=100000;enemy.cooldown=10000;}
        for(int i=0;i<1000&&match.PulverizeProcs==0;i++)Tick(.05f);
        RenderMatch();selectedUnit=leader.id;builderSelected=false;controls.FocusWorld(World(new Vector2(1,0)),26);
        Check(match.PulverizeProcs>0&&match.Pulses.Any(p=>p.text=="Сокрушение"),"Chieftain actually triggers Pulverize in the playable match");
        yield return new WaitForSecondsRealtime(.1f);yield return Shot(folder,"35-orc-pulverize");
        foreach(var u in match.Units)u.hp=0;for(int i=0;i<70;i++)Tick(.05f);
        match.Gold[1]=1000;for(int k=0;k<2;k++)for(int t=0;t<2;t++){var u=match.Spawn(t,k,new Vector2(t==0?-9:9,k*4));u.onRoad=true;}
        for(int i=0;i<2200&&match.Kills<4;i++)Tick(.05f);RenderMatch();controls.Focus("river");builderSelected=true;selectedUnit=-1;
        Check(match.Kills>0&&match.Huts.Any(h=>h.team==1),"Orc bot builds and automatic combat resolves damage and kills");
        yield return new WaitForSecondsRealtime(.3f);yield return Shot(folder,"30-orc-battle");
        Check(!match.Aspects.Any(a=>a>=0)&&match.Lines.Where(l=>!new[]{"h029","h02U","h02H","h05E","h02B","h02K"}.Contains(l.building)).All(l=>l.upgrades.Length==0),"remaining Orc upgrade branches and aspects stay unavailable");
        yield return ExerciseOrcTrolls(folder);
        yield return ExerciseOrcShamans(folder);
        yield return ExerciseOrcKodos(folder);
        yield return ExerciseOrcCompletion(folder);
        MainMenu();yield return Action("main.play");yield return Action("setup.human");yield return Action("setup.start");RenderMatch();
        Check(!OrcsInMatch&&match.Lines.Length==16&&builders[0].raw=="h00C","returning to Humans restores its roster and builder");
        Debug.Log("CF_ORC_PLAYER_PASS stage=6 units=14 buildings=19 hd=true classic=true");
    }
}
}
