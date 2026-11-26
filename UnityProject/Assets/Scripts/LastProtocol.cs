using System;
using System.Collections.Generic;
using UnityEngine;

namespace CastleFight {
public sealed partial class HumanMatch {
    public const string ProtocolTrial="mythic_last_protocol";
    public const int ProtocolCastleHealth=14000;
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
    public bool LastProtocol {get;private set;}
    public bool ProtocolSecondStage {get;private set;}
    public bool ProtocolWarningActive=>ProtocolSecondStage&&Elapsed>=protocolFireAt-ProtocolWarning;
    public bool ProtocolGatesOpen=>Elapsed<protocolOpenUntil;
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
        LastProtocol=(string)ChallengeRules?["id"]==ProtocolTrial;
        if(!LastProtocol)return;
        for(int i=0;i<protocolStations.Length;i++){
            var position=ProtocolStationPoint(i);
            protocolStations[i]=PlaceProtocolStructure("h09T",position,i==0?1900:2300);
            protocolTowers[i]=PlaceProtocolStructure("h05L",position+new Vector2(12,-position.y*2),1150);
        }
        foreach(float lane in new[]{-6f,0,6}){
            protocolMines.Add(new ProtocolMine(new Vector2(55+lane,lane)));
            protocolMines.Add(new ProtocolMine(new Vector2(69-lane,lane)));
            protocolMines.Add(new ProtocolMine(new Vector2(108+lane,lane)));
        }
    }

    Hut PlaceProtocolStructure(string raw,Vector2 position,float hp){
        int kind=Kind(raw);
        if(kind<0)throw new InvalidOperationException("Missing Last Protocol structure "+raw);
        var hut=new Hut{id=nextId++,team=1,kind=kind,x=-1,y=-1,position=position,hp=hp,maxHp=hp,
            paid=true,started=true,mana=BuildingConfig035(kind).maxMana};
        Huts.Add(hut);
        return hut;
    }

    bool IsProtocolStation(Hut hut)=>LastProtocol&&hut!=null&&(hut==protocolStations[0]||hut==protocolStations[1]);
    bool IsProtocolDefense(Hut hut)=>IsProtocolStation(hut)||LastProtocol&&hut!=null&&(hut==protocolTowers[0]||hut==protocolTowers[1]);

    bool ProtocolBlocksPlot(int team,int x,int y){
        if(!LastProtocol||team!=1)return false;
        var point=Plot(team,x,y);
        for(int i=0;i<protocolStations.Length;i++)
            if(ProtocolStationAlive(i)&&(protocolStations[i].position-point).sqrMagnitude<Cell*Cell||
                protocolTowers[i].hp>0&&(protocolTowers[i].position-point).sqrMagnitude<Cell*Cell)return true;
        return false;
    }

    void ApplyCastleDamage(int team,float amount){
        if(team==1&&LastProtocol){
            if(!ProtocolSecondStage){
                amount*=1-.35f*ProtocolStationsUp;
                CastleHp[team]=Mathf.Max(CastleHealthLimit(team)*ProtocolGateHealth,CastleHp[team]-amount);
                AdvanceProtocolPhase();
                return;
            }
            if(!protocolFired)return;
            amount*=ProtocolGatesOpen?1.35f:.55f;
        }
        CastleHp[team]=Mathf.Max(0,CastleHp[team]-amount);
        if(team==WinterBossTeam)AdvanceWinterPhase();
    }

    void AdvanceProtocolPhase(){
        if(ProtocolSecondStage||ProtocolStationsUp>0||CastleHp[1]>CastleHealthLimit(1)*ProtocolGateHealth)return;
        ProtocolSecondStage=true;
        protocolFireAt=Elapsed+ProtocolWarning;
        PulseAt(CastlePosition(1),10,ProtocolAmber,"ПЕРЕГРУЗКА",1.5f);
    }

    void TickLastProtocol(){
        if(!LastProtocol)return;
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
}
}
