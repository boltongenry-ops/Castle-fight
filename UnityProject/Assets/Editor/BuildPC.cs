using System;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildPC {
    static readonly System.Collections.Generic.Dictionary<string,AnimationClip[]> sharedClips=new System.Collections.Generic.Dictionary<string,AnimationClip[]>();
    static T Save<T>(T value,string path) where T:UnityEngine.Object {
        var existing=AssetDatabase.LoadAssetAtPath<T>(path);
        if(existing) {
            EditorUtility.CopySerialized(value,existing);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(existing);return existing;
        }
        AssetDatabase.CreateAsset(value,path);return value;
    }
    public static void ValidateExistingScene() {
        EditorSceneManager.OpenScene("Assets/Scenes/CastleFightMap.unity");var app=UnityEngine.Object.FindAnyObjectByType<PcGame>();if(!app)throw new Exception("Missing saved PC scene");int count=0;
        foreach(var prefab in app.unitModels.Concat(app.buildingModels).Concat(app.builderModels).Where(p=>p)) {
            foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(var mat in skin.sharedMaterials){if(!mat||!mat.mainTexture||!mat.shader||mat.shader.name!="CastleFight/NativeUnit")throw new Exception("Invalid saved runtime material");count++;}
            foreach(var model in prefab.GetComponentsInChildren<PcModel>())if(model.clips==null||model.clips.Any(c=>!c))throw new Exception("Invalid saved animation references");
        }
        if(app.itemIcons==null||app.itemIcons.Length<10||app.itemIcons.Any(t=>!t)||!app.castleIcon)throw new Exception("Missing saved castle item art");
        if(app.reforgedUnits!=null&&app.reforgedUnits.Length>0)BuildReforged.Validate(app);Debug.Log("CF_PC_RUNTIME_REFERENCES_PASS materials="+count);
    }
    public static void PrepareScene() {
        AssetDatabase.Refresh();
        HumanMatchChecks.Run();HumanInventoryChecks.Run();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/CastleFightMap.unity");
        foreach(var old in UnityEngine.Object.FindObjectsByType<PcGame>(FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(old.gameObject);
        Directory.CreateDirectory("Assets/GeneratedPC");
        var appearance=JObject.Parse(File.ReadAllText("Assets/Resources/native-appearance.json"));
        var app=new GameObject("Castle Fight PC").AddComponent<PcGame>();
        var roster=(JArray)JObject.Parse(File.ReadAllText("Assets/Resources/pc-human.json"))["lines"];int count=roster.Count;
        app.unitModels=new GameObject[0];app.buildingModels=new GameObject[0];app.builderModels=new GameObject[0];
        string[] units=roster.Select(v=>(string)v["unit"]).ToArray(),buildings=roster.Select(v=>(string)v["building"]).ToArray();
        app.castleIcon=Texture("CastleShop");app.itemIds=((JObject)JObject.Parse(File.ReadAllText("Assets/Resources/pc-human.json"))["itemCatalog"]).Properties().Select(v=>v.Name).ToArray();app.itemIcons=app.itemIds.Select(v=>Texture("Item-"+v)).ToArray();
        app.buildingIcons=buildings.Select(v=>Texture("Building-"+v)).ToArray();
        app.unitIcons=units.Select(v=>string.IsNullOrEmpty(v)?null:Texture("Unit-"+v)).ToArray();
        app.abilityIds=((JObject)JObject.Parse(File.ReadAllText("Assets/Resources/pc-human.json"))["abilities"]).Properties().Select(v=>v.Name).ToArray();
        app.abilityIcons=app.abilityIds.Select(v=>File.Exists("Assets/SourceAssets/pcui/Ability-"+v+".png")?Texture("Ability-"+v):null).ToArray();
        app.extraCommandIds=new[]{"blink","repair","rescue","pause_all","resume_all","cancel","pause","sell","upgrade","attack","armor","buff","debuff"};
        app.extraCommandIcons=app.extraCommandIds.Select(v=>Texture("Command-"+v)).ToArray();
        app.overlayMaterial=Save(new Material(Shader.Find("CastleFight/PcOverlay")),"Assets/GeneratedPC/Overlay.mat");
        var shell=new Material(Shader.Find("Standard")){color=new Color(.28f,.22f,.13f)};shell.SetFloat("_Metallic",.8f);shell.SetFloat("_Glossiness",.65f);
        app.shellMaterial=Save(shell,"Assets/GeneratedPC/ShotShell.mat");
        app.tracerMaterial=Save(new Material(Shader.Find("CastleFight/PcOverlay")){color=new Color(1,.8f,.3f)},"Assets/GeneratedPC/ShotTracer.mat");
        app.impactMaterial=Save(new Material(Shader.Find("CastleFight/PcOverlay")){color=new Color(1,.55f,.08f,.85f)},"Assets/GeneratedPC/ShotImpact.mat");
        app.warcraftFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/SourceAssets/pcui/WarcraftRussian.ttf");if(!app.warcraftFont)throw new Exception("Original Warcraft font missing");
        app.hudTiles=Enumerable.Range(1,4).Select(i=>Texture("NightElfUITile0"+i)).ToArray();
        app.commandIcons=new[]{"BTNPeasant","BTNHumanBarracks","BTNFarm","BTNWorkshop","BTNMove","BTNStop","BTNHumanBuild","BTNHoldPosition","BTNCancel","BTNFootman"}.Select(Texture).ToArray();
        app.goldIcon=Texture("ResourceGold");app.woodIcon=Texture("ResourceLumber");app.armyIcon=Texture("ResourceArmy");app.timeIndicator=Texture("DayIndicator");
        app.minimapIcons=new[]{"ping","terrain","creep","ally"}.Select(n=>Texture("Minimap-"+n)).ToArray();app.minimapTexture=Texture("Minimap");
        app.buttonUp=Texture("NightElfButton");app.buttonDown=Texture("NightElfButton");app.buttonHighlight=Texture("NightElfButton");
        AssetDatabase.SaveAssets();
        Debug.Log("CF_PC_REFORGED_ONLY_PREPARE_PASS");
        app.catalog=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/pc-human.json");
        app.controls=UnityEngine.Object.FindFirstObjectByType<NativeMapCamera>();
        app.terrainBytes=app.controls.terrainBytes;app.terrainMetadata=app.controls.terrainMetadata;app.waterMetadata=app.controls.waterMetadata;
        app.castleObjects=GameObject.Find("Castles — native final tier").transform.Cast<Transform>().Select(t=>t.gameObject).ToArray();
        EditorSceneManager.SaveScene(scene);
        PlayerSettings.productName="Castle Fight";PlayerSettings.bundleVersion="0.4.0";
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
        AssetDatabase.SaveAssets();Debug.Log("CF_PC_PREPARE_PASS buildings=16 base=11 units=11 teams=2 menu=settings=selection=offline");
    }
    public static Texture2D Texture(string name) {
        string path="Assets/SourceAssets/pcui/"+name+".png";
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(!importer)throw new Exception("Missing native UI texture "+name);
        if(importer.mipmapEnabled||importer.textureCompression!=TextureImporterCompression.Uncompressed||importer.wrapMode!=TextureWrapMode.Clamp) {
            importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.alphaIsTransparency=true;importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    public static GameObject Model(string raw,string suffix,int team,JObject appearance,string explicitPath=null,string animationOwner=null) {
        string path=explicitPath??(suffix=="builder"?"Assets/SourceAssets/models_races/human_alliance.glb":"Assets/SourceAssets/models_lines/"+raw+"_"+suffix+".glb");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!source)throw new Exception("PC model missing: "+path);
        var root=new GameObject(raw+"-team"+team);
        var instance=UnityEngine.Object.Instantiate(source,root.transform);instance.name="Native model";
        if(suffix=="b")instance.transform.localRotation=Quaternion.Euler(0,180,0)*instance.transform.localRotation;
        var config=(JObject)appearance[raw];var surfaces=(JArray)config["surfaces"];int index=0;
        if(suffix=="b"&&File.Exists("Assets/SourceAssets/classic-scales.json"))instance.transform.localScale*=((float?)JObject.Parse(File.ReadAllText("Assets/SourceAssets/classic-scales.json"))[raw]??1);
        if((suffix=="u"||suffix=="builder"&&(raw=="h06P"||raw=="h018"||raw=="u006"||raw=="h051"||raw=="h089"||raw=="h00P"||raw=="h075"||raw=="hUAv"||raw=="hPBu"||raw=="cBld"))&&File.Exists("Assets/SourceAssets/classic-unit-scales.json")) {
            float correction=(float?)JObject.Parse(File.ReadAllText("Assets/SourceAssets/classic-unit-scales.json"))[raw]??1;
            instance.transform.localScale*=correction;var position=instance.transform.localPosition;position.y*=correction;instance.transform.localPosition=position;
        }
        if(File.Exists("Assets/Resources/port-model-sizes.json")){
            var offset=JObject.Parse(File.ReadAllText("Assets/Resources/port-model-sizes.json"))[raw];if(offset!=null){var point=instance.transform.localPosition;point.y+=(float?)offset["ground_offset"]??0;instance.transform.localPosition=point;}
        }
        if(suffix=="fx"&&instance.GetComponentsInChildren<SkinnedMeshRenderer>().Length==0){
            foreach(var filter in instance.GetComponentsInChildren<MeshFilter>()){
                var renderer=filter.GetComponent<MeshRenderer>();if(!renderer)continue;var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name=raw+" static native mesh";
                mesh.boneWeights=Enumerable.Range(0,mesh.vertexCount).Select(_=>new BoneWeight{boneIndex0=0,weight0=1}).ToArray();mesh.bindposes=new[]{Matrix4x4.identity};mesh=Save(mesh,$"Assets/GeneratedPC/{raw}-t{team}-static.asset");
                var host=filter.gameObject;var materials=renderer.sharedMaterials;UnityEngine.Object.DestroyImmediate(renderer);UnityEngine.Object.DestroyImmediate(filter);var skin=host.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterials=materials;skin.rootBone=host.transform;skin.bones=new[]{host.transform};skin.localBounds=mesh.bounds;
            }
        }
        var skins=instance.GetComponentsInChildren<SkinnedMeshRenderer>();
        if(skins.Length!=1)throw new Exception("Expected one animated mesh for "+raw);
        foreach(var skin in skins) {
            var mats=skin.sharedMaterials;
            if(mats.Length!=surfaces.Count)throw new Exception("Surface mismatch: "+raw);
            for(int s=0;s<mats.Length;s++,index++) {
                var mat=new Material(Shader.Find((bool?)surfaces[s]["team_glow"]==true?"CastleFight/CentaurGlow":"CastleFight/NativeUnit")){mainTexture=mats[s].mainTexture?mats[s].mainTexture:raw=="fx_e740be2ceb81"?Texture2D.whiteTexture:null};
                var tint=File.Exists("Assets/SourceAssets/classic-tints.json")?JObject.Parse(File.ReadAllText("Assets/SourceAssets/classic-tints.json"))[raw] as JArray:null;
                var sourceColor=surfaces[s]["color"] as JArray;var nativeColor=sourceColor!=null?new Color((float)sourceColor[0],(float)sourceColor[1],(float)sourceColor[2],(float)sourceColor[3]):Color.white;mat.SetColor("_Tint",nativeColor*(tint!=null?new Color((float)tint[0],(float)tint[1],(float)tint[2],(float)tint[3]):Color.white));
                string blend=(string)surfaces[s]["blend"]??"mask";if(tint!=null&&(float)tint[3]<.999f&&blend!="additive")blend="alpha";bool transparent=blend=="alpha"||blend=="additive";
                mat.SetInt("_SrcBlend",(int)(transparent?BlendMode.SrcAlpha:BlendMode.One));
                mat.SetInt("_DstBlend",(int)(blend=="additive"?BlendMode.One:blend=="alpha"?BlendMode.OneMinusSrcAlpha:BlendMode.Zero));
                mat.SetInt("_ZWrite",transparent?0:1);mat.SetFloat("_Unlit",blend=="additive"||(bool?)surfaces[s]["unlit"]==true?1:0);
                mat.SetFloat("_Cutoff",(float?)surfaces[s]["cutoff"]??.02f);mat.renderQueue=transparent?3000:2450;mat.SetOverrideTag("RenderType",transparent?"Transparent":"TransparentCutout");
                mat.SetFloat("_Team",(bool?)surfaces[s]["team"]==true?1:0);
                mat.SetColor("_TeamColor",team==0?new Color(.1f,.3f,.95f):new Color(.85f,.08f,.06f));
                string matPath=$"Assets/GeneratedPC/{raw}-t{team}-s{index}.mat";
                mats[s]=Save(mat,matPath);
            }
            skin.sharedMaterials=mats;skin.shadowCastingMode=ShadowCastingMode.Off;skin.updateWhenOffscreen=true;
        }
        AnimationClip[] clips;
        if(animationOwner==null||!sharedClips.TryGetValue(animationOwner,out clips)){
        clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Select((original,n)=> {
            string clipPath=animationOwner==null?$"Assets/GeneratedPC/{raw}-t{team}-clip{n}.anim":$"Assets/GeneratedPC/centaur-{animationOwner}-clip{n}.anim";
            var clip=UnityEngine.Object.Instantiate(original);clip.legacy=true;clip.name=original.name;
            clip=Save(clip,clipPath);clip.name=original.name;EditorUtility.SetDirty(clip);return clip;
        }).ToArray();if(animationOwner!=null)sharedClips[animationOwner]=clips;}
        foreach(string required in suffix=="fx"?new string[0]:suffix=="u"&&new[]{"n02K","n02L","n01V"}.Contains(raw)?new[]{"Stand","Walk","Death"}:suffix=="u"&&new[]{"n025","n01C"}.Contains(raw)?new[]{"Stand","Death"}:suffix=="u"&&raw!="h06B"?new[]{"Stand","Walk","Attack","Death"}:suffix=="builder"?raw=="h051"?new[]{"Stand","Stand Work","Death"}:raw=="h00C"?new[]{"Stand","Walk","Stand Work"}:new[]{"Stand","Walk"}:new[]{"Stand"})
            if(!clips.Any(c=>c.name.StartsWith(required,StringComparison.OrdinalIgnoreCase)))throw new Exception("Missing "+required+" on "+raw);
        var view=root.AddComponent<PcModel>();view.raw=raw;view.clips=clips;view.animatedRoot=instance;view.animationProperties=(string)JObject.Parse(File.ReadAllText("Assets/Resources/native-animation-properties.json"))[raw]?["properties"]??"";
        if(raw=="h0A1") {
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            string[] parts={"fx_da9bb6c2f53e","fx_da9bb6c2f53e","fx_da9bb6c2f53e","fx_340eb6d7ea74"};
            float[] angles={170,80,350,220},xs={31.25f,-38,-30.25f,43},ys={-43,-40.25f,30,27.5f},scales={.55f,.55f,.55f,.48f};
            for(int n=0;n<4;n++) {
                var partPrefab=Model(parts[n],"b",team,appearance,"Assets/SourceAssets/models_effects/"+parts[n]+".glb");
                var part=(GameObject)PrefabUtility.InstantiatePrefab(partPrefab,root.transform);part.name="Native sniper detail "+n;
                part.transform.localPosition=new Vector3(xs[n]/30,0,-ys[n]/30);part.transform.localRotation=Quaternion.Euler(0,90-angles[n],0);part.transform.localScale=Vector3.one*scales[n];
            }
        }
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,$"Assets/GeneratedPC/{raw}-t{team}.prefab");
        UnityEngine.Object.DestroyImmediate(root);return prefab;
    }
}
