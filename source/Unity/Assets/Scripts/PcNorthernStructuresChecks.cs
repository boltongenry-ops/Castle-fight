using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator NorthernStructuresChecks(string folder) {
        NorthClear();var launcher=ChaosHut(0,"h048",new Vector2(-148,32));var target=ChaosHut(1,"h000",new Vector2(-138,32));var neighbour=ChaosHut(1,"h004",new Vector2(-136,33));float hp=target.hp,mana=launcher.mana;
        Check(match.CastNorthernBuilding(launcher,"A04J",target.id)&&launcher.mana==mana-12&&target.hp==hp,"Small frost launcher spends twelve mana and releases a real delayed projectile");launcher.paused=true;
        Check(match.Shots.Last().chaosSpell=="north_frost:A04J"&&match.Shots.Last().weapon.speed>0,"Frost launcher retains its native projectile speed and payload");
        for(int i=0;i<100&&match.FrostDisabled(target)<=0;i++)Tick(.05f);
        Check(target.hp<hp&&match.FrostDisabled(target)>9,"Small frost bolt damages and freezes its primary building for ten seconds");
        Check(neighbour.hp==neighbour.maxHp&&match.FrostDisabled(neighbour)==0,"Small frost bolt does not invent splash damage or freeze");
        float timer=target.spawnLeft,income=match.Income(1);NorthTime(1);Check(target.spawnLeft==timer&&match.Income(1)==income,"Frozen production stops while its income remains active");
        match.NorthernFreezes.Single().left=.01f;Tick(.05f);Check(match.FrostDisabled(target)==0&&target.spawnLeft<timer,"Frozen production resumes after the native disable expires");
        NorthClear();launcher=ChaosHut(0,"h048",new Vector2(-148,32));target=ChaosHut(1,"h010",new Vector2(-140,32));target.mana=0;match.CastNorthernBuilding(launcher,"A04J",target.id);launcher.paused=true;for(int i=0;i<100&&match.FrostDisabled(target)<=0;i++)Tick(.05f);mana=target.mana;NorthTime(1);Check(target.mana>mana,"A frozen building continues regenerating mana");
        NorthClear();var big=ChaosHut(0,"h03L",new Vector2(-148,32));target=ChaosHut(1,"h000",new Vector2(-138,32));neighbour=ChaosHut(1,"h004",new Vector2(-136,33));var ground=NorthUnit(1,"h000",new Vector2(-137,31));var air=NorthUnit(1,"h015",new Vector2(-137,33));ground.hp=ground.maxHp=air.hp=air.maxHp=10000;ground.spellResist=1;target.hp=target.maxHp=neighbour.hp=neighbour.maxHp=10000;
        Check(match.CastNorthernBuilding(big,"A04L",target.id),"Large frost launcher can release its upgraded spell");big.paused=true;
        for(int i=0;i<100&&match.FrostDisabled(target)<=0;i++)Tick(.05f);
        Check(target.hp<10000&&neighbour.hp<10000&&match.FrostDisabled(target)>11&&match.FrostDisabled(neighbour)>11,"Large frost bolt splashes and freezes adjacent structures for twelve seconds");
        Check(ground.hp<10000&&air.hp==10000&&!match.Rooted(ground),"Large frost splash reaches spell-immune ground fighters but excludes air and does not root units");
        launcher=ChaosHut(0,"h048",new Vector2(-139,32));var freeze=match.NorthernFreezes.First(v=>v.target==-target.id-2);freeze.left=11;match.CastNorthernBuilding(launcher,"A04J",target.id);launcher.paused=true;NorthTime(.3f);
        Check(match.FrostDisabled(target)>10.5f,"A weaker frost refresh cannot shorten an existing stronger building disable");
        RenderMatch();QaSelect(big);controls.FocusWorld(World(new Vector2(-141,32)),34);yield return Shot(folder,"06-northern-frost-buildings");
        NorthClear();big=ChaosHut(0,"h03L",HumanMatch.CastlePosition(1)-Vector2.right*15);target=ChaosHut(1,"h000",HumanMatch.CastlePosition(1)-Vector2.right*2);float castle=match.CastleHp[1];match.CastNorthernBuilding(big,"A04L",target.id);match.Huts.Remove(big);NorthTime(2);
        Check(match.CastleHp[1]<castle&&match.FrostDisabled(target)>0,"Large frost splash can reach the castle and survives destruction of its launcher");
        NorthClear();launcher=ChaosHut(0,"h048",Vector2.zero);mana=launcher.mana;Check(!match.CastNorthernBuilding(launcher,"A04J")&&launcher.mana==mana,"Frost launcher cannot directly select the enemy castle");
        NorthClear();launcher=ChaosHut(0,"h048",Vector2.zero);target=ChaosHut(1,"h03O",Vector2.right*5);Check(match.CastNorthernBuilding(launcher,"A04J",target.id),"Frost bolt preserves native targeting of legendary buildings");launcher.paused=true;NorthTime(1);Check(match.FrostDisabled(target)>0,"Legendary status alone does not block a frost building disable");
        NorthClear();var mushroom=ChaosHut(0,"h047",new Vector2(-148,32));air=NorthUnit(1,"h015",new Vector2(-140,32));air.hp=air.maxHp=10000;air.spellResist=0;air.stun=0;mana=mushroom.mana;
        Check(match.CastNorthernBuilding(mushroom,"A06P",air.id)&&mushroom.mana==mana-6&&air.hp==10000,"Chilling mushroom spends six mana and launches before damage occurs");match.Huts.Remove(mushroom);NorthTime(1.5f);
        float expectedHammer=200*match.Multiplier("spells",match.Lines[air.kind].armorType);Check(Mathf.Abs((10000-air.hp)-expectedHammer)<2&&air.stun>3&&air.stun<=5,"Mushroom hammer deals two hundred magic damage and stuns after impact even without its caster");
        NorthClear();mushroom=ChaosHut(0,"h047",Vector2.zero);ground=NorthUnit(1,"h000",Vector2.right*4);mana=mushroom.mana;Check(!match.CastNorthernBuilding(mushroom,"A06P",ground.id)&&mushroom.mana==mana,"Mushroom does not cast on ordinary ground fighters");
        NorthClear();mushroom=ChaosHut(0,"h047",Vector2.zero);air=NorthUnit(1,"h015",Vector2.right*4);match.Grant(air,"A10A");match.Grant(air,"A09L");Check(match.CastNorthernBuilding(mushroom,"A06P",air.id)&&match.Shots.Count==0&&!air.abilities.Contains("A10A"),"Mushroom consumes a blocking spell shield before creating its projectile");
        NorthClear();var mage=NorthUnit(0,"h03S",Vector2.zero);target=ChaosHut(1,"h000",Vector2.right*5);target.hp=target.maxHp=10000;var chance=match.Abilities["A046"]["chance"].DeepClone();match.Abilities["A046"]["chance"]=1;mage.stun=0;mage.cooldown=0;mage.skirmish=true;
        for(int i=0;i<70&&!match.NorthernFreezes.Any(v=>v.id=="A045");i++)Tick(.05f);mage.stun=100;
        Check(match.NorthernFreezes.Any(v=>v.id=="A045"&&v.dps==30)&&match.FrostDisabled(target)>11,"Ice Queen royal chains disable structures with thirty magic damage per second");hp=target.hp;NorthTime(1.1f);Check(target.hp<hp-20,"Royal structure chains apply real periodic damage");
        NorthClear();mage=NorthUnit(0,"h03S",HumanMatch.CastlePosition(1)-Vector2.right*10);mage.stun=0;mage.cooldown=0;for(int i=0;i<70&&!match.NorthernFreezes.Any(v=>v.target==-1);i++)Tick(.05f);mage.stun=100;Check(match.NorthernFreezes.Any(v=>v.target==-1&&v.id=="A045"),"Royal chains can proc against the enemy castle");
        var opposite=NorthUnit(1,"h03S",HumanMatch.CastlePosition(0)+Vector2.right*10);opposite.stun=0;opposite.cooldown=0;for(int i=0;i<70&&!match.NorthernFreezes.Any(v=>v.target==-1&&v.team==1);i++)Tick(.05f);opposite.stun=100;Check(match.NorthernFreezes.Count(v=>v.target==-1)==2,"Royal chain jobs on opposite castles retain independent ownership");match.Abilities["A046"]["chance"]=chance;
        NorthClear();var legend=NorthUnit(0,"h06L",new Vector2(-148,32));legend.stun=0;legend.cooldown=0;for(int i=0;i<7;i++){var victim=NorthUnit(1,"h000",new Vector2(-142+i,31+i%2));victim.hp=victim.maxHp=100000;if(i==1)victim.spellResist=1;}
        int shots=match.NorthernBarrages;for(int i=0;i<50&&match.NorthernBarrages==shots;i++)Tick(.05f);legend.stun=100;
        Check(match.NorthernBarrages==shots+4&&match.Shots.Count(s=>s.chaosSpell=="north_barrage")==4,"Legendary tuskarr releases four separate secondary axe missiles for five total targets");
        Check(match.Shots.Where(s=>s.chaosSpell=="north_barrage").All(s=>NorthernMissile(s)),"Legendary secondary axes use the native classic projectile model");legend.hp=0;legend.deathProcessed=true;NorthTime(1.5f);
        Check(match.Units.Count(u=>u.team==1&&u.hp<100000)==5&&match.NorthernBarrages==shots+4,"Released axes survive their source, hit five enemies and do not recurse");
    }
}
}
