using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator MechAspectsChecks(string folder) {
        MainMenu();raceChoice=6;botRaceChoice=0;aspectsOn=true;yield return Action("main.play");yield return Action("setup.start");testHoldAuthority=true;
        Check(aspectWaiting&&AspectCount(6)==2&&AspectIcon(6,0)&&AspectIcon(6,1),"Both Mech aspects appear with source icons before commands resume");yield return Shot(folder,"10-mech-aspects");yield return Action("aspect.choose0");MechClear();
        var target=MechDummy(0,new Vector2(-148,32));target.armor=0;float dealt=match.DamageUnit(target,40,1,"chaos",false);Check(Mathf.Abs(dealt-28)<.001f,"Heavy Armor blocks twelve after ordinary physical damage reduction");dealt=match.DamageUnit(target,3,1,"chaos",false);Check(Mathf.Abs(dealt-3)<.001f,"Heavy Armor never increases a hit below its five-damage floor");dealt=match.DamageUnit(target,14,1,"chaos",false);Check(Mathf.Abs(dealt-5)<.001f,"Heavy Armor retains its native five-damage floor");
        dealt=match.DamageUnit(target,40,1);Check(Mathf.Abs(dealt-28)<.001f,"Heavy Armor also blocks actual magic spell impacts");target.spellResist=1;float hp=target.hp;match.DamageUnit(target,40,1);Check(target.hp==hp,"Heavy Armor cannot turn a fully resisted spell into damage");
        var cannon=MechUnit(0,"h09I",new Vector2(-146,32));cannon.armor=0;cannon.hp=cannon.maxHp=10000;dealt=match.DamageUnit(cannon,40,1,"chaos",false);Check(Mathf.Abs(dealt-22)<.001f,"Heavy Armor and native hardened skin choose stronger absorption without adding both blocks");
        MechClear();var producer=ChaosHut(0,"h05X",Vector2.zero);producer.spawnLeft=10000;int extras=match.AspectExtras;NorthTime(31);Check(match.AspectExtras==extras&&match.Units.Count==0,"Mech Heavy Armor does not inherit Human reinforcements");
        MainMenu();raceChoice=6;botRaceChoice=0;aspectsOn=true;yield return Action("main.play");yield return Action("setup.start");testHoldAuthority=true;yield return Action("aspect.choose1");MechClear();
        var gunner=MechUnit(0,"h05X",new Vector2(-148,32));gunner.castPause=0;gunner.cooldown=0;gunner.cooldownOverride=.45f;gunner.attackOverride=0;var crowd=new HumanMatch.Unit[7];for(int i=0;i<7;i++)crowd[i]=MechDummy(1,new Vector2(-138+i*.3f,32+(i==0?0:1)));int bursts=match.MechHeatBursts;
        for(int i=0;i<150&&!match.Shots.Any(s=>s.mechHeat);i++)Tick(.05f);gunner.castPause=10000;
        Check(gunner.mechHeatAttacks==5&&match.MechHeatBursts==bursts&&match.Shots.Any(s=>s.mechHeat&&!s.hit),"Overheat counts five released attacks and waits for the ranged projectile impact");match.DamageUnit(gunner,1000000,1,"chaos",false,true);NorthTime(.7f);
        Check(match.MechHeatBursts==bursts+1&&crowd.Count(u=>u.hp<10000)==6&&Mathf.Abs(crowd[0].hp-9920)<.1f,"Released Overheat survives its source, guarantees its main victim and deals eighty magic damage to at most six enemies");
        Check(crowd.Any(u=>u.hp==10000),"Overheat preserves the native crowd cap");RenderMatch();selectedUnit=crowd[0].id;builderSelected=false;controls.FocusWorld(World(crowd[0].position),34);yield return Shot(folder,"11-mech-overheat");
        MechClear();var melee=MechUnit(0,"h05U",new Vector2(-148,32));melee.castPause=0;melee.cooldown=0;melee.cooldownOverride=.4f;melee.attackOverride=0;target=MechDummy(1,new Vector2(-146,32));bursts=match.MechHeatBursts;for(int i=0;i<150&&match.MechHeatBursts==bursts;i++)Tick(.05f);melee.castPause=10000;Check(melee.mechHeatAttacks==5&&match.MechHeatBursts==bursts+1&&Mathf.Abs(target.hp-9920)<.1f,"Melee Overheat also works in a duel on the fifth released attack");
        MechClear();gunner=MechUnit(0,"h05X",Vector2.zero);gunner.castPause=0;gunner.cooldown=0;gunner.cooldownOverride=.4f;gunner.attackOverride=0;gunner.skirmish=true;var building=ChaosHut(1,"h000",Vector2.right*5);building.paused=true;NorthTime(3);Check(gunner.attackSerial>0&&gunner.mechHeatAttacks==0,"Attacks against structures do not charge Overheat");aspectsOn=false;
    }
}
}
