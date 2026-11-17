using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    public sealed class ChaosFlame {public int id,team,phase=-1;public Vector2 center;public float age,nextFull,nextTail;}
    public sealed class HellCurse {public int target,team;public float left,dps,tick;}
    public readonly List<ChaosFlame> ChaosFlames=new List<ChaosFlame>();
    public readonly List<HellCurse> HellCurses=new List<HellCurse>();
    public int ChaosGifts,ChaosErases,ChaosEruptions,ChaosFists,ChaosMisses;
    public bool Disarmed(Unit u){foreach(var e in u.effects)if(e.left>0&&(bool?)e.config?["disarm"]==true)return true;return false;}
    public bool Silenced(Unit u){foreach(var e in u.effects)if(e.left>0&&(bool?)e.config?["silence"]==true)return true;return false;}
    public float MissChance(Unit u)=>Mathf.Clamp01(u.effects.Where(e=>e.left>0).Sum(e=>F(e.config,"miss")));
    public float HellDisabled(Hut h) {
        if(HellCurses.Count==0)return 0;
        float best=0;for(int i=0;i<HellCurses.Count;i++){var c=HellCurses[i];if(c.target==h.id&&c.left>best)best=c.left;}
        return best;
    }
    static readonly string[] ChaosBuildingSpells={"chaos_gift","map_erase","eruption","hell_fist"};
    void TickChaosBuilding(Hut h) {
        foreach(string id in Lines[h.kind].abilities)if(ChaosBuildingSpells.Contains((string)Abilities[id]?["kind"])&&(!h.spellTimers.TryGetValue(id,out float wait)||wait<=0)) {
            bool cast=CastChaosBuilding(h,id);h.spellTimers[id]=cast?BuildingCooldown(h,F(Abilities[id],"cd")):.25f;
        }
    }
    public bool CastChaosBuilding(Hut source,string id,int suggested=-1) {
        var cfg=Abilities[id];if(source==null||!Huts.Contains(source)||!source.paid||source.hp<=0||source.paused||BuildingDisabled(source)>0||cfg==null||!Lines[source.kind].abilities.Contains(id)||source.mana<F(cfg,"mana"))return false;
        if(source.spellTimers.TryGetValue(id,out float wait)&&wait>0)return false;
        string kind=(string)cfg["kind"];if(!ChaosBuildingSpells.Contains(kind))return false;
        if(kind=="hell_fist") {
            var targets=Huts.Where(h=>h.paid&&h.hp>0&&h.team!=source.team&&!Lines[h.kind].abilities.Contains("A06V")&&Lines[h.kind].building!="hcas"&&Lines[h.kind].building!="h06M").ToArray();if(targets.Length==0)return false;
            var target=targets.FirstOrDefault(h=>h.id==suggested)??targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];
            var w=Weapon.Read(cfg["missile_weapon"]);var shot=AddShot(source.team,source.kind,source.id,HutTarget(target),source.position,target.position,w.damage,w,0,3);shot.chaosSpell="hell_fist";
            int stage=Elapsed>=35*60?3:Elapsed>=25*60?2:Elapsed>=20*60?1:0;shot.chaosDuration=new[]{360f,480f,729f,1200f}[stage];shot.chaosDps=stage+1;
            ChaosFists++;PulseAt(source.position,2,new Color(1,.2f,.05f),"Кулак ада");
        } else {
            var targets=Units.Where(u=>NatureEligible(u,source.team)&&Vector2.Distance(u.position,source.position)<=F(cfg,"range")&&(kind!="eruption"||!Flying(u))).ToArray();if(targets.Length==0)return false;
            var target=targets.FirstOrDefault(u=>u.id==suggested)??targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];
            if(kind=="chaos_gift"){if(!NagaShieldBlock(target))ChaosGiftOutcome(target,source.team,Mathf.Min(9,(int)(Roll()*10)));ChaosGifts++;PulseAt(target.position,2,new Color(.8f,.25f,1),"Дар Хаоса");}
            else if(kind=="map_erase") {
                string raw=Lines[target.kind].unit;
                using(var snapshot035_1=SimulationBuffers035.Take(Units))foreach(var u in snapshot035_1)if(u.hp>0&&u.team!=source.team&&Lines[u.kind].unit==raw) {
                    if(ChaosEraseShield(u))continue;
                    PulseAt(u.position,2,new Color(.9f,.3f,1),"Стирание");if(!u.abilities.Contains("A070"))DamageUnit(u,100500,source.team,"chaos");
                }
                ChaosErases++;
            } else {
                ChaosFlames.Add(new ChaosFlame {id=nextId++,team=source.team,center=target.position,nextFull=F(cfg,"interval"),nextTail=F(cfg,"full_duration")+F(cfg,"tail_interval")});ChaosEruptions++;
            }
        }
        source.mana-=F(cfg,"mana");source.spellTimers[id]=BuildingCooldown(source,F(cfg,"cd"));source.attackSerial++;source.attackPoseLeft=.35f;SpellsCast++;return true;
    }
    bool ChaosEraseShield(Unit u) {
        if(!u.abilities.Contains("A10A")||!u.abilityLevels.TryGetValue("A10A",out int level)||level<=1)return false;
        foreach(var id in new[]{"A09L","A10A"}){u.abilities.Remove(id);u.abilityLevels.Remove(id);}
        PulseAt(u.position,2,Color.cyan,"Большой щит",.8f,"shield");return true;
    }
    // Explicit outcome also permits deterministic verification of all ten source rolls.
    public void ChaosGiftOutcome(Unit target,int team,int outcome) {
        if(target==null||target.hp<=0)return;
        if(outcome==0){target.noBounty=true;target.noCorpse=true;DamageUnit(target,target.maxHp*1000+1000000,team,"chaos");}
        else if(outcome==1||outcome==2)DamageUnit(target,outcome==1?50000:500,team,"normal");
        else if(outcome==3){Grant(target,"A06T");Grant(target,"A06U");target.hp=target.maxHp;PulseAt(target.position,2,Color.yellow,"+400 HP / +25 урона");}
        else if(outcome==4||outcome==5){string id=outcome==4?"A018":"A0BL";if(Valid(target,team,Abilities[id],false))ApplyEffect(target,id,team);}
        else if(outcome==6){var c=Abilities["A06S"];if(Valid(target,team,c,false)){bool shortDuration=Lines[target.kind].armorType=="hero"||target.abilities.Any(a=>(string)Abilities[a]?["kind"]=="resistant_skin");target.stun=Mathf.Max(target.stun,F(c,shortDuration?"hero_duration":"duration"));DamageUnit(target,F(c,"power"),team);}}
        else if(outcome==7){var mutation=Spawn(team,UnitKind("n01S"),target.position);mutation.direction=target.direction;mutation.onRoad=true;mutation.leaveCastleLane=false;target.hp=0;target.deadAge=100;target.deathProcessed=true;target.pose="Death";}
        else PulseAt(target.position,1,new Color(.7f,.3f,1),"Без эффекта");
    }
    void TickChaosJobs(float dt) {
        using(var snapshot035_2=SimulationBuffers035.Take(HellCurses))foreach(var curse in snapshot035_2) {
            var target=Huts.FirstOrDefault(h=>h.id==curse.target&&h.hp>0);if(target==null){HellCurses.Remove(curse);continue;}
            float step=Mathf.Min(dt,Mathf.Max(0,curse.left));curse.left-=dt;curse.tick+=step;float due=curse.left<=0?curse.tick:Mathf.Floor(curse.tick);
            if(due>0){curse.tick-=due;DamageHut(target,curse.dps*due,"chaos",curse.team,true);}
            if(curse.left<=0)HellCurses.Remove(curse);
        }
        var cfg=Abilities["A01J"];if(cfg==null)return;
        using(var snapshot035_3=SimulationBuffers035.Take(ChaosFlames))foreach(var flame in snapshot035_3) {
            flame.age+=dt;float delay=F(cfg,"cast_delay"),full=F(cfg,"full_duration"),duration=F(cfg,"duration");int phase=flame.age<delay?0:flame.age<delay+full?1:2;
            if(phase!=flame.phase){flame.phase=phase;PulseAt(flame.center,F(cfg,"area"),phase==0?new Color(1,.7f,.1f):phase==1?new Color(1,.25f,.02f):new Color(.7f,.12f,.02f),phase==0?"Извержение":"",phase==0?delay:phase==1?full:duration-full);}
            while(flame.nextFull<=full+.00001f&&flame.age+.00001f>=delay+flame.nextFull){ChaosFlamePulse(flame,F(cfg,"power"));flame.nextFull+=F(cfg,"interval",.25f);}
            while(flame.nextTail<=duration+.00001f&&flame.age+.00001f>=delay+flame.nextTail){ChaosFlamePulse(flame,F(cfg,"tail_power"));flame.nextTail+=F(cfg,"tail_interval",1);}
            if(flame.age>=delay+duration)ChaosFlames.Remove(flame);
        }
    }
    void ChaosFlamePulse(ChaosFlame flame,float amount) {
        var cfg=Abilities["A01J"];float radius=F(cfg,"area");var actors=Units.Where(u=>u.hp>0&&u.team!=flame.team&&(bool?)Lines[u.kind].stats["invulnerable"]!=true&&!Flying(u)&&u.spellResist<1&&Lines[u.kind].unit!="n02E"&&Vector2.Distance(u.position,flame.center)<=radius).ToArray();
        var buildings=Huts.Where(h=>h.paid&&h.hp>0&&h.team!=flame.team&&Vector2.Distance(h.position,flame.center)<=radius).ToArray();bool castle=Vector2.Distance(CastlePosition(1-flame.team),flame.center)<=radius;
        float per=Mathf.Min(amount,F(cfg,"damage_cap")/Mathf.Max(1,actors.Length+buildings.Length+(castle?1:0)));
        foreach(var u in actors)DamageUnit(u,per,flame.team);
        foreach(var h in buildings)DamageHut(h,per*F(cfg,"building_factor"),"magic",flame.team,true);
        if(castle)DamageCastle(1-flame.team,per*F(cfg,"building_factor"),"magic",true);
    }
    void ImpactChaosFist(Shot shot) {
        var target=FindHut(shot.target);if(target==null)return;
        float duration=shot.chaosDuration;var curse=HellCurses.FirstOrDefault(c=>c.target==target.id);
        if(curse==null){curse=new HellCurse {target=target.id};HellCurses.Add(curse);}curse.team=shot.team;curse.left=duration;curse.dps=shot.chaosDps;target.strikeLeft=-1;target.pendingWeapon=null;
        DamageHut(target,shot.damage,"chaos",shot.team,true);PulseAt(target.position,3,new Color(1,.2f,.05f),"Кулак ада · "+duration+" сек.");
    }
    bool CastChaosCurse(Unit source,Unit target) {
        var cfg=Abilities["A01G"];if(!Valid(target,source.team,cfg,false)||Vector2.Distance(source.position,target.position)>F(cfg,"range"))return false;
        if(!target.spellTimers.TryGetValue("spell_shield",out float wait)||wait<=0)foreach(string id in target.abilities)if((string)Abilities[id]?["kind"]=="shield"){target.spellTimers["spell_shield"]=F(Abilities[id],"cd");PulseAt(target.position,1.5f,Color.cyan,"Магический щит");return true;}
        ApplyEffect(target,"A01G",source.team);PulseAt(target.position,1.5f,new Color(.6f,.1f,.8f),"Проклятие");return true;
    }
}
}
