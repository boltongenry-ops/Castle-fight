using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace CastleFight {
public sealed class SimulationState035 {
    public bool reference,auditGround;public int armyCountU=-1;public readonly int[] armyCounts=new int[2];public bool armyCountsValid;public object humanSchedule;
    public readonly List<AuraBatch035.Query> auraQueries=new List<AuraBatch035.Query>();
    public readonly SpatialIndex035 neighbors=new SpatialIndex035(4),targets=new SpatialIndex035(12);
    public readonly List<HumanMatch.Hut> links=new List<HumanMatch.Hut>();
    public int linkTick=-1,linkHutCount=-1;
    static readonly ConditionalWeakTable<HumanMatch,SimulationState035> states=new ConditionalWeakTable<HumanMatch,SimulationState035>();
    public static SimulationState035 Get(HumanMatch match)=>states.GetValue(match,_=>new SimulationState035());
}
public sealed partial class HumanMatch {
    void InvalidateArmyCounts035(){SimulationState035.Get(this).armyCountsValid=false;}
    int ArmyCount035(int team){
        var state=SimulationState035.Get(this);
        if(!Opt(this).buildingActive||state.reference){int n=0;foreach(var u in Units)if(u.team==team&&u.hp>0&&Lines[u.kind].unit!="n02E"&&!Hidden(u))n++;return n;}
        if(!state.armyCountsValid||state.armyCountU!=Units.Count){state.armyCounts[0]=state.armyCounts[1]=0;foreach(var u in Units)if(u.hp>0&&Lines[u.kind].unit!="n02E"&&!Hidden(u))state.armyCounts[u.team]++;state.armyCountU=Units.Count;state.armyCountsValid=true;}
        return state.armyCounts[team];
    }
    bool ElementalLinkNearby035(Hut hut){
        var state=SimulationState035.Get(this);float radius=F(Abilities["A0EL"]?["fields"],"aare",0)/30;
        if(Opt(this).buildingActive&&!state.reference&&state.linkTick==TickCount&&state.linkHutCount==Huts.Count){
            // Keep references rather than baked positions/eligibility. An earlier
            // building can disable, destroy or capture a source during this phase.
            for(int i=0;i<state.links.Count;i++){var h=state.links[i];if(h.team==hut.team&&h.paid&&h.hp>0&&Lines[h.kind].building=="h062"&&BuildingDisabled(h)<=0&&(h.position-hut.position).sqrMagnitude<=radius*radius)return true;}
            return false;
        }
        foreach(var h in Huts)if(h.team==hut.team&&h.paid&&h.hp>0&&Lines[h.kind].building=="h062"&&BuildingDisabled(h)<=0&&(h.position-hut.position).sqrMagnitude<=radius*radius)return true;
        return false;
    }
    void RebuildLinks035(){
        var s=SimulationState035.Get(this);s.links.Clear();foreach(var h in Huts)if(Lines[h.kind].building=="h062")s.links.Add(h);s.linkTick=TickCount;s.linkHutCount=Huts.Count;
    }
}
}
