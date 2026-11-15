using System;
namespace CastleFight {
// Offline fixed-race series; round rules follow Godot match_state.gd.
public sealed class PcSeries {
    public readonly int Rounds;
    public readonly int[] Wins=new int[2];
    public int Round {get;private set;}=1;
    public bool RoundFinished {get;private set;}
    public bool Finished {get;private set;}
    public bool Draw {get;private set;}
    public int RequiredWins=>Rounds/2+1;
    public PcSeries(int rounds){if(rounds!=1&&rounds!=3&&rounds!=5&&rounds!=7)throw new ArgumentOutOfRangeException(nameof(rounds));Rounds=rounds;}
    public void SetNetworkRound(int round,int localWins,int opponentWins){Round=Math.Max(1,Math.Min(Rounds,round));Wins[0]=Math.Max(0,localWins);Wins[1]=Math.Max(0,opponentWins);RoundFinished=false;Finished=false;Draw=false;}
    public void ApplyNetworkResult(int round,int localWins,int opponentWins,bool finished,bool draw){SetNetworkRound(round,localWins,opponentWins);RoundFinished=true;Finished=finished;Draw=draw;}
    public void FinishRound(int winner){
        if(RoundFinished||Finished)return;
        if(winner< -1||winner>1)throw new ArgumentOutOfRangeException(nameof(winner));
        RoundFinished=true;Draw=winner==-1;
        if(!Draw){Wins[winner]++;Finished=Wins[winner]>=RequiredWins||Round>=Rounds;}
    }
    public bool Advance(){if(!RoundFinished||Finished)return false;if(!Draw)Round++;RoundFinished=false;Draw=false;return true;}
}
}
