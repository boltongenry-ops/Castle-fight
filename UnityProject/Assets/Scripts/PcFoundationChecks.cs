using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseFoundation(string folder) {
        NewMatch();testHoldAuthority=true;match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;
        var producer=QaBuild(match.Kind("h000"),7,15);
        var tower=QaBuild(match.Kind("h006"),11,15);
        var horn=QaBuild(match.Kind("h010"),15,15);
        QaBuild(match.Kind("h008"),19,15);
        var enemy=match.Spawn(1,0,tower.position+Vector2.right*8);enemy.onRoad=true;enemy.hp=enemy.maxHp=100000;enemy.cooldown=10000;
        var ally=match.Spawn(0,0,horn.position+Vector2.one);ally.onRoad=true;ally.cooldown=10000;
        foreach(var line in match.Lines)line.speed=0;
        horn.mana=7;producer.spawnLeft=7;float income=match.Income(0);
        FocusBuilder();buildMenu=false;yield return Action("builder.pause-all");
        for(int i=0;i<40;i++)Tick(.05f);
        Check(producer.paused&&producer.spawnLeft==7&&!tower.paused&&!horn.paused,"global pause freezes producer only in the portable player");
        Check(tower.attackSerial>0&&match.HolyCasts>0&&match.Income(0)==income,"defense support and income continue while all training is paused");
        QaSelect(producer);RenderMatch();controls.FocusWorld(World(new Vector2(-150,32)),65);
        yield return Shot(folder,"37-production-pause");
        FocusBuilder();yield return Action("builder.resume-all");Check(!producer.paused,"global resume works through the worker card");

        match.MoveBuilder(0,new Vector2(-130,25));
        Check(match.TryBuild(0,match.Kind("h05G"),22,19,out _),"legendary construction can reserve a plot and resources in player");
        var unfinished=match.Huts.Last();match.Builders[0].position=match.Builders[0].previous=unfinished.position-new Vector2(0,2.4f);Tick(.05f);
        match.MoveBuilder(0,match.Builders[0].position+Vector2.up*3);
        QaSelect(unfinished);RenderMatch();controls.FocusWorld(World(unfinished.position),37);
        Check(BuildingStatus(unfinished).StartsWith("Стройка прервана")&&match.ReservedLegendary(0)==1,"interrupted construction card identifies its state and keeps legendary reservation");
        Check(GoldTooltip().Contains("Резерв очереди: 550")&&GoldTooltip().Contains("Доход:")&&GoldTooltip().Contains("Начисление через:"),"gold tooltip exposes real reservation income and next payment");
        yield return Shot(folder,"38-interrupted-construction");
        yield return Action("building.resume");Check(match.Builders[0].jobs.Contains(unfinished.id),"resume button restores interrupted construction to the worker queue");QaSelect(unfinished);
        var stopped=match.Builders[0].position;yield return Action("building.cancel");
        for(int i=0;i<10;i++)Tick(.05f);
        Check(!match.Huts.Contains(unfinished)&&match.ReservedGold(0)==0&&match.ReservedWood(0)==0&&match.ReservedLegendary(0)==0,"cancel button releases plot gold wood and legendary reservation");
        Check(match.Builders[0].position==stopped&&selectedHut==-1,"cancel button leaves the worker stopped and clears the removed selection");

        producer.spawnLeft=0;
        for(int i=0;i<HumanMatch.ArmyLimit-2;i++){var u=match.Spawn(0,0,Vector2.zero);u.onRoad=true;u.cooldown=10000;}
        while(match.ArmyCount(0)<HumanMatch.ArmyLimit){var u=match.Spawn(0,0,Vector2.zero);u.onRoad=true;u.cooldown=10000;}
        Check(BuildingStatus(producer)=="Ждёт места в армии (160/160)","production card explains waiting for army capacity instead of a frozen zero-second timer");

        NewMatch();testHoldAuthority=true;match.Gold[1]=0;
        var squad=Enumerable.Range(6,4).Select(x=>match.Spawn(0,0,HumanMatch.Plot(0,x,12))).ToArray();
        float closest=float.MaxValue;
        for(int i=0;i<45;i++){Tick(.05f);foreach(var u in squad)closest=Mathf.Min(closest,Vector2.Distance(u.position,HumanMatch.CastlePosition(0)));}
        RenderMatch();builderSelected=false;selectedUnit=squad[0].id;controls.FocusWorld(World(new Vector2(-164,7)),43);
        Check(closest>match.CastleCollision&&squad.All(u=>u.position.x>-178),"newly spawned ground units walk beside their own keep in the player");
        yield return Shot(folder,"39-keep-exit-route");
        for(int i=0;i<500;i++)Tick(.05f);
        Check(squad.All(u=>u.onRoad&&u.position.x>-2016f/30),"every routed unit exits through the own-base arch");
        NewMatch();testHoldAuthority=true;match.Gold[1]=0;match.CastleHp[0]=4000;
        moveOrder=true;targetedSpell="blink";RepairOrder();
        Check(match.Builders[0].repairing&&!moveOrder&&targetedSpell==null&&!buildMenu,"repair replaces pending movement and spell targeting");
        Debug.Log("CF_FOUNDATION_PLAYER_PASS pause=production-only cancel=all-reservations routes=keep-and-arch income=countdown");
        testHoldAuthority=false;
    }
}
}
