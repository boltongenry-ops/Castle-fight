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
    JObject spectatorPending,spectatorChallenge,spectatorBoss;
    byte[] spectatorFog,spectatorExplored;
    bool spectatorPaused,spectatorMusicStarted,spectatorIdsWarned;
    static readonly Rect SpectatorStrip=new Rect(10,39,205,120);
    static readonly Rect SpectatorTargetStrip=new Rect(293,466,510,39);

    bool CanStartSpectating=>spectatorSupported&&HasNetworkProfile&&networkSlot<0&&match==null&&
        !versusActive&&!SpectatorOpen&&spectatorRequestDeadline==0;
    bool SpectatorOpen=>spectatorId.Length>0;
    bool SpectatorSceneReady=>SpectatorOpen&&!spectatorPreparing&&spectatorReplica!=null;
    bool ChallengePresentationPlaying=>winterIntro||winterFinale||choirIntro||hordeIntro||yurneroScene||protocolIntro||
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
            frame=BattleSnapshot.Capture(match,0,FogVisible,hut=>false,spectator:true);
            frame["type"]="spectate_frame";frame["match"]=spectatorBroadcast;
            frame["state"]=page==Page.Match||page==Page.Teams?"live":"paused";
            frame["current"]=Convert.ToBase64String(fog.Current);
            frame["explored"]=Convert.ToBase64String(fog.Explored);
            frame["enemy_builder"]=FogVisible(match.Builders[1].position);
            frame["phase_two"]=match.WinterPhaseTwo||match.HordeSecondStage||match.AbyssSecondPhase||match.QuackDownpour||match.ProtocolMarch;
            frame["downpour"]=match.QuackDownpour;
            frame["boss"]=CaptureSpectatorBoss();
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
        bool expert=HumanMatch.IsExpertTrial(challenge);
        string family=HumanMatch.EpochalFamily(challenge);
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
        if(expert&&family==HumanMatch.WinterTrial)second=HumanMatch.WinterBossCatalog(second);
        if(HasExpertFinale(challenge))second=HumanMatch.ExpertFinaleCatalog(second,family);
        match=new HumanMatch(first,(int?)row["start_gold"]??250,botCatalog:second,
            legacyEpochal:HumanMatch.IsEpochalTrial(challenge)&&!expert){NetworkReplica=true};
        visuals=new GameObject("Просмотр матча");
        visuals.AddComponent<BattleAudio>().Bind(match,World,FogVisible);
        var unitKinds=new System.Collections.Generic.Dictionary<(int,string),int>();
        spectatorReplica=new PcServerReplica(match,0,(team,raw)=>{
            if(!unitKinds.TryGetValue((team,raw),out int kind))unitKinds[(team,raw)]=kind=OnlineKind(team,raw);
            return kind;
        });
        spectatorChallenge=row;
        spectatorTitle=(string)row["title"]??"Испытание";
        spectatorFrameAt=Time.realtimeSinceStartup;
        spectatorState="Ожидание кадра";
        builderSelected=buildMenu=moveOrder=selectedCastle=false;
        selectedUnit=selectedHut=selectedItem=chosen=-1;
        targetedSpell=null;
        ClearGroupSelection();
        controls.Focus("west");
        controls.GameplayViewport(false);
        if((family!=HumanMatch.QuackTrial||expert)&&(a==PepeContent.Race||b==PepeContent.Race)){
            yield return LoadGreatQuackArt(versusSerial);
            if(spectatorId!=id){LeaveSpectator(spectatorState);yield break;}
        }
        if(family==HumanMatch.WinterTrial||family==HumanMatch.ChoirTrial){
            bool blight=family==HumanMatch.ChoirTrial;
            if(!blight&&expert){
                yield return LoadWinterQueenArt(versusSerial);
                if(spectatorId!=id){LeaveSpectator(spectatorState);yield break;}
            }
            GatherRitualGround(World(HumanMatch.CastlePosition(1)));
            yield return PrepareRitualGround(versusSerial,blight);
            if(spectatorId!=id){LeaveSpectator(spectatorState);yield break;}
            SpreadRitualGround(1);
            if(blight){
                PrepareChoirNightLighting();ChoirNight(1);
                choirMusic=visuals.AddComponent<AudioSource>();choirMusic.playOnAwake=false;
                yield return PrepareChoirMusic(versusSerial);
            }
        }else if(family==HumanMatch.HordeTrial){
            if(expert)yield return PrepareYurneroArt(versusSerial);
            if(spectatorId!=id){LeaveSpectator(spectatorState);yield break;}
            yield return PrepareHordeGround(versusSerial);
            if(spectatorId!=id){LeaveSpectator(spectatorState);yield break;}
        }else if(family==HumanMatch.AbyssTrial){
            PrepareAbyssWaterPresentation();
            SetAbyssShore(1);
        }else if(family==HumanMatch.QuackTrial){
            PrepareQuackSwamp();
            quackDrizzle=quackDrizzle?quackDrizzle:MakeQuackRain(false);
            quackDownpour=quackDownpour?quackDownpour:MakeQuackRain(true);
            if(!quackHudFrame)quackHudFrame=LoadProtocolTexture("QuackHudFrame","png",TextureFormat.RGBA32,false);
        }else if(family==HumanMatch.ProtocolTrial){
            protocolHudFrame=LoadProtocolTexture("ProtocolMeter","png",TextureFormat.RGBA32,false);
            protocolHudLight=MakeProtocolHudLight();
        }
        if(SpectatorUsesBossSoundtrack(family)){
            yield return PrepareBossSoundtrack(versusSerial,family==HumanMatch.ProtocolTrial);
        }
        if(spectatorId!=id){LeaveSpectator(spectatorState);yield break;}
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
        if(spectatorFog==null)return;
        controls.GameplayViewport(true);
        if(!testing&&!AccountOverlayOpen){
            if(Input.GetKeyDown(KeyCode.F1))FocusBuilder();
            if(Application.isMobilePlatform)HandleTouchWorld();
            else HandleSelectionGesture();
        }
        RenderMatch();
        UpdateSpectatorMusic();
    }

    bool spectatorEnemyBuilder,spectatorRain,spectatorStorm;
    string spectatorTitle="";
    void ApplySpectatorFrame(JObject frame){
        string state=(string)frame["state"];
        if(state=="cinematic"){spectatorPaused=true;spectatorState="У игрока идёт заставка";return;}
        var current=Convert.FromBase64String((string)frame["current"]??"");
        var explored=Convert.FromBase64String((string)frame["explored"]??"");
        if(current.Length!=SpectatorFogBytes||explored.Length!=SpectatorFogBytes)
            throw new FormatException("Invalid spectator fog dimensions");
        bool firstFrame=spectatorFog==null;
        spectatorReplica.Apply(frame);
        spectatorFog=current;spectatorExplored=explored;
        spectatorBoss=frame["boss"] as JObject;
        spectatorPaused=state=="paused";
        ApplyNetworkCards(frame,0);
        onlineTeamOverview=frame["team_overview"] as JArray;onlineIncomeIn=(float?)frame["income_in"];
        spectatorEnemyBuilder=(bool?)frame["enemy_builder"]==true;
        spectatorState=state=="paused"?"Пауза у игрока":(bool?)frame["phase_two"]==true?"Вторая фаза":"";
        if(quackWetland){
            RevealQuackSwamp();
            spectatorRain=true;spectatorStorm=(bool?)frame["downpour"]==true;
        }
        onlineSnapshotAt=Time.unscaledTime;
        UpdateFog(true);
        DrawOnlineProjectiles(frame["projectiles"] as JArray,match.Elapsed);
        if(firstFrame){
            noticeLeft=0;
            controls.FocusWorld(World(match.Builders[0].position),56);
            Debug.Log($"CF_SPECTATOR_READY buildings={match.Huts.Count} units={match.Units.Count}");
        }
        if(!spectatorIdsWarned&&spectatorReplica.UnknownUnits+spectatorReplica.UnknownBuildings>0){
            spectatorIdsWarned=true;
            Debug.LogWarning($"CF_SPECTATOR_UNKNOWN units={spectatorReplica.UnknownUnits} buildings={spectatorReplica.UnknownBuildings}");
        }
    }

    float SpectatorUnitHeight(HumanMatch.Unit actor)=>
        networkUnitCards.TryGetValue(actor.id,out var row)&&row["line_raw"]?.Type==JTokenType.String&&row["p"] is JArray point
            ?(float)point[1]:match.Height(actor);

    string SpectatorAttackTarget(HumanMatch.Unit actor){
        if(!networkUnitCards.TryGetValue(actor.id,out var row)||(int?)row["attack_target"] is not int target)return null;
        if(target==-1)return FogVisible(match.BattleCastlePosition(1-actor.team))?"Замок противника":"Цель в тумане войны";
        if(target>0){
            var unit=match.Units.FirstOrDefault(u=>u.id==target&&u.hp>0&&CanSeeUnit(u)&&!match.Hidden(u));
            if(unit!=null)return match.Lines[unit.kind].unitName;
        }else{
            var hut=match.Huts.FirstOrDefault(h=>h.id==-(long)target-2&&h.hp>0&&CanInspectHut(h));
            if(hut!=null)return match.Lines[hut.kind].name;
        }
        return "Цель в тумане войны";
    }

    void DrawSpectatorTarget(){
        var unit=SelectedUnit;
        if(unit==null||SpectatorAttackTarget(unit) is not string target)return;
        StoneFrame(SpectatorTargetStrip);
        FitBossText(new Rect(306,471,484,28),match.Lines[unit.kind].unitName+" → "+target,
            new GUIStyle(hudText),16);
    }

    static bool SpectatorUsesBossSoundtrack(string id)=>id==HumanMatch.WinterTrial||id==HumanMatch.HordeTrial||
        id==HumanMatch.ProtocolTrial||id==HumanMatch.AbyssTrial||id==HumanMatch.QuackTrial;

    JObject CaptureSpectatorBoss(){
        if(match.EternalWinter)return new JObject{["left"]=match.WinterChainIn,["second"]=match.WinterPhaseTwo,
            ["frozen"]=match.WinterCastleFrozen,["defeated"]=match.WinterQueenFelled,["storm"]=match.WinterStormActive,
            ["queen_id"]=match.WinterBoss?.id??-1,["queen_hp"]=match.WinterBoss?.hp??0};
        if(match.FuneralChoir)return new JObject{["movement"]=(int)match.Movement,["age"]=match.MovementAge,
            ["left"]=match.ChoirTomb?match.ChoirFinalLeft:match.TollIn};
        if(match.CursedHorde)return new JObject{["left"]=match.HordeNextWaveIn,["second"]=match.HordeSecondStage,
            ["shamans"]=match.HordeShamansAlive,["rage"]=match.HordeAnyRage,
            ["fallen"]=match.ExpertCastleFallen,["final_hp"]=match.ExpertFinalBoss?.hp??0};
        if(match.AbyssTrialActive)return new JObject{["left"]=match.AbyssRaidIn,["second"]=match.AbyssSecondPhase,["warning"]=match.AbyssRaidWarning,
            ["fallen"]=match.ExpertCastleFallen,["final_hp"]=match.ExpertFinalBoss?.hp??0};
        if(match.LastProtocol)return new JObject{["left"]=match.ProtocolTimeLeft,["march"]=match.ProtocolMarch,
            ["mask"]=(match.ProtocolStationAlive(0)?1:0)|(match.ProtocolStationAlive(1)?2:0),["second"]=match.ProtocolSecondStage,
            ["open"]=match.ProtocolGatesOpen,["open_for"]=match.ProtocolOpenFor,["shot"]=match.ProtocolShotIn,["warning"]=match.ProtocolWarningActive};
        return null;
    }

    string SpectatorChallengeId=>(string)spectatorChallenge?["id"]??"";
    HumanMatch.ChoirMovement SpectatorChoirMovement=>(HumanMatch.ChoirMovement)((int?)spectatorBoss?["movement"]??0);
    string SpectatorChallengeFamily=>HumanMatch.EpochalFamily(SpectatorChallengeId);
    Rect SpectatorBossRect=>SpectatorChallengeFamily switch{
        HumanMatch.WinterTrial or HumanMatch.ChoirTrial=>new Rect(218,22,350,101),
        HumanMatch.HordeTrial=>HordeHud,
        HumanMatch.ProtocolTrial=>ProtocolHud,
        HumanMatch.AbyssTrial=>AbyssHud,
        HumanMatch.QuackTrial=>QuackHud,
        _=>default
    };
    bool SpectatorBossContains(Vector2 p)=>SpectatorBossRect.Contains(p)||
        SelectedUnit!=null&&SpectatorTargetStrip.Contains(p)&&SpectatorAttackTarget(SelectedUnit)!=null;

    void DrawSpectatorBoss(){
        switch(SpectatorChallengeFamily){
            case HumanMatch.WinterTrial:DrawWinterHud();break;
            case HumanMatch.ChoirTrial:DrawChoirHud();break;
            case HumanMatch.HordeTrial:DrawHordeHud();break;
            case HumanMatch.ProtocolTrial:DrawProtocolHud();break;
            case HumanMatch.AbyssTrial:DrawAbyssHud();break;
            case HumanMatch.QuackTrial:DrawQuackHud();break;
        }
    }

    void UpdateSpectatorMusic(){
        if(choirMusic){
            int track=ChoirBattleTrack(SpectatorChoirMovement);
            if(choirTrack!=track||choirLastMovement!=SpectatorChoirMovement){
                ChoirTrack(track,(float?)spectatorBoss?["age"]??match.Elapsed,
                    track==0||SpectatorChoirMovement==HumanMatch.ChoirMovement.FinalSiege);
                choirLastMovement=SpectatorChoirMovement;
            }
            choirMusic.volume=track==0?.52f:track==3?.8f:(float?)spectatorBoss?["age"]<HumanMatch.ChoirLead?.62f:1;
            if(spectatorPaused)choirMusic.Pause();else choirMusic.UnPause();
        }
        if(!bossSoundtrack)return;
        if(!spectatorMusicStarted){
            spectatorMusicStarted=true;
            bossSoundtrack.volume=SpectatorChallengeFamily==HumanMatch.ProtocolTrial?ProtocolBattleMusicVolume:WinterBattleMusicVolume;
            bossSoundtrack.time=Mathf.Repeat(match.Elapsed,bossSoundtrack.clip.length);
            StartBossSoundtrack();
        }
        bool faster=(SpectatorChallengeFamily==HumanMatch.WinterTrial||SpectatorChallengeFamily==HumanMatch.HordeTrial)&&
            (bool?)spectatorBoss?["second"]==true;
        bossSoundtrack.pitch=Mathf.MoveTowards(bossSoundtrack.pitch,faster?WinterPhaseTwoPitch:1,Time.unscaledDeltaTime*.5f);
        if(spectatorPaused)bossSoundtrack.Pause();else bossSoundtrack.UnPause();
    }

    void DrawSpectator(){
        EnsureRaceStyles();
        bool live=SpectatorSceneReady&&spectatorFog!=null;
        if(!live)Fill(new Rect(0,0,1280,720),new Color(.025f,.041f,.041f,.96f));
        if(spectatorRain)DrawQuackRain(spectatorStorm);
        if(live){DrawWarcraftHud();DrawSpectatorBoss();DrawSpectatorTarget();DrawSpellDetails();}
        LobbyPanel(SpectatorStrip);
        Text(21,43,181,24,FitAtlasLabel(spectatorName,181,label),label);
        Text(21,68,181,20,FitAtlasLabel(spectatorState.Length>0?spectatorState:spectatorTitle,181,small),small);
        if(live&&LobbyButton("spectator.focus",new Rect(19,90,187,30),"К строителю"))
            controls.FocusWorld(World(match.Builders[0].position),46);
        if(!spectatorPreparing&&LobbyButton("spectator.exit",new Rect(19,121,187,30),"Выйти"))
            LeaveSpectator();
    }

    void LeaveSpectator(string message=""){
        bool active=page==Page.Spectator;
        spectatorId="";spectatorPreparing=false;spectatorRequestDeadline=0;
        spectatorReplica=null;spectatorPending=null;spectatorFog=spectatorExplored=null;
        spectatorChallenge=spectatorBoss=null;spectatorPaused=spectatorMusicStarted=spectatorIdsWarned=false;
        onlineTeamOverview=null;onlineIncomeIn=null;networkUnitCards.Clear();networkBuildingCards.Clear();
        tapFinger=-1;
        spectatorEnemyBuilder=spectatorRain=spectatorStorm=false;
        SendNetwork(new JObject{["type"]="spectate_leave"});
        if(MobileContent.Active&&MobileContent.Current.Busy)MobileContent.Current.Ready();
        if(active)MainMenu();
        if(message.Length>0)Notice(message);
    }
}
}
