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
    readonly Dictionary<int,GameObject> onlineProjectiles=new Dictionary<int,GameObject>();
    sealed class OnlineSticker {public StickerItem item;public string name;public float started;}
    readonly OnlineSticker[] onlineStickers=new OnlineSticker[2];int onlineStickerSerial=-1;
    bool showBattleChat;string battleChat="";string onlineEmoteId="";
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
        if(a<0||b<0||(a!=0&&!Pack(a))||(b!=0&&!Pack(b))) {
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
        onlineMatch=true;onlineStarted=false;onlineDecision=false;showBattleChat=showStickerPicker=showVoicePanel=false;onlineStickers[0]=onlineStickers[1]=null;match.NetworkReplica=true;onlineReplica=new PcServerReplica(match,networkSlot,OnlineKind);
        Notice("Подключение к бою: ожидаем второго игрока");StartCoroutine(NetworkLoaded());
    }
    IEnumerator NetworkLoaded(){int generation=versusSerial;yield return new WaitForEndOfFrame();while(versusActive&&generation==versusSerial&&Time.realtimeSinceStartup-versusAt<1.5f)yield return null;if(generation==versusSerial&&onlineMatch&&network!=null)SendNetwork(new JObject { ["type"]="loaded" });}
    void NetworkSnapshot(JObject packet) {
        if(!onlineMatch||onlineReplica==null||match==null)return;
        try {
            onlineReplica.Apply(packet);ApplyNetworkCards(packet);ApplyNetworkSight(packet);onlineSnapshotAt=Time.unscaledTime;RecoverySnapshotReceived();onlineBattleStats=packet["battle_stats"] as JArray??new JArray();AspectOnlineApply(packet["aspects"] as JObject);DrawOnlineProjectiles(packet["projectiles"] as JArray,(float?)packet["elapsed"]??match.Elapsed,packet["effects"] as JArray);
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
    void DrawNetworkHudOverlay() {
        if(!onlineMatch)return;
        float socialTop=Application.isMobilePlatform?48:33;var chatRect=new Rect(68,socialTop,48,48);var stickersRect=new Rect(124,socialTop,48,48);var voiceRect=new Rect(180,socialTop,48,48);
        if(SocialIconButton("battle.chat.open",chatRect,0,showBattleChat,"Чат сражения · F12")){showBattleChat=!showBattleChat;showStickerPicker=showVoicePanel=false;}
        if(SocialIconButton("battle.sticker.open",stickersRect,1,showStickerPicker,"Стикеры · F8")){showStickerPicker=!showStickerPicker;showBattleChat=showVoicePanel=false;}
        if(SocialIconButton("battle.voice.open",voiceRect,2,showVoicePanel,"Голосовой чат · F7")){showVoicePanel=!showVoicePanel;showStickerPicker=showBattleChat=false;}
        DrawMobileTalk();
        for(int side=0;side<2;side++)DrawOnlineSticker(side);
        if(showBattleChat)DrawBattleChatPanel();
        if(showStickerPicker)DrawStickerPicker();
        
    }
    Vector2 battleChatScroll;
    void DrawBattleChatPanel(){
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.5f));LobbyPanel(new Rect(250,125,780,430));
        Text(276,137,520,58,"ЧАТ СРАЖЕНИЯ",new GUIStyle(heading){fontSize=26,wordWrap=false,clipping=TextClipping.Overflow});
        if(LobbyButton("battle.chat.close",new Rect(832,142,172,48),"Закрыть"))showBattleChat=false;
        var messageStyle=ChatBodyStyle;float total=0;
        foreach(var m in roomMessages){float width=Mathf.Clamp(Mathf.Max(messageStyle.CalcSize(new GUIContent((string)m["text"]??"")).x,new GUIStyle(small).CalcSize(new GUIContent((string)m["name"]??"Игрок")).x)+36,220,650);total+=Mathf.Max(84,messageStyle.CalcHeight(new GUIContent((string)m["text"]??""),width-36)+61)+14;}
        battleChatScroll=GUI.BeginScrollView(new Rect(274,204,732,242),battleChatScroll,new Rect(0,0,700,Mathf.Max(225,total)));
        float rowY=0;foreach(var m in roomMessages){string text=(string)m["text"]??"";bool own=(int?)m["slot"]==networkSlot;float width=Mathf.Clamp(Mathf.Max(messageStyle.CalcSize(new GUIContent(text)).x,new GUIStyle(small).CalcSize(new GUIContent((string)m["name"]??"Игрок")).x)+36,220,650),height=Mathf.Max(84,messageStyle.CalcHeight(new GUIContent(text),width-36)+61),x=own?688-width:10;
            ChatBubble(new Rect(x,rowY,width,height),own);Text(x+18,rowY+9,width-36,26,own?"Вы":(string)m["name"]??"Игрок",new GUIStyle(small){fontSize=13,fontStyle=FontStyle.Bold,wordWrap=false});Text(x+18,rowY+39,width-36,height-51,text,messageStyle);rowY+=height+14;}
        if(roomMessages.Count==0)Text(8,20,670,90,"Пожелайте сопернику удачи. Стикеры отправляются отдельной кнопкой рядом с чатом.",messageStyle);GUI.EndScrollView();
        battleChat=GUI.TextField(new Rect(274,468,518,54),battleChat,180,menuInput);
        if(LobbyButton("battle.chat.send",new Rect(806,468,200,54),"Отправить",true)){var body=battleChat.Trim();if(body.Length>0){SendNetwork(new JObject{["type"]="chat",["text"]=body});battleChat="";battleChatScroll.y=100000;}}
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
    void ReceiveEmote(JObject packet){var m=packet["message"] as JObject;if(m==null)return;int sender=(int?)m["slot"]??-1,serial=(int?)m["serial"]??-1;if(sender<0||sender>1||serial<=onlineStickerSerial)return;LoadStickerCatalog();string id=(string)m["id"]??"";var item=stickerPacks.SelectMany(p=>p.items).FirstOrDefault(x=>x.id==id);if(item==null)return;onlineStickerSerial=serial;onlineEmoteId=id;string name=(string)m["name"]??"Игрок";if(name.Length>24)name=name.Substring(0,24);onlineStickers[sender==networkSlot?0:1]=new OnlineSticker {item=item,name=name,started=Time.unscaledTime};PreviewSticker(item);}
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
