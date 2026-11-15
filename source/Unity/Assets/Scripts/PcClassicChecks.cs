using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseClassicOnly() {
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"classic-validation"));Directory.CreateDirectory(folder);
        Check(!options.hdTextures,"Saved HD preference is overridden by classic mode");
        Check(reforgedUnits.Length==0&&reforgedBuildings.Length==0&&reforgedBuilders.Length==0&&reforgedCastles.Length==0,"Scene has no Reforged model references");
        var all=unitModels.Concat(buildingModels).Concat(builderModels).Concat(orcPack.Prefabs).Concat(nagaPack.Prefabs).Concat(naturePack.Prefabs).Concat(chaosPack.Prefabs).Concat(northernPack.Prefabs).Concat(mechPack.Prefabs).Concat(undeadPack.Prefabs).Concat(corruptedPack.Prefabs).Where(p=>p).Distinct();
        foreach(var prefab in all){var model=prefab.GetComponent<PcModel>();Check(model&&!model.reforged&&model.godotHeight==0&&model.clips.Length>0&&model.clips.All(c=>c),"Classic animated prefab "+prefab.name);foreach(var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(var mat in renderer.sharedMaterials)Check(mat&&mat.shader.isSupported&&mat.shader.name=="CastleFight/NativeUnit"&&mat.mainTexture&&mat.GetFloat("_HDPresent")==0,"Classic textured surface "+mat.name);}
        for(int race=0;race<9;race++) {
            MainMenu();raceChoice=race;botRaceChoice=race;startGold=1000;yield return Action("main.play");yield return Action("setup.start");testHoldAuthority=true;
            foreach(var h in match.Huts)h.paused=true;
            var source=(JArray)JObject.Parse(RaceCatalog(race))["lines"];int placed=0;
            foreach(var line in source) {
                int kind=match.Kind((string)line["building"]);var at=new Vector2(-168+(placed%6)*6,26+(placed/6)*7);placed++;
                if(!string.IsNullOrEmpty((string)line["unit"])) {var u=match.Spawn(0,kind,at+Vector2.up*3);u.onRoad=true;u.cooldown=10000;}
                if((string)line["category"]!="summon")match.Huts.Add(new HumanMatch.Hut{id=900000+placed,team=0,kind=kind,position=at,paid=true,paused=true,hp=5000,maxHp=5000});
            }
            RenderMatch();Check(builders.All(b=>b&&!b.reforged),"Both builders use classic art race="+race);
            Check(units.Values.All(m=>!m.reforged)&&huts.Values.All(m=>!m.reforged),"All visible units and buildings use classic art race="+race);
            selectedUnit=match.Units.FirstOrDefault(u=>u.hp>0)?.id??-1;builderSelected=false;selectedHut=-1;controls.FocusWorld(World(new Vector2(-154,36)),46);
            yield return new WaitForSecondsRealtime(.4f);yield return Shot(folder,"classic-race-"+race);
            Check(reforgedFx.Count==0&&reforgedIcons==null,"No Reforged effects or icons loaded race="+race);
        }
        var result=new JObject {["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["build"]=Version};File.WriteAllText(Path.Combine(folder,"classic-verification.json"),result.ToString());
        Debug.Log("CF_CLASSIC_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}
