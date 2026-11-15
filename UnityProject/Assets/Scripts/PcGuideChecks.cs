using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseGuide(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"guide-validation"));Directory.CreateDirectory(folder);MainMenu();int originalRace=raceChoice,originalBot=botRaceChoice;
        yield return Action("main.guide");Check(page==Page.Guide&&match==null&&guideBuilders.Count==15,"Main menu opens real clearing with all fifteen builders without starting a match");
        Vector3 sky=Camera.main.transform.position;yield return new WaitForSecondsRealtime(1.5f);Check(Vector3.Distance(sky,Camera.main.transform.position)>20,"Sky-to-clearing camera descent animates");
        Check(guideBuilders.All(m=>m.gameObject.activeInHierarchy&&m.clips.Length>0&&!m.reforged),"Every gallery builder uses animated classic source model");yield return Shot(folder,"01-builder-clearing");
        var catalogs=new JArray();for(int race=0;race<15;race++){GuideChooseRace(race);var entries=GuideEntries().ToArray();Check(entries.Length>0&&entries.All(k=>GuideIcon(k)&&GuidePrefab(k,false)&&GuideUnitKinds(k).All(actor=>GuidePrefab(actor,true))),"Complete catalog icons and classic model references for "+GuideRaceName(race));
            foreach(int k in entries){var line=guideAuthority.Lines[k];Check(line.upgrades.All(raw=>guideAuthority.Kind(raw)>=0),"Native upgrade links resolve for "+race+"/"+line.building);}
            catalogs.Add(new JObject{["race"]=ProgressRaces[race],["entries"]=entries.Length});
        }
        GuideChooseRace(0);yield return Shot(folder,"02-human-grid");int first=GuideEntries().First(k=>guideAuthority.Lines[k].Producer&&guideAuthority.Lines[k].upgrades.Length>0);string rawcode=guideAuthority.Lines[first].building;
        yield return Action("guide.entry."+rawcode);Check(guideEntry==first&&guideUnit&&guideModel.raw==guideAuthority.Lines[first].unit,"Producer card opens matching unit model by default");Check(guideDescription.Contains("Здоровье:")&&guideDescription.Contains("Броня:")&&guideDescription.Contains("Цикл при полном вложении:"),"Scrollable description includes native stats and production economy");yield return Shot(folder,"03-human-unit");
        yield return Action("guide.building");Check(!guideUnit&&guideModel.raw==rawcode,"Building toggle shows original production building");yield return Shot(folder,"04-human-building");
        string upgraded=guideAuthority.Lines[first].upgrades[0];yield return Action("guide.upgrade."+upgraded);Check(guideAuthority.Lines[guideEntry].building==upgraded&&guideHistory.Count==1,"Upgrade button opens linked next version and records previous entry");yield return Shot(folder,"05-human-upgrade");yield return Action("guide.back");Check(guideEntry==first,"Back from upgrade returns to original version");
        guideTextScroll=new Vector2(0,1000);yield return Shot(folder,"06-description-scroll");yield return Action("guide.back");Check(guideEntry==-1,"Back returns to construction squares");guideQuery="несуществующая постройка";Check(!GuideEntries().Any(),"Catalog search filters entries");guideQuery="";
        GuideChooseRace(9);Check(GuideUnitKinds(guideAuthority.Kind("h046")).Length==0,"Removed primordial controller has no invented giant unit preview");GuideChooseRace(4);int twin=guideAuthority.Kind("h06O");Check(GuideUnitKinds(twin).Length==2,"Invisible twin controller resolves both actual native child fighters");GuideSelectEntry(twin);yield return Shot(folder,"10-twin-preview");
        GuideChooseRace(1);yield return Shot(folder,"07-orc-grid");GuideChooseRace(12);guideGridScroll=new Vector2(0,10000);yield return Shot(folder,"08-ultimate-grid");
        GuideChooseRace(14);int panda=GuideEntries().First(k=>guideAuthority.Lines[k].Producer);GuideSelectEntry(panda);yield return new WaitForSecondsRealtime(.3f);Check(guideModel.raw==guideAuthority.Lines[panda].unit&&guideCamera.enabled&&guideTexture.IsCreated(),"Pandaren live preview uses its own source model and isolated render target");yield return Shot(folder,"09-pandaren-model");
        float angle=guideAngle;yield return new WaitForSecondsRealtime(.3f);Check(guideAngle>angle,"Live preview rotates smoothly");yield return Action("guide.spin");angle=guideAngle;yield return new WaitForSecondsRealtime(.3f);Check(Mathf.Approximately(angle,guideAngle),"Preview rotation can pause");
        yield return Action("guide.menu");Check(page==Page.Main&&!guideGarden.activeSelf&&!guideCamera.enabled&&raceChoice==originalRace&&botRaceChoice==originalBot,"Leaving guide stops its cameras and models and preserves match setup");yield return Action("main.guide");yield return new WaitForSecondsRealtime(1.4f);Check(guideBuilders.Count==15&&guideGarden.activeSelf,"Reopening reuses gallery without duplicate builders");yield return Shot(folder,"10-reopened-clearing");
        var report=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["build"]=Version,["checks"]=testChecks,["failures"]=testFailures,["catalogs"]=catalogs};File.WriteAllText(Path.Combine(folder,"guide-verification.json"),report.ToString());Debug.Log("CF_GUIDE_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}
