using System;using System.Linq;using Newtonsoft.Json.Linq;using UnityEngine;
namespace CastleFight {public sealed partial class PcGame {
    JArray onlineBattleStats=new JArray(),onlineChoices=new JArray();bool resultDetails;Vector2 resultScroll;Texture2D resultEmblem;
    void DrawOnlineResult(){
        GUI.enabled=false;DrawMatch();GUI.enabled=true;Fill(new Rect(0,0,1280,720),new Color(.008f,.018f,.026f,.89f));LobbyPanel(new Rect(200,35,880,650));
        bool won=match.Winner==0;var gold=new Color(.9f,.77f,.48f);var dim=new Color(.65f,.73f,.79f);
        var emblem=FrontendTexture(ref resultEmblem,"victory-emblem.png");if(emblem){var before=GUI.color;GUI.color=won?Color.white:dim;GUI.DrawTexture(new Rect(500,48,280,100),emblem,ScaleMode.ScaleToFit);GUI.color=before;}
        Text(240,153,800,65,won?"ПОБЕДА":match.Winner==1?"ПОРАЖЕНИЕ":"НИЧЬЯ",new GUIStyle(heading){fontSize=48,alignment=TextAnchor.MiddleCenter,normal={textColor=won?gold:dim}});
        Text(240,222,800,32,(series.Finished?"Серия завершена":"Битва окончена")+" · раунд "+series.Round+" / "+series.Rounds,new GUIStyle(label){alignment=TextAnchor.MiddleCenter});
        Text(242,270,280,36,ResultPlayerName(networkSlot),new GUIStyle(label){alignment=TextAnchor.MiddleCenter});Text(758,270,280,36,ResultPlayerName(1-networkSlot),new GUIStyle(label){alignment=TextAnchor.MiddleCenter});
        Text(528,259,224,62,series.Wins[0]+" : "+series.Wins[1],new GUIStyle(heading){fontSize=43,alignment=TextAnchor.MiddleCenter,normal={textColor=gold}});
        Text(242,317,796,30,TimeSpan.FromSeconds(match.Elapsed).ToString(@"mm\:ss")+" в бою · убийства "+match.TeamKills[0]+" / "+match.TeamKills[1]+" · построек "+match.Huts.Count(h=>h.team==0),new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
        if(resultDetails){resultScroll=GUI.BeginScrollView(new Rect(240,360,800,128),resultScroll,new Rect(0,0,770,200));Text(8,0,750,200,OnlineResultStatistics(),new GUIStyle(label){fontSize=17,wordWrap=true});GUI.EndScrollView();}
        else {Text(260,374,760,88,"Результат и ранг сохраняются на сервере.\nДля продолжения оба игрока выбирают реванш или следующий раунд.\nЕсли кто-то выберет смену расы, вы вернётесь в комнату.",new GUIStyle(label){fontSize=17,wordWrap=true,alignment=TextAnchor.MiddleCenter});}
        bool blocked=NetworkBlocked;GUI.enabled=!onlineDecision&&!blocked;
        if(LobbyButton("result.again",new Rect(240,505,388,55),series.Finished?"Реванш":"Следующий раунд",true)){onlineDecision=true;SendNetwork(new JObject{["type"]="next_round"});}
        if(LobbyButton("result.menu",new Rect(644,505,396,55),"Сменить расу / в комнату")){onlineDecision=true;SendNetwork(new JObject{["type"]="return_lobby"});}GUI.enabled=!blocked;
        if(LobbyButton("result.stats",new Rect(240,574,388,48),resultDetails?"Скрыть статистику":"Статистика"))resultDetails=!resultDetails;
        if(LobbyButton("result.exit",new Rect(644,574,396,48),"В главное меню"))NetworkReturnToMenu();GUI.enabled=true;
        string pending=onlineDecision?"Ожидаем решение соперника…":"Выберите, что делать дальше";
        if(onlineChoices.Count==2){string other=(string)onlineChoices[1-networkSlot];if(other=="lobby")pending="Соперник хочет сменить расу · подтвердите любой выбор";else if(other=="rematch")pending="Соперник готов продолжить";}
        Text(242,639,796,30,pending,new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
    }
    string ResultPlayerName(int slot){var players=networkRoom?["players"] as JArray;return players!=null&&slot>=0&&slot<players.Count?(string)players[slot]?["name"]??"Игрок":"Игрок";}
    string OnlineResultStatistics(){if(onlineBattleStats.Count!=2)return "Статистика сервера пока недоступна";var a=onlineBattleStats[networkSlot];var b=onlineBattleStats[1-networkSlot];string[] keys={"damage","castle_damage","healing","kills","spent","aspect_damage","aspect_prevented","aspect_healing"},names={"Урон","По замку","Лечение","Убийства","Потрачено золота","Бонус аспектов","Поглощено аспектом","Лечение аспектом"};return "Вы / Соперник\n"+string.Join("\n",keys.Select((key,i)=>names[i]+": "+((float?)a[key]??0).ToString("0")+" / "+((float?)b[key]??0).ToString("0")));}
}}
