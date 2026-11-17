using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseWorkshop038(){
        string output=BattleLab.Arg("--pc-results");
        Directory.CreateDirectory(output);
        var catalog=(JArray)progression.Catalog["items"];
        Check(catalog.Count==27&&catalog.Select(item=>(string)item["id"]).Distinct().Count()==27,"27 distinct cosmetics and existing purchases");
        Check(catalog.Count(item=>(string)item["kind"]=="aura")==8&&catalog.Count(item=>(string)item["kind"]=="effect")==6&&
            catalog.Count(item=>(string)item["kind"]=="skin")==8,"Aura, effects, and skin catalog counts");
        Check(progression.Coins==0&&!progression.Buy("skin_emerald",out _),"Fresh wallet rejects unaffordable items");
        foreach(var challenge in ((JArray)progression.Catalog["challenges"]).Take(15))progression.Complete((string)challenge["id"],5);

        MainMenu();raceChoice=0;
        yield return Action("main.challenges");
        yield return Action("progress.shop");
        float limit=Time.realtimeSinceStartup+10;
        while(Workshop.loading&&Time.realtimeSinceStartup<limit)yield return null;
        yield return Action("workshop.pick.aura_frost");
        yield return new WaitForSecondsRealtime(.45f);
        Check(Workshop.forge&&Workshop.mannequin&&Workshop.image&&Workshop.fittingEffects.HasAura,"Forge illustration and animated aura fitting room");
        Check(Workshop.fittingEffects.ParticleCount>0&&!progression.Owned("aura_frost"),"Preview displays unowned aura without unlocking it");
        yield return Shot(output,"01-workshop-auras");
        int before=progression.Coins;
        yield return Action("progress.item.aura_frost");
        Check(!progression.Owned("aura_frost")&&progression.Coins==before,"First action only asks for purchase confirmation");
        yield return Action("progress.item.aura_frost");
        Check(progression.Owned("aura_frost")&&progression.Coins==before-180,"Confirmed aura purchase debits exactly its cost");
        yield return Action("progress.item.aura_frost");
        Check(progression.Equipped("aura")=="aura_frost","Equips aura after purchase");

        yield return Action("workshop.tab.1");
        yield return Action("workshop.pick.effect_hammer");
        yield return new WaitForSecondsRealtime(.25f);
        Check(Workshop.fittingEffects.BurstCount>0,"Construction effect demonstrates itself on the stand");
        yield return Action("progress.item.effect_hammer");
        yield return Action("progress.item.effect_hammer");
        yield return Action("progress.item.effect_hammer");
        yield return Shot(output,"02-workshop-effects");

        yield return Action("workshop.tab.2");
        yield return Action("workshop.pick.skin_emerald");
        yield return Action("workshop.race.next");
        Check(Workshop.mannequin&&Workshop.mannequin.raw=="h019","Race selector swaps the shop model to the Orc builder");
        Check(HasWorkshopCloth(Workshop.mannequin,"skin_emerald"),"New palette appears on the actual cloned skin materials");
        yield return Shot(output,"03-workshop-skins");
        yield return Action("progress.item.skin_emerald");
        yield return Action("progress.item.skin_emerald");
        yield return Action("progress.item.skin_emerald");
        yield return Action("workshop.collection");
        Check(WorkshopItems().Count==1&&Workshop.item=="skin_emerald","Collection filter contains only the purchased skin");
        yield return Action("workshop.collection");
        yield return Action("workshop.tab.4");
        yield return Action("progress.preview.meme");
        Check(showStickerPicker,"Original meme pack can still be previewed from its category");
        showStickerPicker=false;
        var reloaded=new PcProgressionStore(progression.Catalog,BattleLab.Arg("--pc-profile-file"));
        Check(reloaded.Equipped("aura")=="aura_frost"&&reloaded.Equipped("effect")=="effect_hammer"&&
            reloaded.Equipped("skin")=="skin_emerald","All three equipment slots survive reload");

        MainMenu();raceChoice=0;botRaceChoice=1;aspectsOn=false;
        NewMatch();testHoldAuthority=true;RenderMatch();
        Check(HasWorkshopCloth(builders[0],"skin_emerald")&&!HasWorkshopCloth(builders[1],"skin_emerald"),
            "Only the local builder changes appearance in a match");
        Check(Workshop.battleEffects.HasAura,"Equipped new aura displays in an actual bot match");
        match.Gold[0]=match.Wood[0]=100000;
        chosen=match.BaseKinds.First();
        Check(Place(6,15)&&chosen<0,"Ordinary placement still completes and closes single-use placement");
        Check(Workshop.battleEffects.BurstCount>0,"Successful placement emits the chosen cosmetic effect");
        controls.FocusWorld(World(match.Builders[0].position),26);
        yield return Shot(output,"04-workshop-in-battle");
        Check(progression.Equip("skin_emerald",false)&&progression.Equip("aura_frost",false),"Removing appearance keeps ownership");
        RenderMatch();
        Check(!HasWorkshopCloth(builders[0],"skin_emerald")&&!Workshop.battleEffects.HasAura,"Removed skin and aura revert in battle");

        var human=JObject.Parse(RaceCatalog(0));
        RunCastleCrowd038(human);
        yield return CheckSiegeChallenges038(output,human);
        var report=new JObject{
            ["status"]=testFailures==0?"PASS":"FAIL",["build"]=Version,["checks"]=testChecks,["failures"]=testFailures,
            ["items"]=catalog.Count,["challenges"]=((JArray)progression.Catalog["challenges"]).Count
        };
        File.WriteAllText(Path.Combine(output,"workshop-verification.json"),report.ToString());
        Debug.Log("CF_WORKSHOP038_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);
        Application.Quit(testFailures==0?0:1);
    }

    bool HasWorkshopCloth(PcModel model,string id){
        if(!model)return false;
        var color=WorkshopCatalog038.Find(id).Tint;
        return model.GetComponentsInChildren<SkinnedMeshRenderer>()
            .SelectMany(renderer=>renderer.sharedMaterials)
            .Any(material=>material&&material.HasProperty("_TeamColor")&&material.GetColor("_TeamColor")==color);
    }

    void RunCastleCrowd038(JObject human){
        for(int side=0;side<2;side++){
            var scene=new HumanMatch((JObject)human.DeepClone(),0,0);
            var castle=HumanMatch.CastlePosition(1-side);
            int footman=Array.FindIndex(scene.Lines,row=>row.unit=="hfoo");
            int paladin=Array.FindIndex(scene.Lines,row=>row.unit=="h03C");
            if(footman<0)footman=scene.Kind("h000");
            float facing=side==0?1:-1;
            var crowd=new HumanMatch.Unit[24];
            for(int i=0;i<crowd.Length;i++){
                var unit=scene.Spawn(side,i%3==0?paladin:footman,
                    castle+new Vector2(-facing*(10.2f+(i/6)*.89f),-.17f+(i%6)*.14f));
                unit.onRoad=true;
                crowd[i]=unit;
            }
            float startingHp=scene.CastleHp[1-side];
            for(int i=0;i<145&&!scene.Finished;i++)scene.Step(.05f);
            Check(scene.CastleHp[1-side]<startingHp&&crowd.Count(unit=>unit.attackSerial>0)>=22,
                "Dense mixed melee group reaches and damages the castle from team "+side);
            Check(crowd.Max(unit=>unit.position.y)-crowd.Min(unit=>unit.position.y)>2,
                "Siege holds multiple attack positions around the castle from team "+side);

            var step=new HumanMatch((JObject)human.DeepClone(),0,0);
            var one=step.Spawn(side,footman,castle-Vector2.right*facing*15);
            one.onRoad=true;one.pose="Attack";
            float reach=step.WeaponFor(one,null,true).range+step.CastleCollision;
            one.previous=one.position=castle-Vector2.right*facing*(reach+.65f);
            step.TickUnit(one,.05f);
            Check(one.attackSerial==0&&one.position!=one.previous&&one.pose=="Walk",
                "Out-of-range castle target advances instead of repeating an ineffective blow from team "+side);
        }
    }
}
}
