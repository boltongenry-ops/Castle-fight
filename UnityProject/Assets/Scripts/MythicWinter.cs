using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class HumanMatch {
    public const string WinterTrial="mythic_eternal_winter", WinterChain="mythic_winter_chains";
    public const int WinterPhaseTwoHp=2500;
    const int WinterBossTeam=1;
    const float WinterInterval=10, WinterChainDuration=3, PhaseTwoIceDamage=15;
    readonly List<Unit> winterFallen=new List<Unit>();
    public bool WinterPhaseTwo {get;private set;}
    float nextWinterChain;
    JObject winterChain;
    public bool EternalWinter=>winterChain!=null;
    public float WinterChainIn=>Mathf.Max(0,nextWinterChain-Elapsed);

    void PrepareEternalWinter(){
        if((string)ChallengeRules?["id"]!=WinterTrial)return;
        winterChain=new JObject{
            ["id"]=WinterChain,["name"]="Ледяные оковы",
            ["kind"]="debuff",["stack_group"]=WinterChain,["duration"]=WinterChainDuration,
            ["hero_duration"]=WinterChainDuration,["root"]=true,["disarm"]=true
        };
        nextWinterChain=WinterInterval;
    }

    public bool WinterAffects(Unit unit)=>EternalWinter&&unit.team==0&&!TeamNorthern[unit.team];
    float WinterMoveFactor(Unit unit)=>WinterAffects(unit)?.5f:1;
    float WinterAttackFactor(Unit unit)=>WinterAffects(unit)?.7f:1;

    void AdvanceWinterPhase(){
        if(!EternalWinter||WinterPhaseTwo||CastleHp[WinterBossTeam]<=0||CastleHp[WinterBossTeam]>WinterPhaseTwoHp)return;
        WinterPhaseTwo=true;
        winterChain["dps"]=PhaseTwoIceDamage;
    }

    static bool WinterIce(Effect effect)=>effect!=null&&effect.sourceTeam==WinterBossTeam&&
        (effect.group==WinterChain||effect.group=="northern_ice_chains");

    bool QueueWinterDefection(Unit fallen){
        if(!WinterPhaseTwo||!WinterAffects(fallen)||Hidden(fallen)||Invulnerable(fallen)||
            ElementalIllusion(fallen)||Lines[fallen.kind].unit=="YTct")return false;
        foreach(var effect in fallen.effects){
            // The last tick of an ice spell can deal damage at zero remaining duration.
            if(!WinterIce(effect)||(effect.left<=0&&effect!=nerubTickingEffect))continue;
            winterFallen.Add(fallen);
            return true;
        }
        return false;
    }

    void FlushWinterDefections(){
        if(winterFallen.Count==0)return;
        if(!Finished)foreach(var fallen in winterFallen){
            var unit=Spawn(WinterBossTeam,fallen.kind,fallen.position);
            unit.onRoad=true;
            unit.summoned=fallen.summoned;
            unit.summonLeft=fallen.summonLeft;
            unit.noBounty=fallen.noBounty;
            unit.noCorpse=fallen.noCorpse;
            unit.spellBorn=fallen.spellBorn;
            unit.seaSummon=fallen.seaSummon;
            PulseAt(unit.position,2,new Color(.54f,.82f,1),"Ледяное перерождение");
        }
        winterFallen.Clear();
    }

    void TickEternalWinter(){
        if(!EternalWinter||Elapsed+.0001f<nextWinterChain)return;
        nextWinterChain+=WinterInterval;
        Unit chosen=null;
        int eligible=0;
        foreach(var unit in Units){
            if(!WinterAffects(unit)||unit.hp<=0||!ChaosFighter(unit))continue;
            if(++eligible==1||Roll()<1f/eligible)chosen=unit;
        }
        if(chosen==null)return;
        ApplyEffectData(chosen,winterChain,WinterBossTeam);
        PulseAt(chosen.position,1.5f,new Color(.54f,.82f,1),"Ледяные оковы");
    }
}
}
