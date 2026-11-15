// Staged second batch; not part of the player until the correction EXE is certified.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    public sealed class ElvenJob {public string kind,ability;public Unit body;public int team,target,ticks;public float left,power;public int roll=-1;}
    public readonly List<ElvenJob> ElvenJobs=new List<ElvenJob>();
    public int ElvenReturns,ElvenHeals,ElvenSolarHits,ElvenHexes,ElvenFireHits,ElvenBarrages;
    public int ElvenGraceChance(int team)=>Mathf.Min(40,20*Huts.Count(h=>h.team==team&&h.paid&&h.hp>0&&Lines[h.kind].building=="h059"));
    void ElvenDied(Unit unit,int killerTeam){
        if(killerTeam<0||unit.noCorpse||unit.summoned||unit.summonLeft>0||ElementalIllusion(unit)||unit.abilities.Contains("A07G")||unit.abilities.Contains("A07H")||!((string)Lines[unit.kind].stats["unit_types"]??"").ToLowerInvariant().Contains("sapper"))return;
        if(ElvenGraceChance(unit.team)>0)ElvenJobs.Add(new ElvenJob{kind="grace",body=unit,left=2});
    }
    void TickElven(float dt){
        foreach(var job in ElvenJobs.ToArray()){
            job.left-=dt;if(job.left>.00001f)continue;ElvenJobs.Remove(job);
            if(job.kind=="grace"){
                var old=job.body;if(old==null||old.hp>0||!Units.Contains(old)||old.deadAge>=99||old.noCorpse)continue;
                int roll=job.roll<0?(int)(Roll()*100):job.roll;if(roll>=ElvenGraceChance(old.team))continue;
                var fresh=Spawn(old.team,old.kind,old.position);fresh.onRoad=true;fresh.leaveCastleLane=false;Corpses.RemoveAll(c=>c.unit==old);old.deadAge=100;Resurrections++;ElvenReturns++;PulseAt(fresh.position,1,Color.yellow,"Благодать",1.5f,"elven:elven_resurrect:effect");continue;
            }
            int count=Mathf.Min(job.ticks,1+Mathf.FloorToInt(-job.left));
            for(int i=0;i<count;i++)if(job.target==-1){if(CastleHp[1-job.team]>0)DamageCastle(1-job.team,job.power,"magic",true);}else DamageHut(FindHut(job.target),job.power,"magic",job.team,true);
            job.ticks-=count;if(job.ticks>0){job.left+=count;ElvenJobs.Add(job);}
        }
    }
    Unit[] ElvenTargets(int team,Vector2 origin,JToken cfg){
        string kind=(string)cfg["kind"];
        if(kind=="elven_hex")return Units.Where(u=>NatureEligible(u,team)).ToArray();
        var mask=(JObject)cfg.DeepClone();if(kind=="elven_solar")mask["targets"]=(string)cfg["origin_fields"]?["atar"];
        bool ally=kind=="elven_heal";
        return Units.Where(u=>Valid(u,team,mask,ally)&&Vector2.Distance(u.position,origin)<=F(cfg,"range")&&(ally?u.hp<u.maxHp:!IsNagaHero(u))).ToArray();
    }
    bool CastElvenUnit(Unit source,JToken cfg,Unit suggested){
        var targets=ElvenTargets(source.team,source.position,cfg);if(targets.Length==0)return false;
        string kind=(string)cfg["kind"];var chosen=targets.Contains(suggested)?suggested:kind=="elven_heal"?targets.OrderBy(u=>u.hp/u.maxHp).First():targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];
        if(kind=="elven_heal"){
            Heal(chosen,F(cfg["origin_fields"],"Hea1"));var wave=Abilities[(string)cfg["effect_id"]];var used=new HashSet<int>();var current=chosen;Vector2 from=source.position;float beamHeight=Height(source)+1;float amount=F(wave,"power");
            for(int i=0;i<(int)F(wave,"count",1)&&current!=null;i++){
                used.Add(current.id);Heal(current,amount);ElvenBeam(source.team,from,current.position,i==0?"HWPB":"HWSB",beamHeight,Height(current)+1);beamHeight=Height(current)+1;PulseAt(current.position,1,Color.green,"",1,"elven:"+(string)wave["id"]+":target");from=current.position;amount*=1-F(wave,"falloff");
                current=Units.Where(u=>!used.Contains(u.id)&&Valid(u,source.team,wave,true)&&u.hp<u.maxHp&&Vector2.Distance(u.position,from)<=F(wave,"area")).OrderBy(u=>(u.position-from).sqrMagnitude).FirstOrDefault();
            }
            source.castPause=Mathf.Max(source.castPause,.8f);ElvenHeals++;return true;
        }
        if(kind=="elven_solar"){
            var child=Abilities["A08U"];foreach(var victim in Units.Where(u=>u.hp>0&&u.team!=source.team&&!Invulnerable(u)&&Flying(u)&&Organic(u)&&((string)Lines[u.kind].stats["unit_types"]??"").ToLowerInvariant().Contains("sapper")&&Vector2.Distance(u.position,chosen.position)<=400f/30).Take(4).ToArray())ElvenMissile(source.id,source.kind,source.team,source.position,victim.id,victim.position,child);
            return true;
        }
        return false;
    }
    public bool CastElvenBuilding(Hut hut,string id,int suggested=-1){
        var cfg=Abilities[id];if(hut==null||hut.hp<=0||!hut.paid||hut.paused||BuildingDisabled(hut)>0||!Lines[hut.kind].abilities.Contains(id)||hut.mana<F(cfg,"mana")||hut.spellTimers.TryGetValue(id,out float wait)&&wait>0)return false;
        if((string)cfg?["kind"]=="auto_fire"){if(!ElvenAutoFire(hut.id,hut.kind,hut.team,hut.position,cfg,suggested))return false;}
        else if((string)cfg?["kind"]=="elven_hex"){
            var targets=ElvenTargets(hut.team,hut.position,cfg);if(targets.Length==0)return false;var target=targets.FirstOrDefault(u=>u.id==suggested)??targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];
            if(!NagaShieldBlock(target)&&Valid(target,hut.team,Abilities["A018"],false)){
                var status=(JObject)Abilities["A018"].DeepClone();status["move_override"]=Flying(target)?5f:50f/30;ApplyEffectData(target,status,hut.team);target.pendingSpell=null;target.castLeft=target.strikeLeft=-1;PulseAt(target.position,1,Color.magenta,"Сглаз",1,"elven:A018:special");
            }
            ElvenHexes++;
        }else return false;
        hut.mana-=F(cfg,"mana");hut.spellTimers[id]=BuildingCooldown(hut,F(cfg,"cd"));hut.attackSerial++;hut.attackPoseLeft=.5f;SpellsCast++;return true;
    }
    void TickElvenBuilding(Hut hut){foreach(string id in Lines[hut.kind].abilities)if(new[]{"elven_hex","auto_fire"}.Contains((string)Abilities[id]?["kind"])&&(!hut.spellTimers.TryGetValue(id,out float wait)||wait<=0))hut.spellTimers[id]=CastElvenBuilding(hut,id)?BuildingCooldown(hut,F(Abilities[id],"cd")):.25f;}
    bool ElvenBurning(int team,int target,string id)=>target>=0?FindUnit(target)?.effects.Any(e=>e.id==id&&e.left>0)==true:ElvenJobs.Any(j=>j.team==team&&j.target==target&&j.ability==id);
    bool ElvenAutoFire(int source,int line,int team,Vector2 point,JToken cfg,int suggested=-1){
        string id=(string)cfg["id"];var choices=new List<Tuple<int,Vector2>>();
        foreach(var u in Units)if(Valid(u,team,cfg,false)&&Vector2.Distance(u.position,point)<=F(cfg,"range")&&!ElvenBurning(team,u.id,id))choices.Add(Tuple.Create(u.id,u.position));
        if(((string)cfg["targets"]??"").Contains("structure")){
            foreach(var hut in Huts)if(hut.team!=team&&hut.paid&&hut.hp>0&&Vector2.Distance(hut.position,point)<=F(cfg,"range")&&!ElvenBurning(team,HutTarget(hut),id))choices.Add(Tuple.Create(HutTarget(hut),hut.position));
            if(CastleHp[1-team]>0&&Vector2.Distance(CastlePosition(1-team),point)<=F(cfg,"range")&&!ElvenBurning(team,-1,id))choices.Add(Tuple.Create(-1,CastlePosition(1-team)));
        }
        if(choices.Count==0)return false;var target=choices.FirstOrDefault(v=>v.Item1==suggested)??choices[Mathf.Min(choices.Count-1,(int)(Roll()*choices.Count))];ElvenMissile(source,line,team,point,target.Item1,target.Item2,cfg);return true;
    }
    void ElvenMissile(int source,int line,int team,Vector2 from,int target,Vector2 point,JToken cfg){
        var fields=cfg["fields"];var w=new Weapon{type="missile",attack="magic",missile="elven:"+(string)cfg["id"],speed=F(fields,"amsp")/30,homing=F(fields,"amho",1)!=0,arc=F(fields,"amac"),air=true,ground=true,structure=true};var shot=AddShot(team,line,source,target,from,point,F(cfg,"power"),w,0,3);shot.chaosSpell="elven:"+(string)cfg["id"];
    }
    bool ImpactElvenShot(Shot shot){
        if(!(shot.chaosSpell??"").StartsWith("elven:"))return false;string id=shot.chaosSpell.Substring(6);var cfg=Abilities[id];var target=FindUnit(shot.target);
        if(target!=null){if(!Valid(target,shot.team,cfg,false)||id=="A08U"&&NagaShieldBlock(target))return true;DamageUnit(target,shot.damage,shot.team);if(target.hp>0){if(id=="A08U"){target.stun=Mathf.Max(target.stun,F(cfg,IsNagaHero(target)||target.abilities.Any(a=>(string)Abilities[a]?["kind"]=="resistant_skin")?"hero_duration":"duration"));ElvenSolarHits++;}else if(F(cfg,"dps")>0){var status=(JObject)cfg.DeepClone();status["power"]=0;status["kind"]="debuff";ApplyEffectData(target,status,shot.team);}}}
        else if(shot.target<0&&ElvenStructureAlive(shot.target,shot.team)&&((string)cfg["targets"]??"").Contains("structure")){
            if(shot.target==-1)DamageCastle(1-shot.team,shot.damage,"magic",true);else DamageHut(FindHut(shot.target),shot.damage,"magic",shot.team,true);
            if(F(cfg,"dps")>0&&ElvenStructureAlive(shot.target,shot.team))ElvenJobs.Add(new ElvenJob{kind="burn",team=shot.team,target=shot.target,ability=id,power=F(cfg,"dps"),ticks=(int)F(cfg,"duration"),left=1});
        }else return true;
        ElvenFireHits++;PulseAt(shot.end,1,Color.yellow,"",1,"elven:"+id+":target");return true;
    }
    void TickElvenPassive(Unit source){if(Silenced(source))return;foreach(string id in source.abilities.Where(id=>id=="A010"))if(HasSpell(source,id))source.spellTimers[id]=ElvenAutoFire(source.id,source.kind,source.team,source.position,Abilities[id])?SpellCooldown(source,id):.25f;}
    void ElvenBeam(int team,Vector2 from,Vector2 to,string native,float fromHeight=1,float toHeight=1){var shot=AddShot(team,0,-1,-1,from,to,0,new Weapon{type="instant",attack="magic",missile="elven_beam:"+native},0,4);shot.duration=2;shot.hit=true;shot.startHeight=fromHeight;shot.endHeight=toHeight;}
    public float ElvenWeaponFactor(int team,bool critical)=>TeamElven[team]&&Aspects[team]==1?.92f*(critical?1.25f:1):1;
    void ElvenBlood(Unit source,Unit target,float dealt,string attack){if(source!=null&&NightFighter(source,source.team)&&source.team!=target.team&&TeamElven[source.team]&&Aspects[source.team]==0&&dealt>0&&attack=="magic"&&target.mana>0){target.mana=Mathf.Max(0,target.mana-15);PulseAt(target.position,1,Color.red,"−15 маны",.7f,"elven:elven_blood:trigger");}}
    public bool ElvenReveals(Unit target,int team)=>target.effects.Any(e=>e.left>0&&(string)e.config?["code"]=="Afae"&&(bool?)e.config?["reveals"]==true&&e.sourceTeam==team);
    bool ElvenSpellShield(Unit target){
        if(target.spellTimers.TryGetValue("spell_shield",out float wait)&&wait>0)return false;
        foreach(string id in target.abilities)if((string)Abilities[id]?["kind"]=="shield"){
            target.spellTimers["spell_shield"]=F(Abilities[id],"cd");PulseAt(target.position,1.5f,Color.cyan,"Магический щит");return true;
        }
        return false;
    }
    bool ElvenStructureAlive(int target,int team)=>target==-1?CastleHp[1-team]>0:FindHut(target)!=null;
    void ElvenTowerBarrage(Hut source,Weapon weapon,int primary){
        if(!Lines[source.kind].abilities.Contains("A015"))return;
        var cfg=Abilities["A015"];var mask=(JObject)cfg.DeepClone();mask["physical_control"]=true;
        foreach(var target in Units.Where(u=>u.id!=primary&&Valid(u,source.team,mask,false)&&Vector2.Distance(source.position,u.position)<=F(cfg,"secondary_range")).OrderBy(u=>(u.position-source.position).sqrMagnitude).Take((int)F(cfg,"count")-1)){
            var w=weapon.Copy();w.pattern=null;w.missile="elven:A015";w.speed=F(cfg["fields"],"amsp")/30;w.homing=F(cfg["fields"],"amho",1)!=0;w.arc=F(cfg["fields"],"amac");
            var shot=AddShot(source.team,source.kind,source.id,target.id,source.position,target.position,w.damage*MechBuildingDamage(source)*ElvenWeaponFactor(source.team,false),w,0,3);shot.nightSecondary=true;ElvenBarrages++;
        }
    }
    List<Tuple<int,Vector2>> ElvenPatternTargets(Shot shot,bool area){
        var pattern=shot.weapon.pattern;var mask=new JObject{["targets"]=(string)pattern["targets"],["physical_control"]=true,["aoe"]=area};var result=new List<Tuple<int,Vector2>>();
        foreach(var u in Units)if(Valid(u,shot.team,mask,false)&&!Invulnerable(u)&&!Hidden(u)&&NightCanDetect(shot.team,u))result.Add(Tuple.Create(u.id,u.position));
        if(((string)pattern["targets"]??"").Contains("structure")){
            foreach(var h in Huts)if(h.team!=shot.team&&h.paid&&h.hp>0)result.Add(Tuple.Create(HutTarget(h),h.position));
            if(CastleHp[1-shot.team]>0)result.Add(Tuple.Create(-1,CastlePosition(1-shot.team)));
        }
        return result;
    }
    bool ElvenWeaponPattern(Shot shot){
        if(shot.weapon?.pattern==null||!new[]{"h00U","e005"}.Contains(Lines[shot.line].unit))return false;
        var pattern=shot.weapon.pattern;bool line=(string)pattern["type"]=="line";
        if(line){
            var direction=(shot.end-shot.start).normalized;
            foreach(var entry in ElvenPatternTargets(shot,true)){
                if(entry.Item1==shot.target)continue;var offset=entry.Item2-shot.end;float along=Vector2.Dot(offset,direction);
                if(along<0||along>F(pattern,"length")+.00001f||(offset-direction*along).magnitude>F(pattern,"width")+.00001f)continue;
                ResolveAttack(shot.sourceId,shot.team,entry.Item1,shot.damage,shot.weapon,0,false);
            }
            return true;
        }
        int remaining=shot.nightRemaining<0?(int)F(pattern,"count")-1:shot.nightRemaining-1;if(remaining<=0)return true;
        var seen=new HashSet<int>(shot.nightSeen??Array.Empty<int>());seen.Add(shot.target);
        var next=ElvenPatternTargets(shot,false).Where(v=>!seen.Contains(v.Item1)&&Vector2.Distance(v.Item2,shot.end)<=F(pattern,"radius")).OrderBy(v=>(v.Item2-shot.end).sqrMagnitude).FirstOrDefault();
        if(next!=null){var child=AddShot(shot.team,shot.line,shot.sourceId,next.Item1,shot.end,next.Item2,shot.damage*(1-F(pattern,"loss")),shot.weapon,0,3);child.nightSeen=seen.ToArray();child.nightRemaining=remaining;}
        return true;
    }
}
}
