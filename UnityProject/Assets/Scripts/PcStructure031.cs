using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    int constructionPage,sharedChestRow=-1;
    int SharedChestRow=>sharedChestRow<0?sharedChestRow=((JArray)JObject.Parse(catalog.text)["lines"]).ToList().FindIndex(l=>(string)l["building"]=="h008"):sharedChestRow;
    string StructureCatalog(int race,string original){
        if(race==4){var chaos=ContentResources.Load<TextAsset>("Nerubian033/Chaos");return chaos?chaos.text:original;}
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
                var revised=JObject.Parse(nerub.text);var abilityKeys=((JArray)revised["lines"]).SelectMany(row=>HumanMatch.Strings(row["abilities"]).Concat(HumanMatch.Strings(row["stats"]?["abilities"]))).Distinct();foreach(string key in abilityKeys)if(revised["abilities"]?[key]!=null)result["abilities"][key]=revised["abilities"][key].DeepClone();foreach(var row in ((JArray)result["lines"]).OfType<JObject>().ToArray()){
                    var change=((JArray)revised["lines"]).FirstOrDefault(l=>(string)l["building"]==(string)row["building"]);if(change==null)continue;
                    var copy=(JObject)change.DeepClone();copy["base"]=row["base"]?.DeepClone()??new JValue(false);if(row["allowed"]!=null)copy["allowed"]=row["allowed"].DeepClone();row.Replace(copy);
                }
                foreach(var slot in (JArray)result["ultimateSlots"])foreach(var group in ((JArray)slot["groups"]).ToArray())if((string)group["race"]=="Nerubian Swarm"){
                    var buildings=(string)slot["kind"]=="legendary"?new JArray("hNL1"):new JArray(((JArray)group["buildings"]).Where(raw=>(string)raw!="hNL1"&&(string)raw!="hNO1").Select(raw=>raw.DeepClone()));if(buildings.Count==0)group.Remove();else group["buildings"]=buildings;
                }
            }
            var chaos=ContentResources.Load<TextAsset>("Nerubian033/Chaos");if(chaos){var source=JObject.Parse(chaos.text);foreach(var row in result["lines"].Where(l=>(string)l["unit"]=="n000"))row["stats"]=source["lines"].First(l=>(string)l["unit"]=="n000")["stats"].DeepClone();}
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
    GameObject CatalogModel(int race,int team,int row,string raw,bool building,bool classic=false){
        if(race==BlackHand.Race){if(!building&&row>=15)return null;var model=building?BlackHandBuildings.Model(row,team):BlackHandArt.Fighter(row,team);if(model)return model;row=BlackHand.ArtRow(row);}
        var pack=Pack(race);var arr=pack?(building?pack.buildings:pack.units):(building?buildingModels:unitModels);
        int count=arr.Length/2;
        if(raw=="h008"&&row>=count)return building?OptionalModel(buildingModels[team*(buildingModels.Length/2)+SharedChestRow],team):null;
        if(MobileContent.Active&&race==12)return OptionalModel(UltimateModel034(team,row,building),team);
        var modelPrefab=arr[team*count+row];
        return classic?modelPrefab:OptionalModel(modelPrefab,team);
    }
    readonly Dictionary<string,Texture2D> authoredBuildingIcons=new Dictionary<string,Texture2D>(StringComparer.Ordinal);
    Texture2D AuthoredBuildingIcon(string raw){
        if(string.IsNullOrEmpty(raw))return null;
        if(authoredBuildingIcons.TryGetValue(raw,out var icon))return icon;
        var asset=Resources.Load<TextAsset>("AuthoredBuildingIcons/"+raw);
        byte[] png=null;
        if(asset){
            png=asset.bytes;
            Resources.UnloadAsset(asset);
        }else if(!Application.isMobilePlatform){
            var path=MobileContent.Media("Frontend/AuthoredBuildingIcons/"+raw+".png");
            if(File.Exists(path))png=File.ReadAllBytes(path);
        }
        if(png!=null){
            icon=new Texture2D(2,2,TextureFormat.RGBA32,false){name="AuthoredBuilding-"+raw,wrapMode=TextureWrapMode.Clamp};
            if(!ImageConversion.LoadImage(icon,png,true)){
                Destroy(icon);
                icon=null;
            }
        }
        authoredBuildingIcons.Add(raw,icon);
        return icon;
    }
    void DisposeAuthoredBuildingIcons(){
        foreach(var icon in authoredBuildingIcons.Values)if(icon)Destroy(icon);
        authoredBuildingIcons.Clear();
    }
    Texture2D CatalogBuildingIcon(int race,int row,string raw){var generated=AuthoredBuildingIcon(raw);if(generated)return generated;if(race==BlackHand.Race){var icon=BlackHandBuildings.Icon(row);if(icon)return icon;row=BlackHand.ArtRow(row);}var revised=ContentResources.Load<Texture2D>("NerubianBuildingIcon/"+raw);if(revised)return revised;if(MobileContent.Active&&race==12)return UltimateIcon034(row);var pack=Pack(race);var icons=pack?pack.buildingIcons:buildingIcons;return raw=="h008"&&row>=icons.Length?buildingIcons[SharedChestRow]:icons[row];}
    Texture2D MatchBuildingIcon(int kind){string raw=match.Lines[kind].building;int race=raceIndices[matchRace].ContainsKey(raw)?matchRace:matchBotRace;return CatalogBuildingIcon(race,raceIndices[race][raw],raw);}
}
}
