using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseOrcTrolls(string folder) {
        NewMatch();testHoldAuthority=true;options.hdTextures=true;match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;
        var hut=QaBuild(match.Kind("h02H"),7,15);QaSelect(hut);RenderMatch();controls.FocusWorld(World(hut.position),37);
        Check(match.Lines[hut.kind].unit=="n00V"&&huts[hut.id].reforged,"initial Troll hut is playable with its native Reforged model");
        yield return Shot(folder,"40-troll-hut");
        foreach(int team in new[]{0,1}){var u=match.Spawn(team,match.Kind("h02H"),new Vector2(-165,34+team*6));u.onRoad=true;u.cooldown=10000;u.pose="Stand";}
        yield return Action("upgrade.h05E");RenderMatch();Check(hut.spent==280&&match.Lines[hut.kind].unit=="n01O"&&huts[hut.id].raw=="h05E","Troll trapper upgrade through the command card changes production and art");
        yield return Shot(folder,"41-trapper-hut");
        foreach(int team in new[]{0,1}){var u=match.Spawn(team,match.Kind("h05E"),new Vector2(-159,34+team*6));u.onRoad=true;u.cooldown=10000;u.pose="Stand";}
        RenderMatch();builderSelected=false;selectedHut=-1;selectedUnit=match.Units.First(u=>u.team==0&&match.Lines[u.kind].unit=="n01O").id;controls.FocusWorld(World(new Vector2(-158,36)),34);
        Check(AbilityArt("A06I")&&AbilityArt("A02I"),"trapper net and kill-frenzy source icons are available");yield return Shot(folder,"42-trapper-reforged");
        QaSelect(hut);yield return Action("upgrade.h06D");RenderMatch();controls.FocusWorld(World(hut.position),37);
        Check(hut.spent==770&&match.LegendaryUsed(0)==1&&match.Lines[hut.kind].unit=="n02G"&&huts[hut.id].raw=="h06D","legendary Troll upgrade consumes one slot and all 770 invested gold are preserved");
        yield return Shot(folder,"43-troll-leader-hut");
        foreach(int team in new[]{0,1}){var u=match.Spawn(team,match.Kind("h06D"),new Vector2(-153,34+team*6));u.onRoad=true;u.cooldown=10000;u.pose="Stand";}
        RenderMatch();builderSelected=false;selectedHut=-1;selectedUnit=match.Units.First(u=>u.team==0&&match.Lines[u.kind].unit=="n02G").id;controls.FocusWorld(World(new Vector2(-158,36)),34);
        Check(AbilityArt("A0FB")&&AbilityArt("A0AG")&&SelectedUnit.spellResist>.69f&&SelectedUnit.critChance==.15f,"Troll leader receives five-target nets resistance and its native critical profile");
        Check(units.Values.All(v=>v.reforged)&&units.Values.Select(v=>v.raw).Distinct().Count()==3,"all three Troll tiers use actual Reforged models with both team colors");yield return Shot(folder,"44-troll-leader-reforged");
        options.hdTextures=true;Apply(options,false);RenderMatch();yield return Shot(folder,"45-trolls-classic");
        Check(units.Values.All(v=>v.reforged)&&units.Values.Select(v=>v.raw).Distinct().Count()==3,"all three Troll tiers retain Reforged geometry");options.hdTextures=true;Apply(options,false);

        NewMatch();testHoldAuthority=true;match.Gold[1]=0;foreach(var l in match.Lines){l.speed=0;l.regen=0;}
        var troll=match.Spawn(0,match.Kind("h02H"),new Vector2(-6,0));troll.onRoad=true;
        var victim=match.Spawn(1,0,new Vector2(0,0));victim.onRoad=true;victim.hp=1;victim.cooldown=10000;
        for(int i=0;i<100&&match.OrcFrenzies==0;i++)Tick(.05f);
        RenderMatch();builderSelected=false;selectedUnit=troll.id;controls.FocusWorld(World(new Vector2(-3,0)),28);
        Check(match.OrcFrenzies==1&&troll.effects.Count(e=>e.id=="A02I")==1&&match.AttackSpeed(troll)==3,"an actual Troll weapon kill triggers the visible six-second frenzy");yield return Shot(folder,"46-troll-frenzy");

        // Isolated QA opponents: current production matches still use one staged
        // race for both teams. Reuse the Human Gryphon art to verify real flyers.
        var original=JObject.Parse(orcPack.catalog.text);var humans=JObject.Parse(catalog.text);int gryphon=((JArray)humans["lines"]).ToList().FindIndex(l=>(string)l["building"]=="h015");
        var targetLine=humans["lines"][gryphon];int count=match.Lines.Length,index=count+1;
        var savedHd=orcPack.hdUnits[index];
        try {
            orcPack.hdUnits[index]=reforgedUnits[((JArray)humans["lines"]).Count+gryphon];
            foreach(string raw in new[]{"h05E","h06D"}) {
                NewMatch();testHoldAuthority=true;var fixture=(JObject)original.DeepClone();fixture["lines"][1]["stats"]=targetLine["stats"].DeepClone();fixture["lines"][1]["unit"]=targetLine["unit"];fixture["lines"][1]["unitName"]=targetLine["unitName"];
                match=new HumanMatch(fixture,0);foreach(var l in match.Lines){l.speed=0;l.regen=0;}
                var caster=match.Spawn(0,match.Kind(raw),new Vector2(-155,32));caster.onRoad=true;caster.cooldown=10000;
                var offsets=new[]{Vector2.zero,new Vector2(0,-10),new Vector2(10,-5),new Vector2(10,5),new Vector2(0,10),new Vector2(-10,-10),new Vector2(-10,10)};
                var flyers=offsets.Select(p=>match.Spawn(1,1,new Vector2(-141,32)+p)).ToArray();foreach(var u in flyers){u.onRoad=true;u.hp=u.maxHp=100000;u.cooldown=10000;}flyers[0].spellResist=1;
                string spell=raw=="h05E"?"A06I":"A0FB";int targets=raw=="h05E"?2:5;
                Check(match.Cast(caster,spell,flyers[0].id)&&match.OrcNetShots==targets&&flyers.All(match.Flying),"nets launch at "+targets+" real Gryphon QA opponents without applying control early");
                for(int i=0;i<4;i++)Tick(.05f);RenderMatch();builderSelected=false;selectedUnit=caster.id;controls.FocusWorld(World(new Vector2(-144,32)),58);
                Check(projectiles.Values.Any(v=>v.NativeModel&&v.NativeModel.raw=="fx_proj_net_missile"),"in-flight net uses original Reforged projectile model");
                if(targets==5)yield return Shot(folder,"47-net-missiles");
                for(int i=0;i<12;i++)Tick(.05f);match.DamageUnit(caster,100000,1,"chaos",true,true);RenderMatch();selectedUnit=flyers[0].id;
                Check(match.OrcNetHits==targets&&flyers.Count(match.Rooted)==targets&&(netViews.Count+reforgedNets.Count)==targets,"net impact grounds exactly "+targets+" flyers and creates persistent visible cages");
                Check(reforgedAirNets.Count==targets,"original Reforged air nets attach to every grounded target");
                Check(!match.Flying(flyers[0])&&match.Height(flyers[0])==0&&flyers[0].hp==100000,"physical net can ground magic immunity without dealing damage");
                yield return Shot(folder,targets==5?"48-net-grounded":"50-trapper-two-nets");
                for(int i=0;i<330;i++)Tick(.05f);RenderMatch();
                Check(flyers.All(match.Flying)&&netViews.Count==0&&reforgedNets.Count==0&&reforgedAirNets.Count==0,"expiration removes every net cage and restores airborne presentation");
                if(targets==5)yield return Shot(folder,"49-net-flight-restored");
            }
        } finally {orcPack.hdUnits[index]=savedHd;}
        Debug.Log("CF_ORC_TROLL_PLAYER_PASS tiers=3 frenzy=true nets=2,5 hd=true qaOpponent=HumanGryphon");
    }
}
}
