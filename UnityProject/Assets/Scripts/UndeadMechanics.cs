using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    public sealed class UndeadZone {public int id,team;public string kind;public Vector2 point;public float left,clock;}
    public sealed class UndeadWave {public int id,team;public Vector2 origin,direction;public float distance,spent;public readonly HashSet<int> struck=new HashSet<int>();}
    public sealed class UndeadBirth {public int team;public string raw;public Vector2 point;public string[] grants;}
    public readonly List<UndeadZone> UndeadZones=new List<UndeadZone>();
    public readonly List<UndeadWave> UndeadWaves=new List<UndeadWave>();
    public readonly List<UndeadBirth> UndeadBirths=new List<UndeadBirth>();
    public int UndeadRaised,UndeadConversions,UndeadExecutions,UndeadDecays,UndeadInfections,UndeadTransfers;
    float undeadPlagueClock;
    static readonly HashSet<string> UndeadSkeletons=new HashSet<string>{"u002","n00D","u00A","n00C","u00B","n01L","u003","n01M"};
    static readonly string[] UndeadPlagueAuraIds={"A01L","A05P","A05T"};
    internal static bool IsUndeadPlagueAura(string id)=>Array.IndexOf(UndeadPlagueAuraIds,id)>=0;
    static void NameUndeadPlagueAuras(JObject abilities){
        foreach(var id in UndeadPlagueAuraIds){
            if(!(abilities[id] is JObject aura)||!(aura["profile"] is JObject profile))continue;
            aura["name"]="Чума";
            profile["name"]="Чума";
            profile["display"]="Чума";
            aura["icon"]=@"ReplaceableTextures\PassiveButtons\PASBTNPlagueCloud.blp";
            aura["description"]="Наносит "+F(profile["effects"],"damage_tick")+" магического урона в секунду вражеским органическим бойцам в радиусе "+Mathf.RoundToInt(F(profile,"radius")*30)+".";
        }
    }
    public bool UndeadArmy(int team)=>TeamUndead[team]&&Aspects[team]==0;
    bool UndeadRaiser(Hut h)=>Lines[h.kind].abilities.Any(id=>(string)Abilities[id]?["kind"]=="undead_raise_building");
    void UndeadCreated(Unit u) {if(UndeadArmy(u.team)&&UndeadSkeletons.Contains(Lines[u.kind].unit)){u.maxHp*=1.2f;u.hp*=1.2f;}}
    Corpse[] UndeadBodies(Vector2 point,JToken cfg,bool building)=>Corpses.Where(c=>c.left>0&&(building?((string)Lines[c.unit.kind].stats["unit_types"]??"").ToLowerInvariant().Contains("sapper")&&!((string)Lines[c.unit.kind].stats["unit_types"]??"").ToLowerInvariant().Contains("undead"):Vector2.Distance(point,c.unit.position)<=F(cfg,"range"))).OrderBy(c=>(c.unit.position-point).sqrMagnitude).ToArray();
    bool UndeadExecuteTarget(Unit u,int side)=>u!=null&&u.hp>0&&u.team!=side&&!Hidden(u)&&!Invulnerable(u)&&((string)Lines[u.kind].stats["unit_types"]??"").ToLowerInvariant().Contains("sapper")&&!u.abilities.Contains("A08H");
    bool UndeadReady(Unit u,JToken cfg)=>UndeadBodies(u.position,cfg,false).Length>0;
    bool CastUndeadUnit(Unit u,JToken cfg)=>RaiseUndead(u.team,u.position,cfg,false);
    bool RaiseUndead(int team,Vector2 point,JToken cfg,bool building) {
        var bodies=UndeadBodies(point,cfg,building);if(bodies.Length==0)return false;var body=building?bodies[Mathf.Min(bodies.Length-1,(int)(Roll()*bodies.Length))]:bodies[0];
        Corpses.Remove(body);body.unit.deadAge=100;int count=building?1:(int?)cfg["count"]??1;var pool=Strings(cfg[building?"pool":"summon_pool"]);string rare=(string)cfg[building?"rare":"summon_rare"];
        for(int i=0;i<count;i++){bool special=!string.IsNullOrEmpty(rare)&&Roll()<(building?.1f:.125f);string raw=special?rare:pool[Mathf.Min(pool.Length-1,(int)(Roll()*pool.Length))];Vector2 at=body.unit.position+(count>1?new Vector2(Mathf.Cos(i*Mathf.PI*2/5),Mathf.Sin(i*Mathf.PI*2/5))*1.5f:Vector2.zero);UndeadBirths.Add(new UndeadBirth {team=team,raw=raw,point=at,grants=Strings(cfg["summon_grants"])});UndeadRaised++;}
        PulseAt(body.unit.position,2,new Color(.5f,1,.4f),"Поднятие мёртвых",1,"undead_raise");return true;
    }
    public void FlushUndeadBirths() {using(var snapshot035_1=SimulationBuffers035.Take(UndeadBirths))foreach(var b in snapshot035_1){UndeadBirths.Remove(b);int k=Array.FindIndex(Lines,v=>v.unit==b.raw);if(k<0)continue;var u=Spawn(b.team,k,b.point);u.onRoad=true;u.leaveCastleLane=false;u.pose="Birth";u.attackPoseLeft=.3f;u.spellBorn=true;foreach(var id in b.grants??new string[0])Grant(u,id);}}
    void TickUndeadBuilding(Hut h) {foreach(var id in Lines[h.kind].abilities)if(new[]{"undead_raise_building","undead_execute"}.Contains((string)Abilities[id]?["kind"])&&(!h.spellTimers.TryGetValue(id,out float wait)||wait<=0))h.spellTimers[id]=CastUndeadBuilding(h,id)?BuildingCooldown(h,F(Abilities[id],"cd")):.25f;}
    public bool CastUndeadBuilding(Hut h,string id,int suggested=-1) {
        var cfg=Abilities[id];if(h==null||h.hp<=0||!h.paid||h.paused||!Huts.Contains(h)||BuildingDisabled(h)>0||cfg==null||!Lines[h.kind].abilities.Contains(id)||h.mana<F(cfg,"mana")||h.spellTimers.TryGetValue(id,out float wait)&&wait>0)return false;
        string kind=(string)cfg["kind"];if(kind=="undead_raise_building"){if(!RaiseUndead(h.team,h.position,cfg,true))return false;}
        else if(kind=="undead_execute"){var targets=Units.Where(u=>UndeadExecuteTarget(u,h.team)).ToArray();if(targets.Length==0)return false;var victim=targets.FirstOrDefault(u=>u.id==suggested)??targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];if(!NagaShieldBlock(victim))DamageUnit(victim,50000,h.team,"normal",true,false,null,false,true);UndeadExecutions++;PulseAt(victim.position,2,new Color(.7f,1,.3f),"Внезапная смерть",1,"undead_execute");}
        else return false;
        h.mana-=F(cfg,"mana");h.spellTimers[id]=BuildingCooldown(h,F(cfg,"cd"));h.attackPoseLeft=1;h.attackSerial++;SpellsCast++;return true;
    }
    bool UndeadVampireKill(Unit killer,Unit victim) {
        if(killer==null||killer.hp<=0||killer.team==victim.team||ElementalIllusion(killer)||ElementalIllusion(victim))return false;
        string raw=Lines[killer.kind].unit=="h01U"?"h01V":Lines[killer.kind].unit=="h05H"?"h01U":null,types=((string)Lines[victim.kind].stats["unit_types"]??"").ToLowerInvariant();
        if(raw==null||!types.Contains("sapper")||types.Contains("undead")||types.Contains("mechanical")||victim.abilities.Contains("A08H"))return false;
        victim.noCorpse=true;victim.deadAge=100;UndeadBirths.Add(new UndeadBirth{team=killer.team,raw=raw,point=victim.position});UndeadConversions++;PulseAt(victim.position,2,Color.green,"Обращение",1,"undead_raise");return true;
    }
    public void UndeadAfterDamage(Unit source,Unit victim,float actual,float previous,int roll=-1) {
        if(source==null||source.hp<=0||Lines[source.kind].unit!="u00D"||!source.abilities.Contains("A03Q")||ElementalIllusion(source)||victim==null||victim.hp<=0)return;
        int n=roll<0?(int)(Roll()*100):roll;bool mechanical=!Organic(victim);
        if(n<15&&!victim.abilities.Contains("A070")&&!mechanical){Heal(source,previous*.5f);DamageUnit(victim,10000,source.team,"chaos",true,false,source,true,true);PulseAt(victim.position,2,Color.green,"Похищение жизни",1,"undead_soul");}
        else if(n<25)UndeadDecay(source.team,victim.position);
    }
    public void UndeadStructureHit(Unit source,int target,int roll=-1) {if(source==null||source.hp<=0||Lines[source.kind].unit!="u00D"||!source.abilities.Contains("A03Q")||ElementalIllusion(source))return;int n=roll<0?(int)(Roll()*100):roll;var h=FindHut(target);if(target!=-1&&h==null)return;bool mechanical=target==-1||h==null||((string)Lines[h.kind].buildingStats["unit_types"]??"").ToLowerInvariant().Contains("mechanical");if(n<15&&!mechanical&&!Lines[h.kind].abilities.Contains("A070")){Heal(source,h.hp*.5f);DamageHut(h,10000,"chaos",source.team,true,true);}else if(n<25)UndeadDecay(source.team,target==-1?new Vector2(BattleCastlePosition(1-source.team).x,0):TargetPosition(target,source.team));}
    public void UndeadDecay(int team,Vector2 at){UndeadZones.Add(new UndeadZone{id=nextId++,team=team,kind="decay",point=at,left=F(Abilities["A086"],"duration")});UndeadDecays++;PulseAt(at,F(Abilities["A086"],"area"),new Color(.4f,.8f,.25f),"Порча",F(Abilities["A086"],"duration"),"undead_decay");}
    bool UndeadPlagueTarget(Unit u,int team,JToken cfg)=>u.hp>0&&u.team!=team&&!Hidden(u)&&Lines[u.kind].unit!="YTct"&&!Invulnerable(u)&&!((string)Lines[u.kind].stats["unit_types"]??"").ToLowerInvariant().Contains("undead")&&Organic(u)&&!Ethereal(u)&&!u.effects.Any(e=>e.left>0&&(bool?)e.config?["untargetable"]==true)&&(!Flying(u)||((string)cfg["targets"]??"").Contains("air"));
    public void UndeadInfect(Vector2 point,int team,JToken cfg){using(var snapshot035_2=SimulationBuffers035.Take(Units))foreach(var u in snapshot035_2)if(UndeadPlagueTarget(u,team,cfg)&&Vector2.Distance(point,u.position)<=F(cfg,"area")){ApplyEffectData(u,new JObject{["id"]="A05R",["kind"]="poison",["name"]="Чума",["duration"]=F(cfg["fields"],"Apl1"),["dps"]=F(cfg["fields"],"Apl2"),["damage_type"]="chaos",["ignore_armor"]=true,["minimum_hp"]=2,["undispellable"]=true,["stack_group"]="undead_plague",["stack_strength"]=F(cfg["fields"],"Apl2")},team);UndeadInfections++;}}
    void UndeadDied(Unit u,Unit killer) {
        UndeadVampireKill(killer,u);
        if(u.abilities.Contains("A05R")&&!ElementalIllusion(u)){float life=F(Abilities["A05R"]?["fields"],"Apl3");if(life>0){UndeadZones.Add(new UndeadZone{id=nextId++,team=u.team,kind="plague",point=u.position,left=life});PulseAt(u.position,F(Abilities["Aap2"],"area"),Color.green,"Чумное облако",life,"undead_cloud");}}
        foreach(var effect in u.effects.Where(e=>(bool?)e.config?["aspect_plague"]==true&&e.left>0&&F(e.config,"aspect_hops")<2).ToArray()) {
            var neighbors=Units.Where(v=>v!=u&&v.hp>0&&v.team!=effect.sourceTeam&&MechFighter(v)&&v.spellResist<1&&!v.effects.Any(e=>e.id==effect.id)&&Vector2.Distance(v.position,u.position)<=5).OrderBy(v=>(v.position-u.position).sqrMagnitude).Take(3).ToArray();
            foreach(var v in neighbors){var copy=(JObject)effect.config.DeepClone();copy["duration"]=effect.left;copy["aspect_hops"]=F(copy,"aspect_hops")+1;ApplyEffectData(v,copy,effect.sourceTeam);UndeadTransfers++;}
        }
    }
    void UndeadWeaponHit(Unit source,Unit target,float dealt,Weapon weapon) {
        if(dealt<=0||source==null||source.hp<=0||target==null)return;
        if(weapon.type=="normal"&&target.spellResist<1&&Organic(source)&&source.auraVampiric>0)Heal(source,dealt*source.auraVampiric);
        if(TeamUndead[source.team]&&Aspects[source.team]==1&&source.team!=target.team&&MechFighter(source)&&target.hp>0&&target.spellResist<1)ApplyEffectData(target,new JObject{["id"]="aspect_plague_"+source.team,["name"]="Чума",["kind"]="debuff",["duration"]=8,["dps"]=6,["damage_type"]="magic",["aspect_plague"]=true,["aspect_hops"]=0},source.team);
    }
    void UndeadBuildingAuras(){foreach(var h in Huts)if(h.paid&&h.hp>0)foreach(var id in Lines[h.kind].abilities){var profile=Abilities[id]?["profile"];if((string)profile?["kind"]!="aura"||F(profile["effects"],"damage_tick")<=0)continue;foreach(var u in Units)if(u.hp>0&&u.team!=h.team&&!Invulnerable(u)&&Organic(u)&&u.spellResist<1&&Lines[u.kind].unit!="YTct"&&Vector2.Distance(u.position,h.position)<=F(profile,"radius")){u.auraDamageTick=Mathf.Max(u.auraDamageTick,F(profile["effects"],"damage_tick"));u.auraIds.Add(id);}}}
    bool UndeadOrb(Unit source,Unit target,JToken effect) {if((string)effect?["id"]!="A05M")return false;var direction=target.position-source.position;if(direction.sqrMagnitude<.0001f)direction=source.team==0?Vector2.right:Vector2.left;UndeadWaves.Add(new UndeadWave{id=nextId++,team=source.team,origin=source.position,direction=direction.normalized});return true;}
    void TickUndead(float dt) {
        undeadPlagueClock+=dt;bool pulse=undeadPlagueClock>=.5f;if(pulse){undeadPlagueClock%=.5f;using(var snapshot035_3=SimulationBuffers035.Take(Units))foreach(var u in snapshot035_3)if(u.hp>0&&u.abilities.Contains("A05R")&&!ElementalIllusion(u))UndeadInfect(u.position,u.team,Abilities["A05R"]);}
        using(var snapshot035_4=SimulationBuffers035.Take(UndeadZones))foreach(var zone in snapshot035_4){float step=Mathf.Min(dt,zone.left);zone.left-=step;if(zone.kind=="plague"){if(pulse)UndeadInfect(zone.point,zone.team,Abilities["Aap2"]);}else{zone.clock+=step;while(zone.clock>=.25f-.000001f){zone.clock=Mathf.Max(0,zone.clock-.25f);var cfg=(JObject)Abilities["A086"].DeepClone();cfg["physical_control"]=true;using(var snapshot035_5=SimulationBuffers035.Take(Units))foreach(var u in snapshot035_5)if(Valid(u,zone.team,cfg,false)&&Vector2.Distance(u.position,zone.point)<=F(cfg,"area"))DamageUnit(u,u.maxHp*F(cfg["fields"],"Udd1")*.25f,zone.team,"chaos",true,false,null,false,true);}}if(zone.left<=.000001f)UndeadZones.Remove(zone);}
        var waveCfg=Abilities["A05M"];using(var snapshot035_6=SimulationBuffers035.Take(UndeadWaves))foreach(var wave in snapshot035_6){float before=wave.distance;wave.distance=Mathf.Min(F(waveCfg,"wave_length"),before+F(waveCfg,"missile_speed")*dt);using(var snapshot035_7=SimulationBuffers035.Take(Units))foreach(var u in snapshot035_7)if(!wave.struck.Contains(u.id)&&Valid(u,wave.team,waveCfg,false)){var offset=u.position-wave.origin;float along=Vector2.Dot(offset,wave.direction),collision=F(Lines[u.kind].stats,"collision"),width=Mathf.Lerp(F(waveCfg,"start_width"),F(waveCfg,"end_width"),Mathf.Clamp01(along/F(waveCfg,"wave_length")));if(along<Mathf.Max(0,before-collision)||along>wave.distance+collision||(offset-wave.direction*along).magnitude>width+collision)continue;wave.struck.Add(u.id);float hit=Mathf.Min(F(waveCfg,"power"),Mathf.Max(0,F(waveCfg,"max_damage")-wave.spent));wave.spent+=hit;DamageUnit(u,hit,wave.team);}if(wave.distance>=F(waveCfg,"wave_length"))UndeadWaves.Remove(wave);}
    }
}
}
