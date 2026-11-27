#if CF_SPECTATOR_QA
using System;
using System.Collections;
using System.IO;
using System.Linq;
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
        int flyer=Array.FindIndex(match.Lines,line=>line.flyHeight>0&&line.Producer&&raceIndices[matchRace].ContainsKey(line.building));
        var visible=match.Spawn(0,flyer,new Vector2(-141,0));
        var enemy=match.Spawn(1,east,new Vector2(-128,0));
        visible.attackTarget=enemy.id;visible.attackPoseLeft=1;visible.pose="Attack";
        match.Spawn(1,east,new Vector2(146,0));
        var completed=new HumanMatch.Hut{id=510,kind=west,team=0,paid=true,started=true,position=new Vector2(-144,26),hp=700,maxHp=700,spawnLeft=4};
        var construction=new HumanMatch.Hut{id=511,kind=west,team=0,started=true,position=new Vector2(-165,23),hp=700,maxHp=700,buildLeft=2};
        var enemyHut=new HumanMatch.Hut{id=512,kind=east,team=1,paid=true,started=true,position=new Vector2(-125,-2),hp=700,maxHp=700};
        match.Huts.AddRange(new[]{completed,construction,enemyHut});
        match.Builders[0].jobs.Add(construction.id);
        match.Elapsed=185;
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
        yield return WaitSpectatorQa(()=>SpectatorSceneReady&&spectatorFog!=null&&match.Elapsed>=185,
            "The guest stream loads a live rendering replica");
        Check(match.NetworkReplica&&page==Page.Spectator&&!onlineMatch&&networkSlot<0,"The viewer has no playable slot");
        Check(match.Units.Count==2&&match.Units.Exists(u=>u.team==1)&&!FogVisible(new Vector2(146,0)),
            "The live stream excludes enemies behind the player's fog");
        yield return null;
        Check(match.Huts.Count==3&&new[]{510,511,512}.All(id=>huts.TryGetValue(id,out var view)&&view.gameObject.activeSelf),
            "Late join renders a completed producer, an active construction and the visible enemy building");
        Check(match.Builders[0].jobs.SequenceEqual(new[]{511})&&match.Huts.Find(h=>h.id==510).spawnLeft==4,
            "Initial frame restores the construction queue and producer timer");
        Check(units.TryGetValue(visible.id,out var flyerView)&&flyerView.gameObject.activeSelf&&
            flyerView.transform.position.y>World(match.Units[0].position).y+.1f&&units[enemy.id].gameObject.activeSelf,
            "Both combatants render and the flyer keeps its height");
        Check(Mathf.Approximately(match.Elapsed,185),"Spectator does not run the local match simulation");
        SelectAtScreenPoint(UnitSelectionPoint(match.Units.Find(u=>u.id==visible.id)));
        Check(SelectedUnit?.id==visible.id&&SpectatorAttackTarget(SelectedUnit)==match.Lines[east].unitName,
            "Picking the flyer opens its card and identifies the visible target");
        Check(bossSoundtrack&&bossSoundtrack.clip&&bossSoundtrack.isPlaying&&SpectatorBossRect==HordeHud&&
            (float?)spectatorBoss?["left"]==21,"Boss soundtrack and the host's wave timer are active on late join");
        yield return Action("hud.score-toggle");
        yield return Action("hud.score-toggle");
        yield return Action("portrait.focus");
        yield return Shot(folder,"03-live-view");
        var hutPoint=Camera.main.WorldToScreenPoint(World(completed.position)+Vector3.up*2.5f);
        touchPointer=hutPoint;explicitTouchPointer=true;
        try{HandleWorldTap();}finally{explicitTouchPointer=false;}
        Check(SelectedHut?.id==completed.id&&!buildMenu,"A completed producer can be inspected without entering build mode");
        pendingTestAction="building.sell";
        yield return null;
        Check(pendingTestAction=="building.sell"&&SelectedHut?.id==completed.id,"The spectator card does not expose orders");
        pendingTestAction=null;
        yield return Shot(folder,"03b-spectator-building");
        yield return Action("spectator.focus");
        yield return Action("spectator.exit");
        Check(page==Page.Main&&match==null&&HasNetworkProfile,"Leaving watch returns to the main menu with the account intact");
        RequestSpectator("friend");
        yield return WaitSpectatorQa(()=>SpectatorSceneReady&&spectatorFog!=null,"Viewer reconnects to the same active challenge after exiting");
        Check(match.Elapsed==185&&!onlineMatch&&match.Huts.Count==3,"Rejoining restores the current replica without a playable session");
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
