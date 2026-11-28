using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    sealed class HumanJob {public string kind;public int source,target,team,count;public Vector2 center;public float left;}
    public sealed class Fire {public int id,team,sourceId;public string ability;public Vector2 center;public float age,nextFull,nextTail;}
    readonly List<HumanJob> humanJobs=new List<HumanJob>();public readonly List<Fire> Fires=new List<Fire>();
    bool Valid(Unit u,int team,JToken cfg,bool ally) {
        if(u==null||u.hp<=0||!ally&&!NightCanDetect(team,u)||Hidden(u)||CorruptedAirborne(u)||Lines[u.kind].unit=="n02E"||(u.team==team)!=ally||Invulnerable(u))return false;string flags=(string)cfg?["targets"]??"";
        if(Lines[u.kind].unit=="YTct")return flags.Contains("tree");string types=((string)Lines[u.kind].stats["unit_types"]??"").ToLowerInvariant();if((string)cfg?["code"]=="AHbn"&&types.Contains("mechanical")||flags.Contains("nonancient")&&types.Contains("ancient"))return false;
        if((bool?)cfg?["exclude_ethereal"]==true&&(Ethereal(u)||u.effects.Any(e=>e.left>0&&(bool?)e.config?["untargetable"]==true)))return false;
        if(!ally&&u.spellResist>=1&&(bool?)cfg?["physical_control"]!=true)return false;if(flags.Contains("organic")&&!Organic(u))return false;
        if(Flying(u)&&flags.Contains("ground")&&!flags.Contains("air"))return false;
        if(!Flying(u)&&flags.Contains("air")&&!flags.Contains("ground"))return false;return true;
    }
    public void ApplyEffect(Unit u,string id,int sourceTeam=-1,int sourceId=0) {
        if(u==null)return;var c=Abilities[id];if(c!=null){var data=(JObject)c;if(NerubVenom(c)){data=(JObject)data.DeepClone();data["nerub_source"]=sourceId;}ApplyEffectData(u,data,sourceTeam<0?u.team:sourceTeam);}
    }
    void ApplyEffectData(Unit u,JObject c,int sourceTeam) {
        if(u==null||u.hp<=0)return;
        if(IsHordeWarchief(u)&&sourceTeam>=0&&sourceTeam!=u.team&&(bool?)c["physical_control"]!=true)return;
        c=NerubStatus(u,c,sourceTeam);if(ApplyNerubVenom(u,c,sourceTeam))return;string id=(string)c["id"],group=(string)c["stack_group"]??id;float strength=F(c,"stack_strength",F(c,"dps"));
        if(u.effects.Any(e=>e.group==group&&e.strength>strength))return;
        // Refreshing the same poison extends its duration without postponing its next damage tick.
        var previous=(string)c["kind"]=="poison"&&F(c,"dps")>0?u.effects.FirstOrDefault(e=>e.id==id&&e.sourceTeam==sourceTeam&&e.left>0):null;
        float dotTick=previous?.dotTick??0;
        u.effects.RemoveAll(e=>e.id==id||e.group==group);
        bool shortControl=Lines[u.kind].armorType=="hero"||u.abilities.Any(a=>(string)Abilities[a]?["kind"]=="resistant_skin");
        float duration=NorthernStatusDuration(u,c,sourceTeam,F(c,shortControl?"hero_duration":"duration",F(c,"duration")));
        u.effects.Add(new Effect {id=id,group=group,left=duration,lifetime=duration,dotTick=dotTick,config=(JObject)c.DeepClone(),sourceTeam=sourceTeam<0?u.team:sourceTeam,power=F(c,"power"),armor=F(c,"armor"),haste=F(c,"haste"),speed=F(c,"speed")-F(c,"slow"),strength=strength,incoming=F(c,"incoming"),root=(bool?)c["root"]??false,ground=(bool?)c["ground"]??false});
    }
    void TickEffects(Unit u,float dt){using(var snapshot035_1=SimulationBuffers035.Take(u.effects))foreach(var e in snapshot035_1){float step=Mathf.Min(dt,Mathf.Max(0,e.left));e.left-=dt;var c=e.config;
        if(F(c,"dps")>0){e.dotTick+=step;float interval=Mathf.Max(.05f,F(c,"interval",1));float due=e.left<=0?e.dotTick:Mathf.Floor(e.dotTick/interval)*interval;if(due>0){e.dotTick-=due;NerubPeriodicHit(u,e,due);if(u.hp<=0)return;}}
        if(e.id=="A02T"){float age=e.lifetime-e.left;float duration=F(c?["fields"],e.lifetime<=3.01f?"Prg5":"Prg4");e.root=age<duration;e.speed=age<e.lifetime*.8f?-.8f:0;}
        if((string)c?["kind"]=="regen"){u.hp=Mathf.Min(u.maxHp,u.hp+e.power*step/Mathf.Max(.01f,e.lifetime));u.mana=Mathf.Min(u.maxMana,u.mana+F(c,"restore_mana")*step/Mathf.Max(.01f,e.lifetime));}
        else if(F(c,"regen")>0)u.hp=Mathf.Min(u.maxHp,u.hp+F(c,"regen")*step);
        if(e.rainRelay&&e.left<=0&&u.effects.Contains(e))RelayQuackShield(u,e);
        }u.effects.RemoveAll(e=>e.left<=0);RefreshNerubVenomFields(u);}
    void UpdateAuras() {
        foreach(var u in Units){u.auraSpeed=0;u.auraSpeedNegative=0;u.auraHasteNegative=0;u.auraHaste=0;u.auraArmorNegative=0;u.auraVampiric=0;u.auraDamageNegative=0;u.auraArmor=0;u.auraMana=0;u.auraDamage=0;u.auraRegen=0;u.auraThorns=0;u.auraDamageTick=0;u.auraIds.Clear();}
        var queries035=SimulationState035.Get(this).auraQueries;queries035.Clear();
        foreach(var source in Units)if(source.hp>0)foreach(var id in source.abilities){var cfg=Ranked(source,id);if((string)cfg?["kind"]!="aura"||(id=="A00F"&&source.warlockPhase>=0))continue;var profile=cfg["profile"];if(profile!=null)queries035.Add(new AuraBatch035.Query{source=source,id=id,profile=profile});}
        using(var batch035=new AuraBatch035(queries035,Units,SimulationState035.Get(this).reference))
        for(int row035=0;row035<queries035.Count;row035++){
            var query035=queries035[row035];var source=query035.source;var id=query035.id;var profile=query035.profile;
            bool enemy=(bool?)profile["enemy"]??false;string flags=(string)profile["targets"]??"air,ground";
            for(int column035=0;column035<Units.Count;column035++) {
                var u=Units[column035];if(!batch035.Contains(row035,column035,u))continue;
                if((bool?)profile["melee_only"]==true&&!Lines[u.kind].weapons.Any(w=>w.type=="normal"))continue;
                if(u.hp<=0||Lines[u.kind].unit=="YTct"||(u.team!=source.team)!=enemy||enemy&&u.spellResist>=1)continue;
                if(Invulnerable(u)&&!flags.Contains("invu"))continue;
                if(flags.Contains("organic")&&!Organic(u)||flags.Contains("mechanical")&&Organic(u))continue;
                if(flags=="self"&&u!=source)continue;
                if(Flying(u)&&flags.Contains("ground")&&!flags.Contains("air")||!Flying(u)&&flags.Contains("air")&&!flags.Contains("ground"))continue;
                u.auraIds.Add(id);var effects=profile["effects"];u.auraHaste=Mathf.Max(u.auraHaste,F(effects,"haste"));u.auraHasteNegative=Mathf.Min(u.auraHasteNegative,F(effects,"haste"));u.auraSpeed=Mathf.Max(u.auraSpeed,F(effects,"speed"));u.auraSpeedNegative=Mathf.Min(u.auraSpeedNegative,F(effects,"speed"));u.auraArmorNegative=Mathf.Min(u.auraArmorNegative,F(effects,"armor"));u.auraVampiric=Mathf.Max(u.auraVampiric,F(effects,"vampiric"));u.auraDamageNegative=Mathf.Min(u.auraDamageNegative,F(effects,"damage"));u.auraArmor=Mathf.Max(u.auraArmor,F(effects,"armor"));u.auraMana=Mathf.Max(u.auraMana,F(effects,"mana"));u.auraDamage=Mathf.Max(u.auraDamage,F(effects,"damage"));u.auraRegen=Mathf.Max(u.auraRegen,F(effects,"regen"));u.auraThorns=Mathf.Max(u.auraThorns,F(effects,"thorns"));u.auraDamageTick=Mathf.Max(u.auraDamageTick,F(effects,"damage_tick"));
            }
        }
        MechBuildingAuras();UndeadBuildingAuras();CorruptedAuras();
        using(var snapshot035_2=SimulationBuffers035.Take(Units))foreach(var u in snapshot035_2)if(u.hp>0&&u.auraDamageTick>0)DamageUnit(u,u.auraDamageTick*.5f,1-u.team);
    }
    bool HasSpell(Unit u,string id)=>u.abilities.Contains(id)&&u.mana+.0001f>=SimulationConfig035.Get(Abilities[id]).mana&&(!u.spellTimers.TryGetValue(id,out float t)||t<=0);
    float SpellCooldown(Unit u,string id)=>F(Abilities[id],"cd")*(TeamPandaren[u.team]&&Aspects[u.team]==1&&(string)Abilities[id]?["kind"]=="panda_brew"?.8f:1)/(TeamOrc[u.team]&&Aspects[u.team]==1&&id=="A02M"?1.35f:1);
    bool TryAutocast(Unit u) {
        if(Silenced(u))return false;
        using(var snapshot035_3=SimulationBuffers035.Take(AbilityView035.Get(u,Abilities).active))foreach(var id in snapshot035_3) {
            var cfg=Abilities[id];var typed=SimulationConfig035.Get(cfg);string kind=typed.kind;if((!typed.autocast&&!FinalActive.Contains(kind)&&!NerubNets.Contains(id))||typed.followup||!HasSpell(u,id))continue;
            Unit target=null;
            if(NerubNets.Contains(id))target=CastNerubNet(u,cfg,true)?u:null;
            else if(FinalActive.Contains(kind))target=(kind.StartsWith("nerub_",StringComparison.Ordinal)?CastNerub(u.team,u.position,cfg,u,null,null,true):CastPanda(u.team,u.position,cfg,u,null,null,true))?u:null;
            else if(id=="A01G")target=RadiusUnits035(u.position,F(cfg,"range")).Where(v=>Valid(v,u.team,cfg,false)&&Vector2.Distance(u.position,v.position)<=F(cfg,"range")).OrderBy(v=>(v.position-u.position).sqrMagnitude).FirstOrDefault();
            else if(kind=="elven_heal"||kind=="elven_solar")target=ElvenTargets(u.team,u.position,cfg).OrderBy(v=>kind=="elven_heal"?v.hp/v.maxHp:0).FirstOrDefault();
            else if(kind=="debuff"&&(string)cfg["code"]=="Afae")target=RadiusUnits035(u.position,F(cfg,"range")).Where(v=>Valid(v,u.team,cfg,false)&&!v.effects.Any(e=>e.id==id&&e.left>0)&&Vector2.Distance(u.position,v.position)<=F(cfg,"range")).OrderBy(v=>(v.position-u.position).sqrMagnitude).FirstOrDefault();
            else if(kind=="night_roots")target=NightTargets(u.team,u.position,cfg).FirstOrDefault();
            else if(kind=="elemental_barrage")target=ElementalTargets(u.team,u.position,cfg).FirstOrDefault();
            else if(kind=="corrupted_impale")target=CorruptedTargets(u.team,u.position,cfg).FirstOrDefault();
            else if(kind=="undead_raise_unit")target=UndeadReady(u,cfg)?u:null;
            else if(kind=="debuff"&&id=="A01M")target=RadiusUnits035(u.position,F(cfg,"range")).Where(v=>Valid(v,u.team,cfg,false)&&!v.effects.Any(e=>e.id==id)&&Vector2.Distance(v.position,u.position)<=F(cfg,"range")).OrderBy(v=>(v.position-u.position).sqrMagnitude).FirstOrDefault();
            else if(kind=="mech_mines"||kind=="mech_grenade")target=MechUnitReady(u,cfg)?u:null;
            else if(kind!=null&&kind.StartsWith("north_",StringComparison.Ordinal))target=NorthernUnitReady(u,cfg)?u:null;
            else if(kind=="twins_blast")target=ChaosBlastReady(u)?u:null;
            else if(kind=="chaos_roar")target=ChaosRoarReady(u)?u:null;
            else if(kind=="resurrect")target=Corpses.Any(c=>c.unit.team==u.team&&Vector2.Distance(c.unit.position,u.position)<=F(cfg,"area"))?u:null;
            else if(kind=="nature_trees")target=u;
            else if(kind=="taunt")target=RadiusUnits035(u.position,F(cfg,"range")).FirstOrDefault(v=>Valid(v,u.team,cfg,false)&&Vector2.Distance(v.position,u.position)<=F(cfg,"range"));
            else if(kind=="regen")target=RadiusUnits035(u.position,F(cfg,"range")).Where(v=>Valid(v,u.team,cfg,true)&&v.hp<v.maxHp&&!v.effects.Any(e=>e.id==((string)cfg["effect_id"]??id))&&Vector2.Distance(u.position,v.position)<=F(cfg,"range")).OrderBy(v=>v.hp/v.maxHp).FirstOrDefault();
            else if(kind=="debuff"&&(id=="A0AR"||id=="A0AS"||id=="A0AV"))target=RadiusUnits035(u.position,F(cfg,"range")).Where(v=>NatureDebuffTarget(u.team,v,id,cfg)&&Vector2.Distance(u.position,v.position)<=F(cfg,"range")).OrderBy(v=>(v.position-u.position).sqrMagnitude).FirstOrDefault();
            else if(kind=="heal")target=RadiusUnits035(u.position,F(cfg,"range")).Where(v=>Valid(v,u.team,cfg,true)&&v.hp<v.maxHp&&Vector2.Distance(u.position,v.position)<=F(cfg,"range")).OrderBy(v=>v.hp/v.maxHp).FirstOrDefault();
            else if(kind=="warlock_blast"&&u.warlockPhase<0)target=RadiusUnits035(u.position,F(cfg,"range")).Where(v=>Valid(v,u.team,cfg,false)&&!Flying(v)&&Vector2.Distance(u.position,v.position)<=F(cfg,"range")).OrderBy(v=>(v.position-u.position).sqrMagnitude).FirstOrDefault();
            else if(kind=="human_prayer")target=RadiusUnits035(u.position,F(cfg,"range")).FirstOrDefault(v=>Valid(v,u.team,Abilities["A0I1"],true)&&v.hp<v.maxHp&&Vector2.Distance(u.position,v.position)<=F(cfg,"range"));
            else if(kind=="orc_nets")target=RadiusUnits035(u.position,F(cfg,"range")).Where(v=>OrcNetTarget(u.team,v)&&Vector2.Distance(u.position,v.position)<=F(cfg,"range")).OrderBy(v=>(v.position-u.position).sqrMagnitude).FirstOrDefault();
            else if(kind=="buff"&&(bool?)cfg["followup_only"]!=true)target=RadiusUnits035(u.position,F(cfg,"range")).Where(v=>Valid(v,u.team,cfg,true)&&!v.effects.Any(e=>e.id==id)&&Vector2.Distance(u.position,v.position)<=F(cfg,"range")).OrderBy(v=>v.hp/v.maxHp).FirstOrDefault();
            else if(kind=="orc_spell_steal")target=OrcStealChoice(u,cfg)?.donor;
            else if(kind=="naga_purge")target=RadiusUnits035(u.position,F(cfg,"range")).Where(v=>NagaCandidate(v,u.team)&&Valid(v,u.team,cfg,false)&&!IsNagaHero(v)&&Vector2.Distance(u.position,v.position)<=F(cfg,"range")).OrderBy(v=>(v.position-u.position).sqrMagnitude).FirstOrDefault();
            if(target==null)continue;
            u.pendingSpell=id;u.spellTarget=target.id;u.castLeft=UnitConfig035(u.kind).castPoint;u.finalCastPose=(string)cfg["animation"];u.pose=u.finalCastPose??"Spell";u.attackSerial++;
            if(u.castLeft<=0){u.castLeft=-1;u.pendingSpell=null;u.spellTimers[id]=Cast(u,id,target.id)?SpellCooldown(u,id):.25f;}return true;
        }
        return false;
    }
    public bool Cast(Unit source,string id,int targetId=-1) {
        if(source==null||source.hp<=0||source.stun>0||Disabled(source)||Silenced(source)||!HasSpell(source,id))return false;var cfg=Abilities[id];if(cfg==null)return false;string kind=(string)cfg["kind"];var target=FindUnit(targetId);
        if(id=="BH09"||id=="BH10"){if(!ResolveBlackHandTithe(source,target,id=="BH10"))return false;}
        else if(NerubNets.Contains(id)){if(!CastNerubNet(source,cfg))return false;}
        else if(FinalActive.Contains(kind)){if(!(kind.StartsWith("nerub_",StringComparison.Ordinal)?CastNerub(source.team,source.position,cfg,source,null,target):CastPanda(source.team,source.position,cfg,source,null,target)))return false;}
        else if(id=="A01G"){if(!CastChaosCurse(source,target))return false;}
        else if(kind=="elven_heal"||kind=="elven_solar"){if(!CastElvenUnit(source,cfg,target))return false;}
        else if(kind=="debuff"&&(string)cfg["code"]=="Afae"){if(!Valid(target,source.team,cfg,false)||Vector2.Distance(source.position,target.position)>F(cfg,"range"))return false;if(!ElvenSpellShield(target))ApplyEffect(target,id,source.team);}
        else if(kind=="night_roots"){if(!CastNightRoots(source,cfg,target))return false;}
        else if(kind=="elemental_barrage"){if(!CastElementalUnit(source,cfg,target))return false;}
        else if(kind=="corrupted_impale"){if(!CastCorruptedUnit(source,cfg,target))return false;}
        else if(kind=="undead_raise_unit"){if(!CastUndeadUnit(source,cfg))return false;}
        else if(kind=="debuff"&&id=="A01M"){if(!Valid(target,source.team,cfg,false)||Vector2.Distance(target.position,source.position)>F(cfg,"range")||target.effects.Any(e=>e.id==id))return false;ApplyEffect(target,id,source.team);}
        else if(kind=="mech_mines"||kind=="mech_grenade"){if(!CastMechUnit(source,cfg,target))return false;}
        else if(id=="A09F"){if(!CastMechAutoFire(source,cfg,target))return false;BattleAudio.For(this)?.Spell(id,source.position);source.mana-=F(cfg,"mana");source.spellTimers[id]=F(cfg,"cd");SpellsCast++;return true;}
        else if(kind!=null&&kind.StartsWith("north_",StringComparison.Ordinal)){if(!CastNorthernUnit(source,cfg,target))return false;}
        else if(kind=="twins_blast"){if(!CastChaosBlast(source,target))return false;}
        else if(kind=="chaos_roar"){if(!CastChaosRoar(source))return false;}
        else if(kind=="nature_trees"){if(!CastNatureTrees(source))return false;}
        else if(kind=="summon"&&id=="A0BU"){if(!CastNatureEnts(source))return false;}
        else if(kind=="taunt"){if(!CastNatureTaunt(source,id))return false;}
        else if(kind=="regen") {
            if(!Valid(target,source.team,cfg,true)||Vector2.Distance(source.position,target.position)>F(cfg,"range")||target.hp>=target.maxHp)return false;
            string effect=(string)cfg["effect_id"]??id;if(target.effects.Any(e=>e.id==effect))return false;ApplyEffect(target,effect,source.team);PulseAt(target.position,1.5f,Color.green,"Омоложение",1f,"heal");
        } else if(kind=="debuff"&&(id=="A0AR"||id=="A0AS"||id=="A0AV")) {if(!CastNatureDebuff(source,target,id,cfg))return false;
        } else if(kind=="naga_purge") {if(!CastNagaPurge(source,cfg,target))return false;
        } else if(kind=="orc_nets") {if(!CastOrcNets(source,cfg,target))return false;
        } else if(kind=="orc_spell_steal") {if(!CastOrcSteal(source,cfg))return false;PulseAt(source.position,1,Color.white,"",1.23f,"steal_caster");
        } else if(kind=="heal") {
            if(!Valid(target,source.team,cfg,true)||Vector2.Distance(target.position,source.position)>F(cfg,"range")||target.hp>=target.maxHp)return false;
            Heal(target,F(cfg,"power"),id=="A03K"?"heal_direct":null);NatureAfterHeal(source,target,id);if(id=="A03K")QueueHumanJob035(new HumanJob {kind="inner",source=source.id,target=target.id,left=0});
            if(Lines[source.kind].unit=="h03C"){Grant(target,"A03L");QueueHumanJob035(new HumanJob {kind="paladin_raise",source=source.id,left=1});}
        } else if(kind=="buff") {
            if(!Valid(target,source.team,cfg,true)||Vector2.Distance(target.position,source.position)>F(cfg,"range")||target.effects.Any(e=>e.id==id))return false;
            ApplyEffect(target,id);PulseAt(target.position,1.5f,new Color(1,.8f,.4f),(string)cfg["name"]??"Усиление");
            if(id=="A02M"){source.spellTimers[id]=SpellCooldown(source,id);OrcInnerFires++;if(TeamOrc[source.team]&&Aspects[source.team]==1){var echo=Units.Where(v=>v!=target&&Valid(v,source.team,cfg,true)&&!v.effects.Any(e=>e.id==id)&&Vector2.Distance(source.position,v.position)<=F(cfg,"range")).OrderBy(v=>v.hp/v.maxHp).FirstOrDefault();if(echo!=null)ApplyEffect(echo,id);}}
        } else if(kind=="resurrect") {
            if(Raise(source.team,source.position,F(cfg,"area"),(int?)cfg["count"]??1,((string)cfg["targets"]??"").Contains("organic"))==0)return false;PulseAt(source.position,2,Color.yellow,"",4.567f,"resurrect_caster");
        } else if(kind=="warlock_blast") {
            if(source.warlockPhase>=0||!Valid(target,source.team,cfg,false)||Flying(target)||Vector2.Distance(source.position,target.position)>F(cfg,"range"))return false;
            var center=target.position;using(var snapshot035_4=SimulationBuffers035.Take(Units))foreach(var u in snapshot035_4)if(Valid(u,source.team,cfg,false)&&!Flying(u)&&Vector2.Distance(u.position,center)<=F(cfg,"area"))DamageUnit(u,F(cfg,"power"),source.team);
            source.warlockPhase=0;source.warlockLeft=.3f;source.strikeLeft=-1;source.combatTarget=-1;source.auraMana=0;source.auraIds.Remove("A00F");WarlockBlasts++;PulseAt(center,F(cfg,"area"),new Color(.7f,.25f,1),"Магический взрыв",1.267f,"wisp");
        } else if(kind=="human_prayer") {
            if(!Units.Any(v=>Valid(v,source.team,Abilities["A0I1"],true)&&v.hp<v.maxHp&&Vector2.Distance(source.position,v.position)<=F(cfg,"range")))return false;
            foreach(var v in Units.Where(v=>Valid(v,source.team,Abilities["A0I1"],true)&&Vector2.Distance(source.position,v.position)<=15).ToArray()){Heal(v,50);v.mana=Mathf.Min(v.maxMana,v.mana+50);}
            QueueHumanJob035(new HumanJob {kind="prayer_buff",team=source.team,center=source.position,left=.1f,count=1+(int)(Roll()*3)});Prayers++;PulseAt(source.position,15,new Color(1,.9f,.5f),"Молитва");
        } else return false;
        BattleAudio.For(this)?.Spell(id,source.position);source.mana-=F(cfg,"mana");source.castPause=Mathf.Max(source.castPause,id=="A0CG"?1.2f:kind=="warlock_blast"?.35f:F(cfg,"cast_pause",.2f));source.finalCastPose=(string)cfg["animation"];source.pose=source.finalCastPose??"Spell";SpellsCast++;return true;
    }
    public void Heal(Unit u,float amount)=>Heal(u,amount,null);
    void Heal(Unit u,float amount,string fx){if(u==null||u.hp<=0)return;if(u.effects.Any(e=>e.id=="BHJu"&&e.left>0))amount*=.5f;float healed=Mathf.Min(amount,u.maxHp-u.hp);if(healed<=0)return;u.hp+=healed;Heals++;PulseAt(u.position,1,Color.green,"+"+Mathf.RoundToInt(healed),fx==null?.8f:1.833f,fx,fx==null?-1:u.id);}
    public int Raise(int team,Vector2 center,float radius,int count,bool organic) {
        var bodies=Corpses.Where(c=>Lines[c.unit.kind].unit!="nN04"&&c.unit.team==team&&Vector2.Distance(c.unit.position,center)<=radius&&(!organic||Organic(c.unit))).Take(count).ToArray();
        foreach(var c in bodies){var old=c.unit;var fresh=Spawn(team,old.kind,old.position);fresh.onRoad=true;foreach(var id in old.abilities)Grant(fresh,id);fresh.armor=old.armor;fresh.maxHp=old.maxHp;fresh.hp=fresh.maxHp;Corpses.Remove(c);old.deadAge=100;Resurrections++;PulseAt(fresh.position,2,Color.yellow,"Воскрешение",2.367f,"resurrect",fresh.id);}return bodies.Length;
    }
    void TickHumanJobs(float dt) {
        var scheduled035=SimulationState035.Get(this).reference?null:HumanQueue035().Advance(dt);
        using(var humanSnapshot035=SimulationBuffers035.Take(scheduled035??humanJobs))foreach(var job in humanSnapshot035) {
            if(scheduled035==null){job.left-=dt;if(job.left>.00001f)continue;}humanJobs.Remove(job);
            if(job.kind=="prayer_buff") {
                foreach(var u in Units)if(Valid(u,job.team,Abilities["A0I2"],true)&&Vector2.Distance(u.position,job.center)<=15)ApplyEffect(u,"A0I2");
                QueueHumanJob035(new HumanJob {kind="prayer_raise",team=job.team,center=job.center,left=.1f,count=job.count});
            } else if(job.kind=="prayer_raise") {
                if(Corpses.Any(c=>c.unit.team==job.team&&Organic(c.unit)&&Vector2.Distance(c.unit.position,job.center)<=15))Raise(job.team,job.center,F(Abilities["A0I4"],"area"),job.count,true);
            } else {
                var source=FindUnit(job.source);if(source==null||source.stun>0)continue;
                string id=job.kind=="paladin_raise"?"A03H":Lines[source.kind].unit=="h03C"?"A03I":"A03M";
                if(Cast(source,id,job.target))source.spellTimers[id]=F(Abilities[id],"cd");
            }
        }
        using(var snapshot035_6=SimulationBuffers035.Take(Fires))foreach(var f in snapshot035_6) {
            f.age+=dt;var c=Abilities[f.ability];float duration=F(c?["fields"],"adur",1.01f),full=F(c?["fields"],"ahdu",1.01f);
            while(f.nextFull<=full+.00001f&&f.age+.00001f>=f.nextFull){FirePulse(f,F(c,"power"));f.nextFull+=F(c,"interval",.25f);}
            while(f.nextTail<=duration+.00001f&&f.age+.00001f>=f.nextTail){FirePulse(f,F(c?["fields"],"Hfs3",3));f.nextTail+=F(c?["fields"],"Hfs4",1);}
            if(f.age>=duration)Fires.Remove(f);
        }
    }
    bool TickWarlock(Unit u,float dt) {
        if(u.warlockPhase<0)return false;float left=dt;
        while(left>=0) {
            float step=Mathf.Min(left,u.warlockLeft);
            if(u.warlockPhase==1){var delta=CastlePosition(u.team)-u.position;u.strikeLeft=-1;u.pendingSpell=null;u.castLeft=-1;
                if(delta.magnitude>6.2f&&u.stun<=0){var goal=BuilderWaypoint(u.position,CastlePosition(u.team));u.direction=(goal-u.position).normalized;u.position=Vector2.MoveTowards(u.position,goal,MoveSpeed(u)*step);u.pose="Walk";}else u.pose="Stand";
            } else if(u.warlockPhase==2) {
                if(u.stun>0){u.strikeLeft=-1;u.pose="Stand";}
                else if(u.strikeLeft>=0){u.strikeLeft-=step;if(u.strikeLeft<=0){Release(u);u.strikeLeft=-1;}}
                else {var v=Units.Where(v=>v.team!=u.team&&v.hp>0&&WeaponFor(u,v)!=null&&Vector2.Distance(u.position,v.position)<=WeaponFor(u,v).range+1).OrderBy(v=>(v.position-u.position).sqrMagnitude).FirstOrDefault();if(v!=null&&u.cooldown<=0){u.direction=(v.position-u.position).normalized;BeginAttack(u,WeaponFor(u,v),v.id);}}
                u.pose=u.strikeLeft>=0?"Attack":"Stand";
            } else u.pose=u.stun>0?"Stand":"Spell";
            u.warlockLeft=Mathf.Max(0,u.warlockLeft-step);left-=step;
            if(u.warlockLeft>.00001f)break;
            if(u.warlockPhase==0){u.warlockPhase=1;u.warlockLeft=5;}
            else if(u.warlockPhase==1){u.warlockPhase=2;u.warlockLeft=6.6f;}
            else {u.warlockPhase=-1;u.pose="Stand";return true;}
            if(left<=.00001f)break;
        }
        return true;
    }
    void Chain(Unit source,Unit target,JToken cfg) {
        if(cfg==null)return;var used=new HashSet<int>();Unit current=target;Vector2 from=source.position;float amount=F(cfg,"power");
        for(int i=0;i<((int?)cfg["count"]??1)&&current!=null;i++) {
            used.Add(current.id);Shots.Add(new Shot {id=nextId++,team=source.team,kind=4,start=from,end=current.position,duration=new[]{"h07B","h00W"}.Contains(Lines[source.kind].unit)?2:.3f,weapon=new[]{"h07B","h00W"}.Contains(Lines[source.kind].unit)?new Weapon{missile="elven_beam:"+(i==0?"CLPB":"CLSB")}:null,hit=true,startHeight=Height(source)+1.5f,endHeight=Height(current)+1});DamageUnit(current,amount,source.team);Chains++;from=current.position;amount*=1-F(cfg,"falloff");
            current=Units.Where(v=>!used.Contains(v.id)&&Valid(v,source.team,cfg,false)&&Vector2.Distance(v.position,from)<=F(cfg,"area")).OrderBy(v=>(v.position-from).sqrMagnitude).FirstOrDefault();
        }
    }
    public void TickBuilding(Hut h,float dt) {
        var l=Lines[h.kind];if(l.abilities.Length>0||l.buildingWeapons.Length>0)InvalidateArmyCounts035();h.attackPoseLeft=Mathf.Max(0,h.attackPoseLeft-dt);if(h.spellTimers.Count>0)using(var snapshot035_7=SimulationBuffers035.Take(h.spellTimers.Keys))foreach(var id in snapshot035_7)h.spellTimers[id]=Mathf.Max(0,h.spellTimers[id]-dt);if(BuildingDisabled(h)>0){h.cooldown=Mathf.Max(0,h.cooldown-dt);return;}TickFinalBuilding(h);TickPepeBuilding(h);BlackHandBuilding(h);if(TeamElven[h.team]||TeamUltimate[h.team])TickElvenBuilding(h);if(TeamNight[h.team]||TeamUltimate[h.team])TickNightBuilding(h);if(TeamElemental[h.team]||TeamUltimate[h.team])TickElementalBuilding(h);if(TeamCorrupted[h.team]||TeamUltimate[h.team])TickCorruptedBuilding(h);if(TeamUndead[h.team]||TeamUltimate[h.team])TickUndeadBuilding(h);if(TeamMech[h.team]||TeamUltimate[h.team])TickMechBuilding(h);if(TeamNorthern[h.team]||TeamUltimate[h.team])TickNorthernBuilding(h);if(TeamChaos[h.team]||TeamUltimate[h.team])TickChaosBuilding(h);if(TeamOrc[h.team]||TeamUltimate[h.team])TickOrcBuilding(h);if(TeamNature[h.team]||TeamUltimate[h.team])TickNatureBuilding(h);if(TeamNaga[h.team]&&Aspects[h.team]==1&&l.building=="h00G")TickNagaSea(h,dt);if(l.building=="h00G"||l.building=="h00I"||l.building=="h00M"||l.building=="h00N")TickNagaBuilding(h,dt);if(l.building=="h00N")return;
        if(l.building=="h010") {
            var cfg=Abilities["A01K"];if(h.mana>=F(cfg,"mana")&&(!h.spellTimers.TryGetValue("A01K",out float wait)||wait<=0)) {
                var targets=Units.Where(u=>Valid(u,h.team,Abilities["A016"],true)&&((string)Lines[u.kind].stats["unit_types"]??"").ToLowerInvariant().Contains("sapper")&&Vector2.Distance(u.position,h.position)<=F(cfg,"range")).ToArray();
                if(targets.Length>0){foreach(var u in targets)ApplyEffect(u,"A016");h.mana-=F(cfg,"mana");h.spellTimers["A01K"]=BuildingCooldown(h,F(cfg,"cd"));HolyCasts++;SpellsCast++;PulseAt(h.position,F(cfg,"range"),Color.yellow,"Святая сила");}
            }
        }
        h.cooldown=Mathf.Max(0,h.cooldown-dt);
        if(l.building=="h001") {
            h.aimLeft-=dt;if(h.aimLeft<=0){h.aim=new Vector2((1984+Roll()*(6272-1984))/30*(h.team==0?1:-1),(-2048+Roll()*4096)/30);h.aimLeft=9;}
            if(h.cooldown<=0){var w=l.buildingWeapons[0];h.cooldown=w.cooldown/(TeamHuman[h.team]&&Aspects[h.team]==0?1.2f:1);h.attackSerial++;h.attackPoseLeft=.6f;AddShot(h.team,h.kind,h.id,-1,h.position,h.aim,w.damage,w,0,2,true);}return;
        }
        if(l.buildingWeapons.Length==0)return;
        if(h.strikeLeft>=0){h.strikeLeft-=dt;if(h.strikeLeft<=0){var victim=FindUnit(h.attackTarget);if(victim!=null){var w=h.pendingWeapon;float bonus=0,stun=0;foreach(var id in l.abilities){var c=Abilities[id];var p=c?["profile"];if((string)c?["kind"]=="bash"&&(Flying(victim)?((string)p?["targets"]??"").Contains("air"):((string)p?["targets"]??"").Contains("ground"))&&Roll()<F(p,"chance")){bonus+=F(p,"bonus");stun=Mathf.Max(stun,F(p,Lines[victim.kind].armorType=="hero"?"hero_duration":"duration"));}}var towerShot=AddShot(h.team,h.kind,h.id,victim.id,h.position,victim.position,(w.damage+bonus)*MechBuildingDamage(h)*ElvenWeaponFactor(h.team,false),w,stun,3);if(l.building=="bH18")towerShot.chaosSpell=h.attackSerial%3==0?"hand-raven":"hand-raven-arrow";ElvenTowerBarrage(h,w,victim.id);NerubTowerBarrage(h,w,victim.id);}h.strikeLeft=-1;}return;}
        if(h.cooldown>0)return;
        Unit target=null;bool towerIndexed=false;
        if(UnitIndexReady033()) {
            float brMax=0;for(int wi=0;wi<l.buildingWeapons.Length;wi++)brMax=Mathf.Max(brMax,l.buildingWeapons[wi].range);
            float acquisition=Mathf.Min(brMax,l.buildingAcquisitionRange);float brSqr=acquisition*acquisition;
            if(TryFindBestAny033(h.position,brSqr,h.team,false,v=>!CorruptedAirborne(v)&&!Invulnerable(v)&&!Hidden(v)&&NightCanDetect(h.team,v)&&l.buildingWeapons.Any(w=>!HordeImmuneToWeapon(v,w)&&(Flying(v)?w.air:w.ground)&&(v.position-h.position).sqrMagnitude<=w.range*w.range),out target))towerIndexed=true;
        }
        if(!towerIndexed)target=Units.Where(u=>u.team!=h.team&&u.hp>0&&!CorruptedAirborne(u)&&!Invulnerable(u)&&!Hidden(u)&&NightCanDetect(h.team,u)&&l.buildingWeapons.Any(w=>!HordeImmuneToWeapon(u,w)&&(Flying(u)?w.air:w.ground)&&(u.position-h.position).sqrMagnitude<=Mathf.Min(w.range,l.buildingAcquisitionRange)*Mathf.Min(w.range,l.buildingAcquisitionRange))).OrderBy(u=>(u.position-h.position).sqrMagnitude).FirstOrDefault();
        if(target==null)return;h.pendingWeapon=l.buildingWeapons.First(w=>!HordeImmuneToWeapon(target,w)&&(Flying(target)?w.air:w.ground)&&Vector2.Distance(target.position,h.position)<=w.range);h.cooldown=h.pendingWeapon.cooldown;h.strikeLeft=h.pendingWeapon.point;h.attackTarget=target.id;h.attackSerial++;h.attackPoseLeft=h.pendingWeapon.point+h.pendingWeapon.backswing+.2f;
    }
    void AddFire(int team,Vector2 center,string id="A02K",int sourceId=0){var c=Abilities[id];Fires.Add(new Fire {id=nextId++,team=team,sourceId=sourceId,ability=id,center=center,nextFull=F(c,"interval",.25f),nextTail=F(c?["fields"],"ahdu",1.01f)+F(c?["fields"],"Hfs4",1)});ArtilleryFires++;PulseAt(center,F(c,"area",5),new Color(1,.3f,.02f),"Горючая смесь",F(c?["fields"],"adur",1.01f));}
    void FirePulse(Fire fire,float amount) {
        var cfg=Abilities[fire.ability];float radius=F(cfg,"area",5);
        using(var snapshot035_8=SimulationBuffers035.Take(Units))foreach(var u in snapshot035_8)if(Valid(u,fire.team,cfg,false)&&Vector2.Distance(u.position,fire.center)<=radius)DamageUnit(u,amount,fire.team);
        using(var snapshot035_9=SimulationBuffers035.Take(Huts))foreach(var h in snapshot035_9)if(h.team!=fire.team&&h.paid&&h.hp>0&&Vector2.Distance(h.position,fire.center)<=radius)DamageHut(h,amount,"magic",fire.team,true,sourceId:fire.sourceId);
        if(Vector2.Distance(BattleCastlePosition(1-fire.team),fire.center)<=radius)DamageCastle(1-fire.team,amount,"magic",true,fire.sourceId);
    }
    public float BuildingArmor(Hut h)=>F(Lines[h.kind].buildingStats,"armor")-h.dragonArmorRank*2+MechBuildingArmor(h)+(TeamHuman[h.team]&&Aspects[h.team]==0&&Lines[h.kind].building=="h006"?2:0);
    void TickAspects(float dt) {
        aspectAge+=dt;if(aspectAge<.5)return;float step=(float)Math.Min(1,aspectAge);aspectAge=0;
        for(int team=0;team<2;team++)if(TeamHuman[team]&&Aspects[team]==1) {
            reinforcement[team]-=step;if(reinforcement[team]>0)continue;reinforcement[team]=30;
            var producers=Huts.Where(h=>h.team==team&&h.paid&&!h.paused&&h.hp>0&&Lines[h.kind].Producer&&Lines[h.kind].slots==0).ToArray();
            if(producers.Length>0&&ArmyCount(team)<ArmyLimit){var h=producers[Mathf.Min(producers.Length-1,(int)(Roll()*producers.Length))];Spawn(team,h.kind,h.position,false);AspectExtras++;PulseAt(h.position,3,Color.yellow,"Подкрепление");}
        }
        TickNerubQueen(step);TickNatureForest(step);TickChaosAspects(step);TickCorruptedRot(step);
    }
    void TickBotRescue(float dt) {
        botRescueAge-=dt;if(botRescueAge>0)return;botRescueAge=1;
        BotDefend();
    }
    public bool BuyRescue(int team){if(!ChallengeShopAllowed(team)||!ChallengeRescueAllowed)return false;if(Finished||team<0||team>1||AvailableGold(team)<1000)return false;Gold[team]-=1000;RescueCharges[team]++;return true;}
    public bool Rescue(int team,Vector2 p){if(!ChallengeRescueAllowed)return false;if(Finished||team<0||team>1||RescueCharges[team]<=0||RescueCooldown[team]>0||float.IsNaN(p.x)||float.IsNaN(p.y)||Mathf.Abs(p.x)>6144f/30||Mathf.Abs(p.y)>2048f/30)return false;RescueCharges[team]--;RescueCooldown[team]=60;if(team==0&&QuackDownpour){BeginQuackRescueBackfire(p);return true;}using(var snapshot035_10=SimulationBuffers035.Take(Units))foreach(var u in snapshot035_10)if(u.hp>0&&u.team!=team&&!RescueImmune(u)&&Vector2.Distance(p,u.position)<=1000f/30)DamageUnit(u,u.maxHp+1,team,"chaos",true,true);PulseAt(p,1000f/30,Color.cyan,"Спасительный удар",1.267f,"wisp");return true;}
}
}
