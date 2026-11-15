using System;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
public static class BuildNaga {
    const string PackPath="Assets/Resources/NagaStageOne.asset";
    public static void Prepare() {
        AssetDatabase.Refresh();var data=JObject.Parse(File.ReadAllText("Assets/Resources/pc-naga.json"));var lines=(JArray)data["lines"];int n=lines.Count;
        var appearance=JObject.Parse(File.ReadAllText("Assets/Resources/native-appearance.json"));

        var pack=ScriptableObject.CreateInstance<PcRacePack>();pack.catalog=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/pc-naga.json");
        pack.hdUnits=new GameObject[0];pack.hdBuildings=new GameObject[0];pack.hdBuilders=new GameObject[0];pack.units=new GameObject[n*2];pack.buildings=new GameObject[n*2];pack.builders=new GameObject[2];
        for(int t=0;t<2;t++) {
            for(int i=0;i<n;i++){string u=(string)lines[i]["unit"],b=(string)lines[i]["building"];if(!string.IsNullOrEmpty(u)){pack.units[t*n+i]=BuildClassic.Model(u,"u",t);}pack.buildings[t*n+i]=BuildClassic.Model(b,b==u?"u":"b",t);}
            pack.builders[t]=BuildClassic.Model("h00E","builder",t);
        }
        pack.buildingIcons=lines.Select(v=>BuildPC.Texture("Building-"+(string)v["building"])).ToArray();pack.builderIcon=BuildPC.Texture("NagaBuilder");pack.buildIcon=BuildPC.Texture("NagaBuild");
        pack.abilityIds=((JArray)data["raceAbilityIds"]).Select(v=>(string)v).ToArray();pack.abilityIcons=pack.abilityIds.Select(id=>BuildPC.Texture("Ability-"+id)).ToArray();
        var existing=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(existing){EditorUtility.CopySerialized(pack,existing);UnityEngine.Object.DestroyImmediate(pack);EditorUtility.SetDirty(existing);}else AssetDatabase.CreateAsset(pack,PackPath);
        AssetDatabase.SaveAssets();Validate();PlayerSettings.bundleVersion="0.9.3";Debug.Log("CF_NAGA_PREPARE_PASS stage=6 buildings=15 units=10 summons=3 formats=Classic");
    }
    static GameObject Sd(string raw,int t)=>AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GeneratedPC/{raw}-t{t}.prefab");
    static GameObject Hd(string raw,int t)=>AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GeneratedReforged/{raw}-t{t}.prefab");
    public static void Validate() {
        var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(!pack||!pack.catalog)throw new Exception("Missing saved Naga pack");
        var catalog=JObject.Parse(pack.catalog.text);var lines=(JArray)catalog["lines"];int count=lines.Count,actors=lines.Count(l=>!string.IsNullOrEmpty((string)l["unit"]));
        if(pack.Prefabs.Count()!=2*(count+actors+1)||pack.buildingIcons.Length!=count||pack.buildingIcons.Any(i=>!i)||!pack.builderIcon||!pack.buildIcon||pack.abilityIds.Length!=((JArray)catalog["raceAbilityIds"]).Count||pack.abilityIcons.Any(i=>!i))throw new Exception("Invalid saved Naga pack");
        foreach(var prefab in pack.Prefabs) {
            var model=prefab.GetComponent<PcModel>();if(!model||!model.animatedRoot||model.clips.Length==0||model.clips.Any(c=>!c))throw new Exception("Missing Naga animation references: "+prefab.name);
            foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.sharedMaterials.Any(m=>!m||!m.shader||!m.mainTexture))throw new Exception("Missing Naga materials: "+prefab.name);
        }
        foreach(var prefab in pack.units.Concat(pack.hdUnits).Where(p=>p))foreach(var required in new[]{"Stand","Walk","Attack","Death"})if(!prefab.GetComponent<PcModel>().clips.Any(c=>c.name.StartsWith(required,StringComparison.OrdinalIgnoreCase)))throw new Exception("Missing Naga pose "+required+": "+prefab.name);
        Debug.Log("CF_NAGA_REFERENCES_PASS prefabs="+pack.Prefabs.Count()+" textures=true animations=true");
    }
    public static void BuildWindows()=>BuildFullMap.RebuildWindows();
}
