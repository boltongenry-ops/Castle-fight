using System.Collections;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseHudStats(string folder) {
        MainMenu();yield return Action("main.play");yield return Action("setup.human");options.hdTextures=true;yield return Action("setup.start");testHoldAuthority=true;match.Gold[1]=0;
        var u=match.Spawn(0,match.Kind("h000"),new Vector2(-160,35));u.onRoad=true;u.cooldown=1000;u.pose="Stand";
        RenderMatch();builderSelected=false;selectedHut=-1;selectedUnit=u.id;controls.FocusWorld(World(u.position),35);
        var l=match.Lines[u.kind];
        Check(AttackTable(l).Contains("Средняя:  175%")&&AttackTable(l).Contains("Укреплённая:  50%"),"attack tooltip shows source modifiers against armor types");
        Check(ArmorTable(l).Contains("Колющая:  70%")&&ArmorTable(l).Contains("Магическая:  175%"),"armor tooltip shows source modifiers against attack types");
        testHudPointer=new Vector2(520,640);yield return null;yield return Shot(folder,"70-attack-table");Check(hudTipTable&&hudTip!=null&&hudTip.Contains("Урон по типу брони"),"attack icon hover opens attack modifier table");
        testHudPointer=new Vector2(666,640);yield return null;yield return Shot(folder,"71-armor-table");Check(hudTipTable&&hudTip!=null&&hudTip.Contains("Получаемый урон"),"armor icon hover opens defense modifier table");testHudPointer=null;
        yield return Action("hud.score-toggle");Check(!scoreExpanded&&!HudContains(new Vector2(1000,100))&&HudContains(new Vector2(1000,45)),"collapsed scoreboard releases play area");yield return Shot(folder,"72-score-collapsed");
        yield return Action("hud.score-toggle");Check(scoreExpanded&&HudContains(new Vector2(1000,100)),"scoreboard expands and captures its area");
        var original=buildingIcons[0];Check(HdIcon(original)!=original&&HdIcon(original).width>=128&&HdIcon(Extra("attack"))!=null,"Reforged building and command icons load");
        Check(HdIcon(itemIcons[0])!=itemIcons[0]&&HdIcon(abilityIcons[0])!=abilityIcons[0],"Reforged item and ability icons load");
        FocusBuilder();buildMenu=true;yield return Shot(folder,"73-reforged-icons");options.hdTextures=true;Check(HdIcon(original)!=original,"Reforged icons stay active");options.hdTextures=true;
        Debug.Log("CF_HUD_STATS_PASS");
    }
}
}
