using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator NorthernOrbsChecks(string folder) {
        NorthClear();var freezer=ChaosHut(0,"h03O",new Vector2(-148,32));float mana=freezer.mana;
        Check(match.CastNorthernBuilding(freezer,"A04I")&&freezer.mana==mana-35&&match.NorthernOrbs.Count==0,"World Freezer spends thirty-five mana and delays sphere creation");freezer.paused=true;
        NorthTime(1.4f);Check(match.NorthernOrbs.Count==0&&match.NorthernChannel(freezer),"Winter preserves the native one-and-a-half-second delay and Death channel pose");
        match.Huts.Remove(freezer);NorthTime(.15f);Check(match.NorthernOrbs.Count==3&&match.NorthernWeather>6,"Winter creates exactly three moving spheres and snow even after its source is removed");
        Check(match.ArmyCount(0)==0&&match.Units.Count==0,"Winter spheres are effects without army slots or selectable unit bodies");
        foreach(var orb in match.NorthernOrbs)orb.fire=10000;NorthTime(6.5f);Check(match.NorthernWinters.Count==0&&match.NorthernWeather==0&&match.NorthernOrbs.Count==3,"Winter snow ends at 7.8 seconds while its independent travelling spheres remain");
        NorthClear();freezer=ChaosHut(0,"h03O",new Vector2(-148,32));match.CastNorthernBuilding(freezer,"A04I");freezer.paused=true;NorthTime(1.55f);Check(match.NorthernChannel(freezer),"Living World Freezer continues its channel after launching spheres");NorthTime(3.7f);Check(!match.NorthernChannel(freezer),"World Freezer restores its ordinary pose after 5.2 seconds");
        NorthClear();var traveller=match.SpawnNorthernOrb(0,Vector2.zero,0);traveller.fire=10000;NorthTime(.5f);Check(Mathf.Abs(traveller.position.x-4)<.001f&&Mathf.Abs(traveller.position.y)<.001f,"Winter movement follows native ten units per forty-millisecond fixed tick");
        NorthClear();traveller=match.SpawnNorthernOrb(0,new Vector2(0,127.9f),-80);traveller.fire=10000;Tick(.05f);Check(traveller.heading<0&&traveller.position.y<127.9f,"Winter spheres reflect their heading at the native north-south boundary");
        NorthClear();traveller=match.SpawnNorthernOrb(0,new Vector2(6400f/30-.01f,0),0);traveller.fire=10000;Tick(.05f);Check(match.NorthernOrbs.Count==0,"Winter spheres disappear at the native east-west limit");
        NorthClear();traveller=match.SpawnNorthernOrb(0,new Vector2(-148,32),0);traveller.fire=10000;var enemy=NorthUnit(1,"h000",new Vector2(-146,32));var immune=NorthUnit(1,"h000",new Vector2(-147,33));var ally=NorthUnit(0,"h03U",new Vector2(-149,31));immune.spellResist=1;NorthTime(.2f);
        Check(enemy.effects.Count(e=>e.id=="A081")==1&&Mathf.Abs(match.Stat(enemy,"speed")+.4f)<.001f&&Mathf.Abs(match.Stat(enemy,"haste")+.4f)<.001f,"Winter aura slows movement and attack by forty percent without stacking");
        Check(!immune.effects.Any(e=>e.id=="A081")&&!ally.effects.Any(e=>e.id=="A081"),"Winter aura excludes spell immunity and allies");
        match.NorthernOrbs.Clear();NorthTime(.65f);Check(!enemy.effects.Any(e=>e.id=="A081"),"Winter aura expires after leaving its short refresh window");
        NorthClear();traveller=match.SpawnNorthernOrb(0,new Vector2(-148,32),0);traveller.fire=10000;var air=NorthUnit(1,"h015",new Vector2(-140,32));air.hp=air.maxHp=10000;air.spellResist=0;air.stun=0;air.castPause=100;int controlCount=match.NorthernControls;NorthTime(2.1f);
        Check(match.NorthernControls==controlCount+1&&air.hp<9900&&air.stun>7,"Winter control uses a target-local missile for 125 damage and eight seconds of air stun");
        NorthClear();traveller=match.SpawnNorthernOrb(0,new Vector2(-148,32),0);traveller.fire=10000;enemy=NorthUnit(1,"h000",new Vector2(-140,32));enemy.stun=0;enemy.castPause=100;NorthTime(2.1f);
        Check(match.Rooted(enemy)&&match.Disarmed(enemy)&&enemy.effects.Any(e=>e.id=="A04H"&&e.lifetime==15)&&enemy.stun>0,"Ground winter control combines a short stun with fifteen-second ice chains");
        NorthClear();traveller=match.SpawnNorthernOrb(0,new Vector2(-148,32),0);traveller.fire=10000;enemy=NorthUnit(1,"h000",new Vector2(-140,32));enemy.hp=enemy.maxHp=10000;enemy.spellResist=1;enemy.stun=0;enemy.castPause=100;NorthTime(2.1f);
        Check(enemy.hp==10000&&enemy.stun==0&&!match.Rooted(enemy),"Winter control cannot bypass magic immunity");
        NorthClear();traveller=match.SpawnNorthernOrb(0,new Vector2(-148,32),0);enemy=NorthUnit(1,"h000",new Vector2(-139,32));enemy.hp=enemy.maxHp=10000;NorthTime(.4f);Check(match.Shots.Any(s=>s.chaosSpell=="north_spell:A080"),"Winter spheres automatically fire their native third-second missiles");NorthTime(.6f);Check(enemy.hp<10000,"Winter automatic fire applies actual magic damage after flight");
        NorthClear();traveller=match.SpawnNorthernOrb(0,HumanMatch.CastlePosition(1)-Vector2.right*10,0);float hp=match.CastleHp[1];NorthTime(1);Check(match.CastleHp[1]<hp,"Winter automatic fire can target the enemy castle");
        NorthClear();freezer=ChaosHut(0,"h03O",new Vector2(-148,32));match.CastNorthernBuilding(freezer,"A04I");freezer.paused=true;enemy=NorthUnit(1,"h000",new Vector2(-137,32));enemy.hp=enemy.maxHp=10000;NorthTime(2.2f);RenderMatch();QaSelect(freezer);controls.FocusWorld(World(new Vector2(-139,32)),38);yield return new WaitForSeconds(2.5f);yield return Shot(folder,"07-northern-winter");
        Check(northernSnow&&northernSnow.isPlaying&&northernSnow.particleCount>0&&northernOrbViews.Count==3,"Winter renders three interpolated spheres and native-texture snow in the player");
    }
    IEnumerator NorthernAspectsChecks(string folder) {
        MainMenu();raceChoice=5;botRaceChoice=0;aspectsOn=true;yield return Action("main.play");yield return Action("setup.start");testHoldAuthority=true;
        Check(aspectWaiting&&AspectCount(5)==2&&AspectIcon(5,0)&&AspectIcon(5,1),"Both Northern aspects appear with original icons and freeze the choice phase");yield return Shot(folder,"08-northern-aspects");yield return Action("aspect.choose0");
        Check(Mathf.Abs(match.FrostDuration(0,4)-5.4f)<.001f&&match.FrostDuration(0,10)==12&&match.FrostDuration(0,10,true)==11,"Eternal Winter adds thirty-five percent with native two-second and hero one-second caps");
        NorthClear();var enemy=NorthUnit(1,"h000",Vector2.zero);match.ApplyEffect(enemy,"A03W",0);Check(enemy.effects.Single().lifetime==9,"Eternal Winter extends ordinary ice chains by two seconds");match.ApplyEffect(enemy,"A03W",0);Check(enemy.effects.Count==1&&enemy.effects.Single().left==9,"Eternal Winter refreshes rather than accumulating chain duration");
        string armorType=match.Lines[enemy.kind].armorType;match.Lines[enemy.kind].armorType="hero";enemy.effects.Clear();match.ApplyEffect(enemy,"A03W",0);Check(Mathf.Abs(enemy.effects.Single().lifetime-2.3625f)<.001f,"Eternal Winter uses the shorter hero chain duration before extending it");match.Lines[enemy.kind].armorType=armorType;
        NorthClear();var launcher=ChaosHut(0,"h048",Vector2.zero);var target=ChaosHut(1,"h000",Vector2.right*5);match.CastNorthernBuilding(launcher,"A04J",target.id);launcher.paused=true;for(int i=0;i<100&&match.FrostDisabled(target)<=0;i++)Tick(.05f);Check(match.FrostDisabled(target)>11.8f,"Eternal Winter extends actual building frost disables to twelve seconds");
        NorthClear();var orb=match.SpawnNorthernOrb(0,Vector2.zero,0);orb.fire=10000;enemy=NorthUnit(1,"h000",Vector2.right);Tick(.05f);Check(enemy.effects.Any(e=>e.id=="A081"&&e.lifetime==.6f),"Eternal Winter does not extend the travelling sphere aura refresh window");
        MainMenu();raceChoice=5;botRaceChoice=0;aspectsOn=true;yield return Action("main.play");yield return Action("setup.start");testHoldAuthority=true;yield return Action("aspect.choose1");NorthClear();
        var fighter=NorthUnit(0,"h06L",new Vector2(-148,32));fighter.hp=fighter.maxHp=1000;fighter.hp=300;match.DamageUnit(fighter,1,1,"chaos",false,true);
        Check(fighter.northernRageUsed&&fighter.effects.Any(e=>e.id=="aspect_north_rage"&&e.lifetime==8),"Northern Rage triggers once after surviving a hit below thirty percent health");
        Check(Mathf.Abs(match.Stat(fighter,"damage")-.25f)<.001f&&Mathf.Abs(match.AttackSpeed(fighter)-1.5f)<.001f&&Mathf.Abs(match.Stat(fighter,"speed")-.25f)<.001f,"Northern Rage gives native damage, attack and movement bonuses");
        match.NatureDispel(fighter);Check(fighter.effects.Any(e=>e.id=="aspect_north_rage"),"Northern Rage remains undispellable");
        NorthTime(7);float remaining=fighter.effects.Single(e=>e.id=="aspect_north_rage").left;fighter.hp=1000;match.DamageUnit(fighter,800,1,"chaos",false,true);Check(fighter.effects.Single(e=>e.id=="aspect_north_rage").left==remaining,"Healing and another low-health hit do not refresh Northern Rage");
        RenderMatch();builderSelected=false;selectedUnit=fighter.id;controls.FocusWorld(World(fighter.position),30);yield return Shot(folder,"09-northern-rage");
        NorthTime(1.2f);match.DamageUnit(fighter,1,1,"chaos",false,true);Check(!fighter.effects.Any(e=>e.id=="aspect_north_rage")&&fighter.northernRageUsed,"Expired Northern Rage cannot trigger again during the same life");
        var lethal=NorthUnit(0,"h049",Vector2.zero);int rages=match.NorthernRages;match.DamageUnit(lethal,1000000,1,"chaos",false,true);Check(lethal.hp==0&&!lethal.northernRageUsed&&match.NorthernRages==rages,"A lethal hit does not create a Northern Rage on a corpse");
        var immune=NorthUnit(0,"h049",Vector2.zero);immune.hp=immune.maxHp=1000;immune.hp=299;immune.spellResist=1;match.DamageUnit(immune,100,1);Check(!immune.northernRageUsed,"Zero applied damage cannot trigger Northern Rage");
        NorthClear();var producer=ChaosHut(0,"h049",Vector2.zero);producer.spawnLeft=10000;int extras=match.AspectExtras;NorthTime(31);Check(match.AspectExtras==extras&&match.Units.Count==0,"Northern Rage does not inherit the Human reinforcement aspect");
        aspectsOn=false;
    }
}
}
