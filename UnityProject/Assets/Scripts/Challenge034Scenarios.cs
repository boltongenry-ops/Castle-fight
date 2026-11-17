using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public static class Challenge034Scenarios {
    public static void Run(JObject catalog,JObject human,string profile,Action<bool,string> check){
        ChallengeCatalog034.Extend(catalog);ChallengeCatalog034.Extend(catalog);
        var rows=(JArray)catalog["challenges"];
        check(rows.Count==30&&rows.Select(r=>(string)r["id"]).Distinct().Count()==30,"Thirty unique stable challenges; extending twice is safe");
        foreach(JObject row in rows){check((int)row["reward"]>0&&ChallengeCatalog034.Experience(row)>0&&((string)row["description"]).Length>30,"Reward, XP and description: "+row["id"]);}
        var store=new PcProgressionStore(catalog,profile);int coins=store.Coins,xp=store.Experience;
        int reward=store.Complete("five_pillars",100);check(store.Coins==coins+reward&&store.Experience==xp+ChallengeCatalog034.Experience(store.Challenge("five_pillars")),"First win grants tokens and persistent mastery XP");
        int before=store.Experience;store.Complete("five_pillars",90);check(store.Experience==before&&store.Best("five_pillars")==90,"Replay improves best time without farming XP");
        var reload=new PcProgressionStore(catalog,profile);check(reload.Experience==before&&reload.Coins==store.Coins&&reload.MasteryLevel>=2,"Mastery derives from saved completion IDs and survives reload");
        store.Complete("centaur_open_steppe",120);check(store.Experience>before,"New Centaur challenge awards XP");
        var m=new HumanMatch(human,100000,1);m.Wood[0]=m.Wood[1]=100000;m.LegendaryLimit[0]=m.LegendaryLimit[1]=20;
        int producer=m.BaseKinds.First(k=>m.Lines[k].Producer),tower=Array.FindIndex(m.Lines,l=>l.category=="tower"),special=Array.FindIndex(m.Lines,l=>l.category=="special");
        m.ChallengeRules=new JObject{["no_towers"]=true,["no_specials"]=true,["no_shop"]=true};
        check(m.PurchaseError(0,tower).Contains("башни")&&m.PurchaseError(1,tower)=="","Tower restriction applies only to player");
        check(m.PurchaseError(0,special).Contains("специальные")&&m.PurchaseError(0,producer)=="","Army-only challenge allows actual producers");
        check(m.ItemPurchaseError(0,0).Contains("покупки")&&m.ChallengeShopAllowed(1),"Castle shop restriction enforced by authority only for player");
        m.ChallengeRules=new JObject{["max_producers"]=2};
        for(int i=0;i<2;i++)m.Huts.Add(new HumanMatch.Hut{id=100+i,team=0,kind=producer,hp=100,maxHp=100,baseCost=100,spent=100});
        check(m.PurchaseError(0,producer).Contains("максимум 2"),"Unpaid queued producers count towards producer cap");
        check(m.PurchaseError(0,tower)=="","Producer cap does not limit towers");
        m.ChallengeRules=new JObject{["min_upgraded_buildings"]=2,["min_distinct_producers"]=2};m.Winner=0;
        check(!m.ChallengeVictoryEligible,"Bare victory cannot bypass required army composition");
        foreach(var h in m.Huts){h.paid=true;h.spent=150;}
        check(m.ChallengeUpgradedBuildings==2&&m.ChallengeProducerFamilies==1&&!m.ChallengeVictoryEligible,"Repeated producer family counts once, upgrades count independently");
        int another=m.BaseKinds.First(k=>m.Lines[k].Producer&&k!=producer);
        m.Huts.Add(new HumanMatch.Hut{id=200,team=0,kind=another,hp=100,paid=true});
        check(m.ChallengeVictoryEligible,"Required two live families and two upgraded buildings qualify");
        m.Huts[0].hp=0;check(!m.ChallengeVictoryEligible,"Destroyed upgraded building no longer satisfies objective");m.Huts[0].hp=100;
        m.ChallengeRules["max_seconds"]=60;m.Elapsed=61;check(!m.ChallengeVictoryEligible,"Deadline also enforced on composition challenge");m.Elapsed=60;
        m.ChallengeRules["min_castle_fraction"]=.8;m.CastleHp[0]=HumanMatch.CastleMax*.79f;check(!m.ChallengeVictoryEligible,"Castle health objective combined with composition");
        check(ChallengeCatalog034.Level(0)==1&&ChallengeCatalog034.Level(99)==1&&ChallengeCatalog034.Level(100)==2&&ChallengeCatalog034.Level(300)==3,"Mastery level thresholds are exact");
        foreach(JObject row in rows.Skip(18))check((string)row["race"]!=""&&(string)row["opponent"]!="","New challenge contains two races: "+row["id"]);
    }
}
}
