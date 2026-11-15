// Shared round draw: Godot ultimate_loadout.gd, with a PC catalog adapter.
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace CastleFight {
public sealed class UltimateLoadout {
    public const string Race="Ultimate Builder";
    readonly JArray slots,available;
    readonly Func<int,int> choose;
    public UltimateLoadout(JArray sourceSlots,Func<int,int> randomIndex){
        if(sourceSlots==null||sourceSlots.Count==0)throw new ArgumentException("Missing source loadout slots");
        slots=(JArray)sourceSlots.DeepClone();available=(JArray)sourceSlots.DeepClone();choose=randomIndex??throw new ArgumentNullException(nameof(randomIndex));
        foreach(var slot in slots){var groups=slot["groups"] as JArray;if(groups==null||groups.Count==0||groups.Any(g=>!(g["buildings"] is JArray b)||b.Count==0||b.Any(v=>v.Type!=JTokenType.String)))throw new ArgumentException("Invalid source slot group");}
    }
    int Index(int count){int result=choose(count);if(result<0||result>=count)throw new InvalidOperationException("Random index outside source pool");return result;}
    public string[] Draw(){
        if(available.Any(slot=>((JArray)slot["groups"]).Count==0))throw new InvalidOperationException("Loadout draw exhausted; create a new draw for the next round");
        var result=new string[available.Count];
        for(int i=0;i<available.Count;i++){
            var groups=(JArray)available[i]["groups"];int groupIndex=Index(groups.Count);var choices=(JArray)groups[groupIndex]["buildings"];
            result[i]=(string)choices[Index(choices.Count)];groups.RemoveAt(groupIndex);
        }
        return result;
    }
    public bool Valid(JToken ids){
        if(!(ids is JArray array)||array.Count!=slots.Count)return false;
        for(int i=0;i<array.Count;i++)if(array[i].Type!=JTokenType.String||!((JArray)slots[i]["groups"]).Any(g=>((JArray)g["buildings"]).Any(raw=>(string)raw==(string)array[i])))return false;
        return true;
    }
    public JObject Playable(JObject canonical,string[] ids){
        if(!Valid(new JArray(ids)))throw new ArgumentException("Invalid Ultimate loadout");
        var result=(JObject)canonical.DeepClone();var lines=(JArray)result["lines"];var byRaw=lines.OfType<JObject>().ToDictionary(v=>(string)v["building"]);var allowed=new System.Collections.Generic.HashSet<string>();
        Action<string> add=null;add=raw=>{if(!allowed.Add(raw))return;if(!byRaw.TryGetValue(raw,out var row))throw new InvalidOperationException("Missing native Ultimate dependency "+raw);foreach(string child in (JArray)row["upgrades"])add(child);};
        foreach(string raw in ids)add(raw);
        foreach(JObject row in lines){row["base"]=false;row["allowed"]=allowed.Contains((string)row["building"]);row["ultimateSlot"]=-1;}
        var order=new JArray();for(int i=0;i<ids.Length;i++){var row=byRaw[ids[i]];row["base"]=true;row["home_hotkey"]=row["hotkey"]?.DeepClone();row["hotkey"]=(string)slots[i]["key"];row["ultimateSlot"]=i;order.Add(lines.IndexOf(row));}result["botOrder"]=order;result["ultimateLoadout"]=new JArray(ids);return result;
    }
    // Mirrors Godot GameDB lookup order, retaining each chosen building's home hotkey.
    public JObject Catalog(JObject baseRace,JArray ids,JArray allRaces){
        var result=(JObject)baseRace.DeepClone();var buildings=new JArray();result["buildings"]=buildings;if(!Valid(ids))return result;
        for(int i=0;i<ids.Count;i++){
            var entry=allRaces.SelectMany(r=>(JArray)r["buildings"]).OfType<JObject>().FirstOrDefault(b=>(string)b["rawcode"]==(string)ids[i]);
            if(entry==null){result["buildings"]=new JArray();return result;}
            var chosen=(JObject)entry.DeepClone();chosen["home_hotkey"]=chosen["hotkey"]?.DeepClone();chosen["hotkey"]=(string)slots[i]["key"];chosen["ultimate_slot"]=i;buildings.Add(chosen);
        }
        return result;
    }
}
}
