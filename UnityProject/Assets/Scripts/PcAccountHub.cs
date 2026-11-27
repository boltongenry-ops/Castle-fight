using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    enum AccountOverlay { Closed, Friends, Messages }
    sealed class AccountHubState {
        public AccountOverlay overlay;
        public Vector2 listScroll;
        public JToken selected;
        public string owner="", activity="", invitePeer="", result="", mythicComment="", challengeOwner="", resultOwner="";
        public float reconnectAt, inviteDeadline, resultRetryAt;
        public JObject operation,pendingResults=new JObject();
        public bool guestAttempted;
        public readonly Queue<JObject> changes=new Queue<JObject>();
    }
    static readonly ConditionalWeakTable<PcGame,AccountHubState> AccountHubs=new ConditionalWeakTable<PcGame,AccountHubState>();
    AccountHubState AccountHub=>AccountHubs.GetValue(this,_=>new AccountHubState());
    bool AccountOverlayOpen=>AccountHub.overlay!=AccountOverlay.Closed;
    Rect AccountOverlayRect=>AccountHub.overlay==AccountOverlay.Messages?new Rect(580,67,674,610):new Rect(750,60,504,605);
    Rect AccountOverlayScroll{
        get{
            var box=AccountOverlayRect;
            return AccountHub.overlay==AccountOverlay.Messages?
                new Rect(box.x+22,box.y+109,box.width-44,box.height-232):
                new Rect(box.x+20,box.y+75,box.width-40,box.height-127);
        }
    }

    void UpdateAccountHub(){
        if(testing&&BattleLab.Arg("--pc-directory-url")=="")return;
        var hub=AccountHub;
        float now=Time.realtimeSinceStartup;
        if(!directoryReady&&!directoryLoading&&!onlineMatch&&(HasEndpointToken||networkGuest)&&now>=hub.reconnectAt){
            hub.reconnectAt=now+12;
            BeginDirectory();
        }
        if(!HasNetworkProfile||!directoryReady){
            if(directoryReady&&networkGuest&&!HasEndpointToken)TryAccountAuthentication();
            return;
        }
        string challenge=(string)activeChallenge?["id"]??"";
        string activity=challenge!=""?"challenge":match!=null&&!onlineMatch&&page!=Page.Viewer?"solo":"menu";
        string fingerprint=activity+":"+challenge;
        if(hub.activity!=fingerprint){
            hub.activity=fingerprint;
            SendNetwork(new JObject{["type"]="profile_activity",["activity"]=activity,["challenge"]=challenge});
        }
        if(hub.invitePeer!=""){
            if(now>=hub.inviteDeadline){hub.invitePeer="";Notice("Не удалось создать комнату для приглашения.");}
            else if(networkSlot>=0&&!string.IsNullOrEmpty((string)networkRoom?["code"])&&!profilePending&&!socialPending){
                string peer=hub.invitePeer;
                hub.invitePeer="";
                SendRoomInvite(peer);
            }
        }
        if(hub.operation!=null&&!profilePending){
            if((string)hub.operation["type"]=="profile_challenge")hub.resultRetryAt=now+10;
            hub.operation=null;
            hub.result="Сервис профилей не ответил.";
        }
        if(hub.operation!=null||profilePending||socialPending||now<socialNext)return;
        JObject change=null;
        while(hub.changes.Count>0&&change==null){
            var queued=hub.changes.Dequeue();
            if((string)queued["owner"]==hub.owner&&(CloudProgression||(string)queued["type"]=="profile_update")){
                queued.Remove("owner");change=queued;
            }
        }
        if(change==null&&CloudProgression&&now>=hub.resultRetryAt){
            var next=ReadAccountResults(hub.owner).Properties().FirstOrDefault();
            if(next!=null)change=new JObject{["type"]="profile_challenge",["challenge"]=next.Name,["time_ms"]=next.Value.DeepClone()};
        }
        if(change==null)return;
        hub.operation=change;
        profilePending=true;
        socialNext=now+.65f;
        SendNetwork(change);
    }

    void RestoreAccountMode(bool guest){
        ClearOwnAvatarIdentity();
        networkGuest=guest;
        var saved=NetworkAccountStore.Read(guest);
        networkSessionEndpoint=(string)saved?["endpoint"]??"";
        networkSessionToken=(string)saved?["token"]??"";
        rememberedAccountName=(string)saved?["nickname"]??"";
        AccountHub.owner="";AccountHub.activity="";
    }

    void TryAccountAuthentication(){
        if(HasNetworkProfile||profilePending)return;
        if(HasEndpointToken&&NetworkAccountStore.SameService(networkSessionEndpoint,connectedEndpoint)){
            profilePending=true;
            SendNetwork(new JObject{["type"]="profile_auth",["action"]="token",["token"]=networkSessionToken});
        }else if(networkGuest&&!AccountHub.guestAttempted&&(NetworkAccountStore.SameService(connectedEndpoint,NetworkAccountStore.UnityEndpoint)||testing)){
            AccountHub.guestAttempted=true;profilePending=true;
            SendNetwork(new JObject{["type"]="profile_auth",["action"]="guest"});
        }
    }

    void SwitchFromGuest(){
        StopDirectory();RestoreAccountMode(false);
        if(!testing)NetworkAccountStore.SelectMode(false);
        OpenAccountSignIn();
    }

    void ReceiveAccountOperation(string type,JObject packet){
        var hub=AccountHub;
        if(type!=(string)hub.operation?["type"])return;
        var operation=hub.operation;
        hub.operation=null;
        bool ok=(bool?)packet["ok"]==true;
        hub.result=!ok?directoryStatus:type=="profile_cosmetic"?"Коллекция обновлена":type=="profile_update"?"Аватар сохранён":"";
        if(ok&&type=="profile_update"&&operation["avatar_image"]!=null){
            string revision=(string)packet["profile"]?["avatar_ref"];
            if(!CacheAccountAvatar(revision,(string)operation["avatar_image"]))socialDue=0;
        }
        if(type=="profile_challenge"){
            string id=(string)operation["challenge"];
            if(ok&&id==(string)activeChallenge?["id"])challengeReward=(int?)packet["awarded"]??0;
            if(!ok)hub.resultRetryAt=Time.realtimeSinceStartup+15;
            if(ok&&id!=null){
                var pending=ReadAccountResults(hub.owner);
                if((int?)pending[id]==(int?)operation["time_ms"]){
                    pending.Remove(id);
                    SaveAccountResults(hub.owner,pending);
                }
            }
        }
    }

    void QueueAccountChange(JObject change){
        change["owner"]=AccountHub.owner;
        AccountHub.changes.Enqueue(change);
    }

    bool CloudProgression=>HasNetworkProfile&&(int?)networkProfile["account_progression"]==1;
    int AccountCoins=>CloudProgression?((int?)networkProfile["coins"]??0):progression.Coins;
    bool AccountCompleted(string id)=>IsPublishedMedal(id)||(CloudProgression?
        (networkProfile["completed"] as JArray)?.Any(value=>(string)value==id)==true:progression.Completed(id));
    float AccountBest(string id)=>CloudProgression?((int?)networkProfile["best_ms"]?[id]??0)/1000f:progression.Best(id);
    bool AccountOwns(string id)=>CloudProgression?(networkProfile["unlocked"] as JArray)?.Any(value=>(string)value==id)==true:progression.Owned(id);
    string AccountEquipment(string kind)=>CloudProgression?(string)networkProfile["equipped"]?[kind]??"":progression.Equipped(kind);

    void AccountChallengeVictory(string id,float elapsed){
        if(AccountHub.challengeOwner==""||AccountHub.challengeOwner!=AccountHub.owner)return;
        var pending=ReadAccountResults(AccountHub.owner);
        int time=Mathf.Clamp(Mathf.RoundToInt(elapsed*1000),1,86400000);
        if(time>=((int?)pending[id]??int.MaxValue))return;
        pending[id]=time;
        SaveAccountResults(AccountHub.owner,pending);
        AccountHub.resultRetryAt=0;
    }

    JObject ReadAccountResults(string owner){
        var hub=AccountHub;
        if(hub.resultOwner==owner)return hub.pendingResults;
        hub.resultOwner=owner;
        string saved=testing||owner==""?"":PlayerPrefs.GetString("cf-pending-challenges-"+owner,"");
        try{hub.pendingResults=saved==""?new JObject():JObject.Parse(saved);}
        catch(Newtonsoft.Json.JsonException){hub.pendingResults=new JObject();}
        return hub.pendingResults;
    }

    void SaveAccountResults(string owner,JObject results){
        if(testing||owner=="")return;
        string key="cf-pending-challenges-"+owner;
        if(results.HasValues)PlayerPrefs.SetString(key,results.ToString(Newtonsoft.Json.Formatting.None));
        else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    void MigrateLocalChallengeResults(string[] earned){
        if(!CloudProgression||earned.Length==0)return;
        var results=new JArray(earned.Select(id=>new JObject{["challenge"]=id,
            ["time_ms"]=Mathf.Clamp(Mathf.RoundToInt(progression.Best(id)*1000),1,86400000)}));
        QueueAccountChange(new JObject{["type"]="profile_challenge",["results"]=results});
    }

    void ExitNetworkToMenu(){
        if(networkSlot>=0)StopDirectory();
        else{showNetworkProfile=false;showNetworkRacePicker=false;OpenSocialTab(0);}
        CloseAccountOverlay();
        page=Page.Main;
    }

    void DrawFriendsShortcut(Rect area){
        if(AtlasButton("account.friends",area,campaignIcons?"":"●",icon:2)){
            if(AccountOverlayOpen)CloseAccountOverlay();else OpenFriendsOverlay();
        }
        int incoming=(socialState["requests"] as JArray)?.Count(r=>(bool?)r["incoming"]==true)??0;
        int unread=(socialState["conversations"] as JArray)?.Sum(r=>(int?)r["unread"]??0)??0;
        if(incoming+unread>0)Text(area.xMax-21,area.y+1,21,19,Mathf.Min(incoming+unread,99).ToString(),small);
    }

    void OpenFriendsOverlay(){
        var hub=AccountHub;
        hub.overlay=AccountOverlay.Friends;hub.listScroll=Vector2.zero;hub.selected=null;hub.result="";
        socialDue=0;socialTouchMoved=false;
    }

    void CloseAccountOverlay(){
        var hub=AccountHub;
        if(hub.overlay==AccountOverlay.Messages){socialPeer="";socialTab=0;}
        hub.overlay=AccountOverlay.Closed;hub.selected=null;hub.result="";
        socialTouchMoved=false;
        CloseAccountKeyboard();
    }

    void OpenOverlayConversation(JToken player){
        var hub=AccountHub;
        OpenConversation((string)player["id"]);
        socialPlayer=player as JObject;
        hub.overlay=AccountOverlay.Messages;hub.result="";
    }

    void InvitePlayer(string id){
        if(!HasNetworkProfile||onlineMatch)return;
        if(networkSlot>=0){SendRoomInvite(id);return;}
        var hub=AccountHub;
        hub.invitePeer=id;hub.inviteDeadline=Time.realtimeSinceStartup+45;
        CloseAccountOverlay();
        OpenNetwork();
        EnterRoom("create","");
    }

    void SendRoomInvite(string id){
        SocialRequest("send",new JObject{["peer"]=id,["kind"]="invite",["text"]=(string)networkRoom?["code"]??"",
            ["nonce"]=Guid.NewGuid().ToString("N")});
    }

    void DrawAccountOverlay(){
        EnsureRaceStyles();
        if(AccountHub.overlay==AccountOverlay.Messages)
            Fill(new Rect(0,0,1280,720),new Color(0,0,0,.35f));
        var box=AccountOverlayRect;
        if(Event.current.type==EventType.MouseDown&&!box.Contains(Event.current.mousePosition)){
            Event.current.Use();CloseAccountOverlay();return;
        }
        AtlasSurface(box,true);
        if(LobbyButton("account.overlay.close",new Rect(box.xMax-63,box.y+15,45,40),"×")){CloseAccountOverlay();return;}
        if(AccountHub.overlay==AccountOverlay.Messages)DrawOverlayConversation(box);
        else DrawFriendsOverlay(box);
    }

    void DrawFriendsOverlay(Rect box){
        var hub=AccountHub;
        Text(box.x+23,box.y+12,390,48,"Друзья и игроки",atlasTitle);
        var friends=socialState["friends"] as JArray??new JArray();
        var online=socialState["playing_now"] as JArray??new JArray();
        var requests=socialState["requests"] as JArray??new JArray();
        var conversations=socialState["conversations"] as JArray??new JArray();
        var area=AccountOverlayScroll;
        float content=172+66*(friends.Count+online.Count+requests.Count+conversations.Count);
        bool enabled=GUI.enabled;
        GUI.enabled=enabled&&hub.selected==null;
        hub.listScroll=GUI.BeginScrollView(area,hub.listScroll,new Rect(0,0,area.width-22,Mathf.Max(area.height,content)));
        float y=0;
        DrawHubGroup("Друзья",friends,ref y,area.width-25,"Пока нет друзей");
        DrawHubGroup("Сейчас играют",online,ref y,area.width-25,"Других игроков в сети пока нет");
        if(requests.Count>0){
            Text(4,y,400,29,"Заявки",label);y+=30;
            foreach(var request in requests){
                var player=request["player"];string id=(string)player?["id"];
                if(string.IsNullOrEmpty(id))continue;
                Text(6,y,210,49,FitAtlasLabel((string)player["name"],202,label),label);
                bool incoming=(bool?)request["incoming"]==true;
                if(LobbyButton("hub.request."+id,new Rect(218,y+4,107,42),incoming?"Принять":"Отменить"))
                    SocialRequest(incoming?"accept":"cancel",new JObject{["peer"]=id});
                if(incoming&&LobbyButton("hub.decline."+id,new Rect(333,y+4,96,42),"Отклонить"))
                    SocialRequest("decline",new JObject{["peer"]=id});
                y+=58;
            }
        }
        if(conversations.Count>0){
            Text(4,y,400,29,"Диалоги",label);y+=30;
            foreach(var row in conversations){
                var player=row["player"];string id=(string)player?["id"];
                if(string.IsNullOrEmpty(id))continue;
                int unread=(int?)row["unread"]??0;
                string title=(string)player["name"]+(unread>0?" · "+unread:"");
                if(LobbyButton("hub.dialogue."+id,new Rect(5,y+3,420,46),FitAtlasLabel(title,385,label)))
                    OpenOverlayConversation(player);
                y+=58;
            }
        }
        GUI.EndScrollView();
        GUI.enabled=enabled;
        Text(box.x+23,box.yMax-47,box.width-46,30,FitAtlasLabel(hub.result,box.width-54,small),small);
        if(hub.overlay==AccountOverlay.Friends&&hub.selected!=null)DrawFriendActions(box,hub.selected,friends,requests);
    }

    void DrawHubGroup(string title,JArray rows,ref float y,float width,string empty){
        Text(4,y,420,29,title+" · "+rows.Count,label);y+=30;
        if(rows.Count==0){Text(6,y,width,32,empty,small);y+=36;return;}
        foreach(var player in rows){
            string id=(string)player["id"];
            var row=new Rect(4,y+2,width-8,60);
            if(LobbyButton("hub.peer."+title+id,row,""))AccountHub.selected=player;
            var avatar=AccountAvatar(player);
            if(avatar)GUI.DrawTexture(new Rect(15,y+11,38,38),avatar,ScaleMode.ScaleToFit);
            Fill(new Rect(49,y+43,9,9),(bool?)player["online"]==true?new Color(.54f,.79f,.43f):new Color(.47f,.48f,.45f));
            var nameStyle=new GUIStyle(label){padding=new RectOffset(),alignment=TextAnchor.MiddleLeft};
            var statusStyle=new GUIStyle(small){padding=new RectOffset(),alignment=TextAnchor.MiddleLeft};
            Text(66,y+6,width-88,29,FitAtlasLabel((string)player["name"],width-92,nameStyle),nameStyle);
            Text(66,y+32,width-88,24,FitAtlasLabel(AccountPeerStatus(player),width-92,statusStyle),statusStyle);
            y+=66;
        }
    }

    void DrawFriendActions(Rect box,JToken player,JArray friends,JArray requests){
        string id=(string)player["id"];
        var pane=new Rect(box.x+26,box.y+165,box.width-52,286);
        Fill(new Rect(box.x,box.y+61,box.width,box.height-61),new Color(0,0,0,.66f));
        AtlasSurface(pane,true);
        Text(pane.x+19,pane.y+9,pane.width-90,41,FitAtlasLabel((string)player["name"],pane.width-95,label),label);
        if(LobbyButton("hub.action.back",new Rect(pane.xMax-59,pane.y+10,40,39),"×")){AccountHub.selected=null;return;}
        var action=new Rect(pane.x+19,pane.y+56,pane.width-38,46);
        if(LobbyButton("hub.action.message",action,"Написать")){OpenOverlayConversation(player);return;}
        action.y+=53;
        bool friend=friends.Any(f=>(string)f["id"]==id);
        var request=requests.FirstOrDefault(r=>(string)r["player"]?["id"]==id);
        bool incoming=(bool?)request?["incoming"]==true;
        bool enabled=GUI.enabled;
        GUI.enabled=enabled&&!friend&&(request==null||incoming);
        if(LobbyButton("hub.action.add",action,friend?"Уже в друзьях":incoming?"Принять заявку":request!=null?"Заявка отправлена":"Добавить в друзья")){
            SocialRequest(incoming?"accept":"request",new JObject{["peer"]=id});AccountHub.selected=null;
        }
        action.y+=53;
        GUI.enabled=enabled&&(bool?)player["online"]==true&&!onlineMatch;
        if(LobbyButton("hub.action.invite",action,"Пригласить в матч"))InvitePlayer(id);
        action.y+=53;GUI.enabled=enabled;
        if(LobbyButton("hub.action.profile",action,"Профиль")){
            CloseAccountOverlay();page=Page.Profile;OpenPlayer(id);
        }
    }

    void DrawOverlayConversation(Rect box){
        var hub=AccountHub;
        Text(box.x+23,box.y+11,box.width-95,47,FitAtlasLabel(DirectPeerName,box.width-100,atlasTitle),atlasTitle);
        string status=AccountPeerStatus(socialPlayer);
        Text(box.x+24,box.y+60,box.width-224,30,FitAtlasLabel(status,box.width-232,small),small);
        if(LobbyButton("hub.chat.back",new Rect(box.xMax-192,box.y+58,170,40),"← Друзья")){
            CloseAccountOverlay();OpenFriendsOverlay();return;
        }
        var area=AccountOverlayScroll;
        Fill(area,new Color(.027f,.049f,.046f,.92f));Border(area,AtlasEdge);
        float width=area.width-24,bubble=width*.81f;
        var body=ChatBodyStyle;
        var timestamp=new GUIStyle(small){padding=new RectOffset(),alignment=TextAnchor.MiddleLeft};
        float Height(JToken m)=>(string)m["kind"]=="invite"?115:Mathf.Max(57,body.CalcHeight(new GUIContent(MessageSummary(m)),bubble-24)+35);
        messageScroll=GUI.BeginScrollView(area,messageScroll,new Rect(0,0,width,Mathf.Max(area.height,socialMessages.Sum(m=>Height(m)+8)+55)));
        float y=7;
        if(socialMore&&LobbyButton("hub.chat.older",new Rect(85,y,width-170,42),"Более ранние сообщения")){
            SocialRequest("history",new JObject{["peer"]=socialPeer,["before"]=(long?)socialMessages.First?["id"]??0,["older"]=true});
        }
        if(socialMore)y+=48;
        if(socialMessages.Count==0)Text(17,18,width-34,40,"Здесь появится ваша переписка",small);
        foreach(var message in socialMessages){
            bool own=(string)message["from"]==(string)networkProfile?["id"];
            float x=own?width-bubble-10:10,h=Height(message);
            ChatBubble(new Rect(x,y,bubble,h),own);
            long at=(long?)message["at"]??0;
            Text(x+12,y+4,bubble-24,23,at>0?DateTimeOffset.FromUnixTimeMilliseconds(at).LocalDateTime.ToString("dd.MM HH:mm"):"",timestamp);
            bool invite=(string)message["kind"]=="invite";
            bool declined=!own&&invite&&InvitationDeclined(message);
            Text(x+12,y+28,bubble-24,h-30,declined?"Приглашение отклонено":MessageSummary(message),body);
            if(!own&&invite&&!declined&&CanAcceptSocialInvitation&&LobbyButton("hub.chat.join."+message["id"],new Rect(x+12,y+58,bubble-24,40),"Войти в комнату")){
                AcceptSocialInvitation((string)message["text"]);break;
            }
            y+=h+8;
        }
        GUI.EndScrollView();
        Text(box.x+24,box.yMax-111,box.width-48,29,FitAtlasLabel(hub.result,box.width-52,small),small);
        socialDraft=AccountText("hub.chat.draft",new Rect(box.x+22,box.yMax-74,box.width-219,50),socialDraft,500,false,"Сообщение");
        if(!Application.isMobilePlatform&&socialDraft==""&&GUI.GetNameOfFocusedControl()!="hub.chat.draft")
            Text(box.x+34,box.yMax-73,box.width-240,49,"Сообщение",small);
        bool active=GUI.enabled;GUI.enabled=active&&!socialPending&&!profilePending&&!string.IsNullOrWhiteSpace(socialDraft);
        if(LobbyButton("hub.chat.send",new Rect(box.xMax-183,box.yMax-74,161,50),"Отправить",true)){
            CloseAccountKeyboard();SendDirect("text",socialDraft);
        }
        GUI.enabled=active;
    }

    string AccountPeerStatus(JToken player){
        if((bool?)player?["online"]!=true)return PeerStatus(player);
        string activity=(string)player["activity"];
        if(activity=="challenge"){
            string name=(string)progression.Challenge((string)player["challenge"]??"")?["title"];
            return string.IsNullOrEmpty(name)?"В испытании":"Испытание · "+name;
        }
        if(activity=="solo")return "Бой с ботом";
        return activity=="menu"?"В меню":PeerStatus(player);
    }

#if CF_ACCOUNT_HUB_QA
    System.Collections.IEnumerator ExerciseAccountHub(){
        string folder=BattleLab.Arg("--pc-results");
        System.IO.Directory.CreateDirectory(folder);
        yield return new WaitForSecondsRealtime(2);
        var fixture=new JObject{["id"]="qa-main",["name"]="Страж замка",["wins"]=13,["coins"]=160,["avatar"]="ogru",["avatar_ref"]="qa-avatar",
            ["account_progression"]=1,["best_ms"]=new JObject{["five_pillars"]=94000},
            ["completed"]=new JArray("five_pillars")};
        networkSessionToken="qa-fixture";networkSessionEndpoint=NetworkAccountStore.UnityEndpoint;
        ApplyAccountProfile(fixture);
        var avatar=TemplateAvatar("ogru");
        var uploaded=new Texture2D(2,2,TextureFormat.RGB24,false);
        uploaded.SetPixels(new[]{Color.cyan,Color.blue,Color.cyan,Color.blue});uploaded.Apply();
        Avatars.images.Add("qa-avatar",uploaded);
        yield return Action("main.profile");
        Check(ProfileAvatar()==uploaded,"Opening the full profile retains the uploaded avatar");
        StopDirectory();MainMenu();
        Check(!HasNetworkProfile&&ProfileAvatar()==uploaded,"Header retains the uploaded avatar while reconnecting");
        var template=(JObject)fixture.DeepClone();
        template["avatar_ref"]="";
        ApplyAccountProfile(template);networkProfile=null;
        Check(avatar&&ProfileAvatar()==avatar,"Header retains an avatar template while reconnecting");
        ClearAccountIdentity();
        Check(ProfileAvatar()!=uploaded&&Avatars.ownIdentity==null,"Signing out clears the remembered avatar");
        networkSessionToken="qa-fixture";networkSessionEndpoint=NetworkAccountStore.UnityEndpoint;
        var photo=new Texture2D(128,128,TextureFormat.RGB24,false);
        var pixels=Enumerable.Repeat(new Color32(28,170,120,255),128*128).ToArray();
        photo.SetPixels32(pixels);photo.Apply();
        byte[] jpeg=photo.EncodeToJPG(85);
        Destroy(photo);
        string revision;
        using(var hash=System.Security.Cryptography.SHA256.Create())
            revision=BitConverter.ToString(hash.ComputeHash(jpeg)).Replace("-","").ToLowerInvariant();
        string encoded=Convert.ToBase64String(jpeg);
        var confirmed=(JObject)template.DeepClone();
        confirmed["avatar"]="hfoo";
        confirmed["avatar_ref"]=revision;
        ApplyAccountProfile(confirmed);
        Check(ProfileAvatar()==null,"An authoritative custom avatar never shows the soldier while pending");
        AccountHub.operation=new JObject{["type"]="profile_update",["avatar_image"]=encoded};
        ReceiveAccountOperation("profile_update",new JObject{["ok"]=true,["profile"]=confirmed});
        Check(ProfileAvatar()&&ProfileAvatar().width==128,"The confirmed upload displays its actual JPEG immediately");
        var displayed=ProfileAvatar();
        yield return Action("main.profile");
        yield return Action("account-profile.back");
        yield return Resources.UnloadUnusedAssets();
        Check(page==Page.Main&&ProfileAvatar()==displayed&&displayed,
            "Returning from the actual profile button keeps the uploaded texture in the menu across asset cleanup");
        yield return Shot(folder,"00a-menu-uploaded-avatar");
        Destroy(displayed);
        yield return null;
        var restored=ProfileAvatar();
        Check(restored&&restored!=displayed&&restored.width==128,
            "If the render texture was destroyed, the verified image restores the same avatar without a network request");
        Destroy(restored);
        Avatars.images.Remove(revision);
        ClearOwnAvatarIdentity();
        ResetAccountAvatarRequests();
        networkProfile=null;
        ApplyAccountProfile(confirmed);
        Check(ProfileAvatar()==null&&!CacheAccountAvatar(new string('0',64),encoded),
            "A cold login does not display a default or accept the wrong revision");
        ReceiveSocial(new JObject{["ok"]=true,["action"]="avatar",["avatar_owner"]="qa-main",["avatar_ref"]=revision,["avatar_image"]=encoded});
        var downloaded=ProfileAvatar();
        Check(downloaded&&downloaded.width==128,"The downloaded custom avatar reappears after a cold login");
        StopDirectory();
        Check(ProfileAvatar()==downloaded,"A cached remote avatar survives reconnecting");
        ApplyAccountProfile(fixture);
        Check(!IntroAlreadyShown(0,0)&&IntroAlreadyShown(1,0)&&IntroAlreadyShown(0,1),"Startup video plays on new installs and is skipped after an attempt or legacy completion");
        var viewport=new Vector2(2400,1080);
        var imageSize=new Vector2(1920,1080);
        var start=LandscapeFrame(viewport,imageSize,0,1);
        var later=LandscapeFrame(viewport,imageSize,12,1);
        Check(start!=later&&start.Contains(Vector2.zero)&&start.Contains(viewport)&&later.Contains(Vector2.zero)&&later.Contains(viewport),
            "Animated landscape moves and covers an ultrawide phone screen");
        var peer=new JObject{["id"]="qa-peer",["name"]="Мастер осады",["avatar"]="ogru",["online"]=true,["activity"]="challenge",
            ["challenge"]=HumanMatch.AbyssTrial,["wins"]=7,["games"]=14};
        var offline=new JObject{["id"]="qa-offline",["name"]="Маршал",["avatar"]="hrif",["online"]=false,
            ["last_seen"]=DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeMilliseconds()};
        socialState=new JObject{["friends"]=new JArray(offline),["playing_now"]=new JArray(peer)};
        Check(AccountCompleted("five_pillars")&&Mathf.Approximately(AccountBest("five_pillars"),94)&&AccountCoins==160,"Account progress is drawn from the authenticated profile");
        Check(AccountPeerStatus(peer).Contains((string)progression.Challenge(HumanMatch.AbyssTrial)["title"]),"Online player shows the exact challenge name");
        Check(AccountPeerStatus(offline).Contains("был(а)"),"Offline friend shows last seen");
        yield return Shot(folder,"00-main");
        yield return Action("account.friends");
        Check(AccountHub.overlay==AccountOverlay.Friends,"Friends dropdown opens from the main menu");
        yield return Shot(folder,"01-friends");
        yield return Action("hub.peer.Сейчас играютqa-peer");
        yield return Shot(folder,"02-actions");
        yield return Action("hub.action.message");
        Check(socialPeer=="qa-peer"&&AccountHub.overlay==AccountOverlay.Messages,"Message action opens an overlay without changing the main menu");
        socialMessages.Add(new JObject{["id"]=1,["from"]="qa-peer",["kind"]="text",["text"]="Привет! Дошёл до второй волны в испытании. Сыграем вместе?",["at"]=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()});
        socialMessages.Add(new JObject{["id"]=2,["from"]="qa-main",["kind"]="text",["text"]="Да, сейчас создам комнату.",["at"]=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()});
        socialMessages.Add(new JObject{["id"]=3,["from"]="qa-peer",["kind"]="invite",["text"]="TEST42",["at"]=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()});
        yield return Shot(folder,"03-dialogue");
        yield return Action("account.overlay.close");
        Check(!AccountOverlayOpen&&page==Page.Main,"Closing a conversation returns to the same screen");

        long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var incoming=new JObject{["id"]=44,["from"]="qa-peer",["to"]="qa-main",["kind"]="text",
            ["text"]="Привет! Проверим пропуск и посмотрим, как работает вторая стадия?",["at"]=now};
        var snapshot=new JObject{["ok"]=true,["action"]="state",["friends"]=new JArray(),
            ["conversations"]=new JArray(new JObject{["player"]=peer,["last"]=incoming,["unread"]=1})};
        ReceiveSocial(snapshot);
        ReceiveSocial(snapshot);
        Check(socialNotifications.Count==1,"A repeated social poll does not duplicate a notification");
        yield return null;
        Check(activeNotification?.Id==44&&notificationChime,"The message toast displays with a notification sound");
        yield return Shot(folder,"07-message-toast");
        yield return Action("social.toast.open");
        Check(socialPeer=="qa-peer"&&AccountHub.overlay==AccountOverlay.Messages&&page==Page.Main,"The toast opens the correct direct chat");
        CloseAccountOverlay();

        var invite=new JObject{["id"]=45,["from"]="qa-peer",["to"]="qa-main",["kind"]="invite",["text"]="TEST42",["at"]=now};
        var ownReply=new JObject{["id"]=46,["from"]="qa-main",["to"]="qa-peer",["kind"]="text",["text"]="Сейчас зайду",["at"]=now};
        var scan=new NotificationHistory{Peer="qa-peer",Last=46,Unread=1};
        FinishNotificationHistory(scan,new JObject{["ok"]=true,["peer"]=peer,["messages"]=new JArray(invite,ownReply)});
        yield return null;
        Check(activeNotification?.Invitation==true&&activeNotification.Room=="TEST42","An invite is found even if the conversation's last message is outgoing");
        yield return Action("social.toast.open");
        Check(notificationDecision,"The invitation opens accept and decline controls");
        directoryReady=true;
        yield return Shot(folder,"08-match-invite");
        yield return Action("social.toast.decline");
        Check(activeNotification==null&&InvitationDeclined(invite),"Declining closes the toast and marks the invitation in chat");

        var nextInvite=(JObject)invite.DeepClone();
        nextInvite["id"]=47;
        QueueSocialNotification(peer,nextInvite);
        directoryReady=true;
        yield return null;
        yield return Action("social.toast.open");
        yield return Action("social.toast.accept");
        Check(page==Page.Network&&activeNotification==null&&directoryStatus=="Входим в комнату…",
            "Accepting opens the invited room flow on the existing connection");
        directoryReady=false;

        pendingMythicScore=new MythicScore{challenge=HumanMatch.AbyssTrial,race=2,timeMs=365000,token=networkSessionToken};
        page=Page.Progression;mythicGuestPrompt=true;AccountHub.mythicComment="Вторая попытка. Выстоял до последней волны!";
        yield return Shot(folder,"04-score-comment");
        yield return Action("mythic.score.later");
        OpenProgression(false);mythicTab=true;selectedMythicId=HumanMatch.AbyssTrial;
        mythicLeaderboard=new JArray(new JObject{["place"]=1,["race"]=2,["nickname"]="Страж замка",["time_ms"]=365000,["comment"]=AccountHub.mythicComment});
        yield return Shot(folder,"05-leaderboard");
        OpenAccountProfile();
        yield return Action("account-profile.avatar");
        Check(Avatars.open,"Avatar editor opens from the account profile");
        yield return Shot(folder,"06-avatar");
        yield return Action("profile.avatar.close");
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"account-ui-verification.json"),
            new JObject{["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks}.ToString());
        Application.Quit(testFailures==0?0:1);
    }
#endif
}
}
