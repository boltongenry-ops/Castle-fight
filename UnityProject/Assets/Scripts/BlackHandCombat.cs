using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class HumanMatch {
    sealed class Shrapnel { public Vector2 center; public string ammo; public int team,ticks; public float age,radius; }
    readonly List<Shrapnel> shrapnel = new List<Shrapnel>();
    static readonly Color iron = new Color(.56f,.48f,.62f);
    static Color StoneColor(string ammo) => ammo=="bU12"?Color.green:ammo=="bU13"?new Color(1,.42f,.08f):ammo=="bU14"?new Color(.72f,.24f,.88f):Color.gray;
    static bool HandFighter(Line line)=>line.unit!=null&&line.unit.StartsWith("bU",System.StringComparison.Ordinal);

    void TriggerHandIron(Unit unit,float previousHp) {
        if(unit.handIronUsed||Aspects[unit.team]!=0||!HandFighter(Lines[unit.kind])||
           unit.hp<=0||previousHp<unit.maxHp*.5f||unit.hp>=unit.maxHp*.5f)return;
        unit.handIronUsed=true;
        HandEffect(unit,unit.team,"BHIr",5);
        PulseAt(unit.position,1.6f,iron,"Железный устав",.63f,"blackhand:guard",unit.id);
    }

    float HandIronDamage(Unit unit,float amount) {
        if(Aspects[unit.team]==0&&unit.handIronUsed&&HandFighter(Lines[unit.kind])&&
           unit.effects.Exists(e=>e.id=="BHIr"&&e.left>0&&e.sourceTeam==unit.team))return amount*.8f;
        return amount;
    }

    bool ReadyHand(Unit u,string key) => !u.spellTimers.TryGetValue(key,out var t)||t<=0;
    bool HandTarget(Unit target,int team) => target.team!=team && target.hp>0 && !Invulnerable(target) && PepeVisible(target,team);
    void HandEffect(Unit target,int team,string id,int duration,float power=0) {
        var e=target.effects.Find(x=>x.id==id && x.sourceTeam==team);
        if(e==null){e=new Effect{id=id,group=id,sourceTeam=team,config=(JObject)Abilities[id]};target.effects.Add(e);}
        e.lifetime=e.left=duration;e.power=power;
    }
    float BlackHandMelee(Unit owner,Unit victim,float amount,bool primary) {
        if(!primary||owner==null||owner.team==victim.team||Invulnerable(victim)||Ethereal(victim))return amount;
        string id=Lines[owner.kind].unit;
        if((id=="bU05"||id=="bU06")&&ReadyHand(owner,"BHLunge")){
            amount+=id=="bU06"?40:25;owner.spellTimers["BHLunge"]=id=="bU06"?7:8;
            PulseAt(victim.position,1.35f,iron,"",.38f,"blackhand:pike",victim.id);
        }
        string guard=Lines[victim.kind].unit;
        if((guard=="bU00"||guard=="bU01"||guard=="bU02")&&ReadyHand(victim,"BHGuard")){
            victim.spellTimers["BHGuard"]=guard=="bU00"?8:7;
            PulseAt(victim.position,1.55f,iron,"Блок",.48f,"blackhand:guard",victim.id);amount*=guard=="bU00"?.6f:.45f;
        }
        return amount;
    }
    void BlackHandHit(Unit owner,Unit victim) {
        string id=Lines[owner.kind].unit;
        if(id=="bU05"||id=="bU06"){
            if(Organic(victim)&&Roll()<(id=="bU05"?.25f:.35f)){
                bool strong=id=="bU06"||victim.effects.Any(e=>e.id=="BHW2"&&e.left>0&&e.sourceTeam==owner.team);
                if(strong)victim.effects.RemoveAll(e=>e.id=="BHW1");
                HandEffect(victim,owner.team,strong?"BHW2":"BHW1",5);
                PulseAt(victim.position,1.35f,Color.red,"Кровотечение",.55f,"blackhand:bleed",victim.id);
            }
        }else if(id=="bU07"||id=="bU08"){
            bool elite=id=="bU08";string skill=elite?"BH08":"BH07";
            if(Flying(victim)||Invulnerable(victim)||!ReadyHand(owner,skill)||victim.hp>Mathf.Min(elite?200:120,victim.maxHp*(elite?.2f:.15f)))return;
            owner.spellTimers[skill]=elite?8:10;
            DamageUnit(victim,victim.hp,owner.team,"normal",false,true,owner);
            PulseAt(victim.position,1.75f,Color.red,"Казнь",.70f,"blackhand:execute");
        }else if(id=="bU02"&&ReadyHand(owner,"BH02")){
            owner.spellTimers["BH02"]=8;
            Vector2 forward=(victim.position-owner.position).normalized;
            foreach(var v in PepeInRange(owner.position,5).Where(v=>HandTarget(v,owner.team)&&!Flying(v)&&Vector2.Dot(forward,v.position-owner.position)>=0).OrderBy(v=>v==victim?0:1).Take(3).ToArray())
                DamageUnit(v,65,owner.team,"normal",false,false,owner);
            PulseAt(victim.position,2.4f,iron,"Удар щитом",.55f,"blackhand:bash");
        }
    }
    bool BlackHandCast(Unit u) {
        string id=Lines[u.kind].unit;
        if(id!="bU09"&&id!="bU10")return false;
        bool elite=id=="bU10";string skill=elite?"BH10":"BH09";int mana=elite?45:35;
        if(Silenced(u)||u.mana<mana||!ReadyHand(u,skill))return false;
        var reserved=Units.Where(actor=>actor.team==u.team&&actor.hp>0&&
            (actor.pendingSpell=="BH09"||actor.pendingSpell=="BH10")).Select(actor=>actor.spellTarget).ToHashSet();
        var target=PepeInRange(u.position,20).Where(v=>HandTarget(v,u.team)&&v.spellResist<1&&
            !reserved.Contains(v.id)&&!v.effects.Any(e=>e.id=="BHTx"))
            .OrderBy(v=>Lines[v.kind].cooldown).ThenBy(v=>v.id).FirstOrDefault();
        if(target==null)return false;
        u.pendingSpell=skill;u.spellTarget=target.id;u.castLeft=.45f;
        u.finalCastPose="Attack";u.pose=u.finalCastPose;u.attackSerial++;
        PulseAt(u.position,1.4f,iron,"",.45f,"blackhand:invoke",u.id);
        return true;
    }
    bool ResolveBlackHandTithe(Unit u,Unit target,bool elite) {
        if(target==null||!HandTarget(target,u.team)||target.spellResist>=1||
            (target.position-u.position).sqrMagnitude>400||target.effects.Any(e=>e.id=="BHTx"))return false;
        if(ElvenSpellShield(target))return true;
        DamageUnit(target,elite?90:65,u.team);
        if(target.hp>0){HandEffect(target,u.team,"BHTx",elite?7:6);target.effects.Find(e=>e.id=="BHTx").dotTick=elite?5:4;}
        PulseAt(target.position,1.6f,iron,"Железная десятина",.72f,"blackhand:tithe",target.id);
        return true;
    }
    bool BlackHandRelease(Unit u) {
        if(u.effects.Count==0)return false;
        var e=u.effects.Find(x=>x.id=="BHTx"&&x.left>0&&x.sourceTeam!=u.team);
        if(e!=null){
            DamageUnit(u,e.lifetime>6?45:30,e.sourceTeam);
            if(--e.dotTick<=0)u.effects.Remove(e);
            PulseAt(u.position,1.35f,iron,"Десятина",.44f,"blackhand:tithe-hit",u.id);
        }
        return u.hp<=0;
    }
    void HandBolt(Shot shot) {
        if(shot.chaosSpell!="hand-bolt")return;
        Vector2 direction=(shot.end-shot.start).normalized;
        var second=PepeInRange(shot.end,8).Where(v=>v.id!=shot.target&&HandTarget(v,shot.team)&&(Flying(v)?shot.weapon.air:shot.weapon.ground))
            .Where(v=>{var delta=v.position-shot.end;float along=Vector2.Dot(delta,direction);return along>0&&(delta-direction*along).sqrMagnitude<=2.25f;})
            .OrderBy(v=>(v.position-shot.end).sqrMagnitude).FirstOrDefault();
        if(second==null)return;
        var bolt=AddShot(shot.team,shot.line,shot.sourceId,second.id,shot.end,second.position,shot.damage*(Lines[shot.line].unit=="bU04"?.65f:.4f),shot.weapon,0,3);
        bolt.nightSecondary=true;bolt.chaosSpell="hand-pierce";
        PulseAt(shot.end,1.15f,iron,"",.4f,"blackhand:bolt");
    }
    void BlackHandSplit(Shot shot,float dt) {
        if(shot.kind!=2||shot.hit||shot.age>=shot.duration*.72f||shot.age+dt<shot.duration*.72f)return;
        string ammo=Lines[shot.line].unit;if(!IsHandStone(ammo))return;
        for(int i=-1;i<=1;i++){
            Vector2 offset=new Vector2(i*2,1.5f-i*i);
            var fragment=AddShot(shot.team,shot.line,shot.sourceId,shot.target,Vector2.Lerp(shot.start,shot.end,.72f),shot.end+offset,0,shot.weapon,0,3);
            fragment.chaosSpell="hand-shard";fragment.donorId=BlackHand.Ammo(ammo);
            fragment.arc=0;fragment.homing=false;fragment.startHeight=Mathf.Lerp(shot.startHeight,shot.endHeight,.72f)+4*.72f*.28f*Vector2.Distance(shot.start,shot.end)*shot.arc;fragment.endHeight=.2f;fragment.duration=shot.duration*.28f;
        }
        // The split is drawn by the in-flight shard views. Ground effects start on impact.
    }
    void BlackHandBuilding(Hut hut) {
        string id=Lines[hut.kind].building;
        if(id!="bH15"&&id!="bH16"&&id!="bH17")return;
        string skill=id=="bH15"?"BH15":id=="bH16"?"BH16":"BH17";
        if(hut.spellTimers.TryGetValue(skill,out float left)&&left>0)return;
        int team=hut.team;
        if(id=="bH16"){
            var ally=PepeInRange(hut.position,20).Where(v=>v.team==team&&v.hp>0&&v.hp<v.maxHp).OrderBy(v=>v.hp/v.maxHp).FirstOrDefault();
            if(ally==null)return;
            Heal(ally,75,"blackhand:heal");hut.spellTimers[skill]=5;return;
        }
        if(id=="bH15"){
            var caster=PepeInRange(hut.position,20).FirstOrDefault(v=>HandTarget(v,team)&&v.pendingSpell!=null&&v.castLeft>0);
            if(caster==null)return;
            caster.spellTimers[caster.pendingSpell]=SpellCooldown(caster,caster.pendingSpell);
            caster.pendingSpell=null;caster.castLeft=-1;caster.castPause=.5f;
            PulseAt(caster.position,2,iron,"Чары прерваны",.68f,"blackhand:silence",caster.id);
            PulseAt(hut.position,2.2f,iron,"",.60f,"blackhand:bell");hut.spellTimers[skill]=12;return;
        }
        var enemies=PepeInRange(hut.position,24).Where(v=>HandTarget(v,team)).ToArray();
        if(enemies.Length==0)return;
        var center=enemies.OrderByDescending(c=>enemies.Count(v=>(c.position-v.position).sqrMagnitude<=36)).ThenBy(c=>c.id).First();
        foreach(var v in enemies.Where(v=>(center.position-v.position).sqrMagnitude<=36).OrderBy(v=>(center.position-v.position).sqrMagnitude).Take(5)){
            DamageUnit(v,100,team);
            if(v.hp>0)HandEffect(v,team,"BHJu",6);
        }
        PulseAt(center.position,6,iron,"Последний суд",6,"blackhand:judgment");hut.spellTimers[skill]=30;
    }
    static bool IsHandStone(string id)=>id=="bU11"||id=="bU12"||id=="bU13"||id=="bU14";
    bool BlackHandImpact(Shot shot) {
        if(shot.kind!=2)return false;
        string ammo=Lines[shot.line].unit;if(!IsHandStone(ammo))return false;
        if(TargetAlive(shot.target)&&(TargetPosition(shot.target,shot.team)-shot.end).sqrMagnitude<=4)
            ResolveAttack(shot.sourceId,shot.team,shot.target,shot.damage,shot.weapon,0,false,true);
        float radius=Aspects[shot.team]==1?7:6;
        shrapnel.Add(new Shrapnel{team=shot.team,ammo=ammo,center=shot.end,radius=radius});
        PulseAt(shot.end,radius,StoneColor(ammo),"",.48f,AmmoFx(ammo));
        return true;
    }
    static string AmmoFx(string ammo)=>ammo=="bU12"?"blackhand:poison":ammo=="bU13"?"blackhand:fire":ammo=="bU14"?"blackhand:curse":"blackhand:stone";
    void BlackHandZones(float dt) {
        for(int i=shrapnel.Count-1;i>=0;i--){
            var zone=shrapnel[i];zone.age+=dt;
            while(zone.ticks<3&&zone.age>=zone.ticks+1){
                zone.ticks++;
                bool fire=zone.ammo=="bU13",poison=zone.ammo=="bU12",curse=zone.ammo=="bU14";
                foreach(var v in PepeInRange(zone.center,zone.radius).Where(v=>v.team!=zone.team&&v.hp>0&&!Flying(v)&&!Invulnerable(v)).ToArray()){
                    DamageUnit(v,fire?(zone.ticks==3?42:24):poison||curse?12:10,zone.team,fire?"magic":poison?"pierce":curse?"chaos":"siege",true);
                    if(v.hp<=0)continue;
                    if(poison&&Organic(v))HandEffect(v,zone.team,"BHPs",3);
                    if(curse)HandEffect(v,zone.team,"BHCr",3,-.2f);
                }
                PulseAt(zone.center,zone.radius,StoneColor(zone.ammo),"",.52f,AmmoFx(zone.ammo));
            }
            if(zone.ticks==3)shrapnel.RemoveAt(i);
        }
    }
}
}
