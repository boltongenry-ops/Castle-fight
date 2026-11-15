using System.Linq;
using Newtonsoft.Json.Linq;
namespace CastleFight {
public sealed partial class HumanMatch {
    public JObject ChallengeRules;
    string ChallengePurchaseError(int team,int kind,int replacing){
        if(ChallengeRules==null)return "";
        if((bool?)ChallengeRules["no_legends"]==true&&Lines[kind].slots>0)return "Испытание: легендарные постройки недоступны";
        int cap=(int?)ChallengeRules["max_buildings"]??0;
        if(team==0&&replacing<0&&cap>0&&Huts.Count(h=>h.team==0)>=cap)return "Испытание: максимум "+cap+" построек (включая очередь)";
        return "";
    }
    public bool ChallengeRescueAllowed=>(bool?)ChallengeRules?["no_rescue"]!=true;
    public bool ChallengeVictoryEligible=>Winner==0&&Elapsed<=F(ChallengeRules,"max_seconds",float.MaxValue)&&CastleHp[0]>=CastleMax*F(ChallengeRules,"min_castle_fraction");
}
}
