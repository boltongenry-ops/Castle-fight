using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
public static class BuildNerubian032 {
    public static JObject Registry(){
        var index=new JObject{["Nerubian032/Catalog"]="Assets/Nerubian032/pc-nerubian.json"};
        var rows=(JArray)JObject.Parse(File.ReadAllText("Assets/Nerubian032/pc-nerubian.json"))["lines"];
        foreach(var row in rows){string raw=(string)row["building"],unit=(string)row["unit"];
            if(raw!="nN04")index["NerubianBuildingIcon/"+raw]="Assets/Nerubian032/Icons/Building-"+raw+".png";
            if(!string.IsNullOrEmpty(unit))index["NerubianUnitIcon/"+unit]="Assets/Nerubian032/Icons/Unit-"+unit+".png";
        }return index;
    }
    public static void PrepareAndRender(){
        Directory.CreateDirectory("Assets/Nerubian032/Icons");AssetDatabase.Refresh();
        var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>("Assets/Resources/NerubianSwarm.asset");
        var rows=(JArray)JObject.Parse(pack.catalog.text)["lines"];var profiles=JObject.Parse(File.ReadAllText("Assets/Resources/native-appearance.json"));int family=-1;
        for(int i=0;i<rows.Count;i++){
            var row=rows[i];if((bool)row["base"])family++;string raw=(string)row["building"],unit=(string)row["unit"];bool upgraded=!(bool)row["base"];
            if(raw!="nN04")Save(BuildCentaurIcons.Capture(pack.buildings[i],(JObject)profiles[raw],family,upgraded,false),"Building-"+raw);
            if(!string.IsNullOrEmpty(unit))Save(BuildCentaurIcons.Capture(pack.units[i],(JObject)profiles[unit],family,upgraded,true),"Unit-"+unit);
        }
        AssetDatabase.Refresh();foreach(var entry in Registry().Properties())if(!AssetDatabase.LoadMainAssetAtPath((string)entry.Value))throw new Exception("Missing Nerubian art "+entry.Name);
        BuildStructure031.Register();Debug.Log("CF_NERUBIAN032_ICONS_PASS");
    }
    static void Save(Texture2D image,string name){File.WriteAllBytes("Assets/Nerubian032/Icons/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
    public static void Windows(){BuildStructure031.Windows();Debug.Log("CF_NERUBIAN032_WINDOWS_PASS");}
    public static void Android(){BuildStructure031.Android();Debug.Log("CF_NERUBIAN032_ANDROID_PASS");}
    public static void AndroidPlayer(){
        Android();const string output="Builds/ContentAndroid021";var manifest=JObject.Parse(File.ReadAllText(output+"/manifest.json"));
        string file="Builds/CentaurAndroid028/centaur-028",hash;using(var stream=File.OpenRead(file))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        File.Copy(file,output+"/"+hash+".cfb",true);
        var bundle=((JArray)manifest["bundles"]).First(row=>(string)row["name"]=="centaur-028");bundle["sha256"]=hash;bundle["file"]=hash+".cfb";bundle["size"]=new FileInfo(file).Length;
        var lookup=JObject.Parse(File.ReadAllText("Assets/Centaur028/index.json"));var resources=(JArray)manifest["resources"];
        foreach(var entry in lookup.Properties()){
            var existing=resources.FirstOrDefault(row=>(string)row["key"]==entry.Name);var row=new JObject{["key"]=entry.Name,["asset"]=entry.Value.DeepClone(),["bundle"]="centaur-028"};
            if(existing==null)resources.Add(row);else existing.Replace(row);
        }
        manifest["version"]="0.32.0";manifest["minimumPlayerVersion"]="0.32.0";
        File.WriteAllText(output+"/manifest.json",manifest.ToString());File.WriteAllText("Assets/MobileBootstrap/content.json",manifest.ToString());AssetDatabase.ImportAsset("Assets/MobileBootstrap/content.json",ImportAssetOptions.ForceSynchronousImport);
        BuildMobileContent.RebuildPlayer();Debug.Log("CF_NERUBIAN032_ANDROID_PLAYER_PASS");
    }
}
