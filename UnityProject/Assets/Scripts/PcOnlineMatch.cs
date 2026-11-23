using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    bool onlineMatch,onlineStarted,onlineDecision,onlineFog;float onlineSnapshotAt;
    JArray networkRoundLoadouts;string networkLoadError="";
    PcServerReplica onlineReplica;
    JArray onlineTeamOverview;
    float? onlineIncomeIn;
    readonly Dictionary<int,GameObject> onlineProjectiles=new Dictionary<int,GameObject>();
    sealed class OnlineSticker {public StickerItem item;public string name;public float started;}
    readonly OnlineSticker[] onlineStickers=new OnlineSticker[2];
    readonly int[] onlineStickerSerials={-1,-1};
    float nextBattleSticker,stickerDrawerProgress;
    sealed class BattleMessage {public string text;public bool own;public float time;}
    readonly List<BattleMessage> battleMessages=new List<BattleMessage>(4);
    static readonly Rect BattleChatEntry=new Rect(18,459,478,55);
    const string BattleChatField="battle-chat-input";
    bool showBattleChat,focusBattleChat;
    string battleChat="",onlineEmoteId="";

    void ResetBattleSocial(){
        battleChat="";battleMessages.Clear();showBattleChat=focusBattleChat=false;
        showStickerPicker=false;stickerDrawerProgress=0;nextBattleSticker=0;
        onlineEmoteId="";
        for(int side=0;side<onlineStickers.Length;side++){onlineStickers[side]=null;onlineStickerSerials[side]=-1;}
    }
    int OnlineKind(int team,string raw) {
        if(raw=="u00F")raw="h030"; // Native immediate lava replacement has no renderable parent model.
        int race=team==0?matchRace:matchBotRace;
        for(int i=0;i<match.Lines.Length;i++)if(match.Lines[i].unit==raw&&raceIndices[race].ContainsKey(match.Lines[i].building))return i;
        for(int i=0;i<match.Lines.Length;i++)if(match.Lines[i].unit==raw)return i;
        return -1;
    }
    void NetworkLoad(JObject packet){failedNetworkLoad=(JObject)packet.DeepClone();networkLoadError="";StartCoroutine(GuardNetworkLoad(NetworkVersus(packet)));}
    void NetworkLoadCore(JObject packet) {
        ResetRecovery(false);resultDetails=false;onlineChoices=new JArray();onlineBattleStats=new JArray();
        var races=packet["races"] as JArray;var config=packet["config"] as JObject;
        int a=races!=null&&races.Count==2&&(networkSlot==0||networkSlot==1)?Array.IndexOf(networkRaceNames,(string)races[networkSlot]):-1;
        int b=races!=null&&races.Count==2&&(networkSlot==0||networkSlot==1)?Array.IndexOf(networkRaceNames,(string)races[1-networkSlot]):-1;
        if(a<0||b<0||(a!=0&&a!=BlackHand.Race&&!Pack(a))||(b!=0&&b!=BlackHand.Race&&!Pack(b))) {
            FailNetworkLoad("Не удалось подготовить выбранную расу. Повторите загрузку ресурсов и вход в комнату.");return;
        }
        SaveOfflineNetworkSetup();matchMode=0;raceChoice=a;botRaceChoice=b;startGold=(int?)config?["gold"]??250;aspectsOn=(bool?)config?["aspects"]??false;
        int rounds=(int?)config?["rounds"]??1;roundsChoice=new[]{1,3,5,7}.Contains(rounds)?rounds:1;
        networkRoundLoadouts=packet["ultimate_loadouts"] as JArray;
        if((a==12||b==12)&&!ValidateNetworkLoadouts(a,b,networkRoundLoadouts)){FailNetworkLoad("Сервер прислал некорректный набор Высшего строителя.");networkRoundLoadouts=null;return;}
        onlineFog=(bool?)config?["fog"]??true;ClearOnlineProjectiles();
        try{series=new PcSeries(roundsChoice);StartRound();}finally{networkRoundLoadouts=null;}
        var wins=packet["wins"] as JArray;series.SetNetworkRound((int?)packet["round"]??1,wins!=null&&wins.Count==2?(int?)wins[networkSlot]??0:0,wins!=null&&wins.Count==2?(int?)wins[1-networkSlot]??0:0);
        AspectBeginRound(false);aspectWaiting=false;aspectOnlinePending=-1;match.Aspects[0]=-1;match.Aspects[1]=-1;
        onlineMatch=true;onlineStarted=false;onlineDecision=false;ResetBattleSocial();showVoicePanel=false;match.NetworkReplica=true;onlineReplica=new PcServerReplica(match,networkSlot,OnlineKind);
        onlineTeamOverview=null;onlineIncomeIn=null;
        Notice("Подключение к бою: ожидаем второго игрока");StartCoroutine(NetworkLoaded());
    }
    IEnumerator NetworkLoaded(){int generation=versusSerial;yield return new WaitForEndOfFrame();while(versusActive&&generation==versusSerial&&Time.realtimeSinceStartup-versusAt<1.5f)yield return null;if(generation==versusSerial&&onlineMatch&&network!=null)SendNetwork(new JObject { ["type"]="loaded" });}
    void NetworkSnapshot(JObject packet) {
        if(!onlineMatch||onlineReplica==null||match==null)return;
        try {
            onlineReplica.Apply(packet);ApplyNetworkCards(packet);ApplyNetworkSight(packet);onlineSnapshotAt=Time.unscaledTime;RecoverySnapshotReceived();onlineBattleStats=packet["battle_stats"] as JArray??new JArray();AspectOnlineApply(packet["aspects"] as JObject);DrawOnlineProjectiles(packet["projectiles"] as JArray,(float?)packet["elapsed"]??match.Elapsed,packet["effects"] as JArray);
            onlineTeamOverview=packet["team_overview"] as JArray;onlineIncomeIn=(float?)packet["income_in"];
            if(onlineReplica.UnknownUnits+onlineReplica.UnknownBuildings>0)Debug.LogWarning("CF_NETWORK_UNKNOWN units="+onlineReplica.UnknownUnits+" buildings="+onlineReplica.UnknownBuildings);
            if(match.Finished&&!series.RoundFinished){if(packet["wins"] is JArray score&&score.Count==2)series.ApplyNetworkResult((int?)packet["round"]??1,(int?)score[networkSlot]??0,(int?)score[1-networkSlot]??0,(bool?)packet["series_finished"]??false,match.Winner<0);else series.FinishRound(match.Winner);page=Page.Result;chosen=-1;controls.buildGrid.SetActive(false);onlineDecision=false;}
        } catch(Exception e){Debug.LogError("CF_NETWORK_SNAPSHOT_ERROR "+e);Notice("Ошибка данных боя; соединение закрыто");NetworkReturnToMenu();}
    }
    void DrawOnlineProjectiles(JArray rows,float stamp=float.NaN,JArray effects=null) {
        if(float.IsNaN(stamp))stamp=Time.unscaledTime;ProjectileStamp(stamp);
        var live=new HashSet<int>();if(rows!=null)foreach(var row in rows.OfType<JObject>()) {
            int id=(int?)row["id"]??0;var p=row["p"] as JArray;if(id<=0||p==null||p.Count<3)continue;live.Add(id);
            HdModels.ShotPaths[id]=(string)row["path"];
            if(!onlineProjectiles.TryGetValue(id,out var obj)||!obj){var prefab=ClassicOnlineMissile(row);if(prefab){obj=Instantiate(prefab,visuals.transform);}else{obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);Destroy(obj.GetComponent<Collider>());obj.GetComponent<Renderer>().sharedMaterial=shellMaterial;obj.transform.localScale=Vector3.one*.35f;obj.transform.SetParent(visuals.transform);}obj.name="Сетевой снаряд "+id;onlineProjectiles[id]=obj;}
            float yaw=(float?)row["yaw"]??0,pitch=(float?)row["pitch"]??0;var forward=new Vector3((networkSlot==1?-1:1)*Mathf.Sin(yaw)*Mathf.Cos(pitch),-Mathf.Sin(pitch),-Mathf.Cos(yaw)*Mathf.Cos(pitch));var rotation=Quaternion.LookRotation(forward);
            if(!projectileTracks.TryGetValue(id,out var track)){
                track=new ProjectileTrack();projectileTracks[id]=track;
                var originPoint=row["origin"] as JArray??p;var soundPoint=NetworkProjectilePoint(originPoint);
                BattleAudio.For(match)?.Missile((string)row["path"],3,false,new Vector2(soundPoint.x,-soundPoint.z));
                if(row["origin"] is JArray origin&&origin.Count==3){track.born=Mathf.Min(stamp,(float?)row["born"]??stamp);track.motion.Push(NetworkProjectilePoint(origin),rotation,track.born);}
            }
            track.retire=float.PositiveInfinity;track.motion.Push(NetworkProjectilePoint(p),rotation,stamp);
            var pose=track.motion.Sample(projectileRender);obj.transform.SetPositionAndRotation(pose.position,pose.rotation);obj.SetActive(projectileRender>=track.born);
            if(row["s"] is JArray scale&&scale.Count==3)obj.transform.localScale=new Vector3((float)scale[0],(float)scale[1],(float)scale[2]);
        }
        foreach(var pair in projectileTracks)if(!live.Contains(pair.Key)&&float.IsPositiveInfinity(pair.Value.retire)){
            var track=pair.Value;track.retire=stamp;
            if(effects!=null)foreach(var effect in effects.OfType<JObject>())if((string)effect["kind"]=="missile_end"&&(int?)effect["id"]==pair.Key&&effect["p"] is JArray end&&end.Count==3){
                track.retire=Mathf.Max(track.motion.LastTime,Mathf.Min(stamp,(float?)effect["time"]??stamp));
                var impactPoint=NetworkProjectilePoint(end);HdModels.ShotPaths.TryGetValue(pair.Key,out var missile);BattleAudio.For(match)?.Missile(missile,3,true,new Vector2(impactPoint.x,-impactPoint.z));
                track.motion.Push(NetworkProjectilePoint(end),track.motion.Sample(track.motion.LastTime).rotation,track.retire);break;
            }
        }
    }
    void NetworkReturnToMenu(){onlineMatch=false;onlineStarted=false;onlineReplica=null;showBattleChat=false;StopDirectory();MainMenu();}
    bool NetworkCommand(JObject packet){
        if(onlineMatch&&onlineStarted&&!NetworkBlocked&&network!=null&&network.Connected){SendNetwork(packet);return true;}
        Notice("Ожидаем соединение с боем");return false;
    }
    void ToggleBattleChat(){
        if(!onlineMatch||page!=Page.Match)return;
        showBattleChat=!showBattleChat;
        focusBattleChat=showBattleChat&&!Application.isMobilePlatform;
        showStickerPicker=showVoicePanel=false;
    }

    void ToggleBattleStickers(){
        if(!onlineMatch||page!=Page.Match)return;
        if(!showStickerPicker){LoadStickerCatalog();stickerDrawerProgress=0;socialStickerPeer="";}
        showStickerPicker=!showStickerPicker;
        showBattleChat=showVoicePanel=false;
    }

    void AddBattleMessage(JObject message){
        string body=(string)message["text"]??"";
        if(body.Length==0)return;
        bool own=(int?)message["slot"]==networkSlot;
        string name=own?"Вы":(string)message["name"]??"Игрок";
        if(battleMessages.Count==4)battleMessages.RemoveAt(0);
        battleMessages.Add(new BattleMessage{text=name+": "+body,own=own,time=Time.unscaledTime});
    }

    void SendBattleChat(){
        string body=battleChat.Trim();
        if(body.Length==0)return;
        if(NetworkCommand(new JObject{["type"]="chat",["text"]=body}))battleChat="";
    }

    Rect BattleStickerArea(){
        int rows=stickerPacks==null?2:Mathf.CeilToInt(stickerPacks[stickerPack].items.Count/5f);
        return new Rect(124,Application.isMobilePlatform?101:86,366,79+81*rows);
    }

    bool BattleSocialContains(Vector2 point)=>onlineMatch&&page==Page.Match&&
        (showBattleChat&&BattleChatEntry.Contains(point)||showStickerPicker&&BattleStickerArea().Contains(point));

    void DrawNetworkHudOverlay() {
        if(!onlineMatch)return;
        float socialTop=Application.isMobilePlatform?48:33;
        var chatRect=new Rect(68,socialTop,48,48);
        var stickersRect=new Rect(124,socialTop,48,48);
        var voiceRect=new Rect(180,socialTop,48,48);
        if(SocialIconButton("battle.chat.open",chatRect,0,showBattleChat,"Чат сражения · Enter / F12"))ToggleBattleChat();
        if(SocialIconButton("battle.sticker.open",stickersRect,1,showStickerPicker,"Стикеры · F8"))ToggleBattleStickers();
        if(SocialIconButton("battle.voice.open",voiceRect,2,showVoicePanel,"Голосовой чат · F7")){
            showVoicePanel=!showVoicePanel;showStickerPicker=showBattleChat=false;
        }
        DrawMobileTalk();
        for(int side=0;side<2;side++)DrawOnlineSticker(side);
        DrawBattleMessages();
        if(showBattleChat)DrawBattleChatPanel();
        if(showStickerPicker)DrawBattleStickerDrawer();
    }

    void DrawBattleMessages(){
        if(Event.current.type!=EventType.Repaint)return;
        var style=new GUIStyle(label){fontSize=16,wordWrap=true,richText=false,alignment=TextAnchor.UpperLeft,padding=new RectOffset()};
        float y=BattleChatEntry.y-7;
        for(int i=battleMessages.Count-1;i>=0;i--){
            var line=battleMessages[i];
            float alpha=Mathf.Clamp01((12-(Time.unscaledTime-line.time))/1.1f);
            if(alpha<=0)break;
            var content=new GUIContent(line.text);
            float width=BattleChatEntry.width;
            float height=Mathf.Max(32,style.CalcHeight(content,width-24)+17);
            y-=height;
            if(y<112)break;
            var rect=new Rect(BattleChatEntry.x,y,width,height);
            Fill(rect,new Color(.022f,.031f,.035f,.89f*alpha));
            Fill(new Rect(rect.x,rect.y,2,height),line.own?new Color(.84f,.66f,.35f,alpha):new Color(.51f,.7f,.85f,alpha));
            style.normal.textColor=new Color(.95f,.93f,.86f,alpha);
            GUI.Label(new Rect(rect.x+12,rect.y+5,width-24,height-9),content,style);
            y-=5;
        }
    }

    void DrawBattleChatPanel(){
        var rect=BattleChatEntry;
        LobbyPanel(rect);
        var field=new Rect(rect.x+7,rect.y+7,306,41);
        GUI.SetNextControlName(BattleChatField);
        battleChat=GUI.TextField(field,battleChat,180,menuInput);
        if(focusBattleChat&&Event.current.type==EventType.Repaint){GUI.FocusControl(BattleChatField);focusBattleChat=false;}
        if(battleChat.Length==0&&GUI.GetNameOfFocusedControl()!=BattleChatField)
            Text(field.x+12,field.y+9,field.width-24,25,"Сообщение в бой",small);
        if(BattleCompactButton("battle.chat.send",new Rect(rect.x+319,rect.y+7,111,41),"Отправить",true))SendBattleChat();
        if(BattleCompactButton("battle.chat.close",new Rect(rect.x+435,rect.y+7,36,41),"×")){
            GUI.FocusControl(null);showBattleChat=false;
        }
    }

    bool BattleCompactButton(string id,Rect rect,string caption,bool primary=false){
        bool clicked=LobbyButton(id,rect,"",primary);
        GUI.Label(rect,caption,new GUIStyle(label){
            alignment=TextAnchor.MiddleCenter,wordWrap=false,padding=new RectOffset(),fontSize=16,
            normal={textColor=primary?new Color(.96f,.84f,.57f):new Color(.90f,.89f,.83f)}
        });
        return clicked;
    }

    void ReceiveStickerRejection(JObject packet){
        float retry=(float?)packet["retry_after"]??0;
        nextBattleSticker=Time.unscaledTime+Mathf.Max(0,retry);
        if(retry<=0)Notice((string)packet["message"]??"Стикер недоступен");
    }

    void DrawBattleStickerDrawer(){
        LoadStickerCatalog();
        if(Event.current.type==EventType.Repaint)
            stickerDrawerProgress=Mathf.MoveTowards(stickerDrawerProgress,1,Time.unscaledDeltaTime*8);
        Rect rect=BattleStickerArea();
        rect.height*=Mathf.SmoothStep(0,1,stickerDrawerProgress);
        if(rect.height<1)return;
        LobbyPanel(rect);
        GUI.BeginGroup(rect);
        if(BattleCompactButton("sticker.prev",new Rect(8,7,40,36),"<"))
            stickerPack=(stickerPack+stickerPacks.Count-1)%stickerPacks.Count;
        var pack=stickerPacks[stickerPack];
        if(LobbyButton("sticker.pack",new Rect(53,7,217,36),pack.name))
            stickerPack=(stickerPack+1)%stickerPacks.Count;
        if(BattleCompactButton("sticker.next",new Rect(275,7,40,36),">"))
            stickerPack=(stickerPack+1)%stickerPacks.Count;
        if(BattleCompactButton("sticker.close",new Rect(320,7,38,36),"×"))showStickerPicker=false;
        pack=stickerPacks[stickerPack];
        float remaining=nextBattleSticker-Time.unscaledTime;
        var captionStyle=new GUIStyle(hudSmall){fontSize=11,wordWrap=true,alignment=TextAnchor.UpperCenter};
        for(int i=0;i<pack.items.Count;i++){
            var item=pack.items[i];
            var cell=new Rect(9+i%5*71,49+i/5*81,64,75);
            bool owned=CanSendSticker(item.id);
            bool enabled=GUI.enabled;
            GUI.enabled=enabled&&remaining<=0;
            bool clicked=LobbyButton("sticker."+i,cell,"");
            GUI.enabled=enabled;
            StickerImage(new Rect(cell.x+5,cell.y+3,54,48),item);
            GUI.Label(new Rect(cell.x+2,cell.y+51,60,24),item.label,captionStyle);
            if(!owned)Fill(new Rect(cell.x,cell.y,cell.width,cell.height),new Color(0,0,0,.38f));
            if(!clicked)continue;
            if(!owned){Notice("Рофлопак открывается в магазине за 300 жетонов");PreviewSticker(item);}
            else if(NetworkCommand(new JObject{["type"]="emote",["id"]=item.id}))nextBattleSticker=Time.unscaledTime+1.5f;
        }
        string hint=remaining>0?"Следующий стикер через "+Mathf.CeilToInt(remaining)+" с":
            pack.paid&&!CanSendSticker(pack.items[0].id)?"Рофлопак · 300 жетонов в магазине":"Нажмите стикер, чтобы отправить";
        Text(10,51+81*Mathf.CeilToInt(pack.items.Count/5f),345,23,hint,small);
        GUI.EndGroup();
    }

    void DrawOnlineSticker(int side) {
        var bubble=onlineStickers[side];if(bubble==null)return;float age=Time.unscaledTime-bubble.started;if(age>=3.6f){onlineStickers[side]=null;return;}
        float x=side==0?834:1054;float alpha=Mathf.Clamp01((3.6f-age)/.35f),t=Mathf.Clamp01(age/.45f),back=1+2.70158f*Mathf.Pow(t-1,3)+1.70158f*Mathf.Pow(t-1,2),scale=Mathf.LerpUnclamped(.55f,1,back);
        float wobbleT=Mathf.Clamp01(age/.65f),angle=Mathf.Sin(wobbleT*Mathf.PI*6)*.12f*(1-wobbleT)*Mathf.Rad2Deg;var oldColor=GUI.color;GUI.color=new Color(1,1,1,alpha);
        var nameStyle=new GUIStyle(small){alignment=TextAnchor.MiddleCenter,clipping=TextClipping.Clip,wordWrap=false,normal={textColor=side==0?new Color(.96f,.78f,.38f):new Color(.95f,.71f,.63f)}};
        var shadow=new GUIStyle(nameStyle){normal={textColor=Color.black}};GUI.Label(new Rect(x+1,141,210,24),bubble.name,shadow);GUI.Label(new Rect(x,140,210,24),bubble.name,nameStyle);
        var oldMatrix=GUI.matrix;Vector2 centre=new Vector2(x+105,269);GUIUtility.RotateAroundPivot(angle,centre);float w=210*scale;StickerImage(new Rect(centre.x-w*.5f,centre.y-w*.5f,w,w),bubble.item);GUI.matrix=oldMatrix;
        if(!bubble.item.atlas){var caption=new GUIStyle(label){alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};GUI.Label(new Rect(x,379,210,25),bubble.item.label,caption);}GUI.color=oldColor;
    }
    void ReceiveEmote(JObject packet){
        if(!(packet["message"] is JObject message))return;
        int sender=(int?)message["slot"]??-1,serial=(int?)message["serial"]??-1;
        if(sender<0||sender>1||serial>=0&&serial<=onlineStickerSerials[sender])return;
        LoadStickerCatalog();
        string id=(string)message["id"]??"";
        var item=stickerPacks.SelectMany(p=>p.items).FirstOrDefault(x=>x.id==id);
        if(item==null)return;
        if(serial>=0)onlineStickerSerials[sender]=serial;
        if(sender==networkSlot)
            nextBattleSticker=Time.unscaledTime+Mathf.Max(0,(float?)packet["cooldown"]??1.5f);
        onlineEmoteId=id;
        string name=(string)message["name"]??"Игрок";
        if(name.Length>24)name=name.Substring(0,24);
        onlineStickers[sender==networkSlot?0:1]=new OnlineSticker{item=item,name=name,started=Time.unscaledTime};
        PreviewSticker(item);
    }
    Texture2D chatToolbarIcon,micToolbarIcon;
    Texture2D ToolbarGlyph(bool mic){
        var t=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];
        for(int y=0;y<64;y++)for(int x=0;x<64;x++){
            bool ink=false;
            if(mic){float cap=Mathf.Pow((x-32)/10f,2)+Mathf.Pow(Mathf.Max(0,Mathf.Abs(y-39)-7)/10f,2);float radius=Vector2.Distance(new Vector2(x,y),new Vector2(32,28));ink=cap<=1&&!(y%7<2&&x>26&&x<38)||y<=28&&radius>=15&&radius<=18||y>=28&&y<=38&&(x>=14&&x<=17||x>=47&&x<=50)||x>=30&&x<=34&&y>=10&&y<=14||x>=21&&x<=43&&y>=6&&y<=10;}
            else{float outer=RoundedGlyph(x,y,32,35,27,19,6),inner=RoundedGlyph(x,y,32,35,22,14,4);ink=outer<=0&&inner>=0||y>=9&&y<=20&&x>=13&&x<=25-(20-y)||y>=31&&y<=37&&(x>=17&&x<=22||x>=29&&x<=34||x>=41&&x<=46);}
            pixels[y*64+x]=ink?Color.Lerp(new Color(.7f,.48f,.18f),new Color(1,.91f,.65f),y/63f):Color.clear;
        }t.SetPixels(pixels);t.Apply(false,true);t.filterMode=FilterMode.Bilinear;return t;
    }
    static float RoundedGlyph(float x,float y,float cx,float cy,float hx,float hy,float r){var q=new Vector2(Mathf.Abs(x-cx)-hx+r,Mathf.Abs(y-cy)-hy+r);return new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-r;}
    bool SocialIconButton(string id,Rect rect,int kind,bool selected,string tip){
        bool clicked=Button(id,rect,"",false,menuButton);bool hover=rect.Contains(Event.current.mousePosition);
        var frame=hover?buttonHighlight:buttonUp;if(frame)GUI.DrawTexture(rect,frame,ScaleMode.StretchToFill);
        if(kind==0&&!chatToolbarIcon)chatToolbarIcon=ToolbarGlyph(false);if(kind==2&&!micToolbarIcon)micToolbarIcon=ToolbarGlyph(true);
        var icon=kind==0?chatToolbarIcon:kind==2?micToolbarIcon:ContentResources.Load<Texture2D>("PcStickers/orc");
        if(icon)GUI.DrawTexture(new Rect(rect.x+7,rect.y+7,rect.width-14,rect.height-14),icon,ScaleMode.ScaleToFit);
        if(selected)Border(rect,new Color(.94f,.77f,.36f),2);
        if(kind==2&&voice!=null&&voice.Connected)Fill(new Rect(rect.xMax-11,rect.yMax-11,6,6),voice.MicrophoneEnabled?Color.green:new Color(.9f,.7f,.3f));
        if(hover&&!Application.isMobilePlatform&&!showBattleChat&&!showStickerPicker&&!showVoicePanel){Fill(new Rect(68,105,340,42),new Color(.015f,.02f,.025f,.96f));Border(new Rect(68,105,340,42),new Color(.6f,.48f,.3f));Text(80,110,316,34,tip,new GUIStyle(label){fontSize=16,wordWrap=false});}
        return clicked;
    }

}
}
