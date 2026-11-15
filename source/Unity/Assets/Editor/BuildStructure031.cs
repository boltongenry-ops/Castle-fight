using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
public static class BuildStructure031 {
    public static void Register(){
        BuildCombat030.Register();var path="Assets/Centaur028/index.json";var index=JObject.Parse(File.ReadAllText(path));
        index["Structure031/Pandaren"]="Assets/Structure031/pc-pandaren.json";
        File.WriteAllText(path,index.ToString());AssetDatabase.Refresh();AssetDatabase.SaveAssets();
    }
    public static void Windows(){Register();BuildCentaurs.FinishWindows();Debug.Log("CF_STRUCTURE031_WINDOWS_PASS");}
    public static void Android(){Register();BuildCentaurs.Android();Debug.Log("CF_STRUCTURE031_ANDROID_PASS");}
}
