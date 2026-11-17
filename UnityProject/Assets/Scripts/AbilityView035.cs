using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
namespace CastleFight {
public sealed class AbilityView035 {
    public JObject catalog;public int generation=-1;
    public string[] ids=System.Array.Empty<string>();
    public readonly List<string> active=new List<string>(),passives=new List<string>();
    public JToken wind;public readonly HashSet<string> kinds=new HashSet<string>();
    static readonly ConditionalWeakTable<HumanMatch.Unit,AbilityView035> views=new ConditionalWeakTable<HumanMatch.Unit,AbilityView035>();
    public static AbilityView035 Get(HumanMatch.Unit unit,JObject catalog){
        var view=views.GetValue(unit,_=>new AbilityView035());
        bool same=ReferenceEquals(view.catalog,catalog)&&view.generation==SimulationConfig035.Generation&&view.ids.Length==unit.abilities.Count;
        if(same){int i=0;foreach(string id in unit.abilities)if(view.ids[i++]!=id){same=false;break;}}
        if(same)return view;
        if(!ReferenceEquals(view.catalog,catalog))SimulationConfig035.Get(catalog);view.catalog=catalog;view.ids=new string[unit.abilities.Count];unit.abilities.CopyTo(view.ids);view.active.Clear();view.passives.Clear();view.kinds.Clear();view.wind=null;
        foreach(string id in view.ids){var cfg=catalog[id];var typed=SimulationConfig035.Get(cfg);if(typed.kind!=null)view.kinds.Add(typed.kind);if(typed.kind=="self_passive"||cfg?["ranks"] is JArray)view.passives.Add(id);if(typed.autocast||id=="A01G"||id=="AUW1"||id=="AUW2"||id=="AUWB"||id=="AUMW")view.active.Add(id);if(view.wind==null&&typed.kind=="wind_walk")view.wind=cfg;}
        view.generation=SimulationConfig035.Generation;return view;
    }
}
}
