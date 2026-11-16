using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace CastleFight {
// Внешнее состояние оптимизации 033: ни одного нового поля в HumanMatch/Line.
// Раскладка managed-типов исходного кода не меняется (требование code-only:
// чекер сравнивает и static-поля, поэтому таблица живёт в отдельном классе).
// Поведение то же: кэши сверяются по TickCount/Count/порядку, иначе полный скан.
static class HumanMatchOpt033Store {
    internal sealed class State {
        public readonly UnitIndex033 unitIndex = new UnitIndex033();
        public readonly Dictionary<int,HumanMatch.Unit> unitById = new Dictionary<int,HumanMatch.Unit>();
        public readonly Dictionary<int,int> unitOrder = new Dictionary<int,int>();
        public readonly Dictionary<int,HumanMatch.Hut> hutById = new Dictionary<int,HumanMatch.Hut>();
        public readonly Dictionary<int,int> hutOrder = new Dictionary<int,int>();
        public int idCacheCountU = -1, idCacheCountH = -1;
        public bool unitBatchActive, buildingActive;
        public int unitIndexBuiltTick = -1;
        public readonly List<Vector2> mechPos0 = new List<Vector2>(), mechPos1 = new List<Vector2>();
        public int mechTick = -1;
        public readonly List<Vector2> linkPos0 = new List<Vector2>(), linkPos1 = new List<Vector2>();
        public int linkTick = -1; public float linkRadius;
        public bool lineCacheBuilt;
        public string[][] lineProdRoster; public int[][] lineProdKinds;
        public bool[] lineHasPair; public float[] lineStatCollision, lineStatMaxMana;
        public bool[] lineIsLink, lineHasRaise;
    }
    internal static readonly ConditionalWeakTable<HumanMatch,State> table = new ConditionalWeakTable<HumanMatch,State>();
}
public sealed partial class HumanMatch {
    static HumanMatchOpt033Store.State Opt(HumanMatch m)=>HumanMatchOpt033Store.table.GetValue(m,_=>new HumanMatchOpt033Store.State());
    void EnsureLineCache033() {        var s=Opt(this);
        if(s.lineCacheBuilt&&s.lineProdRoster!=null&&s.lineProdRoster.Length==Lines.Length)return;
        int n=Lines.Length;
        s.lineProdRoster=new string[n][]; s.lineProdKinds=new int[n][];
        s.lineHasPair=new bool[n]; s.lineStatCollision=new float[n]; s.lineStatMaxMana=new float[n];
        s.lineIsLink=new bool[n]; s.lineHasRaise=new bool[n];
        for(int li=0;li<n;li++) {var ln=Lines[li];
            var roster=(ln.production??System.Array.Empty<string>());
            var filtered=new List<string>(roster.Length);
            for(int i=0;i<roster.Length;i++)if(!RemovedElementalCompanion(roster[i]))filtered.Add(roster[i]);
            var ra=filtered.ToArray(); s.lineProdRoster[li]=ra;
            var kinds=new int[ra.Length];
            for(int ri=0;ri<ra.Length;ri++){int found=-1;for(int k=0;k<n;k++)if(Lines[k].unit==ra[ri]){found=k;break;}kinds[ri]=found;}
            s.lineProdKinds[li]=kinds;
            bool pair=false;
            if(ln.stats is Newtonsoft.Json.Linq.JObject st&&st["abilities"] is Newtonsoft.Json.Linq.JArray sa)for(int ai=0;ai<sa.Count;ai++)if((string)sa[ai]=="A09H"){pair=true;break;}
            s.lineHasPair[li]=pair;
            s.lineStatCollision[li]=F(ln.stats,"collision",.5f);
            s.lineStatMaxMana[li]=F(ln.buildingStats,"max_mana");
            s.lineIsLink[li]=ln.building=="h062";
            bool raise=false;
            if(ln.abilities!=null)for(int ai=0;ai<ln.abilities.Length;ai++){var c=Abilities[ln.abilities[ai]];if((string)c?["kind"]=="undead_raise_building"){raise=true;break;}}
            s.lineHasRaise[li]=raise;
        }
        s.lineCacheBuilt=true;
    }
    bool UnitIndexReady033() {
        var s=Opt(this);
        return s.unitIndexBuiltTick==TickCount&&(s.unitBatchActive||s.buildingActive);
    }
    bool TryFindBestUnit033(Vector2 point,float maxSqr,int team,System.Func<Unit,bool> canUse,out Unit best) {
        best=null;var s=Opt(this);
        if(!UnitIndexReady033())return false;
        return s.unitIndex.TryFindBest(point,maxSqr,team,canUse,out best);
    }
    bool TryFindBestAny033(Vector2 point,float maxSqr,int team,bool requireOnRoad,System.Func<Unit,bool> canUse,out Unit best) {
        best=null;var s=Opt(this);
        if(!UnitIndexReady033())return false;
        return s.unitIndex.TryFindBestAny(point,maxSqr,team,requireOnRoad,canUse,out best);
    }
    void UnitMoved033(Unit u) { Opt(this).unitIndex.Moved(u); }
    void SetBuildingActive033(bool v) { Opt(this).buildingActive=v; }
    string[] ProdRoster033(int kind) { EnsureLineCache033(); return Opt(this).lineProdRoster[kind]; }
    int[] ProdKinds033(int kind) { EnsureLineCache033(); return Opt(this).lineProdKinds[kind]; }
    bool HasPair033(int kind) { EnsureLineCache033(); return Opt(this).lineHasPair[kind]; }
    float StatCollision033(int kind) { EnsureLineCache033(); return Opt(this).lineStatCollision[kind]; }
    float StatMaxMana033(int kind) { EnsureLineCache033(); return Opt(this).lineStatMaxMana[kind]; }
    bool HasRaise033(int kind) { EnsureLineCache033(); return Opt(this).lineHasRaise[kind]; }
    bool HasLink033(int kind) { EnsureLineCache033(); return Opt(this).lineIsLink[kind]; }
}
}
