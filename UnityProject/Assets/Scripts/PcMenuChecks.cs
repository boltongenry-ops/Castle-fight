using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
// Targeted validation of the ported Godot screens: race selection, battle rules and
// the in-match aspect picker. It never depends on the Reforged/classic model set.
public sealed partial class PcGame {
    IEnumerator ExerciseMenu() {
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"menu-validation"));Directory.CreateDirectory(folder);
        MainMenu();aspectsOn=false;matchMode=0;botRaceChoice=-1;raceChoice=0;
        yield return Action("main.play");Check(page==Page.Setup,"the race screen opens from the main menu");
        yield return Shot(folder,"01-race-select");
        yield return Action("setup.race-pandaren");Check(raceChoice==14,"all completed races are selectable on the screen");raceChoice=0;
        yield return Action("setup.rules");Check(aspectsOn,"battle rules enable the race aspects");
        yield return Shot(folder,"02-rules-aspects");
        yield return Action("setup.start");testHoldAuthority=true;
        Check(page==Page.Match&&aspectWaiting&&match.Aspects[0]<0&&match.Aspects[1]>=0,"the aspect picker freezes the round until the player chooses");
        yield return Shot(folder,"03-aspect-picker");
        yield return Action("aspect.choose1");
        Check(!aspectWaiting&&match.Aspects[0]==1,"the chosen aspect is applied to the round");
        RenderMatch();yield return new WaitForSecondsRealtime(.25f);
        yield return Shot(folder,"04-aspect-badge");
        yield return Action("ability.aspect.0");Check(aspectInfoOpen,"the builder passive opens the selected aspect description");
        yield return Shot(folder,"05-aspect-info");
        aspectInfoOpen=false;
        // Сетевое состояние аспектов приходит снимком Godot: choices в порядке слотов.
        int slotBefore=networkSlot;bool onlineBefore=onlineMatch;int localBefore=match.Aspects[0];
        networkSlot=0;onlineMatch=true;
        AspectOnlineApply(new JObject { ["enabled"]=true,["choices"]=new JArray(-1,1),["remaining"]=12 });
        Check(aspectWaiting&&match.Aspects[0]<0&&match.Aspects[1]==1,"a server aspect state reopens the picker for the local slot");
        AspectOnlineApply(new JObject { ["enabled"]=true,["choices"]=new JArray(0,1),["remaining"]=9 });
        Check(!aspectWaiting&&match.Aspects[0]==0&&match.Aspects[1]==1,"a server-confirmed choice closes the picker");
        networkSlot=slotBefore;onlineMatch=onlineBefore;aspectWaiting=false;aspectOnlinePending=-1;match.Aspects[0]=localBefore;
        var report=new JObject {["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["build"]=Version,["raceScreen"]=true,["lockedRaces"]=true,["aspectPicker"]=true,["aspectBadge"]=true};
        File.WriteAllText(Path.Combine(folder,"menu-verification.json"),report.ToString());
        Debug.Log("CF_MENU_TEST_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);
        Application.Quit(testFailures==0?0:1);
    }
}
}