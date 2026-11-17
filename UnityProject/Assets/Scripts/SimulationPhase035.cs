using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
namespace CastleFight {
// Opt-in QA instrumentation. No stopwatch/dictionary work in normal matches.
public static class SimulationPhase035 {
    public static bool Enabled;
    static long start;
    static readonly Dictionary<string,double> times=new Dictionary<string,double>();
    public static void Begin(){if(Enabled){times.Clear();start=Stopwatch.GetTimestamp();}}
    public static void Mark(string name){if(!Enabled)return;long now=Stopwatch.GetTimestamp();times[name]=(now-start)*1000.0/Stopwatch.Frequency;start=now;}
    public static JObject Result()=>JObject.FromObject(times);
}
}
