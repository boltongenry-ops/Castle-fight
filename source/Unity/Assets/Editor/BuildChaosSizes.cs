using System;
using System.IO;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// Adjust only the final pack's existing prefabs, retaining every saved GUID.
public static class BuildChaosSizes {
    public static void Prepare() {
        AssetDatabase.Refresh();
        var paths=JObject.Parse(File.ReadAllText("Assets/SourceAssets/classic-models.json"));
        var scales=JObject.Parse(File.ReadAllText("Assets/SourceAssets/classic-unit-scales.json"));
        foreach(string raw in new[]{"n00L","n02K","n02L"})for(int team=0;team<2;team++) {
            string path=$"Assets/GeneratedPC/{raw}-t{team}.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try {
                var view=root.GetComponent<PcModel>();
                var source=AssetDatabase.LoadAssetAtPath<GameObject>((string)paths[raw]);
                if(!view||!view.animatedRoot||!source)throw new Exception("Missing classic size reference: "+raw);
                float correction=(float)scales[raw];
                view.animatedRoot.transform.localScale=source.transform.localScale*correction;
                var position=source.transform.localPosition;position.y*=correction;view.animatedRoot.transform.localPosition=position;
                view.godotHeight=0;
                PrefabUtility.SaveAsPrefabAsset(root,path);
                Debug.Log($"CF_CHAOS_SIZE_PASS raw={raw} team={team} correction={correction:F6}");
            } finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();BuildChaos.Validate();
    }
    public static void BuildWindows(){Prepare();BuildFullMap.RebuildRuntimeWindows();}
}
