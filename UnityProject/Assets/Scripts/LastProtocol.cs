using System;
using System.Collections.Generic;
using UnityEngine;

namespace CastleFight {
public sealed partial class HumanMatch {
    public const string ProtocolTrial="mythic_last_protocol";
    public const int ProtocolCastleHealth=14000;
    public const float ProtocolDeadline=7*60;
    public const float ProtocolBeamHalfWidth=6, ProtocolMineRadius=3.8f;
    public const float ProtocolWarning=3, ProtocolVulnerable=12, ProtocolCycle=27;
    const float ProtocolGateHealth=.4f;
    static readonly Color ProtocolAmber=new Color(1,.61f,.15f);
    static readonly Color ProtocolBlue=new Color(.25f,.78f,1);
    readonly Hut[] protocolStations=new Hut[2];
    readonly Hut[] protocolTowers=new Hut[2];
    readonly List<ProtocolMine> protocolMines=new List<ProtocolMine>(9);
    float protocolScanAt,protocolFireAt,protocolOpenUntil;
    bool protocolFired;
    float protocolMeleeAt,protocolRocketAt,protocolAttackUntil;
    int protocolActorKind;
    static readonly Weapon ProtocolRocket=new Weapon{
        type="missile",attack="pierce",missile=@"Abilities\Weapons\GyroCopter\GyroCopterImpact.mdl",
        targets="air",air=true,homing=true,speed=30,arc=.13f
    };
    public bool LastProtocol {get;private set;}
    public bool ProtocolMarch {get;private set;}
    public Vector2 ProtocolMarchPosition {get;private set;}
    public Vector2 ProtocolMarchPrevious {get;private set;}
    public int ProtocolMarchStrike {get;private set;}
    public bool ProtocolMarchAttacking=>ProtocolMarch&&Elapsed<protocolAttackUntil;
    public float ProtocolTimeLeft=>Mathf.Max(0,ProtocolDeadline-Elapsed);
    public Vector2 BattleCastlePosition(int team)=>team==1&&ProtocolMarch?ProtocolMarchPosition:CastlePosition(team);
    public bool ProtocolSecondStage {get;private set;}
    public bool ProtocolWarningActive=>!ProtocolMarch&&ProtocolSecondStage&&Elapsed>=protocolFireAt-ProtocolWarning;
    public bool ProtocolGatesOpen=>!ProtocolMarch&&Elapsed<protocolOpenUntil;
    public float ProtocolShotIn=>Mathf.Max(0,protocolFireAt-Elapsed);
    public float ProtocolOpenFor=>Mathf.Max(0,protocolOpenUntil-Elapsed);
    public int ProtocolStationsUp=>(ProtocolStationAlive(0)?1:0)+(ProtocolStationAlive(1)?1:0);
    public float ProtocolBeamX=>CastlePosition(1).x-9;
    public const float ProtocolBeamStart=55;
    public bool ProtocolStationAlive(int index)=>protocolStations[index]!=null&&protocolStations[index].hp>0;
    public static Vector2 ProtocolStationPoint(int index)=>index==0?new Vector2(86,8):new Vector2(128,-8);

    public sealed class ProtocolMine {
        public readonly Vector2 position;
        public float detonatesAt=-1;
        public bool spent;
        public ProtocolMine(Vector2 position){this.position=position;}
    }
    public IReadOnlyList<ProtocolMine> ProtocolMines=>protocolMines;

    void BeginLastProtocol(){
        LastProtocol=ChallengeFamily==ProtocolTrial;
        if(!LastProtocol)return;
        protocolActorKind=Kind("h09J");
        for(int i=0;i<protocolStations.Length;i++){
            var position=ProtocolStationPoint(i);
            protocolStations[i]=PlaceChallengeStructure("h09T",position,i==0?1900:2300);
            protocolTowers[i]=PlaceChallengeStructure("h05L",position+new Vector2(12,-position.y*2),1150);
        }
        foreach(float lane in new[]{-6f,0,6}){
            protocolMines.Add(new ProtocolMine(new Vector2(55+lane,lane)));
            protocolMines.Add(new ProtocolMine(new Vector2(69-lane,lane)));
            protocolMines.Add(new ProtocolMine(new Vector2(108+lane,lane)));
        }
    }

    Hut PlaceChallengeStructure(string raw,Vector2 position,float hp){
        int kind=Kind(raw);
        if(kind<0)throw new InvalidOperationException("Missing challenge structure "+raw);
        var hut=new Hut{id=nextId++,team=1,kind=kind,x=-1,y=-1,position=position,hp=hp,maxHp=hp,
            paid=true,started=true,mana=BuildingConfig035(kind).maxMana};
        Huts.Add(hut);
        return hut;
    }

    bool IsProtocolStation(Hut hut)=>LastProtocol&&hut!=null&&(hut==protocolStations[0]||hut==protocolStations[1]);
    bool IsProtocolDefense(Hut hut)=>IsProtocolStation(hut)||LastProtocol&&hut!=null&&(hut==protocolTowers[0]||hut==protocolTowers[1]);

    bool ProtocolBlocksPlot(int team,int x,int y){
        if(team!=1||!LastProtocol)return false;
        var point=Plot(team,x,y);
        for(int i=0;i<protocolStations.Length;i++)
            if(ProtocolStationAlive(i)&&(protocolStations[i].position-point).sqrMagnitude<Cell*Cell||
                protocolTowers[i].hp>0&&(protocolTowers[i].position-point).sqrMagnitude<Cell*Cell)return true;
        return false;
    }

    const float ProtocolReflection=.4f;
    float ProtocolStructureDamageScale(int defender,string attack)=>LastProtocol&&ExpertTrial&&defender==1&&attack=="siege"?.4f:1f;

    void ReflectProtocolHit(int sourceId,float dealt){
        if(!LastProtocol||!ExpertTrial||sourceId<=0||dealt<=0)return;
        float reflected=dealt*ProtocolReflection;
        var unit=FindUnit(sourceId);
        if(unit!=null){
            if(unit.team!=0||unit.hp<=0)return;
            if(DamageUnit(unit,reflected,1,execute:true)>0)
                PulseAt(unit.position,1.6f,ProtocolAmber,"Отражение",.45f);
            return;
        }
        var hut=FindHut(-sourceId-2);
        if(hut==null||hut.team!=0||!hut.paid||hut.hp<=0)return;
        ApplyHutDamage(hut,reflected,1);
        PulseAt(hut.position,1.6f,ProtocolAmber,"Отражение",.45f);
    }

    void ApplyCastleDamage(int team,float amount){
        if(QuackCastleHit(team,amount))return;
        if(WinterCastleHit(team,amount))return;
        if(ChoirCastleHit(team,amount))return;
        if(ExpertFinaleCastleHit(team,amount))return;
        if(team==1&&LastProtocol&&!ProtocolMarch){
            if(!ProtocolSecondStage){
                amount*=1-.35f*ProtocolStationsUp;
                CastleHp[team]=Mathf.Max(CastleHealthLimit(team)*ProtocolGateHealth,CastleHp[team]-amount);
                AdvanceProtocolPhase();
                return;
            }
            if(!protocolFired)return;
            amount*=ProtocolGatesOpen?1.35f:.55f;
        }
        if(team==1&&ProtocolMarch)amount*=.72f;
        CastleHp[team]=Mathf.Max(0,CastleHp[team]-amount);
        if(team==WinterBossTeam)AdvanceWinterPhase();
    }

    void AdvanceProtocolPhase(){
        if(ProtocolSecondStage||ProtocolStationsUp>0||CastleHp[1]>CastleHealthLimit(1)*ProtocolGateHealth)return;
        ProtocolSecondStage=true;
        protocolFireAt=Elapsed+ProtocolWarning;
        PulseAt(CastlePosition(1),10,ProtocolAmber,"ПЕРЕГРУЗКА",1.5f);
    }

    void TickLastProtocol(float dt=0){
        if(!LastProtocol)return;
        if(!ProtocolMarch&&Elapsed>=ProtocolDeadline)StartProtocolMarch();
        if(ProtocolMarch){
            TickProtocolMarch(dt);
            return;
        }
        AdvanceProtocolPhase();
        for(int i=0;i<protocolMines.Count;i++){
            var mine=protocolMines[i];
            if(mine.spent||mine.detonatesAt<0||Elapsed<mine.detonatesAt)continue;
            mine.spent=true;
            using(var snapshot=SimulationBuffers035.Take(Units))foreach(var unit in snapshot)
                if(ProtocolTarget(unit)&&!Flying(unit)&&(unit.position-mine.position).sqrMagnitude<=ProtocolMineRadius*ProtocolMineRadius)
                    DamageUnit(unit,45+Mathf.Min(55,unit.maxHp*.12f),1,"siege",false);
            PulseAt(mine.position,ProtocolMineRadius,ProtocolAmber,"",.55f,"cleave");
        }
        if(Elapsed>=protocolScanAt){
            protocolScanAt=Elapsed+.15f;
            for(int i=0;i<protocolMines.Count;i++){
                var mine=protocolMines[i];
                if(mine.spent||mine.detonatesAt>=0)continue;
                foreach(var unit in Units){
                    if(!ProtocolTarget(unit)||Flying(unit)||(unit.position-mine.position).sqrMagnitude>2.6f*2.6f)continue;
                    mine.detonatesAt=Elapsed+.38f;
                    break;
                }
            }
        }
        if(!ProtocolSecondStage||Elapsed<protocolFireAt)return;
        protocolFired=true;
        protocolFireAt=Elapsed+ProtocolCycle;
        protocolOpenUntil=Elapsed+ProtocolVulnerable;
        using(var snapshot=SimulationBuffers035.Take(Units))foreach(var unit in snapshot)
            if(ProtocolTarget(unit)&&unit.position.x>=ProtocolBeamStart&&unit.position.x<=ProtocolBeamX&&
                Mathf.Abs(unit.position.y)<=ProtocolBeamHalfWidth)
                DamageUnit(unit,100+Mathf.Min(120,unit.maxHp*.24f),1,"siege",false);
        PulseAt(CastlePosition(1),8,ProtocolBlue,"ВОРОТА ОТКРЫТЫ",1.2f);
    }

    bool ProtocolTarget(Unit unit)=>unit.team==0&&unit.hp>0&&!Invulnerable(unit)&&!Hidden(unit)&&
        Lines[unit.kind].category!="visual"&&Lines[unit.kind].unit!="YTct";

    void StartProtocolMarch(){
        ProtocolMarch=true;
        ProtocolMarchPosition=ProtocolMarchPrevious=CastlePosition(1);
        RestoreCastleHealth(1,1600);
        foreach(var mine in protocolMines)mine.spent=true;
        PulseAt(ProtocolMarchPosition,11,ProtocolAmber,"ТЕРМИНАТОР ВЫХОДИТ НА МАРШ",2.5f);
    }

    internal void CompleteProtocolLaunch(){
        if(!ProtocolMarch)return;
        var launched=new Vector2(ProtocolMarchPosition.x-5,0);
        ProtocolMarchPosition=ProtocolMarchPrevious=launched;
    }

    void TickProtocolMarch(float dt){
        var castle=CastlePosition(0);
        var stop=new Vector2(castle.x+CastleCollision+4,0);
        var pos=ProtocolMarchPosition;
        ProtocolMarchPrevious=pos;
        var next=Mathf.Abs(pos.y)>.01f?new Vector2(pos.x,0):stop;
        ProtocolMarchPosition=Vector2.MoveTowards(pos,next,5.2f*dt);
        pos=ProtocolMarchPosition;
        if(Elapsed>=protocolMeleeAt){
            protocolMeleeAt=Elapsed+.35f;
            var center=pos+Vector2.left*5;
            bool siege=pos.x<=stop.x+.05f;
            bool hit=false;
            using(var snapshot=SimulationBuffers035.Take(Units))foreach(var unit in snapshot){
                if(!ProtocolTarget(unit)||Flying(unit)||(unit.position-center).sqrMagnitude>8*8)continue;
                DamageUnit(unit,220,1,"siege",false);
                hit=true;
            }
            using(var snapshot=SimulationBuffers035.Take(Huts))foreach(var hut in snapshot){
                if(hut.team!=0||!hut.paid||hut.hp<=0||(hut.position-center).sqrMagnitude>8*8)continue;
                DamageHut(hut,170,"siege",1);
                hit=true;
            }
            if(siege){
                DamageCastle(0,340,"siege");
                if(CastleHp[0]<=0){Winner=1;return;}
            }
            if(hit||siege){
                protocolMeleeAt=Elapsed+1.55f;
                ProtocolMarchStrike++;
                protocolAttackUntil=Elapsed+.55f;
                PulseAt(center,8,ProtocolAmber,"",.5f,"cleave");
            }
        }
        if(Elapsed<protocolRocketAt||protocolActorKind<0)return;
        protocolRocketAt=Elapsed+.35f;
        Unit first=null,second=null;
        float closest=32*32,nextClosest=closest;
        foreach(var unit in Units){
            if(!ProtocolTarget(unit)||!Flying(unit))continue;
            float d=(unit.position-pos).sqrMagnitude;
            if(d<closest){second=first;nextClosest=closest;first=unit;closest=d;}
            else if(d<nextClosest){second=unit;nextClosest=d;}
        }
        if(first==null)return;
        protocolRocketAt=Elapsed+1.85f;
        FireProtocolRocket(first,pos);
        if(second!=null)FireProtocolRocket(second,pos);
    }

    void FireProtocolRocket(Unit target,Vector2 from){
        var shot=AddShot(1,protocolActorKind,-700,target.id,from,target.position,175,ProtocolRocket,0,3);
        shot.startHeight=6;
    }
}
}
