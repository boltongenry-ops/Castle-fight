using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
public static class BuildReforged {
    const string Root="Assets/SourceAssets/Reforged/",Generated="Assets/GeneratedReforged/";
    static JObject manifest,appearance;static readonly Dictionary<string,GameObject> prefabs=new Dictionary<string,GameObject>();
    static T Save<T>(T value,string path) where T:UnityEngine.Object {var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing){EditorUtility.CopySerialized(value,existing);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(existing);return existing;}AssetDatabase.CreateAsset(value,path);return value;}
    static Texture2D Map(string raw,JObject entry,JToken texture,bool linear=false) {
        if(texture==null)return null;int index=texture is JObject?(int)texture["index"]:(int)texture;int source=(int)entry["textures"][index]["source"];string path=Root+raw+"/"+(string)entry["images"][source]["uri"];
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)throw new Exception("Missing Reforged texture: "+path);
        if(!importer.mipmapEnabled||importer.sRGBTexture==linear||importer.maxTextureSize!=1024||importer.textureCompression!=TextureImporterCompression.Compressed) {
            importer.mipmapEnabled=true;importer.sRGBTexture=!linear;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Compressed;importer.compressionQuality=75;importer.filterMode=FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Repeat;importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static GameObject Model(string raw,int team) {
        string key=raw+"-t"+team;if(prefabs.TryGetValue(key,out var ready))return ready;
        var entry=(JObject)manifest[raw];if(entry==null)throw new Exception("Missing Reforged manifest: "+raw);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>((string)entry["asset"]);if(!source)throw new Exception("Reforged glTF failed to import: "+raw);
        var root=new GameObject(raw+"-reforged-team"+team);var instance=UnityEngine.Object.Instantiate(source,root.transform);instance.name="Reforged model";
        var surfaces=(JArray)entry["appearance"]["surfaces"];var skins=instance.GetComponentsInChildren<SkinnedMeshRenderer>();if(skins.Length!=1)throw new Exception("Reforged mesh mapping: "+raw+" skins="+skins.Length);
        var mats=skins[0].sharedMaterials;if(mats.Length!=surfaces.Count)throw new Exception("Reforged submesh mapping: "+raw+" "+mats.Length+" vs "+surfaces.Count);
        for(int i=0;i<mats.Length;i++) {
            var material=(JObject)entry["materials"][(int)surfaces[i]["material"]];var pbr=(JObject)material["pbrMetallicRoughness"];var m=new Material(Shader.Find("CastleFight/ReforgedUnit"));
            m.mainTexture=Map(raw,entry,pbr["baseColorTexture"]);if(!m.mainTexture)throw new Exception("Missing Reforged diffuse "+raw);
            var normal=Map(raw,entry,material["normalTexture"],true);if(normal){m.SetTexture("_NormalMap",normal);m.SetFloat("_HasNormal",1);}
            var orm=Map(raw,entry,pbr["metallicRoughnessTexture"],true);if(orm){m.SetTexture("_ORMMap",orm);m.SetFloat("_HasORM",1);}
            var emission=Map(raw,entry,material["emissiveTexture"]);if(emission)m.SetTexture("_EmissionMap",emission);
            var teamMask=Map(raw,entry,material["extras"]?["teamColorMask"],true);if(teamMask)m.SetTexture("_TeamMask",teamMask);
            m.SetColor("_TeamColor",team==0?new Color(.1f,.3f,.95f):new Color(.85f,.08f,.06f));m.SetFloat("_Cutoff",(float?)surfaces[i]["cutoff"]??.001f);
            m.SetInt("_SrcBlend",(int)BlendMode.One);m.SetInt("_DstBlend",0);m.SetInt("_ZWrite",1);m.renderQueue=2450;
            mats[i]=Save(m,Generated+key+"-s"+i+".mat");
        }
        skins[0].sharedMaterials=mats;skins[0].shadowCastingMode=ShadowCastingMode.Off;skins[0].updateWhenOffscreen=true;
        var clips=AssetDatabase.LoadAllAssetsAtPath((string)entry["asset"]).OfType<AnimationClip>().Select((sourceClip,i)=>{var clip=UnityEngine.Object.Instantiate(sourceClip);clip.legacy=true;clip.name=sourceClip.name;var saved=Save(clip,Generated+key+"-clip"+i+".anim");saved.name=sourceClip.name;EditorUtility.SetDirty(saved);return saved;}).ToArray();
        var view=root.AddComponent<PcModel>();view.raw=raw;view.reforged=true;view.appearanceKey=raw+"_ref";view.animatedRoot=instance;view.clips=clips;
        view.animationProperties=(string)JObject.Parse(File.ReadAllText("Assets/Resources/native-animation-properties.json"))[raw]?["properties"]??"";if(raw=="hcas")view.animationProperties="upgrade,second";
        foreach(string required in raw=="h00C"?new[]{"Stand","Walk","Stand Work"}:raw=="fx_wisp_explode"?new[]{"Birth"}:new[]{"Stand"})if(!clips.Any(c=>c.name.StartsWith(required,StringComparison.OrdinalIgnoreCase)))throw new Exception("Reforged missing "+required+" "+raw);
        if(raw=="h0A1") {
            skins[0].enabled=false;string[] parts={"fx_da9bb6c2f53e","fx_da9bb6c2f53e","fx_da9bb6c2f53e","fx_340eb6d7ea74"};float[] angles={170,80,350,220},xs={31.25f,-38,-30.25f,43},ys={-43,-40.25f,30,27.5f},scales={.55f,.55f,.55f,.48f};
            for(int n=0;n<4;n++){var part=(GameObject)PrefabUtility.InstantiatePrefab(Model(parts[n],team),root.transform);part.transform.localPosition=new Vector3(xs[n]/30,0,-ys[n]/30);part.transform.localRotation=Quaternion.Euler(0,90-angles[n],0);part.transform.localScale=Vector3.one*scales[n];}
        }
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Generated+key+".prefab");UnityEngine.Object.DestroyImmediate(root);prefabs[key]=prefab;return prefab;
    }
    static void ConfigureTextures() {
        AssetDatabase.StartAssetEditing();
        try {foreach(var path in Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories)) {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);bool linear=Path.GetFileName(path).Contains("_norm")||Path.GetFileName(path).Contains("_orm")||Path.GetFileName(path).Contains("_team");
            if(importer.textureType!=TextureImporterType.Default||!importer.mipmapEnabled||importer.sRGBTexture==linear||importer.maxTextureSize!=1024||importer.textureCompression!=TextureImporterCompression.Compressed) {
                importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=true;importer.sRGBTexture=!linear;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Compressed;importer.compressionQuality=75;importer.filterMode=FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Repeat;importer.SaveAndReimport();
            }
        }}finally{AssetDatabase.StopAssetEditing();}AssetDatabase.Refresh();
    }
    public static void Prepare() {
        AssetDatabase.Refresh();ConfigureTextures();Directory.CreateDirectory(Generated);prefabs.Clear();manifest=JObject.Parse(File.ReadAllText(Root+"manifest.json"));appearance=JObject.Parse(File.ReadAllText("Assets/Resources/native-appearance.json"));
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/CastleFightMap.unity");var app=UnityEngine.Object.FindAnyObjectByType<PcGame>();if(!app)throw new Exception("Prepare existing PC scene before Reforged");
        var lines=(JArray)JObject.Parse(File.ReadAllText("Assets/Resources/pc-human.json"))["lines"];int count=lines.Count;
        app.reforgedUnits=new GameObject[count*2];app.reforgedBuildings=new GameObject[count*2];app.reforgedBuilders=new GameObject[2];app.reforgedCastles=new GameObject[2];
        for(int t=0;t<2;t++) {
            for(int i=0;i<count;i++){string unit=(string)lines[i]["unit"],building=(string)lines[i]["building"];if(!string.IsNullOrEmpty(unit))app.reforgedUnits[t*count+i]=Model(unit,t);app.reforgedBuildings[t*count+i]=Model(building,t);}
            app.reforgedBuilders[t]=Model("h00C",t);app.reforgedCastles[t]=Model("hcas",t);
        }
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);PlayerSettings.bundleVersion="0.6.0";Validate(app);Debug.Log("CF_REFORGED_PREPARE_PASS objects="+manifest.Count);
    }
    public static void ValidateEffectModels() {
        string[] keys={"net","net_air","aura_armor","wisp_explode","orc_rune","serpent_rock","naga_coral","naga_roots","proj_axe_missile","proj_wyvern_missile","proj_poison_arrow","proj_illidan_missile","proj_guard_missile","proj_net_missile","proj_mortar_missile","proj_catapult_missile","proj_cannon_missile","proj_gryphon_missile"};
        foreach(var key in keys){var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/RefFx/"+key+".prefab");if(!model||!model.GetComponent<PcModel>()||model.GetComponent<PcModel>().clips.Any(c=>!c))throw new Exception("Missing Reforged effect model "+key);foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.sharedMaterials.Any(m=>!m||!m.shader||!m.mainTexture))throw new Exception("Missing Reforged effect material "+key);}
        var audit=JObject.Parse(File.ReadAllText("../reforged-effects-import-audit.json"));int count=0;foreach(var e in (JArray)audit["effects"]){string name=(string)e["name"];var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/RefFx/"+name+".png");var meta=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/RefFx/"+name+".json");if(!texture||!meta||texture.width<128)throw new Exception("Missing Reforged effect sheet "+name);count++;}
        if(!AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/RefFx/ReforgedEffect.shader")||!AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/RefFx/FlameParticle.shader")||!AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/RefFx/flame_clouds.png"))throw new Exception("Missing Reforged effect shader or flame texture");Debug.Log("CF_REFORGED_EFFECT_REFERENCES_PASS models="+keys.Length+" sheets="+count);
    }
    public static void PrepareEffectModels() {
        string[] keys={"ensnare_target","ensnare_air","orc_armor_aura","wisp_explode","orc_rune","serpent_rock","naga_coral","naga_roots","proj_axe_missile","proj_wyvern_missile","proj_poison_arrow","proj_illidan_missile","proj_guard_missile","proj_net_missile","proj_mortar_missile","proj_catapult_missile","proj_cannon_missile","proj_gryphon_missile"};
        foreach(var key in keys){var source=Model("fx_"+key,0);var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            string path="Assets/Resources/RefFx/"+(key=="ensnare_target"?"net":key=="ensnare_air"?"net_air":key=="orc_armor_aura"?"aura_armor":key)+".prefab";var result=PrefabUtility.SaveAsPrefabAsset(instance,path);UnityEngine.Object.DestroyImmediate(instance);if(!result||!result.GetComponent<PcModel>())throw new Exception("Failed to save Reforged effect model "+key);}
        AssetDatabase.SaveAssets();ValidateEffectModels();Debug.Log("CF_REFORGED_EFFECT_MODELS_PASS count="+keys.Length);
    }
    public static void PrepareModels(string[] rawcodes) {
        AssetDatabase.Refresh();ConfigureTextures();Directory.CreateDirectory(Generated);prefabs.Clear();manifest=JObject.Parse(File.ReadAllText(Root+"manifest.json"));
        foreach(var raw in rawcodes)for(int team=0;team<2;team++)Model(raw,team);
        AssetDatabase.SaveAssets();
    }
    public static void Validate(PcGame app) {
        int mats=0,models=0;foreach(var prefab in app.reforgedUnits.Concat(app.reforgedBuildings).Concat(app.reforgedBuilders).Concat(app.reforgedCastles).Where(p=>p)) {
            var model=prefab.GetComponent<PcModel>();if(!model||!model.reforged||model.clips.Length==0||model.clips.Any(c=>!c))throw new Exception("Missing saved Reforged animation references: "+prefab.name);models++;
            foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(var m in skin.sharedMaterials){if(!m||!m.mainTexture||!m.shader||m.shader.name!="CastleFight/ReforgedUnit")throw new Exception("Missing saved Reforged material: "+prefab.name);mats++;}
        }
        if(models!=58)throw new Exception("Reforged prefab count "+models);Debug.Log("CF_REFORGED_REFERENCES_PASS prefabs="+models+" materials="+mats);
    }
    public static void ReplaceMapCastles() {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/CastleFightMap.unity");var app=UnityEngine.Object.FindAnyObjectByType<PcGame>();if(!app||app.reforgedCastles.Length!=2||app.castleObjects.Length!=2)throw new Exception("Missing castle replacements");
        var roots=app.castleObjects.ToArray();var replacement=new GameObject[2];
        for(int t=0;t<2;t++){var old=roots[t];var parent=old.transform.parent;var pos=old.transform.position;replacement[t]=(GameObject)PrefabUtility.InstantiatePrefab(app.reforgedCastles[t],parent);replacement[t].name=t==0?"Castle West — Reforged":"Castle East — Reforged";replacement[t].transform.position=pos;replacement[t].transform.rotation=Quaternion.Euler(0,t==0?0:180,0);UnityEngine.Object.DestroyImmediate(old);}
        app.castleObjects=replacement;EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("CF_REFORGED_MAP_CASTLES_PASS count=2");
    }
    public static void BuildWindows(){Prepare();BuildFullMap.RebuildRuntimeWindows();}
}
