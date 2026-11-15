using System;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class BuildNight {
    const string PackPath="Assets/Resources/NightElves.asset";
    public static void Prepare() {
        AssetDatabase.Refresh();var data=JObject.Parse(File.ReadAllText("Assets/Resources/pc-night.json"));var lines=(JArray)data["lines"];int n=lines.Count;
        var pack=ScriptableObject.CreateInstance<PcRacePack>();pack.catalog=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/pc-night.json");
        pack.hdUnits=pack.hdBuildings=pack.hdBuilders=Array.Empty<GameObject>();pack.units=new GameObject[n*2];pack.buildings=new GameObject[n*2];pack.builders=new GameObject[2];
        for(int team=0;team<2;team++) {
            for(int i=0;i<n;i++){string unit=(string)lines[i]["unit"],raw=(string)lines[i]["building"];if(!string.IsNullOrEmpty(unit))pack.units[team*n+i]=BuildClassic.Model(unit,"u",team);pack.buildings[team*n+i]=BuildClassic.Model(raw,"b",team);}
            pack.builders[team]=BuildClassic.Model("h089","builder",team);
        }
        pack.buildingIcons=lines.Select(v=>BuildPC.Texture("Building-"+(string)v["building"])).ToArray();pack.builderIcon=BuildPC.Texture("NightBuilder");pack.buildIcon=BuildPC.Texture("NightBuild");
        pack.abilityIds=((JArray)data["raceAbilityIds"]).Select(v=>(string)v).ToArray();pack.abilityIcons=pack.abilityIds.Select(id=>BuildPC.Texture("Ability-"+id)).ToArray();
        var existing=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(existing){EditorUtility.CopySerialized(pack,existing);UnityEngine.Object.DestroyImmediate(pack);EditorUtility.SetDirty(existing);}else AssetDatabase.CreateAsset(pack,PackPath);
        PrepareEffects();PrepareActorParticles();AssetDatabase.SaveAssets();Validate();Debug.Log("CF_NIGHT_PREPARE_PASS catalog=16 base=11 upgrades=5 classic=true");
    }
    static void PrepareEffects() {
        Directory.CreateDirectory("Assets/Resources/NightFx");Directory.CreateDirectory("Assets/Resources/NightFxMaterials");AssetDatabase.Refresh();
        var particles=JObject.Parse(File.ReadAllText("Assets/SourceAssets/night-particles.json"));var meshKeys=JArray.Parse(File.ReadAllText("Assets/SourceAssets/night-effects.json")).Select(v=>(string)v).ToArray();
        foreach(string key in meshKeys.Concat(particles.Properties().Select(p=>p.Name).Where(k=>!k.StartsWith("actor_"))).Distinct())for(int team=0;team<2;team++) {
            GameObject root;
            if(meshKeys.Contains(key)){var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedPC/"+key+"-t"+team+".prefab");if(!source)source=BuildClassic.Model(key,"fx",team);root=UnityEngine.Object.Instantiate(source);}
            else {root=new GameObject(key+"-"+team);var model=root.AddComponent<PcModel>();model.raw=key;model.animatedRoot=root;model.particleOnly=true;model.clips=Array.Empty<AnimationClip>();}
            if(particles[key] is JObject profile){var clips=(JObject)profile["clips"];var interval=(JArray)(clips["Stand"]??clips["Birth"]??clips.Properties().First().Value);BuildNightParticles.Add(root,profile,interval,key+"-"+team);}
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/NightFx/"+key+"-"+team+".prefab");UnityEngine.Object.DestroyImmediate(root);
        }
    }
    static void PrepareActorParticles(){
        var profiles=JObject.Parse(File.ReadAllText("Assets/SourceAssets/night-particles.json"));
        var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);
        foreach(var source in pack.Prefabs.Distinct()){
            var raw=source.GetComponent<PcModel>().raw;var profile=profiles["actor_"+raw] as JObject;if(profile==null)continue;
            var path=AssetDatabase.GetAssetPath(source);var instance=PrefabUtility.LoadPrefabContents(path);
            foreach(var emitter in instance.GetComponentsInChildren<NativeMapEmitter>())UnityEngine.Object.DestroyImmediate(emitter.gameObject);
            var clips=(JObject)profile["clips"];var interval=(JArray)(clips["stand"]??clips["Stand"]??clips.Properties().First().Value);
            BuildNightParticles.Add(instance,profile,interval,"actor-"+source.name);
            PrefabUtility.SaveAsPrefabAsset(instance,path);PrefabUtility.UnloadPrefabContents(instance);
        }
    }
    public static void RebuildEffectsWindows(){PrepareEffects();AssetDatabase.SaveAssets();Validate();BuildFullMap.RebuildRuntimeWindows();}
    public static void Validate() {
        var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(!pack||!pack.catalog)throw new Exception("Missing Night pack");var lines=(JArray)JObject.Parse(pack.catalog.text)["lines"];int n=lines.Count,actors=lines.Count(l=>!string.IsNullOrEmpty((string)l["unit"]));
        if(pack.Prefabs.Count()!=2*(n+actors+1)||pack.buildingIcons.Length!=n||pack.buildingIcons.Any(v=>!v)||!pack.builderIcon||!pack.buildIcon||pack.abilityIcons.Any(v=>!v))throw new Exception("Incomplete Night pack");
        foreach(var prefab in pack.Prefabs) {
            var model=prefab.GetComponent<PcModel>();if(!model||model.reforged||model.godotHeight!=0||!model.animatedRoot||model.clips.Length==0||model.clips.Any(v=>!v))throw new Exception("Invalid Night model: "+prefab.name);
            foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.sharedMaterials.Any(v=>!v||!v.shader||!v.mainTexture))throw new Exception("Missing Night material: "+prefab.name);
        }
        foreach(var prefab in pack.units.Where(v=>v))foreach(var pose in new[]{"Stand","Walk","Attack","Death"}){if(!prefab.GetComponent<PcModel>().clips.Any(v=>v.name.StartsWith(pose,StringComparison.OrdinalIgnoreCase)))throw new Exception("Missing Night "+pose+": "+prefab.name);}
        foreach(string key in (JArray)JObject.Parse(File.ReadAllText("Assets/Resources/night-fx-index.json"))["available"])for(int team=0;team<2;team++){var fx=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NightFx/"+key+"-"+team+".prefab");if(!fx||!fx.GetComponent<PcModel>()||fx.GetComponent<PcModel>().clips.Any(v=>!v))throw new Exception("Missing saved Night effect: "+key);}
        foreach(var prefab in pack.Prefabs.Concat(Resources.LoadAll<GameObject>("NightFx")))foreach(var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>())if(!renderer.sharedMaterial||!renderer.sharedMaterial.mainTexture||!renderer.sharedMaterial.shader)throw new Exception("Missing saved night particle material: "+prefab.name);
        Debug.Log("CF_NIGHT_REFERENCES_PASS textures=true animations=true");
    }
    public static void RebuildPresentationWindows(){AssetDatabase.Refresh();var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);var data=JObject.Parse(File.ReadAllText("Assets/Resources/pc-night.json"));pack.abilityIds=((JArray)data["raceAbilityIds"]).Select(v=>(string)v).ToArray();pack.abilityIcons=pack.abilityIds.Select(id=>BuildPC.Texture("Ability-"+id)).ToArray();EditorUtility.SetDirty(pack);PrepareEffects();AssetDatabase.SaveAssets();Validate();BuildFullMap.RebuildRuntimeWindows();}
    public static void BuildWindows(){Prepare();BuildCorrupted.RepairParticles();BuildFullMap.RebuildRuntimeWindows();}
}
