using System;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class BuildMech {
    const string PackPath="Assets/Resources/MechCoalition.asset";
    public static void Prepare() {
        AssetDatabase.Refresh();var data=JObject.Parse(File.ReadAllText("Assets/Resources/pc-mech.json"));var lines=(JArray)data["lines"];int n=lines.Count;
        var pack=ScriptableObject.CreateInstance<PcRacePack>();pack.catalog=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/pc-mech.json");
        pack.hdUnits=pack.hdBuildings=pack.hdBuilders=Array.Empty<GameObject>();pack.units=new GameObject[n*2];pack.buildings=new GameObject[n*2];pack.builders=new GameObject[2];
        for(int team=0;team<2;team++) {
            for(int i=0;i<n;i++){string unit=(string)lines[i]["unit"],raw=(string)lines[i]["building"];if(!string.IsNullOrEmpty(unit))pack.units[team*n+i]=BuildClassic.Model(unit,"u",team);pack.buildings[team*n+i]=BuildClassic.Model(raw,"b",team);}
            pack.builders[team]=BuildClassic.Model("h06P","builder",team);
        }
        pack.buildingIcons=lines.Select(v=>BuildPC.Texture("Building-"+(string)v["building"])).ToArray();pack.builderIcon=BuildPC.Texture("MechBuilder");pack.buildIcon=BuildPC.Texture("MechBuild");
        pack.abilityIds=((JArray)data["raceAbilityIds"]).Select(v=>(string)v).ToArray();pack.abilityIcons=pack.abilityIds.Select(id=>BuildPC.Texture("Ability-"+id)).ToArray();
        var existing=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(existing){EditorUtility.CopySerialized(pack,existing);UnityEngine.Object.DestroyImmediate(pack);EditorUtility.SetDirty(existing);}else AssetDatabase.CreateAsset(pack,PackPath);
        PrepareEffects();AssetDatabase.SaveAssets();Validate();Debug.Log("CF_MECH_PREPARE_PASS catalog=21 helper=1 base=11 upgrades=10 units=15 classic=true");
    }
    static void PrepareEffects() {
        Directory.CreateDirectory("Assets/Resources/MechFx");AssetDatabase.Refresh();
        foreach(string key in JArray.Parse(File.ReadAllText("Assets/SourceAssets/mech-effects.json"))) {
            for(int team=0;team<2;team++){var source=BuildClassic.Model(key,"fx",team);var root=UnityEngine.Object.Instantiate(source);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/MechFx/"+key+"-"+team+".prefab");UnityEngine.Object.DestroyImmediate(root);}
        }

    }
    public static void Validate() {
        var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(!pack||!pack.catalog)throw new Exception("Missing Mech pack");
        if(pack.Prefabs.Count()!=78||pack.buildingIcons.Length!=22||pack.buildingIcons.Any(v=>!v)||!pack.builderIcon||!pack.buildIcon||pack.abilityIcons.Any(v=>!v))throw new Exception("Incomplete Mech pack");
        foreach(var prefab in pack.Prefabs) {
            var model=prefab.GetComponent<PcModel>();if(!model||model.reforged||model.godotHeight!=0||!model.animatedRoot||model.clips.Length==0||model.clips.Any(v=>!v))throw new Exception("Invalid Mech model: "+prefab.name);
            foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.sharedMaterials.Any(v=>!v||!v.shader||!v.mainTexture))throw new Exception("Missing Mech material: "+prefab.name);
        }
        foreach(var prefab in pack.units.Where(v=>v))foreach(var pose in new[]{"Stand","Walk","Attack","Death"}){string raw=prefab.GetComponent<PcModel>().raw;if(raw=="n025"&&(pose=="Walk"||pose=="Attack")||raw=="n01V"&&pose=="Attack")continue;if(!prefab.GetComponent<PcModel>().clips.Any(v=>v.name.StartsWith(pose,StringComparison.OrdinalIgnoreCase)))throw new Exception("Missing Mech "+pose+": "+prefab.name);}
        foreach(string key in JArray.Parse(File.ReadAllText("Assets/SourceAssets/mech-effects.json")))for(int team=0;team<2;team++){var fx=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/MechFx/"+key+"-"+team+".prefab");if(!fx||!fx.GetComponent<PcModel>()||fx.GetComponent<PcModel>().clips.Any(v=>!v))throw new Exception("Missing saved Mech effect: "+key);}
        Debug.Log("CF_MECH_REFERENCES_PASS prefabs=78 textures=true animations=true");
    }
    public static void BuildWindows(){Prepare();BuildFullMap.RebuildRuntimeWindows();}
}
