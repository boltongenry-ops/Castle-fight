using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace CastleFight {
public sealed partial class PcGame {
    JObject[] profileChallenges;
    HashSet<string> ownMedals=new HashSet<string>(StringComparer.Ordinal);
    HashSet<string> peerMedals=new HashSet<string>(StringComparer.Ordinal);
    string medalOwner="",medalPeer="",medalSyncStatus="";
    bool medalSyncBusy,peerMedalsLoading,peerMedalsError,showPeerMedals,showAllProfileChallenges;
    int profileMedalPage,peerMedalPage,medalPeerRequest;
    const int MedalsPerPage=8;

    string AchievementToken=>!networkGuest&&NetworkAccountStore.SameService(networkSessionEndpoint,NetworkAccountStore.UnityEndpoint)
        ?networkSessionToken:"";
    string AccountMedalOwner=>(string)networkProfile?["id"]??"";
    JObject[] ProfileChallenges=>profileChallenges??(profileChallenges=
        ((JArray)progression.Catalog["challenges"]).OfType<JObject>()
        .Concat(((JArray)progression.Catalog["mythic"]).OfType<JObject>()).ToArray());

    void OpenAccountProfile(){
        page=Page.Profile;profileMedalPage=0;showAllProfileChallenges=false;medalSyncStatus="";
        if(HasNetworkProfile)StartCoroutine(SyncProfileMedals(Array.Empty<string>()));
        else if(!string.IsNullOrEmpty(AchievementToken))BeginDirectory();
    }

    void ProfileAuthChanged(){
        ownMedals.Clear();medalOwner="";medalSyncStatus="";
        if(page==Page.Profile&&HasNetworkProfile){
            showNetworkProfile=false;CloseAccountKeyboard();
            StartCoroutine(SyncProfileMedals(Array.Empty<string>()));
        }
    }

    void ShareChallengeVictory(string id){
        if(!string.IsNullOrEmpty(AchievementToken))
            StartCoroutine(SyncProfileMedals(new[]{id}));
    }

    IEnumerator SyncProfileMedals(string[] earned){
        string token=AchievementToken,url=MythicLeaderboardUrl();
        if(string.IsNullOrEmpty(token)||string.IsNullOrEmpty(url))yield break;
        while(medalSyncBusy&&token==AchievementToken)yield return null;
        if(token!=AchievementToken)yield break;
        medalSyncBusy=true;
        var data=new JObject{["action"]="sync_achievements",["achievements"]=new JArray(earned)};
        using(var req=new UnityWebRequest(url,"POST")){
            req.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(data.ToString(Newtonsoft.Json.Formatting.None)));
            req.downloadHandler=new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type","application/json");
            req.SetRequestHeader("Authorization","Bearer "+token);
            req.timeout=10;
            yield return req.SendWebRequest();
            if(token==AchievementToken){
                var reply=MythicReply(req);
                if(req.result==UnityWebRequest.Result.Success&&reply?["achievements"] is JArray medals){
                    medalOwner=(string)reply["player"]??"";ownMedals=MedalIds(medals);
                    medalSyncStatus=earned.Length>0?"Достижения опубликованы в профиле.":"";
                }else medalSyncStatus=req.responseCode==401?"Войдите в аккаунт снова.":"Сервер достижений временно недоступен.";
            }
        }
        medalSyncBusy=false;
    }

    static HashSet<string> MedalIds(JArray values)=>new HashSet<string>(
        values.Where(value=>value.Type==JTokenType.String).Select(value=>(string)value),StringComparer.Ordinal);

    IEnumerator FetchPeerMedals(string player){
        string url=MythicLeaderboardUrl();
        int generation=++medalPeerRequest;
        medalPeer=player;peerMedals.Clear();peerMedalsError=false;
        if(string.IsNullOrEmpty(url)){peerMedalsLoading=false;peerMedalsError=true;yield break;}
        peerMedalsLoading=true;
        using(var req=UnityWebRequest.Get(url+"?profile="+Uri.EscapeDataString(player))){
            req.timeout=10;
            yield return req.SendWebRequest();
            if(generation!=medalPeerRequest)yield break;
            var reply=MythicReply(req);
            if(req.result==UnityWebRequest.Result.Success&&(string)reply?["player"]==player&&reply?["achievements"] is JArray medals)
                peerMedals=MedalIds(medals);
            else peerMedalsError=true;
        }
        peerMedalsLoading=false;
    }

    void DrawAccountProfile(){
        DrawMenuBackground();EnsureRaceStyles();
        if(showNetworkProfile){DrawNetworkProfile();return;}
        LobbyPanel(new Rect(20,18,1240,684));
        DrawProfileBadge(new Rect(45,39,72,72));
        Text(137,35,740,43,HasNetworkProfile?(string)networkProfile["name"]??"Профиль":"Профиль игрока",heading);
        int shared=medalOwner==AccountMedalOwner?ownMedals.Count:0;
        int complete=ProfileChallenges.Count(row=>IsProfileChallengeComplete((string)row["id"]));
        string rank=HasNetworkProfile?RankNames[RankIndex((int?)networkProfile["wins"]??0)]+" · ":"";
        Text(139,81,880,31,rank+complete+" / "+ProfileChallenges.Length+" пройдено"+(HasNetworkProfile?" · "+shared+" опубликовано":""),label);
        if(LobbyButton("account-profile.back",new Rect(1054,42,181,48),"← В меню")){MainMenu();return;}

        if(LobbyButton("account-profile.earned",new Rect(45,135,235,46),"Достижения",!showAllProfileChallenges)){
            showAllProfileChallenges=false;profileMedalPage=0;
        }
        if(LobbyButton("account-profile.all",new Rect(293,135,266,46),"Все испытания",showAllProfileChallenges)){
            showAllProfileChallenges=true;profileMedalPage=0;
        }
        if(HasNetworkProfile){
            GUI.enabled=!medalSyncBusy;
            if(LobbyButton("account-profile.sync",new Rect(694,135,306,46),medalSyncBusy?"Синхронизация…":"Опубликовать с устройства")){
                var earned=ProfileChallenges.Select(row=>(string)row["id"]).Where(progression.Completed).ToArray();
                StartCoroutine(SyncProfileMedals(earned));
            }
            GUI.enabled=true;
        }
        if(LobbyButton("account-profile.login",new Rect(1012,135,223,46),HasNetworkProfile?"Аккаунт":"Войти в аккаунт")){
            showNetworkProfile=true;profileName=HasEndpointToken?networkName:"";
            networkGuest=false;networkEntryAccount=false;
            if(!directoryReady)BeginDirectory();
        }
        string status=!string.IsNullOrEmpty(medalSyncStatus)?medalSyncStatus:HasNetworkProfile?
            "Опубликуйте старые прохождения с этого устройства. Новые победы публикуются при входе в аккаунт.":
            "Достижения остаются на устройстве. Войдите в аккаунт, чтобы их видели другие игроки.";
        Text(48,190,1182,31,status,small);
        var rows=showAllProfileChallenges?ProfileChallenges:ProfileChallenges.Where(row=>IsProfileChallengeComplete((string)row["id"])).ToArray();
        DrawMedalGrid(rows,232,98,ref profileMedalPage,true);
    }

    bool IsProfileChallengeComplete(string id)=>progression.Completed(id)||IsPublishedMedal(id);
    bool IsPublishedMedal(string id)=>HasNetworkProfile&&medalOwner==AccountMedalOwner&&ownMedals.Contains(id);

    void DrawMedalGrid(JObject[] rows,float top,float height,ref int currentPage,bool own){
        EnsureRaceStyles();
        int pages=Math.Max(1,(rows.Length+MedalsPerPage-1)/MedalsPerPage);
        currentPage=Mathf.Clamp(currentPage,0,pages-1);
        if(rows.Length==0){Text(54,top+10,1170,55,own?"Пройдите испытание, чтобы получить первую награду.":"Опубликованных достижений пока нет.",label);return;}
        var titleStyle=new GUIStyle(raceTitleStyle){fontSize=19,wordWrap=true,padding=new RectOffset()};
        var detailStyle=new GUIStyle(small){fontSize=15,wordWrap=false,padding=new RectOffset(),alignment=TextAnchor.MiddleLeft};
        for(int slot=0;slot<MedalsPerPage&&currentPage*MedalsPerPage+slot<rows.Length;slot++){
            var row=rows[currentPage*MedalsPerPage+slot];
            string id=(string)row["id"];
            bool earned=!own||IsProfileChallengeComplete(id);
            bool mythic=id.StartsWith("mythic_",StringComparison.Ordinal);
            bool siege=ChallengeCampaign038.IsSiege(row);
            var color=mythic?new Color(.43f,.77f,.92f):siege?new Color(.70f,.62f,.90f):new Color(.82f,.67f,.38f);
            if(!earned)color=new Color(.31f,.37f,.39f);
            float x=45+(slot%2)*600,y=top+(slot/2)*(height+8);
            var card=new Rect(x,y,590,height);
            Fill(card,new Color(.035f,.064f,.078f));Border(card,color*.70f);
            Fill(new Rect(x+2,y+2,3,height-4),color);
            var sign=new Rect(x+13,y+8,height-16,height-16);
            Fill(sign,new Color(.024f,.035f,.046f));Border(sign,color);
            var emblem=RaceEmblem(Array.IndexOf(ProgressRaces,(string)row[mythic?"opponent":"race"]));
            if(emblem){var old=GUI.color;if(!earned)GUI.color=new Color(.58f,.61f,.63f,.72f);GUI.DrawTexture(new Rect(sign.x+3,sign.y+3,sign.width-6,sign.height-6),emblem,ScaleMode.ScaleToFit);GUI.color=old;}
            Text(x+height+9,y+5,560-height,height-33,(string)row["title"],titleStyle);
            string type=mythic?"Эпохальное":siege?"Осадное":"Кампания";
            string state=earned?!own||IsPublishedMedal(id)?"Получено":"На устройстве":"Не пройдено";
            float best=own?progression.Best(id):0;
            Text(x+height+9,y+height-33,560-height,26,type+" · "+state+(best>0?" · "+TimeSpan.FromSeconds(best).ToString(best>=3600?@"h\:mm\:ss":@"mm\:ss"):""),detailStyle);
        }
        if(pages<=1)return;
        float nav=top+4*(height+8)+5;
        string key=own?"account-profile.page":"player.medals.page";
        if(currentPage>0&&LobbyButton(key+".back",new Rect(45,nav,188,32),"← Назад"))currentPage--;
        Text(526,nav,230,32,"Страница "+(currentPage+1)+" / "+pages,small);
        if(currentPage+1<pages&&LobbyButton(key+".next",new Rect(1047,nav,188,32),"Дальше →"))currentPage++;
    }
}
}
