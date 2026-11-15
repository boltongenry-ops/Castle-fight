using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
public static class BuildCombat030 {
    public static void Register(){
        AssetDatabase.Refresh();var path="Assets/Centaur028/index.json";
        if(!File.Exists(path))throw new Exception("Missing compatibility supplement registry");
        var lookup=JObject.Parse(File.ReadAllText(path));
        foreach(string kind in new[]{"attack","armor"})foreach(string name in kind=="attack"?new[]{"melee","piercing","siege","magic","hero","chaos"}:new[]{"unarmored","small","medium","large","fortified","hero","divine"}){
            var texture=BuildPC.Texture("Combat-"+kind+"-"+name);if(!texture)throw new Exception("Missing combat icon");
            lookup["CombatType/"+kind+"-"+name]=AssetDatabase.GetAssetPath(texture);
        }
        File.WriteAllText(path,lookup.ToString());AssetDatabase.Refresh();BuildCentaur030.Register();AssetDatabase.SaveAssets();
    }
    public static void Windows(){Register();BuildCentaurs.FinishWindows();Debug.Log("CF_COMBAT030_WINDOWS_PASS");}
    public static void Android(){Register();BuildCentaurs.Android();Debug.Log("CF_COMBAT030_ANDROID_PASS");}
}
