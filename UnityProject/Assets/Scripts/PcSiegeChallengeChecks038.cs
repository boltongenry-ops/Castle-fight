using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator CheckSiegeChallenges038(string output,JObject human){
        var rows=(JArray)progression.Catalog["challenges"];
        var siege=rows.OfType<JObject>().Where(ChallengeCampaign038.IsSiege).ToArray();
        Check(rows.Count==42&&siege.Length==12&&rows.Select(row=>(string)row["id"]).Distinct().Count()==42,
            "Twelve new siege challenges and thirty stable original IDs");
        var duplicate=(JObject)progression.Catalog.DeepClone();
        ChallengeCampaign038.Extend(duplicate);
        Check(((JArray)duplicate["challenges"]).Count==42,"Extending the campaign twice does not duplicate rewards");
        foreach(var row in siege){
            int player=Array.IndexOf(ProgressRaces,(string)row["race"]),opponent=Array.IndexOf(ProgressRaces,(string)row["opponent"]);
            var state=new HumanMatch((JObject)human.DeepClone(),(int?)row["start_gold"]??500,0);
            state.BeginChallengeRules(row);
            Check(RaceStaged(player)&&RaceStaged(opponent)&&state.CastleHp[0]>0&&state.CastleHp[1]>0&&
                state.CastleHp[0]<=state.CastleHealthLimit(0)&&state.CastleHp[1]<=state.CastleHealthLimit(1),
                "Valid race pair and fortress values for "+row["id"]);
        }

        MainMenu();yield return Action("main.challenges");yield return Action("progress.siege");
        Check(Workshop.siegeOnly&&page==Page.Progression,"Siege tab shows the new chapter");
        yield return Shot(output,"05-siege-challenges");
        yield return Action("challenge.siege_iron_marathon");
        testHoldAuthority=true;
        Check(match.CastleHp[0]==40000&&match.CastleHp[1]==40000&&match.Gold[0]==1600&&match.Wood[1]==600,
            "Actual Great Siege opens at 40,000 HP for both castles with the configured budget");
        match.Step(.05f);
        Check(match.CastleHp[0]==40000&&match.CastleHp[1]==40000,"Natural regeneration never clamps the 40,000 HP match back to 5000");
        match.CastleHp[0]=39600;
        Check(match.RestoreCastleHealth(0,1200)==400&&match.CastleHp[0]==40000,"Healing clamps to the challenge's real maximum");
        match.CastleHp[0]=39999;
        match.Builders[0].position=HumanMatch.CastlePosition(0)+Vector2.right*6.7f;
        Check(match.RepairCastle(0),"Builder can repair missing health above 5000 HP");
        Tick(.15f);
        Check(match.CastleHp[0]==40000&&!match.Builders[0].repairing,"Builder finishes repair at 40,000 HP");
        SelectCastle();yield return Shot(output,"06-siege-40000");
        int wallet=progression.Coins;
        match.CastleHp[1]=0;Tick(.05f);
        Check(match.Winner==0&&progression.Coins==wallet+300&&progression.Completed("siege_iron_marathon"),
            "Destroying the 40,000 HP fortress pays the first-completion reward");
        SettleChallenge();
        Check(progression.Coins==wallet+300,"Siege reward cannot be paid twice");
        MainMenu();

        var shield=new HumanMatch((JObject)human.DeepClone(),1100,0);
        shield.BeginChallengeRules(progression.Challenge("siege_steel_watch"));
        Check(shield.CastleHealthLimit(0)==8000&&shield.CastleHealthLimit(1)==20000&&shield.Gold[1]==1600&&shield.Wood[1]==650&&shield.RescueCharges[0]==0,
            "Asymmetric fortress, economy, and rescue restrictions reach authority");
        shield.Elapsed=359.9f;shield.CastleHp[0]=5400;
        shield.Step(.2f);
        Check(shield.Winner==0&&shield.ChallengeVictoryEligible,"Survival deadline awards victory when the health floor is met");
        var failure=new HumanMatch((JObject)human.DeepClone(),1100,0);
        failure.BeginChallengeRules(progression.Challenge("siege_steel_watch"));
        failure.Elapsed=359.9f;failure.CastleHp[0]=5190;
        failure.Step(.2f);
        Check(failure.Winner==1&&!failure.ChallengeVictoryEligible,"Survival fails below the required percentage of the true limit");
        var preempt=new HumanMatch((JObject)human.DeepClone(),1100,0);
        preempt.BeginChallengeRules(progression.Challenge("siege_steel_watch"));
        preempt.Elapsed=359.9f;preempt.CastleHp[0]=0;
        preempt.Step(.2f);
        Check(preempt.Winner==1,"A destroyed castle loses even on the survival deadline");
        var ice=new HumanMatch((JObject)human.DeepClone(),1200,0);
        ice.BeginChallengeRules(progression.Challenge("siege_no_rest"));
        ice.CastleHp[0]=10000;
        Check(!ice.RepairCastle(0)&&ice.NaturalCastleRecovery==0,"Only the defined challenge disables builder repair and natural recovery");
        ice.Step(.2f);
        Check(ice.CastleHp[0]==10000,"Fortress stays wounded without natural regeneration");
        ice.Elapsed=1320;ice.Step(.05f);
        Check(ice.Winner==1,"Regular deadline still enforces a loss when it expires");
        var casual=new HumanMatch((JObject)human.DeepClone(),250,0);
        Check(casual.CastleHp.All(hp=>hp==5000)&&casual.NaturalCastleRecovery>0,"Casual matches retain the 5000 HP limit and natural recovery");
    }
}
}
