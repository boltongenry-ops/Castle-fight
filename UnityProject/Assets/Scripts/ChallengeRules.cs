using System.Linq;
using Newtonsoft.Json.Linq;
namespace CastleFight {
public sealed partial class HumanMatch {
    public JObject ChallengeRules;
    string ChallengePurchaseError(int team,int kind,int replacing){
        if(ChallengeRules==null)return "";
        if((bool?)ChallengeRules["no_legends"]==true&&Lines[kind].slots>0)return "Испытание: легендарные постройки недоступны";
        int cap=(int?)ChallengeRules["max_buildings"]??0;
        if(team==0){
            var line=Lines[kind];
            if((bool?)ChallengeRules["no_towers"]==true&&line.category=="tower")return "Испытание: защитные башни недоступны";
            if((bool?)ChallengeRules["no_specials"]==true&&!line.Producer&&line.category!="tower"&&line.building!="h008")return "Испытание: специальные постройки недоступны";
            int producers=(int?)ChallengeRules["max_producers"]??0;
            bool wasProducer=replacing>=0&&Lines[replacing].Producer;
            if(producers>0&&line.Producer&&!wasProducer&&Huts.Count(h=>h.team==0&&Lines[h.kind].Producer)>=producers)return "Испытание: максимум "+producers+" производителя";
        }
        if(team==0&&replacing<0&&cap>0&&Huts.Count(h=>h.team==0)>=cap)return "Испытание: максимум "+cap+" построек (включая очередь)";
        return "";
    }
    public bool ChallengeRescueAllowed=>(bool?)ChallengeRules?["no_rescue"]!=true;
    string ProducerFamily(int kind){var seen=new System.Collections.Generic.HashSet<int>();while(seen.Add(kind)){int parent=System.Array.FindIndex(Lines,l=>l.upgrades.Contains(Lines[kind].building));if(parent<0)break;kind=parent;}return Lines[kind].building;}
    public int ChallengeUpgradedBuildings=>Huts.Count(h=>h.team==0&&h.paid&&h.hp>0&&h.spent>h.baseCost);
    public int ChallengeProducerFamilies=>Huts.Where(h=>h.team==0&&h.paid&&h.hp>0&&Lines[h.kind].Producer).Select(h=>ProducerFamily(h.kind)).Distinct().Count();
    public bool ChallengeShopAllowed(int team)=>team!=0||(bool?)ChallengeRules?["no_shop"]!=true;
    public bool ChallengeVictoryEligible=>Winner==0&&Elapsed<=F(ChallengeRules,"max_seconds",float.MaxValue)&&CastleHp[0]>=CastleMax*F(ChallengeRules,"min_castle_fraction")&&ChallengeUpgradedBuildings>=F(ChallengeRules,"min_upgraded_buildings")&&ChallengeProducerFamilies>=F(ChallengeRules,"min_distinct_producers");
}
}
