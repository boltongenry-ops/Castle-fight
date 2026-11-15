using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Reforged assets remain in the project, without references from the classic player.
public static class BuildClassic {
    static readonly Dictionary<string,GameObject> models=new Dictionary<string,GameObject>();
    static JObject paths,appearance;
    public static GameObject Model(string raw,string suffix,int team) {
        if(paths==null){paths=JObject.Parse(File.ReadAllText("Assets/SourceAssets/classic-models.json"));appearance=JObject.Parse(File.ReadAllText("Assets/Resources/native-appearance.json"));}
        string key=raw+"/"+team;if(models.TryGetValue(key,out var saved))return saved;
        string path=(string)paths[raw];if(string.IsNullOrEmpty(path))throw new Exception("Missing classic source mapping: "+raw);
        return models[key]=BuildPC.Model(raw,suffix,team,appearance,path);
    }
    public static void Prepare() {
        AssetDatabase.Refresh();models.Clear();paths=null;
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/CastleFightMap.unity");
        var app=UnityEngine.Object.FindAnyObjectByType<PcGame>();if(!app)throw new Exception("PC scene missing");
        var lines=(JArray)JObject.Parse(app.catalog.text)["lines"];int n=lines.Count;
        app.unitModels=new GameObject[n*2];app.buildingModels=new GameObject[n*2];app.builderModels=new GameObject[2];
        for(int t=0;t<2;t++) {
            for(int i=0;i<n;i++){string u=(string)lines[i]["unit"],b=(string)lines[i]["building"];if(!string.IsNullOrEmpty(u))app.unitModels[t*n+i]=Model(u,"u",t);app.buildingModels[t*n+i]=Model(b,"b",t);}
            app.builderModels[t]=Model("h00C","builder",t);
            var old=app.castleObjects[t];var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GeneratedMap/Prefabs/castle{t}.prefab");if(!prefab)throw new Exception("Missing classic castle");
            var replacement=(GameObject)PrefabUtility.InstantiatePrefab(prefab,old.transform.parent);replacement.name=t==0?"Castle West — Classic":"Castle East — Classic";
            replacement.transform.SetPositionAndRotation(old.transform.position,Quaternion.Euler(0,t==0?0:180,0));app.castleObjects[t]=replacement;UnityEngine.Object.DestroyImmediate(old);
        }
        app.reforgedUnits=app.reforgedBuildings=app.reforgedBuilders=app.reforgedCastles=Array.Empty<GameObject>();
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        BuildOrcs.Prepare();BuildNaga.Prepare();BuildNature.Prepare();if(File.Exists("Assets/Resources/pc-chaos.json"))BuildChaos.Prepare();if(File.Exists("Assets/Resources/pc-northern.json"))BuildNorthern.Prepare();if(File.Exists("Assets/Resources/pc-mech.json"))BuildMech.Prepare();if(File.Exists("Assets/Resources/pc-undead.json"))BuildUndead.Prepare();if(File.Exists("Assets/Resources/pc-corrupted.json"))BuildCorrupted.Prepare();if(File.Exists("Assets/Resources/pc-elemental.json"))BuildElemental.Prepare();if(File.Exists("Assets/Resources/pc-night.json"))BuildNight.Prepare();if(File.Exists("Assets/Resources/pc-elven.json"))BuildElven.Prepare();if(File.Exists("Assets/Resources/pc-ultimate.json"))BuildFinalRaces.Prepare();
        // Resources are always packaged; archive HD-only resources with their GUIDs intact.
        if(!AssetDatabase.IsValidFolder("Assets/ArchivedReforged"))AssetDatabase.CreateFolder("Assets","ArchivedReforged");
        foreach(string folder in new[]{"RefFx","ReforgedIcons"}) {
            string from="Assets/Resources/"+folder,to="Assets/ArchivedReforged/"+folder;
            if(AssetDatabase.IsValidFolder(from)){string error=AssetDatabase.MoveAsset(from,to);if(error!="")throw new Exception(error);}
        }
        AssetDatabase.SaveAssets();BuildPC.ValidateExistingScene();Debug.Log("CF_CLASSIC_PREPARE_PASS races=4 models="+models.Count+" reforged=false");
    }
    public static void BuildWindows(){Prepare();BuildFullMap.RebuildRuntimeWindows();}
}
