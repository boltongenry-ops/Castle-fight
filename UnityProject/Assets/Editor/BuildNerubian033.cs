using System;
using System.IO;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEngine;
public static class BuildNerubian033 {
    public static void Validate(){
        var results=new JArray();string folder=Path.GetFullPath("../Validation/Nerubian033");Directory.CreateDirectory(folder);
        try{
            var own=JObject.Parse(File.ReadAllText("Assets/Nerubian032/pc-nerubian.json"));var human=JObject.Parse(File.ReadAllText("Assets/Resources/pc-human.json"));
            Nerubian033Scenarios.Run(own,human,(ok,message)=>results.Add(new JObject{["pass"]=ok,["check"]=message}));
            Infernal033Scenarios.Run(JObject.Parse(File.ReadAllText("Assets/Nerubian033/pc-chaos.json")),human,(ok,message)=>results.Add(new JObject{["pass"]=ok,["check"]=message}));
            FinalRaceScenarios.PoisonRefresh(own,human,(ok,message)=>results.Add(new JObject{["pass"]=ok,["check"]=message}));
            File.WriteAllText(Path.Combine(folder,"editor-verification.json"),new JObject{["status"]="PASS",["checks"]=results}.ToString());Debug.Log("CF_NERUBIAN033_AUTHORITY_PASS checks="+results.Count);
        }catch(Exception e){File.WriteAllText(Path.Combine(folder,"editor-verification.json"),new JObject{["status"]="FAIL",["checks"]=results,["error"]=e.ToString()}.ToString());throw;}
    }
}
