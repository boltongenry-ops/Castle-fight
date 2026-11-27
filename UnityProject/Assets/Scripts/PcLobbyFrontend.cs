using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    string directoryEndpoint=NetworkAccountStore.UnityEndpoint,directoryStatus="Подключитесь, чтобы посмотреть комнаты";
    JArray directoryRooms=new JArray(),roomMessages=new JArray();JObject networkRoom;
    Vector2 directoryScroll,chatScroll;PcNetworkClient network;
    bool networkProductionSync;
    bool networkEntryAccount,networkGuest,networkEndpointOpen;
    bool HasEndpointToken=>!string.IsNullOrEmpty(networkSessionToken)&&NetworkAccountStore.SameService(networkSessionEndpoint,directoryEndpoint.Trim());
    bool WaitingForNetworkAccount=>networkEntryAccount&&!networkGuest&&!HasNetworkProfile;
    bool directoryLoading,directoryReady,directoryCanCreate,showNetworkProfile,profilePending;
    float directoryDeadline;
    int networkSlot=-1,networkRace;string networkName="",networkChat="",connectedEndpoint="";
    string[] networkServerRaces;
    bool NetworkRaceAvailable(int index)=>index>=0&&index<networkRaceNames.Length&&(networkServerRaces??networkRaceNames.Take(15).ToArray()).Contains(networkRaceNames[index]);
    int NetworkRaceCount=>(networkServerRaces??networkRaceNames.Take(15).ToArray()).Length;
    string profileName="",profilePassword="",rememberedAccountName="";JObject networkProfile;
    readonly string[] networkRaceNames={"Human Alliance","Orcish Horde","Naga Faction","Nature Force","Chaos Legions","Northern Realms","Mech Coalition","Undead Forces","Corrupted Conglomerate","Elementals' Universe","Night Elfs","Elven Union","Ultimate Builder","Nerubian Swarm","Pandaren Order","Centaur Khanates","Pepe Brotherhood","Black Hand"};
    readonly string[] networkRaceLabels={"Люди","Орки","Наги","Природа","Хаос","Северяне","Механизмы","Нежить","Осквернённые","Элементали","Ночные эльфы","Союз эльфов","Высший строитель","Нерубы","Пандарены","Кентавры","Пепе","Чёрная Рука"};
    void PollLobby() {
        if(profilePending&&profileDeadline>0&&Time.realtimeSinceStartup>profileDeadline){profilePending=false;directoryStatus="Сервис профилей не ответил. Повторите вход";}TickRecovery();UpdateSocial();if(network==null)return;
        if(directoryLoading&&Time.realtimeSinceStartup>=directoryDeadline){network.Dispose();network=null;directoryLoading=false;directoryReady=false;directoryStatus="Сервер не ответил вовремя";return;}
        var current=network;while(network==current&&current.TryRead(out JObject packet)) {
            switch((string)packet["type"]) {
                case "_status":
                    string status=(string)packet["message"]??"";
                    if(status!="Подключено к серверу"){directoryStatus=status;directoryLoading=false;}break;
                case "_disconnected":if(onlineMatch&&!string.IsNullOrEmpty(recoveryToken)){RecoveryDisconnected();}else if(onlineMatch){NetworkReturnToMenu();Notice("Соединение с сервером потеряно");}else{directoryLoading=false;directoryReady=false;networkSlot=-1;networkRoom=null;networkProfile=null;profilePending=false;profilePassword="";}break;
                case "production_sync":
                    int count=(int?)packet["count"]??0;
                    if(onlineMatch)Notice(count>=2?"Следующий выпуск соберёт отряд из "+count+" зданий":"Нужно хотя бы два действующих здания выпуска");
                    break;
                case "directory":
                    networkProductionSync=(int?)packet["production_sync"]==1;
                    spectatorSupported=(bool?)packet["spectator_supported"]==true;
                    socialSupported=(int?)packet["social_supported"]==1;directoryRooms=packet["rooms"] as JArray??new JArray();directoryReady=true;directoryLoading=false;directoryCanCreate=(bool?)packet["can_create"]??false;
                    networkServerRaces=packet["races"] is JArray roster?roster.Select(row=>(string)row).Where(name=>!string.IsNullOrEmpty(name)).ToArray():null;
                    TryAccountAuthentication();
                    directoryStatus=profilePending?"Проверяем сохранённый вход…":"Подключено · открытых игр: "+directoryRooms.Count;break;
                case "joined":ResetBattleSocial();networkProductionSync=(int?)packet["production_sync"]==1;recoveryToken=(string)packet["resume_token"]??"";networkSlot=(int?)packet["slot"]??-1;directoryStatus="Вы в комнате";break;
                case "lobby":networkRoom=packet;roomMessages=(JArray)(packet["messages"]?.DeepClone()??new JArray());break;
                case "chat":if(packet["message"] is JObject message){roomMessages.Add(message.DeepClone());while(roomMessages.Count>40)roomMessages.RemoveAt(0);chatScroll.y=100000;if(onlineMatch&&onlineStarted)AddBattleMessage(message);}break;
                case "profile":
                    if((string)packet["request_type"]=="profile_social"){ReceiveSocial(packet);break;}
                    socialSupported=socialSupported||(int?)packet["social_supported"]==1;profilePending=false;profilePassword="";directoryStatus=(string)packet["message"]??((bool?)packet["ok"]==true?"Профиль обновлён":"Ошибка профиля");
                    string request=(string)packet["request_type"];
                    bool succeeded=(bool?)packet["ok"]==true;
                    bool signedIn=request=="profile_auth"&&succeeded;
                    bool signedOut=request=="profile_logout"&&succeeded;
                    if(signedIn||signedOut){bool supported=socialSupported;ResetSocial();socialSupported=supported;}
                    if(signedIn){
                        networkSessionToken=(string)packet["token"]??"";
                        networkSessionEndpoint=connectedEndpoint;
                        rememberedAccountName="";
                    }
                    bool tokenRejected=request=="profile_auth"&&!succeeded&&(string)packet["action"]=="token"&&directoryStatus.StartsWith("Сессия истекла",StringComparison.Ordinal);
                    if(signedOut||tokenRejected)ClearAccountIdentity();
                    if(succeeded){
                        if(!signedOut&&packet["profile"] is JObject updated)ApplyAccountProfile(updated);
                        if(signedIn||signedOut)ProfileAuthChanged();
                        if(networkEntryAccount&&HasNetworkProfile){
                            networkEntryAccount=false;showNetworkProfile=false;
                        }
                        socialDue=0;
                    }
                    ReceiveAccountOperation(request,packet);
                    break;
                case "error":directoryStatus=(string)packet["message"]??"Ошибка сервера";Debug.LogWarning("CF_NETWORK_SERVER_ERROR "+directoryStatus);if(onlineMatch)Notice(directoryStatus);break;
                case "room_closed":directoryStatus=(string)packet["message"]??"Комната закрыта";if(onlineMatch){string reason=directoryStatus;NetworkReturnToMenu();Notice(reason);}else{networkSlot=-1;networkRoom=null;}break;
                case "resumed":case "connection_hold":case "connection_restored":case "resume_rejected":RecoveryPacket(packet);break;
                case "load":NetworkLoad(packet);break;
                case "snapshot":NetworkSnapshot(packet);break;
                case "spectate_start":case "spectate_error":case "spectate_count":case "spectate_frame":case "spectate_end":
                    ReceiveSpectator(packet);break;
                case "start":if(onlineMatch){onlineStarted=true;CloseVersus();Notice("Сетевой бой начался");}break;
                case "voice_session":ReceiveVoiceSession(packet);break;
                case "emote":if(onlineMatch)ReceiveEmote(packet);break;
                case "emote_rejected":if(onlineMatch)ReceiveStickerRejection(packet);break;
                case "post_match":if(onlineMatch){onlineChoices=packet["choices"] as JArray??new JArray();Notice("Ожидаем решение соперника");}break;
                case "return_lobby":ResetBattleSocial();ResetRecovery(false);socialTab=0;onlineChoices=new JArray();onlineMatch=false;onlineStarted=false;onlineReplica=null;ClearMatch();ReleaseIdleRaceAssets(true);series=null;page=Page.Network;directoryStatus="Вернулись в комнату";break;
            }
        }
    }
    void ApplyAccountProfile(JObject profile){
        networkProfile=profile;
        if(!HasNetworkProfile)return;
        RememberOwnAvatar(profile);
        rememberedAccountName=(string)profile["name"]??"";
        if(rememberedAccountName.Length>0)networkName=rememberedAccountName;
        networkGuest=(bool?)profile["guest"]==true;
        if((string)profile["id"]!=AccountHub.owner){AccountHub.owner=(string)profile["id"]??"";AccountHub.activity="";}
        if(!testing){
            if(NetworkAccountStore.GuestMode!=networkGuest)NetworkAccountStore.SelectMode(networkGuest);
            NetworkAccountStore.Save(networkSessionEndpoint,networkSessionToken,rememberedAccountName,networkGuest);
        }
    }

    void ClearAccountIdentity(){
        networkProfile=null;
        ClearOwnAvatarIdentity();
        AccountHub.owner="";AccountHub.activity="";CloseAccountOverlay();
        networkSessionToken=rememberedAccountName="";
        if(!testing)NetworkAccountStore.Clear(networkGuest);
    }

    async void SendNetwork(JObject packet){
        string type=(string)packet["type"]??"";
        if(type.StartsWith("profile_",StringComparison.Ordinal)&&type!="profile_activity"){
            float now=Time.realtimeSinceStartup;
            socialNext=Mathf.Max(socialNext,now+.65f);
            if(profilePending)profileDeadline=now+18;
        }
        try{if(network!=null)await network.SendAsync(packet);}
        catch(Exception e){directoryStatus="Ошибка отправки: "+e.GetBaseException().Message;}
    }
    void StopDirectory() {
        CloseVersus();showNetworkRacePicker=false;networkLoadError="";failedNetworkLoad=null;networkSight.Clear();networkUnitCards.Clear();networkBuildingCards.Clear();RestoreOfflineNetworkSetup();
        ResetRecovery();ResetSocial();var previous=network;network=null;connectedEndpoint="";directoryLoading=false;directoryReady=false;directoryCanCreate=false;networkSlot=-1;networkRoom=null;networkProfile=null;profilePending=false;showNetworkProfile=false;showStickerPicker=false;profilePassword="";directoryRooms=new JArray();roomMessages=new JArray();
        ResetBattleSocial();DisposeVoice();if(previous!=null)_=previous.LeaveAsync();
    }
    void BeginDirectory() {
        if(directoryLoading)return;
        if(network!=null&&network.Connected&&connectedEndpoint==directoryEndpoint.Trim()){SendNetwork(PcNetworkClient.Handshake("browse","",networkName,networkRaceNames[networkRace]));return;}
        if(!Uri.TryCreate(directoryEndpoint.Trim(),UriKind.Absolute,out var uri)){directoryStatus="Введите адрес сервера wss:// или локальный ws://";return;}
        networkProfile=null;ResetSocial();AccountHub.guestAttempted=false;network?.Dispose();network=new PcNetworkClient();connectedEndpoint=directoryEndpoint.Trim();directoryLoading=true;directoryDeadline=Time.realtimeSinceStartup+15;directoryReady=false;directoryStatus="Подключение к серверу…";directoryRooms=new JArray();
        _=network.ConnectAsync(uri);
    }
    void OpenNetwork(){
        page=Page.Network;
        if(testing){
            string endpoint=BattleLab.Arg("--pc-directory-url");
            if(endpoint=="")return;
            directoryEndpoint=endpoint;
        }
        networkEntryAccount=!testing&&!HasNetworkProfile&&!networkGuest;
        showNetworkProfile=networkEntryAccount;
        profileName=HasEndpointToken?networkName:"";
        BeginDirectory();
    }

    void CloseNetworkAccount(){
        CloseAccountKeyboard();
        profilePassword="";
        showNetworkProfile=false;
        if(!WaitingForNetworkAccount)return;
        networkEntryAccount=false;
        StopDirectory();
        page=Page.Main;
    }

    void ContinueNetworkAsGuest(){
        CloseAccountKeyboard();
        StopDirectory();
        profilePassword="";
        RestoreAccountMode(true);
        if(!testing)NetworkAccountStore.SelectMode(true);
        networkEntryAccount=false;
        showNetworkProfile=false;
        BeginDirectory();
    }
    void EnterRoom(string action,string code) {
        if(MobileContent.Active){StartCoroutine(PrepareMobileRoom(action,code));return;}
        EnterRoomCore(action,code);
    }
    void EnterRoomCore(string action,string code){
        networkName=networkName.Trim();if(networkName.Length==0)networkName="Игрок";PlayerPrefs.SetString("cf-network-name",networkName);PlayerPrefs.Save();
        SendNetwork(PcNetworkClient.Handshake(action,code,networkName,networkRaceNames[networkRace]));directoryStatus=action=="create"?"Создаём комнату…":"Входим в комнату…";
    }
    void ChangeRace(){showNetworkRacePicker=true;}
    void ChooseNetworkRace(int index){if(!NetworkRaceAvailable(index))return;networkRace=index;showNetworkRacePicker=false;if(networkSlot>=0)SendNetwork(new JObject{["type"]="race",["race"]=networkRaceNames[index]});}
    void Configure(string key,JToken value){if(networkSlot!=0||!(networkRoom?["config"] is JObject old))return;var cfg=(JObject)old.DeepClone();cfg[key]=value;SendNetwork(new JObject { ["type"]="configure",["config"]=cfg });}
    Font lobbyFont;
    void LobbyPanel(Rect r)=>AtlasSurface(r);
    bool LobbyButton(string id,Rect r,string caption,bool prominent=false) {
        bool wasEnabled=GUI.enabled;
        GUI.enabled=wasEnabled&&(!showNetworkRacePicker||id.StartsWith("network.race.",StringComparison.Ordinal));
        bool clicked=AtlasControl(id,r,caption.Replace("▾","").TrimEnd(),prominent,caption.Contains("▾"));
        GUI.enabled=wasEnabled;
        return clicked&&!(Application.isMobilePlatform&&socialTouchMoved);
    }
    void DrawNetwork() {
        EnsureCampaignStyles();
        if(!lobbyFont)lobbyFont=ContentResources.Load<Font>("PcLobby/Manrope");
        var oldLabel=label.font;var oldSmall=small.font;var oldHeading=heading.font;var oldInput=menuInput.font;
        if(lobbyFont){label.font=lobbyFont;small.font=lobbyFont;heading.font=lobbyFont;menuInput.font=lobbyFont;}
        try{DrawNetworkContent();if(showNetworkRacePicker)DrawNetworkRacePicker();if(!string.IsNullOrEmpty(networkLoadError))DrawNetworkLoadError();}finally{label.font=oldLabel;small.font=oldSmall;heading.font=oldHeading;menuInput.font=oldInput;}
    }
    void DrawNetworkContent() {
        DrawMenuBackground();AtlasSurface(new Rect(20,14,1240,669));
        bool joined=networkSlot>=0&&networkRoom!=null;
        Text(42,23,580,47,joined?"Игровая комната":"Сетевая игра",atlasTitle);
        if(showNetworkProfile){DrawNetworkProfile();return;}
        if(HasNetworkProfile)DrawFriendsShortcut(new Rect(627,24,49,44));
        if((joined||socialTab>0)&&LobbyButton("network.profile",new Rect(690,24,184,44),"Профиль")){
            if(HasNetworkProfile)OpenSocialTab(3);
            else{profileName="";showNetworkProfile=true;}
        }
        if(joined&&LobbyButton("network.leave",new Rect(887,24,184,44),"К списку игр")){StopDirectory();BeginDirectory();}
        if(LobbyButton("network.back",new Rect(1084,24,154,44),"В меню"))ExitNetworkToMenu();
        DrawSocialTabs();
        Fill(new Rect(20,684,1240,30),AtlasInk);Text(44,684,1160,28,directoryStatus,small);
        if(showStickerPicker){DrawStickerPicker();return;}
        if(socialTab>0){DrawSocialContent();return;}
        GUI.BeginGroup(new Rect(0,26,1280,650));
        if(networkSlot<0||networkRoom==null)DrawDirectory();else DrawRoom();
        GUI.EndGroup();
    }

    void DrawDirectory() {
        LobbyPanel(new Rect(42,112,816,519));
        LobbyPanel(new Rect(877,112,361,519));
        Text(64,129,490,41,"Открытые игры",heading);
        GUI.enabled=!directoryLoading;
        if(LobbyButton("network.refresh",new Rect(679,133,157,38),directoryReady?"Обновить":"Подключить"))BeginDirectory();
        GUI.enabled=true;
        Accent(64,181,772);
        Text(74,188,466,33,"Игра",small);
        Text(530,188,161,33,"Игроки",small);
        directoryScroll=GUI.BeginScrollView(new Rect(62,223,775,384),directoryScroll,new Rect(0,0,747,Mathf.Max(376,directoryRooms.Count*74)));
        if(directoryRooms.Count==0)Text(12,14,715,48,directoryLoading?"Подключаемся к серверу…":directoryReady?"Список пуст. Создайте игру, чтобы пригласить соперника.":"Нет соединения с сервером.",label);
        int row=0;
        foreach(var room in directoryRooms.OfType<JObject>()) {
            bool joinable=(bool?)room["joinable"]==true;
            string state=(string)room["state"]=="playing"?"Идёт бой":(string)room["state"]=="loading"?"Загрузка":joinable?"Ожидает игрока":"Места заняты";
            float y=row*74;
            AtlasTile(new Rect(0,y,741,66),false,false);
            Text(12,y+4,445,28,(string)room["host"]??"Игрок",label);
            Text(12,y+33,445,24,state,small);
            Text(470,y+12,72,42,((int?)room["players"]??0)+" / "+((int?)room["capacity"]??2),label);
            GUI.enabled=directoryReady&&joinable;
            if(LobbyButton("network.join."+row,new Rect(573,y+10,155,46),"Войти",true))EnterRoom("join",(string)room["id"]??"");
            GUI.enabled=true;
            row++;
        }
        GUI.EndScrollView();
        Text(900,131,312,40,HasNetworkProfile?"Вы вошли":"Гостевой вход",heading);
        if(HasNetworkProfile){
            DrawProfileBadge(new Rect(900,174,38,38));
            Text(948,174,260,38,FitAtlasAccountName(networkName,253),atlasProfileName);
        }
        else networkName=AccountText("guest.name",new Rect(900,173,315,44),networkName,24,false,"Имя в игре");
        GUI.enabled=directoryReady;
        if(LobbyButton("network.profile",new Rect(900,230,315,47),"Профиль")){
            if(HasNetworkProfile)OpenSocialTab(3);
            else{profileName="";showNetworkProfile=true;}
        }
        GUI.enabled=directoryReady&&directoryCanCreate;
        if(LobbyButton("network.create",new Rect(900,301,315,58),"Создать игру",true))EnterRoom("create","");
        GUI.enabled=true;
        AtlasScene(2,new Rect(900,374,315,112));
        if(LobbyButton("network.endpoint",new Rect(900,500,315,41),"Адрес сервера"))networkEndpointOpen=!networkEndpointOpen;
        if(networkEndpointOpen){
            directoryEndpoint=GUI.TextField(new Rect(900,548,315,46),directoryEndpoint,256,menuInput);
        }
    }
    void DrawRoom() {
        var players=networkRoom["players"] as JArray??new JArray();var config=networkRoom["config"] as JObject??new JObject();
        string mode=(string)config["mode"]??"fixed";
        for(int i=0;i<2;i++) {
            var player=i<players.Count?players[i] as JObject:null;float y=112+i*108;bool present=player!=null&&player.HasValues;
            LobbyPanel(new Rect(42,y,570,96));
            Text(57,y+7,300,21,i==0?"Создатель":"Соперник",new GUIStyle(small){normal={textColor=new Color(.85f,.73f,.47f)}});
            Text(470,y+7,125,21,present?((bool?)player["ready"]==true?"ГОТОВ":"ВЫБИРАЕТ"):"ОЖИДАНИЕ",small);
            string name=present?(string)player["name"]??"Игрок":"Свободный слот";
            string account=(string)player?["id"]??"";if(!string.IsNullOrEmpty(account)){if(LobbyButton("network.player."+i,new Rect(58,y+31,232,42),name+(i==networkSlot?" · вы":""))){socialTab=1;OpenPlayer(account);}}else Text(58,y+31,232,42,name+(i==networkSlot?" · вы":""),new GUIStyle(label){wordWrap=false,clipping=TextClipping.Clip});
            if(present){RankImage(new Rect(302,y+38,36,36),(int?)player["wins"]??0);}
            string raw=present?(string)player["race"]??"":"";int raceIndex=Array.IndexOf(networkRaceNames,raw);
            string race=mode=="random"?"Случайная раса":mode=="mirror"&&i==1?"Как у создателя":raceIndex>=0?networkRaceLabels[raceIndex]:raw;
            if(i==networkSlot&&present){GUI.enabled=mode!="random"&&!(mode=="mirror"&&i==1);if(LobbyButton("network.change-race",new Rect(350,y+36,246,42),race+"  ▾"))ChangeRace();GUI.enabled=true;}
            else Text(350,y+38,242,36,present?race:"Ждём вашего друга…",small);
        }
        LobbyPanel(new Rect(42,329,570,184));
        Text(58,338,520,25,"НАСТРОЙКИ СРАЖЕНИЯ",new GUIStyle(small){normal={textColor=new Color(.85f,.73f,.47f)}});
        int gold=(int?)config["gold"]??250,rounds=(int?)config["rounds"]??1;bool fog=(bool?)config["fog"]??true;
        GUI.enabled=networkSlot==0;
        int wins=rounds/2+1;if(LobbyButton("network.rounds",new Rect(58,369,250,39),"До "+wins+(wins==1?" победы":" побед")+"  ▾"))Configure("rounds",rounds==1?3:rounds==3?5:rounds==5?7:1);
        if(LobbyButton("network.gold",new Rect(320,369,276,39),"Золото: "+gold+"  ▾"))Configure("gold",gold==250?500:gold==500?1000:250);
        if(LobbyButton("network.mode",new Rect(58,418,538,37),mode=="fixed"?"Выбранные расы  ▾":mode=="random"?"Случайные расы  ▾":"Зеркальные расы  ▾"))Configure("mode",mode=="fixed"?"mirror":mode=="mirror"?"random":"fixed");
        if(LobbyButton("network.fog",new Rect(58,464,250,36),"Туман войны: "+(fog?"вкл.":"выкл.")))Configure("fog",!fog);
        bool aspects=(bool?)config["aspects"]??false;
        if(LobbyButton("network.aspects",new Rect(320,464,276,36),"Аспекты: "+(aspects?"вкл.":"выкл.")))Configure("aspects",!aspects);
        GUI.enabled=true;
        LobbyPanel(new Rect(630,112,608,401));Text(648,124,560,25,"ЧАТ КОМНАТЫ  ·  КОД "+((string)networkRoom["code"]??""),new GUIStyle(small){normal={textColor=new Color(.85f,.73f,.47f)}});
        var roomChatStyle=new GUIStyle(small){fontSize=15,wordWrap=true,padding=new RectOffset(0,0,0,0)};float chatHeight=roomMessages.Sum(item=>Mathf.Max(58,roomChatStyle.CalcHeight(new GUIContent((string)item["text"]??""),510)+34)+10);
        chatScroll=GUI.BeginScrollView(new Rect(648,157,572,278),chatScroll,new Rect(0,0,545,Mathf.Max(265,chatHeight)));
        if(roomMessages.Count==0)Text(4,9,520,58,"Сообщений пока нет.",small);
        float chatY=0;foreach(var item in roomMessages){string name=(string)item["name"]??"Игрок",body=(string)item["text"]??"";float height=Mathf.Max(58,roomChatStyle.CalcHeight(new GUIContent(body),510)+34);Fill(new Rect(0,chatY,535,height),new Color(.045f,.079f,.092f));Text(12,chatY+5,510,24,name,new GUIStyle(small){fontSize=13,wordWrap=false,normal={textColor=new Color(.84f,.72f,.48f)}});Text(12,chatY+30,510,height-30,body,roomChatStyle);chatY+=height+10;}GUI.EndScrollView();
        networkChat=GUI.TextField(new Rect(648,453,394,42),networkChat,180,menuInput);
        if(LobbyButton("network.chat",new Rect(1052,453,168,42),"Отправить")){string body=networkChat.Trim();if(body.Length>0){SendNetwork(new JObject { ["type"]="chat",["text"]=body });networkChat="";}}
        bool ready=networkSlot<players.Count&&(bool?)players[networkSlot]?["ready"]==true;
        if(LobbyButton("network.ready",new Rect(42,531,570,51),ready?"Отменить готовность":"Я готов"))SetNetworkReady(!ready);
        bool supported=players.Count==2&&players.All(p=>p is JObject j&&j.HasValues&&Array.IndexOf(networkRaceNames,(string)j["race"])>=0&&(bool?)j["ready"]==true);
        GUI.enabled=networkSlot==0&&supported;if(LobbyButton("network.start",new Rect(630,531,608,51),networkSlot==0?"Начать сражение →":"Ожидаем старта…",true))SendNetwork(new JObject { ["type"]="start" });GUI.enabled=true;
        Text(44,583,1160,22,Application.isMobilePlatform&&mode=="random"?"Случайные расы: перед готовностью скачаются модели всех рас; в память загрузятся только две.":"После готовности обоих игроков создатель комнаты нажимает «Начать сражение».",small);
        if(LobbyButton("network.stickers",new Rect(42,605,280,42),"Стикеры"))showStickerPicker=true;
        if(LobbyButton("network.voice",new Rect(334,605,280,42),"Голосовая связь"))showVoicePanel=true;
    }
    void AuthenticateProfile(string action) {
        CloseAccountKeyboard();string name=profileName.Trim();
        if(name.Length<3||name.Length>20||profilePassword.Length<6){directoryStatus="Имя: 3–20 символов, пароль: не менее 6";return;}
        if(profilePending||network==null||!network.Connected)return;
        profilePending=true;directoryStatus=action=="login"?"Выполняем вход…":"Создаём аккаунт…";
        SendNetwork(new JObject { ["type"]="profile_auth",["action"]=action,["name"]=name,["password"]=profilePassword });profilePassword="";
    }
    void DrawNetworkProfile() {
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.72f));
        AtlasSurface(new Rect(335,115,610,494),true);
        Text(374,151,535,45,HasNetworkProfile?"Профиль игрока":page==Page.Profile?"Вход в аккаунт":"Вход в сетевую игру",heading);
        Accent(375,204,530);
        bool entryCheckingToken=WaitingForNetworkAccount&&HasEndpointToken&&(directoryLoading||profilePending||!directoryReady);
        if(entryCheckingToken){
            Text(380,264,500,74,"Восстанавливаем вход в сетевой аккаунт…",label);
        }else if(!HasNetworkProfile) {
            Text(380,221,515,48,"Аккаунт хранит ранг, историю матчей и достижения.",small);
            Text(380,295,150,29,"Имя",label);profileName=AccountText("account.login",new Rect(530,291,350,40),profileName,20,false,"Логин");
            Text(380,350,150,29,"Пароль",label);profilePassword=AccountText("account.password",new Rect(530,346,350,40),profilePassword,128,true,"Пароль");
            GUI.enabled=!profilePending&&directoryReady;
            if(Button("profile.login",new Rect(380,416,235,43),"Войти",true))AuthenticateProfile("login");
            if(Button("profile.register",new Rect(630,416,250,43),"Создать аккаунт"))AuthenticateProfile("register");GUI.enabled=true;
        } else {
            Text(380,232,500,34,networkGuest?"Гостевой аккаунт · ключ хранится на устройстве":"Логин · "+((string)networkProfile["login"]??(string)networkProfile["name"]),label);
            Text(380,271,500,30,"Победы в сети · "+((int?)networkProfile["wins"]??0)+" / "+((int?)networkProfile["games"]??0)+"   |   Жетоны · "+AccountCoins,small);
            Text(380,319,110,41,"Ник",label);
            if(string.IsNullOrEmpty(socialNickname))socialNickname=(string)networkProfile["name"]??"";
            socialNickname=AccountText("account.nickname",new Rect(487,319,393,43),socialNickname,24,false,"Ник");
            GUI.enabled=!profilePending&&!socialPending&&Time.realtimeSinceStartup>=socialNext&&networkSlot<0;
            if(Button("profile.logout",new Rect(380,415,241,43),networkGuest?"Войти в аккаунт":"Выйти из аккаунта")){
                if(networkGuest){SwitchFromGuest();GUI.enabled=true;return;}
                profilePending=true;SendNetwork(new JObject{["type"]="profile_logout"});
            }
            if(Button("profile.rename",new Rect(635,415,245,43),"Сохранить ник",true)){CloseAccountKeyboard();profilePending=true;SendNetwork(new JObject{["type"]="profile_update",["nickname"]=socialNickname.Trim()});}
            GUI.enabled=true;
        }
        Text(380,478,500,38,directoryStatus,small);
        if(!HasNetworkProfile&&!directoryReady&&!directoryLoading&&!profilePending&&
            LobbyButton("profile.retry",new Rect(380,263,500,30),"Подключиться снова"))BeginDirectory();
        bool entry=WaitingForNetworkAccount;
        bool offerGuest=!HasNetworkProfile&&!networkGuest;
        if(offerGuest&&Button("profile.guest",new Rect(380,542,318,40),"Играть гостем"))ContinueNetworkAsGuest();
        if(Button("profile.close",new Rect(offerGuest?716:380,542,offerGuest?164:500,40),entry?"В меню":"Закрыть"))CloseNetworkAccount();
    }
    IEnumerator ExerciseNetworkLobby(string folder) {
        yield return Action("network.create");float until=Time.realtimeSinceStartup+12;
        while((networkSlot!=0||networkRoom==null)&&Time.realtimeSinceStartup<until)yield return null;
        Check(networkSlot==0&&networkRoom!=null,"network room created from UI");
        if(networkSlot!=0||networkRoom==null)yield break;
        yield return Action("network.rounds");until=Time.realtimeSinceStartup+8;
        while((int?)networkRoom?["config"]?["rounds"]!=3&&Time.realtimeSinceStartup<until)yield return null;
        Check((int?)networkRoom?["config"]?["rounds"]==3,"network round setting updates through server");
        yield return Action("network.ready");until=Time.realtimeSinceStartup+8;
        while((bool?)networkRoom?["players"]?[0]?["ready"]!=true&&Time.realtimeSinceStartup<until)yield return null;
        Check((bool?)networkRoom?["players"]?[0]?["ready"]==true,"network ready state updates through server");
        networkChat="Привет из Unity";yield return Action("network.chat");until=Time.realtimeSinceStartup+8;
        while(roomMessages.Count==0&&Time.realtimeSinceStartup<until)yield return null;
        Check(roomMessages.Count>0&&(string)roomMessages[roomMessages.Count-1]["text"]=="Привет из Unity","network room chat updates through server");
        yield return Action("network.stickers");Check(stickerPacks!=null&&stickerPacks.Count>1&&ContentResources.Load<Texture2D>(stickerPacks[0].items[0].resource)!=null,"Godot sticker catalog and classic art loaded");
        yield return Action("sticker.pack");Check(ContentResources.Load<Texture2D>(stickerPacks[stickerPack].items[0].resource)!=null,"Godot race sticker sheet loaded");yield return Shot(folder,"01c-stickers");yield return Action("sticker.close");
        yield return Action("network.leave");until=Time.realtimeSinceStartup+8;
        while(!directoryReady&&Time.realtimeSinceStartup<until)yield return null;
        Check(directoryReady&&networkSlot<0,"leaving room returns to directory");
    }
}
}
