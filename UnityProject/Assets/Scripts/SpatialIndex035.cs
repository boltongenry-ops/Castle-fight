using System.Collections.Generic;
using UnityEngine;
using Entry=CastleFight.UnitIndex033.Entry;
namespace CastleFight {
public sealed class SpatialIndex035 {
    readonly Dictionary<int, Entry> members = new Dictionary<int, Entry>();
    readonly Dictionary<long, List<Entry>> buckets = new Dictionary<long, List<Entry>>();
    static long Key(int cx, int cy)=>((long)cx<<32)^(uint)cy;
    readonly float cell;
    readonly Stack<List<Entry>> recycled=new Stack<List<Entry>>();
    readonly List<Entry> nearby=new List<Entry>();
    readonly List<Entry> ordered=new List<Entry>();ulong[] marks=System.Array.Empty<ulong>();
    static readonly int[] bitIndex={0,1,48,2,57,49,28,3,61,58,50,42,38,29,17,4,62,55,59,36,53,51,43,22,45,39,33,30,24,18,12,5,63,47,56,27,60,41,37,16,54,35,52,21,44,32,23,11,46,26,40,15,34,20,31,10,25,14,19,9,13,8,7,6};
    public SpatialIndex035(float cellSize=12f){cell=cellSize;}
    int Cell(float v)=>Mathf.FloorToInt(v/cell);
    List<Entry> Rent(){return recycled.Count>0?recycled.Pop():new List<Entry>();}
    public List<Entry> WithinOrdered(Vector2 at,float radius){
        nearby.Clear();float sq=radius*radius;int words=(Count+63)/64;
        if(marks.Length<words)marks=new ulong[System.Math.Max(words,marks.Length*2)];else System.Array.Clear(marks,0,words);
        for(int x=Cell(at.x-radius);x<=Cell(at.x+radius);x++)for(int y=Cell(at.y-radius);y<=Cell(at.y+radius);y++)if(buckets.TryGetValue(Key(x,y),out var list))for(int i=0;i<list.Count;i++){var e=list[i];if((e.unit.position-at).sqrMagnitude<=sq)marks[e.order>>6]|=1UL<<(e.order&63);}
        for(int word=0;word<words;word++){ulong bits=marks[word];while(bits!=0){ulong low=bits&unchecked(0UL-bits);int bit=bitIndex[(low*0x03F79D71B4CB0A89UL)>>58];nearby.Add(ordered[(word<<6)+bit]);bits&=bits-1;}}
        return nearby;
    }
    public int Count;
    public void Rebuild(List<HumanMatch.Unit> units) {
        members.Clear();ordered.Clear(); foreach(var pair in buckets){pair.Value.Clear();recycled.Push(pair.Value);}buckets.Clear();
        for(int i=0;i<units.Count;i++) {
            var u=units[i];
            int cx=Cell(u.position.x), cy=Cell(u.position.y);
            var e=new Entry{unit=u,cx=cx,cy=cy,order=i};
            members[u.id]=e;ordered.Add(e);
            long k=Key(cx,cy);
            if(!buckets.TryGetValue(k,out var list)){list=Rent();buckets[k]=list;}
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
            if(old.Count==0){buckets.Remove(ok);recycled.Push(old);}
        }
        e.cx=ncx; e.cy=ncy;
        members[u.id]=e;
        long nk=Key(ncx,ncy);
        if(!buckets.TryGetValue(nk,out var nl)){nl=Rent();buckets[nk]=nl;}
        nl.Add(e);
    }
    public void Add(HumanMatch.Unit u,int order) {
        int cx=Cell(u.position.x), cy=Cell(u.position.y);
        var e=new Entry{unit=u,cx=cx,cy=cy,order=order};
        members[u.id]=e;ordered.Add(e);
        long k=Key(cx,cy);
        if(!buckets.TryGetValue(k,out var list)){list=Rent();buckets[k]=list;}
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
}
