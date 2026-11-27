using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class HumanMatch {
    public float CastleHealthLimit(int team){
        if(ChallengeRules==null)return CastleMax;
        float shared=F(ChallengeRules,"castle_hp",CastleMax);
        return Mathf.Max(1,F(ChallengeRules,team==0?"player_castle_hp":"opponent_castle_hp",shared));
    }

    public float NaturalCastleRecovery=>(bool?)ChallengeRules?["no_castle_regen"]==true?0:CastleRegen;
    public bool ChallengeBuilderRepairAllowed(int team)=>team==1?!LastProtocol:(bool?)ChallengeRules?["no_builder_repair"]!=true;
    public float ChallengeSurvivalTime=>F(ChallengeRules,"survive_seconds");

    public void BeginChallengeRules(JObject rules){
        ChallengeRules=(JObject)rules.DeepClone();
        int commonWood=(int?)ChallengeRules["start_wood"]??Wood[0];
        for(int team=0;team<2;team++){
            string startFraction=team==0?"player_castle_start_fraction":"opponent_castle_start_fraction";
            CastleHp[team]=Mathf.Max(1,CastleHealthLimit(team)*Mathf.Clamp01(F(ChallengeRules,startFraction,1)));
            Wood[team]=Mathf.Max(0,team==1?((int?)ChallengeRules["opponent_start_wood"]??commonWood):commonWood);
            if(!ChallengeRescueAllowed)RescueCharges[team]=0;
        }
        Gold[1]=Mathf.Max(0,F(ChallengeRules,"opponent_start_gold",Gold[1]));
        PrepareEternalWinter();BeginChoir();BeginCursedHorde();BeginLastProtocol();BeginAbyssTrial();BeginQuackTrial();
    }

    public float RestoreCastleHealth(int team,float amount){
        if(amount<=0||CastleHp[team]<=0||team==WinterBossTeam&&WinterCastleFrozen)return 0;
        float before=CastleHp[team];
        CastleHp[team]=Mathf.Min(CastleHealthLimit(team),before+amount);
        return Mathf.Max(0,CastleHp[team]-before);
    }

    bool ChallengeFinalConditions(){
        float requiredHealth=F(ChallengeRules,"min_castle_fraction");
        if(requiredHealth>0&&CastleHp[0]<CastleHealthLimit(0)*requiredHealth)return false;
        float upgrades=F(ChallengeRules,"min_upgraded_buildings");
        if(upgrades>0&&ChallengeUpgradedBuildings<upgrades)return false;
        float families=F(ChallengeRules,"min_distinct_producers");
        return families<=0||ChallengeProducerFamilies>=families;
    }

    void ResolveChallengeClock(float dt){
        if(ChallengeRules==null||Finished)return;
        TickEternalWinter();TickWinterQueenStorm();TickChoir();TickCursedHorde();TickLastProtocol(dt);TickAbyssTide();TickQuackTrial();
        float survive=ChallengeSurvivalTime;
        if(survive>0&&Elapsed>=survive){
            Winner=ChallengeFinalConditions()?0:1;
            return;
        }
        if(Elapsed>F(ChallengeRules,"max_seconds",float.MaxValue))Surrender(0);
    }
}
}
