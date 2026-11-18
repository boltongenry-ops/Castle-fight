using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    readonly Dictionary<string,Texture2D> combatTypeIcons=new Dictionary<string,Texture2D>();
    // Exact native infocard names. WC3 aliases Normal armor to Small and has no
    // separate spell-damage infocard: the Magic art is paired with its real label.
    static string CombatIconKey(bool armor,string type){
        string native=type;
        if(armor){if(type=="light"||type=="normal")native="small";else if(type=="heavy")native="large";}
        else {if(type=="normal")native="melee";else if(type=="pierce")native="piercing";else if(type=="spells")native="magic";}
        return "CombatType/"+(armor?"armor-":"attack-")+native;
    }
    Texture2D CombatIcon(bool armor,string type){
        string key=CombatIconKey(armor,type);
        var hd=HdModels.Icon(ReforgedModelPack.IconAddress("Combat-"+key.Substring("CombatType/".Length)));
        if(hd)return hd;
        if(!combatTypeIcons.TryGetValue(key,out var icon)){icon=ContentResources.Load<Texture2D>(key);if(icon)combatTypeIcons[key]=icon;}
        return icon;
    }
    HumanMatch.Weapon[] CardWeapons(HumanMatch.Unit unit)=>unit.elementalWeapons??match.Lines[unit.kind].weapons;
    static string[] CardAttacks(IEnumerable<HumanMatch.Weapon> weapons)=>weapons.Select(w=>w.attack??"normal").Distinct().ToArray();
    static string ShortAttack(string type)=>type=="normal"?"Обыч.":type=="pierce"?"Кол.":type=="siege"?"Осад.":type=="magic"?"Маг.":type=="hero"?"Гер.":type=="spells"?"Закл.":"Хаос";
    void DrawCombatStat(float x,bool armor,string[] types,string number,string tip){
        var rect=new Rect(x,623,36,36);
        if(types.Length==1){var icon=CombatIcon(armor,types[0]);if(icon)GUI.DrawTexture(rect,icon,ScaleMode.ScaleToFit);}
        else for(int i=0;i<Math.Min(types.Length,2);i++){var icon=CombatIcon(armor,types[i]);if(icon)GUI.DrawTexture(new Rect(x+i*18,632,18,18),icon,ScaleMode.ScaleToFit);}
        string label=types.Length==0?"Не атакует":armor?ArmorLabel(types[0]):types.Length==1?AttackLabel(types[0]):string.Join(" / ",types.Select(ShortAttack));
        GUI.Label(new Rect(x+41,621,105,20),number,hudSmall);
        GUI.Label(new Rect(x+41,641,105,18),label,new GUIStyle(hudSmall){fontSize=11});
        if(rect.Contains(HudPointer)&&types.Length>0){hudTip=tip;hudTipTable=true;Border(rect,new Color(1,.95f,.64f),2);}
    }
    void DrawBuildingCombat(HumanMatch.Hut hut){
        var line=match.Lines[hut.kind];var weapons=line.buildingWeapons;var attacks=CardAttacks(weapons);
        DrawCombatStat(504,false,attacks,attacks.Length==0?"": "Урон: "+NetworkCardStat(hut.id,"damage",weapons[0].damage,true).ToString("F0"),AttackTable(weapons));
        string armor=(string)line.buildingStats["armor_type"]??"fortified";
        DrawCombatStat(650,true,new[]{armor},"Броня: "+NetworkCardStat(hut.id,"armor",match.BuildingArmor(hut),true).ToString("F0"),ArmorTable(armor));
    }
}
}
