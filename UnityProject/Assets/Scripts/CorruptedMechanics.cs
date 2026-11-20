using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    public sealed class CorruptedRay {public int id,team,source;public Vector2 origin,direction;public float distance,artDistance;public readonly HashSet<int> struck=new HashSet<int>();}
    public sealed class CorruptedFlight {public Unit unit;public int source,team;public float age;}
    public sealed class CorruptedWave {public int id,team;public Vector2 origin,direction;public float distance;public readonly HashSet<int> struck=new HashSet<int>();}
    public sealed class CorruptedJob {public string kind;public int team;public Vector2 point;public float left;public JObject config;public Unit body;}
    public readonly List<CorruptedRay> CorruptedRays=new List<CorruptedRay>();
    public readonly List<CorruptedFlight> CorruptedFlights=new List<CorruptedFlight>();
    public readonly List<CorruptedWave> CorruptedWaves=new List<CorruptedWave>();
    public readonly List<CorruptedJob> CorruptedJobs=new List<CorruptedJob>();
    public int CorruptedCharms,CorruptedPrisons,CorruptedImpales,CorruptedEnhanced,CorruptedIncinerations,CorruptedParasites;
    public bool CorruptedAirborne(Unit u)=>u!=null&&u.effects.Any(e=>e.left>0&&(bool?)e.config?["impale_airborne"]==true);
    public float CorruptedLift(Unit u)=>CorruptedFlights.Where(f=>f.unit==u).Select(f=>Mathf.Sin(Mathf.PI*Mathf.Clamp01(f.age/Mathf.Max(.001f,F(Abilities["A0AF"]?["fields"],"Uim4"))))*2).DefaultIfEmpty(0).Max();
    bool CorruptedFighter(Unit u)=>u!=null&&u.hp>0&&!Invulnerable(u)&&!Hidden(u)&&Lines[u.kind].unit!="YTct"&&Lines[u.kind].unit!="n02E"&&!ElementalIllusion(u);
    Unit[] CorruptedTargets(int team,Vector2 point,JToken cfg) {
        string kind=(string)cfg["kind"];
        if(kind=="corrupted_impale"){var mask=(JObject)cfg.DeepClone();mask["targets"]=(string)cfg["origin_fields"]?["atar"];return Units.Where(u=>Valid(u,team,mask,false)&&!IsNagaHero(u)&&Vector2.Distance(point,u.position)<=F(cfg,"range")).ToArray();}
        return Units.Where(u=>NatureEligible(u,team)&&!CorruptedAirborne(u)&&(kind!="corrupted_tentacles"||!Flying(u))).ToArray();
    }
    bool CastCorruptedUnit(Unit source,JToken cfg,Unit suggested) {
        var targets=CorruptedTargets(source.team,source.position,cfg);if(targets.Length==0)return false;var target=targets.Contains(suggested)?suggested:targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];
        float heading=Mathf.Atan2(target.position.y-source.position.y,target.position.x-source.position.x);
        foreach(int angle in new[]{-20,-10,0,10}){float a=heading+angle*Mathf.Deg2Rad;CorruptedRays.Add(new CorruptedRay{id=nextId++,team=source.team,source=source.id,origin=source.position,direction=new Vector2(Mathf.Cos(a),Mathf.Sin(a))});}
        source.corruptedCastPause=F(Lines[source.kind].stats,"cast_point",.3f);CorruptedImpales++;return true;
    }
    void TickCorruptedBuilding(Hut h){foreach(string id in Lines[h.kind].abilities)if(new[]{"corrupted_pain","corrupted_tentacles"}.Contains((string)Abilities[id]?["kind"])&&(!h.spellTimers.TryGetValue(id,out float left)||left<=0))h.spellTimers[id]=CastCorruptedBuilding(h,id)?BuildingCooldown(h,F(Abilities[id],"cd")):.25f;}
    public bool CastCorruptedBuilding(Hut source,string id,int suggested=-1) {
        var cfg=Abilities[id];if(source==null||!Huts.Contains(source)||!source.paid||source.paused||source.hp<=0||BuildingDisabled(source)>0||cfg==null||!Lines[source.kind].abilities.Contains(id)||source.mana<F(cfg,"mana")||source.spellTimers.TryGetValue(id,out float wait)&&wait>0)return false;
        var targets=CorruptedTargets(source.team,source.position,cfg);if(targets.Length==0)return false;var target=targets.FirstOrDefault(u=>u.id==suggested)??targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];
        if((string)cfg["kind"]=="corrupted_pain"){PulseAt(target.position,2,new Color(.7f,.15f,.8f),"Касание боли",.8f,"corrupted:A057:target");if(!NagaShieldBlock(target))DamageUnit(target,200,source.team);}
        else if((string)cfg["kind"]=="corrupted_tentacles"){
            for(int i=0;i<6;i++){float angle=i*60*Mathf.Deg2Rad;var tentacle=NatureSummon(source.team,"n01C",target.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*64/30,9);tentacle.direction=new Vector2(Mathf.Cos(-angle),Mathf.Sin(-angle));tentacle.pose="Birth";tentacle.castPause=.3f;}CorruptedPrisons++;
        }else return false;
        source.mana-=F(cfg,"mana");source.spellTimers[id]=BuildingCooldown(source,F(cfg,"cd"));source.attackSerial++;source.attackPoseLeft=.5f;SpellsCast++;return true;
    }
    void CorruptedEnhance(Unit source,ref Weapon weapon) {
        string id=source.abilities.Contains("A069")?"A069":source.abilities.Contains("A04Z")?"A04Z":null;if(id==null||ElementalIllusion(source))return;
        var cfg=Abilities[id];var target=FindUnit(source.attackTarget);var mask=(JObject)cfg.DeepClone();mask["physical_control"]=true;
        if(source.mana<F(cfg,"mana")||!Valid(target,source.team,mask,false))return;
        source.mana-=F(cfg,"mana");var f=cfg["fields"];var copy=weapon.Copy();
        copy.type="msplash";copy.missile="corrupted:"+id;copy.speed=F(f,"amsp")/30;copy.homing=true;copy.arc=0;copy.full=F(f,"fak4")/30;copy.half=F(f,"fak5")/30;copy.quarter=F(f,"aare")/30;copy.halfFactor=F(f,"fak2");copy.quarterFactor=F(f,"fak3");copy.structure=false;weapon=copy;source.shotDamage+=F(f,"fak1");CorruptedEnhanced++;
    }
    bool CorruptedOrb(Unit source,Unit target,JToken child) {
        string id=(string)child?["id"];if(id=="A04Q"||id=="A055"||id=="A0AB"){if(Valid(target,source.team,child,false)){ApplyEffect(target,id,source.team);PulseAt(target.position,1,Color.magenta,"",.5f,"corrupted:"+id+":caster");}return true;}
        if(id=="A04O"){
            if(Valid(target,source.team,child,false)&&!IsNagaHero(target)&&F(Lines[target.kind].stats,"level",1)<=F(child,"level_limit",10)&&!target.abilities.Contains("A070")&&!target.abilities.Contains("A06V")){
                PepeChangeTeam(target);target.team=source.team;target.combatTarget=target.attackTarget=-1;target.strikeLeft=target.castLeft=-1;target.pendingSpell=null;target.leaveCastleLane=false;target.onRoad=true;target.direction=source.team==0?Vector2.right:Vector2.left;ChaosSealBonus(target);CorruptedCharms++;PulseAt(target.position,2,Color.magenta,"Подчинение",1.2f,"corrupted:A04O:target");
            }return true;
        }
        if(id=="A04X"){if(Valid(target,source.team,child,false))LaunchCorruptedWave(source,target.position);return true;}return false;
    }
    void LaunchCorruptedWave(Unit source,Vector2 target){var direction=target-source.position;if(direction.sqrMagnitude<.001f)direction=source.team==0?Vector2.right:Vector2.left;CorruptedWaves.Add(new CorruptedWave{id=nextId++,team=source.team,origin=source.position,direction=direction.normalized});}
    void CorruptedStructureHit(Unit source,int target){if(source.hp>0&&(target==-1||FindHut(target)!=null)&&!ElementalIllusion(source)&&source.abilities.Contains("A04W"))LaunchCorruptedWave(source,target==-1?new Vector2(CastlePosition(1-source.team).x,0):TargetPosition(target,source.team));}
    float CorruptedIncinerate(Unit source,Unit target) {
        if(source==null||ElementalIllusion(source))return 0;
        foreach(string id in source.abilities){var cfg=Abilities[id];if((string)cfg?["kind"]!="incinerate"||!Valid(target,source.team,cfg,false)||!Organic(target))continue;
            int stacks=1+target.effects.Where(e=>e.left>0&&(string)e.config?["kind"]=="incinerate").Sum(e=>(int?)e.config["stacks"]??0);var mark=(JObject)cfg.DeepClone();mark["stacks"]=stacks;ApplyEffectData(target,mark,source.team);var effect=target.effects.First(e=>e.id==id);effect.power=0;return F(cfg,"power")*stacks;
        }return 0;
    }
    void CorruptedDied(Unit u) {
        if(ElementalIllusion(u))return;
        var incinerate=u.effects.FirstOrDefault(e=>e.left>0&&(string)e.config?["kind"]=="incinerate");if(incinerate!=null){u.noCorpse=true;CorruptedJobs.Add(new CorruptedJob{kind="incinerate",body=u,team=incinerate.sourceTeam,point=u.position,left=Mathf.Max(.001f,F(incinerate.config,"blast_delay")),config=(JObject)incinerate.config.DeepClone()});CorruptedIncinerations++;}
        foreach(string id in u.abilities){var cfg=Abilities[id];if((string)cfg?["kind"]!="death_blast")continue;CorruptedBlast(u.team,u.position,cfg,false);}
        int side=1-u.team;if(!TeamCorrupted[side]||Aspects[side]!=1||u.summoned||u.summonLeft>0||u.noBounty||Invulnerable(u)||Hidden(u)||Lines[u.kind].unit=="YTct"||Lines[u.kind].unit=="n02E"||Lines.Any(l=>l.unit==Lines[u.kind].unit&&l.slots>0)||!Units.Any(v=>v.team==side&&CorruptedFighter(v)&&Vector2.Distance(v.position,u.position)<=500f/30)||Units.Count(v=>v.team==side&&v.hp>0&&v.corruptedThrall)+CorruptedJobs.Count(v=>v.kind=="parasite"&&v.team==side)>=6||Roll()>=.2f)return;
        CorruptedJobs.Add(new CorruptedJob{kind="parasite",team=side,point=u.position});
    }
    void CorruptedBlast(int team,Vector2 point,JToken cfg,bool incinerate) {
        float full=F(cfg,"area"),outer=F(cfg,"half_area",full);using(var snapshot035_1=SimulationBuffers035.Take(Units))foreach(var u in snapshot035_1)if(u.hp>0&&u.team!=team&&!Invulnerable(u)&&!Hidden(u)&&!CorruptedAirborne(u)&&u.spellResist<1&&Lines[u.kind].unit!="YTct"&&(incinerate||Valid(u,team,cfg,false))){float d=Vector2.Distance(point,u.position);if(d<=outer)DamageUnit(u,F(cfg,incinerate?d<=full?"blast":"half_blast":d<=full?"power":"half_power"),team);}
        if(!incinerate&&((string)cfg["targets"]??"").Contains("structure")){using(var snapshot035_2=SimulationBuffers035.Take(Huts))foreach(var h in snapshot035_2)if(h.team!=team&&h.paid&&h.hp>0){float d=Vector2.Distance(h.position,point);if(d<=outer)DamageHut(h,F(cfg,d<=full?"power":"half_power"),"magic",team,true);}float gap=Vector2.Distance(point,CastlePosition(1-team));if(gap<=outer)DamageCastle(1-team,F(cfg,gap<=full?"power":"half_power"),"magic",true);}
        PulseAt(point,outer,new Color(.6f,.2f,1),"",.8f,"corrupted:incinerate_blast:effect");
    }
    bool CorruptedTouches(Vector2 offset,Vector2 direction,float previous,float distance,float radius,float width){float along=Vector2.Dot(offset,direction);return along>=Mathf.Max(0,previous-radius)&&along<=distance+radius&&(offset-direction*along).magnitude<=width+radius;}
    void AdvanceCorruptedFlight(CorruptedFlight flight,float dt){flight.age+=dt;var u=flight.unit;var f=Abilities["A0AF"]["fields"];foreach(var e in u.effects.Where(e=>(bool?)e.config?["impale_airborne"]==true))e.left=3600;if(flight.age<F(f,"Uim4"))return;u.effects.RemoveAll(e=>(bool?)e.config?["impale_airborne"]==true);CorruptedFlights.Remove(flight);if(u.hp<=0)return;DamageUnit(u,F(f,"Uim3"),flight.team,"magic",true,false,Units.FirstOrDefault(v=>v.id==flight.source));if(u.hp>0){var cfg=(JObject)Abilities["A0AF"].DeepClone();cfg["power"]=0;cfg["disable"]=true;ApplyEffectData(u,cfg,flight.team);}}
    void TickCorrupted(float dt) {
        foreach(var u in Units)u.corruptedCastPause=Mathf.Max(0,u.corruptedCastPause-dt);using(var snapshot035_3=SimulationBuffers035.Take(CorruptedFlights))foreach(var flight in snapshot035_3)AdvanceCorruptedFlight(flight,dt);
        var f=Abilities["A0AF"]?["fields"];if(f!=null){float length=F(f,"Uim1")/30,velocity=length/Mathf.Max(.001f,F(f,"Uim2"));using(var snapshot035_4=SimulationBuffers035.Take(CorruptedRays))foreach(var ray in snapshot035_4){
            float previous=ray.distance;ray.distance=Mathf.Min(length,ray.distance+velocity*dt);while(ray.artDistance<=ray.distance){PulseAt(ray.origin+ray.direction*ray.artDistance,1,Color.magenta,"",1.4f,"corrupted:A0AF:special");ray.artDistance+=80f/30;}
            using(var snapshot035_5=SimulationBuffers035.Take(Units))foreach(var u in snapshot035_5)if(!ray.struck.Contains(u.id)&&Valid(u,ray.team,Abilities["A0AF"],false)&&CorruptedTouches(u.position-ray.origin,ray.direction,previous,ray.distance,F(Lines[u.kind].stats,"collision"),F(f,"aare")/30)){
                ray.struck.Add(u.id);if(CorruptedAirborne(u)||u.effects.Any(e=>e.left>0&&e.id=="A0AF"))continue;
                ApplyEffectData(u,new JObject{["id"]="corrupted_impale_air",["name"]="Подбрасывание",["duration"]=3600,["impale_airborne"]=true,["disable"]=true,["untargetable"]=true},ray.team);u.strikeLeft=u.castLeft=-1;u.pendingSpell=null;
                var flight=new CorruptedFlight{unit=u,source=ray.source,team=ray.team};CorruptedFlights.Add(flight);float along=Vector2.Dot(u.position-ray.origin,ray.direction);AdvanceCorruptedFlight(flight,Mathf.Max(0,dt-Mathf.Max(0,along-previous)/velocity));
            }if(ray.distance>=length)CorruptedRays.Remove(ray);
        }}
        var cfg=Abilities["A04X"];using(var snapshot035_6=SimulationBuffers035.Take(CorruptedWaves))foreach(var wave in snapshot035_6){float previous=wave.distance;wave.distance=Mathf.Min(F(cfg,"wave_length"),wave.distance+F(cfg,"missile_speed")*dt);
            using(var snapshot035_7=SimulationBuffers035.Take(Units))foreach(var u in snapshot035_7)if(!wave.struck.Contains(u.id)&&Valid(u,wave.team,cfg,false)&&CorruptedTouches(u.position-wave.origin,wave.direction,previous,wave.distance,F(Lines[u.kind].stats,"collision"),F(cfg,"start_width"))){wave.struck.Add(u.id);DamageUnit(u,F(cfg,"power"),wave.team);}
            using(var snapshot035_8=SimulationBuffers035.Take(Huts))foreach(var h in snapshot035_8)if(h.team!=wave.team&&h.paid&&h.hp>0&&!wave.struck.Contains(HutTarget(h))&&CorruptedTouches(h.position-wave.origin,wave.direction,previous,wave.distance,F(Lines[h.kind].buildingStats,"collision"),F(cfg,"start_width"))){wave.struck.Add(HutTarget(h));DamageHut(h,F(cfg,"power"),"magic",wave.team,true);}
            Vector2 castle=new Vector2(CastlePosition(1-wave.team).x,0);if(!wave.struck.Contains(-1)&&CorruptedTouches(castle-wave.origin,wave.direction,previous,wave.distance,0,F(cfg,"start_width"))){wave.struck.Add(-1);DamageCastle(1-wave.team,F(cfg,"power"),"magic",true);}if(wave.distance>=F(cfg,"wave_length"))CorruptedWaves.Remove(wave);
        }
        using(var snapshot035_9=SimulationBuffers035.Take(CorruptedJobs))foreach(var job in snapshot035_9)if((job.left-=dt)<=0){CorruptedJobs.Remove(job);if(job.kind=="incinerate"){if(job.body!=null)job.body.deadAge=100;CorruptedBlast(job.team,job.point,job.config,true);}else if(job.kind=="parasite"){
            var u=NatureSummon(job.team,"n01I",job.point,25);u.corruptedThrall=true;u.noBounty=true;u.abilities.Clear();u.abilityLevels.Clear();u.effects.Clear();u.armor=0;u.maxHp=u.hp=180;u.attackOverride=12;u.cooldownOverride=1;u.critChance=u.evasion=u.spellResist=0;CorruptedParasites++;
        }}
    }
    void TickCorruptedRot(float step){for(int side=0;side<2;side++)if(TeamCorrupted[side]&&Aspects[side]==0){var sources=Units.Where(u=>u.team==side&&CorruptedFighter(u)).ToArray();using(var snapshot035_10=SimulationBuffers035.Take(Units))foreach(var enemy in snapshot035_10)if(enemy.team!=side&&CorruptedFighter(enemy)&&enemy.spellResist<1&&sources.Any(u=>Vector2.Distance(u.position,enemy.position)<=200f/30)){DamageUnit(enemy,8*step,side);PulseAt(enemy.position,1,new Color(.4f,.7f,.2f),"",.4f,"corrupted:rot:effect");}}}
    void CorruptedAuras(){foreach(var h in Huts)if(h.paid&&h.hp>0)foreach(var id in Lines[h.kind].abilities){var p=Abilities[id]?["profile"];if((string)p?["kind"]!="aura"||F(p["effects"],"armor")>=0)continue;foreach(var u in Units)if(u.hp>0&&u.team!=h.team&&!Invulnerable(u)&&!Hidden(u)&&!CorruptedAirborne(u)&&u.spellResist<1&&Lines[u.kind].unit!="YTct"&&Vector2.Distance(h.position,u.position)<=F(p,"radius")){u.auraArmorNegative=Mathf.Min(u.auraArmorNegative,F(p["effects"],"armor"));u.auraIds.Add(id);}}}
}
}
