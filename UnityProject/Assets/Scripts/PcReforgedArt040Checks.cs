using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseReforgedArt040(){
        string folder=BattleLab.Arg("--pc-results");
        Directory.CreateDirectory(folder);
        string relay=BattleLab.Arg("--pc-reforged-fixture");
        Check(!HdModels.Active&&!HdArt040,"Classic is the initial style");
        yield return HdModels.TransferArt(relay);
        Check(!HdModels.ArtInstalled,"Art does not download without opt-in");
        HdModels.Draft=true;
        HdModels.Select(true);
        yield return HdModels.Transfer(true,relay);
        Check(HdModels.Active&&!HdArt040,"Cached model pack still works by itself");
        yield return HdModels.TransferArt(relay);
        Check(HdArt040&&HdModels.ArtInstalled&&HdModels.Bytes==HdModels.Total,"Art pack verified and mounted separately");
        Check(HdModels.Asset<Shader>("fx-040-shader")?.isSupported==true,"Billboard shader is supported in packaged player");

        const int elemental=9;
        raceChoice=botRaceChoice=elemental;
        matchMode=0;
        NewMatch();
        testHoldAuthority=true;
        options.fogOfWar=false;
        var roster=match.Lines;
        var builder=match.Builders[0];
        var fire=match.Spawn(0,System.Array.FindIndex(roster,line=>line.unit=="h02Z"),builder.position+new Vector2(4,0));
        var greater=match.Spawn(0,System.Array.FindIndex(roster,line=>line.unit=="h030"),builder.position+new Vector2(8,0));
        controls.FocusWorld(World(builder.position+new Vector2(4,0)),28);
        RenderMatch();
        Check(builders[0].reforged&&units[fire.id].reforged&&units[greater.id].reforged,"Elemental builder and both Fire Elementals instantiate in HD");
        var special=HdModels.ArtEffects["specialActors"];
        foreach(string raw in new[]{"h051","h02Z","h030"}){
            string key=(string)special[raw]["idle"];
            Check(reforgedFx.TryGetValue(key,out var art)&&art.info.HalfSize>0&&art.texture&&art.material.shader.isSupported,
                "Live renderer loads actual native emitter atlas: "+raw);
        }
        builder.pose="Work";
        RenderMatch();
        string work=(string)special["h051"]["work"];
        Check(work!=(string)special["h051"]["idle"]&&reforgedFx.ContainsKey(work),"Builder work has a distinct original emitter");
        builder.pose="Stand";
        var wispIcon=HdModels.Icon(ReforgedModelPack.IconAddress("ElementalBuilder"));
        Check(wispIcon&&CommandIcon(0)==wispIcon,"Elemental builder HUD uses the Reforged Wisp icon");
        var storm=HdModels.Icon(ReforgedModelPack.IconAddress("Aspect-elemental_storm"));
        var resonance=HdModels.Icon(ReforgedModelPack.IconAddress("Aspect-elemental_resonance"));
        Check(storm&&resonance&&storm!=resonance,"Elemental aspects use separate source icons");
        Check(Enumerable.Range(0,AspectCount(elemental)).All(index=>AspectIcon(elemental,index)&&AspectIcon(elemental,index)==
            HdModels.Icon(ReforgedModelPack.IconAddress("Aspect-"+AspectId(elemental,index)))),"Aspect card resolves both HD icons");
        Check(AbilityArt("A06R")==HdModels.Icon(ReforgedModelPack.IconAddress("Ability-A06R"))&&AbilityArt("A06R"),"Ability card resolves the indexed HD icon");

        var paths=(JObject)HdModels.ArtEffects["missilePaths"];
        var entries=paths.Properties().Select(property=>(string)property.Value).Distinct().ToArray();
        var sheets=entries.Where(key=>HdLayers040(key)!=null).ToArray();
        Check(sheets.Length>=45&&sheets.All(key=>HdLayers040(key).Any(layer=>EnsureHdSheet040((string)layer["key"]))),
            "Each indexed native missile atlas resolves its source art");
        var geometry=entries.Where(key=>HdGeometry040(key,0)).ToArray();
        Check(geometry.Length>=8&&geometry.All(key=>HdGeometry040(key,1)?.GetComponent<ClassicProjectileModel>()!=null),
            "Geometry-only missiles have a prefab for both teams");
        var distinct=sheets.Select(key=>(string)HdLayers040(key)[0]["key"]).Distinct().Count();
        Check(distinct>=30,"Missiles use distinct effect assets");
        string path=paths.Properties().First(row=>HdLayers040((string)row.Value)!=null&&
            ((string)row.Value).StartsWith("fx_")&&row.Name.Contains("missile")).Name;
        var weapon=new HumanMatch.Weapon{missile=path};
        var shot=new HumanMatch.Shot{id=750001,kind=3,line=-1,team=0,weapon=weapon,start=builder.position,
            end=builder.position+Vector2.right*10,duration=1,age=.4f,startHeight=1,endHeight=1};
        var view=new GameObject("QA interpolated missile").AddComponent<PcShotView>();
        view.Setup(3,shellMaterial,tracerMaterial,impactMaterial);
        view.Draw(shot,World,.025f,false,null,DrawHdShot040);
        var first=view.transform.position;
        view.Draw(shot,World,0,false,null,DrawHdShot040);
        Check(Vector3.Distance(first,view.transform.position)>.01f&&!view.VisualHit,"HD missile uses the same interpolated flight position");
        Destroy(view.gameObject);
        yield return Shot(folder,"01-elemental-reforged");

        HdModels.Select(false);
        ReleaseOptionalVortex();
        RenderMatch();
        Check(!HdArt040&&HdModels.ArtInstalled&&!builders[0].reforged&&!units[fire.id].reforged&&reforgedFx.Count==0,
            "Classic restores actor art, clears HD effects and retains the download");
        var wisp=builders[0].GetComponent<ClassicWispBody040>();
        Check(wisp&&wisp.FacingPlanes==30&&wisp.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.subMeshCount==6,
            "Classic Wisp restores the source geosets and billboard nodes");
        Check(builders[0].GetComponentsInChildren<ParticleSystemRenderer>().All(renderer=>!renderer.forceRenderingOff),
            "Classic particle renderers remain visible");
        builders[0].Play("Death");
        Check(wisp.CurrentClip=="Death","Classic Wisp switches to the source death animation");
        builders[0].Play("Stand");
        yield return Shot(folder,"02-elemental-classic");
        HdModels.Select(true);
        yield return HdModels.OpenCached();
        Check(HdArt040&&HdModels.ArtInstalled,"The verified art pack reopens entirely from disk");
        var cache=Path.Combine(Path.GetDirectoryName(settingsPath),"reforged-models-v1");
        string unrelated=Path.Combine(cache,"keep.txt");
        File.WriteAllText(unrelated,"fixture");
        string partial=Path.Combine(cache,new string('a',64)+".cfb.part");
        File.WriteAllText(partial,"incomplete");
        page=Page.Pause;
        OpenSettings();
        yield return Action("settings.models");
        yield return Action("models.delete");
        yield return Action("models.keep");
        Check(HdArt040&&File.Exists(partial),"Canceling deletion leaves active art and partial downloads intact");
        yield return Action("models.delete");
        yield return Action("models.delete.confirm");
        Check(!HdArt040&&!HdModels.Active&&!HdModels.HasCache&&!File.Exists(partial)&&File.Exists(unrelated),
            "Settings delete the active optional packs and partial data while preserving other files");
        yield return HdModels.Transfer(true,relay);
        yield return HdModels.Download(true,BattleLab.Arg("--pc-reforged-art-source"));
        Check(HdArt040&&HdModels.ArtInstalled,"Deleted packs can be reinstalled through the normal download route");
        yield return Shot(folder,"03-model-settings");
        File.WriteAllText(Path.Combine(folder,"art040-verification.json"),new JObject{
            ["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,
            ["weaponPaths"]=entries.Length,["weaponAtlases"]=sheets.Length,["weaponGeometry"]=geometry.Length
        }.ToString());
        Debug.Log("CF_REFORGED_ART040_TEST_RESULT failures="+testFailures+" checks="+testChecks.Count);
        Application.Quit(testFailures==0?0:1);
    }
}
}
