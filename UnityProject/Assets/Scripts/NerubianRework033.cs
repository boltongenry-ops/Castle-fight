using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    // Runtime-only state: no change to serialized player/prefab layouts.
    Effect nerubTickingEffect;
    sealed class NerubPlagueBudget {public int used;public float until;}
    readonly Dictionary<int,NerubPlagueBudget> nerubPlagueBudgets=new Dictionary<int,NerubPlagueBudget>();
    public const int NerubBroodLimit=20;
    bool NerubVenom(JToken c)=>(string)c?["stack_group"]=="nerub_venom"&&(string)c?["kind"]=="poison";
    int NerubVenomSource(Effect e)=>(int?)e.config?["nerub_source"]??0;
    Effect[] NerubVenoms(Unit u)=>u.effects.Where(e=>e.left>0&&NerubVenom(e.config)).OrderByDescending(e=>F(e.config,"dps")).ThenBy(e=>e.sourceTeam).ThenBy(NerubVenomSource).ToArray();
    bool ApplyNerubVenom(Unit u,JObject config,int side){
        if(!NerubVenom(config))return false;
        if(!Valid(u,side,config,false))return true;
        int source=(int?)config["nerub_source"]??0;
        var live=NerubVenoms(u);var previous=live.FirstOrDefault(e=>e.sourceTeam==side&&NerubVenomSource(e)==source);
        if(previous!=null&&F(previous.config,"dps")>F(config,"dps"))return true;
        if(previous==null&&live.Length>=2){var weakest=live.Last();if(F(weakest.config,"dps")>=F(config,"dps"))return true;u.effects.Remove(weakest);}
        float duration=NorthernStatusDuration(u,config,side,F(config,"duration"));
        bool shortControl=Lines[u.kind].armorType=="hero"||u.abilities.Any(a=>(string)Abilities[a]?["kind"]=="resistant_skin");
        if(shortControl)duration=NorthernStatusDuration(u,config,side,F(config,"hero_duration",F(config,"duration")));
        u.effects.RemoveAll(e=>NerubVenom(e.config)&&(e.left<=0||e.sourceTeam==side&&NerubVenomSource(e)==source));
        u.effects.Add(new Effect{id=(string)config["id"],group="nerub_venom",left=duration,lifetime=duration,dotTick=previous?.dotTick??0,sourceTeam=side,config=(JObject)config.DeepClone(),strength=F(config,"dps")});
        RefreshNerubVenomFields(u);return true;
    }
    void RefreshNerubVenomFields(Unit u){
        var effects=NerubVenoms(u);foreach(var e in effects){e.armor=0;e.speed=0;}
        if(effects.Length==0)return;
        effects[0].armor=effects.Min(e=>F(e.config,"armor"));
        effects[0].speed=-effects.Max(e=>F(e.config,"slow"));
    }
    float NerubArmorOverlap(Unit u){
        var negative=u.effects.Where(e=>e.left>0&&(NerubVenom(e.config)||e.id=="AUAc")&&e.armor<0).Select(e=>e.armor).ToArray();
        return negative.Length<2?0:negative.Min()-negative.Sum();
    }
    float NerubPeriodicDamage(Unit u,Effect e){
        if(!NerubVenom(e.config))return F(e.config,"dps");
        // Include the current final tick; it still owns its slot while damage is resolved.
        var layers=u.effects.Where(v=>NerubVenom(v.config)).OrderByDescending(v=>F(v.config,"dps")).ThenBy(v=>v.sourceTeam).ThenBy(NerubVenomSource).ToArray();
        int rank=Array.IndexOf(layers,e);return F(e.config,"dps")*(rank==0?1:rank==1?.5f:0);
    }
    void NerubPeriodicHit(Unit u,Effect e,float seconds){
        var previous=nerubTickingEffect;nerubTickingEffect=e;
        try{var c=e.config;DamageUnit(u,NerubPeriodicDamage(u,e)*seconds,e.sourceTeam,(string)c["damage_type"]??"magic",true,false,null,false,(bool?)c["ignore_armor"]==true,F(c,"minimum_hp"));}
        finally{nerubTickingEffect=previous;}
    }
    float NerubHuntMultiplier(Unit source,Unit target){
        if(source==null||!Nerub(source)||!FinalFighter(target)||source.team==target.team)return 1;
        var mark=target.effects.Where(e=>e.id=="ANMK"&&e.sourceTeam==source.team&&e.left>0).OrderByDescending(e=>F(e.config,"hunt_bonus")).FirstOrDefault();
        if(mark==null)return 1;float value=F(mark.config,"hunt_bonus")/100f;
        if(Lines[source.kind].unit=="nN04")value*=.25f;
        NerubHunts++;return 1+value;
    }
    Unit NerubHuntTarget(Unit source,Unit current){
        if(!Nerub(source)||!source.abilities.Any(id=>(string)Abilities[id]?["kind"]=="nerub_ambush"))return current;
        float nearest=current==null?6:Mathf.Min(6,Vector2.Distance(source.position,current.position)+1);
        return Units.Where(v=>v.team!=source.team&&v.hp>0&&v.onRoad&&WeaponFor(source,v)!=null&&Vector2.Distance(source.position,v.position)<=nearest&&v.effects.Any(e=>e.id=="ANMK"&&e.sourceTeam==source.team&&e.left>0)).OrderBy(v=>(v.position-source.position).sqrMagnitude).FirstOrDefault()??current;
    }
    void NerubTowerBarrage(Hut tower,Weapon weapon,int primary){
        if(Finished||NetworkReplica||tower.hp<=0||!tower.paid||tower.paused||BuildingDisabled(tower)>0||!new[]{"hNT1","hNT2"}.Contains(Lines[tower.kind].building))return;
        Unit target=null;bool barrageIndexed=false;
        if(UnitIndexReady033()) {
            float wrSqr=weapon.range*weapon.range;
            if(TryFindBestAny033(tower.position,wrSqr,tower.team,false,v=>v.id!=primary&&Lines[v.kind].unit!="YTct"&&!Hidden(v)&&!Invulnerable(v)&&!CorruptedAirborne(v)&&NightCanDetect(tower.team,v)&&(Flying(v)?weapon.air:weapon.ground)&&(!Ethereal(v)||weapon.attack=="magic"||weapon.attack=="spells"),out target))barrageIndexed=true;
        }
        if(!barrageIndexed)target=Units.Where(u=>u.id!=primary&&u.team!=tower.team&&u.hp>0&&Lines[u.kind].unit!="YTct"&&!Hidden(u)&&!Invulnerable(u)&&!CorruptedAirborne(u)&&NightCanDetect(tower.team,u)&&(Flying(u)?weapon.air:weapon.ground)&&(!Ethereal(u)||weapon.attack=="magic"||weapon.attack=="spells")&&Vector2.Distance(u.position,tower.position)<=weapon.range).OrderBy(u=>(u.position-tower.position).sqrMagnitude).FirstOrDefault();
        if(target!=null)AddShot(tower.team,tower.kind,tower.id,target.id,tower.position,target.position,weapon.damage*.6f*MechBuildingDamage(tower)*ElvenWeaponFactor(tower.team,false),weapon,0,3);
    }
    void NerubPlagueDeath(Unit dead,Effect effect){
        if(ElementalIllusion(dead)||dead.summoned||effect.sourceTeam==dead.team||effect.left<=0&&effect!=nerubTickingEffect)return;
        int cast=(int?)effect.config["nerub_plague_cast"]??0;NerubPlagueBudget budget;
        if(cast!=0&&nerubPlagueBudgets.TryGetValue(cast,out budget)){if(budget.used>=3)return;budget.used++;}
        foreach(var ally in Units.Where(u=>Nerub(u)&&u.team==effect.sourceTeam&&u.hp<u.maxHp&&Vector2.Distance(u.position,dead.position)<=350f/30).OrderBy(u=>Lines[u.kind].unit=="nN04"?1:0).ThenBy(u=>u.hp/u.maxHp).Take(2))Heal(ally,F(effect.config,"death_heal",60));
        if(Organic(dead))QueueNerub(effect.sourceTeam,dead.position,F(effect.config,"death_spawn_life",12));
    }
    void PruneNerubPlague(){foreach(var id in nerubPlagueBudgets.Where(p=>p.Value.until<Elapsed).Select(p=>p.Key).ToArray())nerubPlagueBudgets.Remove(id);}
}
}
