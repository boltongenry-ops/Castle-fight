using System.Collections.Generic;
using UnityEngine;
namespace CastleFight {
// Порт Godot unit_index.gd: CELL=12, buckets/members, ties по исходному order.
// Использование только внутри активного батча Step (BeginUnitBatch033/EndUnitBatch033).
// Вне батча все запросы идут полным сканом — поведение тестов не меняется.
public sealed class UnitIndex033 {
    public const float CELL = 12f;
    public struct Entry { public HumanMatch.Unit unit; public int cx, cy, order; }
    readonly Dictionary<int, Entry> members = new Dictionary<int, Entry>();
    readonly Dictionary<long, List<Entry>> buckets = new Dictionary<long, List<Entry>>();
    static long Key(int cx, int cy)=>((long)cx<<32)^(uint)cy;
    static int Cell(float v)=>Mathf.FloorToInt(v/CELL);
    public int Count;
    public void Rebuild(List<HumanMatch.Unit> units) {
        members.Clear(); buckets.Clear();
        for(int i=0;i<units.Count;i++) {
            var u=units[i];
            int cx=Cell(u.position.x), cy=Cell(u.position.y);
            var e=new Entry{unit=u,cx=cx,cy=cy,order=i};
            members[u.id]=e;
            long k=Key(cx,cy);
            if(!buckets.TryGetValue(k,out var list)){list=new List<Entry>();buckets[k]=list;}
            list.Add(e);
        }
        Count=units.Count;
    }
    public void Moved(HumanMatch.Unit u) {
        if(!members.TryGetValue(u.id,out var e)) return; // вне батча/свежие спавны уже добавлены через Add
        int ncx=Cell(u.position.x), ncy=Cell(u.position.y);
        if(ncx==e.cx&&ncy==e.cy) {
            // Позиция внутри ячейки обновляется по ссылке (unit живой), порядок тот же.
            // Entry хранит ссылку, ничего двигать не нужно.
            return;
        }
        long ok=Key(e.cx,e.cy);
        if(buckets.TryGetValue(ok,out var old)) {
            for(int i=0;i<old.Count;i++) if(old[i].unit==u||old[i].unit.id==u.id){old.RemoveAt(i);break;}
            if(old.Count==0)buckets.Remove(ok);
        }
        e.cx=ncx; e.cy=ncy;
        members[u.id]=e;
        long nk=Key(ncx,ncy);
        if(!buckets.TryGetValue(nk,out var nl)){nl=new List<Entry>();buckets[nk]=nl;}
        nl.Add(e);
    }
    public void Add(HumanMatch.Unit u,int order) {
        int cx=Cell(u.position.x), cy=Cell(u.position.y);
        var e=new Entry{unit=u,cx=cx,cy=cy,order=order};
        members[u.id]=e;
        long k=Key(cx,cy);
        if(!buckets.TryGetValue(k,out var list)){list=new List<Entry>();buckets[k]=list;}
        list.Add(e);
        Count++;
    }
    // Ближайший враг в радиусе с tie по исходному order.
    // Возвращает false только для сигнала fallback (слишком широкий радиус):
    // вызывающий делает полный скан. При true best может быть null (никого нет).
    public bool TryFindBestAny(Vector2 point,float maxSqr,int selfTeam,bool requireOnRoad,System.Func<HumanMatch.Unit,bool> canUse,out HumanMatch.Unit best) {
        best=null;
        float maxR=Mathf.Sqrt(maxSqr);
        int lowerX=Cell(point.x-maxR), upperX=Cell(point.x+maxR);
        int lowerY=Cell(point.y-maxR), upperY=Cell(point.y+maxR);
        long cells=(long)(upperX-lowerX+1)*(upperY-lowerY+1);
        // Как в Godot: глобальный радиус не сканирует тысячи пустых ячеек.
        if(cells>System.Math.Max(16,Count*2))return false;
        float bestSqr=float.MaxValue;int bestOrder=int.MaxValue;
        for(int cx=lowerX;cx<=upperX;cx++) for(int cy=lowerY;cy<=upperY;cy++) {
            if(!buckets.TryGetValue(Key(cx,cy),out var list))continue;
            for(int i=0;i<list.Count;i++) {
                var e=list[i];var v=e.unit;
                if(v==null||v.hp<=0||v.team==selfTeam)continue;
                if(requireOnRoad&&!v.onRoad)continue;
                var d=v.position-point;float s=d.x*d.x+d.y*d.y;
                if(s>maxSqr)continue;
                if(s>bestSqr)continue;
                if(s==bestSqr&&e.order>=bestOrder)continue;
                if(!canUse(v))continue;
                best=v;bestSqr=s;bestOrder=e.order;
            }
        }
        return true;
    }
    public bool TryFindBest(Vector2 point,float maxSqr,int selfTeam,System.Func<HumanMatch.Unit,bool> canUse,out HumanMatch.Unit best)
        =>TryFindBestAny(point,maxSqr,selfTeam,true,canUse,out best);
    public bool TryGetOrder(int unitId,out int order) {
        if(members.TryGetValue(unitId,out var e)){order=e.order;return true;}
        order=-1;return false;
    }
}
public sealed partial class HumanMatch {
    void RebuildIdCache033() {
        var s=Opt(this);
        s.unitById.Clear(); s.unitOrder.Clear(); s.hutById.Clear(); s.hutOrder.Clear();
        for(int i=0;i<Units.Count;i++) {var u=Units[i];if(!s.unitById.ContainsKey(u.id)){s.unitById[u.id]=u;s.unitOrder[u.id]=i;}}
        for(int i=0;i<Huts.Count;i++) {var h=Huts[i];if(!s.hutById.ContainsKey(h.id)){s.hutById[h.id]=h;s.hutOrder[h.id]=i;}}
        s.idCacheCountU=Units.Count; s.idCacheCountH=Huts.Count;
    }
    Unit FindUnitFast033(int target) {
        if(target<0)return null;
        var s=Opt(this);
        if(s.idCacheCountU==Units.Count&&s.unitById.TryGetValue(target,out var u)&&u!=null&&u.hp>0
            &&s.unitOrder.TryGetValue(target,out int ord)&&ord>=0&&ord<Units.Count&&ReferenceEquals(Units[ord],u))return u;
        for(int i=0;i<Units.Count;i++){var v=Units[i];if(v.id==target&&v.hp>0)return v;}
        return null;
    }
    Hut FindHutFast033(int target) {
        if(target>=-1)return null;
        int want=-target-2;
        var s=Opt(this);
        if(s.idCacheCountH==Huts.Count&&s.hutById.TryGetValue(want,out var h)&&h!=null&&h.hp>0&&h.paid
            &&s.hutOrder.TryGetValue(want,out int ord)&&ord>=0&&ord<Huts.Count&&ReferenceEquals(Huts[ord],h))return h;
        for(int i=0;i<Huts.Count;i++){var v=Huts[i];if(v.id==want&&v.hp>0&&v.paid)return v;}
        return null;
    }
    void BeginUnitBatch033() {
        var s=Opt(this);
        RebuildIdCache033();
        SimulationState035.Get(this).targets.Rebuild(Units); SimulationState035.Get(this).neighbors.Rebuild(Units);s.unitIndexBuiltTick=TickCount;
        s.unitBatchActive=true;
    }
    void EndUnitBatch033() { Opt(this).unitBatchActive=false; }
    void EnsureUnitIndex033() {
        var s=Opt(this);
        if(s.unitIndexBuiltTick==TickCount)return;
        SimulationState035.Get(this).targets.Rebuild(Units); s.unitIndexBuiltTick=TickCount;
    }
    void RebuildElementalLinkCache033() {
        RebuildLinks035();var s=Opt(this);
        s.linkPos0.Clear(); s.linkPos1.Clear();
        s.linkRadius=F(Abilities["A0EL"]?["fields"],"aare",0)/30;
        for(int i=0;i<Huts.Count;i++){var h=Huts[i];if(!h.paid||h.hp<=0)continue;EnsureLineCache033();if(!Opt(this).lineIsLink[h.kind])continue;if(BuildingDisabled(h)>0)continue;(h.team==0?s.linkPos0:s.linkPos1).Add(h.position);}
        s.linkTick=TickCount;
    }
    bool ElementalLinkNearby033(int team,Vector2 p) {
        var s=Opt(this);
        if(s.linkTick!=TickCount)return false;
        float r=s.linkRadius;if(r<=0)return false;float rsqr=r*r;
        var list=team==0?s.linkPos0:s.linkPos1;
        for(int i=0;i<list.Count;i++){var d=list[i]-p;if(d.x*d.x+d.y*d.y<=rsqr)return true;}
        return false;
    }
    void OnSpawnIndexed033(Unit u) {
        var s=Opt(this);
        if(s.unitIndexBuiltTick==TickCount){SimulationState035.Get(this).targets.Add(u,Units.Count-1);if(s.unitBatchActive)SimulationState035.Get(this).neighbors.Add(u,Units.Count-1);}
        if(!s.unitById.ContainsKey(u.id)){s.unitById[u.id]=u;s.unitOrder[u.id]=Units.Count-1;}
        s.idCacheCountU=Units.Count;
    }
}
}
