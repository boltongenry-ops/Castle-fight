using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseOrcShamans(string folder) {
        NewMatch();testHoldAuthority=true;options.hdTextures=true;match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;
        var lodge=QaBuild(match.Kind("h02B"),7,15);QaSelect(lodge);RenderMatch();controls.FocusWorld(World(lodge.position),37);
        Check(lodge.spent==170&&huts[lodge.id].raw=="h02B"&&huts[lodge.id].reforged,"Shaman lodge is playable with its Reforged geometry and source price");yield return Shot(folder,"51-shaman-lodge");
        for(int t=0;t<2;t++){var u=match.Spawn(t,match.Kind("h02B"),new Vector2(-165,34+t*7));u.onRoad=true;u.cooldown=10000;u.pose="Stand";}
        yield return Action("upgrade.h06I");RenderMatch();
        Check(lodge.spent==340&&match.Lines[lodge.kind].unit=="o00F"&&huts[lodge.id].raw=="h06I"&&match.LegendaryUsed(0)==0,"command card upgrades Shaman lodge to magic stealers for another 170 gold");
        Check(orcDecorModels.ContainsKey(lodge.id+"/orc_rune/0")&&orcRuneMaterial&&orcRuneMaterial.GetInt("_DstBlend")==10&&reforgedFx.ContainsKey("orc_rune_1")&&reforgedFx.ContainsKey("orc_rune_2"),"upgraded Shaman lodge uses transparent native rune and both imported particle layers");
        var rune=orcDecorModels[lodge.id+"/orc_rune/0"];Check(Quaternion.Angle(rune.transform.rotation,huts[lodge.id].transform.rotation*Quaternion.Euler(0,180,0))<.1f,"Shaman rune uses the original building-relative orientation");
        for(int i=0;i<10;i++)Tick(.05f);RenderMatch();yield return Shot(folder,"91-shaman-rune");yield return Shot(folder,"52-stealer-lodge");
        for(int t=0;t<2;t++){var u=match.Spawn(t,match.Kind("h06I"),new Vector2(-153,34+t*7));u.onRoad=true;u.cooldown=10000;u.pose="Stand";}
        RenderMatch();builderSelected=false;selectedHut=-1;selectedUnit=match.Units.First(u=>u.team==0&&match.Lines[u.kind].unit=="o00F").id;controls.FocusWorld(World(new Vector2(-159,37)),36);
        Check(units.Count==4&&units.Values.All(v=>v.reforged)&&AbilityArt("A02M")&&AbilityArt("A0A6"),"Shamans and stealers have Reforged models for both colors and native ability icons");
        foreach(var v in units.Values)Check(v.clips.Any(c=>c.name.StartsWith("Spell"))&&v.clips.Any(c=>c.name.StartsWith("Walk")),"native spell and movement clips exist for "+v.raw);
        yield return Shot(folder,"53-shamans-reforged");options.hdTextures=true;Apply(options,false);RenderMatch();yield return Shot(folder,"54-shamans-classic");Check(units.Values.All(v=>v.reforged)&&huts.Values.All(v=>v.reforged),"both Shaman tiers retain Reforged geometry");options.hdTextures=true;Apply(options,false);

        NewMatch();testHoldAuthority=true;match.Gold[1]=0;foreach(var l in match.Lines){l.speed=0;l.regen=0;}
        var shaman=match.Spawn(0,match.Kind("h02B"),new Vector2(-155,30));var ally=match.Spawn(0,0,new Vector2(-147,30));
        foreach(var u in match.Units){u.onRoad=true;u.cooldown=10000;u.manaRegen=0;}ally.hp=ally.maxHp*.4f;
        for(int i=0;i<30&&match.OrcInnerFires==0;i++)Tick(.05f);RenderMatch();builderSelected=false;selectedUnit=ally.id;controls.FocusWorld(World(new Vector2(-151,30)),30);
        Check(match.OrcInnerFires==1&&ally.effects.Any(e=>e.id=="A02M")&&shaman.mana==85&&match.Stat(ally,"armor")==4,"Shaman actually autocasts spiritual flame with exact mana damage and armor modifiers");yield return Shot(folder,"55-spiritual-flame");

        NewMatch();testHoldAuthority=true;match.Gold[1]=0;foreach(var l in match.Lines){l.speed=0;l.regen=0;}
        var thief=match.Spawn(0,match.Kind("h06I"),new Vector2(-155,30));var donor=match.Spawn(1,0,new Vector2(-135,30));var recipient=match.Spawn(0,0,new Vector2(-143,30));
        foreach(var u in match.Units){u.onRoad=true;u.cooldown=10000;u.abilities.Clear();u.manaRegen=0;}thief.abilities.Add("A0A6");match.ApplyEffect(donor,"A02M");donor.effects[0].left=17;
        for(int i=0;i<25&&match.OrcStealShots==0;i++)Tick(.05f);Tick(.05f);RenderMatch();builderSelected=false;selectedUnit=thief.id;controls.FocusWorld(World(new Vector2(-145,30)),35);
        Check(match.OrcStealShots==1&&match.Shots.Any(s=>s.kind==6&&!s.hit)&&donor.effects.Count==1&&recipient.effects.Count==0,"stealer automatically launches a visible missile while the donor retains its buff");yield return Shot(folder,"56-spell-steal-flight");
        for(int i=0;i<12&&match.OrcStealHits==0;i++)Tick(.05f);RenderMatch();selectedUnit=recipient.id;
        Check(match.OrcStealHits==1&&donor.effects.Count==0&&recipient.effects.Any(e=>e.id=="A02M"&&e.left<17&&e.left>15)&&thief.mana==160,"spell steal transfers on actual missile impact and keeps the remaining duration");
        var beforeMove=EffectAnchor(recipient,units[recipient.id],4);recipient.position+=new Vector2(1,0);recipient.previous=recipient.position;RenderMatch();var afterMove=EffectAnchor(recipient,units[recipient.id],4);
        Check(match.Pulses.Any(p=>p.fx=="spellsteal"&&p.visualUnitId==recipient.id)&&afterMove.y>units[recipient.id].transform.position.y&&Vector3.Distance(beforeMove,afterMove)>.5f,"spell steal impact follows the moving recipient overhead attachment");yield return Shot(folder,"57-spell-steal-impact");
        Debug.Log("CF_ORC_SHAMAN_PLAYER_PASS stages=2 innerFire=true spellSteal=true hd=true classic=true");
    }
}
}
