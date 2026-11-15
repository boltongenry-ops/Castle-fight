using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildFullMap {
    const string Generated="Assets/GeneratedMap";
    static JObject manifest;static NativeMapTerrain terrain;static readonly Dictionary<string,GameObject> prefabs=new Dictionary<string,GameObject>();
    static JArray modelChecks;static int emitterCount,waterTiles;
    static AnimationClip castleClip;
    static T Save<T>(T asset,string relative) where T:UnityEngine.Object {
        string path=Generated+"/"+relative;Directory.CreateDirectory(Path.GetDirectoryName(path));
        AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(asset,path);return asset;
    }
    public static void BuildWindows(){Prepare();Build(BuildTarget.StandaloneWindows64,"Builds/MapWindows/CastleFightMap.exe");}
    public static void RebuildWindows(){Build(BuildTarget.StandaloneWindows64,"Builds/MapWindows/CastleFightMap.exe");}
    public static void BuildAndroid(){Prepare();PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.castlefight.unitymap");Build(BuildTarget.Android,"Builds/MapAndroid/CastleFightMap.apk");}
    public static void RebuildRuntimeWindows(){Build(BuildTarget.StandaloneWindows64,"Builds/MapWindows/CastleFightMap.exe",false);}
    public static void RebuildScriptsWindows(){Build(BuildTarget.StandaloneWindows64,"Builds/MapWindows/CastleFightMap.exe",false,true);}
    static void Build(BuildTarget platform,string path,bool regenerate=true,bool scriptsOnly=false) {
        if(File.Exists("Assets/Resources/pc-orcs.json"))OrcMatchChecks.Run();
        if(regenerate){BuildPC.PrepareScene();BuildClassic.Prepare();}else {HumanMatchChecks.Run();HumanInventoryChecks.Run();BuildPC.ValidateExistingScene();if(File.Exists("Assets/Resources/pc-orcs.json"))BuildOrcs.Validate();if(File.Exists("Assets/Resources/pc-naga.json"))BuildNaga.Validate();if(File.Exists("Assets/Resources/pc-nature.json"))BuildNature.Validate();if(File.Exists("Assets/Resources/pc-chaos.json"))BuildChaos.Validate();if(File.Exists("Assets/Resources/pc-northern.json"))BuildNorthern.Validate();if(File.Exists("Assets/Resources/pc-mech.json"))BuildMech.Validate();if(File.Exists("Assets/Resources/pc-undead.json"))BuildUndead.Validate();if(File.Exists("Assets/Resources/pc-corrupted.json"))BuildCorrupted.Validate();if(File.Exists("Assets/Resources/pc-elemental.json"))BuildElemental.Validate();if(File.Exists("Assets/Resources/pc-night.json"))BuildNight.Validate();if(File.Exists("Assets/Resources/pc-elven.json"))BuildElven.Validate();if(File.Exists("Assets/Resources/pc-ultimate.json"))BuildFinalRaces.Validate();if(File.Exists("Assets/Resources/RefFx/net.prefab"))BuildReforged.ValidateEffectModels();}
        PlayerSettings.bundleVersion="0.20.0";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Scenes/CastleFightMap.unity"},target=platform,locationPathName=path,options=scriptsOnly?BuildOptions.BuildScriptsOnly:BuildOptions.None});
        if(scriptsOnly&&result.summary.result!=BuildResult.Succeeded){Debug.LogWarning("CF_SCRIPTS_ONLY_FALLBACK full player rebuild required");result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Scenes/CastleFightMap.unity"},target=platform,locationPathName=path,options=BuildOptions.None});}
        if(scriptsOnly&&result.summary.result==BuildResult.Succeeded)Debug.Log("CF_RUNTIME_SCRIPT_BUILD_PASS");
        if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Map build: "+result.summary.result);
        if(platform==BuildTarget.StandaloneWindows64) {
            string script=Path.GetFullPath("../PortableTools/Update-Portable.ps1");
            var start=new System.Diagnostics.ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -BuildDirectory \""+Path.GetFullPath(Path.GetDirectoryName(path))+"\"") {
                UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true
            };
            using(var process=System.Diagnostics.Process.Start(start)) {
                var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                if(process.ExitCode!=0)throw new Exception("Portable packaging failed: "+output.Result+errors.Result);
                Debug.Log(output.Result);
            }
        }
    }
    public static void CompileCheck(){Debug.Log("CF_COMPILE_CHECK_OK version="+PlayerSettings.bundleVersion);}
    [MenuItem("Castle Fight/Restore Full Map")]
    public static void Prepare() {
        prefabs.Clear();modelChecks=new JArray();emitterCount=0;castleClip=null;
        manifest=JObject.Parse(File.ReadAllText("Assets/Editor/MapSource/manifest.json"));
        var meta=JObject.Parse(File.ReadAllText("Assets/StreamingAssets/SourceData/terrain.json"));
        var water=(JObject)JObject.Parse(File.ReadAllText("Assets/StreamingAssets/SourceData/landscape_reference.json"))["water"];
        terrain=new NativeMapTerrain(File.ReadAllBytes("Assets/Resources/terrain.bytes"),meta,water);
        foreach(string dir in new[]{"Meshes","Materials","Prefabs","Clips"})Directory.CreateDirectory(Generated+"/"+dir);
        File.Copy("Assets/StreamingAssets/SourceData/terrain.json","Assets/Resources/map-terrain.json",true);
        File.Copy("Assets/StreamingAssets/SourceData/landscape_reference.json","Assets/Resources/map-landscape.json",true);
        AssetDatabase.Refresh();
        foreach(JObject model in (JArray)manifest["models"]) {
            string key=(string)model["key"];
            if(key=="castle"){prefabs["castle0"]=CreatePrefab(model,0);prefabs["castle1"]=CreatePrefab(model,1);}else prefabs[key]=CreatePrefab(model,-1);
        }
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("Castle Fight — Original map");
        var groundRoot=new GameObject("Terrain — native heights and 18 texture layers");groundRoot.transform.SetParent(root.transform);
        for(int s=0;s<18;s++) {
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/SourceAssets/data/tex/T{s%9}.png");
            var mesh=terrain.GroundMesh(s,texture);if(mesh.vertexCount==0){UnityEngine.Object.DestroyImmediate(mesh);continue;}
            Save(mesh,$"Meshes/ground{s}.asset");var obj=new GameObject("Ground "+s);obj.transform.SetParent(groundRoot.transform);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var mat=new Material(Shader.Find("CastleFight/NativeTerrain")){mainTexture=texture,renderQueue=s>=9?2450+s%9:2000};mat.SetFloat("_Layer",s>=9?1:0);Save(mat,$"Materials/ground{s}.mat");
            obj.AddComponent<MeshRenderer>().sharedMaterial=mat;if(s<9)obj.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        var waterRoot=new GameObject("Water — native ASha, 45 frames at 12 FPS");waterRoot.transform.SetParent(root.transform);
        var waterMesh=Save(terrain.WaterMesh(out waterTiles),"Meshes/water.asset");waterRoot.AddComponent<MeshFilter>().sharedMesh=waterMesh;
        var waterMaterial=new Material(Shader.Find("CastleFight/NativeWater")){mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceAssets/data/tex/water_native.png")};Save(waterMaterial,"Materials/water.mat");waterRoot.AddComponent<MeshRenderer>().sharedMaterial=waterMaterial;
        var scenery=new GameObject("Doodads — 1257 native placements");scenery.transform.SetParent(root.transform);
        foreach(JObject rec in (JArray)manifest["placements"])Place(rec,scenery.transform);
        var blockers=new GameObject("Pathing — source invisible blockers (data only)");blockers.transform.SetParent(root.transform);
        foreach(JObject excluded in (JArray)manifest["excluded"])if((string)excluded["reason"]=="invisible_path_blocker") {
            var rec=(JObject)excluded["record"];var obj=new GameObject("LOS "+rec["sourceIndex"]);obj.transform.SetParent(blockers.transform);
            obj.transform.position=terrain.Position((float)rec["x"],(float)rec["y"]);Stamp(obj,rec,true);
        }
        var castles=new GameObject("Castles — native final tier");castles.transform.SetParent(root.transform);
        for(int team=0;team<2;team++) {
            float x=team==0?-5120:5120;var castle=new GameObject(team==0?"Castle West — blue":"Castle East — red");castle.transform.SetParent(castles.transform);castle.transform.position=terrain.Position(x,64);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(prefabs["castle"+team]);model.transform.SetParent(castle.transform,false);model.transform.localRotation=Quaternion.Euler(0,team==0?90:-90,0);AddCastleMarkers(castle.transform,team);
        }
        var grid=CreateGrid(root.transform);
        var background=GameObject.CreatePrimitive(PrimitiveType.Plane);background.name="Black map border";background.transform.SetParent(root.transform);background.transform.position=new Vector3(0,-8,0);background.transform.localScale=Vector3.one*400;
        var black=new Material(Shader.Find("Unlit/Color")){color=Color.black};Save(black,"Materials/border.mat");background.GetComponent<MeshRenderer>().sharedMaterial=black;UnityEngine.Object.DestroyImmediate(background.GetComponent<Collider>());
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.55f,.55f);RenderSettings.fog=false;
        var sky=new Material(Shader.Find("CastleFight/NativeMapSky"));Save(sky,"Materials/sky.mat");RenderSettings.skybox=sky;
        var sun=new GameObject("Sun — native direction").AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.93f,.78f);sun.intensity=1.15f;sun.shadows=LightShadows.None;sun.transform.rotation=Quaternion.Euler(52,32,0);
        var camera=new GameObject("RTS Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=Color.black;camera.fieldOfView=50;
        camera.transform.position=terrain.Position(-4544,448)+new Vector3(0,55.27f,-37.28f);camera.transform.LookAt(terrain.Position(-4544,448));
        var controls=camera.gameObject.AddComponent<NativeMapCamera>();controls.terrainBytes=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/terrain.bytes");controls.terrainMetadata=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/map-terrain.json");controls.waterMetadata=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/map-landscape.json");controls.waterRoot=waterRoot;controls.sceneryRoot=scenery;controls.buildGrid=grid;controls.waterMaterial=waterMaterial;
        if(!controls.terrainBytes||!controls.terrainMetadata||!controls.waterMetadata)throw new Exception("Runtime map assets were not imported");
        Validate(root,scenery,castles,grid);
        Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,"Assets/Scenes/CastleFightMap.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/CastleFightMap.unity",true),new EditorBuildSettingsScene("Assets/Scenes/BattleLab.unity",true)};
        PlayerSettings.productName="Castle Fight — Full Map";PlayerSettings.bundleVersion="0.2.0";PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
        AssetDatabase.SaveAssets();Debug.Log("CF_FULL_MAP_PREPARE_PASS placements=1257 models=135 castles=2 waterTiles="+waterTiles+" emitters="+emitterCount);
    }
    static GameObject CreatePrefab(JObject model,int team) {
        string key=(string)model["key"],variant=key+(team<0?"":team.ToString());bool castle=key=="castle";
        bool particleOnly=(bool?)model["particleOnly"]==true;
        var imported=particleOnly?null:AssetDatabase.LoadAssetAtPath<GameObject>((string)model["unityAsset"]);if(!particleOnly&&!imported)throw new Exception("Missing map GLB: "+key);
        var wrapper=new GameObject(variant);var instance=particleOnly?new GameObject("Native particle model"):UnityEngine.Object.Instantiate(imported,wrapper.transform);if(particleOnly)instance.transform.SetParent(wrapper.transform,false);instance.name="Native model";
        // glTFast reflects X. Rotate 180° so the map uses Godot (x,y,z) -> Unity (x,y,-z).
        instance.transform.localRotation=Quaternion.Euler(0,180,0)*instance.transform.localRotation;
        var visibility=(JObject)model["visibility"];var surfaces=(JArray)visibility["surfaces"];
        var clips=particleOnly?Array.Empty<AnimationClip>():AssetDatabase.LoadAllAssetsAtPath((string)model["unityAsset"]).OfType<AnimationClip>().ToArray();
        var clip=castle?clips.FirstOrDefault(c=>c.name=="Stand Upgrade Second"):clips.FirstOrDefault(c=>c.name.StartsWith("stand",StringComparison.OrdinalIgnoreCase));
        if(clip)clip.SampleAnimation(instance,0);
        foreach(var animator in instance.GetComponentsInChildren<Animator>())animator.enabled=false;
        var interval=clip?((JArray)visibility["clips"][clip.name]??new JArray(0,0)):new JArray(0,0);
        float correction=1;
        if(castle)correction=1.2000000476837158f;
        else if(new[]{"ltbs_0_barrelsunit","ltbx_0_barrelsunit1","ltbr_0_barrelsunit0"}.Contains(key))correction=(1f/30)/((float?)visibility["scale"]??1f/30);
        instance.transform.localScale*=correction;
        int surfaceIndex=0,liveSurfaces=0;float maxRadius=0;
        foreach(var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>()) {
            var original=skin.sharedMaterials;var mats=new Material[original.Length];var baked=new Mesh();skin.BakeMesh(baked);
            for(int s=0;s<original.Length;s++,surfaceIndex++) {
                var cfg=surfaceIndex<surfaces.Count?(JObject)surfaces[surfaceIndex]:new JObject();
                float alpha=NativeAppearance.Sample((JObject)cfg["geoset"]??new JObject(),interval,0)*NativeAppearance.Sample((JObject)cfg["layer"]??new JObject(),interval,0);
                var mat=new Material(Shader.Find("CastleFight/NativeMapProp")){mainTexture=original[s].mainTexture,enableInstancing=!castle};
                mat.SetFloat("_NativeAlpha",alpha);mat.SetFloat("_Team",(bool?)cfg["team"]==true?1:0);mat.SetColor("_TeamColor",team==0?new Color(.1f,.3f,.95f):team==1?new Color(.85f,.08f,.06f):new Color(.55f,.55f,.55f));
                if(original[s].name.EndsWith("_add",StringComparison.OrdinalIgnoreCase)){mat.SetFloat("_Unlit",1);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.One);mat.SetFloat("_ZWrite",0);mat.renderQueue=3000;}
                Save(mat,$"Materials/{variant}-s{surfaceIndex}.mat");mats[s]=mat;
                if(alpha<=.001f)baked.SetTriangles(Array.Empty<int>(),s);else liveSurfaces++;
            }
            if(castle){skin.sharedMaterials=mats;skin.shadowCastingMode=ShadowCastingMode.Off;UnityEngine.Object.DestroyImmediate(baked);}
            else {
                baked.name=variant+" Stand";baked.RecalculateBounds();maxRadius=Mathf.Max(maxRadius,baked.bounds.size.magnitude);
                Save(baked,$"Meshes/{variant}-{surfaceIndex}.asset");var obj=skin.gameObject;
                UnityEngine.Object.DestroyImmediate(skin);obj.AddComponent<MeshFilter>().sharedMesh=baked;obj.AddComponent<MeshRenderer>().sharedMaterials=mats;
            }
        }
        if(surfaceIndex!=surfaces.Count)throw new Exception("Map surface mapping: "+key+" "+surfaceIndex+"/"+surfaces.Count);
        AddEmitters(instance,(JObject)model["particles"],interval,variant);
        if(castle){
            if(!castleClip){castleClip=UnityEngine.Object.Instantiate(clip);castleClip.legacy=true;Save(castleClip,"Clips/castle-stand.anim");castleClip.name=clip.name;EditorUtility.SetDirty(castleClip);}
            var anim=instance.GetComponentInChildren<Animation>();if(!anim)anim=instance.AddComponent<Animation>();var loop=wrapper.AddComponent<NativeMapLoop>();loop.player=anim;loop.clip=castleClip;
        }
        else foreach(var anim in instance.GetComponentsInChildren<Animation>())UnityEngine.Object.DestroyImmediate(anim);
        modelChecks.Add(new JObject { ["key"]=variant,["sourceAsset"]=model["sourceAsset"],["standClip"]=clip?clip.name:"",["surfaces"]=surfaceIndex,["liveSurfaces"]=liveSurfaces,["scaleCorrection"]=correction,["frozenScenery"]=!castle,["bakedBoundsMagnitude"]=maxRadius });
        var prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,Generated+"/Prefabs/"+variant+".prefab");UnityEngine.Object.DestroyImmediate(wrapper);return prefab;
    }
    static void AddEmitters(GameObject instance,JObject profile,JArray interval,string key) {
        float scale=(float?)profile["scale"]??1f/30;int i=0;
        foreach(JObject e in (JArray)profile["emitters"]??new JArray()) {
            var obj=new GameObject("Native emitter "+i);string bone=(string)e["bone"];var parent=instance.transform;
            if(!string.IsNullOrEmpty(bone))parent=instance.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name==bone)??parent;
            obj.transform.SetParent(parent,false);var offset=(JArray)e["bone_offset"];var p=(JArray)e["position"];
            obj.transform.localPosition=parent!=instance.transform&&offset!=null?new Vector3(-(float)offset[0],(float)offset[1],(float)offset[2]):new Vector3(-(float)p[0],(float)p[2],-(float)p[1])*scale;
            // Unity box emitters launch along local +Z; native WC3 emitters use +Y.
            obj.transform.localRotation=Quaternion.Euler(-90,0,0);
            var ps=obj.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.startLifetime=Mathf.Max(.05f,(float)e["lifetime"]);main.loop=true;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            float speed=(float)e["speed"]*scale,variation=(float)e["variation"];main.startSpeed=new ParticleSystem.MinMaxCurve(speed*(1-variation),speed*(1+variation));main.startSize=1;
            float maxRate=(float)e["rate"];foreach(var r in (JArray)(e["rate_track"]?["values"])??new JArray())maxRate=Mathf.Max(maxRate,(float)r);
            main.maxParticles=Mathf.Clamp(Mathf.RoundToInt(maxRate*(float)e["lifetime"]*.25f),1,256);
            var force=ps.forceOverLifetime;force.enabled=true;force.space=ParticleSystemSimulationSpace.Local;force.z=-(float)e["gravity"]*scale;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3((float)e["width"]*scale,(float)e["length"]*scale,.001f);shape.rotation=Vector3.zero;shape.randomDirectionAmount=Mathf.Clamp01((float)e["latitude"]/180);
            var colors=(JArray)e["colors"];var alphas=(JArray)e["alphas"];var sizes=(JArray)e["scales"];var ck=new GradientColorKey[3];var ak=new GradientAlphaKey[3];var curve=new AnimationCurve();float mid=Mathf.Clamp((float)e["middle"],.01f,.99f);
            for(int k=0;k<3;k++){float t=k==0?0:k==1?mid:1;var c=(JArray)colors[k];ck[k]=new GradientColorKey(new Color((float)c[0],(float)c[1],(float)c[2]),t);ak[k]=new GradientAlphaKey((float)alphas[k]/255,t);curve.AddKey(k*.5f,(float)sizes[k]*scale);}
            var gradient=new Gradient();gradient.SetKeys(ck,ak);var color=ps.colorOverLifetime;color.enabled=true;color.color=gradient;var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,curve);
            var atlas=ps.textureSheetAnimation;atlas.enabled=true;atlas.numTilesX=Mathf.Max(1,(int)e["columns"]);atlas.numTilesY=Mathf.Max(1,(int)e["rows"]);atlas.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));
            int filter=(int)e["filter"];string shader=filter==1?"Legacy Shaders/Particles/Additive":filter==2||filter==3?"Legacy Shaders/Particles/Multiply":"Legacy Shaders/Particles/Alpha Blended";
            var mat=new Material(Shader.Find(shader)){mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>((string)e["unityTexture"])};Save(mat,$"Materials/{key}-particle{i}.mat");
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;
            var controller=obj.AddComponent<NativeMapEmitter>();controller.emitter=ps;controller.sourceProfile=e.ToString(Newtonsoft.Json.Formatting.None);controller.clipInterval=interval.ToString(Newtonsoft.Json.Formatting.None);i++;emitterCount++;
        }
    }
    static void Stamp(GameObject obj,JObject rec,bool blocker=false) {
        var tag=obj.AddComponent<NativePlacement>();tag.sourceIndex=(int)rec["sourceIndex"];tag.rawcode=(string)rec["id"];tag.modelKey=(string)rec["resolvedModel"]??(string)rec["model"];tag.wc3Position=new Vector3((float)rec["x"],(float?)rec["z"]??0,(float)rec["y"]);tag.wc3Scale=new Vector3((float?)rec["sx"]??1,(float?)rec["sy"]??1,(float?)rec["sz"]??1);tag.wc3AngleRadians=(float?)rec["angle"]??0;tag.invisiblePathBlocker=blocker;
    }
    static void Place(JObject rec,Transform parent) {
        string key=(string)rec["resolvedModel"];var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[key]);obj.name=rec["sourceIndex"]+" — "+rec["id"]+" — "+key;obj.transform.SetParent(parent);
        obj.transform.position=new Vector3((float)rec["x"]/30,rec["z"]!=null?(float)rec["z"]/30:terrain.Height((float)rec["x"],(float)rec["y"]),-(float)rec["y"]/30);
        obj.transform.rotation=Quaternion.Euler(0,-((float)rec["angle"]*Mathf.Rad2Deg+90),0);obj.transform.localScale=new Vector3((float)rec["sx"],(float)rec["sz"],(float)rec["sy"]);Stamp(obj,rec);
    }
    static GameObject CreateGrid(Transform parent) {
        var root=new GameObject("Build grid — 128 WC3, G to toggle");root.transform.SetParent(parent);
        var lines=new List<Vector3>();int cells=0;
        foreach(int side in new[]{-1,1})foreach(int half in new[]{-1,1})for(int x=0;x<32;x++)for(int y=0;y<12;y++) {
            float wx=(side<0?-6144:2048)+64+x*128,wy=(half<0?-1984:448)+64+y*128;
            if(Mathf.Abs(Mathf.Abs(wx)-3520)<128)continue;cells++;
            var c=new[]{terrain.Position(wx-64,wy-64),terrain.Position(wx+64,wy-64),terrain.Position(wx+64,wy+64),terrain.Position(wx-64,wy+64)};
            for(int k=0;k<4;k++){lines.Add(c[k]+Vector3.up*.08f);lines.Add(c[(k+1)%4]+Vector3.up*.08f);}
        }
        var mesh=new Mesh {name="Native build cells",indexFormat=IndexFormat.UInt32};mesh.SetVertices(lines);mesh.SetIndices(Enumerable.Range(0,lines.Count).ToArray(),MeshTopology.Lines,0);Save(mesh,"Meshes/grid.asset");root.AddComponent<MeshFilter>().sharedMesh=mesh;
        var mat=new Material(Shader.Find("Unlit/Color")){color=new Color(.35f,.65f,.45f)};Save(mat,"Materials/grid.mat");root.AddComponent<MeshRenderer>().sharedMaterial=mat;
        var recommendations=new GameObject("Five native recommended plots");recommendations.transform.SetParent(parent);
        int n=0;foreach(var offset in new[]{new Vector2(0,0),new Vector2(128,128),new Vector2(256,256),new Vector2(128,384),new Vector2(512,0)}) {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Quad);obj.name="Recommended "+(++n);obj.transform.SetParent(recommendations.transform);obj.transform.position=terrain.Position(-6080+offset.x,512+offset.y)+Vector3.up*.08f;obj.transform.rotation=Quaternion.Euler(90,0,0);obj.transform.localScale=Vector3.one*(128f/30);
            var marker=new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse")){mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceAssets/ui/building_floor.png")};Save(marker,$"Materials/recommended{n}.mat");obj.GetComponent<MeshRenderer>().sharedMaterial=marker;UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        }
        Debug.Log("CF_MAP_GRID cells="+cells);root.SetActive(false);return root;
    }
    static void AddCastleMarkers(Transform castle,int team) {
        var color=team==0?new Color(.2f,.45f,.95f):new Color(.9f,.25f,.2f);
        var mat=new Material(Shader.Find("Unlit/Color")){color=color};Save(mat,$"Materials/castle{team}-marker.mat");
        var poleMat=new Material(Shader.Find("Unlit/Color")){color=new Color(.25f,.2f,.15f)};Save(poleMat,$"Materials/castle{team}-pole.mat");
        var pole=GameObject.CreatePrimitive(PrimitiveType.Cylinder);pole.name="Team banner pole";pole.transform.SetParent(castle,false);pole.transform.localPosition=new Vector3(0,11,0);pole.transform.localScale=new Vector3(.24f,4.5f,.24f);pole.GetComponent<MeshRenderer>().sharedMaterial=poleMat;UnityEngine.Object.DestroyImmediate(pole.GetComponent<Collider>());
        var flag=GameObject.CreatePrimitive(PrimitiveType.Cube);flag.name="Team banner";flag.transform.SetParent(castle,false);flag.transform.localPosition=new Vector3(0,14.4f,-1.4f);flag.transform.localScale=new Vector3(.1f,1.6f,2.6f);flag.GetComponent<MeshRenderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(flag.GetComponent<Collider>());
        var ring=new GameObject("Team base ring");ring.transform.SetParent(castle,false);ring.transform.localPosition=Vector3.up*.06f;
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int i=0;i<=64;i++)for(int j=0;j<=8;j++){float a=i*Mathf.PI*2/64,b=j*Mathf.PI*2/8;vertices.Add(new Vector3(Mathf.Cos(a)*(8.9f+.3f*Mathf.Cos(b)),.3f*Mathf.Sin(b),Mathf.Sin(a)*(8.9f+.3f*Mathf.Cos(b))));}
        for(int i=0;i<64;i++)for(int j=0;j<8;j++){int p=i*9+j;triangles.AddRange(new[]{p,p+9,p+1,p+1,p+9,p+10});}
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();Save(mesh,$"Meshes/castle{team}-ring.asset");ring.AddComponent<MeshFilter>().sharedMesh=mesh;
        var ringMat=new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse")){color=new Color(color.r,color.g,color.b,.55f)};Save(ringMat,$"Materials/castle{team}-ring.mat");ring.AddComponent<MeshRenderer>().sharedMaterial=ringMat;
    }
    static void Validate(GameObject root,GameObject scenery,GameObject castles,GameObject grid) {
        if(scenery.transform.childCount!=1257||castles.transform.childCount!=2||terrain.width!=129||terrain.height!=65)throw new Exception("Map count mismatch");
        foreach(var tag in scenery.GetComponentsInChildren<NativePlacement>()) {
            Vector3 p=tag.transform.position;Vector3 expected=new Vector3(tag.wc3Position.x,tag.wc3Position.y,-tag.wc3Position.z)/30;
            if((p-expected).sqrMagnitude>.000001f)throw new Exception("Placement drift "+tag.sourceIndex);
            var scale=tag.transform.localScale;
            if((scale-new Vector3(tag.wc3Scale.x,tag.wc3Scale.z,tag.wc3Scale.y)).sqrMagnitude>.000001f)throw new Exception("Scale drift "+tag.sourceIndex);
            if(Quaternion.Angle(tag.transform.rotation,Quaternion.Euler(0,-tag.wc3AngleRadians*Mathf.Rad2Deg-90,0))>.001f)throw new Exception("Rotation drift "+tag.sourceIndex);
        }
        var fixtures=JObject.Parse(File.ReadAllText("Assets/Editor/MapSource/terrain-golden.json"));
        float maxHeightError=0;
        foreach(JObject f in (JArray)fixtures["heights"])foreach(bool walking in new[]{false,true}) {
            float error=Mathf.Abs(terrain.Height((float)f["x"],(float)f["y"],walking)-(float)f[walking?"walk":"ground"]);maxHeightError=Mathf.Max(maxHeightError,error);
            if(error>.0001f)throw new Exception("Godot terrain fixture mismatch "+f);
        }
        foreach(JObject f in (JArray)fixtures["waterColors"]) {
            var c=terrain.DepthColor((float)f["depth"]);var v=(JArray)f["rgba"];var expected=new Vector4((float)v[0],(float)v[1],(float)v[2],(float)v[3]);
            if(((Vector4)c-expected).magnitude>.000001f)throw new Exception("Godot water color mismatch");
        }
        if(waterTiles!=534)throw new Exception("Water mask mismatch");
        var heightChecks=new JArray();foreach(float x in new[]{-5120,-2048,-1536,0,1536,2048,5120})heightChecks.Add(new JObject { ["wc3X"]=x,["ground"]=terrain.Height(x,0),["walk"]=terrain.Height(x,0,true) });
        var result=new JObject { ["status"]="PASS",["sourceBuild"]="0.6.17",["placements"]=scenery.transform.childCount,["uniqueDoodadModels"]=135,["castles"]=2,["waterTiles"]=waterTiles,["waterFrames"]=45,["waterRate"]=12,["sourceRecords"]=1992,["blockersDataOnly"]=706,["restoredBeyondGodot"]=12,["excludedOutside"]=29,["godotHeightFixtures"]=1005,["godotWaterColorFixtures"]=8,["maxHeightErrorMetres"]=maxHeightError,["heightSamples"]=heightChecks,["modelChecks"]=modelChecks,["allPlacementPositionsScalesAnglesChecked"]=true,["utc"]=DateTime.UtcNow.ToString("O") };
        File.WriteAllText(Path.GetFullPath("../map-restoration.json"),result.ToString());Debug.Log("CF_FULL_MAP_VALIDATE_PASS water="+waterTiles);
    }
}