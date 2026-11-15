using System;using Newtonsoft.Json.Linq;using UnityEngine;
namespace CastleFight {public sealed partial class PcGame {
    string recoveryToken="",recoveryMessage="";bool reconnecting,peerWaiting,awaitingSnapshot,recoveryActive;
    float retryDeadline,peerDeadline,retryAt,syncDeadline,applicationPausedAt=-1;
    bool NetworkBlocked=>onlineMatch&&(reconnecting||peerWaiting||awaitingSnapshot);
    void ResetRecovery(bool forget=true){reconnecting=peerWaiting=awaitingSnapshot=recoveryActive=false;retryDeadline=peerDeadline=retryAt=syncDeadline=0;applicationPausedAt=-1;if(forget)recoveryToken="";}
    bool BeginRecovery(string reason){
        if(!onlineMatch||string.IsNullOrEmpty(recoveryToken))return false;
        float now=Time.realtimeSinceStartup;
        if(!reconnecting){retryDeadline=now+90;if(peerWaiting&&peerDeadline>0)retryDeadline=Mathf.Min(retryDeadline,peerDeadline);Debug.Log("CF_NETWORK_RECOVERY_BEGIN");}
        reconnecting=recoveryActive=true;awaitingSnapshot=false;recoveryMessage=reason;retryAt=now+1;
        network?.Dispose();network=null;directoryLoading=false;return true;
    }
    void TickRecovery(){
        if(!onlineMatch)return;float now=Time.realtimeSinceStartup;
        if(awaitingSnapshot&&now>=syncDeadline)BeginRecovery("Не получен снимок матча после восстановления");
        if(!NetworkBlocked&&onlineStarted&&network!=null&&network.Connected&&network.SecondsSinceReceive>12)BeginRecovery("Сервер перестал отвечать");
        if(!reconnecting)return;
        if(now>=retryDeadline){FailRecovery("Не удалось восстановить связь за 90 секунд. Профиль сохранён; создайте новый матч.");return;}
        if(network!=null||now<retryAt)return;
        JObject opening=PcNetworkClient.Handshake("resume","",networkName,networkRaceNames[networkRace]);opening["token"]=recoveryToken;
        network=new PcNetworkClient(opening);directoryLoading=false;retryAt=now+2;_=network.ConnectAsync(new Uri(connectedEndpoint));
    }
    void RecoveryDisconnected(){if(!reconnecting&&!BeginRecovery("Соединение с сервером прервалось"))return;network?.Dispose();network=null;retryAt=Time.realtimeSinceStartup+2;}
    void RecoveryPacket(JObject packet){
        if(!onlineMatch)return;float now=Time.realtimeSinceStartup;
        switch((string)packet["type"]){
            case "resumed":
                if((int?)packet["slot"]!=networkSlot){FailRecovery("Сервер вернул неверное место в матче");return;}
                reconnecting=false;awaitingSnapshot=recoveryActive=true;syncDeadline=now+12;peerWaiting=(bool?)packet["waiting"]??false;onlineStarted=(bool?)packet["playing"]==true&&(bool?)packet["loading"]!=true;directoryReady=true;
                if((bool?)packet["loading"]==true)StartCoroutine(NetworkLoaded());Debug.Log("CF_NETWORK_RESUMED awaitingSnapshot=true");break;
            case "connection_hold":peerWaiting=recoveryActive=true;peerDeadline=now+Mathf.Clamp(((int?)packet["remaining_ms"]??90000)/1000f,0,90);break;
            case "connection_restored":peerWaiting=false;peerDeadline=0;TryRecoveryComplete();break;
            case "resume_rejected":FailRecovery((string)packet["message"]??"Время восстановления истекло или матч завершён");break;
        }
    }
    void RecoverySnapshotReceived(){if(!awaitingSnapshot)return;awaitingSnapshot=false;TryRecoveryComplete();}
    void TryRecoveryComplete(){if(!recoveryActive||NetworkBlocked)return;recoveryActive=false;retryDeadline=syncDeadline=0;directoryStatus="Соединение восстановлено · матч продолжается";Notice(directoryStatus);Debug.Log("CF_NETWORK_RECOVERY_COMPLETE");}
    void FailRecovery(string reason){NetworkReturnToMenu();directoryStatus=reason;Notice(reason);Debug.Log("CF_NETWORK_RECOVERY_ENDED "+reason);}
    void OnApplicationPause(bool paused){if(!onlineMatch)return;if(paused)applicationPausedAt=Time.realtimeSinceStartup;else if(applicationPausedAt>=0){float absent=Time.realtimeSinceStartup-applicationPausedAt;applicationPausedAt=-1;if(absent>2&&!reconnecting)BeginRecovery("Возвращаемся в матч после сворачивания");}}
    void DrawNetworkRecovery(){
        if(!NetworkBlocked)return;Fill(new Rect(0,0,1280,720),new Color(.008f,.018f,.025f,.88f));LobbyPanel(new Rect(300,160,680,400));
        Text(336,190,608,56,reconnecting?"ВОССТАНАВЛИВАЕМ СВЯЗЬ":awaitingSnapshot?"СИНХРОНИЗАЦИЯ МАТЧА":"ОЖИДАЕМ СОПЕРНИКА",new GUIStyle(heading){fontSize=25,alignment=TextAnchor.MiddleCenter});
        Text(340,267,600,76,reconnecting?"Подключаемся к серверу снова.\nВаше место и состояние боя сохранены.":awaitingSnapshot?"Получаем актуальное состояние поля боя…":"У соперника прервалось соединение.\nБой продолжится после его возвращения.",new GUIStyle(label){alignment=TextAnchor.MiddleCenter});
        int remaining=Mathf.Max(0,Mathf.CeilToInt((reconnecting?retryDeadline:peerDeadline)-Time.realtimeSinceStartup));
        Text(340,356,600,35,awaitingSnapshot?"Приказы временно заблокированы":remaining+" с · бой приостановлен",new GUIStyle(label){alignment=TextAnchor.MiddleCenter});
        GUI.enabled=reconnecting&&network==null;if(LobbyButton("recovery.retry",new Rect(338,422,286,54),"Повторить сейчас",true))retryAt=0;GUI.enabled=true;
        if(LobbyButton("recovery.leave",new Rect(640,422,302,54),"Покинуть матч"))FailRecovery("Вы покинули матч во время восстановления связи");
        Text(338,498,606,30,"90 секунд на возвращение · состояние хранится на сервере",small);
    }
}}
