using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using CastleFight;
using UnityEditor;
using UnityEngine;
public static class BuildCentaurs {
    const string PackPath="Assets/Resources/CentaurKhanates.asset";
    public static void Prepare(){
        AssetDatabase.Refresh();Directory.CreateDirectory("Assets/Resources/CentaurFx");Directory.CreateDirectory("Assets/Centaur028");AssetDatabase.Refresh();
        var data=JObject.Parse(File.ReadAllText("Assets/Resources/pc-centaur.json"));var rows=(JArray)data["lines"];int n=rows.Count;
        var appearance=JObject.Parse(File.ReadAllText("Assets/Resources/native-appearance.json"));var paths=JObject.Parse(File.ReadAllText("Assets/SourceAssets/classic-models.json"));
        GameObject Model(string raw,string suffix,int team){string source=(string)paths[raw];return BuildPC.Model(raw,suffix,team,appearance,source,Path.GetFileNameWithoutExtension(source));}
        var pack=ScriptableObject.CreateInstance<PcRacePack>();pack.catalog=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/pc-centaur.json");pack.units=new GameObject[n*2];pack.buildings=new GameObject[n*2];pack.builders=new GameObject[2];pack.hdUnits=pack.hdBuildings=pack.hdBuilders=Array.Empty<GameObject>();
        for(int team=0;team<2;team++){
            for(int i=0;i<n;i++){string unit=(string)rows[i]["unit"],raw=(string)rows[i]["building"];if(!string.IsNullOrEmpty(unit))pack.units[team*n+i]=Model(unit,"u",team);pack.buildings[team*n+i]=Model(raw,"b",team);}
            pack.builders[team]=Model("cBld","builder",team);
            var fx=UnityEngine.Object.Instantiate(Model("cAxe","fx",team));PrefabUtility.SaveAsPrefabAsset(fx,"Assets/Resources/CentaurFx/cAxe-"+team+".prefab");UnityEngine.Object.DestroyImmediate(fx);
        }
        pack.buildingIcons=rows.Select(v=>BuildPC.Texture("Building-"+(string)v["building"])).ToArray();pack.builderIcon=BuildPC.Texture("CentaurBuilder");pack.buildIcon=BuildPC.Texture("CentaurBuild");pack.abilityIds=HumanMatch.Strings(data["raceAbilityIds"]);pack.abilityIcons=pack.abilityIds.Select(id=>BuildPC.Texture("Ability-"+id)).ToArray();
        var old=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(old){EditorUtility.CopySerialized(pack,old);UnityEngine.Object.DestroyImmediate(pack);EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(pack,PackPath);
        AssetDatabase.SaveAssets();Validate();
        var lookup=new JObject{["CentaurKhanates"]=PackPath,["pc-centaur"]="Assets/Resources/pc-centaur.json",["MobileCatalogs/15"]="Assets/Resources/pc-centaur.json",["MobileBuilders/15"]=AssetDatabase.GetAssetPath((old?old:pack).builders[0]),["RaceIcon-centaur"]="Assets/Resources/RaceIcon-centaur.png",["Aspect-centaur_spear"]="Assets/Resources/Aspect-centaur_spear.png",["Aspect-centaur_caravan"]="Assets/Resources/Aspect-centaur_caravan.png",["CentaurFx/cAxe-0"]="Assets/Resources/CentaurFx/cAxe-0.prefab",["CentaurFx/cAxe-1"]="Assets/Resources/CentaurFx/cAxe-1.prefab"};
        foreach(var row in rows){string actor=(string)row["unit"],file="Assets/SourceAssets/pcui/CentaurUnit-"+(string)row["unit"]+".png";if(!string.IsNullOrEmpty(actor)&&File.Exists(file))lookup["CentaurUnitIcon/"+actor]=file;}
        File.WriteAllText("Assets/Centaur028/index.json",lookup.ToString());AssetDatabase.Refresh();BuildCombat030.Register();Debug.Log("CF_CENTAUR_PREPARE_PASS lines="+n);
    }
    public static void Validate(){
        var p=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(!p||!p.catalog||p.units.Length!=38||p.buildings.Length!=38||p.builders.Length!=2||p.buildingIcons.Any(t=>!t)||p.abilityIcons.Any(t=>!t)||!p.builderIcon)throw new Exception("Incomplete centaur pack");
        foreach(var prefab in p.Prefabs){var m=prefab.GetComponent<PcModel>();if(!m||m.reforged||m.clips.Length==0||m.clips.Any(c=>!c))throw new Exception("Invalid centaur model "+prefab.name);foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.sharedMaterials.Any(mat=>!mat||!mat.mainTexture))throw new Exception("Missing centaur texture "+prefab.name);}
    }
    static void Bundle(BuildTarget target,string output){
        Directory.CreateDirectory(output);var lookup=JObject.Parse(File.ReadAllText("Assets/Centaur028/index.json"));var assets=lookup.Properties().Select(v=>(string)v.Value).Concat(new[]{"Assets/Centaur028/index.json"}).Distinct().ToArray();
        if(target==BuildTarget.Android)foreach(string file in AssetDatabase.GetDependencies(assets,true))if(AssetImporter.GetAtPath(file) is TextureImporter importer){var s=importer.GetPlatformTextureSettings("Android");s.name="Android";s.overridden=true;s.maxTextureSize=file.Contains("/pcui/")?512:512;s.format=TextureImporterFormat.ETC2_RGBA8;s.textureCompression=TextureImporterCompression.Compressed;importer.SetPlatformTextureSettings(s);importer.SaveAndReimport();}
        var result=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="centaur-028",assetNames=assets}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,target);if(!result)throw new Exception("Centaur bundle failed");Debug.Log("CF_CENTAUR_BUNDLE_PASS "+target);
    }
    public static void Windows(){Prepare();Bundle(BuildTarget.StandaloneWindows64,"Builds/CentaurWindows028");File.Copy("Builds/CentaurWindows028/centaur-028","Assets/StreamingAssets/Frontend/centaur-028.windows",true);File.Copy("Builds/CentaurWindows028/centaur-028","Builds/MapWindows/CastleFightMap_Data/StreamingAssets/Frontend/centaur-028.windows",true);BuildFrontendRich.RebuildPlayerOnlyWindows();}
    public static void FinishWindows(){
        BuildCentaurIcons.FinishPortraits();AssetDatabase.Refresh();var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);if(!pack)throw new Exception("Prepare centaurs first");
        var data=JObject.Parse(pack.catalog.text);var rows=(JArray)data["lines"];var appearance=JObject.Parse(File.ReadAllText("Assets/Resources/native-appearance.json"));
        foreach(var prefab in pack.Prefabs){var raw=prefab.GetComponent<PcModel>().raw;var surfaces=appearance[raw]["surfaces"] as JArray;foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())for(int i=0;i<skin.sharedMaterials.Length;i++)if((bool?)surfaces?[i]?["team_glow"]==true){var material=skin.sharedMaterials[i];material.shader=Shader.Find("CastleFight/CentaurGlow");EditorUtility.SetDirty(material);}}
        EditorUtility.SetDirty(pack);AssetDatabase.SaveAssets();Validate();
        if(!File.Exists("Assets/Centaur028/index.json"))throw new Exception("Missing centaur registry");
        Bundle(BuildTarget.StandaloneWindows64,"Builds/CentaurWindows028");
        File.Copy("Builds/CentaurWindows028/centaur-028","Assets/StreamingAssets/Frontend/centaur-028.windows",true);File.Copy("Builds/CentaurWindows028/centaur-028","Builds/MapWindows/CastleFightMap_Data/StreamingAssets/Frontend/centaur-028.windows",true);BuildFrontendRich.RebuildPlayerOnlyWindows();
    }
    public static void Android(){Validate();Bundle(BuildTarget.Android,"Builds/CentaurAndroid028");}
}
