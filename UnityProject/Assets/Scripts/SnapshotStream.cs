using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CastleFight {
// Port of scripts/network/snapshot_stream.gd, PROTOCOL 1 / build 0.6.17.
// Effects are transient; consumers own the returned clone and can retain it safely.
public sealed class SnapshotStream {
    JObject baseline; public int Sequence {get;private set;}
    public bool WaitingFull {get;private set;}
    static readonly string[] Transient={"type","seq","base","full","effects","_changes"};
    public void Reset(){baseline=null;Sequence=0;WaitingFull=false;}
    static JObject State(JObject wire) {
        var result=(JObject)wire.DeepClone();foreach(string k in Transient)result.Remove(k);return result;
    }
    static void Merge(JObject target,JObject patch) {
        foreach(var key in (JArray)patch["remove"])target.Remove((string)key);
        foreach(var item in ((JObject)patch["set"]).Properties()) {
            if(item.Value is JObject box) {
                if(box["$"] is JObject nested) {
                    var child=target[item.Name] as JObject ?? new JObject();Merge(child,nested);target[item.Name]=child;
                }else target[item.Name]=box["="]?.DeepClone() ?? throw new FormatException("Invalid replacement");
            }else target[item.Name]=item.Value.DeepClone();
        }
    }
    public JObject Decode(JObject wire) {
        string type=(string)wire["type"];
        if(type=="snapshot") {baseline=State(wire);Sequence=(int?)wire["seq"]??0;WaitingFull=false;}
        else if(type=="snapshot_delta") {
            if(WaitingFull||baseline==null||(int?)wire["base"]!=Sequence||(int?)wire["seq"]!=Sequence+1) {WaitingFull=true;return null;}
            // Transactional update: reject a broken row without corrupting the last valid state.
            var next=(JObject)baseline.DeepClone();Merge(next,(JObject)wire["patch"]);
            foreach(var table in ((JObject)wire["tables"]).Properties()) {
                var rows=((JArray)next[table.Name]).OfType<JObject>().ToDictionary(r=>(long)r["id"]);
                foreach(var id in (JArray)table.Value["remove"])rows.Remove((long)id);
                foreach(JObject update in (JArray)table.Value["update"]) {
                    long id=(long)update["id"];
                    if(update["new"] is JObject value) rows[id]=(JObject)value.DeepClone();
                    else if(rows.TryGetValue(id,out var row))Merge(row,(JObject)update["patch"]);
                    else {WaitingFull=true;return null;}
                }
                next[table.Name]=new JArray(rows.Values);
            }
            baseline=next;Sequence=(int)wire["seq"];
        } else return null;
        var result=(JObject)baseline.DeepClone();result["effects"]=wire["effects"]?.DeepClone()??new JArray();return result;
    }
}
}
