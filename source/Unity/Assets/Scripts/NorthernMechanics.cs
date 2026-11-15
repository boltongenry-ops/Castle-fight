using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    public sealed class NorthernFreeze {public int target,team;public string id;public float left,tick,dps;}
    public sealed class NorthernWinter {public int source,team;public Vector2 point;public float age;public bool spawned;}
    public sealed class NorthernOrb {public int id,team,controlTicks;public Vector2 position,previous;public float heading,accumulator,fire=.33f;}
    public readonly List<NorthernFreeze> NorthernFreezes=new List<NorthernFreeze>();
    public readonly List<NorthernWinter> NorthernWinters=new List<NorthernWinter>();
    public readonly List<NorthernOrb> NorthernOrbs=new List<NorthernOrb>();
    public int NorthernBolts,NorthernRoots,NorthernBursts,NorthernBarrages,NorthernControls,NorthernRages;
    public float NorthernWeather;
    static readonly HashSet<string> NorthernFrostIds=new HashSet<string> {"A03Y","A045","A043","Bfrz","BNbf","NWY2","NWY3","NWY4"};
    bool ShortNorthernControl(Unit u)=>Lines[u.kind].armorType=="hero"||u.abilities.Any(id=>(string)Abilities[id]?["kind"]=="resistant_skin");
    public float FrostDuration(int team,float duration,bool hero=false)=>TeamNorthern[team]&&Aspects[team]==0?duration+Mathf.Min(hero?1:2,duration*.35f):duration;
    float NorthernStatusDuration(Unit u,JObject cfg,int team,float duration) {
        string id=(string)cfg["id"]??"",group=(string)cfg["stack_group"]??"";
        return team!=u.team&&(NorthernFrostIds.Contains(id)||group.StartsWith("frost",StringComparison.Ordinal)||group=="northern_ice_chains")?FrostDuration(team,duration,ShortNorthernControl(u)):duration;
    }
    public float FrostDisabled(Hut h)=>NorthernFreezes.Where(v=>v.target==HutTarget(h)).Select(v=>v.left).DefaultIfEmpty(0).Max();
    public float BuildingDisabled(Hut h)=>Mathf.Max(HellDisabled(h),FrostDisabled(h));
    public bool NorthernChannel(Hut h)=>NorthernWinters.Any(v=>v.source==h.id&&v.age<5.2f);
    void NorthernRage(Unit u) {
        if(!TeamNorthern[u.team]||Aspects[u.team]!=1||u.northernRageUsed||u.hp<=0||u.hp>=u.maxHp*.3f||Hidden(u)||(bool?)Lines[u.kind].stats["invulnerable"]==true||ElementalIllusion(u)||Lines[u.kind].unit=="YTct")return;
        u.northernRageUsed=true;NorthernRages++;
        ApplyEffectData(u,new JObject { ["id"]="aspect_north_rage",["name"]="Берсерк Севера",["kind"]="buff",["duration"]=8,["haste"]=.5f,["power"]=.25f,["speed"]=.25f,["undispellable"]=true },u.team);
        PulseAt(u.position,2,new Color(1,.45f,.3f),"Берсерк Севера");
    }
    void NorthernChill(Unit target,int team,JToken cfg,float duration=-1,float heroDuration=-1) {
        if(!Valid(target,team,cfg,false))return;
        ApplyEffectData(target,new JObject { ["id"]="A03Y",["name"]="Охлаждение",["kind"]="debuff",["duration"]=duration<0?F(cfg,"duration",5):duration,["hero_duration"]=heroDuration<0?F(cfg,"hero_duration",5):heroDuration,["slow"]=.5f,["haste"]=-.25f,["stack_group"]="frost_chill" },team);
    }
    void NorthernAfterWeapon(int sourceId,Unit victim,Weapon weapon) {
        if(victim==null||victim.hp<=0)return;var source=Units.FirstOrDefault(v=>v.id==sourceId);var tower=Huts.FirstOrDefault(v=>v.id==sourceId);
        int team=source?.team??tower?.team??-1;if(team<0)return;
        foreach(string id in source!=null?source.abilities: tower!=null?Lines[tower.kind].abilities:Enumerable.Empty<string>())if((string)Abilities[id]?["kind"]=="frost_attack")NorthernChill(victim,team,Abilities[id]);
        if(source==null||source.hp<=0||weapon.type!="normal")return;
        var armor=victim.effects.FirstOrDefault(e=>(bool?)e.config?["frost_armor"]==true);
        if(armor!=null){var cfg=(JObject)armor.config.DeepClone();cfg["targets"]="air,ground,enemies";NorthernChill(source,victim.team,cfg,F(cfg,"chill_duration"),F(cfg,"chill_hero_duration"));}
    }
    bool NorthernOrbEffect(Unit source,Unit target,JToken cfg) {
        if((string)cfg?["stack_group"]!="northern_ice_chains")return false;
        if(Valid(target,source.team,cfg,false)){ApplyEffectData(target,(JObject)cfg,source.team);NorthernRoots++;PulseAt(target.position,1.5f,Color.cyan,"Ледяные оковы");}return true;
    }
    void FreezeNorthernStructure(int target,int team,string id,float duration,float dps=0) {
        float left=FrostDuration(team,duration);
        if(id=="Bfrz")left=Mathf.Max(left,NorthernFreezes.Where(v=>v.target==target&&v.team==team&&v.id==id).Select(v=>v.left).DefaultIfEmpty(0).Max());
        NorthernFreezes.RemoveAll(v=>v.target==target&&v.team==team&&v.id==id);
        NorthernFreezes.Add(new NorthernFreeze {target=target,team=team,id=id,left=left,dps=dps});
    }
    void NorthernStructureHit(Unit source,int target) {
        if(source==null||source.hp<=0||!source.abilities.Contains("A046")||Roll()>=F(Abilities["A046"],"chance"))return;
        var cfg=Abilities["A045"];FreezeNorthernStructure(target,source.team,"A045",F(cfg,"duration"),F(cfg,"dps"));NorthernRoots++;
        PulseAt(TargetPosition(target,source.team),2,Color.cyan,"Королевские оковы");
    }
    Unit[] NorthernSpellTargets(Unit source,JToken cfg) {
        var mask=(JObject)cfg.DeepClone();mask["targets"]=(string)cfg["origin_fields"]?["atar"]??(string)cfg["targets"];
        return Units.Where(v=>Valid(v,source.team,mask,false)&&!IsNagaHero(v)&&Vector2.Distance(v.position,source.position)<=F(cfg,"range")).ToArray();
    }
    bool NorthernUnitReady(Unit source,JToken cfg)=>NorthernSpellTargets(source,cfg).Length>0;
    bool CastNorthernUnit(Unit source,JToken cfg,Unit suggested) {
        var targets=NorthernSpellTargets(source,cfg);if(targets.Length==0)return false;
        var target=targets.Contains(suggested)?suggested:targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];string kind=(string)cfg["kind"],effect=(string)cfg["effect_id"];
        var child=Abilities[effect];var center=kind=="north_nova"?target.position:source.position;
        foreach(var u in Units.ToArray())if(Valid(u,source.team,child,false)&&Vector2.Distance(u.position,center)<=F(child,"area")) {
            if(kind=="north_howl")ApplyEffect(u,effect,source.team);
            else {
                DamageUnit(u,kind=="north_nova"&&u!=target?F(child,"secondary",F(child,"power")):F(child,"power"),source.team);
                if(u.hp<=0)continue;var status=(JObject)child.DeepClone();status["power"]=0;
                if(kind=="north_stomp")status["disable"]=true;
                ApplyEffectData(u,status,source.team);
            }
        }
        PulseAt(center,F(child,"area"),new Color(.4f,.75f,1),kind=="north_nova"?"Ледяная звезда":kind=="north_howl"?"Леденящий крик":"Громовая поступь");NorthernBursts++;return true;
    }
    void TickNorthernBuilding(Hut h) {
        foreach(string id in Lines[h.kind].abilities)if((string)Abilities[id]?["kind"] is string kind&&new[]{"north_winter","north_frost_bolt","north_mushroom"}.Contains(kind)&&(!h.spellTimers.TryGetValue(id,out float left)||left<=0))h.spellTimers[id]=CastNorthernBuilding(h,id)?BuildingCooldown(h,F(Abilities[id],"cd")):.25f;
    }
    public bool CastNorthernBuilding(Hut source,string id,int suggested=-1) {
        var cfg=Abilities[id];if(source==null||!Huts.Contains(source)||!source.paid||source.hp<=0||source.paused||BuildingDisabled(source)>0||cfg==null||!Lines[source.kind].abilities.Contains(id)||source.mana<F(cfg,"mana")||source.spellTimers.TryGetValue(id,out float wait)&&wait>0)return false;
        string kind=(string)cfg["kind"];
        if(kind=="north_winter")NorthernWinters.Add(new NorthernWinter {source=source.id,team=source.team,point=source.position});
        else if(kind=="north_frost_bolt") {
            var targets=Huts.Where(h=>h.paid&&h.hp>0&&h.team!=source.team&&!MechPowered(h)&&Lines[h.kind].building!="hcas"&&Lines[h.kind].building!="h06M").ToArray();if(targets.Length==0)return false;
            var target=targets.FirstOrDefault(h=>h.id==suggested||HutTarget(h)==suggested)??targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];
            var shot=AddShot(source.team,source.kind,source.id,HutTarget(target),source.position,target.position,0,Weapon.Read(cfg["missile_weapon"]),0,3);shot.chaosSpell="north_frost:"+id;NorthernBolts++;
        } else if(kind=="north_mushroom") {
            var targets=Units.Where(u=>NatureEligible(u,source.team)&&Flying(u)).ToArray();if(targets.Length==0)return false;
            var target=targets.FirstOrDefault(u=>u.id==suggested)??targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];
            if(!NagaShieldBlock(target))NorthernSpellShot(source.team,source.kind,source.id,source.position,target.id,"A0AK",3);
        } else return false;
        source.mana-=F(cfg,"mana");source.spellTimers[id]=BuildingCooldown(source,F(cfg,"cd"));source.attackSerial++;source.attackPoseLeft=.5f;SpellsCast++;return true;
    }
    Shot NorthernSpellShot(int team,int kind,int owner,Vector2 start,int target,string id,int shotKind=3) {
        var cfg=Abilities[id];var fields=cfg["fields"];var w=new Weapon {type="missile",attack="magic",speed=F(fields,"amsp")/30,homing=F(fields,"amho",1)!=0,arc=F(fields,"amac"),air=true,ground=true,structure=true};
        var shot=AddShot(team,kind,owner,target,start,TargetPosition(target,team),F(cfg,"power"),w,0,shotKind);shot.chaosSpell="north_spell:"+id;return shot;
    }
    bool ImpactNorthernShot(Shot shot) {
        if(string.IsNullOrEmpty(shot.chaosSpell)||!shot.chaosSpell.StartsWith("north_",StringComparison.Ordinal))return false;
        if(shot.chaosSpell=="north_barrage"){if(TargetAlive(shot.target))ResolveAttack(shot.sourceId,shot.team,shot.target,shot.damage,shot.weapon,0,false);return true;}
        string id=shot.chaosSpell.Substring(shot.chaosSpell.IndexOf(':')+1);var cfg=Abilities[id];
        if(shot.chaosSpell.StartsWith("north_frost:",StringComparison.Ordinal)) {
            float radius=F(cfg,"area");
            foreach(var u in Units.ToArray())if(u.hp>0&&u.team!=shot.team&&!Hidden(u)&&(bool?)Lines[u.kind].stats["invulnerable"]!=true&&!Flying(u)&&(id=="A04L"&&Vector2.Distance(u.position,shot.end)<=radius))DamageUnit(u,F(cfg,"power"),shot.team,"chaos",false);
            foreach(var h in Huts.ToArray())if(h.paid&&h.hp>0&&h.team!=shot.team&&(HutTarget(h)==shot.target||id=="A04L"&&Vector2.Distance(h.position,shot.end)<=radius)){DamageHut(h,F(cfg,"power"),"chaos",shot.team);if(h.hp>0)FreezeNorthernStructure(HutTarget(h),shot.team,"Bfrz",F(cfg,"freeze"));}
            if(id=="A04L"&&Vector2.Distance(CastlePosition(1-shot.team),shot.end)<=radius)DamageCastle(1-shot.team,F(cfg,"power"),"chaos");
            PulseAt(shot.end,Mathf.Max(2,radius),Color.cyan,"Заморозка");return true;
        }
        var target=FindUnit(shot.target);
        if(target!=null&&Valid(target,shot.team,cfg,false)) {
            DamageUnit(target,F(cfg,"power"),shot.team);
            if(target.hp>0&&F(cfg,"stun")>0){target.stun=Mathf.Max(target.stun,F(cfg,ShortNorthernControl(target)?"hero_duration":"stun"));NorthernControls++;}
        } else if(id=="A080"&&target==null&&TargetAlive(shot.target)) {var hut=FindHut(shot.target);if(hut!=null)DamageHut(hut,F(cfg,"power"),"magic",shot.team,true);else if(shot.target==-1)DamageCastle(1-shot.team,F(cfg,"power"),"magic",true);}
        PulseAt(shot.end,1,Color.cyan,"Лёд");return true;
    }
    void NorthernBarrage(Unit source,Weapon active) {
        foreach(string id in source.abilities) {
            var cfg=Abilities[id];if((bool?)cfg?["projectile_barrage"]!=true||id=="A03N"||id=="A03O")continue;
            var mask=(JObject)cfg.DeepClone();mask["physical_control"]=true;
            var targets=Units.Where(v=>v.id!=source.attackTarget&&Valid(v,source.team,mask,false)&&Vector2.Distance(v.position,source.position)<=F(cfg,"area")).OrderBy(v=>(v.position-source.position).sqrMagnitude).Take(Mathf.Max(0,((int?)cfg["count"]??1)-1)).ToArray();
            foreach(var target in targets){var w=Weapon.Read(Lines[source.kind].stats["weapons"][0]);w.speed=F(cfg["fields"],"amsp")/30;w.homing=F(cfg["fields"],"amho",1)!=0;w.arc=F(cfg["fields"],"amac");float damage=active.damage*Mathf.Max(0,1+Stat(source,"damage"))*Mathf.Max(0,1+source.auraDamage+source.auraDamageNegative)*InventoryDamage(source);var shot=AddShot(source.team,source.kind,source.id,target.id,source.position,target.position,damage,w,0,3);shot.chaosSpell="north_barrage";NorthernBarrages++;}
        }
    }
    public NorthernOrb SpawnNorthernOrb(int team,Vector2 point,float nativeDegrees) {var orb=new NorthernOrb {id=nextId++,team=team,position=point,previous=point,heading=-nativeDegrees*Mathf.Deg2Rad};NorthernOrbs.Add(orb);return orb;}
    void NorthernOrbFire(NorthernOrb orb) {
        var cfg=Abilities["A080"];float range=F(cfg,"range");var targets=Units.Where(v=>Valid(v,orb.team,cfg,false)&&Vector2.Distance(v.position,orb.position)<=range).Select(v=>v.id).ToList();
        targets.AddRange(Huts.Where(v=>v.paid&&v.hp>0&&v.team!=orb.team&&Vector2.Distance(v.position,orb.position)<=range).Select(HutTarget));if(CastleHp[1-orb.team]>0&&Vector2.Distance(CastlePosition(1-orb.team),orb.position)<=range)targets.Add(-1);
        if(targets.Count==0)return;int target=targets[Mathf.Min(targets.Count-1,(int)(Roll()*targets.Count))];var shot=NorthernSpellShot(orb.team,Mathf.Max(0,Kind("h03O")),-orb.id,orb.position,target,"A080");shot.startHeight=F(Abilities["A04I"],"orbFlyHeight",10);
    }
    void NorthernOrbControl(NorthernOrb orb) {
        var targets=Units.Where(v=>NatureEligible(v,orb.team)&&Vector2.Distance(v.position,orb.position)<=700f/30).ToArray();if(targets.Length==0)return;
        var target=targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];string id=Flying(target)?"A082":"A083";var shot=NorthernSpellShot(orb.team,Mathf.Max(0,Kind("h03O")),-orb.id,target.position,target.id,id);shot.startHeight=Height(target)+1;shot.duration=.05f;
        if(!Flying(target)&&Valid(target,orb.team,Abilities["A04H"],false))ApplyEffect(target,"A04H",orb.team);
    }
    void TickNorthern(float dt) {
        NorthernWeather=Mathf.Max(0,NorthernWeather-dt);
        foreach(var freeze in NorthernFreezes.ToArray()) {
            var hut=FindHut(freeze.target);bool castle=freeze.target==-1;if(!castle&&hut==null||castle&&CastleHp[1-freeze.team]<=0){NorthernFreezes.Remove(freeze);continue;}
            float step=Mathf.Min(dt,Mathf.Max(0,freeze.left));freeze.left-=dt;freeze.tick+=step;float due=freeze.left<=0?freeze.tick:Mathf.Floor(freeze.tick);
            if(due>0){freeze.tick-=due;if(freeze.dps>0){if(castle)DamageCastle(1-freeze.team,freeze.dps*due,"magic",true);else DamageHut(hut,freeze.dps*due,"magic",freeze.team,true);}}if(freeze.left<=0)NorthernFreezes.Remove(freeze);
        }
        foreach(var winter in NorthernWinters.ToArray()) {winter.age+=dt;if(!winter.spawned&&winter.age>=1.5f){winter.spawned=true;for(int i=0;i<3;i++)SpawnNorthernOrb(winter.team,winter.point,Roll()*110-55+(winter.team==0?0:180));NorthernWeather=Mathf.Max(NorthernWeather,7.8f-winter.age);}if(winter.age>=7.8f)NorthernWinters.Remove(winter);}
        foreach(var orb in NorthernOrbs.ToArray()) {
            orb.accumulator+=dt;
            while(orb.accumulator+.000001f>=.04f) {
                orb.accumulator-=.04f;orb.previous=orb.position;float x=orb.position.x+10f/30*Mathf.Cos(orb.heading),y=orb.position.y+10f/30*Mathf.Sin(orb.heading);
                if(Mathf.Abs(x)>=6400f/30){NorthernOrbs.Remove(orb);break;}if(Mathf.Abs(y)>=3840f/30){y=orb.position.y+Mathf.Sin(-orb.heading);orb.heading=-orb.heading;}orb.position=new Vector2(x,y);
                if(++orb.controlTicks>50){orb.controlTicks=0;NorthernOrbControl(orb);}
            }
            if(!NorthernOrbs.Contains(orb))continue;orb.fire-=dt;while(orb.fire<=.000001f){orb.fire+=F(Abilities["A080"],"cd");NorthernOrbFire(orb);}
            var aura=Abilities["A081"];foreach(var u in Units.ToArray())if(Valid(u,orb.team,aura,false)&&Vector2.Distance(u.position,orb.position)<=F(aura,"area")) {
                var status=u.effects.FirstOrDefault(e=>e.id=="A081");
                if(status!=null){status.left=status.lifetime=.6f;status.sourceTeam=orb.team;}
                else ApplyEffectData(u,new JObject { ["id"]="A081",["name"]="Аура зимы",["duration"]=.6f,["speed"]=F(aura["fields"],"Oae1"),["haste"]=F(aura["fields"],"Oae2") },orb.team);
            }
        }
    }
}
}
