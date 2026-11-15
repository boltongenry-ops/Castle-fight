using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    sealed class CentaurHits {public int team,hits;}
    readonly Dictionary<int,CentaurHits> centaurCombatHits=new Dictionary<int,CentaurHits>();
    public int CentaurThrusts,CentaurBreaks,CentaurNeedles,CentaurRallies,CentaurStomps;
    bool CentaurVisible(Unit observer,Unit target)=>target!=null&&target.hp>0&&target.team!=observer.team&&!Invulnerable(target)&&!Hidden(target)&&!CorruptedAirborne(target)&&NightCanDetect(observer.team,target)&&(VisionTest==null||VisionTest(observer.team,target.position))&&(observer.team!=1||BotSeesUnit(target));
    bool CentaurCanCast(Unit unit)=>unit.hp>0&&!Invulnerable(unit)&&!Hidden(unit)&&!Disabled(unit)&&!Silenced(unit)&&unit.stun<=0&&unit.castLeft<0&&unit.castPause<=0&&unit.strikeLeft<0;
    static bool CentaurReady(Unit unit,string id)=>!unit.spellTimers.TryGetValue(id,out float left)||left<=0;
    float CentaurPhysicalBlock(Unit victim,float amount)=>victim.abilities.Contains("CT19")?Mathf.Min(amount,Mathf.Max(1,amount-(Lines[victim.kind].unit=="cU12"?12:7))):amount;
    void CentaurCombatHit(Unit source,Unit target,float dealt){
        if(Finished||NetworkReplica||source==null||target==null||source.hp<=0||dealt<=0||target.team==source.team)return;
        bool thrust=source.abilities.Contains("CT18"),crack=source.abilities.Contains("CT20");if(!thrust&&!crack)return;
        if(!centaurCombatHits.TryGetValue(source.id,out var state)||state.team!=source.team)centaurCombatHits[source.id]=state=new CentaurHits{team=source.team};
        int hits=++state.hits;
        // Killing blows still advance the fighter's own hit counter. They cannot
        // apply additional damage or a status to an already defeated target.
        if(target.hp<=0)return;
        if(thrust&&hits%4==0){float ratio=Lines[source.kind].unit=="cU11"?.7f:.5f;DamageUnit(target,source.shotDamage*ratio,source.team,Lines[source.kind].weapons[0].attack,false,false,source);CentaurThrusts++;PulseAt(target.position,1.2f,new Color(1,.75f,.25f),"Выпад",.4f,"cleave");}
        if(crack&&hits%3==0&&!Invulnerable(target)&&!CorruptedAirborne(target)){
            string effect=Lines[source.kind].unit=="cU13"?"CT22":"CT21";ApplyEffect(target,effect,source.team);CentaurBreaks++;PulseAt(target.position,1.2f,new Color(.85f,.5f,.2f),"Раскол брони",.4f);
        }
    }
    void TickCentaurCombat(){
        foreach(int id in centaurCombatHits.Keys.Where(id=>!Units.Any(u=>u.id==id&&u.hp>0)).ToArray())centaurCombatHits.Remove(id);
        foreach(var unit in Units.Where(CentaurCanCast).ToArray()){
            // An earlier caster in this same batch may just have stunned or
            // killed this one. Recheck before letting it execute an instant spell.
            if(!CentaurCanCast(unit))continue;
            if(unit.abilities.Contains("CT23")&&unit.mana>=25&&CentaurReady(unit,"CT23")){
                var target=Units.Where(v=>CentaurVisible(unit,v)&&!Flying(v)&&v.spellResist<1&&Vector2.Distance(v.position,unit.position)<=18).OrderBy(v=>(v.position-unit.position).sqrMagnitude).FirstOrDefault();
                if(target!=null){unit.mana-=25;unit.spellTimers["CT23"]=8;unit.castPause=.4f;float damage=Lines[unit.kind].unit=="cU15"?120:60;
                    foreach(var victim in Units.Where(v=>CentaurVisible(unit,v)&&!Flying(v)&&v.spellResist<1&&Vector2.Distance(v.position,target.position)<=5).ToArray())DamageUnit(victim,damage,unit.team,"magic",true,false,unit);
                    CentaurNeedles++;PulseAt(target.position,5,new Color(.95f,.68f,.27f),"Песчаные иглы",.65f);
                }
            }
            if(unit.abilities.Contains("CT24")&&unit.mana>=30&&CentaurReady(unit,"CT24")){
                var allies=Units.Where(v=>v.team==unit.team&&v.hp>0&&v.hp<v.maxHp&&!Invulnerable(v)&&!Hidden(v)&&Lines[v.kind].unit!="YTct"&&Vector2.Distance(v.position,unit.position)<=15).OrderBy(v=>v.hp/v.maxHp).ThenBy(v=>v.id).Take(4).ToArray();
                if(allies.Length>0){unit.mana-=30;unit.spellTimers["CT24"]=8;unit.castPause=.4f;float heal=Lines[unit.kind].unit=="cU16"?70:35;foreach(var ally in allies){ally.hp=Mathf.Min(ally.maxHp,ally.hp+heal);PulseAt(ally.position,1,Color.green,"Перекличка",.5f,"heal");}CentaurRallies++;}
            }
            // Do not spend the .4s cast pause before a charged melee opening:
            // that pause would reset the stride and suppress the Khan's cry.
            if(unit.abilities.Contains("CT26")&&CentaurReady(unit,"CT26")&&CentaurChargeProgress(unit)<1){
                var victims=Units.Where(v=>CentaurVisible(unit,v)&&!Flying(v)&&v.spellResist<1&&Vector2.Distance(v.position,unit.position)<=6).ToArray();
                if(victims.Length>0){unit.spellTimers["CT26"]=10;unit.castPause=.4f;foreach(var victim in victims){if(NagaShieldBlock(victim))continue;DamageUnit(victim,90,unit.team,"magic",true,false,unit);if(victim.hp>0)victim.stun=Mathf.Max(victim.stun,Lines[victim.kind].armorType=="hero"||victim.abilities.Any(id=>(string)Abilities[id]?["kind"]=="resistant_skin")?.5f:1);}CentaurStomps++;PulseAt(unit.position,6,new Color(.9f,.7f,.3f),"Топот покорителя",.7f);}
            }
        }
    }
}
}
