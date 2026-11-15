using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    public sealed class NagaWave {public int team;public Vector2 origin,direction;public float distance,previousDistance,age,spentDamage;public readonly HashSet<int> struck=new HashSet<int>();}
    public readonly List<NagaWave> ActiveNagaWaves=new List<NagaWave>();
    public int NagaQuakes {get;private set;}
    public int NagaControls {get;private set;}
    public int NagaWaves {get;private set;}
    bool NagaCandidate(Unit u,int team)=>u.hp>0&&u.team!=team&&((bool?)Lines[u.kind].stats["invulnerable"]??false)==false&&((string)Lines[u.kind].stats["unit_types"]??"").ToLowerInvariant().Contains("sapper")&&!u.abilities.Contains("A08H");
    bool IsNagaHero(Unit unit) {string raw=(string)Lines[unit.kind].stats["base"]??Lines[unit.kind].unit;return !string.IsNullOrEmpty(raw)&&char.IsLetter(raw[0])&&char.IsUpper(raw[0]);}
    bool NagaShieldBlock(Unit unit) {
        if(unit.abilities.Contains("A10A")) {
            int shield=unit.abilityLevels.TryGetValue("A10A",out int v)?v:1;
            int companion=unit.abilityLevels.TryGetValue("A09L",out int c)?c:1;
            if(shield==2){if(companion==2){unit.abilityLevels["A10A"]=1;Grant(unit,"A10B");unit.hp=unit.maxHp;}}
            else {unit.abilities.Remove("A09L");unit.abilities.Remove("A10A");unit.abilityLevels.Remove("A09L");unit.abilityLevels.Remove("A10A");}
            PulseAt(unit.position,2,Color.cyan,"Щит",.8f,"shield");return true;
        }
        if(Lines[unit.kind].unit=="n01T") {
            if(Roll()>=.6f)return false;int level=(unit.abilityLevels.TryGetValue("A09C",out int v)?v:1)+1;
            if(level<6)foreach(string id in new[]{"A09C","A09B","A00J"}){Grant(unit,id);unit.abilityLevels[id]=level;}
            PulseAt(unit.position,2,Color.cyan,"Щит",.8f,"shield");return true;
        }
        return unit.abilities.Contains("A070");
    }
    void TickNagaBuilding(Hut h,float dt) {
        string raw=Lines[h.kind].building;
        if(raw=="h00G") {
            float radius=F(Abilities["A007"],"area",700f/30);
            foreach(var u in Units)if(u.team==h.team&&u.hp>0&&Vector2.Distance(u.position,h.position)<=radius)u.hp=Mathf.Min(u.maxHp,u.hp+7.5f*dt);
            foreach(var target in Huts)if(target.team==h.team&&target.paid&&target.hp>0&&Vector2.Distance(target.position,h.position)<=radius)target.hp=Mathf.Min(target.maxHp,target.hp+2.5f*dt);
            if(Vector2.Distance(CastlePosition(h.team),h.position)<=radius)CastleHp[h.team]=Mathf.Min(CastleMax,CastleHp[h.team]+2.5f*dt);
        }
        if(raw=="h00I") {
            var cfg=Abilities["A00M"];if(h.mana<F(cfg,"mana")||(h.spellTimers.TryGetValue("A00M",out float wait)&&wait>0))return;
            var targets=Units.Where(u=>NagaCandidate(u,h.team)&&!Flying(u)).ToArray();if(targets.Length==0)return;var center=targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))].position;
            h.mana-=F(cfg,"mana");h.spellTimers["A00M"]=BuildingCooldown(h,F(cfg,"cd"));var child=Abilities["A00B"];
            foreach(var u in Units.ToArray())if(Valid(u,h.team,child,false)&&Vector2.Distance(u.position,center)<=F(child,"area")){DamageUnit(u,F(child,"power"),h.team);if(u.hp>0){ApplyEffect(u,"A00B",h.team);var slow=u.effects.LastOrDefault(e=>e.id=="A00B");if(slow!=null)slow.power=0;}}
            NagaQuakes++;SpellsCast++;PulseAt(center,F(child,"area"),new Color(.45f,.75f,1),"Землетрясение",1.05f,"naga_quake");
        }
        if(raw=="h00M"||raw=="h00N") {
            string id=raw=="h00M"?"A00O":"A09X";var cfg=Abilities[id];
            if(h.mana<F(cfg,"mana")||(h.spellTimers.TryGetValue(id,out float wait)&&wait>0))return;
            var targets=Units.Where(u=>NagaCandidate(u,h.team)).ToArray();if(targets.Length==0)return;
            var target=targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];
            h.mana-=F(cfg,"mana");h.spellTimers[id]=BuildingCooldown(h,F(cfg,"cd"));SpellsCast++;
            if(raw=="h00M") {
                bool blocked=NagaShieldBlock(target);
                if(!blocked&&Valid(target,h.team,Abilities["A00N"],false)&&!IsNagaHero(target)&&F(Lines[target.kind].stats,"level",1)<=F(cfg,"level_limit",5)) {
                    target.team=h.team;ApplyUltimate(target);target.combatTarget=target.attackTarget=-1;target.strikeLeft=-1;target.castLeft=-1;target.pendingSpell=null;target.leaveCastleLane=false;target.onRoad=true;target.direction=h.team==0?Vector2.right:Vector2.left;NagaControls++;
                    PulseAt(target.position,2,new Color(.4f,.8f,1),"Подчинение",3.3f,"naga_charm");
                }
            } else {
                Vector2 delta=target.position-h.position;var direction=delta.sqrMagnitude<.001f?(h.team==0?Vector2.right:Vector2.left):delta.normalized;
                ActiveNagaWaves.Add(new NagaWave {team=h.team,origin=h.position,direction=direction});NagaWaves++;
            }
        }
    }
    void TickNagaWaves(float dt) {
        var cfg=Abilities["A00P"];if(cfg==null)return;
        foreach(var wave in ActiveNagaWaves.ToArray()) {
            wave.age+=dt;float previous=wave.previousDistance=wave.distance;wave.distance=Mathf.Min(F(cfg,"wave_length",400),wave.distance+F(cfg,"missile_speed",50)*dt);
            foreach(var victim in Units.ToArray()) {
                if(wave.struck.Contains(victim.id)||!Valid(victim,wave.team,cfg,false)||Flying(victim))continue;
                var offset=victim.position-wave.origin;float along=Vector2.Dot(offset,wave.direction),radius=F(Lines[victim.kind].stats,"collision");
                if(along<Mathf.Max(0,previous-radius)||along>wave.distance+radius||(offset-wave.direction*along).magnitude>F(cfg,"start_width",200f/30)+radius)continue;
                wave.struck.Add(victim.id);float amount=Mathf.Min(F(cfg,"power"),Mathf.Max(0,F(cfg,"max_damage",99999)-wave.spentDamage));if(amount<=0)continue;wave.spentDamage+=amount;DamageUnit(victim,amount,wave.team);PulseAt(victim.position,1,new Color(.35f,.7f,1),"",.65f,"naga_wave_impact");
            }
            if(wave.distance>=F(cfg,"wave_length",400))ActiveNagaWaves.Remove(wave);
        }
    }
}
}
