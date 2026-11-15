using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    PcRacePack ultimatePack,nerubianPack,pandarenPack;
    public string[][] ultimateRoundLoadouts={Array.Empty<string>(),Array.Empty<string>()};
    void ApplyUltimateRound(ref JObject first,ref JObject second){
        ultimateRoundLoadouts=new[]{Array.Empty<string>(),Array.Empty<string>()};if(matchRace!=12&&matchBotRace!=12)return;
        var canonical=JObject.Parse(RaceCatalog(12));var draw=new UltimateLoadout((JArray)canonical["ultimateSlots"],n=>UnityEngine.Random.Range(0,n));
        if(networkRoundLoadouts!=null){
            if(matchRace==12){ultimateRoundLoadouts[0]=HumanMatch.Strings(networkRoundLoadouts[networkSlot]);first=draw.Playable(canonical,ultimateRoundLoadouts[0]);}
            if(matchBotRace==12){ultimateRoundLoadouts[1]=HumanMatch.Strings(networkRoundLoadouts[1-networkSlot]);second=draw.Playable(canonical,ultimateRoundLoadouts[1]);}
            return;
        }
        if(matchRace==12){ultimateRoundLoadouts[0]=draw.Draw();first=draw.Playable(canonical,ultimateRoundLoadouts[0]);}
        if(matchBotRace==12){ultimateRoundLoadouts[1]=draw.Draw();second=draw.Playable(canonical,ultimateRoundLoadouts[1]);}
        Debug.Log("CF_ULTIMATE_ROUND_DRAW "+new JArray(ultimateRoundLoadouts.Select(ids=>new JArray(ids))));
    }
}
}
