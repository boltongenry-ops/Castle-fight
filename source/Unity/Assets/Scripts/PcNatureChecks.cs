using System.Collections;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    void NatureClear(){foreach(var u in match.Units){u.hp=0;u.deadAge=100;u.deathProcessed=true;u.pose="Death";}foreach(var h in match.Huts)h.paused=true;match.Shots.Clear();for(int i=0;i<8;i++)Tick(.05f);}
    HumanMatch.Unit NatureUnit(int team,string raw,Vector2 at){var u=match.Spawn(team,match.Kind(raw),at);u.onRoad=true;u.cooldown=10000;return u;}
    IEnumerator ExerciseNatureOnly() {
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"nature-validation"));Directory.CreateDirectory(folder);
        MainMenu();botRaceChoice=0;yield return Action("main.play");yield return Action("setup.nature");yield return Shot(folder,"01-nature-selection");
        Check(raceChoice==3&&naturePack&&botRaceChoice==0,"Nature first pack can be selected against Humans");
        startGold=1000;yield return Action("setup.start");testHoldAuthority=true;RenderMatch();
        Check(match.TeamNature[0]&&!match.TeamNature[1]&&match.Aspects[0]==-1,"Nature match keeps the race identity and starts with aspects off in classic rules");
        Check(builders[0].raw=="h01A"&&!builders[0].reforged&&builders[1].raw=="h00C","Nature uses classic Ent and opponent keeps Human builder");
        controls.Focus("river");yield return Action("hud.builder");Check(builderSelected&&CommandIcon(0)==HdIcon(naturePack.builderIcon),"Ent portrait shortcut selects and focuses the worker");
        Check(match.BaseKinds.Length==12&&match.PurchaseError(0,match.Kind("h000"))=="Постройка другой расы","Nature first pack exposes twelve base constructions and rejects enemy buildings");
        match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;
        var eggs=QaBuild(match.Kind("h002"),7,15);RenderMatch();
        Check(eggs.paid&&eggs.spent==170&&match.Wood[0]>=100000&&huts[eggs.id].raw=="h002","Ent physically completes source egg sack for 170 gold with wood reward");
        eggs.spawnLeft=.01f;int before=match.ArmyCount(0);Tick(.05f);Check(match.ArmyCount(0)==before+2,"Spider production creates the source pair");
        QaSelect(eggs);yield return Action("upgrade.h00Q");RenderMatch();Check(eggs.spent==370&&Mathf.Abs(eggs.spawnLeft-33.5f)<.01f&&huts[eggs.id].raw=="h00Q","Giant spider upgrade costs 200 with cumulative production interval");
        QaSelect(eggs);yield return Action("upgrade.h00R");RenderMatch();Check(eggs.spent==580&&Mathf.Abs(eggs.spawnLeft-44)<.01f&&huts[eggs.id].raw=="h00R","Brood mother upgrade costs 210 and updates the original model");
        var lodge=QaBuild(match.Kind("h00Y"),13,15);QaSelect(lodge);yield return Action("upgrade.h012");RenderMatch();Check(lodge.spent==400&&huts[lodge.id].raw=="h012"&&Mathf.Abs(lodge.spawnLeft-35)<.01f,"Elder shaman upgrade uses original cumulative cost and timer");
        var roost=QaBuild(match.Kind("h027"),19,15);Check(roost.paid&&roost.spent==310,"Emerald dragon roost costs 310 gold");
        NatureClear();string[] raws={"h002","h00Q","h00R","h00Y","h012","h027","n00T"};
        for(int i=0;i<raws.Length;i++)NatureUnit(0,raws[i],new Vector2(-160+(i%4)*5,32+(i/4)*7));
        RenderMatch();builderSelected=false;selectedHut=-1;selectedUnit=match.Units.Last().id;controls.FocusWorld(World(new Vector2(-152,35)),36);yield return new WaitForSecondsRealtime(.3f);yield return Shot(folder,"02-nature-classic");
        foreach(var raw in raws){var unit=match.Units.Last(u=>u.hp>0&&u.kind==match.Kind(raw));var view=units[unit.id];Check(!view.reforged&&view.raw==match.Lines[unit.kind].unit&&view.clips.Any(c=>c.name.StartsWith("Walk"))&&view.clips.Any(c=>c.name.StartsWith("Attack")),"Textured animated classic actor "+raw);Check(view.godotHeight==0&&view.BodyBounds().size.y>.05f,"Classic actor retains source geometry "+raw);}
        NatureClear();for(int i=0;i<4;i++)NatureUnit(0,new[]{"h002","h00Q","h00R","n00T"}[i],new Vector2(-156+i*3,32));RenderMatch();selectedUnit=match.Units.Last(u=>u.hp>0&&match.Lines[u.kind].unit=="n01W").id;controls.FocusWorld(World(new Vector2(-151,32)),17);yield return new WaitForSecondsRealtime(.25f);yield return Shot(folder,"04-nature-spiders");
        Check(AbilityArt("A0AR")&&AbilityArt("A0AV")&&AbilityArt("A0C1")&&AbilityArt("A0C3")&&AbilityArt("A0C9"),"Imported source icons are available for Nature abilities and armor debuff");
        NatureClear();var giant=NatureUnit(0,"h00Q",Vector2.zero);match.DamageUnit(giant,100000,1,"chaos",true,true);Tick(.05f);
        Check(match.Units.Count(u=>u.hp>0&&u.team==0&&match.Lines[u.kind].unit=="n00T")==2,"Giant spider death queues two forest spiders without modifying active iteration");
        NatureClear();var mother=NatureUnit(0,"h00R",Vector2.zero);match.DamageUnit(mother,100000,1,"chaos",true,true);Tick(.05f);
        Check(match.Units.Count(u=>u.hp>0&&u.team==0&&match.Lines[u.kind].unit=="n01N")==1,"Brood mother death creates giant spider through production unit mapping");
        NatureClear();mother=NatureUnit(0,"h00R",Vector2.zero);var host=NatureUnit(1,"h000",Vector2.right*3);mother.mana=mother.maxMana;
        Check(match.Cast(mother,"A0AV",host.id)&&host.effects.Any(e=>e.id=="A0AV"),"Brood mother spends mana to attach parasite to organic enemy");
        match.DamageUnit(host,100000,0,"chaos",true,true);Tick(.05f);Check(match.Units.Count(u=>u.hp>0&&u.team==0&&match.Lines[u.kind].unit=="n00T")==2,"Parasite death spawns two forest spiders for caster team");
        NatureClear();giant=NatureUnit(0,"h00Q",Vector2.zero);var flyer=NatureUnit(1,"h027",Vector2.right*3);flyer.spellResist=1;
        Check(match.Flying(flyer)&&match.Cast(giant,"A0AR",flyer.id)&&match.Flying(flyer)&&match.Shots.Any(s=>s.effectId=="A0AR"&&!s.hit),"Web launches a projectile and does not ground before impact");
        giant.stun=100;flyer.stun=100;for(int i=0;i<8;i++)Tick(.05f);
        Check(!match.Flying(flyer)&&match.Rooted(flyer)&&flyer.effects.Any(e=>e.id=="A0AR"),"Physical web grounds magic immune flyer on impact");
        flyer.effects.First(e=>e.id=="A0AR").left=.05f;Tick(.05f);Check(match.Flying(flyer)&&!match.Rooted(flyer),"Web expiration restores flight and movement");
        Check(!match.Cast(giant,"A0AR",NatureUnit(1,"h000",Vector2.right).id),"Web rejects ground targets");
        NatureClear();var shaman=NatureUnit(0,"h00Y",Vector2.zero);var patient=NatureUnit(0,"h000",Vector2.right);patient.maxHp=2000;patient.hp=500;shaman.mana=100;
        Check(match.Cast(shaman,"A0C0",patient.id)&&shaman.mana==50&&Mathf.Abs(patient.hp-500)<.01f,"Rejuvenation charges 50 mana without instant heal");
        Check(!match.Cast(shaman,"A0C0",patient.id)&&Mathf.Abs(match.Stat(patient,"damage"))<.001f,"Rejuvenation neither duplicates nor adds weapon damage");
        shaman.stun=100;patient.stun=100;float hp=patient.hp;for(int i=0;i<100;i++)Tick(.05f);float natural=match.Lines[patient.kind].regen*5;
        Check(Mathf.Abs((patient.hp-hp-natural)-100)<.15f,"Rejuvenation restores 100 HP in five of its ten seconds");
        NatureClear();var elder=NatureUnit(0,"h012",Vector2.zero);patient=NatureUnit(0,"h000",Vector2.right);patient.maxHp=2000;patient.hp=500;elder.mana=100;
        Check(match.Cast(elder,"A0C1",patient.id)&&patient.effects.Any(e=>e.id=="A0AX"&&e.power==400),"Elder uses the separate 400 HP rejuvenation status");
        NatureClear();elder=NatureUnit(0,"h012",Vector2.left);elder.cooldown=0;var caster=NatureUnit(1,"h002",Vector2.right);caster.maxHp=caster.hp=100000;caster.maxMana=caster.mana=80;caster.manaRegen=0;caster.stun=100;
        int feedback=match.FeedbackHits;for(int i=0;i<1000&&match.FeedbackHits==feedback;i++)Tick(.05f);
        Check(match.FeedbackHits>feedback&&caster.mana==0,"Elder mana burn procs on an actual hit and drains only available 80 mana");
        NatureClear();var dragon=NatureUnit(0,"h027",Vector2.left);dragon.cooldown=0;var victim=NatureUnit(1,"h000",Vector2.right);victim.maxHp=victim.hp=100000;victim.stun=100;
        for(int i=0;i<400;i++)Tick(.05f);
        Check(victim.abilityLevels.TryGetValue("A0C9",out int rank)&&rank==3&&match.Stat(victim,"armor")==-6&&victim.effects.Count(e=>e.id=="A0C9")==1,"Emerald dragon actual missiles cap permanent armor erosion at -6");
        RenderMatch();selectedUnit=victim.id;controls.FocusWorld(World(victim.position),30);yield return new WaitForSecondsRealtime(.25f);yield return Shot(folder,"03-nature-combat");
        NatureClear();var enemyHut=new HumanMatch.Hut{id=999999,team=1,kind=match.Kind("h000"),position=Vector2.right*2,paid=true,paused=true,hp=100000,maxHp=100000};match.Huts.Add(enemyHut);dragon=NatureUnit(0,"h027",Vector2.left*2);dragon.cooldown=0;dragon.skirmish=true;float armor=match.BuildingArmor(enemyHut);for(int i=0;i<400;i++)Tick(.05f);Check(enemyHut.dragonArmorRank==3&&Mathf.Abs(match.BuildingArmor(enemyHut)-(armor-6))<.001f,"Emerald dragon armor erosion also caps at -6 on buildings");match.Huts.Remove(enemyHut);
        NatureClear();var spider=NatureUnit(0,"h002",Vector2.left);spider.cooldown=0;victim=NatureUnit(1,"h000",Vector2.right);victim.hp=victim.maxHp=100000;victim.stun=100;
        for(int i=0;i<100&&!victim.effects.Any(e=>e.id=="A0AO");i++)Tick(.05f);
        Check(victim.effects.Any(e=>e.id=="A0AO"&&e.config.Value<float>("dps")==5&&e.haste==-.1f),"Black spider applies source poison damage and attack speed penalty on hit");
        // Буйство природы / Живой лес — Godot aspects.gd.
        MainMenu();raceChoice=3;botRaceChoice=0;aspectsOn=true;
        yield return Action("main.play");yield return Action("setup.nature");yield return Action("setup.start");testHoldAuthority=true;
        Check(page==Page.Match&&aspectWaiting&&match.Aspects[0]<0&&match.Aspects[1]>=0,"Nature aspect picker waits for the player and the bot already chose");
        yield return Action("aspect.choose0");Check(!aspectWaiting&&match.Aspects[0]==0,"Nature resilience aspect applies to the round");
        match.Gold[0]=1000;match.Wood[0]=0;
        Check(match.GoldPrice(0,match.Kind("h002"))==187&&match.GoldPrice(0,match.Kind("h008"))==350,"Resilience marks up producer gold by 10% and leaves other buildings alone");
        var spiderAspect=NatureUnit(0,"h002",new Vector2(-160,30));
        Check(Mathf.Abs(spiderAspect.spellResist-.2f)<.001f,"Resilience gives fighters 20 points of magic resistance");
        var nestAspect=QaBuild(match.Kind("h002"),7,15);
        Check(nestAspect.paid&&match.Gold[0]==1000-187&&Mathf.Abs(nestAspect.income-3.4f)<.001f,"Marked-up producer charges 187 gold but keeps its 3.4 income");
        MainMenu();raceChoice=3;botRaceChoice=0;aspectsOn=true;
        yield return Action("main.play");yield return Action("setup.nature");yield return Action("setup.start");testHoldAuthority=true;
        yield return Action("aspect.choose1");Check(match.Aspects[0]==1,"Nature forest aspect applies to the round");
        match.Gold[0]=100000;match.Wood[0]=100000;
        var nestLeft=QaBuild(match.Kind("h002"),7,15);var nestRight=QaBuild(match.Kind("h00Y"),13,15);
        // Both buildings must be inside the 450-unit aura of the same ally.
        var between=(nestLeft.position+nestRight.position)*.5f;
        var healed=NatureUnit(0,"h002",between);healed.hp=100;healed.maxHp=1000;
        var outside=NatureUnit(0,"h002",nestLeft.position+new Vector2(60,0));outside.hp=100;outside.maxHp=1000;
        match.TickNatureForest(1f);
        float forestHeal=healed.hp-100,controlHeal=outside.hp-100;
        Check(Mathf.Abs(forestHeal-4f)<.05f&&controlHeal==0f,"Two working buildings heal a nearby ally at 4 HP/s");
        QaBuild(match.Kind("h00Y"),9,15);QaBuild(match.Kind("h002"),11,15);
        healed.position=between;healed.previous=between;healed.hp=100;
        match.TickNatureForest(1f);
        Check(Mathf.Abs(healed.hp-100-6f)<.05f,"The forest aura caps at 6 HP/s however many buildings stand around measured="+(healed.hp-100).ToString("F2"));
        var chest=QaBuild(match.Kind("h008"),19,15);
        Check(Mathf.Abs(match.BuildingCooldown(chest,2f)-1.6f)<.001f,"Forest shortens special building spell cooldowns by 20%");
        Check(Mathf.Abs(match.BuildingManaRegen(chest,.8f)-1f)<.001f,"Forest raises special building mana regeneration by 25%");
        aspectsOn=false;
        MainMenu();botRaceChoice=3;raceChoice=0;yield return Action("main.play");yield return Action("setup.start");testHoldAuthority=true;for(int i=0;i<160;i++)Tick(.05f);RenderMatch();
        Check(match.TeamNature[1]&&builders[1].raw=="h01A"&&match.Huts.Any(h=>h.team==1&&h.paid&&match.Lines[h.kind].building=="h002"),"Nature opponent uses Ent and builds its own spider producer");
        yield return NatureCompletionChecks(folder);
        var result=new JObject {["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["build"]=Version};File.WriteAllText(Path.Combine(folder,"nature-verification.json"),result.ToString());
        Debug.Log("CF_NATURE_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}