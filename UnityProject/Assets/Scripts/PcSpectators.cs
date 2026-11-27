using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    const float SpectatorFrameInterval=.2f;
    const int SpectatorFogBytes=FogGrid.Width*FogGrid.Height;
    bool spectatorSupported,spectatorSending,spectatorPreparing;
    int spectatorCount;
    float spectatorNext,spectatorRequestDeadline,spectatorFrameAt;
    string spectatorBroadcast="",spectatorOwner="",spectatorId="",spectatorName="",spectatorState="";
    HumanMatch spectatorSource;
    PcNetworkClient spectatorTransport;
    PcServerReplica spectatorReplica;
    JObject spectatorPending;
    byte[] spectatorFog,spectatorExplored;
    static readonly Rect SpectatorStrip=new Rect(18,14,1244,62);

    bool CanStartSpectating=>spectatorSupported&&HasNetworkProfile&&networkSlot<0&&match==null&&
        !versusActive&&!SpectatorOpen&&spectatorRequestDeadline==0;
    bool SpectatorOpen=>spectatorId.Length>0;
    bool SpectatorSceneReady=>SpectatorOpen&&!spectatorPreparing&&spectatorReplica!=null;
    bool ChallengePresentationPlaying=>winterIntro||choirIntro||hordeIntro||protocolIntro||
        protocolMarchIntro||abyssIntro||quackScene!=QuackScene.None;

    void RequestSpectator(string friend){
        if(!CanStartSpectating)return;
        CloseAccountOverlay();
        spectatorRequestDeadline=Time.realtimeSinceStartup+12;
        Notice("Подключаемся к испытанию…");
        SendNetwork(new JObject{["type"]="spectate_join",["peer"]=friend,["version"]=Version});
    }

    void UpdateChallengeBroadcast(){
        bool allowed=spectatorSupported&&network!=null&&network.Connected&&HasNetworkProfile&&
            !options.disableFriendViewing&&activeChallenge!=null&&match!=null&&!match.Finished&&
            !onlineMatch&&!SpectatorOpen&&networkSlot<0&&!versusActive;
        if(!allowed||spectatorTransport!=network||spectatorSource!=match||spectatorOwner!=AccountHub.owner){
            if(spectatorBroadcast.Length>0&&spectatorTransport==network)
                SendNetwork(new JObject{["type"]="spectate_close",
                    ["result"]=spectatorSource!=null&&spectatorSource.Finished?spectatorSource.Winner:-2});
            spectatorBroadcast=spectatorOwner="";
            spectatorSource=null;spectatorTransport=null;spectatorCount=0;
            if(!allowed)return;
            spectatorSource=match;spectatorTransport=network;spectatorOwner=AccountHub.owner;
            spectatorBroadcast=Guid.NewGuid().ToString("N");
            SendNetwork(new JObject{["type"]="spectate_open",["match"]=spectatorBroadcast,["version"]=Version,
                ["challenge"]=(string)activeChallenge["id"],["races"]=new JArray(matchRace,matchBotRace),
                ["ultimate"]=new JArray(ultimateRoundLoadouts.Select(ids=>new JArray(ids)))});
        }
        if(spectatorCount==0||spectatorSending||Time.realtimeSinceStartup<spectatorNext)return;
        spectatorNext=Time.realtimeSinceStartup+SpectatorFrameInterval;
        bool presentation=ChallengePresentationPlaying;
        var frame=new JObject{["type"]="spectate_frame",["match"]=spectatorBroadcast,
            ["state"]="cinematic"};
        if(!presentation){
            UpdateFog();
            frame=BattleSnapshot.Capture(match,0,FogVisible,hut=>false);
            frame["type"]="spectate_frame";frame["match"]=spectatorBroadcast;
            frame["state"]=page==Page.Match||page==Page.Teams?"live":"paused";
            frame["current"]=Convert.ToBase64String(fog.Current);
            frame["explored"]=Convert.ToBase64String(fog.Explored);
            frame["enemy_builder"]=FogVisible(match.Builders[1].position);
            frame["phase_two"]=match.WinterPhaseTwo||match.HordeSecondStage||match.AbyssSecondPhase||match.QuackDownpour||match.ProtocolMarch;
            frame["downpour"]=match.QuackDownpour;
        }
        SendSpectatorFrame(spectatorTransport,spectatorBroadcast,frame);
    }

    async void SendSpectatorFrame(PcNetworkClient transport,string id,JObject frame){
        spectatorSending=true;
        try{
            byte[] payload=await Task.Run(()=>PcNetworkClient.CompressSpectatorFrame(frame));
            if(id==spectatorBroadcast&&transport==network)
                await transport.SendSpectatorFrameAsync(payload);
        }catch(Exception error){
            Debug.LogWarning("CF_SPECTATOR_SEND "+error.GetBaseException().Message);
        }finally{spectatorSending=false;}
    }

    void ReceiveSpectator(JObject packet){
        string type=(string)packet["type"];
        string id=(string)packet["match"]??"";
        if(type=="spectate_count"){
            if(id==spectatorBroadcast){
                int viewers=Mathf.Clamp((int?)packet["viewers"]??0,0,4);
                if(viewers>spectatorCount)Notice("Друг подключился к просмотру");
                spectatorCount=viewers;
            }
            return;
        }
        if(type=="spectate_error"){
            if(spectatorRequestDeadline>0){spectatorRequestDeadline=0;Notice((string)packet["message"]??"Просмотр недоступен.");}
            return;
        }
        if(type=="spectate_start"){
            if(spectatorRequestDeadline==0||match!=null||networkSlot>=0||versusActive){
                SendNetwork(new JObject{["type"]="spectate_leave"});return;
            }
            spectatorRequestDeadline=0;spectatorId=id;spectatorPreparing=true;
            spectatorName=(string)packet["host"]??"";
            spectatorState="Загрузка поля боя";spectatorTitle="";
            page=Page.Spectator;controls.InputBlocked=true;
            StartCoroutine(GuardNetworkLoad(PrepareSpectatorScene(packet,id),message=>LeaveSpectator(message)));
            return;
        }
        if(!SpectatorOpen||id!=spectatorId)return;
        if(type=="spectate_end"){
            if(spectatorPreparing){spectatorId="";spectatorState=(string)packet["message"]??"Просмотр завершён";}
            else LeaveSpectator((string)packet["message"]??"Просмотр завершён");
            return;
        }
        if(type=="spectate_frame"){
            spectatorFrameAt=Time.realtimeSinceStartup;
            spectatorPending=packet;
        }
    }

    IEnumerator PrepareSpectatorScene(JObject packet,string id){
        string challenge=(string)packet["challenge"];
        var row=progression.Challenge(challenge);
        if(row==null||(string)packet["version"]!=Version||!(packet["races"] is JArray races)||races.Count!=2)
            throw new InvalidDataException("Испытание недоступно в этой версии.");
        int a=(int)races[0],b=(int)races[1];
        if(a<0||a>=AllRaceUnity.Length||b<0||b>=AllRaceUnity.Length||!RaceStaged(a)||!RaceStaged(b))
            throw new InvalidDataException("Ресурсы рас недоступны.");
        var draw=packet["ultimate"] as JArray;
        if(MobileContent.Active)yield return PrepareMobileMatch(a,b);
        else yield return PrepareDesktopMatch(a,b,versusSerial);
        if(spectatorId!=id){LeaveSpectator(spectatorState);yield break;}
        if((a==12||b==12)&&!ValidateNetworkLoadouts(a,b,draw,0))
            throw new InvalidDataException("Некорректный набор Высшего строителя.");
        if(MobileContent.Active){
            yield return PrepareUltimateDraw034(a,b,draw,0);
            MobileContent.Current.Ready();
        }
        if(spectatorId!=id){LeaveSpectator(spectatorState);yield break;}
        matchRace=a;matchBotRace=b;PrepareRaceIndices();
        JObject first=JObject.Parse(RaceCatalog(a)),second=JObject.Parse(RaceCatalog(b));
        ApplySpectatorUltimate(ref first,a,draw,0);
        ApplySpectatorUltimate(ref second,b,draw,1);
        match=new HumanMatch(first,(int?)row["start_gold"]??250,botCatalog:second){NetworkReplica=true};
        visuals=new GameObject("Просмотр матча");
        visuals.AddComponent<BattleAudio>().Bind(match,World,FogVisible);
        var unitKinds=new System.Collections.Generic.Dictionary<(int,string),int>();
        spectatorReplica=new PcServerReplica(match,0,(team,raw)=>{
            if(!unitKinds.TryGetValue((team,raw),out int kind))unitKinds[(team,raw)]=kind=OnlineKind(team,raw);
            return kind;
        });
        spectatorTitle=(string)row["title"]??"Испытание";
        spectatorFrameAt=Time.realtimeSinceStartup;
        spectatorState="Ожидание кадра";
        builderSelected=buildMenu=moveOrder=selectedCastle=false;
        selectedUnit=selectedHut=selectedItem=chosen=-1;
        targetedSpell=null;
        ClearGroupSelection();
        controls.Focus("west");
        controls.GameplayViewport(false);
        if(challenge==HumanMatch.WinterTrial||challenge==HumanMatch.ChoirTrial){
            bool blight=challenge==HumanMatch.ChoirTrial;
            GatherRitualGround(World(HumanMatch.CastlePosition(1)));
            yield return PrepareRitualGround(versusSerial,blight);
            if(spectatorId!=id){LeaveSpectator(spectatorState);yield break;}
            SpreadRitualGround(1);
            if(blight){PrepareChoirNightLighting();ChoirNight(1);}
        }else if(challenge==HumanMatch.HordeTrial){
            yield return PrepareHordeGround(versusSerial);
            if(spectatorId!=id){LeaveSpectator(spectatorState);yield break;}
        }else if(challenge==HumanMatch.AbyssTrial){
            PrepareAbyssWaterPresentation();
            SetAbyssShore(1);
        }else if(challenge==HumanMatch.QuackTrial){
            PrepareQuackSwamp();
            quackDrizzle=quackDrizzle?quackDrizzle:MakeQuackRain(false);
            quackDownpour=quackDownpour?quackDownpour:MakeQuackRain(true);
        }
        spectatorPreparing=false;
        spectatorFrameAt=Time.realtimeSinceStartup;
    }

    void ApplySpectatorUltimate(ref JObject catalog,int race,JArray loadouts,int team){
        if(race!=12)return;
        var builder=new UltimateLoadout((JArray)catalog["ultimateSlots"],_=>0);
        catalog=builder.Playable(catalog,HumanMatch.Strings(loadouts[team]));
    }

    void UpdateSpectator(){
        if(spectatorRequestDeadline>0&&Time.realtimeSinceStartup>=spectatorRequestDeadline){
            spectatorRequestDeadline=0;Notice("Подключение к просмотру не ответило.");
            SendNetwork(new JObject{["type"]="spectate_leave"});
        }
        if(page!=Page.Spectator)return;
        controls.InputBlocked=!SpectatorSceneReady||AccountOverlayOpen;
        controls.EdgePan=SpectatorSceneReady&&!AccountOverlayOpen&&!Application.isMobilePlatform&&Application.isFocused;
        controls.buildGrid.SetActive(false);
        if(Input.GetKeyDown(KeyCode.Escape)&&!spectatorPreparing){
            if(AccountOverlayOpen)CloseAccountOverlay();
            else {LeaveSpectator();return;}
        }
        if(!SpectatorSceneReady)return;
        if(network==null||!network.Connected||Time.realtimeSinceStartup-spectatorFrameAt>15){
            LeaveSpectator("Связь с испытанием прервалась.");return;
        }
        var frame=spectatorPending;
        spectatorPending=null;
        if(frame!=null)try{ApplySpectatorFrame(frame);}
        catch(Exception error){
            Debug.LogWarning("CF_SPECTATOR_FRAME "+error.GetBaseException().Message);
            LeaveSpectator("Не удалось прочитать данные просмотра.");return;
        }
        if(spectatorFog!=null)RenderMatch();
    }

    bool spectatorEnemyBuilder,spectatorRain,spectatorStorm;
    string spectatorTitle="";
    void ApplySpectatorFrame(JObject frame){
        string state=(string)frame["state"];
        if(state=="cinematic"){spectatorState="У игрока идёт заставка";return;}
        var current=Convert.FromBase64String((string)frame["current"]??"");
        var explored=Convert.FromBase64String((string)frame["explored"]??"");
        if(current.Length!=SpectatorFogBytes||explored.Length!=SpectatorFogBytes)
            throw new FormatException("Invalid spectator fog dimensions");
        spectatorReplica.Apply(frame);
        spectatorFog=current;spectatorExplored=explored;
        spectatorEnemyBuilder=(bool?)frame["enemy_builder"]==true;
        spectatorState=state=="paused"?"Пауза у игрока":(bool?)frame["phase_two"]==true?"Вторая фаза":"";
        if(quackWetland){
            RevealQuackSwamp();
            spectatorRain=true;spectatorStorm=(bool?)frame["downpour"]==true;
        }
        onlineSnapshotAt=Time.unscaledTime;
        UpdateFog(true);
        DrawOnlineProjectiles(frame["projectiles"] as JArray,match.Elapsed);
    }

    void DrawSpectator(){
        EnsureRaceStyles();
        if(spectatorFog==null)Fill(new Rect(0,0,1280,720),new Color(.025f,.041f,.041f,.96f));
        if(spectatorRain)DrawQuackRain(spectatorStorm);
        LobbyPanel(SpectatorStrip);
        Text(37,22,405,29,FitAtlasLabel("Просмотр · "+spectatorName,396,label),label);
        float seconds=match?.Elapsed??0;
        string watch=TimeSpan.FromSeconds(Mathf.Clamp(seconds,0,86400)).ToString(@"mm\:ss");
        Text(38,49,430,23,FitAtlasLabel(watch+"   "+spectatorTitle+"   "+spectatorState,425,small),small);
        if(SpectatorSceneReady&&spectatorFog!=null){
            Text(485,26,320,40,$"Замки   {(int)match.CastleHp[0]} / {(int)match.CastleHp[1]}",label);
            if(LobbyButton("spectator.focus",new Rect(794,24,190,42),"К строителю"))
                controls.FocusWorld(World(match.Builders[0].position),46);
        }
        if(!spectatorPreparing&&LobbyButton("spectator.exit",new Rect(1012,24,231,42),"Выйти из просмотра"))
            LeaveSpectator();
    }

    void LeaveSpectator(string message=""){
        bool active=page==Page.Spectator;
        spectatorId="";spectatorPreparing=false;spectatorRequestDeadline=0;
        spectatorReplica=null;spectatorPending=null;spectatorFog=spectatorExplored=null;
        spectatorEnemyBuilder=spectatorRain=spectatorStorm=false;
        SendNetwork(new JObject{["type"]="spectate_leave"});
        if(MobileContent.Active&&MobileContent.Current.Busy)MobileContent.Current.Ready();
        if(active)MainMenu();
        if(message.Length>0)Notice(message);
    }
}
}
