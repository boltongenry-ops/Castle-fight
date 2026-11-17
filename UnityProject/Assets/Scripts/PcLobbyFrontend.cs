using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    string directoryEndpoint="wss://45-15-156-101.fatmetal.net/play",directoryStatus="Подключитесь, чтобы посмотреть комнаты";
    JArray directoryRooms=new JArray(),roomMessages=new JArray();JObject networkRoom;
    Vector2 directoryScroll,chatScroll;PcNetworkClient network;
    bool directoryLoading,directoryReady,directoryCanCreate,showNetworkProfile,profilePending;
    float directoryDeadline;
    int networkSlot=-1,networkRace;string networkName="",networkChat="",connectedEndpoint="";
    string profileName="",profilePassword="";JObject networkProfile;
    readonly string[] networkRaceNames={"Human Alliance","Orcish Horde","Naga Faction","Nature Force","Chaos Legions","Northern Realms","Mech Coalition","Undead Forces","Corrupted Conglomerate","Elementals' Universe","Night Elfs","Elven Union","Ultimate Builder","Nerubian Swarm","Pandaren Order"};
    readonly string[] networkRaceLabels={"Люди","Орки","Наги","Природа","Хаос","Северяне","Механизмы","Нежить","Осквернённые","Элементали","Ночные эльфы","Союз эльфов","Высший строитель","Нерубы","Пандарены"};
    void PollLobby() {
        if(profilePending&&profileDeadline>0&&Time.realtimeSinceStartup>profileDeadline){profilePending=false;directoryStatus="Сервис профилей не ответил. Повторите вход";}TickRecovery();UpdateSocial();if(network==null)return;
        if(directoryLoading&&Time.realtimeSinceStartup>=directoryDeadline){network.Dispose();network=null;directoryLoading=false;directoryReady=false;directoryStatus="Сервер не ответил вовремя";return;}
        var current=network;while(network==current&&current.TryRead(out JObject packet)) {
            switch((string)packet["type"]) {
                case "_status":
                    string status=(string)packet["message"]??"";
                    if(status!="Подключено к серверу"){directoryStatus=status;directoryLoading=false;}break;
                case "_disconnected":if(onlineMatch&&!string.IsNullOrEmpty(recoveryToken)){RecoveryDisconnected();}else if(onlineMatch){NetworkReturnToMenu();Notice("Соединение с сервером потеряно");}else{directoryLoading=false;directoryReady=false;networkSlot=-1;networkRoom=null;networkProfile=null;profilePending=false;profilePassword="";}break;
                case "directory":
                    socialSupported=(int?)packet["social_supported"]==1;directoryRooms=packet["rooms"] as JArray??new JArray();directoryReady=true;directoryLoading=false;directoryCanCreate=(bool?)packet["can_create"]??false;
                    if(!HasNetworkProfile&&!profilePending&&!string.IsNullOrEmpty(networkSessionToken)&&networkSessionEndpoint==connectedEndpoint){profilePending=true;SendNetwork(new JObject{["type"]="profile_auth",["action"]="token",["token"]=networkSessionToken});}
                    directoryStatus="Сервер ответил · версия "+((string)packet["build"]??"?")+" · комнат: "+directoryRooms.Count;break;
                case "joined":recoveryToken=(string)packet["resume_token"]??"";networkSlot=(int?)packet["slot"]??-1;directoryStatus="Вы в комнате";break;
                case "lobby":networkRoom=packet;roomMessages=(JArray)(packet["messages"]?.DeepClone()??new JArray());break;
                case "chat":if(packet["message"] is JObject message){roomMessages.Add(message.DeepClone());while(roomMessages.Count>40)roomMessages.RemoveAt(0);chatScroll.y=100000;}break;
                case "profile":
                    if((string)packet["request_type"]=="profile_social"){ReceiveSocial(packet);break;}
                    socialSupported=socialSupported||(int?)packet["social_supported"]==1;profilePending=false;profilePassword="";directoryStatus=(string)packet["message"]??((bool?)packet["ok"]==true?"Профиль обновлён":"Ошибка профиля");
                    if((string)packet["request_type"]=="profile_auth"){if((bool?)packet["ok"]==true){bool supported=socialSupported;ResetSocial();socialSupported=supported;networkSessionToken=(string)packet["token"]??"";networkSessionEndpoint=connectedEndpoint;}else if((string)packet["action"]=="token")networkSessionToken="";}
                    if((string)packet["request_type"]=="profile_logout"&&(bool?)packet["ok"]==true){networkSessionToken="";bool supported=socialSupported;ResetSocial();socialSupported=supported;}
                    if((bool?)packet["ok"]==true){if(packet["profile"] is JObject updated)networkProfile=updated;if(networkProfile!=null&&networkProfile.HasValues)networkName=(string)networkProfile["name"]??networkName;socialDue=0;}break;
                case "error":directoryStatus=(string)packet["message"]??"Ошибка сервера";Debug.LogWarning("CF_NETWORK_SERVER_ERROR "+directoryStatus);if(onlineMatch)Notice(directoryStatus);break;
                case "room_closed":directoryStatus=(string)packet["message"]??"Комната закрыта";if(onlineMatch){string reason=directoryStatus;NetworkReturnToMenu();Notice(reason);}else{networkSlot=-1;networkRoom=null;}break;
                case "resumed":case "connection_hold":case "connection_restored":case "resume_rejected":RecoveryPacket(packet);break;
                case "load":NetworkLoad(packet);break;
                case "snapshot":NetworkSnapshot(packet);break;
                case "start":if(onlineMatch){onlineStarted=true;CloseVersus();Notice("Сетевой бой начался");}break;
                case "voice_session":ReceiveVoiceSession(packet);break;
                case "emote":if(onlineMatch)ReceiveEmote(packet);break;
                case "post_match":if(onlineMatch){onlineChoices=packet["choices"] as JArray??new JArray();Notice("Ожидаем решение соперника");}break;
                case "return_lobby":ResetRecovery(false);socialTab=0;onlineChoices=new JArray();onlineMatch=false;onlineStarted=false;onlineReplica=null;ClearMatch();ReleaseIdleRaceAssets(true);series=null;page=Page.Network;directoryStatus="Вернулись в комнату";break;
            }
        }
    }
    async void SendNetwork(JObject packet){if(((string)packet["type"]??"").StartsWith("profile_")&&(string)packet["type"]!="profile_social")profileDeadline=Time.realtimeSinceStartup+18;try{if(network!=null)await network.SendAsync(packet);}catch(Exception e){directoryStatus="Ошибка отправки: "+e.GetBaseException().Message;}}
    void StopDirectory() {
        CloseVersus();showNetworkRacePicker=false;networkLoadError="";failedNetworkLoad=null;networkSight.Clear();networkUnitCards.Clear();networkBuildingCards.Clear();RestoreOfflineNetworkSetup();
        ResetRecovery();ResetSocial();var previous=network;network=null;connectedEndpoint="";directoryLoading=false;directoryReady=false;directoryCanCreate=false;networkSlot=-1;networkRoom=null;networkProfile=null;profilePending=false;showNetworkProfile=false;showStickerPicker=false;profilePassword="";directoryRooms=new JArray();roomMessages=new JArray();
        onlineStickerSerial=-1;onlineStickers[0]=onlineStickers[1]=null;DisposeVoice();if(previous!=null)_=previous.LeaveAsync();
    }
    void BeginDirectory() {
        if(directoryLoading)return;
        if(network!=null&&network.Connected&&connectedEndpoint==directoryEndpoint.Trim()){SendNetwork(PcNetworkClient.Handshake("browse","",networkName,networkRaceNames[networkRace]));return;}
        if(!Uri.TryCreate(directoryEndpoint.Trim(),UriKind.Absolute,out var uri)){directoryStatus="Введите адрес сервера wss:// или локальный ws://";return;}
        networkProfile=null;ResetSocial();network?.Dispose();network=new PcNetworkClient();connectedEndpoint=directoryEndpoint.Trim();directoryLoading=true;directoryDeadline=Time.realtimeSinceStartup+15;directoryReady=false;directoryStatus="Подключение к серверу…";directoryRooms=new JArray();
        _=network.ConnectAsync(uri);
    }
    void OpenNetwork(){page=Page.Network;if(testing){string endpoint=BattleLab.Arg("--pc-directory-url");if(endpoint=="")return;directoryEndpoint=endpoint;}BeginDirectory();}
    void EnterRoom(string action,string code) {
        if(MobileContent.Active){StartCoroutine(PrepareMobileRoom(action,code));return;}
        EnterRoomCore(action,code);
    }
    void EnterRoomCore(string action,string code){
        networkName=networkName.Trim();if(networkName.Length==0)networkName="Игрок";PlayerPrefs.SetString("cf-network-name",networkName);PlayerPrefs.Save();
        SendNetwork(PcNetworkClient.Handshake(action,code,networkName,networkRaceNames[networkRace]));directoryStatus=action=="create"?"Создаём комнату…":"Входим в комнату…";
    }
    void ChangeRace(){showNetworkRacePicker=true;}
    void ChooseNetworkRace(int index){networkRace=index;showNetworkRacePicker=false;if(networkSlot>=0)SendNetwork(new JObject{["type"]="race",["race"]=networkRaceNames[index]});}
    void Configure(string key,JToken value){if(networkSlot!=0||!(networkRoom?["config"] is JObject old))return;var cfg=(JObject)old.DeepClone();cfg[key]=value;SendNetwork(new JObject { ["type"]="configure",["config"]=cfg });}
    GUIStyle lobbyButtonStyle,lobbyPrimaryStyle;Font lobbyFont;
    // The layout and palette follow the Godot lobby: a common header, two player
    // cards and room settings on the left, chat on the right, actions below.
    void LobbyPanel(Rect r) {
        Fill(new Rect(r.x+3,r.y+4,r.width,r.height),new Color(0,0,0,.28f));
        Fill(r,new Color(.025f,.05f,.065f,.96f));Border(r,new Color(.32f,.38f,.33f,.95f));
        Fill(new Rect(r.x+1,r.y+1,r.width-2,2),new Color(.43f,.43f,.32f,.7f));
    }
    bool LobbyButton(string id,Rect r,string caption,bool prominent=false) {
        bool enabled=GUI.enabled&&(!showNetworkRacePicker||id.StartsWith("network.race.")),hovered=enabled&&r.Contains(Event.current.mousePosition);
        bool dropdown=caption.Contains("▾");caption=caption.Replace("▾","").TrimEnd();
        Color background=!enabled?new Color(.063f,.10f,.125f):hovered?new Color(.13f,.20f,.22f):prominent?new Color(.13f,.20f,.20f):new Color(.078f,.137f,.165f);
        Color edge=!enabled?new Color(.19f,.24f,.26f):hovered?new Color(.75f,.69f,.51f):prominent?new Color(.64f,.54f,.35f):new Color(.27f,.34f,.35f);
        Fill(new Rect(r.x+2,r.y+3,r.width,r.height),new Color(0,0,0,.32f));Fill(r,background);Border(r,edge);
        Fill(new Rect(r.x+7,r.y+2,r.width-14,1),new Color(1,1,1,enabled?.11f:.04f));
        if(prominent&&enabled)Fill(new Rect(r.x+2,r.y+r.height*.5f-7,2,14),new Color(.85f,.72f,.47f));
        if(lobbyButtonStyle==null){lobbyButtonStyle=new GUIStyle(menuButton){fontSize=16,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(8,8,0,0)};
            lobbyButtonStyle.normal.background=null;lobbyButtonStyle.hover.background=null;lobbyButtonStyle.active.background=null;lobbyButtonStyle.focused.background=null;
            lobbyPrimaryStyle=new GUIStyle(lobbyButtonStyle);}
        var style=prominent?lobbyPrimaryStyle:lobbyButtonStyle;if(lobbyFont)style.font=lobbyFont;
        style.normal.textColor=!enabled?new Color(.45f,.5f,.49f):prominent?new Color(1,.88f,.64f):new Color(.91f,.9f,.86f);
        if(dropdown)DrawLobbyChevron(new Rect(r.xMax-25,r.center.y-4,10,8),enabled);
        string visible=caption;float available=r.width-(dropdown?48:18);if(style.CalcSize(new GUIContent(visible)).x>available){while(visible.Length>1&&style.CalcSize(new GUIContent(visible+"…")).x>available)visible=visible.Substring(0,visible.Length-1);visible+="…";}bool clicked=Button(id,r,visible,false,style);return clicked&&enabled&&!(Application.isMobilePlatform&&socialTouchMoved);
    }
    void DrawNetwork() {
        if(!lobbyFont)lobbyFont=ContentResources.Load<Font>("PcLobby/Manrope");
        var oldLabel=label.font;var oldSmall=small.font;var oldHeading=heading.font;var oldInput=menuInput.font;
        if(lobbyFont){label.font=lobbyFont;small.font=lobbyFont;heading.font=lobbyFont;menuInput.font=lobbyFont;}
        try{DrawNetworkContent();if(showNetworkRacePicker)DrawNetworkRacePicker();if(!string.IsNullOrEmpty(networkLoadError))DrawNetworkLoadError();}finally{label.font=oldLabel;small.font=oldSmall;heading.font=oldHeading;menuInput.font=oldInput;}
    }
    void DrawNetworkContent() {
        DrawMenuBackground();Fill(new Rect(0,0,1280,720),new Color(.01f,.025f,.035f,.45f));
        bool joined=networkSlot>=0&&networkRoom!=null;
        Text(42,25,580,42,joined?"КОМНАТА  /  1 × 1":"ЗАЛ БИТВ  /  1 × 1",new GUIStyle(heading){fontSize=29,normal={textColor=new Color(.85f,.73f,.47f)}});
        GUI.enabled=directoryReady;if(LobbyButton("network.profile",new Rect(718,24,206,44),networkProfile!=null&&networkProfile.HasValues?"Профиль: "+networkName:"Профиль / Войти")){if(HasNetworkProfile)OpenSocialTab(3);else{profileName=networkName;showNetworkProfile=true;}}GUI.enabled=true;
        if(joined&&LobbyButton("network.leave",new Rect(936,24,144,44),"К комнатам")){StopDirectory();BeginDirectory();}
        if(LobbyButton("network.back",new Rect(1092,24,146,44),"← В меню")){StopDirectory();page=Page.Main;}
        DrawSocialTabs();
        Text(44,687,1160,28,directoryStatus,small);
        if(showNetworkProfile){DrawNetworkProfile();return;}if(showStickerPicker){DrawStickerPicker();return;}
        if(socialTab>0){DrawSocialContent();return;}
        networkPageScroll=GUI.BeginScrollView(new Rect(0,136,1270,540),networkPageScroll,new Rect(0,0,1250,590));
        GUI.BeginGroup(new Rect(0,-110,1280,720));
        if(networkSlot<0||networkRoom==null)DrawDirectory();else DrawRoom();
        GUI.EndGroup();GUI.EndScrollView();
    }

    void DrawDirectory() {
        LobbyPanel(new Rect(42,112,1196,66));
        Text(60,119,1100,28,"Создайте комнату — друг увидит её в этом списке.",label);
        Text(60,148,1100,24,"Сетевая битва 1 × 1",small);
        networkName=AccountText("guest.name",new Rect(42,194,810,48),networkName,24,false,"Имя игрока");
        if(LobbyButton("network.race",new Rect(866,194,372,48),"Раса: "+networkRaceLabels[networkRace]+"  ▾"))ChangeRace();
        GUI.enabled=directoryReady&&directoryCanCreate;if(LobbyButton("network.create",new Rect(42,255,956,50),"+ Создать комнату",true))EnterRoom("create","");GUI.enabled=true;
        GUI.enabled=!directoryLoading;if(LobbyButton("network.refresh",new Rect(1012,255,226,50),directoryReady?"Обновить":"Подключиться"))BeginDirectory();GUI.enabled=true;
        Text(44,319,550,26,"ОТКРЫТЫЕ КОМНАТЫ",new GUIStyle(small){normal={textColor=new Color(.85f,.73f,.47f)}});
        LobbyPanel(new Rect(42,349,1196,212));
        directoryScroll=GUI.BeginScrollView(new Rect(54,359,1172,192),directoryScroll,new Rect(0,0,1138,Mathf.Max(180,directoryRooms.Count*76)));
        if(directoryRooms.Count==0)Text(16,20,1000,40,directoryLoading?"Подключаемся к серверу…":directoryReady?"Пока нет комнат. Создайте первую.":"Подключитесь к серверу.",label);
        int row=0;foreach(var token in directoryRooms) {
            if(!(token is JObject room))continue;string host=(string)room["host"]??"Игрок";if(host.Length>24)host=host.Substring(0,24);
            string state=(string)room["state"]=="playing"?"Идёт сражение":(string)room["state"]=="loading"?"Загрузка боя":(bool?)room["joinable"]==true?"Ожидает соперника":"Все места заняты";
            float y=row*76;Fill(new Rect(2,y+2,1120,68),new Color(.045f,.075f,.08f,.88f));
            Text(16,y+5,850,29,"Комната · "+host,label);Text(16,y+36,850,24,((int?)room["players"]??0)+" / "+((int?)room["capacity"]??2)+" игроков  ·  "+state,small);
            GUI.enabled=(bool?)room["joinable"]??false;if(LobbyButton("network.join."+row,new Rect(922,y+11,184,48),"Войти →",true))EnterRoom("join",(string)room["id"]??"");GUI.enabled=true;row++;
        }
        GUI.EndScrollView();
        Text(42,580,240,23,"АДРЕС СЕРВЕРА",small);directoryEndpoint=GUI.TextField(new Rect(42,608,842,42),directoryEndpoint,256,menuInput);
        Text(42,665,1150,32,"15 рас · выбранные, зеркальные или случайные расы · туман и аспекты настраиваются в комнате",small);
    }
    void DrawRoom() {
        var players=networkRoom["players"] as JArray??new JArray();var config=networkRoom["config"] as JObject??new JObject();
        string mode=(string)config["mode"]??"fixed";
        for(int i=0;i<2;i++) {
            var player=i<players.Count?players[i] as JObject:null;float y=112+i*108;bool present=player!=null&&player.HasValues;
            LobbyPanel(new Rect(42,y,570,96));
            Text(57,y+7,300,21,i==0?"01  /  СОЗДАТЕЛЬ":"02  /  СОПЕРНИК",new GUIStyle(small){normal={textColor=new Color(.85f,.73f,.47f)}});
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
        if(roomMessages.Count==0)Text(4,9,520,58,"Договоритесь о тактике\nили пожелайте удачи сопернику.",small);
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
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.7f));Box(new Rect(345,140,590,440));Text(380,169,500,40,"ПРОФИЛЬ ИГРОКА",heading);Accent(380,220,520);
        if(networkProfile==null||!networkProfile.HasValues) {
            Text(380,239,515,52,"Войдите или играйте гостем. Пароль не сохраняется. Сессия действует до закрытия игры.",small);
            Text(380,295,150,29,"Имя",label);profileName=AccountText("account.login",new Rect(530,291,350,40),profileName,20,false,"Логин");
            Text(380,350,150,29,"Пароль",label);profilePassword=AccountText("account.password",new Rect(530,346,350,40),profilePassword,128,true,"Пароль");
            GUI.enabled=!profilePending;
            if(Button("profile.login",new Rect(380,416,235,43),"Войти",true))AuthenticateProfile("login");
            if(Button("profile.register",new Rect(630,416,250,43),"Создать аккаунт"))AuthenticateProfile("register");GUI.enabled=true;
        } else {
            Text(380,246,500,36,(string)networkProfile["name"]??"Игрок",heading);Text(380,282,500,25,"Логин: "+((string)networkProfile["login"]??(string)networkProfile["name"]),small);
            Text(380,307,500,70,"Сеть: "+((int?)networkProfile["wins"]??0)+" побед / "+((int?)networkProfile["games"]??0)+" игр\nЖетоны: "+((int?)networkProfile["coins"]??0),label);
            GUI.enabled=!profilePending&&!socialPending&&Time.realtimeSinceStartup>=socialNext;if(Button("profile.logout",new Rect(380,416,500,43),"Выйти из аккаунта")){profilePending=true;SendNetwork(new JObject { ["type"]="profile_logout" });}GUI.enabled=true;
        }
        Text(380,478,500,38,directoryStatus,small);
        if(Button("profile.close",new Rect(380,521,500,38),"Закрыть")){CloseAccountKeyboard();profilePassword="";showNetworkProfile=false;}
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
