using System;
using UnityEngine;

namespace CastleFight {
public sealed partial class HumanMatch {
    public const string QuackTrial="mythic_quackalypse";
    internal const string QuackKingBuilding="pB13";
    public const int QuackCastleHealth=14000;
    internal const int QuackRainLimit=30;
    internal const float QuackWaveInterval=180;
    const float QuackRainWarning=3, QuackBellShield=105;
    const float QuackInfiltrationChance=.5f, QuackBackfireDelay=.72f;
    const float QuackBackfireRadius=1000f/30;
    System.Random quackInfiltrationRandom;
    Unit quackBackfireFrog;
    Vector2 quackBackfirePoint;
    float quackBackfireAt;
    static readonly string[] QuackBellRoster={"pB12","pB12","pB14","pB05","pB03"};
    static readonly Vector2[] QuackPools={
        new Vector2(84,-14),new Vector2(46,13),new Vector2(-12,-16),new Vector2(-77,15),new Vector2(-131,-15)
    };
    internal static int QuackPoolCount=>QuackPools.Length;
    internal static Vector2 QuackPoolPoint(int index)=>QuackPools[index];
    bool quackActive;
    int quackStage,quackRainKind=-1,quackNextPool,quackInfiltratorKind=-1;
    int[] quackBellKinds;
    int quackRainLeft;
    float quackBellAt,quackRainAt,quackNextHatch;
    public bool QuackActive=>quackActive;
    bool QuackOpening=>quackActive&&quackStage==0;
    public bool QuackRitualPending=>quackActive&&quackStage==1;
    public bool QuackDownpour=>quackActive&&quackStage==2;
    public float QuackNextWaveIn=>Mathf.Max(0,(QuackDownpour?quackRainAt:quackBellAt)-Elapsed);
    internal int QuackBellCount {get;private set;}
    public int QuackInfiltrations {get;private set;}
    public int QuackRescueBackfires {get;private set;}

    void BeginQuackTrial(){
        quackActive=ChallengeFamily==QuackTrial&&teamBaseBuildings[1].Contains("pB31");
        if(!quackActive)return;
        quackStage=0;
        quackBellAt=Elapsed+QuackWaveInterval;
        quackRainKind=Kind(ExpertTrial?QuackKingBuilding:"pB06");
        quackBellKinds=Array.ConvertAll(QuackBellRoster,Kind);
        LegendaryLimit[1]=2;
        quackInfiltratorKind=Kind("pB31");
        quackInfiltrationRandom=new System.Random(Guid.NewGuid().GetHashCode());
    }

    float QuackBotPriority(int kind,Hut replacing,int copies){
        if(!QuackOpening)return 0;
        switch(Lines[kind].building){
            case "pB31": return copies==0&&botAir<.82f?botProducers==0?22:14:0;
            case "pB32": return copies==0?botProducers==0&&botAir<.35f?10:20:
                botAir>.35f&&botAirCoverage<.66f&&copies<3?12:0;
            case "pB05": return copies==0&&botProducers>=2?13:0;
            case "pB18": return botArmy>=8&&copies<=2?12:0;
            case "pB10": return copies<2?(botAir>.38f?15:botDanger>3?10:0)/(copies+1):0;
            case "pB12": return botProducers>=2&&botAir<.67f?12:0;
            case "pB14": return (copies<3&&botAir>.28f?17:8);
            case "pB03": return copies==0&&botProducers>=3&&botAir<.55f?12:0;
            case "pB15": return botAir<.50f&&botVisible.Count>=4?14:0;
            case "pB07": return copies==0&&botProducers>=3&&botAir<.67f?10:0;
            case "pB16": return botProducers>=4&&botAir<.65f?12:0;
            case "pB08": return copies==0&&botAir<.48f&&botVisible.Count>=5?10:0;
            case "pB09": return copies==0&&botProducers>=4?18:0;
            case "pB13": return botProducers>=4&&botAir<.70f&&replacing!=null?16:0;
            case "pB04": return copies==0&&botProducers>=3&&botAir>.25f?9:0;
            case "pB17": return botAir>.38f?13:0;
            default: return 0;
        }
    }

    Unit QuackFocusMarked(Unit attacker,Unit current){
        if(!QuackOpening||attacker.team!=1||!attacker.abilities.Contains("PGSh")
            ||PepeShield(attacker)!=null||current!=null&&PepeHasMark(current,1))return current;
        float best=5.5f*5.5f;
        foreach(var candidate in RadiusUnits035(attacker.position,5.5f)){
            if(candidate.team!=0||candidate.hp<=0||!PepeHasMark(candidate,1)||OnForcedRetreat(candidate))continue;
            var weapon=WeaponFor(attacker,candidate);
            if(weapon==null)continue;
            float d=(candidate.position-attacker.position).sqrMagnitude;
            if(d>best||d>(weapon.range+1)*(weapon.range+1))continue;
            best=d;
            current=candidate;
        }
        return current;
    }

    bool TryQuackInfiltration(Hut producer){
        if(!QuackOpening||producer.team!=0||quackInfiltratorKind<0||
           ArmyCount(1)>=ArmyLimit||quackInfiltrationRandom.NextDouble()>=QuackInfiltrationChance)return false;
        QuackInfiltrations++;
        var point=producer.position+Vector2.up*(producer.position.y>0?-2.4f:2.4f);
        var unit=Spawn(1,quackInfiltratorKind,point);
        unit.onRoad=true;
        var weapon=WeaponFor(unit,null,true);
        if(weapon!=null)BeginAttack(unit,weapon,HutTarget(producer));
        PulseAt(point,3.2f,PepeRules.ReedGreen,"Подмена!",1.1f,"pepe:PGSh");
        return true;
    }

    internal Vector2 QuackRescueAim(){
        Unit front=null;
        foreach(var unit in Units)
            if(unit.team==0&&unit.hp>0&&(front==null||unit.position.x>front.position.x))front=unit;
        return front?.position??Builders[0].position;
    }

    void BeginQuackRescueBackfire(Vector2 requested){
        Unit target=null;
        float distance=float.PositiveInfinity;
        foreach(var unit in Units){
            if(unit.team!=0||unit.hp<=0)continue;
            float squared=(unit.position-requested).sqrMagnitude;
            if(squared>=distance)continue;
            target=unit;
            distance=squared;
        }
        quackBackfirePoint=target?.position??Builders[0].position;
        quackBackfireAt=Elapsed+QuackBackfireDelay;
        QuackRescueBackfires++;
        if(quackInfiltratorKind>=0){
            quackBackfireFrog=Spawn(1,quackInfiltratorKind,quackBackfirePoint);
            quackBackfireFrog.noBounty=quackBackfireFrog.noCorpse=true;
            quackBackfireFrog.stun=QuackBackfireDelay+.1f;
            quackBackfireFrog.abilities.Add("Avul");
        }
        PulseAt(quackBackfirePoint,QuackBackfireRadius,PepeRules.ReedGreen,"КВАА!",QuackBackfireDelay);
    }

    void TickQuackRescueBackfire(){
        if(quackBackfireAt<=0||Elapsed<quackBackfireAt)return;
        quackBackfireAt=0;
        var frog=quackBackfireFrog;
        quackBackfireFrog=null;
        using(var victims=SimulationBuffers035.Take(Units)){
            foreach(var victim in victims)
                if(victim.team==0&&victim.hp>0&&
                   (victim.position-quackBackfirePoint).sqrMagnitude<=QuackBackfireRadius*QuackBackfireRadius)
                    DamageUnit(victim,victim.maxHp+1,1,"chaos",true,true,frog);
        }
        if(frog!=null){frog.hp=0;frog.deathProcessed=true;frog.pose="Death";InvalidateArmyCounts035();}
        PulseAt(quackBackfirePoint,QuackBackfireRadius,PepeRules.ReedGreen,"Обратный взрыв",1.267f,"wisp");
    }

    void StrengthenQuackUnit(Unit unit){
        if(!quackActive||unit.team!=1||!teamBuildings[1].Contains(Lines[unit.kind].building)||!PepeMainFighter(unit))return;
        unit.maxHp*=1.2f;
        unit.hp*=1.2f;
    }

    void RelayQuackShield(Unit source,Effect shield){
        if(source.hp<=0||source.team!=1||shield.power<2||shield.sourceTeam!=source.team)return;
        Unit destination=null;
        float best=11*11;
        foreach(var unit in RadiusUnits035(source.position,11)){
            if(unit==source||unit.team!=1||!teamBuildings[1].Contains(Lines[unit.kind].building)
                ||!PepeMainFighter(unit)||PepeShield(unit)!=null)continue;
            float distance=(unit.position-source.position).sqrMagnitude;
            if(distance>best||distance==best&&destination!=null&&unit.id>destination.id)continue;
            best=distance;destination=unit;
        }
        if(destination!=null)PepeGiveShield(destination,shield.power*.5f,3);
    }

    bool QuackCastleHit(int team,float amount){
        if(!quackActive||team!=1||quackStage==2)return false;
        if(QuackRitualPending)return true;
        if(CastleHp[1]>amount)return false;
        CastleHp[1]=0;
        quackStage=1;
        PulseAt(CastlePosition(1),11,PepeRules.ReedGreen,"",2.3f,"wisp");
        return true;
    }

    internal bool OpenQuackDownpour(){
        if(!QuackRitualPending||Finished||CastleHp[0]<=0)return false;
        quackStage=2;
        CastleHp[1]=CastleHealthLimit(1);
        Units.RemoveAll(unit=>unit.team==0);
        Corpses.RemoveAll(corpse=>corpse.unit.team==0);
        Shots.Clear();
        InvalidateArmyCounts035();
        RebuildIdCache033();
        quackNextPool=0;
        quackRainLeft=QuackPools.Length;
        quackNextHatch=Elapsed+.8f;
        quackRainAt=Elapsed+QuackWaveInterval;
        foreach(var pool in QuackPools)QuackShowPool(pool,3.5f);
        return true;
    }

    void QuackShowPool(Vector2 point,float seconds){
        PulseAt(point,4.8f,PepeRules.ReedGreen,"",seconds);
    }

    internal int QuackRainAlive(){
        int count=0;
        foreach(var unit in Units)
            if(unit.hp>0&&unit.team==1&&unit.kind==quackRainKind&&unit.noCorpse&&unit.noBounty)count++;
        return count;
    }

    Vector2 QuackSpawnPoint(Vector2 anchor,int slot){
        var offset=new Vector2((slot%3-1)*2.4f,(slot/3-.5f)*2.8f);
        var point=anchor+offset;
        foreach(var hut in Huts){
            if(hut.hp<=0||(hut.position-point).sqrMagnitude>16)continue;
            return new Vector2(point.x,HumanMatch.CastlePosition(1).y);
        }
        return point;
    }

    void TickQuackTrial(){
        if(!quackActive||QuackRitualPending)return;
        if(!QuackDownpour){
            if(Elapsed<quackBellAt)return;
            quackBellAt+=QuackWaveInterval;
            QuackBellCount++;
            var point=CastlePosition(1)+new Vector2(-22,0);
            PulseAt(point,9,PepeRules.ReedGreen,"Болотный колокол",2,"pepe:PBll");
            for(int i=0;i<quackBellKinds.Length;i++){
                if(ArmyCount(1)>=ArmyLimit)break;
                if(quackBellKinds[i]<0)continue;
                var unit=Spawn(1,quackBellKinds[i],QuackSpawnPoint(point,i));
                if(i<2)PepeGiveShield(unit,QuackBellShield,9,"PBll");
            }
            return;
        }

        TickQuackRescueBackfire();
        if(quackRainKind<0)return;
        if(quackRainLeft==0&&Elapsed>=quackRainAt-QuackRainWarning){
            quackRainLeft=2;
            quackNextHatch=quackRainAt;
            quackRainAt+=QuackWaveInterval;
            for(int i=0;i<quackRainLeft;i++)QuackShowPool(QuackPools[(quackNextPool+i)%QuackPools.Length],QuackRainWarning);
        }
        if(quackRainLeft==0||Elapsed<quackNextHatch)return;
        quackNextHatch=Elapsed+.42f;
        HatchQuackPool();
    }

    void HatchQuackPool(){
        var pool=QuackPools[quackNextPool++%QuackPools.Length];
        quackRainLeft--;
        int space=Mathf.Min(ArmyLimit-ArmyCount(1),QuackRainLimit-QuackRainAlive());
        for(int i=0;i<Mathf.Min(6,space);i++){
            var unit=Spawn(1,quackRainKind,QuackSpawnPoint(pool,i));
            unit.noBounty=unit.noCorpse=true;
        }
        PulseAt(pool,5,PepeRules.ReedGreen,"",1.1f,"pepe:PTmp");
    }
}
}
