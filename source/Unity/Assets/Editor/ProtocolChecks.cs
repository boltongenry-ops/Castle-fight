using System;
using System.IO;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class ProtocolChecks {
    static void Check(bool value,string name) {if(!value)throw new Exception("Protocol test: "+name);}
    public static void Run() {
        var d=new SnapshotStream();
        var full=JObject.Parse("{type:'snapshot',seq:1,full:true,elapsed:0,units:[{id:1,hp:[100,100],state:{slow:0,old:2}}],huts:[],projectiles:[],coins:[],effects:[{k:'hit'}]}");
        var first=d.Decode(full);Check(d.Sequence==1 && first["effects"].HasValues,"full");
        var delta=JObject.Parse("{type:'snapshot_delta',seq:2,base:1,patch:{set:{elapsed:0.1},remove:[]},tables:{units:{update:[{id:1,patch:{set:{hp:[90,100],state:{$:{set:{slow:1},remove:['old']}}},remove:[]}},{id:2,new:{id:2,hp:[50,50]}}],remove:[]}},effects:[]}");
        var next=d.Decode(delta);Check((float)next["units"][0]["hp"][0]==90,"row patch");
        Check(next["units"][0]["state"]["old"]==null,"nested removal");
        Check((int)first["units"][0]["hp"][0]==100,"ownership");
        Check(!next["effects"].HasValues && ((JArray)next["units"]).Count==2,"transient effects and new entity");
        var gap=(JObject)delta.DeepClone();gap["seq"]=4;Check(d.Decode(gap)==null&&d.WaitingFull,"sequence gap");
        Check(d.Decode(delta)==null,"wait for resync");d.Decode(full);Check(!d.WaitingFull,"full recovery");
        var remove=JObject.Parse("{type:'snapshot_delta',seq:2,base:1,patch:{set:{},remove:[]},tables:{units:{update:[],remove:[1]}},effects:[]}");
        Check(!d.Decode(remove)["units"].HasValues,"entity removal");
        d.Decode(full);var replace=JObject.Parse("{type:'snapshot_delta',seq:2,base:1,patch:{set:{config:{'=':{fog:false}}},remove:[]},tables:{},effects:[]}");
        Check(!(bool)d.Decode(replace)["config"]["fog"],"dictionary replacement");
        var nativeCases=JArray.Parse(File.ReadAllText("Assets/Editor/protocol-fixtures.json"));
        d.Reset();
        foreach(JObject fixture in nativeCases) {
            var actual=d.Decode((JObject)fixture["wire"]);
            Check(JToken.DeepEquals(Canonical(actual),Canonical(fixture["expected"])),"Godot encoder fixture "+d.Sequence);
        }
        Debug.Log("CF_PROTOCOL_PASS full,nested,remove,new,events,ownership,gap,resync,replacement");
    }
    static JToken Canonical(JToken value) {
        if(value is JObject obj) {var sorted=new JObject();foreach(var p in System.Linq.Enumerable.OrderBy(obj.Properties(),p=>p.Name))sorted[p.Name]=Canonical(p.Value);return sorted;}
        if(value is JArray array) {var result=new JArray();foreach(var item in array)result.Add(Canonical(item));return result;}
        if(value.Type==JTokenType.Float||value.Type==JTokenType.Integer)return new JValue((double)value);
        return value.DeepClone();
    }
}
