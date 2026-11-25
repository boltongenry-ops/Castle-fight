using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class HumanMatch {
    public const string HordeTrial="mythic_cursed_horde", HordeRageId="mythic_horde_rage", HordeFatigueId="mythic_horde_fatigue";
    public const int HordeCastleHealth=12000;
    const float HordePreparation=180, HordeRitualDuration=2, HordeRageDuration=12, HordeFatigueDuration=6;
    public const int HordeRitualTargets=8;
    public const float HordeNormalInterval=40, HordeWarchiefInterval=25;
    const int HordeBossTeam=1;
    static readonly Vector2[] HordeRoutes={
        new Vector2(0,-12),new Vector2(0,0),new Vector2(0,12)
    };
    static readonly Color HordeRed=new Color(1,.21f,.1f);
    static readonly JObject HordeRageConfig=new JObject{
        ["id"]=HordeRageId,["name"]="Ярость эликсира",["kind"]="buff",
        ["description"]="Движение +25%, скорость атаки +35%, урон оружия +20%.",
        ["duration"]=HordeRageDuration,["haste"]=.35f,["power"]=.2f
    };
    static readonly JObject HordeFatigueConfig=new JObject{
        ["id"]=HordeFatigueId,["name"]="Истощение",["kind"]="debuff",
        ["description"]="Скорость атаки −25% после действия эликсира.",
        ["duration"]=HordeFatigueDuration,["haste"]=-.25f
    };
    sealed class HordeShaman {
        public Unit carrier;
        public Vector2 altar;
        public float ritualEnd=-1;
    }
    readonly List<HordeShaman> hordeShamans=new List<HordeShaman>(3);
    readonly List<Unit> hordeAffected=new List<Unit>(24);
    readonly List<Unit> hordeCandidates=new List<Unit>(24);
    float nextHordeWave=HordePreparation, hordeChargeAt, hordeChargeRecovery;
    Vector2 hordeChargeStart,hordeChargeEnd;
    Unit hordeWarchief;
    int hordeShamanKind,hordeWarchiefKind,hordeGuardKind;
    public bool CursedHorde {get;private set;}
    public bool HordeSecondStage {get;private set;}
    public int HordeDrumBeat {get;private set;}
    public int HordeShamansAlive=>hordeShamans.Count;
    public float HordeNextWaveIn=>Mathf.Max(0,nextHordeWave-Elapsed);
    public float HordeWaveInterval=>hordeWarchief!=null&&hordeWarchief.hp>0?HordeWarchiefInterval:HordeNormalInterval;
    public bool HordeRaging(Unit u)=>CursedHorde&&u.team==HordeBossTeam&&u.effects.Exists(e=>e.id==HordeRageId&&e.left>0);
    public bool HordeAnyRage=>hordeAffected.Exists(u=>u.hp>0&&HordeRaging(u));
    public bool IsHordeShaman(Unit u)=>CursedHorde&&u.team==HordeBossTeam&&hordeShamans.Exists(s=>s.carrier==u);
    public bool IsHordeWarchief(Unit u)=>CursedHorde&&u!=null&&u==hordeWarchief;
    bool HordeImmuneToWeapon(Unit victim,Weapon weapon)=>IsHordeWarchief(victim)&&
        (weapon.attack=="magic"||weapon.attack=="spells");
    public bool HordeCharging=>CursedHorde&&hordeWarchief!=null&&hordeWarchief.hp>0&&hordeChargeAt>0;
    public Vector2 HordeChargeStart=>hordeChargeStart;
    public Vector2 HordeChargeEnd=>hordeChargeEnd;

    void BeginCursedHorde(){
        CursedHorde=(string)ChallengeRules?["id"]==HordeTrial;
        if(!CursedHorde)return;
        hordeShamanKind=Array.FindIndex(Lines,l=>l.unit=="o003");
        hordeWarchiefKind=Array.FindIndex(Lines,l=>l.unit=="o005");
        hordeGuardKind=Array.FindIndex(Lines,l=>l.unit=="o002");
    }

    void StartHordeWave(){
        nextHordeWave=Elapsed+HordeWaveInterval;
        if(hordeShamanKind<0||hordeShamans.Count>0)return;
        HordeDrumBeat++;
        for(int i=0;i<HordeRoutes.Length;i++){
            var position=CastlePosition(HordeBossTeam)+new Vector2(-15,(i-1)*9);
            var carrier=Spawn(HordeBossTeam,hordeShamanKind,position);
            hordeShamans.Add(new HordeShaman{carrier=carrier,altar=HordeRoutes[i]});
            if(hordeGuardKind>=0)
                for(int escort=0;escort<3;escort++)
                    if(ArmyCount(HordeBossTeam)<ArmyLimit)
                        Spawn(HordeBossTeam,hordeGuardKind,position+new Vector2(-5-escort*3,(escort-1)*2));
        }
    }

    void EnrageHordeUnit(Unit u){
        if(HordeRaging(u)||u.effects.Exists(e=>e.id==HordeFatigueId&&e.left>0))return;
        ApplyEffectData(u,HordeRageConfig,HordeBossTeam);
        hordeAffected.Add(u);
        PulseAt(u.position,1.5f,HordeRed,"",.45f);
    }

    void CompleteHordeRitual(HordeShaman shaman){
        HordeDrumBeat++;
        var center=shaman.carrier.position;
        PulseAt(center,17,HordeRed,"Эликсир ярости",.8f);
        hordeCandidates.Clear();
        foreach(var unit in Units)
            if(unit.team==HordeBossTeam&&unit!=shaman.carrier&&ChaosFighter(unit)&&
                !IsHordeShaman(unit)&&!IsHordeWarchief(unit)&&!HordeRaging(unit)&&
                !unit.effects.Exists(e=>e.id==HordeFatigueId&&e.left>0)&&
                (unit.position-center).sqrMagnitude<=17*17)hordeCandidates.Add(unit);
        hordeCandidates.Sort((a,b)=>{
            int distance=(a.position-center).sqrMagnitude.CompareTo((b.position-center).sqrMagnitude);
            return distance!=0?distance:a.id.CompareTo(b.id);
        });
        for(int i=0;i<Mathf.Min(HordeRitualTargets,hordeCandidates.Count);i++)EnrageHordeUnit(hordeCandidates[i]);
        hordeCandidates.Clear();
    }

    void TickCursedHorde(){
        if(!CursedHorde)return;
        for(int i=hordeShamans.Count-1;i>=0;i--){
            var shaman=hordeShamans[i];
            if(shaman.carrier.hp<=0||shaman.carrier.team!=HordeBossTeam){hordeShamans.RemoveAt(i);continue;}
            if(shaman.ritualEnd<0||Elapsed<shaman.ritualEnd)continue;
            if(shaman.carrier.stun>0||Disabled(shaman.carrier))continue;
            CompleteHordeRitual(shaman);
            hordeShamans.RemoveAt(i);
        }
        for(int i=hordeAffected.Count-1;i>=0;i--){
            var unit=hordeAffected[i];
            if(unit.hp>0&&HordeRaging(unit))continue;
            if(unit.hp>0&&unit.team==HordeBossTeam)ApplyEffectData(unit,HordeFatigueConfig,HordeBossTeam);
            else unit.effects.RemoveAll(e=>e.id==HordeRageId);
            hordeAffected.RemoveAt(i);
        }
        if(!HordeSecondStage&&CastleHp[1]>0&&CastleHp[1]<=CastleHealthLimit(1)*.5f){
            HordeSecondStage=true;HordeDrumBeat++;
            var position=CastlePosition(1)+Vector2.left*16;
            PulseAt(position,9,HordeRed,"Разбитый котёл",1.3f);
            if(hordeWarchiefKind>=0){
                hordeWarchief=Spawn(1,hordeWarchiefKind,position);
                hordeWarchief.hp=hordeWarchief.maxHp*=2.5f;
                hordeWarchief.spellResist=1;
                hordeChargeRecovery=Elapsed+4;
                if(hordeGuardKind>=0)
                    for(int i=-2;i<=2;i++){
                        var guard=Spawn(1,hordeGuardKind,position+new Vector2(-5,i*3.5f));
                        guard.hp=guard.maxHp*=1.25f;
                        EnrageHordeUnit(guard);
                    }
                if(Elapsed>=HordePreparation)nextHordeWave=Mathf.Min(nextHordeWave,Elapsed+HordeWarchiefInterval);
            }
        }
        if(hordeWarchief!=null&&(hordeWarchief.hp<=0||hordeWarchief.team!=1)){
            if(Elapsed>=HordePreparation)nextHordeWave=Elapsed+HordeNormalInterval;
            hordeWarchief=null;hordeChargeAt=0;
        }
        if(Elapsed>=nextHordeWave)StartHordeWave();
    }

    bool TickHordeActor(Unit u,float dt){
        if(!CursedHorde||u.team!=HordeBossTeam)return false;
        var shaman=hordeShamans.Find(s=>s.carrier==u);
        if(shaman!=null){
            if(shaman.ritualEnd>=0){u.pose="Spell";return true;}
            if((u.position-shaman.altar).sqrMagnitude<=4){
                shaman.ritualEnd=Elapsed+HordeRitualDuration;
                u.attackSerial++;u.pose="Spell";
                PulseAt(u.position,3.5f,HordeRed,"Ритуал ярости",HordeRitualDuration);
                return true;
            }
            var waypoint=ArmyWaypoint(u,shaman.altar,false);
            u.direction=(waypoint-u.position).normalized;
            u.position=Vector2.MoveTowards(u.position,waypoint,MoveSpeed(u)*dt);
            u.pose="Walk";
            return true;
        }
        if(u!=hordeWarchief)return false;
        if(hordeChargeAt>0){
            if(Elapsed<hordeChargeAt){u.pose="Spell";return true;}
            var segment=hordeChargeEnd-hordeChargeStart;
            u.position=hordeChargeEnd;u.previous=hordeChargeEnd;
            using(var snapshot=SimulationBuffers035.Take(Units))foreach(var victim in snapshot){
                if(victim.team==u.team||victim.hp<=0||Flying(victim)||!ChaosFighter(victim))continue;
                var relative=victim.position-hordeChargeStart;
                var along=Mathf.Clamp01(Vector2.Dot(relative,segment)/segment.sqrMagnitude);
                if((relative-segment*along).sqrMagnitude<=3.2f*3.2f)
                    DamageUnit(victim,95,u.team,"normal",false,killer:u);
            }
            hordeChargeAt=0;hordeChargeRecovery=Elapsed+12;
            u.pose="Attack";u.attackSerial++;
            u.castPause=2;
            ApplyEffectData(u,new JObject{["id"]="mythic_horde_open",["name"]="Открытая стойка",
                ["kind"]="debuff",["duration"]=2,["incoming"]=.25f},u.team);
            PulseAt(u.position,3,HordeRed,"Прорыв",.5f);
            return true;
        }
        if(Elapsed<hordeChargeRecovery||Rooted(u)||Disarmed(u))return false;
        Unit target=null;
        float best=15*15;
        foreach(var candidate in Units){
            if(candidate.team==u.team||candidate.hp<=0||Flying(candidate)||WeaponFor(u,candidate)==null)continue;
            float distance=(candidate.position-u.position).sqrMagnitude;
            if(distance<best&&distance>1){target=candidate;best=distance;}
        }
        if(target==null)return false;
        var destination=u.position+(target.position-u.position).normalized*12;
        if(ArmyWaypoint(u,destination,false)!=destination)return false;
        hordeChargeStart=u.position;hordeChargeEnd=destination;hordeChargeAt=Elapsed+1;
        u.strikeLeft=-1;u.pendingSpell=null;u.castLeft=-1;
        u.direction=(destination-u.position).normalized;
        u.pose="Spell";u.attackSerial++;
        return true;
    }
}
}
