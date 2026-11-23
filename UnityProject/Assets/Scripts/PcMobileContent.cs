using System;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    
    IEnumerator PrepareMobileRoom(string action,string code){MobileContent.Current.Begin();yield return MobileContent.Current.EnsureRace(networkRace);MobileContent.Current.Ready();EnterRoomCore(action,code);}
    void SetNetworkReady(bool ready){if(MobileContent.Active&&ready){StartCoroutine(PrepareMobileReady());return;}SendNetwork(new Newtonsoft.Json.Linq.JObject{["type"]="ready",["ready"]=ready});}
    IEnumerator PrepareMobileReady(){
        MobileContent.Current.Begin();
        var players=networkRoom?["players"] as Newtonsoft.Json.Linq.JArray;
        int[] races=players==null?new int[0]:players.OfType<Newtonsoft.Json.Linq.JObject>().Select(row=>Array.IndexOf(networkRaceNames,(string)row["race"])).Where(race=>race>=0).Distinct().ToArray();
        string mode=(string)networkRoom?["config"]?["mode"]??"fixed";
        if(mode=="random"){
            // Cache, but do not instantiate every possible race before the server
            // starts its finite loading timer. Decode only its actual draw later.
            yield return MobileContent.Current.CacheRaces(Enumerable.Range(0,networkRaceNames.Length));
            for(int race=0;race<networkRaceNames.Length;race++)yield return MobileContent.Current.EnsureMedia("Frontend/Versus/"+VersusKeys[race]+".png");
            races=Array.Empty<int>();
        }else if(mode=="mirror"&&players?.Count==2){int host=Array.IndexOf(networkRaceNames,(string)players[0]?["race"]);races=host<0?Array.Empty<int>():new[]{host};}
        for(int race=1;race<MobileContent.RaceResources.Length;race++)if(!races.Contains(race))AssignPack(race,null);
        yield return Resources.UnloadUnusedAssets();foreach(int race in races){yield return PrepareMobileRace(race);yield return MobileContent.Current.EnsureMedia("Frontend/Versus/"+VersusKeys[race]+".png");}
        MobileContent.Current.Ready();if(networkSlot>=0&&network!=null&&network.Connected)SendNetwork(new Newtonsoft.Json.Linq.JObject{["type"]="ready",["ready"]=true});
    }
    bool mobileRoundPrepared;
    void AssignPack(int race,PcRacePack value){switch(race){case 1:orcPack=value;break;case 2:nagaPack=value;break;case 3:naturePack=value;break;case 4:chaosPack=value;break;case 5:northernPack=value;break;case 6:mechPack=value;break;case 7:undeadPack=value;break;case 8:corruptedPack=value;break;case 9:elementalPack=value;break;case 10:nightPack=value;break;case 11:elvenPack=value;break;case 12:ultimatePack=value;if(!value){var state=MobileUltimate034.For(this);if(state.shell)Destroy(state.shell);state.shell=null;state.draw=null;}break;case 13:nerubianPack=value;break;case 14:pandarenPack=value;break;case 15:centaurPack=value;break;case PepeContent.Race:pepePack=value;break;}}
    IEnumerator PrepareMobileRace(int race){
        if(!MobileContent.Active)yield break;
        yield return MobileContent.Current.EnsureRace(race);
        if(race==BlackHand.Race)yield return BlackHandArt.Open();
        if(race==0||race==BlackHand.Race)yield break;
        if(race==PepeContent.Race)AssignPack(race,PepeContent.Load<PcRacePack>(PepeContent.Pack));
        else if(race==12){yield return PrepareUltimateShell034();}else if(!Pack(race)){yield return MobileContent.Current.LoadAsync<PcRacePack>(MobileContent.RaceResources[race],value=>AssignPack(race,value));if(!Pack(race))throw new InvalidOperationException("Missing downloaded race "+race);}
        ApplyRaceMaterialDefaults(race);
        Debug.Log("CF_MOBILE_RACE_READY race="+race);
    }
    IEnumerator PrepareMobileMatch(int a,int b){
        if(!MobileContent.Active)yield break;
        MobileContent.Current.Begin();
        ClearMatch();
        var ultimateState=MobileUltimate034.For(this);if(ultimateState.shell){Destroy(ultimateState.shell);ultimateState.shell=null;ultimatePack=null;}ultimateState.draw=null;
        yield return null;
        // Do not keep models of every previously played race alive.
        for(int race=1;race<MobileContent.RaceResources.Length;race++)if(race!=a&&race!=b)AssignPack(race,null);
        yield return Resources.UnloadUnusedAssets();
        yield return PrepareMobileRace(a);if(a!=b)yield return PrepareMobileRace(b);
    }
    IEnumerator PrepareMobileGuide(int race){
        MobileContent.Current.Begin();
        if(DiscardRaceAssets(race)){ClearRacePrefabCaches();yield return Resources.UnloadUnusedAssets();}
        yield return PrepareMobileRace(race);
        if(page==Page.Guide)GuideChooseRace(race);
        MobileContent.Current.Ready();
    }
    GameObject MenuBuilder(int race)=>race==BlackHand.Race?ClassicMenuBuilder(race):OptionalModel(ClassicMenuBuilder(race),0);
    GameObject ClassicMenuBuilder(int race){
        if(race==BlackHand.Race)return BlackHandArt.Builder(FirstModel(builderModels),0);
        if(race==0)return FirstModel(builderModels);
        if(race==PepeContent.Race)return PepeContent.Load<GameObject>("Pepe/Builder")??PepeContent.Load<PcRacePack>(PepeContent.Pack)?.builders[0];
        if(MobileContent.Active)return ContentResources.Load<GameObject>("MobileBuilders/"+race);
        if(desktopMenuBuilders)return desktopMenuBuilders.LoadAsset<GameObject>("builders/"+race);
        var pack=Pack(race);
        if(!pack)pack=ContentResources.Load<PcRacePack>(MobileContent.RaceResources[race]);
        return pack?FirstModel(pack.builders):FirstModel(builderModels);
    }
}
}
