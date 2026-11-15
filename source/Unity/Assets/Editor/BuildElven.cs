using System;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class BuildElven {
    const string PackPath="Assets/Resources/ElvenUnion.asset";
    public static void Prepare() {
        AssetDatabase.Refresh();var data=JObject.Parse(File.ReadAllText("Assets/Resources/pc-elven.json"));var lines=(JArray)data["lines"];int n=lines.Count;
        var pack=ScriptableObject.CreateInstance<PcRacePack>();pack.catalog=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/pc-elven.json");
        pack.hdUnits=pack.hdBuildings=pack.hdBuilders=Array.Empty<GameObject>();pack.units=new GameObject[n*2];pack.buildings=new GameObject[n*2];pack.builders=new GameObject[2];
        for(int team=0;team<2;team++) {
            for(int i=0;i<n;i++){string unit=(string)lines[i]["unit"],raw=(string)lines[i]["building"];if(!string.IsNullOrEmpty(unit))pack.units[team*n+i]=BuildClassic.Model(unit,"u",team);pack.buildings[team*n+i]=BuildClassic.Model(raw,"b",team);}
            pack.builders[team]=BuildClassic.Model("h00P","builder",team);
        }
        pack.buildingIcons=lines.Select(v=>BuildPC.Texture("Building-"+(string)v["building"])).ToArray();pack.builderIcon=BuildPC.Texture("ElvenBuilder");pack.buildIcon=BuildPC.Texture("ElvenBuild");
        pack.abilityIds=((JArray)data["raceAbilityIds"]).Select(v=>(string)v).ToArray();pack.abilityIcons=pack.abilityIds.Select(id=>BuildPC.Texture("Ability-"+id)).ToArray();
        var existing=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(existing){EditorUtility.CopySerialized(pack,existing);UnityEngine.Object.DestroyImmediate(pack);EditorUtility.SetDirty(existing);}else AssetDatabase.CreateAsset(pack,PackPath);
        PrepareEffects();PrepareActorParticles();AssetDatabase.SaveAssets();Validate();Debug.Log("CF_ELVEN_PREPARE_PASS catalog=14 base=11 upgrades=3 classic=true");
    }
    static void PrepareEffects() {
        Directory.CreateDirectory("Assets/Resources/ElvenFx");Directory.CreateDirectory("Assets/Resources/ElvenFxMaterials");AssetDatabase.Refresh();
        var lightning=(JObject)JObject.Parse(File.ReadAllText("Assets/Resources/elven-fx-index.json"))["lightning"];
        foreach(var entry in lightning.Properties()){
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/"+(string)entry.Value["resource"]+".png");if(!texture)throw new Exception("Missing native lightning texture "+entry.Name);
            string path="Assets/Resources/ElvenFxMaterials/Lightning-"+entry.Name+".mat";var material=new Material(Shader.Find("Legacy Shaders/Particles/Additive")){mainTexture=texture};material.SetColor("_TintColor",Color.white);var oldMaterial=AssetDatabase.LoadAssetAtPath<Material>(path);if(oldMaterial){EditorUtility.CopySerialized(material,oldMaterial);UnityEngine.Object.DestroyImmediate(material);EditorUtility.SetDirty(oldMaterial);}else AssetDatabase.CreateAsset(material,path);
        }
        var particles=JObject.Parse(File.ReadAllText("Assets/SourceAssets/elven-particles.json"));var meshKeys=JArray.Parse(File.ReadAllText("Assets/SourceAssets/elven-effects.json")).Select(v=>(string)v).ToArray();
        foreach(string key in meshKeys.Concat(particles.Properties().Select(p=>p.Name).Where(k=>!k.StartsWith("actor_"))).Distinct())for(int team=0;team<2;team++) {
            GameObject root;
            if(meshKeys.Contains(key)){var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedPC/"+key+"-t"+team+".prefab");if(!source)source=BuildClassic.Model(key,"fx",team);root=UnityEngine.Object.Instantiate(source);}
            else {root=new GameObject(key+"-"+team);var model=root.AddComponent<PcModel>();model.raw=key;model.animatedRoot=root;model.particleOnly=true;model.clips=Array.Empty<AnimationClip>();}
            if(particles[key] is JObject profile){var clips=(JObject)profile["clips"];var interval=(JArray)(clips["Stand"]??clips["Birth"]??clips.Properties().First().Value);BuildElvenParticles.Add(root,profile,interval,key+"-"+team);}
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/ElvenFx/"+key+"-"+team+".prefab");UnityEngine.Object.DestroyImmediate(root);
        }
    }
    static void PrepareActorParticles(){
        var profiles=JObject.Parse(File.ReadAllText("Assets/SourceAssets/elven-particles.json"));
        var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);
        foreach(var source in pack.Prefabs.Distinct()){
            var raw=source.GetComponent<PcModel>().raw;var profile=profiles["actor_"+raw] as JObject;if(profile==null)continue;
            var path=AssetDatabase.GetAssetPath(source);var instance=PrefabUtility.LoadPrefabContents(path);
            foreach(var emitter in instance.GetComponentsInChildren<NativeMapEmitter>())UnityEngine.Object.DestroyImmediate(emitter.gameObject);
            var clips=(JObject)profile["clips"];var interval=(JArray)(clips["stand"]??clips["Stand"]??clips.Properties().First().Value);
            BuildElvenParticles.Add(instance,profile,interval,"actor-"+source.name);
            PrefabUtility.SaveAsPrefabAsset(instance,path);PrefabUtility.UnloadPrefabContents(instance);
        }
    }
    public static void RebuildEffectsWindows(){PrepareEffects();AssetDatabase.SaveAssets();Validate();BuildFullMap.RebuildRuntimeWindows();}
    public static void Validate() {
        var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(!pack||!pack.catalog)throw new Exception("Missing Elven pack");var lines=(JArray)JObject.Parse(pack.catalog.text)["lines"];int n=lines.Count,actors=lines.Count(l=>!string.IsNullOrEmpty((string)l["unit"]));
        if(pack.Prefabs.Count()!=2*(n+actors+1)||pack.buildingIcons.Length!=n||pack.buildingIcons.Any(v=>!v)||!pack.builderIcon||!pack.buildIcon||pack.abilityIcons.Any(v=>!v))throw new Exception("Incomplete Elven pack");
        foreach(var prefab in pack.Prefabs) {
            var model=prefab.GetComponent<PcModel>();if(!model||model.reforged||model.godotHeight!=0||!model.animatedRoot||model.clips.Length==0||model.clips.Any(v=>!v))throw new Exception("Invalid Elven model: "+prefab.name);
            foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.sharedMaterials.Any(v=>!v||!v.shader||!v.mainTexture))throw new Exception("Missing Elven material: "+prefab.name);
        }
        foreach(var prefab in pack.units.Where(v=>v))foreach(var pose in new[]{"Stand","Walk","Attack","Death"}){if(!prefab.GetComponent<PcModel>().clips.Any(v=>v.name.StartsWith(pose,StringComparison.OrdinalIgnoreCase)))throw new Exception("Missing Elven "+pose+": "+prefab.name);}
        foreach(string key in (JArray)JObject.Parse(File.ReadAllText("Assets/Resources/elven-fx-index.json"))["available"])for(int team=0;team<2;team++){var fx=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ElvenFx/"+key+"-"+team+".prefab");if(!fx||!fx.GetComponent<PcModel>()||fx.GetComponent<PcModel>().clips.Any(v=>!v))throw new Exception("Missing saved Elven effect: "+key);}
        foreach(var prefab in pack.Prefabs.Concat(Resources.LoadAll<GameObject>("ElvenFx")))foreach(var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>())if(!renderer.sharedMaterial||!renderer.sharedMaterial.mainTexture||!renderer.sharedMaterial.shader)throw new Exception("Missing saved elven particle material: "+prefab.name);
        Debug.Log("CF_ELVEN_REFERENCES_PASS textures=true animations=true");
    }
    public static void RebuildPresentationWindows(){AssetDatabase.Refresh();var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);var data=JObject.Parse(File.ReadAllText("Assets/Resources/pc-elven.json"));pack.abilityIds=((JArray)data["raceAbilityIds"]).Select(v=>(string)v).ToArray();pack.abilityIcons=pack.abilityIds.Select(id=>BuildPC.Texture("Ability-"+id)).ToArray();EditorUtility.SetDirty(pack);PrepareEffects();AssetDatabase.SaveAssets();Validate();BuildFullMap.RebuildRuntimeWindows();}
    public static void BuildWindows(){Prepare();BuildFullMap.RebuildRuntimeWindows();}
}
