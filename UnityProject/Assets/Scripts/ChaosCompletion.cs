using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    public static readonly string[] BloodPool={"A01B","A02B","A02B","A009","A05J","A01A","A00D","A01U","A012"};
    public int BloodFiends,TwinPairs,TwinBlasts,ChaosSeals;
    public bool Hidden(Unit u)=>MechMine(u)||UnitConfig035(u.kind).hidden;
    bool ChaosFighter(Unit u)=>u!=null&&u.hp>0&&!Hidden(u)&&(bool?)Lines[u.kind].stats["invulnerable"]!=true&&Lines[u.kind].unit!="YTct"&&Lines[u.kind].unit!="n02E";
    public float ChaosHaste(Unit u)=>ChaosFighter(u)&&TeamChaos[u.team]&&Aspects[u.team]==0?.35f:0;
    public float ChaosSealBonus(Unit u){if(u.chaosSealTeam!=u.team){u.chaosSealTeam=u.team;u.chaosSeal=0;}return TeamChaos[u.team]&&Aspects[u.team]==1?u.chaosSeal*2:0;}
    public void RollBloodFiend(Unit u,int roll=-1,int first=-1,int second=-1) {
        if(u==null||Lines[u.kind].unit!="n00L"||u.bloodRoll>=0)return;
        u.bloodRoll=roll<0?Mathf.Min(99,(int)(Roll()*100)):Mathf.Clamp(roll,0,99);BloodFiends++;
        if(u.bloodRoll<30)return;if(u.bloodRoll<40){ApplyEffect(u,"A0FZ",u.team);return;}
        var pool=BloodPool.ToList();first=first<0?Mathf.Min(8,(int)(Roll()*9)):Mathf.Clamp(first,0,8);string initial=pool[first];
        if(first==0||first==1){pool[0]=pool[8];pool[1]=pool[7];pool[2]=pool[6];pool.RemoveRange(6,3);}else {pool[first]=pool[8];pool.RemoveAt(8);}
        second=second<0?Mathf.Min(pool.Count-1,(int)(Roll()*pool.Count)):Mathf.Clamp(second,0,pool.Count-1);
        Grant(u,initial);Grant(u,pool[second]);
    }
    void ChaosProduced(Unit u) {
        if(Lines[u.kind].unit=="n00L")RollBloodFiend(u);
        if(u.abilities.Contains("A0FO")){foreach(var raw in new[]{"n02K","n02L"}){var twin=Spawn(u.team,UnitKind(raw),u.position,false);twin.direction=u.direction;}TwinPairs++;}
    }
    bool ChaosBlastReady(Unit u)=>Units.Any(v=>NatureEligible(v,u.team)&&!Flying(v)&&Vector2.Distance(u.position,v.position)<=F(Abilities["A0FU"],"range"));
    bool CastChaosBlast(Unit u,Unit suggested) {
        var cfg=Abilities["A0FU"];var targets=Units.Where(v=>NatureEligible(v,u.team)&&!Flying(v)&&Vector2.Distance(u.position,v.position)<=F(cfg,"range")).ToArray();if(targets.Length==0)return false;
        var target=targets.Contains(suggested)?suggested:targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];var w=Weapon.Read(cfg["missile_weapon"]);w.speed=700f/30;w.homing=false;
        var shot=AddShot(u.team,u.kind,u.id,target.id,u.position,target.position,0,w,0,3);shot.chaosSpell="twins_blast";TwinBlasts++;return true;
    }
    void ImpactChaosBlast(Shot shot) {
        var clap=Abilities["A0FS"];PulseAt(shot.end,F(clap,"area"),new Color(.4f,.6f,1),"Опустошение маны");
        foreach(var u in Units.ToArray())if(u.hp>0&&Vector2.Distance(u.position,shot.end)<=F(clap,"area")) {
            if(Valid(u,shot.team,clap,false)){DamageUnit(u,F(clap,"power"),shot.team);if(u.hp>0){ApplyEffect(u,"A0FS",shot.team);var slow=u.effects.LastOrDefault(e=>e.id=="A0FS");if(slow!=null)slow.power=0;}}
            if(!NatureEligible(u,shot.team)||Flying(u))continue;float drain=Mathf.Min(100,u.mana);u.mana-=drain;if(drain>0){DamageUnit(u,drain,shot.team,"chaos");PulseAt(u.position,1,Color.cyan,"−"+Mathf.RoundToInt(drain)+" маны");}
        }
    }
    void TickChaosAspects(float step) {
        foreach(var u in Units)if(ChaosFighter(u)&&TeamChaos[u.team]&&Aspects[u.team]==0&&u.chaosAttackingUntil>Elapsed)u.hp=Mathf.Min(u.hp,Mathf.Max(1,u.hp-u.maxHp*.01f*step));
    }
    void ChaosDeath(Unit victim) {
        var stats=Lines[victim.kind].stats;
        if(Hidden(victim)||(bool?)stats["invulnerable"]==true||Lines[victim.kind].unit=="YTct"||Lines[victim.kind].unit=="n02E"||victim.summoned||victim.summonLeft>0||victim.noBounty||victim.spellBorn||(bool?)stats["illusion"]==true)return;
        int team=1-victim.team;if(!TeamChaos[team]||Aspects[team]!=1)return;
        var recipient=Units.Where(u=>ChaosFighter(u)&&u.team==team&&Vector2.Distance(u.position,victim.position)<=600f/30).OrderBy(u=>(u.position-victim.position).sqrMagnitude).FirstOrDefault();if(recipient==null)return;
        ChaosSealBonus(recipient);recipient.chaosSeal=Mathf.Min(10,recipient.chaosSeal+1);ChaosSeals++;PulseAt(recipient.position,1.5f,new Color(1,.6f,.1f),"Печать · "+recipient.chaosSeal);
    }
}
}
