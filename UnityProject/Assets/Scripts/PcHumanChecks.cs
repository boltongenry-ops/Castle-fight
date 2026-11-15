using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    bool testHoldAuthority;
    HumanMatch.Hut QaBuild(int kind,int x,int y) {
        Check(match.TryBuild(0,kind,x,y,out var error),"human roster build "+match.Lines[kind].building+" "+error);
        var h=match.Huts.Last();var b=match.Builders[0];b.position=b.previous=h.position+new Vector2(0,-Mathf.Sign(h.position.y)*2.4f);
        for(int i=0;i<40&&!h.paid;i++)Tick(.05f);Check(h.paid,"human roster completes "+match.Lines[kind].building);return h;
    }
    void QaSelect(HumanMatch.Hut h){selectedHut=h.id;selectedUnit=-1;builderSelected=false;chosen=-1;buildMenu=false;}
    IEnumerator ExerciseHumanRoster(string folder) {
        startGold=1000;NewMatch();testHoldAuthority=true;match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;
        foreach(int i in match.BaseKinds){int n=System.Array.IndexOf(match.BaseKinds,i);QaBuild(i,2+n%6*4,15+n/6*4);}
        Check(match.Huts.Count==11&&match.LegendaryUsed(0)==1,"all 11 Human starting buildings playable");
        int index=0;foreach(var line in match.Lines.Where(l=>l.Producer)) {
            int k=match.Kind(line.building);var p=new Vector2(-191+index*7.3f,60);var u=match.Spawn(0,k,p);u.onRoad=true;u.pose="Stand";index++;
        }
        RenderMatch();Check(units.Values.Select(v=>v.raw).Distinct().Count()==11,"all 11 Human unit models instantiated");
        controls.FocusWorld(World(new Vector2(-151,39)),97);FocusBuilder();controls.FocusWorld(World(new Vector2(-151,39)),97);
        yield return Action("builder.build");yield return Shot(folder,"12-human-buildings");
        var barracks=match.Huts.First(h=>h.kind==match.Kind("h000"));QaSelect(barracks);yield return Action("upgrade.h039");Check(barracks.kind==match.Kind("h039")&&barracks.spent==275,"guard upgrade through command card");
        var nest=match.Huts.First(h=>h.kind==match.Kind("h003"));QaSelect(nest);yield return Action("upgrade.h0A1");Check(nest.kind==match.Kind("h0A1"),"sniper anti-air branch through card");RenderMatch();Check(huts[nest.id].GetComponentsInChildren<PcModel>().Length==5,"native sniper nest uses four visible detail parts");
        var second=QaBuild(match.Kind("h003"),22,19);QaSelect(second);yield return Action("upgrade.h05D");Check(second.kind==match.Kind("h05D"),"cannon branch through card");
        var chapel=match.Huts.First(h=>h.kind==match.Kind("h037"));QaSelect(chapel);yield return Action("upgrade.h038");Check(chapel.kind==match.Kind("h038"),"paladin cathedral upgrade through card");
        yield return Action("upgrade.h072");Check(chapel.kind==match.Kind("h038"),"legendary upgrade blocked while shrine occupies slot");
        var shrine=match.Huts.First(h=>h.kind==match.Kind("h05G"));QaSelect(shrine);yield return Action("building.sell");Check(match.LegendaryUsed(0)==0&&!match.Huts.Contains(shrine),"sell releases legendary slot");
        QaSelect(chapel);yield return Action("upgrade.h072");Check(chapel.kind==match.Kind("h072")&&chapel.spent==725,"holy warrior legendary branch and cumulative price");
        yield return Action("building.pause");float timer=chapel.spawnLeft;for(int i=0;i<20;i++)Tick(.05f);Check(chapel.paused&&chapel.spawnLeft==timer,"command card pauses producer clock");yield return Action("building.pause");Check(!chapel.paused,"command card resumes producer");
        RenderMatch();Check(huts[chapel.id].raw=="h072"&&huts[barracks.id].raw=="h039","upgrades replace persisted building models");controls.FocusWorld(World(new Vector2(-151,39)),97);yield return Shot(folder,"13-human-upgrades");
        NewMatch();testHoldAuthority=true;match.Gold[1]=0;
        var wizard=match.Spawn(0,match.Kind("h00K"),new Vector2(-10,0));
        var crusader=match.Spawn(0,match.Kind("h037"),new Vector2(-8,1));
        var paladin=match.Spawn(0,match.Kind("h038"),new Vector2(-6,-1));
        var saint=match.Spawn(0,match.Kind("h072"),new Vector2(-4,0));
        var wounded=match.Spawn(0,0,new Vector2(-5,1));wounded.hp=100;
        match.Spawn(0,3,new Vector2(-2,0));match.Spawn(0,2,new Vector2(-16,0));match.Spawn(0,1,new Vector2(-13,0));
        for(int i=0;i<6;i++)match.Spawn(1,i%2==0?0:match.Kind("h039"),new Vector2(5+i*.8f,i%3-1));match.Spawn(1,3,new Vector2(8,0));
        foreach(var u in match.Units)u.onRoad=true;
        for(int i=0;i<10;i++)Tick(.05f);RenderMatch();selectedHut=-1;builderSelected=false;selectedUnit=paladin.id;
        controls.FocusWorld(World(Vector2.zero),38);yield return Shot(folder,"14-human-spells");
        for(int i=0;i<25;i++)Tick(.05f);RenderMatch();Check(match.WarlockBlasts>0&&match.Heals>0&&match.Prayers>0,"human spell handlers active in native player");
        Check(wizard.warlockPhase==1&&wizard.position.x< -10,"warlock visibly retreats after blast");yield return Shot(folder,"15-human-combat");testHoldAuthority=false;
    }
}
}
