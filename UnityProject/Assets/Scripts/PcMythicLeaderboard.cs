using System;
using System.Collections;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace CastleFight {
public sealed partial class PcGame {
    sealed class MythicScore {
        public int race, timeMs;
        public string token,challenge;
    }

    const string MythicLeaderboardPath="/mythic-leaderboard";
    const string MythicGuestName="cf-mythic-guest-name";
    const string MythicGuestId="cf-mythic-guest-id";
    const int MythicRowsPerPage=8;
    MythicScore pendingMythicScore;
    JArray mythicLeaderboard=new JArray();
    GUIStyle mythicNicknameStyle,mythicRaceStyle,mythicBoardHeading;
    Texture2D mythicCrown;
    bool mythicBoardLoading,mythicScoreSending,mythicGuestPrompt;
    int mythicBoardPage,mythicPlace;
    string mythicBoardError="",mythicScoreError="",mythicNickname="";

    string MythicLeaderboardUrl(){
        if(testing)return BattleLab.Arg("--pc-mythic-board");
        return new UriBuilder(NetworkAccountStore.UnityEndpoint){
            Scheme="https",Port=-1,Path=MythicLeaderboardPath,Query=""
        }.Uri.AbsoluteUri;
    }

    void CaptureMythicScore(){
        if(!CurrentMythic||!match.ChallengeVictoryEligible||MythicLeaderboardUrl().Length==0)return;
        string token=!networkGuest&&NetworkAccountStore.SameService(networkSessionEndpoint,NetworkAccountStore.UnityEndpoint)?
            networkSessionToken:"";
        pendingMythicScore=new MythicScore{challenge=(string)activeChallenge["id"],race=matchRace,timeMs=Mathf.Max(1000,Mathf.RoundToInt(match.Elapsed*1000)),token=token};
        mythicPlace=0;mythicScoreError="";
        mythicNickname=testing?"":PlayerPrefs.GetString(MythicGuestName,"");
        if(string.IsNullOrEmpty(token))mythicGuestPrompt=true;
        else StartCoroutine(SendMythicScore(pendingMythicScore));
    }

    static JObject MythicReply(UnityWebRequest request){
        try{return JObject.Parse(request.downloadHandler.text);}
        catch(Newtonsoft.Json.JsonException){return null;}
    }

    string selectedMythicId=HumanMatch.WinterTrial;
    int mythicRequestGeneration;
    void SelectMythicTrial(string id){
        if(selectedMythicId==id)return;
        selectedMythicId=id;
        mythicRequestGeneration++;
        mythicBoardLoading=false;
        mythicLeaderboard=new JArray();
        mythicBoardError="";
        mythicBoardPage=mythicPlace=0;
        StartCoroutine(LoadMythicLeaderboard());
    }

    IEnumerator LoadMythicLeaderboard(){
        if(mythicBoardLoading)yield break;
        string url=MythicLeaderboardUrl();
        if(string.IsNullOrEmpty(url))yield break;
        mythicBoardLoading=true;mythicBoardError="";
        int generation=mythicRequestGeneration;
        using(var request=UnityWebRequest.Get(url+"?challenge="+Uri.EscapeDataString(selectedMythicId))){
            request.timeout=10;
            yield return request.SendWebRequest();
            if(generation!=mythicRequestGeneration)yield break;
            var reply=MythicReply(request);
            if(request.result==UnityWebRequest.Result.Success&&(string)reply?["challenge"]==selectedMythicId&&reply?["entries"] is JArray entries)
                mythicLeaderboard=entries;
            else mythicBoardError="Таблица временно недоступна";
        }
        mythicBoardLoading=false;
    }

    IEnumerator SendMythicScore(MythicScore score){
        if(mythicScoreSending||score==null)yield break;
        string url=MythicLeaderboardUrl();
        if(string.IsNullOrEmpty(url))yield break;
        mythicScoreError="";
        JObject payload=new JObject{["challenge"]=score.challenge,["race"]=score.race,["time_ms"]=score.timeMs};
        if(score.challenge==HumanMatch.WinterTrial)payload["rules_revision"]=HumanMatch.WinterRulesRevision;
        if(!string.IsNullOrEmpty(score.token))payload["token"]=score.token;
        else{
            string name=string.Join(" ",mythicNickname.Trim().Split((char[])null,StringSplitOptions.RemoveEmptyEntries));
            if(name.Length<2||name.Length>24||name.Any(c=>char.IsControl(c)||c=='<'||c=='>')){
                mythicScoreError="Введите ник длиной от 2 до 24 символов.";
                yield break;
            }
            mythicNickname=name;
            string guest=testing?"":PlayerPrefs.GetString(MythicGuestId,"");
            if(!Guid.TryParseExact(guest,"N",out _))guest=Guid.NewGuid().ToString("N");
            if(!testing){
                PlayerPrefs.SetString(MythicGuestId,guest);
                PlayerPrefs.SetString(MythicGuestName,name);
                PlayerPrefs.Save();
            }
            payload["guest"]=guest;payload["nickname"]=name;
            CloseAccountKeyboard();
        }
        mythicScoreSending=true;
        using(var request=new UnityWebRequest(url,"POST")){
            request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload.ToString(Newtonsoft.Json.Formatting.None)));
            request.downloadHandler=new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type","application/json");
            request.timeout=10;
            yield return request.SendWebRequest();
            var reply=MythicReply(request);
            if(request.result==UnityWebRequest.Result.Success&&(bool?)reply?["ok"]==true&&(string)reply["challenge"]==score.challenge&&reply["entries"] is JArray entries){
                if(selectedMythicId==score.challenge){mythicLeaderboard=entries;mythicBoardError="";}
                if(pendingMythicScore==score){
                    mythicPlace=selectedMythicId==score.challenge?((int?)reply["place"]??0):0;
                    pendingMythicScore=null;
                    mythicGuestPrompt=false;
                }
            }else if(pendingMythicScore==score){
                var error=(string)reply?["error"];
                if(error=="session_expired"){
                    score.token="";
                    mythicScoreError="Сессия истекла. Для этой записи можно ввести гостевой ник.";
                    mythicGuestPrompt=page==Page.Result;
                }else if(error=="outdated_winter_rules"){
                    mythicScoreError="Обновите игру и пройдите испытание заново, чтобы записать результат.";
                }else{
                    mythicScoreError=error=="invalid_nickname"?"Проверьте ник и повторите запись.":"Сервер не ответил. Повторите запись.";
                }
            }
        }
        mythicScoreSending=false;
    }

    void SubmitPendingMythicScore(){
        if(pendingMythicScore==null||mythicScoreSending)return;
        if(string.IsNullOrEmpty(pendingMythicScore.token))mythicGuestPrompt=true;
        else StartCoroutine(SendMythicScore(pendingMythicScore));
    }

    static string MythicTime(int milliseconds){
        var time=TimeSpan.FromMilliseconds(milliseconds);
        return ((int)time.TotalMinutes)+":"+time.Seconds.ToString("00")+"."+time.Milliseconds.ToString("000");
    }

    void DrawMythicStandingPrompt(){
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.78f));
        LobbyPanel(new Rect(338,178,604,388));
        Text(370,203,540,47,"Запись в таблицу лидеров",heading);
        Text(370,258,540,35,RaceTitle(pendingMythicScore.race)+" · "+MythicTime(pendingMythicScore.timeMs),label);
        mythicNickname=AccountText("mythic.nickname",new Rect(370,310,540,57),mythicNickname,24,false,"Ник для таблицы");
        if(mythicScoreError.Length>0)
            Text(370,378,540,55,mythicScoreError,new GUIStyle(small){wordWrap=true});
        bool enabled=GUI.enabled;GUI.enabled=enabled&&!mythicScoreSending;
        if(LobbyButton("mythic.score.save",new Rect(660,476,250,57),mythicScoreSending?"Сохраняем…":"Записать"))StartCoroutine(SendMythicScore(pendingMythicScore));
        if(LobbyButton("mythic.score.later",new Rect(370,476,235,57),"Позже")){
            mythicGuestPrompt=false;
            CloseAccountKeyboard();
        }
        GUI.enabled=enabled;
    }

    void DrawMythicChallenges(){
        var trials=(JArray)progression.Catalog["mythic"];
        const int stride=85;
        Workshop.maxChallengeScroll=Mathf.Max(0,trials.Count*stride-500);
        bool scrolling=Workshop.maxChallengeScroll>0;
        var area=new Rect(42,176,593,500);
        float width=area.width-(scrolling?20:0);
        if(scrolling)progressionScroll=GUI.BeginScrollView(area,progressionScroll,new Rect(0,0,width,trials.Count*stride));
        bool enabled=GUI.enabled;
        GUI.enabled=enabled&&!Workshop.dragged;
        for(int i=0;i<trials.Count;i++)
            DrawMythicTrialRow((JObject)trials[i],new Rect(scrolling?0:area.x,(scrolling?0:area.y)+i*stride,width,69));
        GUI.enabled=enabled;
        if(scrolling)GUI.EndScrollView();
        DrawMythicLeaderboard();
    }

    void DrawMythicLeaderboard(){
        LobbyPanel(new Rect(669,176,570,500));
        if(mythicBoardHeading==null){
            mythicBoardHeading=new GUIStyle(heading){alignment=TextAnchor.MiddleLeft,padding=new RectOffset()};
            mythicNicknameStyle=new GUIStyle(label){fontSize=17,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(),clipping=TextClipping.Clip};
            mythicRaceStyle=new GUIStyle(small){fontSize=13,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(),clipping=TextClipping.Clip};
        }
        Text(689,186,393,48,"Таблица лидеров",mythicBoardHeading);
        if(LobbyButton("mythic.board.refresh",new Rect(1090,188,126,38),"Обновить"))StartCoroutine(LoadMythicLeaderboard());
        Text(689,226,520,25,(string)progression.Challenge(selectedMythicId)?["title"],small);
        if(mythicLeaderboard.Count==0)
            Text(689,256,524,55,mythicBoardLoading?"Загружаем результаты…":mythicBoardError!=""?mythicBoardError:"Результатов пока нет",label);
        int pages=Mathf.Max(1,Mathf.CeilToInt(mythicLeaderboard.Count/(float)MythicRowsPerPage));
        mythicBoardPage=Mathf.Clamp(mythicBoardPage,0,pages-1);
        for(int i=0;i<MythicRowsPerPage;i++){
            int position=mythicBoardPage*MythicRowsPerPage+i;
            if(position>=mythicLeaderboard.Count)break;
            var row=mythicLeaderboard[position];
            float y=252+i*44;
            Fill(new Rect(688,y,528,44),new Color(.06f,.095f,.11f,i%2==0?.72f:.38f));
            int place=(int?)row["place"]??position+1;
            Text(695,y+3,43,31,place.ToString(),small);
            if(place==1){
                if(!mythicCrown)mythicCrown=MakeMythicCrown();
                GUI.DrawTexture(new Rect(711,y+9,21,19),mythicCrown,ScaleMode.ScaleToFit);
            }
            Text(742,y+1,304,26,(string)row["nickname"]??"",mythicNicknameStyle);
            int race=(int?)row["race"]??-1;
            Text(742,y+24,304,18,RaceTitle(race),mythicRaceStyle);
            Text(1052,y+4,153,30,MythicTime((int?)row["time_ms"]??0),label);
        }
        if(pages>1){
            if(mythicBoardPage>0&&LobbyButton("mythic.board.prev",new Rect(689,610,118,30),"Назад"))mythicBoardPage--;
            Text(822,610,182,30,(mythicBoardPage+1)+" / "+pages,small);
            if(mythicBoardPage+1<pages&&LobbyButton("mythic.board.next",new Rect(1098,610,118,30),"Вперёд"))mythicBoardPage++;
        }
        if(pendingMythicScore!=null&&selectedMythicId==pendingMythicScore.challenge&&LobbyButton("mythic.board.submit",new Rect(689,642,526,33),
            mythicScoreSending?"Сохраняем…":"Записать своё время"))SubmitPendingMythicScore();
        else if(mythicPlace>0)Text(689,641,526,34,"Ваше место · "+mythicPlace,small);
        else if(mythicBoardError!=""&&mythicLeaderboard.Count>0)Text(689,641,526,34,mythicBoardError,small);
    }
    static Texture2D MakeMythicCrown(){
        const int width=48,height=40;
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name="Mythic first place crown",
            wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        var pixels=new Color32[width*height];
        var peaks=new[]{.70f,.41f,.92f,.41f,.70f};
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){
            float u=(x+.5f)/width,v=(y+.5f)/height;
            float span=Mathf.InverseLerp(.09f,.91f,u)*4;
            int segment=Mathf.Min(3,Mathf.FloorToInt(span));
            float crest=Mathf.Lerp(peaks[segment],peaks[segment+1],span-segment);
            float left=Mathf.Lerp(.20f,.05f,Mathf.InverseLerp(.18f,.70f,v));
            if(v<.14f||v>crest||u<left||u>1-left)continue;
            float light=Mathf.InverseLerp(.14f,.9f,v);
            var gold=Color.Lerp(new Color(.69f,.35f,.06f),new Color(1,.88f,.45f),light);
            if(v>=.21f&&v<.29f)gold=new Color(.97f,.68f,.17f);
            if(Mathf.Abs(u-.5f)*1.4f+Mathf.Abs(v-.40f)<.070f)gold=new Color(1,.92f,.62f);
            pixels[y*width+x]=gold;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false,true);
        return texture;
    }
}
}
