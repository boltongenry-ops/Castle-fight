using System;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class BuildUndead {
    const string PackPath="Assets/Resources/UndeadForces.asset";
    public static void Prepare() {
        AssetDatabase.Refresh();var data=JObject.Parse(File.ReadAllText("Assets/Resources/pc-undead.json"));var lines=(JArray)data["lines"];int n=lines.Count;
        var pack=ScriptableObject.CreateInstance<PcRacePack>();pack.catalog=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/pc-undead.json");
        pack.hdUnits=pack.hdBuildings=pack.hdBuilders=Array.Empty<GameObject>();pack.units=new GameObject[n*2];pack.buildings=new GameObject[n*2];pack.builders=new GameObject[2];
        for(int team=0;team<2;team++) {
            for(int i=0;i<n;i++){string unit=(string)lines[i]["unit"],raw=(string)lines[i]["building"];if(!string.IsNullOrEmpty(unit))pack.units[team*n+i]=BuildClassic.Model(unit,"u",team);pack.buildings[team*n+i]=BuildClassic.Model(raw,"b",team);}
            pack.builders[team]=BuildClassic.Model("h018","builder",team);
        }
        pack.buildingIcons=lines.Select(v=>BuildPC.Texture("Building-"+(string)v["building"])).ToArray();pack.builderIcon=BuildPC.Texture("UndeadBuilder");pack.buildIcon=BuildPC.Texture("UndeadBuild");
        pack.abilityIds=((JArray)data["raceAbilityIds"]).Select(v=>(string)v).ToArray();pack.abilityIcons=pack.abilityIds.Select(id=>BuildPC.Texture("Ability-"+id)).ToArray();
        var existing=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(existing){EditorUtility.CopySerialized(pack,existing);UnityEngine.Object.DestroyImmediate(pack);EditorUtility.SetDirty(existing);}else AssetDatabase.CreateAsset(pack,PackPath);
        PrepareEffects();AssetDatabase.SaveAssets();Validate();Debug.Log("CF_UNDEAD_PREPARE_PASS catalog=17 replacements=9 base=11 upgrades=6 units=20 classic=true");
    }
    static void PrepareEffects() {
        Directory.CreateDirectory("Assets/Resources/UndeadFx");Directory.CreateDirectory("Assets/Resources/UndeadFxMaterials");AssetDatabase.Refresh();
        var particles=JObject.Parse(File.ReadAllText("Assets/SourceAssets/undead-particles.json"));var meshKeys=JArray.Parse(File.ReadAllText("Assets/SourceAssets/undead-effects.json")).Select(v=>(string)v).ToArray();
        foreach(string key in meshKeys.Concat(particles.Properties().Select(p=>p.Name)).Distinct())for(int team=0;team<2;team++) {
            GameObject root;
            if(meshKeys.Contains(key)){var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedPC/"+key+"-t"+team+".prefab");if(!source)source=BuildClassic.Model(key,"fx",team);root=UnityEngine.Object.Instantiate(source);}
            else {root=new GameObject(key+"-"+team);var model=root.AddComponent<PcModel>();model.raw=key;model.animatedRoot=root;model.particleOnly=true;model.clips=Array.Empty<AnimationClip>();}
            if(particles[key] is JObject profile){var clips=(JObject)profile["clips"];var interval=(JArray)(clips["Stand"]??clips["Birth"]??clips.Properties().First().Value);BuildUndeadParticles.Add(root,profile,interval,key+"-"+team);}
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/UndeadFx/"+key+"-"+team+".prefab");UnityEngine.Object.DestroyImmediate(root);
        }
    }
    public static void RebuildEffectsWindows(){PrepareEffects();AssetDatabase.SaveAssets();Validate();BuildFullMap.RebuildRuntimeWindows();}
    public static void Validate() {
        var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(!pack||!pack.catalog)throw new Exception("Missing Undead pack");
        if(pack.Prefabs.Count()!=94||pack.buildingIcons.Length!=26||pack.buildingIcons.Any(v=>!v)||!pack.builderIcon||!pack.buildIcon||pack.abilityIcons.Any(v=>!v))throw new Exception("Incomplete Undead pack");
        foreach(var prefab in pack.Prefabs) {
            var model=prefab.GetComponent<PcModel>();if(!model||model.reforged||model.godotHeight!=0||!model.animatedRoot||model.clips.Length==0||model.clips.Any(v=>!v))throw new Exception("Invalid Undead model: "+prefab.name);
            foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.sharedMaterials.Any(v=>!v||!v.shader||!v.mainTexture))throw new Exception("Missing Undead material: "+prefab.name);
        }
        foreach(var prefab in pack.units.Where(v=>v))foreach(var pose in new[]{"Stand","Walk","Attack","Death"}){if(!prefab.GetComponent<PcModel>().clips.Any(v=>v.name.StartsWith(pose,StringComparison.OrdinalIgnoreCase)))throw new Exception("Missing Undead "+pose+": "+prefab.name);}
        foreach(string key in (JArray)JObject.Parse(File.ReadAllText("Assets/Resources/undead-fx-index.json"))["available"])for(int team=0;team<2;team++){var fx=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UndeadFx/"+key+"-"+team+".prefab");if(!fx||!fx.GetComponent<PcModel>()||fx.GetComponent<PcModel>().clips.Any(v=>!v))throw new Exception("Missing saved Undead effect: "+key);}
        Debug.Log("CF_UNDEAD_REFERENCES_PASS prefabs=94 textures=true animations=true");
    }
    public static void BuildWindows(){Prepare();BuildFullMap.RebuildRuntimeWindows();}
}
