using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    bool showNetworkRacePicker;
    JObject failedNetworkLoad;
    int[] offlineSetupBeforeNetwork;
    void SaveOfflineNetworkSetup(){if(offlineSetupBeforeNetwork==null)offlineSetupBeforeNetwork=new[]{raceChoice,botRaceChoice,matchMode,startGold,difficulty,roundsChoice,aspectsOn?1:0};}
    void RestoreOfflineNetworkSetup(){if(offlineSetupBeforeNetwork==null)return;var saved=offlineSetupBeforeNetwork;offlineSetupBeforeNetwork=null;raceChoice=saved[0];botRaceChoice=saved[1];matchMode=saved[2];startGold=saved[3];difficulty=saved[4];roundsChoice=saved[5];aspectsOn=saved[6]==1;}
    struct NetworkSight {public Vector2 position;public float radius;}
    readonly List<NetworkSight> networkSight=new List<NetworkSight>();
    readonly Dictionary<int,JObject> networkUnitCards=new Dictionary<int,JObject>(),networkBuildingCards=new Dictionary<int,JObject>();
    readonly float[] networkDamageBoost=new float[2];
    void ApplyNetworkCards(JObject packet){
        networkSpellStamp=Time.unscaledTime;
        networkUnitCards.Clear();networkBuildingCards.Clear();
        if(packet["banks"] is JArray banks&&banks.Count==2)for(int team=0;team<2;team++)networkDamageBoost[team]=(float?)banks[team==0?networkSlot:1-networkSlot]?["boost"]??0;
        if(packet["units"] is JArray actors)foreach(var row in actors.OfType<JObject>())networkUnitCards[(int?)row["id"]??0]=row;
        if(packet["huts"] is JArray buildings)foreach(var row in buildings.OfType<JObject>())networkBuildingCards[(int?)row["id"]??0]=row;
    }
    static float NetworkStatusValue(JObject row,string key)=>HumanMatch.F(row["aura_stats"],key)+HumanMatch.F(row["aura_stats"],key+"_negative")+HumanMatch.F(row["stats"],key)+HumanMatch.F(row["passive"],key);
    float NetworkCardStat(int id,string key,float fallback,bool building=false){
        var table=building?networkBuildingCards:networkUnitCards;if(!onlineMatch||!table.TryGetValue(id,out var row))return fallback;
        var state=row["state"];if(building)return HumanMatch.F(row,key,fallback);
        if(key=="armor")return HumanMatch.F(row,"armor",fallback)+HumanMatch.F(state,"buff_armor")+HumanMatch.F(state,"item_armor")+NetworkStatusValue(row,"armor");
        if(key=="damage"){
            var aspect=state?["aspect_info"];float flat=HumanMatch.F(aspect,"stacks")*((string)aspect?["id"]=="chaos_seal"?2:4);
            var unit=match.Units.FirstOrDefault(actor=>actor.id==id);int team=unit?.team??((int?)row["t"]==networkSlot?0:1);float inventory=match.ItemPassive(team,"damage_bonus",unit);
            float weapon=row["weapon_damage"] is JArray weapons&&weapons.Count>0?(float)weapons[0]:HumanMatch.F(row,"damage",fallback);
            return (weapon+flat)*(1+inventory+networkDamageBoost[team])*(1+HumanMatch.F(aspect?["mods"],"damage"))*HumanMatch.F(state,"buff_mult",1)*Mathf.Max(0,1+NetworkStatusValue(row,"damage"));
        }
        return HumanMatch.F(row,key,fallback);
    }
    void DrawLobbyChevron(Rect rect,bool enabled){
        if(Event.current.type!=EventType.Repaint)return;
        var color=enabled?new Color(.86f,.76f,.54f):new Color(.42f,.46f,.44f);
        for(int y=0;y<5;y++)Fill(new Rect(rect.x+y,rect.y+y,10-2*y,1),color);
    }
    void DrawNetworkRacePicker(){
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.8f));LobbyPanel(new Rect(185,76,910,572));
        Text(215,96,620,48,"ВЫБЕРИТЕ РАСУ",heading);
        if(LobbyButton("network.race.close",new Rect(900,98,165,42),"Закрыть"))showNetworkRacePicker=false;
        for(int i=0;i<networkRaceNames.Length;i++){
            int col=i%3,row=i/3;var rect=new Rect(215+col*287,166+row*85,267,66);
            if(LobbyButton("network.race.select."+i,rect,networkRaceLabels[i],i==networkRace))ChooseNetworkRace(i);
        }
    }
    bool ValidateNetworkLoadouts(int a,int b,JArray loadouts){
        if(loadouts==null||loadouts.Count!=2||!ultimatePack)return false;
        var draw=new UltimateLoadout((JArray)JObject.Parse(ultimatePack.catalog.text)["ultimateSlots"],n=>0);
        return (a!=12||draw.Valid(loadouts[networkSlot]))&&(b!=12||draw.Valid(loadouts[1-networkSlot]));
    }
    void FailNetworkLoad(string message){
        Debug.LogError("CF_NETWORK_LOAD_FAILED "+message);CloseVersus();onlineMatch=false;onlineStarted=false;onlineReplica=null;
        ClearMatch();page=Page.Network;networkLoadError=directoryStatus=message;
    }
    void DrawNetworkLoadError(){
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.85f));LobbyPanel(new Rect(300,205,680,310));
        Text(330,225,610,48,"НЕ УДАЛОСЬ ЗАГРУЗИТЬ БОЙ",new GUIStyle(heading){fontSize=25});
        Text(330,285,610,100,networkLoadError,label);
        if(LobbyButton("network.load.retry",new Rect(330,422,298,54),"Повторить",true)&&failedNetworkLoad!=null&&network!=null&&network.Connected){networkLoadError="";NetworkLoad(failedNetworkLoad);}
        if(LobbyButton("network.load.leave",new Rect(650,422,298,54),"Выйти из комнаты")){StopDirectory();BeginDirectory();}
    }
    // Unity normally logs an iterator exception and silently abandons loading.
    // Own nested iterators so resource/scene errors remain visible and retryable.
    IEnumerator GuardNetworkLoad(IEnumerator routine){
        var pending=new Stack<IEnumerator>();pending.Push(routine);
        try{
            while(pending.Count>0){
                object current=null;bool moved=false;Exception failure=null;
                try{moved=pending.Peek().MoveNext();if(moved)current=pending.Peek().Current;}catch(Exception e){failure=e;}
                if(failure!=null){FailNetworkLoad("Ошибка подготовки боя: "+failure.GetBaseException().Message);yield break;}
                if(!moved){(pending.Pop() as IDisposable)?.Dispose();continue;}
                if(current is IEnumerator child)pending.Push(child);else yield return current;
            }
        }finally{while(pending.Count>0)(pending.Pop() as IDisposable)?.Dispose();}
    }
    void ApplyNetworkSight(JObject packet){
        networkSight.Clear();
        foreach(string key in new[]{"north_sight","night_sight"})if(packet[key] is JArray rows)foreach(var row in rows.OfType<JObject>()){
            if((int?)row["team"]!=networkSlot||!(row["p"] is JArray p)||p.Count<3)continue;
            networkSight.Add(new NetworkSight{position=new Vector2((networkSlot==1?-1:1)*(float)p[0],(float)p[2]),radius=(key=="north_sight"?700:1100)/30f});
        }
    }
}
}
