using System;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildLab {
    public static void BuildWindows() { Prepare(); Build(BuildTarget.StandaloneWindows64,"Builds/Windows/CastleFightLab.exe"); }
    public static void BuildAndroid() {
        Prepare(); PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
        Build(BuildTarget.Android,"Builds/Android/CastleFightLab.apk");
    }
    static void Build(BuildTarget target,string path) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Scenes/BattleLab.unity"},locationPathName=path,target=target,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded) throw new Exception(report.summary.result.ToString());
    }
    [MenuItem("Castle Fight/Prepare Lab")]
    public static void Prepare() {
        AssetDatabase.Refresh();
        Directory.CreateDirectory("Assets/Scenes"); Directory.CreateDirectory("Assets/Generated");
        PlayerSettings.companyName="CastleFightLab";PlayerSettings.productName="Castle Fight Unity Lab";
        PlayerSettings.bundleVersion="0.1.0";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.castlefight.unitylab");
        PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.resizableWindow=true; PlayerSettings.runInBackground=true;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var lab=new GameObject("Castle Fight Lab").AddComponent<BattleLab>();
        var paths=new[]{"Assets/SourceAssets/models_lines/hfoo_u.glb","Assets/SourceAssets/models_lines/o002_u.glb"};
        lab.models=paths.Select(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p)).ToArray();
        if(lab.models.Any(m=>m==null))throw new Exception("GLB import failed");
        var visibility=JObject.Parse(File.ReadAllText("Assets/Resources/native-appearance.json"));
        for(int team=0;team<2;team++) {
            var instance=UnityEngine.Object.Instantiate(lab.models[team]);
            var skin=instance.GetComponentInChildren<SkinnedMeshRenderer>();var mats=skin.sharedMaterials;
            var surfaces=(JArray)visibility[team==0?"hfoo":"o002"]["surfaces"];
            if(mats.Length!=surfaces.Count)throw new Exception("Surface count mismatch");
            for(int j=0;j<mats.Length;j++) {
                var unitMaterial=new Material(Shader.Find("CastleFight/NativeUnit"));unitMaterial.mainTexture=mats[j].mainTexture;
                unitMaterial.SetFloat("_Team",(bool?)surfaces[j]["team"]==true?1:0);
                unitMaterial.SetColor("_TeamColor",team==0?new Color(.1f,.3f,.95f):new Color(.85f,.08f,.06f));
                string path=$"Assets/Generated/team{team}-surface{j}.mat";
                AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(unitMaterial,path);mats[j]=unitMaterial;
            }
            skin.sharedMaterials=mats;
            string prefab=$"Assets/Generated/team{team}.prefab";
            lab.models[team]=PrefabUtility.SaveAsPrefabAsset(instance,prefab);
            UnityEngine.Object.DestroyImmediate(instance);
        }
        var clips=new AnimationClip[2][];
        for(int i=0;i<2;i++) {
            clips[i]=AssetDatabase.LoadAllAssetsAtPath(paths[i]).OfType<AnimationClip>().Select((original,index)=>{
                var clip=UnityEngine.Object.Instantiate(original);clip.legacy=true;clip.name=original.name;
                var path=$"Assets/Generated/team{i}-clip{index}.anim";
                AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(clip,path);
                clip.name=original.name;EditorUtility.SetDirty(clip);return clip;
            }).ToArray();
            foreach(string name in new[]{"Walk","Attack","Death"}) if(!clips[i].Any(c=>c.name.StartsWith(name,StringComparison.OrdinalIgnoreCase)))throw new Exception("Clip missing: "+name);
        }
        lab.blueClips=clips[0];lab.redClips=clips[1];
        for(int i=0;i<2;i++) AnimationChecks.Check(lab.models[i],clips[i]);
        lab.groundTextures=Enumerable.Range(0,9).Select(i=>AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/SourceAssets/data/tex/T{i}.png")).ToArray();
        var material=new Material(Shader.Find("CastleFight/NativeTerrain"));
        AssetDatabase.DeleteAsset("Assets/Generated/Ground.mat");AssetDatabase.CreateAsset(material,"Assets/Generated/Ground.mat");lab.groundMaterial=material;
        var particle=new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        AssetDatabase.DeleteAsset("Assets/Generated/NativeParticle.mat");AssetDatabase.CreateAsset(particle,"Assets/Generated/NativeParticle.mat");lab.particleMaterial=particle;
        var all=JObject.Parse(File.ReadAllText("Assets/StreamingAssets/SourceData/unit_traits.json"));
        File.WriteAllText("Assets/Resources/benchmark-traits.json",new JObject { ["hfoo"]=all["hfoo"],["o002"]=all["o002"] }.ToString());
        AssetDatabase.Refresh();
        ProtocolChecks.Run();
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/BattleLab.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/BattleLab.unity",true)};
        AssetDatabase.SaveAssets();
        Debug.Log("CF_PREPARE_PASS models=2 clips="+clips.Sum(c=>c.Length));
    }
}
