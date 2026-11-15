using System;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
public static class BuildPlayerPolish {
    // Remove the last two helper rows without regenerating any race models.
    public static void BuildWindows(){
        AssetDatabase.Refresh();var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>("Assets/Resources/ElementalsUniverse.asset");
        int old=pack.buildingIcons.Length,count=((JArray)JObject.Parse(File.ReadAllText("Assets/Resources/pc-elemental.json"))["lines"]).Count;
        if(old==24&&count==22){
            foreach(var model in pack.units.Concat(pack.buildings).Where(p=>p&&HumanMatch.RemovedElementalCompanion(p.GetComponent<PcModel>().raw)))Debug.Log("CF_REMOVED_PRIMORDIAL_MODEL "+model.name);
            pack.units=Enumerable.Range(0,2).SelectMany(team=>pack.units.Skip(team*old).Take(count)).ToArray();
            pack.buildings=Enumerable.Range(0,2).SelectMany(team=>pack.buildings.Skip(team*old).Take(count)).ToArray();
            pack.buildingIcons=pack.buildingIcons.Take(count).ToArray();EditorUtility.SetDirty(pack);AssetDatabase.SaveAssets();
        }else if(old!=count)throw new Exception("Unexpected Elemental catalog layout: "+old+" / "+count);
        if(pack.Prefabs.Any(p=>HumanMatch.RemovedElementalCompanion(p.GetComponent<PcModel>().raw)))throw new Exception("Removed primordial model still referenced");
        BuildElemental.Validate();Debug.Log("CF_PLAYER_POLISH_PACK_PASS rows=22 models="+pack.Prefabs.Count());
        // Catalog and serialized pack changed: a full player-data build is required.
        BuildFullMap.RebuildRuntimeWindows();
    }
}
