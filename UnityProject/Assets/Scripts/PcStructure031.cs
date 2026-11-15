using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    int constructionPage,sharedChestRow=-1;
    int SharedChestRow=>sharedChestRow<0?sharedChestRow=((JArray)JObject.Parse(catalog.text)["lines"]).ToList().FindIndex(l=>(string)l["building"]=="h008"):sharedChestRow;
    string StructureCatalog(int race,string original){
        if(race!=12&&race!=13&&race!=14&&race!=15)return original;
        var replacement=race==13?ContentResources.Load<TextAsset>("Nerubian032/Catalog"):race==14||race==12?ContentResources.Load<TextAsset>("Structure031/Pandaren"):null;
        if(race==12){
            var result=JObject.Parse(original);if(!replacement)return original;var panda=JObject.Parse(replacement.text);
            foreach(var row in ((JArray)result["lines"]).OfType<JObject>().ToArray()){
                var revised=((JArray)panda["lines"]).FirstOrDefault(l=>(string)l["building"]==(string)row["building"]);if(revised==null)continue;
                var copy=(JObject)revised.DeepClone();copy["base"]=row["base"]?.DeepClone()??new JValue(false);if(row["allowed"]!=null)copy["allowed"]=row["allowed"].DeepClone();row.Replace(copy);
            }
            result["abilities"]["APKg"]=panda["abilities"]["APKg"].DeepClone();
            foreach(var slot in (JArray)result["ultimateSlots"])foreach(var group in (JArray)slot["groups"])if((string)group["race"]=="Pandaren Order")group["buildings"]=(string)slot["kind"]=="legendary"?new JArray("pK01"):new JArray(((JArray)group["buildings"]).Where(raw=>(string)raw!="pK01").Select(raw=>raw.DeepClone()));
            var nerub=ContentResources.Load<TextAsset>("Nerubian032/Catalog");if(nerub){
                var revised=JObject.Parse(nerub.text);foreach(var row in ((JArray)result["lines"]).OfType<JObject>().ToArray()){
                    var change=((JArray)revised["lines"]).FirstOrDefault(l=>(string)l["building"]==(string)row["building"]);if(change==null)continue;
                    var copy=(JObject)change.DeepClone();copy["base"]=row["base"]?.DeepClone()??new JValue(false);if(row["allowed"]!=null)copy["allowed"]=row["allowed"].DeepClone();row.Replace(copy);
                }
                foreach(var slot in (JArray)result["ultimateSlots"])foreach(var group in ((JArray)slot["groups"]).ToArray())if((string)group["race"]=="Nerubian Swarm"){
                    var buildings=(string)slot["kind"]=="legendary"?new JArray("hNL1"):new JArray(((JArray)group["buildings"]).Where(raw=>(string)raw!="hNL1"&&(string)raw!="hNO1").Select(raw=>raw.DeepClone()));if(buildings.Count==0)group.Remove();else group["buildings"]=buildings;
                }
            }
            return result.ToString();
        }
        var data=JObject.Parse(replacement?replacement.text:original);var lines=(JArray)data["lines"];
        // Shared chest art and economy already belong to the Human bootstrap.
        if(!lines.Any(l=>(string)l["building"]=="h008"))lines.Add(JObject.Parse(catalog.text)["lines"].First(l=>(string)l["building"]=="h008").DeepClone());
        return data.ToString();
    }
    static int ConstructionRole(HumanMatch.Line line)=>line.building=="h008"?3:line.Producer&&line.unit!="n01U"?0:line.category=="tower"&&line.building!="h001"?2:1;
    int[] ConstructionKinds()=>match.BaseKinds.OrderBy(i=>matchRace==12?match.Lines[i].ultimateSlot:ConstructionRole(match.Lines[i])).ThenBy(i=>i).ToArray();
    int[] ConstructionPageKinds(){var order=ConstructionKinds();int size=order.Length>11?10:11;constructionPage=Mathf.Clamp(constructionPage,0,(order.Length-1)/size);return order.Skip(constructionPage*size).Take(size).ToArray();}
    GameObject CatalogModel(int race,int team,int row,string raw,bool building){
        var pack=Pack(race);var arr=pack?(building?pack.buildings:pack.units):(building?buildingModels:unitModels);
        int count=arr.Length/2;
        if(raw=="h008"&&row>=count)return building?buildingModels[team*(buildingModels.Length/2)+SharedChestRow]:null;
        return arr[team*count+row];
    }
    Texture2D CatalogBuildingIcon(int race,int row,string raw){var revised=ContentResources.Load<Texture2D>("NerubianBuildingIcon/"+raw);if(revised)return revised;var pack=Pack(race);var icons=pack?pack.buildingIcons:buildingIcons;return raw=="h008"&&row>=icons.Length?buildingIcons[SharedChestRow]:icons[row];}
    Texture2D MatchBuildingIcon(int kind){string raw=match.Lines[kind].building;int race=raceIndices[matchRace].ContainsKey(raw)?matchRace:matchBotRace;return CatalogBuildingIcon(race,raceIndices[race][raw],raw);}
}
}
