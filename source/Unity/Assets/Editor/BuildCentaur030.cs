using System;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
public static class BuildCentaur030 {
    public static void Register(){
        var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>("Assets/Resources/CentaurKhanates.asset");if(!pack)throw new Exception("Missing custom-race pack");
        var data=JObject.Parse(File.ReadAllText("Assets/Resources/pc-centaur.json"));pack.abilityIds=HumanMatch.Strings(data["raceAbilityIds"]);pack.abilityIcons=pack.abilityIds.Select(id=>BuildPC.Texture("Ability-"+id)).ToArray();
        if(pack.abilityIcons.Distinct().Count()!=pack.abilityIcons.Length)throw new Exception("Duplicated custom-race ability icons");
        EditorUtility.SetDirty(pack);
    }
}
