using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
internal static class WorkshopCatalog038 {
    internal sealed class Finish {
        public readonly string id,kind,title,description,color,accent;
        public readonly int cost,pattern;

        public Finish(string id,string kind,string title,string description,int cost,string color,string accent,int pattern){
            this.id=id;this.kind=kind;this.title=title;this.description=description;this.cost=cost;
            this.color=color;this.accent=accent;this.pattern=pattern;
        }

        public Color Tint=>Parse(color);
        public Color Highlight=>Parse(accent);
        static Color Parse(string hex)=>ColorUtility.TryParseHtmlString("#"+hex,out var value)?value:Color.white;
        public JObject Row()=>new JObject{
            ["id"]=id,["kind"]=kind,["title"]=title,["description"]=description,["cost"]=cost,["color"]=color
        };
    }

    static readonly Finish[] Added={
        new Finish("aura_frost","aura","Северный венец","Холодные кристаллы вращаются над светящимся кольцом.",180,"7dcdf6","e4fbff",2),
        new Finish("aura_moon","aura","Лунная печать","Серебряные дуги проходят по кругу, оставляя лиловое сияние.",230,"aa88f7","f5dcff",3),
        new Finish("aura_storm","aura","Грозовой обод","Искры бегут по трём расходящимся кольцам.",260,"72e0fa","b8a9ff",4),
        new Finish("aura_anvil","aura","Печать кузнеца","Медные заклёпки и кольцо тлеющих рун под ногами.",150,"efa24d","ffe4a1",5),
        new Finish("aura_sand","aura","Песчаный вихрь","Перекрещённые струи золотой пыли кружат у земли.",180,"deb776","fff1a5",6),
        new Finish("aura_blossom","aura","Цветение рощи","Розовые лепестки и зелёные огни раскрываются вокруг строителя.",220,"85dc99","fca7c5",7),

        new Finish("effect_coals","effect","Горячие следы","При ходьбе за строителем остаётся короткий след из угольков.",100,"ed7834","ffd179",0),
        new Finish("effect_ice","effect","Стеклянный иней","Шаги рассыпают на земле небольшие ледяные осколки.",125,"7fd9ef","e6fbff",1),
        new Finish("effect_stardust","effect","Звёздная пыль","Во время движения у ног загораются сиреневые искры.",150,"ad8aff","f8caff",2),
        new Finish("effect_hammer","effect","Удар наковальни","При постановке здания над фундаментом взлетают янтарные искры.",140,"eba348","fff1b4",3),
        new Finish("effect_vine","effect","Вспышка лозы","При постановке здания по земле расходятся зелёные огни и розовые лепестки.",175,"73dc99","ffd1d6",4),
        new Finish("effect_rivet","effect","Грозовая заклёпка","Постановку здания отмечает короткая вспышка голубых осколков.",210,"5ae7f5","b69bff",5),

        new Finish("skin_copper","skin","Медный мастер","Рыже-медная ткань и тёплая отделка инструмента.",120,"cd743b","f9c891",0),
        new Finish("skin_frost","skin","Ледяной караул","Холодная лазурь и светлая стальная отделка.",160,"68c9ed","c2eefb",1),
        new Finish("skin_crimson","skin","Багряная гвардия","Глубокая алая ткань и тёплый металл.",170,"a92958","f8a3a6",2),
        new Finish("skin_midnight","skin","Ночной дозор","Фиолетовая ткань и холодные сумеречные оттенки.",190,"534390","b9a7ee",3),
        new Finish("skin_emerald","skin","Изумрудный цех","Изумрудная ткань и мятная отделка.",150,"279361","b4ebbb",4),
        new Finish("skin_sand","skin","Песочный обоз","Песочная ткань и бронзовые оттенки.",130,"c2a260","f8dbad",5),
        new Finish("skin_silver","skin","Серебряная форма","Светлая серо-голубая ткань и серебристая отделка.",200,"a6bacb","d9effb",6),
        new Finish("skin_obsidian","skin","Обсидиан и золото","Угольная ткань и приглушённые золотые отблески.",260,"493b37","e8b767",7),
    };

    static readonly Dictionary<string,Finish> ById=Index();
    static Dictionary<string,Finish> Index(){
        var rows=new Dictionary<string,Finish>(StringComparer.Ordinal){
            ["aura_jade"]=new Finish("aura_jade","aura","Нефритовое сияние","",250,"52d99d","cdfad9",0),
            ["aura_flame"]=new Finish("aura_flame","aura","Угольки Хаоса","",250,"ff9a4d","ffde79",1)
        };
        foreach(var finish in Added)rows.Add(finish.id,finish);
        return rows;
    }
    internal static Finish Find(string id)=>id!=null&&ById.TryGetValue(id,out var finish)?finish:null;
    internal static void Extend(JArray items){
        foreach(var finish in Added)items.Add(finish.Row());
    }
}
}
