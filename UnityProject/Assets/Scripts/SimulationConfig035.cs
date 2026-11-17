using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
namespace CastleFight {
// JObject edits (including QA/mod overrides) invalidate the typed view. No
// cache is written into the serialized Line/Unit/Hut layouts.
public sealed class SimulationConfig035 {
    public readonly JObject source;
    public float collision,maxMana,manaRegen,hpRegen,minSpeed,maxSpeed,castPoint,mana;
    public bool hidden,flying,invulnerable,illusion,organic,sapper,followup,autocast;
    public string kind,code,types;
    public SimulationConfig035(JObject value){source=value;Read();if(source!=null)source.PropertyChanged+=Changed;}
    public static int Generation;
    void Changed(object sender,PropertyChangedEventArgs args){Read();Generation++;}
    void Read(){
        collision=HumanMatch.F(source,"collision",.5f);maxMana=HumanMatch.F(source,"max_mana");manaRegen=HumanMatch.F(source,"mana_regen");hpRegen=HumanMatch.F(source,"hp_regen");
        minSpeed=HumanMatch.F(source,"min_speed",5);maxSpeed=HumanMatch.F(source,"max_speed",13.333333f);castPoint=HumanMatch.F(source,"cast_point",.3f);mana=HumanMatch.F(source,"mana");
        hidden=(bool?)source?["hidden_controller"]??false;flying=(bool?)source?["flying"]??false;invulnerable=(bool?)source?["invulnerable"]??false;illusion=(bool?)source?["illusion"]??false;followup=(bool?)source?["followup_only"]??false;
        types=((string)source?["unit_types"]??"").ToLowerInvariant();organic=!types.Contains("mechanical");sapper=types.Contains("sapper");kind=(string)source?["kind"];code=(string)source?["code"];
        autocast=!followup&&(kind!=null&&(kind.StartsWith("north_",StringComparison.Ordinal)||ActiveKinds.Contains(kind))||(string)source?["id"]=="A01G");
    }
    static readonly HashSet<string> ActiveKinds=new HashSet<string>{"elven_heal","elven_solar","debuff","night_roots","elemental_barrage","corrupted_impale","undead_raise_unit","mech_mines","mech_grenade","twins_blast","chaos_roar","resurrect","nature_trees","taunt","regen","heal","warlock_blast","human_prayer","orc_nets","buff","orc_spell_steal","naga_purge",
        "nerub_fissure","nerub_brood","panda_heal","panda_stomp","panda_gust","panda_fountain","panda_gong","nerub_web","nerub_multiweb","nerub_cocoon","nerub_cleanse","nerub_command","nerub_acid","nerub_plague","panda_brew","panda_keg","panda_split","panda_fire","panda_haze"};
    static readonly SimulationConfig035 empty=new SimulationConfig035(null);
    public static SimulationConfig035 Get(JToken value){if(!(value is JObject obj))return empty;var view=obj.Annotation<SimulationConfig035>();if(view!=null&&ReferenceEquals(view.source,obj))return view;obj.RemoveAnnotations<SimulationConfig035>();view=new SimulationConfig035(obj);obj.AddAnnotation(view);return view;}
}
public sealed partial class HumanMatch {
    SimulationConfig035 UnitConfig035(int kind)=>SimulationConfig035.Get(Lines[kind].stats);
    SimulationConfig035 BuildingConfig035(int kind)=>SimulationConfig035.Get(Lines[kind].buildingStats);
}
}
