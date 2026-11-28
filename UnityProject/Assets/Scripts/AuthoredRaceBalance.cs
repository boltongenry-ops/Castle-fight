using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
internal static class AuthoredRaceBalance {
    static readonly Dictionary<string,int> Gold=new Dictionary<string,int>{
        ["c001"]=125,["c004"]=225,["c005"]=280,["c006"]=325,["c007"]=475,["c012"]=225,["c00A"]=470,
        ["pH01"]=205,["pS01"]=270,["pW01"]=280,["pL01"]=315,["pG01"]=300,
        ["pB06"]=100,["pB31"]=125,["pB32"]=145,["pB04"]=270,["pB05"]=215,["pB12"]=145,["pB13"]=375,["pB09"]=585,
        ["bH00"]=115,["bH01"]=150,["bH02"]=335,["bH03"]=165,["bH05"]=145,["bH07"]=220,["bH09"]=260,["bH11"]=245,["bH15"]=270
    };
    static readonly Dictionary<string,int> Wood=new Dictionary<string,int>{
        ["c00A"]=250,["pG01"]=420,["pB09"]=1050,["bH15"]=375
    };
    static readonly (string building,int hp,int impact,float cooldown,int fragment,int finale,string description)[] HandSiege={
        ("bH11",320,74,3.5f,14,0,"Обычные осколки наносят по 14 осадного урона в зоне попадания."),
        ("bH12",450,86,3.25f,16,0,"Ядовитые осколки наносят по 16 колющего урона и отравляют живые цели на 3 с по 7 урона в секунду."),
        ("bH13",460,98,3.5f,24,18,"Огненные осколки наносят по 24 магического урона. Последний удар наносит 42."),
        ("bH14",430,90,3.3f,17,0,"Проклятые осколки наносят по 17 урона хаосом и снижают урон обычного оружия на 20%. При прямом попадании цель получает все три удара, даже если вышла из зоны.")
    };

    static void ApplyHandSiege(JObject[] lines,JObject abilities){
        foreach(var profile in HandSiege){
            var line=lines.FirstOrDefault(row=>(string)row["building"]==profile.building);
            if(line==null)continue;
            var stats=line["stats"];
            var weapon=stats["weapons"][0];
            stats["hp"]=profile.hp;
            stats["damage"]=weapon["damage"]=profile.impact;
            stats["cooldown"]=weapon["cooldown"]=profile.cooldown;
            var ability=abilities["BH"+profile.building.Substring(2)];
            ability["fragment_damage"]=profile.fragment;
            ability["fragment_finale"]=profile.finale;
            ability["fragment_first_tick"]=0;
            ability["description"]=$"Прямое попадание ядра наносит {profile.impact} осадного урона. " +
                "По наземным бойцам в радиусе 180 проходят три удара: при попадании, через 1 и через 2 с. "+profile.description;
        }
    }

    internal static JObject Apply(JObject original){
        if(!((JArray)original["lines"]).Any(row=>Gold.ContainsKey((string)row["building"]??"")))return original;
        var catalog=(JObject)original.DeepClone();
        var lines=((JArray)catalog["lines"]).OfType<JObject>().ToArray();
        var costs=new Dictionary<string,int>();
        var parents=new Dictionary<string,string>();
        foreach(var row in lines){
            string id=(string)row["building"];
            int old=(int?)row["cost"]??0;
            if(Gold.TryGetValue(id,out int gold)&&old>0){
                row["cost"]=gold;
                row["woodReward"]=Mathf.RoundToInt(((int?)row["woodReward"]??0)*gold/(float)old);
            }
            if(Wood.TryGetValue(id,out int wood))row["woodCost"]=wood;
            costs[id]=(int)row["cost"];
            foreach(var upgrade in HumanMatch.Strings(row["upgrades"]))parents.TryAdd(upgrade,id);
        }
        foreach(var row in lines){
            if(string.IsNullOrEmpty((string)row["unit"]))continue;
            string id=(string)row["building"];
            int invested=0;bool changed=false;
            for(int link=0;link<lines.Length&&costs.TryGetValue(id,out int cost);link++){
                invested+=cost;changed|=Gold.ContainsKey(id);
                if(!parents.TryGetValue(id,out id))break;
            }
            if(changed)row["seconds"]=15+invested/20f;
        }

        var abilities=(JObject)catalog["abilities"];
        ApplyHandSiege(lines,abilities);
        if(abilities?["CT06"] is JObject supply){
            supply["reserve_duration"]=48;supply["cd"]=16;
            supply["description"]="Лечит до пяти союзников в своей зоне на 8 HP каждые 2 с. Раз в 16 с даёт запасной натиск трём всадникам в радиусе 720. Запас действует 48 с. Он расходуется на удар после первого обычного натиска и его 6-секундной перезарядки.";
        }
        if(abilities?["APGo"] is JObject gong){
            gong["purify_debt"]=75;gong["count"]=5;gong["cd"]=8;
            gong["description"]="За 20 маны снимает до 75 ед. отложенного урона у пяти ближайших пандаренов с отложенным уроном в радиусе 900. Срабатывает только при наличии подходящей цели. Перезарядка 8 с.";
            gong["range"]=30;
        }
        if(abilities?["PBll"] is JObject bell){
            bell["break_slow"]=.25f;bell["power"]=145;bell["count"]=4;
            bell["description"]="Даёт до четырёх щитов по 145 HP бойцам в бою на 6 с. Если враг разбивает щит, ударная волна замедляет до трёх врагов рядом на 25% на 2,5 с. Нужны хотя бы два союзника без щита.";
        }
        if(abilities?["BH15"] is JObject knell){
            knell["reserve_duration"]=14;knell["cd"]=26;knell["range"]=25;
            knell["description"]="Когда в радиусе 750 собираются минимум два наземных врага, набат вызывает щитоносца и двух пикинёров на 14 с. Перезарядка 26 с общая для всех набатов стороны.";
        }
        return catalog;
    }
}
}
