using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseOrcKodos(string folder) {
        NewMatch();testHoldAuthority=true;options.hdTextures=true;match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;
        string[] codes={"h092","h035","h036"};string[] shots={"59-enchanted-kodo","60-drum-kodo","61-armored-kodo"};
        for(int i=0;i<3;i++) {
            var farm=QaBuild(match.Kind("h02K"),7+i*5,15);QaSelect(farm);RenderMatch();controls.FocusWorld(World(farm.position),36);
            if(i==0){Check(match.Lines[farm.kind].upgrades.SequenceEqual(codes),"Kodo farm exposes all three mutually exclusive upgrades in the card");yield return Shot(folder,"58-kodo-upgrade-menu");}
            yield return Action("upgrade."+codes[i]);RenderMatch();Check(farm.spent==360&&match.Lines[farm.kind].building==codes[i]&&huts[farm.id].reforged,"Kodo branch upgrades through command card using Reforged art "+codes[i]);
            Check(BuildingEffectAnchor(farm,huts[farm.id],1).name=="Sprite second Ref","Kodo decoration uses the original second sprite attachment "+codes[i]);
            if(i==0){Check(reforgedFx.ContainsKey("magic_sentry"),"enchanted Kodo building uses the original Magic Sentry effect");yield return Shot(folder,"89-enchanted-building-effect");}
            if(i==1)Check(reforgedFx.ContainsKey("orc_damage_aura"),"drummer building uses the original Curse decoration");
            if(i==2){Check(orcDecorModels.ContainsKey(farm.id+"/aura_armor/0")&&orcDecorModels.ContainsKey(farm.id+"/aura_armor/1")&&armorAuraMaterial&&armorAuraMaterial.GetInt("_DstBlend")==10,"armored Kodo building has two transparent native shield decorations");yield return Shot(folder,"90-armor-building-effect");}
            for(int t=0;t<2;t++){var u=match.Spawn(t,match.Kind(codes[i]),new Vector2(-165+i*7,32+t*7));u.onRoad=true;u.cooldown=10000;u.pose="Stand";}
            RenderMatch();builderSelected=false;selectedHut=-1;selectedUnit=match.Units.First(u=>u.team==0&&u.kind==match.Kind(codes[i])).id;controls.FocusWorld(World(new Vector2(-158,35)),40);yield return Shot(folder,shots[i]);
        }
        Check(units.Count==6&&units.Values.All(v=>v.reforged)&&AbilityArt("A08O")&&AbilityArt("Aakb")&&AbilityArt("A034"),"all Kodo upgrades have both Reforged team colors and source passive icons");
        options.hdTextures=true;Apply(options,false);RenderMatch();yield return Shot(folder,"62-kodos-classic");Check(units.Values.All(v=>v.reforged)&&huts.Values.All(v=>v.reforged),"Kodo Reforged models remain present");options.hdTextures=true;Apply(options,false);RenderMatch();
        var armoredFarm=match.Huts.First(h=>match.Lines[h.kind].building=="h036");int soldId=armoredFarm.id;Check(orcDecorModels.Keys.Any(k=>k.StartsWith(soldId+"/"))&&match.Sell(0,soldId),"armored Kodo farm can be sold while its decoration is active");RenderMatch();Check(!orcDecorModels.Keys.Any(k=>k.StartsWith(soldId+"/"))&&!orcDecorAnchors.ContainsKey(soldId),"sale removes the building decoration and cached anchors");
        NewMatch();testHoldAuthority=true;match.Gold[1]=0;foreach(var l in match.Lines){l.speed=0;l.regen=0;}
        var drum=match.Spawn(0,match.Kind("h035"),new Vector2(-156,30));var armor=match.Spawn(0,match.Kind("h036"),new Vector2(-151,30));var grunt=match.Spawn(0,0,new Vector2(-146,30));foreach(var u in match.Units){u.onRoad=true;u.cooldown=10000;}
        for(int i=0;i<15;i++)Tick(.05f);RenderMatch();builderSelected=false;selectedHut=-1;selectedUnit=grunt.id;controls.FocusWorld(World(new Vector2(-151,30)),36);
        Check(Mathf.Abs(grunt.auraDamage-.1f)<.001f&&Mathf.Abs(match.Stat(grunt,"armor")-4)<.001f&&grunt.auraIds.Contains("Aakb")&&grunt.auraIds.Contains("A034"),"different Kodo auras retain source IDs and increase allied damage and armor in the player");yield return Shot(folder,"63-kodo-auras");
        Debug.Log("CF_ORC_KODO_PLAYER_PASS upgrades=3 auraDamage=10percent auraArmor=4 hd=true classic=true");
    }
}
}
