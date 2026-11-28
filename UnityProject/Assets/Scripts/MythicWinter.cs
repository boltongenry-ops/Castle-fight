using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class HumanMatch {
    public const string WinterTrial="mythic_eternal_winter", WinterChain="mythic_winter_chains";
    public const int WinterPhaseTwoHp=2500;
    public const int WinterRulesRevision=2;
    public const string WinterQueenRaw="winter_queen",WinterQueenLine="winter_queen_boss",
        WinterJudgment="winter_judgment",WinterQueenFreeze="winter_queen_freeze",WinterQueenStorm="winter_queen_storm";
    public const int WinterQueenHealth=9000,WinterQueenDamage=200,WinterQueenArmor=10,WinterStormDamage=40;
    public const int WinterFreezeTargets=3,WinterStormDuration=5;
    public const float WinterStormRadius=15;
    const float QueenFreezeRange=20,QueenFreezeDuration=3,QueenFreezeCooldown=12,QueenStormCooldown=16;
    const int WinterBossTeam=1;
    const float WinterInterval=10, WinterChainDuration=3, PhaseTwoIceDamage=15;
    readonly List<Unit> winterFallen=new List<Unit>();
    public bool WinterPhaseTwo {get;private set;}
    float nextWinterChain;
    JObject winterChain;
    bool winterAwakeningStarted;
    Unit winterBoss;
    float winterStormStart;
    int winterStormTick=WinterStormDuration;
    public bool EternalWinter=>winterChain!=null;
    public bool WinterCastleFrozen {get;private set;}
    public bool WinterAwakeningPending {get;private set;}
    public bool WinterQueenFelled {get;private set;}
    public Unit WinterBoss=>winterBoss;
    public bool WinterStormActive=>winterBoss!=null&&winterBoss.hp>0&&winterStormTick<WinterStormDuration;
    public float WinterChainIn=>Mathf.Max(0,nextWinterChain-Elapsed);

    public static JObject WinterBossCatalog(JObject northern){
        var catalog=(JObject)northern.DeepClone();
        var lines=(JArray)catalog["lines"];
        var row=(JObject)lines.OfType<JObject>().First(l=>(string)l["unit"]=="n014").DeepClone();
        row["building"]=WinterQueenLine;row["unit"]=WinterQueenRaw;
        row["name"]=row["unitName"]="Джайна · Ледяная королева";
        row["category"]="boss";row["base"]=false;row["allowed"]=false;row["slots"]=1;
        var stats=(JObject)row["stats"];
        stats["base"]="Hjai";stats["hp"]=WinterQueenHealth;stats["armor"]=WinterQueenArmor;
        stats["armor_type"]="light";stats["attack_type"]="magic";stats["damage"]=WinterQueenDamage;
        stats["level"]=10;stats["unit_types"]="hero";stats["hp_regen"]=0;
        stats["max_mana"]=stats["start_mana"]=stats["mana_regen"]=0;
        stats.Remove("ability");
        stats["abilities"]=new JArray(WinterQueenFreeze,WinterQueenStorm);
        foreach(var weapon in (JArray)stats["weapons"]){
            weapon["damage"]=WinterQueenDamage;
            weapon["attack_type"]="magic";
        }
        row["abilities"]=new JArray();row["buildingStats"]=new JObject();
        lines.Add(row);
        var abilities=(JObject)catalog["abilities"];
        abilities[WinterQueenFreeze]=new JObject{["id"]=WinterQueenFreeze,["name"]="Ледяная хватка",
            ["description"]="Замораживает до трёх ближайших противников на 3 секунды. Летающие цели падают на землю.",
            ["kind"]="winter_queen_cast",["cd"]=QueenFreezeCooldown,["range"]=QueenFreezeRange};
        abilities[WinterQueenStorm]=new JObject{["id"]=WinterQueenStorm,["name"]="Буря Ледяной королевы",
            ["description"]="Следует за Джайной 5 секунд и наносит ближайшим противникам по 40 магического урона в секунду.",
            ["kind"]="winter_queen_cast",["cd"]=QueenStormCooldown,["area"]=WinterStormRadius};
        return catalog;
    }

    bool WinterCastleHit(int team,float damage){
        if(team!=WinterBossTeam||!EternalWinter||!ExpertTrial||WinterQueenFelled)return false;
        if(!WinterCastleFrozen&&damage<CastleHp[team])return false;
        if(!WinterCastleFrozen){
            WinterCastleFrozen=WinterAwakeningPending=true;
            CastleHp[team]=1;
            AdvanceWinterPhase();
        }
        return true;
    }

    internal bool BeginWinterAwakening(){
        if(!WinterAwakeningPending||winterAwakeningStarted)return false;
        int kind=Kind(WinterQueenLine);
        if(kind<0)throw new System.InvalidOperationException("Missing Winter Queen challenge unit.");
        winterAwakeningStarted=true;
        winterBoss=Spawn(WinterBossTeam,kind,CastlePosition(WinterBossTeam)+Vector2.left*13);
        winterBoss.onRoad=true;winterBoss.noBounty=winterBoss.noCorpse=true;
        winterBoss.pose="Stand";
        return true;
    }

    internal void FreezeWinterArmy(){
        if(!WinterAwakeningPending||winterBoss==null)return;
        foreach(var unit in Units)if(unit.team==0&&unit.hp>0){
            unit.effects.Add(new Effect{id=WinterJudgment,group=WinterJudgment,sourceTeam=WinterBossTeam,
                left=4,lifetime=4,root=true,ground=true,config=new JObject{["id"]=WinterJudgment,
                    ["name"]="Ледяной приговор",["kind"]="debuff",["disable"]=true,["undispellable"]=true}});
            unit.pose="Stand";
        }
        winterBoss.pose="Spell";winterBoss.attackSerial++;
    }

    internal bool ReleaseWinterQueen(){
        if(!WinterAwakeningPending||winterBoss==null)return false;
        foreach(var unit in Units)if(unit.team==0&&unit.hp>0){
            unit.hp=0;unit.deathProcessed=unit.noCorpse=true;unit.deadAge=0;
            unit.pandaSplit=false;unit.pendingSpell=null;unit.strikeLeft=unit.castLeft=-1;
            unit.effects.Clear();unit.pose="Death";Kills++;TeamKills[WinterBossTeam]++;
        }
        InvalidateArmyCounts035();
        Shots.RemoveAll(shot=>shot.team==0);
        winterFallen.Clear();
        winterBoss.pose="Walk";
        WinterAwakeningPending=false;
        return true;
    }

    void WinterQueenDeath(Unit unit){
        if(unit!=winterBoss||!WinterCastleFrozen)return;
        WinterCastleFrozen=false;WinterQueenFelled=true;
        winterStormTick=WinterStormDuration;
        PulseAt(CastlePosition(WinterBossTeam),10,Color.cyan,"Лёд треснул · замок уязвим",2);
    }

    bool WinterQueenTarget(Unit unit)=>unit.team==0&&unit.hp>0&&!Hidden(unit)&&!Invulnerable(unit)&&
        !CorruptedAirborne(unit)&&unit.spellResist<1&&Lines[unit.kind].unit!="YTct";

    bool TryWinterQueenCast(Unit unit){
        if(unit!=winterBoss||unit.team!=WinterBossTeam||Silenced(unit))return false;
        if(!unit.spellTimers.TryGetValue(WinterQueenFreeze,out float freeze)||freeze<=0){
            var targets=RadiusUnits035(unit.position,QueenFreezeRange).Where(target=>WinterQueenTarget(target)&&
                (target.position-unit.position).sqrMagnitude<=QueenFreezeRange*QueenFreezeRange&&
                !target.effects.Any(effect=>effect.id==WinterQueenFreeze&&effect.left>0))
                .OrderBy(target=>(target.position-unit.position).sqrMagnitude).ThenBy(target=>target.id).Take(WinterFreezeTargets).ToArray();
            if(targets.Length>0){
                var ice=new JObject{["id"]=WinterQueenFreeze,["name"]="Ледяная хватка",["kind"]="debuff",
                    ["duration"]=QueenFreezeDuration,["hero_duration"]=QueenFreezeDuration,
                    ["root"]=true,["disarm"]=true,["disable"]=true,["ground"]=true};
                foreach(var target in targets)ApplyEffectData(target,ice,WinterBossTeam);
                unit.spellTimers[WinterQueenFreeze]=QueenFreezeCooldown;
                PulseAt(unit.position,3,Color.cyan,"Ледяная хватка",1);
                return PlayWinterQueenCast(unit);
            }
        }
        if((!unit.spellTimers.TryGetValue(WinterQueenStorm,out float storm)||storm<=0)&&
            RadiusUnits035(unit.position,WinterStormRadius).Any(target=>WinterQueenTarget(target)&&
                (target.position-unit.position).sqrMagnitude<=WinterStormRadius*WinterStormRadius)){
            winterStormStart=Elapsed;winterStormTick=0;
            unit.spellTimers[WinterQueenStorm]=QueenStormCooldown;
            PulseAt(unit.position,WinterStormRadius,Color.cyan,"Ледяная буря",.85f);
            return PlayWinterQueenCast(unit);
        }
        return false;
    }

    bool PlayWinterQueenCast(Unit unit){
        unit.castPause=.7f;unit.pose=unit.finalCastPose="Spell";unit.attackSerial++;SpellsCast++;
        return true;
    }

    void TickWinterQueenStorm(){
        while(WinterStormActive&&Elapsed+.001f>=winterStormStart+winterStormTick+1){
            winterStormTick++;
            var center=winterBoss.position;
            using(var candidates=SimulationBuffers035.Take(Units))
                foreach(var target in candidates)if(WinterQueenTarget(target)&&
                    (target.position-center).sqrMagnitude<=WinterStormRadius*WinterStormRadius)
                    DamageUnit(target,WinterStormDamage,WinterBossTeam,killer:winterBoss);
        }
    }

    internal void DebugWinterCastleHit()=>ApplyCastleDamage(WinterBossTeam,CastleHealthLimit(WinterBossTeam)*3);

    internal void CheckWinterQueenSpells(System.Action<bool,string> check){
        int kind=Kind("h000");
        var queen=winterBoss;
        var point=queen.position;
        var targets=Enumerable.Range(1,4).Select(i=>Spawn(0,kind,point+Vector2.left*(i+2))).ToArray();
        var outside=Spawn(0,kind,point+Vector2.left*(WinterStormRadius+1));
        var ally=Spawn(1,kind,point+Vector2.right*4);
        // A direct cast verifies the selection and exact tick count without movement or attacks.
        queen.spellTimers.Clear();
        check(TryWinterQueenCast(queen)&&targets.Count(u=>u.effects.Any(e=>e.id==WinterQueenFreeze))==WinterFreezeTargets&&
            !targets[3].effects.Any(e=>e.id==WinterQueenFreeze)&&!ally.effects.Any(e=>e.id==WinterQueenFreeze),
            "Ice Grip freezes exactly three nearby opponents with no friendly fire");
        foreach(var u in targets)u.hp=u.maxHp=1000;
        float time=Elapsed,before=targets[0].hp,allyHp=ally.hp,outsideHp=outside.hp;
        check(TryWinterQueenCast(queen)&&WinterStormActive,"The storm becomes active around the queen");
        for(int i=1;i<=6;i++){Elapsed=time+i;TickWinterQueenStorm();}
        check(before-targets[0].hp==WinterStormDamage*WinterStormDuration&&ally.hp==allyHp&&outside.hp==outsideHp&&
            !WinterStormActive,"Five ticks of 40 damage reach only enemies inside the storm");
        Elapsed=time;winterStormStart=time;winterStormTick=0;
    }

    void PrepareEternalWinter(){
        if(ChallengeFamily!=WinterTrial)return;
        winterChain=new JObject{
            ["id"]=WinterChain,["name"]="Ледяные оковы",
            ["kind"]="debuff",["stack_group"]=WinterChain,["duration"]=WinterChainDuration,
            ["hero_duration"]=WinterChainDuration,["root"]=true,["disarm"]=true
        };
        nextWinterChain=WinterInterval;
    }

    public bool WinterAffects(Unit unit)=>EternalWinter&&unit.team!=WinterBossTeam;
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
        if(WinterAwakeningPending||!WinterPhaseTwo||!WinterAffects(fallen)||Hidden(fallen)||Invulnerable(fallen)||
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
