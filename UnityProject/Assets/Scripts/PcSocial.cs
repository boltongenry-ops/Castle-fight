using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    string networkSessionToken="",networkSessionEndpoint="";float profileDeadline;bool socialTouchMoved;Vector2 socialTouchOrigin;int socialTab;bool socialSupported,socialPending;float socialDue,socialDeadline,socialNext;
    string socialSearch="",socialPeer="",socialDraft="",socialNickname="",socialViewed="",socialConfirm="",socialStickerPeer="";
    JObject socialQueued;string socialRequest="";JObject socialState=new JObject(),socialPlayer;JArray socialFound=new JArray(),socialMessages=new JArray(),socialMatches=new JArray();
    Vector2 socialScroll,messageScroll,matchScroll;bool socialMore;long socialRead;Texture2D rankSheet,castleLogo;
    static readonly int[] RankThresholds={0,3,10,25,50,100};
    static readonly string[] RankNames={"Ополченец","Защитник","Страж","Воевода","Хранитель цитадели","Владыка замков"};
    bool HasNetworkProfile=>networkProfile!=null&&networkProfile.HasValues;
    sealed class SocialNotification {
        public JToken Sender;
        public long Id;
        public string Text,Room,Preview;
        public bool Invitation=>!string.IsNullOrEmpty(Room);
    }
    sealed class NotificationHistory {
        public string Peer;
        public long Last;
        public int Unread;
    }
    readonly Queue<SocialNotification> socialNotifications=new Queue<SocialNotification>();
    readonly Dictionary<string,long> notificationSeen=new Dictionary<string,long>();
    readonly Dictionary<string,long> notificationScanned=new Dictionary<string,long>();
    readonly Queue<NotificationHistory> notificationHistory=new Queue<NotificationHistory>();
    readonly HashSet<long> declinedInvitations=new HashSet<long>();
    SocialNotification activeNotification;
    NotificationHistory notificationReading;
    float nextNotificationState,notificationAge;
    bool notificationDecision;
    AudioSource notificationSource;
    AudioClip notificationChime;
    GUIStyle notificationName,notificationPreview,notificationKind;

    void ResetSocialNotifications(){
        socialNotifications.Clear();
        notificationSeen.Clear();
        notificationScanned.Clear();
        notificationHistory.Clear();
        declinedInvitations.Clear();
        notificationReading=null;
        activeNotification=null;
        notificationDecision=false;
        nextNotificationState=0;
    }

    long NotificationSeen(string peer){
        if(!notificationSeen.TryGetValue(peer,out long id)){
            if(!testing)long.TryParse(PlayerPrefs.GetString(NotificationKey(peer),"0"),out id);
            notificationSeen[peer]=id;
        }
        return id;
    }
    string NotificationKey(string peer)=>"cf-notification-"+(string)networkProfile["id"]+"-"+peer;

    void ObserveSocialNotifications(JArray conversations){
        if(conversations==null||!HasNetworkProfile)return;
        foreach(var row in conversations){
            var sender=row["player"];
            string peer=(string)sender?["id"]??"";
            var last=row["last"];
            long id=(long?)last?["id"]??0;
            int unread=(int?)row["unread"]??0;
            if(peer.Length==0||unread==0||id<=NotificationSeen(peer))continue;
            if(unread==1&&(string)last["from"]==peer){
                QueueSocialNotification(sender,last);
            }else if((!notificationScanned.TryGetValue(peer,out long scanned)||scanned<id)&&
                     !notificationHistory.Any(request=>request.Peer==peer)){
                notificationHistory.Enqueue(new NotificationHistory{Peer=peer,Last=id,Unread=unread});
            }
        }
    }

    bool RequestNotificationHistory(){
        if(notificationHistory.Count==0)return false;
        notificationReading=notificationHistory.Dequeue();
        SocialRequest("history",new JObject{["peer"]=notificationReading.Peer});
        return true;
    }

    void FinishNotificationHistory(NotificationHistory request,JObject packet){
        var sender=packet["peer"];
        if((bool?)packet["ok"]!=true||(string)sender?["id"]!=request.Peer||!(packet["messages"] is JArray messages))return;
        notificationScanned[request.Peer]=request.Last;
        string recipient=(string)networkProfile?["id"];
        var incoming=messages.Reverse().Where(message=>(long?)message["id"]<=request.Last&&
            (string)message["from"]==request.Peer&&(string)message["to"]==recipient).Take(request.Unread).Reverse();
        foreach(var message in incoming)QueueSocialNotification(sender,message);
    }

    void QueueSocialNotification(JToken sender,JToken message){
        if(!HasNetworkProfile)return;
        string peer=(string)sender?["id"]??"";
        long id=(long?)message?["id"]??0;
        if(peer.Length==0||(string)message?["from"]!=peer||(string)message?["to"]!=(string)networkProfile["id"]||
          id<=NotificationSeen(peer))return;
        notificationSeen[peer]=id;
        if(!testing)PlayerPrefs.SetString(NotificationKey(peer),id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        bool invite=(string)message["kind"]=="invite";
        bool reading=socialTab==2&&socialPeer==peer&&(AccountHub.overlay==AccountOverlay.Messages||page==Page.Network);
        long age=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-((long?)message["at"]??0);
        if(age>(invite?900000L:86400000L)||(!invite&&reading))return;
        string room=invite?(string)message["text"]:null;
        if(invite&&string.IsNullOrWhiteSpace(room))return;
        if(socialNotifications.Count>=8)socialNotifications.Dequeue();
        socialNotifications.Enqueue(new SocialNotification{Sender=sender,Id=id,Room=room,
            Text=invite?"Комната "+room:MessageSummary(message)});
    }

    bool SocialNotificationVisible=>activeNotification!=null&&HasNetworkProfile&&!showNetworkProfile&&
        !Avatars.open&&!showUpdatePanel&&!showVoicePanel&&!showNetworkRacePicker&&!showStickerPicker&&
        !versusActive&&!winterIntro&&!hordeIntro&&!protocolIntro&&!protocolMarchIntro&&!choirIntro&&!abyssIntro&&
        quackScene==QuackScene.None&&!mythicGuestPrompt;
    Rect SocialNotificationRect=>new Rect(895,
        (page==Page.Match||page==Page.Teams||page==Page.Pause)?(scoreExpanded?153:69):58,
        358,notificationDecision?162:111);
    bool SocialNotificationAt(Vector2 position)=>SocialNotificationVisible&&SocialNotificationRect.Contains(position);
    bool CanAcceptSocialInvitation=>!onlineMatch&&networkSlot<0&&directoryReady;
    string DeclinedInvitationKey(long id)=>"cf-invite-declined-"+(string)networkProfile?["id"]+"-"+id;

    bool InvitationDeclined(JToken message){
        long id=(long?)message["id"]??0;
        return declinedInvitations.Contains(id)||!testing&&PlayerPrefs.GetInt(DeclinedInvitationKey(id),0)==1;
    }

    void UpdateSocialNotifications(){
        if(!HasNetworkProfile)return;
        if(activeNotification==null&&socialNotifications.Count>0){
            activeNotification=socialNotifications.Dequeue();
            notificationAge=-1;
            notificationDecision=false;
        }
        if(!SocialNotificationVisible)return;
        if(notificationAge<0){notificationAge=0;PlaySocialNotification();}
        notificationAge+=Time.unscaledDeltaTime;
        if(notificationAge>(notificationDecision?22:activeNotification.Invitation?12:6.5f))activeNotification=null;
    }

    void PlaySocialNotification(){
        if(options.volume<=0)return;
        if(!notificationChime){
            const int rate=22050;
            var samples=new float[rate*24/100];
            for(int i=0;i<samples.Length;i++){
                float t=i/(float)rate, noteTime=t<.075f?t:t-.075f;
                float frequency=t<.075f?587.33f:783.99f;
                float envelope=Mathf.Clamp01(noteTime*190)*Mathf.Exp(-noteTime*22)*Mathf.Clamp01((.24f-t)*30);
                samples[i]=.38f*envelope*Mathf.Sin(2*Mathf.PI*frequency*noteTime);
            }
            notificationChime=AudioClip.Create("Личное уведомление",samples.Length,1,rate,false);
            notificationChime.SetData(samples,0);
        }
        if(!notificationSource){
            notificationSource=gameObject.AddComponent<AudioSource>();
            notificationSource.playOnAwake=false;
            notificationSource.ignoreListenerPause=true;
        }
        notificationSource.PlayOneShot(notificationChime,options.volume*.56f);
    }

    void DrawSocialNotification(){
        if(!SocialNotificationVisible)return;
        EnsureCampaignStyles();
        if(notificationName==null){
            notificationName=new GUIStyle(atlasProfileName){fontSize=19};
            notificationPreview=new GUIStyle(label){alignment=TextAnchor.UpperLeft,padding=new RectOffset(),
                fontSize=16,wordWrap=true,richText=false,clipping=TextClipping.Clip,normal={textColor=AtlasIvory}};
            notificationKind=new GUIStyle(atlasProfileName){fontSize=13,normal={textColor=AtlasGold}};
        }
        var item=activeNotification;
        var box=SocialNotificationRect;
        AtlasSurface(box,true);
        var avatarBox=new Rect(box.x+14,box.y+14,51,51);
        Fill(avatarBox,AtlasWell);
        var avatar=AccountAvatar(item.Sender);
        if(avatar)GUI.DrawTexture(avatarBox,avatar,ScaleMode.ScaleAndCrop);
        Border(avatarBox,AtlasGold,1);
        float left=box.x+77,width=box.width-98;
        Text(left,box.y+9,width-32,19,item.Invitation?"Приглашение в матч":"Личное сообщение",notificationKind);
        string name=(string)item.Sender["name"]??"Игрок";
        Text(left,box.y+27,width-30,25,FitAtlasLabel(name,width-30,notificationName),notificationName);
        var preview=new Rect(left,box.y+54,width,45);
        if(item.Preview==null)item.Preview=FitNotificationPreview(item.Text,preview);
        string text=item.Preview;
        if(notificationDecision&&!CanAcceptSocialInvitation)
            text=directoryReady?"Выйдите из текущей комнаты.":"Нет связи с сервером.";
        GUI.Label(preview,text,notificationPreview);
        Fill(new Rect(box.x+13,box.yMax-5,(box.width-26)*Mathf.Clamp01(1-notificationAge/(notificationDecision?22:item.Invitation?12:6.5f)),2),AtlasGold);
        var close=new Rect(box.xMax-49,box.y+9,40,30);
        if(AtlasControl("social.toast.close",close,"×")){activeNotification=null;return;}
        if(notificationDecision){
            float top=box.yMax-50;
            if(AtlasControl("social.toast.decline",new Rect(box.x+14,top,151,38),"Отклонить")){
                declinedInvitations.Add(item.Id);
                if(!testing)PlayerPrefs.SetInt(DeclinedInvitationKey(item.Id),1);
                activeNotification=null;
                return;
            }
            bool enabled=GUI.enabled;
            GUI.enabled=enabled&&CanAcceptSocialInvitation;
            bool accepted=AtlasControl("social.toast.accept",new Rect(box.x+174,top,170,38),
                match!=null?"Принять и выйти":"Принять",true);
            GUI.enabled=enabled;
            if(accepted)AcceptSocialInvitation(item.Room);
            return;
        }
        var body=new Rect(box.x,box.y,box.width,box.height);
        if(Button("social.toast.open",body,"",false,GUIStyle.none)){
            if(item.Invitation){
                notificationDecision=true;
                notificationAge=0;
            }else{
                activeNotification=null;
                OpenOverlayConversation(item.Sender);
            }
        }
    }

    string FitNotificationPreview(string text,Rect area){
        int length=Mathf.Min(text.Length,140);
        while(length>0){
            int count=length;
            if(count<text.Length&&char.IsHighSurrogate(text[count-1]))count--;
            string preview=count==text.Length?text:text.Substring(0,count).TrimEnd()+"…";
            if(notificationPreview.CalcHeight(new GUIContent(preview),area.width)<=area.height)return preview;
            length--;
        }
        return "…";
    }

    void AcceptSocialInvitation(string room){
        if(!CanAcceptSocialInvitation)return;
        activeNotification=null;
        CloseAccountOverlay();
        if(match!=null)MainMenu();
        page=Page.Network;
        showNetworkProfile=networkEntryAccount=false;
        socialTab=0;
        EnterRoom("join",room);
    }
    static int RankIndex(int wins){int i=0;while(i<5&&wins>=RankThresholds[i+1])i++;return i;}
    Texture2D FrontendTexture(ref Texture2D texture,string name){if(!texture){string file=Path.Combine(MobileContent.MediaRoot,"Frontend",name);if(File.Exists(file)){texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(File.ReadAllBytes(file));texture.filterMode=FilterMode.Bilinear;}}return texture;}
    System.Collections.IEnumerator LoadMobileFrontendArt(){
        if(!Application.isMobilePlatform)yield break;
        foreach(string name in new[]{"castle-fight-logo.png","rank-emblems.png","victory-emblem.png"}){
            if(MobileContent.Active)yield return MobileContent.Current.EnsureMedia("Frontend/"+name);
            string artUrl=MobileContent.MediaRoot+"/Frontend/"+name;if(MobileContent.Active)artUrl=new Uri(artUrl).AbsoluteUri;
            using(var request=UnityEngine.Networking.UnityWebRequest.Get(artUrl)){
                yield return request.SendWebRequest();if(request.result!=UnityEngine.Networking.UnityWebRequest.Result.Success)continue;
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(request.downloadHandler.data);texture.filterMode=FilterMode.Bilinear;
                if(name=="rank-emblems.png"){if(rankSheet)Destroy(rankSheet);rankSheet=texture;}else if(name=="victory-emblem.png"){if(resultEmblem)Destroy(resultEmblem);resultEmblem=texture;}else{if(castleLogo)Destroy(castleLogo);castleLogo=texture;}
            }
        }
    }
    void DrawCastleLogo(Rect r){var texture=FrontendTexture(ref castleLogo,"castle-fight-logo.png");if(texture)GUI.DrawTexture(r,texture,ScaleMode.ScaleToFit);else Text(r.x,r.y+50,r.width,r.height,"CASTLE\nFIGHT",title);}
    void RankImage(Rect r,int wins){var texture=FrontendTexture(ref rankSheet,"rank-emblems.png");if(!texture)return;int i=RankIndex(wins);float split=462f/1024f;GUI.DrawTextureWithTexCoords(r,texture,new Rect((i%3)/3f,i<3?1-split:0,1/3f,i<3?split:1-split),true);}
    static string PeerStatus(JToken player){if((bool?)player?["online"]==true)return (string)player["activity"]=="playing"?"В бою":(string)player["activity"]=="room"?"В комнате":"Онлайн";long stamp=(long?)player?["last_seen"]??0;if(stamp<=0)return "Офлайн";var date=DateTimeOffset.FromUnixTimeMilliseconds(stamp).LocalDateTime;return "Офлайн · был(а) "+(date.Date==DateTime.Today?"сегодня "+date.ToString("HH:mm"):date.ToString("dd.MM HH:mm"));}
    void ResetSocial(){socialQueued=null;socialPending=false;socialSupported=false;socialTab=0;socialPeer=socialViewed=socialDraft=socialStickerPeer=socialConfirm="";socialState=new JObject();socialFound=new JArray();socialMessages=new JArray();socialMatches=new JArray();socialPlayer=null;socialRead=0;ResetSocialNotifications();ResetAccountAvatarRequests();}
    float SocialPollInterval=>AccountOverlayOpen||page==Page.Network?3:5;
    void SocialRequest(string action,JObject fields=null){
        if(!HasNetworkProfile){showNetworkProfile=true;directoryStatus="Войдите в аккаунт, чтобы пользоваться друзьями и сообщениями";return;}
        if(!socialSupported){directoryStatus="Социальные функции недоступны на этом сервере";return;}
        var packet=fields??new JObject();packet["type"]="profile_social";packet["action"]=action;if(socialPending||profilePending||Time.realtimeSinceStartup<socialNext){socialQueued=packet;return;}socialRequest=action;socialPending=true;socialDeadline=Time.realtimeSinceStartup+15;socialNext=Time.realtimeSinceStartup+.65f;SendNetwork(packet);
    }
    void UpdateSocial(){
        if(socialPending&&Time.realtimeSinceStartup>socialDeadline){socialPending=false;notificationReading=null;directoryStatus="Сервис профилей не ответил. Повторите запрос";}
        if(socialQueued!=null&&!socialPending&&!profilePending&&Time.realtimeSinceStartup>=socialNext){var next=socialQueued;socialQueued=null;SocialRequest((string)next["action"],next);return;}
        if(!directoryReady||!HasNetworkProfile||!socialSupported||socialPending||profilePending||Time.realtimeSinceStartup<socialDue||Time.realtimeSinceStartup<socialNext)return;
        if(RequestAccountAvatar()||showNetworkProfile||showStickerPicker)return;
        float now=Time.realtimeSinceStartup;
        socialDue=now+SocialPollInterval;
        if(RequestNotificationHistory())return;
        if(now>=nextNotificationState){
            nextNotificationState=now+8;
            SocialRequest("state");
            return;
        }
        if(!string.IsNullOrEmpty(socialViewed)){SocialRequest("player",new JObject{["peer"]=socialViewed});return;}
        if(socialTab==2&&string.IsNullOrEmpty(socialViewed)&&!string.IsNullOrEmpty(socialPeer))SocialRequest("history",new JObject{["peer"]=socialPeer,["read"]=socialRead});else SocialRequest("state");
    }
    void ReceiveSocial(JObject packet){
        socialPending=false;socialDue=Time.realtimeSinceStartup+SocialPollInterval;string action=(string)packet["action"]??socialRequest;
        if(notificationReading!=null&&action=="history"){
            var request=notificationReading;
            notificationReading=null;
            FinishNotificationHistory(request,packet);
            socialDue=0;
            return;
        }
        if(action=="avatar"){
            ReceiveAccountAvatar(packet);
            socialDue=Mathf.Max(Time.realtimeSinceStartup,socialNext);
            return;
        }
        if((bool?)packet["ok"]!=true){AccountHub.result=directoryStatus=(string)packet["message"]??"Не удалось выполнить запрос";if(action=="send"&&!AccountOverlayOpen)Notice(directoryStatus);return;}
        if(packet["friends"] is JArray){
            socialState=(JObject)packet.DeepClone();
            ObserveSocialNotifications(packet["conversations"] as JArray);
        }
        if(packet["found"] is JArray found){socialFound=found;directoryStatus="Найдено игроков: "+found.Count;}
        if(packet["player"] is JObject player&&(string)player["id"]==socialViewed){socialPlayer=player;socialViewed=(string)player["id"]??"";socialMatches=packet["matches"] as JArray??new JArray();}
        else if(packet["matches"] is JArray matches&&socialTab==3&&string.IsNullOrEmpty(socialViewed))socialMatches=matches;
        if(packet["messages"] is JArray messages&&socialTab==2&&string.IsNullOrEmpty(socialViewed)&&(string)packet["peer"]?["id"]==socialPeer){
            bool newer=messages.Count>0&&((long?)messages.Last["id"]??0)>socialRead;
            bool older=(bool?)packet["older"]==true;
            if(older){var all=new JArray(messages.Concat(socialMessages).GroupBy(m=>(long)m["id"]).Select(g=>g.First()).OrderBy(m=>(long)m["id"]));socialMessages=all;}
            else if(socialMessages.Count<=50)socialMessages=messages;
            else{var all=new JArray(socialMessages.Concat(messages).GroupBy(m=>(long)m["id"]).Select(g=>g.First()).OrderBy(m=>(long)m["id"]));socialMessages=all;}
            if(older||socialMessages.Count<=50)socialMore=(bool?)packet["has_more"]??false;if(newer){LoadStickerCatalog();foreach(var m in messages.Where(m=>(long)m["id"]>socialRead&&(string)m["from"]!=(string)networkProfile["id"]&&(string)m["kind"]=="sticker")){var item=stickerPacks.SelectMany(p=>p.items).FirstOrDefault(it=>it.id==(string)m["sticker"]);if(item!=null)PreviewSticker(item);}}if(messages.Count>0)socialRead=Math.Max(socialRead,(long)messages.Last["id"]);if(newer)messageScroll.y=100000;
            if(packet["peer"] is JObject remote)socialPlayer=remote;
            if(socialPlayer!=null&&newer)
                foreach(var message in messages.Where(row=>(string)row["kind"]=="invite"))QueueSocialNotification(socialPlayer,message);
        }
        if(action=="send"){
            if((string)packet["sent"]?["kind"]=="text"&&(string)packet["sent"]?["to"]==socialPeer&&
                (string)packet["sent"]?["text"]==socialDraft.Trim())socialDraft="";
            socialDue=0;AccountHub.result=directoryStatus=(string)packet["sent"]?["kind"]=="invite"?"Приглашение отправлено":"Сообщение отправлено";
            if((string)packet["sent"]?["kind"]=="invite"&&!AccountOverlayOpen)Notice(directoryStatus);
        }
        if(new[]{"request","accept","decline","cancel","remove"}.Contains(action)){socialConfirm="";socialDue=0;AccountHub.result=directoryStatus=action=="request"?"Заявка отправлена":action=="accept"?"Игрок добавлен в друзья":"Список обновлён";}
    }
    void OpenSocialTab(int tab){socialTab=tab;showNetworkProfile=false;showStickerPicker=false;socialViewed="";socialConfirm="";socialScroll=Vector2.zero;socialDue=0;showPeerMedals=false;if(tab==3&&HasNetworkProfile){socialMatches=new JArray();socialNickname=(string)networkProfile["name"]??"";SocialRequest("matches");}}
    void OpenPlayer(string id){if(string.IsNullOrEmpty(id))return;if(id==(string)networkProfile?["id"]){OpenSocialTab(3);return;}socialViewed=id;socialPlayer=null;showPeerMedals=false;socialMatches=new JArray();matchScroll=Vector2.zero;SocialRequest("player",new JObject{["peer"]=id});}
    void OpenConversation(string id){socialTab=2;socialViewed="";socialPeer=id;socialPlayer=null;socialMessages=new JArray();socialRead=0;socialMore=false;socialDraft="";messageScroll=Vector2.zero;socialDue=0;}
    void SendDirect(string kind,string value){if(kind=="sticker"&&!CanSendSticker(value)){Notice("Сначала откройте рофлопак в магазине");return;}var request=new JObject{["peer"]=socialPeer,["kind"]=kind,["nonce"]=Guid.NewGuid().ToString("N")};request[kind=="sticker"?"sticker":"text"]=value;SocialRequest("send",request);}
    int SocialUnread=>((socialState["conversations"] as JArray)??new JArray()).Sum(c=>(int?)c["unread"]??0);
    void DrawSocialTabs(){
        string[] captions={"Битвы","Друзья"+(((socialState["requests"] as JArray)?.Count(r=>(bool?)r["incoming"]==true)??0)>0?" ●":""),"Сообщения"+(SocialUnread>0?" · "+SocialUnread:"")};
        for(int i=0;i<captions.Length;i++)if(LobbyButton("social.tab."+i,new Rect(42+i*402,78,392,48),captions[i],socialTab==i))OpenSocialTab(i);
    }
    void DrawSocialContent(){
        if(!HasNetworkProfile){LobbyPanel(new Rect(42,149,1196,480));Text(78,194,1000,43,"Друзья, личные сообщения и ранг",heading);Text(78,257,1000,60,"Войдите в сетевой аккаунт. Он общий с Godot-версией — прогресс и друзья хранятся на VPS metal.",label);if(LobbyButton("social.login",new Rect(78,351,440,52),"Войти / создать аккаунт",true)){profileName=networkName;showNetworkProfile=true;}return;}
        if(!socialSupported){Text(64,200,1100,100,"На этом сервере социальные функции ещё недоступны.",label);return;}
        if(!string.IsNullOrEmpty(socialViewed)){DrawSocialPlayer(false);return;}
        if(socialTab==1)DrawFriends();else if(socialTab==2)DrawDirectMessages();else DrawSocialPlayer(true);
    }
    void DrawFriendRow(JObject player,float y,string suffix,string action,string caption,string detail=null){
        if(player==null)return;string id=(string)player["id"];Fill(new Rect(0,y,1158,86),new Color(.045f,.075f,.085f));RankImage(new Rect(7,y+3,74,74),(int?)player["wins"]??0);
        if(LobbyButton("social.player."+suffix,new Rect(91,y+3,515,44),(string)player["name"]??"Игрок"))OpenPlayer(id);
        Text(100,y+49,590,30,detail??(RankNames[RankIndex((int?)player["wins"]??0)]+" · "+PeerStatus(player)),small);
        GUI.enabled=!socialPending&&Time.realtimeSinceStartup>=socialNext;
        if(!string.IsNullOrEmpty(action)&&LobbyButton("social.action."+suffix,new Rect(905,y+17,237,50),caption,true)){if(action=="message")OpenConversation(id);else SocialRequest(action,new JObject{["peer"]=id});}
        GUI.enabled=true;
    }
    void DrawFriends(){
        socialSearch=GUI.TextField(new Rect(42,145,902,48),socialSearch,24,menuInput);GUI.enabled=!socialPending&&Time.realtimeSinceStartup>=socialNext;if(LobbyButton("social.search",new Rect(958,145,280,48),"Найти игрока"))SocialRequest("search",new JObject{["query"]=socialSearch});GUI.enabled=true;
        Text(46,200,1150,28,"Поиск по нику: от 3 символов. Нажмите имя, чтобы открыть профиль.",small);
        var friends=socialState["friends"] as JArray??new JArray();var requests=socialState["requests"] as JArray??new JArray();int rows=friends.Count+requests.Count+socialFound.Count;
        socialScroll=GUI.BeginScrollView(new Rect(42,236,1196,436),socialScroll,new Rect(0,0,1160,Mathf.Max(420,rows*96+144)));
        float y=0;
        if(socialFound.Count>0){Text(8,y,1000,30,"РЕЗУЛЬТАТЫ ПОИСКА",small);y+=36;int i=0;foreach(JObject p in socialFound){bool isFriend=friends.Any(f=>(string)f["id"]==(string)p["id"]);bool outgoing=requests.Any(r=>(bool?)r["incoming"]!=true&&(string)r["player"]?["id"]==(string)p["id"]);DrawFriendRow(p,y,"found."+i++,isFriend?"message":outgoing?"cancel":"request",isFriend?"Написать":outgoing?"Отменить заявку":"Добавить");y+=94;}}
        if(requests.Count>0){Text(8,y,1000,30,"ЗАЯВКИ",small);y+=36;int i=0;foreach(JObject row in requests){bool incoming=(bool?)row["incoming"]==true;DrawFriendRow(row["player"] as JObject,y,"request."+i,incoming?"accept":"cancel",incoming?"Принять":"Отменить");if(incoming&&LobbyButton("social.decline."+i,new Rect(720,y+18,170,48),"Отклонить"))SocialRequest("decline",new JObject{["peer"]=row["player"]["id"]});y+=94;i++;}}
        Text(8,y,1000,30,"ДРУЗЬЯ · "+friends.Count,small);y+=36;int index=0;foreach(JObject player in friends.OrderByDescending(f=>(bool?)f["online"]==true).ThenBy(f=>(string)f["name"])) {DrawFriendRow(player,y,"friend."+index++,"message","Написать");y+=94;}
        if(rows==0)Text(8,y+12,1050,90,"Найдите друга по нику и отправьте заявку. После принятия появится личная переписка.",label);GUI.EndScrollView();
    }
    void DrawDirectMessages(){
        var conversations=socialState["conversations"] as JArray??new JArray();var friends=socialState["friends"] as JArray??new JArray();
        if(string.IsNullOrEmpty(socialPeer)){
            Text(46,146,1160,38,"ЛИЧНЫЕ СООБЩЕНИЯ",heading);socialScroll=GUI.BeginScrollView(new Rect(42,200,1196,466),socialScroll,new Rect(0,0,1160,Mathf.Max(450,(conversations.Count+friends.Count)*94)));
            float y=0;int i=0;foreach(JObject row in conversations){DrawFriendRow(row["player"] as JObject,y,"dialog."+i++,"message",((int?)row["unread"]??0)>0?"Открыть · "+row["unread"]:"Открыть",MessageSummary(row["last"]));y+=94;}
            foreach(JObject player in friends.Where(f=>!conversations.Any(c=>(string)c["player"]?["id"]==(string)f["id"]))) {DrawFriendRow(player,y,"new."+i++,"message","Написать");y+=94;}if(i==0)Text(15,40,1050,70,"Здесь появится переписка с друзьями. Сначала добавьте друга во вкладке «Друзья».",label);GUI.EndScrollView();return;
        }
        if(LobbyButton("dm.back",new Rect(42,142,215,48),"← Диалоги")){socialPeer="";socialDue=0;return;}
        string name=(string)socialPlayer?["name"]??(string)friends.FirstOrDefault(f=>(string)f["id"]==socialPeer)?["name"]??"Профиль игрока";
        if(LobbyButton("dm.profile",new Rect(274,142,675,48),name+" · "+PeerStatus(socialPlayer)))OpenPlayer(socialPeer);
        GUI.enabled=networkSlot>=0&&!socialPending&&Time.realtimeSinceStartup>=socialNext;if(LobbyButton("dm.invite",new Rect(963,142,275,48),"Пригласить в комнату"))SendDirect("invite",(string)networkRoom?["code"]??"");GUI.enabled=true;
        LobbyPanel(new Rect(42,205,1196,370));messageScroll=GUI.BeginScrollView(new Rect(55,215,1170,350),messageScroll,new Rect(0,0,1130,Mathf.Max(334,socialMessages.Sum(m=>MessageHeight(m)+16)+60)));
        float top=0;if(socialMore){GUI.enabled=!socialPending&&Time.realtimeSinceStartup>=socialNext;if(LobbyButton("dm.older",new Rect(270,0,580,44),"Загрузить более ранние"))SocialRequest("history",new JObject{["peer"]=socialPeer,["before"]=(long?)socialMessages.First?["id"]??0,["older"]=true});GUI.enabled=true;top=54;}
        foreach(JObject message in socialMessages){
            bool own=(string)message["from"]==(string)networkProfile["id"];string kind=(string)message["kind"]??"text";float height=MessageHeight(message),width=MessageWidth(message),x=own?1118-width:12;
            ChatBubble(new Rect(x,top,width,height),own);string stamp=DateTimeOffset.FromUnixTimeMilliseconds((long?)message["at"]??0).LocalDateTime.ToString("dd.MM HH:mm");
            Text(x+18,top+11,width-36,25,own?"Вы":name,new GUIStyle(small){fontSize=13,fontStyle=FontStyle.Bold,wordWrap=false});
            Text(x+18,top+11,width-36,25,stamp,new GUIStyle(small){fontSize=13,wordWrap=false,alignment=TextAnchor.MiddleRight});
            if(kind=="sticker"){LoadStickerCatalog();var sticker=stickerPacks.SelectMany(p=>p.items).FirstOrDefault(it=>it.id==(string)message["sticker"]);if(sticker!=null)StickerImage(new Rect(x+(width-196)*.5f,top+44,196,196),sticker);}
            else if(kind=="invite"){
                bool declined=!own&&InvitationDeclined(message);
                Text(x+18,top+42,width-36,31,declined?"Приглашение отклонено":"Комната "+message["text"],ChatBodyStyle);
                bool enabled=GUI.enabled;
                GUI.enabled=enabled&&!own&&!declined&&CanAcceptSocialInvitation;
                if(LobbyButton("dm.join."+message["id"],new Rect(x+18,top+80,width-36,46),"Войти в комнату →"))
                    AcceptSocialInvitation((string)message["text"]);
                GUI.enabled=enabled;
            }
            else Text(x+18,top+43,width-36,height-57,(string)message["text"]??"",ChatBodyStyle);
            top+=height+16;
        }if(socialMessages.Count==0)Text(20,40,1000,70,"Начните разговор. Личные сообщения видны только вам и этому другу.",label);GUI.EndScrollView();
        socialDraft=GUI.TextField(new Rect(42,595,796,54),socialDraft,500,menuInput);GUI.enabled=!socialPending&&Time.realtimeSinceStartup>=socialNext;
        if(LobbyButton("dm.stickers",new Rect(850,595,172,54),"Стикеры")){socialStickerPeer=socialPeer;showStickerPicker=true;}
        if(LobbyButton("dm.send",new Rect(1034,595,204,54),"Отправить",true)&&!string.IsNullOrWhiteSpace(socialDraft))SendDirect("text",socialDraft);GUI.enabled=true;
        Text(46,655,1140,25,"До 500 символов · переписка сохраняется на сервере · стикеры из Godot",small);
    }
    Texture2D ownChatBubble,peerChatBubble;GUIStyle ownChatBubbleStyle,peerChatBubbleStyle;
    void ChatBubble(Rect rect,bool own){
        var style=own?ownChatBubbleStyle:peerChatBubbleStyle;
        if(style==null){var t=new Texture2D(32,32,TextureFormat.RGBA32,false);var colors=new Color[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){float d=RoundedGlyph(x+.5f,y+.5f,16,16,16,16,10);var c=own?Color.Lerp(new Color(.10f,.21f,.21f),new Color(.14f,.25f,.24f),y/31f):Color.Lerp(new Color(.055f,.10f,.13f),new Color(.075f,.13f,.16f),y/31f);c.a=Mathf.Clamp01(.5f-d)*.97f;colors[y*32+x]=c;}
            t.SetPixels(colors);t.Apply(false,true);t.filterMode=FilterMode.Bilinear;style=new GUIStyle{normal={background=t},border=new RectOffset(11,11,11,11)};
            if(own){ownChatBubble=t;ownChatBubbleStyle=style;}else{peerChatBubble=t;peerChatBubbleStyle=style;}
        }GUI.Label(rect,GUIContent.none,style);
    }
    string DirectPeerName=>(string)socialPlayer?["name"]??"Собеседник";
    GUIStyle ChatBodyStyle=>new GUIStyle(label){fontSize=17,wordWrap=true,padding=new RectOffset(),clipping=TextClipping.Clip};
    float MessageWidth(JToken message){
        bool own=(string)message["from"]==(string)networkProfile?["id"];string kind=(string)message["kind"]??"text";
        float metadata=new GUIStyle(small){fontSize=13}.CalcSize(new GUIContent((own?"Вы":DirectPeerName)+"   00.00 00:00")).x+48;
        float content=kind=="sticker"?244:kind=="invite"?440:ChatBodyStyle.CalcSize(new GUIContent((string)message["text"]??"")).x+36;
        return Mathf.Clamp(Mathf.Max(metadata,content),240,760);
    }
    float MessageHeight(JToken message){string kind=(string)message["kind"]??"text";return kind=="sticker"?254:kind=="invite"?140:Mathf.Max(88,ChatBodyStyle.CalcHeight(new GUIContent((string)message["text"]??""),MessageWidth(message)-36)+66);}

    static string MessageSummary(JToken message)=>message==null?"":(string)message["kind"]=="sticker"?"Стикер":(string)message["kind"]=="invite"?"Приглашение в комнату":((string)message["text"]??"").Replace("\n"," ");
    void DrawSocialPlayer(bool own){
        JObject player=own?networkProfile:socialPlayer;if(player==null){Text(70,230,1050,70,socialPending?"Загружаем профиль…":"Профиль не найден. Повторите запрос.",label);if(LobbyButton("player.retry",new Rect(42,145,300,48),"← Назад"))socialViewed="";return;}
        LobbyPanel(new Rect(42,145,1196,214));int wins=(int?)player["wins"]??0,rank=RankIndex(wins);
        var image=AccountAvatar(player);
        if(image){GUI.DrawTexture(new Rect(66,170,130,130),image,ScaleMode.ScaleToFit);RankImage(new Rect(147,258,59,70),wins);}
        else RankImage(new Rect(55,157,158,170),wins);
        Text(230,162,650,40,(string)player["name"]??"Игрок",heading);Text(232,211,650,31,RankNames[rank]+" · "+wins+" побед / "+((int?)player["games"]??0)+" игр",label);
        Text(232,250,650,29,own?"Ваш сетевой ранг общий с Godot-версией":AccountPeerStatus(player),small);Text(232,283,650,27,rank<5?"Следующий ранг: "+RankNames[rank+1]+" · ещё "+Mathf.Max(0,RankThresholds[rank+1]-wins)+" побед":"Достигнут высший ранг",small);
        if(!own){
            if(LobbyButton("player.back",new Rect(954,159,264,46),"← Назад")){socialViewed="";socialDue=0;return;}
            bool friend=((socialState["friends"] as JArray)??new JArray()).Any(f=>(string)f["id"]==socialViewed);
            if(LobbyButton("player.message",new Rect(954,217,264,48),"Написать",true)){OpenOverlayConversation(player);return;}
            if(!friend&&LobbyButton("player.add",new Rect(954,277,264,48),"Добавить в друзья"))SocialRequest("request",new JObject{["peer"]=socialViewed});
            if(friend&&LobbyButton("player.remove",new Rect(954,277,264,48),socialConfirm==socialViewed?"Подтвердить удаление":"Удалить из друзей")){
                if(socialConfirm==socialViewed)SocialRequest("remove",new JObject{["peer"]=socialViewed});else socialConfirm=socialViewed;
            }
        }
        else{if(LobbyButton("player.account",new Rect(954,161,264,48),"Управление аккаунтом"))showNetworkProfile=true;}
        string playerId=(string)player["id"]??"";
        if(LobbyButton("player.medals",new Rect(620,305,310,42),showPeerMedals?"История матчей":"Достижения",showPeerMedals)){
            showPeerMedals=!showPeerMedals;
            if(showPeerMedals){peerMedalPage=0;StartCoroutine(FetchPeerMedals(playerId));}
        }
        if(showPeerMedals){
            if(peerMedalsLoading)Text(46,391,1130,55,"Загружаем достижения…",label);
            else if(peerMedalsError){
                Text(46,391,780,55,"Сервер достижений временно недоступен.",label);
                if(LobbyButton("player.medals.retry",new Rect(950,391,280,42),"Повторить"))StartCoroutine(FetchPeerMedals(playerId));
            }else if(medalPeer==playerId)DrawMedalGrid(ProfileChallenges.Where(row=>peerMedals.Contains((string)row["id"])).ToArray(),370,67,ref peerMedalPage,false);
            return;
        }
        float historyTop=own?444:385;
        if(own){Text(45,374,200,32,"Ник в игре",label);socialNickname=GUI.TextField(new Rect(242,369,670,48),socialNickname,24,menuInput);GUI.enabled=!profilePending&&!socialPending&&Time.realtimeSinceStartup>=socialNext;if(LobbyButton("player.rename",new Rect(927,369,311,48),"Сохранить ник")){profilePending=true;socialNext=Time.realtimeSinceStartup+.65f;SendNetwork(new JObject{["type"]="profile_update",["nickname"]=socialNickname.Trim()});}GUI.enabled=true;}
        Text(46,historyTop,1150,30,"ИСТОРИЯ СЕТЕВЫХ МАТЧЕЙ · последние 100 раундов",small);matchScroll=GUI.BeginScrollView(new Rect(42,historyTop+38,1196,660-historyTop-38),matchScroll,new Rect(0,0,1160,Mathf.Max(180,socialMatches.Count*122)));
        int i=0;foreach(JObject row in socialMatches){
            float y=i++*122;bool won=(bool?)row["won"]==true;var resultColor=won?new Color(.46f,.83f,.60f):new Color(.91f,.48f,.42f);
            Fill(new Rect(0,y,1155,112),new Color(.035f,.065f,.078f,.97f));Fill(new Rect(0,y+7,3,98),resultColor);
            Text(18,y+12,170,34,won?"ПОБЕДА":"ПОРАЖЕНИЕ",new GUIStyle(label){fontSize=17,fontStyle=FontStyle.Bold,wordWrap=false,normal={textColor=resultColor}});
            Text(18,y+53,170,49,DateTimeOffset.FromUnixTimeMilliseconds((long?)row["at"]??0).LocalDateTime.ToString("dd.MM.yyyy\nHH:mm"),new GUIStyle(small){fontSize=13});
            string opponent="Соперник: "+((string)row["opponent"]??"Гость");
            if(!string.IsNullOrEmpty((string)row["opponent_id"])){if(LobbyButton("match.player."+row["id"],new Rect(204,y+12,710,42),opponent))OpenPlayer((string)row["opponent_id"]);}
            else Text(210,y+14,700,36,opponent,label);
            Text(210,y+65,704,38,RaceNetworkLabel((string)row["race"])+"  /  "+RaceNetworkLabel((string)row["opponent_race"]),new GUIStyle(small){fontSize=15,wordWrap=true});
            Text(944,y+16,190,30,"Раунд "+((int?)row["round"]??1),new GUIStyle(small){fontSize=15});float seconds=(float?)row["duration"]??0;if(seconds>0)Text(944,y+63,190,32,TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss"),small);
        }
        if(i==0)Text(12,20,1090,95,"Новых матчей пока нет. Подробная история записывается с обновления социальных функций. Прежние победы сохранены в ранге.",label);GUI.EndScrollView();
    }
    string RaceNetworkLabel(string race){int i=Array.IndexOf(networkRaceNames,race);return i<0?(string.IsNullOrEmpty(race)?"Раса не записана":race):networkRaceLabels[i];}
}
}
