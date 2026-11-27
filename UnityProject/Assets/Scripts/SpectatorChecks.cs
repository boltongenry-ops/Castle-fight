#if CF_SPECTATOR_QA
using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseSpectator(){
        string folder=BattleLab.Arg("--pc-results");
        Directory.CreateDirectory(folder);
        directoryEndpoint=BattleLab.Arg("--pc-directory-url");
        networkSessionEndpoint=directoryEndpoint;networkSessionToken="host";
        BeginDirectory();
        yield return WaitSpectatorQa(()=>spectatorSupported&&HasNetworkProfile,"Live gateway handshake");
        Check(!options.disableFriendViewing,"Settings from previous releases enable viewing by default");
        var row=progression.Challenge("five_pillars");
        if(row==null)throw new InvalidOperationException("Missing challenge fixture");
        testHoldAuthority=true;
        BeginChallenge(row,0);
        yield return WaitSpectatorQa(()=>spectatorCount==1,"Python guest subscribes to the running challenge");
        int west=Array.FindIndex(match.Lines,line=>line.Producer&&raceIndices[matchRace].ContainsKey(line.building));
        int east=Array.FindIndex(match.Lines,line=>line.Producer&&raceIndices[matchBotRace].ContainsKey(line.building));
        var visible=match.Spawn(0,west,new Vector2(-141,0));
        match.Spawn(1,east,new Vector2(146,0));
        match.Elapsed=38;
        yield return new WaitForSecondsRealtime(1);
        Check(spectatorBroadcast.Length==32&&spectatorCount==1&&CanSeeUnit(visible),"Real game broadcasts while a friend is connected");
        page=Page.Pause;OpenSettings();
        yield return Action("settings.tab.4");
        yield return Shot(folder,"01-watch-settings");
        yield return Action("settings.friend-viewing");
        yield return Action("settings.apply");
        Check(options.disableFriendViewing&&page==Page.Pause,"The privacy toggle applies from the game menu");
        Check((bool?)JObject.Parse(File.ReadAllText(settingsPath))["disableFriendViewing"]==true,"The setting is written to disk");
        yield return WaitSpectatorQa(()=>spectatorCount==0&&spectatorBroadcast=="","Disabling access stops the active stream");
        MainMenu();
        socialDue=0;
        yield return WaitSpectatorQa(()=>socialState["friends"]?[0]?["spectate"]?.Value<bool>()==true,
            "Authorized friend advertises a running challenge");
        yield return Action("account.friends");
        yield return Action("hub.peer.Друзьяfriend");
        yield return Shot(folder,"02-watch-friend");
        yield return Action("hub.action.watch");
        yield return WaitSpectatorQa(()=>SpectatorSceneReady&&spectatorFog!=null&&match.Elapsed>=38,
            "The guest stream loads a live rendering replica");
        Check(match.NetworkReplica&&page==Page.Spectator&&!onlineMatch&&networkSlot<0,"The viewer has no playable slot");
        Check(match.Units.Count==1&&match.Units[0].team==0&&!FogVisible(new Vector2(146,0)),
            "The live stream excludes enemies behind the player's fog");
        Check(Mathf.Approximately(match.Elapsed,38),"Spectator does not run the local match simulation");
        yield return Shot(folder,"03-live-view");
        yield return Action("spectator.focus");
        yield return Action("spectator.exit");
        Check(page==Page.Main&&match==null&&HasNetworkProfile,"Leaving watch returns to the main menu with the account intact");
        RequestSpectator("friend");
        yield return WaitSpectatorQa(()=>SpectatorSceneReady&&spectatorFog!=null,"Viewer reconnects to the same active challenge after exiting");
        Check(match.Elapsed==38&&!onlineMatch,"Rejoining shows the current replica without creating a playable session");
        yield return Action("spectator.exit");
        socialDue=0;nextNotificationState=0;
        yield return WaitSpectatorQa(()=>activeNotification?.FriendRequest==true,"A new incoming friend request appears in the same notification area");
        Check(socialNotifications.Count==0,"The outgoing request and repeated polls do not produce additional toasts");
        yield return Shot(folder,"04-friend-request");
        yield return Action("social.toast.open");
        yield return Shot(folder,"05-friend-request-actions");
        yield return Action("social.toast.accept");
        yield return WaitSpectatorQa(()=>((JArray)socialState["friends"]).Count==2,"Accept from the toast updates the friends list");
        yield return Action("account.friends");
        yield return Action("hub.peer.Друзьяfriend");
        float historyOpened=Time.realtimeSinceStartup;
        socialNext=Mathf.Max(socialNext,historyOpened+.35f);
        nextNotificationState=0;socialDue=historyOpened+5;
        yield return Action("hub.action.message");
        Check(socialMessages.Count>0||ConversationEmptyText("empty")=="Загружаем сообщения…",
            "New chat indicates loading before history is available");
        yield return WaitSpectatorQa(()=>socialMessages.Count==1,"Opening the chat requests history ahead of presence polling");
        Check(Time.realtimeSinceStartup-historyOpened<2.5f,"The first chat history does not wait for the five-second poll");
        CloseAccountOverlay();
        OpenOverlayConversation(new JObject{["id"]="friend",["name"]="Друг"});
        Check(socialMessages.Count==1&&(string)socialMessages[0]["text"]=="История готова",
            "Reopening the same chat shows cached messages immediately");
        yield return Shot(folder,"06-chat-history");
        File.WriteAllText(Path.Combine(folder,"client.json"),new JObject{["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks}.ToString());
        Application.Quit(testFailures==0?0:1);
    }
    IEnumerator WaitSpectatorQa(Func<bool> condition,string detail){
        float limit=Time.realtimeSinceStartup+32;
        while(!condition()&&Time.realtimeSinceStartup<limit)yield return null;
        Check(condition(),detail);
        if(!condition()){Debug.LogError("CF_SPECTATOR_TIMEOUT "+detail);Application.Quit(1);}
    }
}
}
#endif
