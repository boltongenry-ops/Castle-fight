using System;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class BuildNorthern {
    const string PackPath="Assets/Resources/NorthernRealms.asset";
    public static void Prepare() {
        AssetDatabase.Refresh();var data=JObject.Parse(File.ReadAllText("Assets/Resources/pc-northern.json"));var lines=(JArray)data["lines"];int n=lines.Count;
        var pack=ScriptableObject.CreateInstance<PcRacePack>();pack.catalog=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/pc-northern.json");
        pack.hdUnits=pack.hdBuildings=pack.hdBuilders=Array.Empty<GameObject>();pack.units=new GameObject[n*2];pack.buildings=new GameObject[n*2];pack.builders=new GameObject[2];
        for(int team=0;team<2;team++) {
            for(int i=0;i<n;i++){string unit=(string)lines[i]["unit"],raw=(string)lines[i]["building"];if(!string.IsNullOrEmpty(unit))pack.units[team*n+i]=BuildClassic.Model(unit,"u",team);pack.buildings[team*n+i]=BuildClassic.Model(raw,"b",team);}
            pack.builders[team]=BuildClassic.Model("h017","builder",team);
        }
        pack.buildingIcons=lines.Select(v=>BuildPC.Texture("Building-"+(string)v["building"])).ToArray();pack.builderIcon=BuildPC.Texture("NorthernBuilder");pack.buildIcon=BuildPC.Texture("NorthernBuild");
        pack.abilityIds=((JArray)data["raceAbilityIds"]).Select(v=>(string)v).ToArray();pack.abilityIcons=pack.abilityIds.Select(id=>BuildPC.Texture("Ability-"+id)).ToArray();
        var existing=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(existing){EditorUtility.CopySerialized(pack,existing);UnityEngine.Object.DestroyImmediate(pack);EditorUtility.SetDirty(existing);}else AssetDatabase.CreateAsset(pack,PackPath);
        PrepareEffects();AssetDatabase.SaveAssets();Validate();Debug.Log("CF_NORTHERN_PREPARE_PASS catalog=17 base=11 upgrades=6 units=11 classic=true");
    }
    static void PrepareEffects() {
        Directory.CreateDirectory("Assets/Resources/NorthernFx");AssetDatabase.Refresh();
        foreach(string key in JArray.Parse(File.ReadAllText("Assets/SourceAssets/northern-effects.json"))) {
            var source=BuildClassic.Model(key,"fx",0);var root=UnityEngine.Object.Instantiate(source);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/NorthernFx/"+key+".prefab");UnityEngine.Object.DestroyImmediate(root);
        }
        var snow=new Material(Shader.Find("CastleFight/NativeUnit"));snow.mainTexture=BuildPC.Texture("NorthernSnow");snow.SetInt("_SrcBlend",5);snow.SetInt("_DstBlend",10);snow.SetInt("_ZWrite",0);snow.SetFloat("_Unlit",1);snow.SetFloat("_Cutoff",.002f);snow.SetColor("_Tint",new Color(200f/255,200f/255,1));snow.renderQueue=3000;
        const string path="Assets/Resources/NorthernSnow.mat";var saved=AssetDatabase.LoadAssetAtPath<Material>(path);if(saved){EditorUtility.CopySerialized(snow,saved);UnityEngine.Object.DestroyImmediate(snow);EditorUtility.SetDirty(saved);}else AssetDatabase.CreateAsset(snow,path);
    }
    public static void Validate() {
        var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(!pack||!pack.catalog)throw new Exception("Missing Northern pack");
        if(pack.Prefabs.Count()!=58||pack.buildingIcons.Length!=17||pack.buildingIcons.Any(v=>!v)||!pack.builderIcon||!pack.buildIcon||pack.abilityIcons.Any(v=>!v))throw new Exception("Incomplete Northern pack");
        foreach(var prefab in pack.Prefabs) {
            var model=prefab.GetComponent<PcModel>();if(!model||model.reforged||model.godotHeight!=0||!model.animatedRoot||model.clips.Length==0||model.clips.Any(v=>!v))throw new Exception("Invalid Northern model: "+prefab.name);
            foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.sharedMaterials.Any(v=>!v||!v.shader||!v.mainTexture))throw new Exception("Missing Northern material: "+prefab.name);
        }
        foreach(var prefab in pack.units.Where(v=>v))foreach(var pose in new[]{"Stand","Walk","Attack","Death"})if(!prefab.GetComponent<PcModel>().clips.Any(v=>v.name.StartsWith(pose,StringComparison.OrdinalIgnoreCase)))throw new Exception("Missing Northern "+pose+": "+prefab.name);
        foreach(string key in JArray.Parse(File.ReadAllText("Assets/SourceAssets/northern-effects.json"))){var fx=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NorthernFx/"+key+".prefab");if(!fx||!fx.GetComponent<PcModel>()||fx.GetComponent<PcModel>().clips.Any(v=>!v))throw new Exception("Missing saved Northern effect: "+key);}
        Debug.Log("CF_NORTHERN_REFERENCES_PASS prefabs=58 textures=true animations=true");
    }
    public static void BuildWindows(){Prepare();BuildFullMap.RebuildRuntimeWindows();}
}
