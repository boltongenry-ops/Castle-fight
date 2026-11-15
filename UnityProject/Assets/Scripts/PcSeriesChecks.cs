using System.Collections;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseSeries(string folder) {
        MainMenu();roundsChoice=1;yield return Action("main.play");yield return Action("setup.human");yield return Action("setup.rounds");
        Check(roundsChoice==3,"series setup selects best of three");yield return Shot(folder,"74-series-setup");
        startGold=1000;yield return Action("setup.start");testHoldAuthority=true;match.Gold[1]=0;
        Check(series.Round==1&&series.RequiredWins==2&&series.Wins[0]==0&&series.Wins[1]==0,"new series starts with empty score");
        Check(match.TryBuild(0,match.Kind("h000"),7,15,out _),"first round contains a construction job");
        match.Spawn(0,match.Kind("h000"),new Vector2(-160,35));match.CastleHp[1]=0;Tick(.05f);
        Check(page==Page.Result&&series.RoundFinished&&!series.Finished&&series.Wins[0]==1,"destroying a castle credits the winner once");series.FinishRound(0);Check(series.Wins[0]==1,"duplicate result does not add another win");yield return Shot(folder,"75-series-round-result");
        yield return Action("result.again");
        Check(page==Page.Match&&series.Round==2&&series.Wins[0]==1&&match.Units.Count==0&&match.Huts.Count==0&&match.Builders[0].jobs.Count==0&&match.Gold[0]==startGold&&match.CastleHp[1]==HumanMatch.CastleMax,"next round resets battle and economy while retaining series score");
        yield return Shot(folder,"76-series-score");
        yield return Action("match.pause");yield return Action("pause.leave");yield return Action("leave.confirm");
        Check(series.Wins[0]==1&&series.Wins[1]==1&&!series.Finished,"surrender credits the opponent for the current round");
        yield return Action("result.again");Check(series.Round==3,"deciding round advances");
        match.CastleHp[1]=0;Tick(.05f);Check(series.Finished&&series.Wins[0]==2&&series.Wins[1]==1,"series ends at the required majority");yield return Shot(folder,"77-series-finished");
        yield return Action("result.again");Check(series.Round==1&&series.Wins[0]==0&&series.Wins[1]==0,"rematch resets the complete series");
        match.CastleHp[0]=match.CastleHp[1]=0;Tick(.05f);Check(series.Draw&&!series.Finished&&series.Wins[0]==0&&series.Wins[1]==0,"simultaneous castle loss records a draw without a win");
        yield return Action("result.again");Check(series.Round==1&&!series.RoundFinished&&series.Wins[0]==0&&series.Wins[1]==0,"draw replays the same round");
        var five=new PcSeries(5);for(int i=0;i<3;i++){five.FinishRound(1);if(i<2)Check(five.Advance(),"five round series advances");}Check(five.Finished&&five.Wins[1]==3&&!five.Advance(),"best of five stops at three wins");
        var seven=new PcSeries(7);for(int i=0;i<4;i++){seven.FinishRound(0);if(i<3)seven.Advance();}Check(seven.Finished&&seven.Wins[0]==4,"best of seven stops at four wins");
        roundsChoice=1;Debug.Log("CF_SERIES_PLAYER_PASS");
    }
}
}
