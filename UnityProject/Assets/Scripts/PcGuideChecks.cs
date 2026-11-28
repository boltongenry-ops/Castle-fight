using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseGuide(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"guide-validation"));
        Directory.CreateDirectory(folder);
        MainMenu();
        int originalRace=raceChoice,originalBot=botRaceChoice;
        yield return Action("main.guide");
        Check(page==Page.Guide&&match==null&&guideRace<0,"Main menu opens the race catalog without starting a match");
        Check(AllRaceUnity.Length==18&&AllRaceUnity.All(RaceStaged),"All eighteen races are available in the guide");
        yield return Shot(folder,"01-races");
        var catalogs=new JArray();
        for(int race=0;race<AllRaceUnity.Length;race++){
            GuideChooseRace(race);
            float deadline=Time.realtimeSinceStartup+10;
            while(guideLoading&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(guideRace==race&&guideAuthority!=null,"Opened "+GuideRaceName(race));
            if(guideRace!=race)continue;
            var portrait=RaceArtworkFor(race);
            Check(portrait&&portrait.width*2==portrait.height*3,"Dedicated full-frame portrait loaded for "+GuideRaceName(race));
            var entries=GuideEntries().ToArray();
            Check(entries.Length>0&&entries.All(k=>GuideIcon(k)),"Construction icons loaded for "+GuideRaceName(race));
            Check(entries.All(k=>guideAuthority.Lines[k].upgrades.All(raw=>guideAuthority.Kind(raw)>=0)),
                "Upgrade branches resolve for "+GuideRaceName(race));
            catalogs.Add(new JObject{["race"]=GuideRaceName(race),["entries"]=entries.Length});
        }
        GuideChooseRace(0);
        yield return Shot(folder,"02-buildings");
        int first=GuideEntries().First(k=>guideAuthority.Lines[k].Producer&&guideAuthority.Lines[k].upgrades.Length>0);
        string rawcode=guideAuthority.Lines[first].building;
        yield return Action("guide.entry."+rawcode);
        Check(guideEntry==first&&guideUnit&&GuideUnitIcon(GuideActorKind),"Producer opens its fighter portrait");
        Check(guideDescription.Contains("Здоровье:")&&guideDescription.Contains("Броня:")&&guideDescription.Contains("Цикл при полном вложении:"),
            "Fighter card shows combat stats and production cycle");
        yield return Shot(folder,"03-unit");
        yield return Action("guide.building");
        Check(!guideUnit&&GuideIcon(guideEntry)&&guideDescription.Contains("Постройка:"),"Building tab shows its icon and cost");
        string upgraded=guideAuthority.Lines[first].upgrades[0];
        yield return Action("guide.upgrade."+upgraded);
        Check(guideAuthority.Lines[guideEntry].building==upgraded&&guideHistory.Count==1,"Upgrade action follows the catalog link");
        yield return Action("guide.back");
        Check(guideEntry==first,"Back restores the previous entry");
        yield return Action("guide.tab.1");
        Check(GuideEntries().All(k=>GuideUnitKinds(k).Length>0),"Unit tab contains only actual fighters");
        yield return Shot(folder,"04-roster");
        yield return Action("guide.tab.2");
        Check(GuideEntries().All(k=>!guideAuthority.Lines[k].Producer||guideAuthority.Lines[k].slots>0),"Special tab shows spells, towers and legendary buildings");
        guideQuery="несуществующая постройка";
        Check(!GuideEntries().Any(),"Name search filters the catalog");
        yield return Action("guide.tab.3");
        Check(AspectCount(guideRace)>0,"Aspect tab uses the selected race's aspect catalog");
        yield return Shot(folder,"05-aspects");
        GuideChooseRace(4);
        int twin=guideAuthority.Kind("h06O");
        Check(GuideUnitKinds(twin).Length==2,"Twin controller resolves both actual fighters");
        GuideSelectEntry(twin);
        yield return Action("guide.unit");
        Check(guideActorIndex==1,"Twin entry switches between both portraits and stat cards");
        yield return Action("guide.back");
        yield return Action("guide.back");
        Check(page==Page.Guide&&guideRace<0,"Back opens the race index");
        yield return Action("guide.back");
        Check(page==Page.Main&&guideAuthority==null&&raceChoice==originalRace&&botRaceChoice==originalBot,
            "Leaving the guide releases its catalog and preserves match setup");
        var report=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["build"]=Version,
            ["checks"]=testChecks,["failures"]=testFailures,["catalogs"]=catalogs};
        File.WriteAllText(Path.Combine(folder,"guide-verification.json"),report.ToString());
        Debug.Log("CF_GUIDE_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);
        Application.Quit(testFailures==0?0:1);
    }
}
}
