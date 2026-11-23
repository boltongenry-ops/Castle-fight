using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class HumanMatch {
    sealed class Shrapnel { public Vector2 center; public string ammo; public int team,ticks; public Unit cursedVictim; public float age,radius; }
    readonly List<Shrapnel> shrapnel = new List<Shrapnel>();
    static readonly Color iron = new Color(.56f,.48f,.62f);
    static Color StoneColor(string ammo) => ammo=="bU12"?Color.green:ammo=="bU13"?new Color(1,.42f,.08f):ammo=="bU14"?new Color(.72f,.24f,.88f):Color.gray;
    static bool HandFighter(Line line)=>line.unit!=null&&line.unit.StartsWith("bU",System.StringComparison.Ordinal);

    static bool BerserkImmune(Unit unit,string attack) =>
        attack!="magic" && attack!="spells" && unit.spellTimers.TryGetValue("BHBz",out float left) && left>0;
    void TriggerBerserk(Unit unit,float before){
        if(unit.hp<=0 || Lines[unit.kind].unit!="bU02" || before*10<unit.maxHp*3 ||
           unit.hp*10>=unit.maxHp*3 || unit.spellTimers.ContainsKey("BHBz"))return;
        unit.spellTimers["BHBz"]=5;
        PulseAt(unit.position,2,Color.red,"Берсерк",.8f,"blackhand:guard",unit.id);
    }
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
        bool soldierBlock = guard=="bU00" && amount>0 && Roll()<.15f;
        bool veteranBlock = (guard=="bU01"||guard=="bU02") && ReadyHand(victim,"BHGuard");
        if(soldierBlock||veteranBlock){
            if(veteranBlock)victim.spellTimers["BHGuard"]=7;
            PulseAt(victim.position,1.55f,iron,"Блок",.48f,"blackhand:guard",victim.id);
            amount=soldierBlock?0:amount*.45f;
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
        if(shot.chaosSpell=="hand-bolt")PierceHandBolt(shot);
        if(shot.kind!=3 || shot.spell || Lines[shot.line].unit!="bU04")return;
        var target=FindUnit(shot.target);
        if(target!=null && target.team!=shot.team && target.hp>0 && target.maxHp>=450)
            shot.damage*=1.35f;
    }

    void PierceHandBolt(Shot shot) {
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
            var allies=PepeInRange(hut.position,40).Where(v=>v.team==team&&v.hp>0&&v.hp<v.maxHp)
                .OrderBy(v=>v.hp/v.maxHp).ThenBy(v=>v.id).Take(3).ToArray();
            foreach(var ally in allies)Heal(ally,75,"blackhand:heal");
            hut.spellTimers[skill]=allies.Length>0?5:.5f;
            return;
        }
        if(id=="bH15"){
            var targets=Units.Where(v=>HandTarget(v,team)&&!v.effects.Any(e=>e.id=="BHKn"&&e.left>0))
                .OrderBy(v=>(v.position-hut.position).sqrMagnitude).ThenBy(v=>v.id).Take(5).ToArray();
            foreach(var enemy in targets){
                DamageUnit(enemy,45,team);
                if(enemy.hp>0)HandEffect(enemy,team,"BHKn",6,-.30f);
                PulseAt(enemy.position,2,iron,"Набат",.65f,"blackhand:silence",enemy.id);
            }
            if(targets.Length>0)PulseAt(hut.position,3,iron,"",.7f,"blackhand:bell");
            hut.spellTimers[skill]=targets.Length>0?14:1;
            return;
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
        bool directHit=TargetAlive(shot.target)&&(TargetPosition(shot.target,shot.team)-shot.end).sqrMagnitude<=4;
        if(directHit)
            ResolveAttack(shot.sourceId,shot.team,shot.target,shot.damage,shot.weapon,0,false,true);
        float radius=Aspects[shot.team]==1?7:6;
        var struck=directHit && ammo=="bU14" ? FindUnit(shot.target) : null;
        if(struck!=null && struck.hp>0 && struck.team!=shot.team && !Invulnerable(struck))
            HandEffect(struck,shot.team,"BHCr",3,-.2f);
        shrapnel.Add(new Shrapnel{team=shot.team,ammo=ammo,center=shot.end,radius=radius,cursedVictim=struck});
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
                var targets=PepeInRange(zone.center,zone.radius).Where(v=>v.team!=zone.team&&v.hp>0&&!Flying(v)&&!Invulnerable(v)).ToList();
                var struck=zone.cursedVictim;
                if(struck!=null && struck.team!=zone.team && struck.hp>0 && !Flying(struck) && !Invulnerable(struck) && !targets.Contains(struck))
                    targets.Add(struck);
                foreach(var v in targets){
                    DamageUnit(v,fire?(zone.ticks==3?42:24):poison||curse?12:10,zone.team,fire?"magic":poison?"pierce":curse?"chaos":"siege",true);
                    if(v.hp<=0)continue;
                    if(poison&&Organic(v))HandEffect(v,zone.team,"BHPs",3);
                    if(curse){
                        if(v!=struck)HandEffect(v,zone.team,"BHCr",3,-.2f);
                        PulseAt(v.position,1.4f,StoneColor(zone.ammo),"",.46f,"blackhand:curse",v.id);
                    }
                }
                PulseAt(zone.center,zone.radius,StoneColor(zone.ammo),"",.52f,AmmoFx(zone.ammo));
            }
            if(zone.ticks==3)shrapnel.RemoveAt(i);
        }
    }
}
}
