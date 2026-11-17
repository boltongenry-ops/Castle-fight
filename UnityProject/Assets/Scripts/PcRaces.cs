using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    PcRacePack orcPack,nagaPack,naturePack,chaosPack,northernPack,mechPack,undeadPack,corruptedPack,elementalPack,nightPack;int raceChoice,matchRace,botRaceChoice=-1,matchBotRace;
    readonly Dictionary<string,int>[] raceIndices=Enumerable.Range(0,MobileContent.RaceResources.Length).Select(_=>new Dictionary<string,int>()).ToArray();
    PcRacePack Pack(int race)=>race==1?orcPack:race==2?nagaPack:race==3?naturePack:race==4?chaosPack:race==5?northernPack:race==6?mechPack:race==7?undeadPack:race==8?corruptedPack:race==9?elementalPack:race==10?nightPack:race==11?elvenPack:race==12?ultimatePack:race==13?nerubianPack:race==14?pandarenPack:race==15?centaurPack:null;
    string RaceCatalog(int race){
        if(race==0)return StructureCatalog(0,catalog.text);
        var pack=Pack(race);
        var asset=MobileContent.Active?ContentResources.Load<TextAsset>("MobileCatalogs/"+race):
            pack?pack.catalog:desktopMenuBuilders?desktopMenuBuilders.LoadAsset<TextAsset>("catalogs/"+race):null;
        if(!asset&&!MobileContent.Active)asset=Resources.Load<TextAsset>("pc-"+DesktopRaceCatalogs[race]);
        if(!asset)throw new InvalidOperationException("Missing race catalog "+race);
        return StructureCatalog(race,asset.text);
    }
    void PrepareRaceIndices(){for(int race=0;race<raceIndices.Length;race++){raceIndices[race].Clear();if(race!=0&&!Pack(race)&&!MobileContent.Active)continue;var j=JObject.Parse(RaceCatalog(race));int i=0;foreach(var line in (JArray)j["lines"])raceIndices[race][(string)line["building"]]=i++;}}
    GameObject RaceModel(int team,int kind,bool building){int race=team==0?matchRace:matchBotRace;string key=match.Lines[kind].building;if(!raceIndices[race].ContainsKey(key)){int other=team==0?matchBotRace:matchRace;if(raceIndices[other].ContainsKey(key))race=other;}return raceIndices[race].TryGetValue(key,out int index)?CatalogModel(race,team,index,key,building):null;}
    GameObject RaceBuilder(int team){var pack=Pack(team==0?matchRace:matchBotRace);return pack?pack.builders[team]:builderModels[team];}
    string BotRaceName=>matchBotRace==15?"Кентавры":matchBotRace==12?"Высший строитель":matchBotRace==13?"Нерубы":matchBotRace==14?"Панды":matchBotRace==1?"Орки":matchBotRace==2?"Наги":matchBotRace==3?"Природа":matchBotRace==4?"Хаос":matchBotRace==5?"Северяне":matchBotRace==11?"Союз эльфов":matchBotRace==10?"Ночные эльфы":matchBotRace==9?"Элементали":matchBotRace==8?"Осквернённые":matchBotRace==7?"Нежить":matchBotRace==6?"Механизмы":"Люди";
    bool OrcsInMatch=>match!=null&&matchRace==1;
    string RaceName=>matchRace==15?"Кентавры":matchRace==12?"Высший строитель":matchRace==13?"Нерубы":matchRace==14?"Панды":matchRace==1?"Орки":matchRace==2?"Наги":matchRace==3?"Природа":matchRace==4?"Хаос":matchRace==5?"Северяне":matchRace==11?"Союз эльфов":matchRace==10?"Ночные эльфы":matchRace==9?"Элементали":matchRace==8?"Осквернённые":matchRace==7?"Нежить":matchRace==6?"Механизмы":"Люди";
    string RaceEnglish=>matchRace==15?"Centaur Khanates":matchRace==12?"Ultimate Builder":matchRace==13?"Nerubian Swarm":matchRace==14?"Pandaren Order":matchRace==1?"Orcish Horde":matchRace==2?"Naga Faction":matchRace==3?"Nature Force":matchRace==4?"Chaos Legions":matchRace==5?"Northern Realms":matchRace==11?"Elven Union":matchRace==10?"Night Elfs":matchRace==9?"Elementals' Universe":matchRace==8?"Corrupted Conglomerate":matchRace==7?"Undead Forces":matchRace==6?"Mech Coalition":"Human Alliance";
    string BuilderRaw=>matchRace==15?"cBld":matchRace==12?"h075":matchRace==13?"hUAv":matchRace==14?"hPBu":matchRace==1?"h019":matchRace==2?"h00E":matchRace==3?"h01A":matchRace==4?"h00O":matchRace==5?"h017":matchRace==11?"h00P":matchRace==10?"h089":matchRace==9?"h051":matchRace==8?"u006":matchRace==7?"h018":matchRace==6?"h06P":"h00C";
    Texture2D[] BuildingIcons=>Pack(matchRace)?Pack(matchRace).buildingIcons:buildingIcons;
    Texture2D CommandIcon(int index){var pack=Pack(matchRace);return HdIcon(pack&&index==0?pack.builderIcon:pack&&index==6?pack.buildIcon:commandIcons[index]);}
}
}
