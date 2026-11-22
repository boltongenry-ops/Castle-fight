using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CastleFight {
public static class BlackHand {
    public static int Ammo(string id)=>id=="bU12"?1:id=="bU13"?2:id=="bU14"?3:0;
    public static UnityEngine.Color StoneColor(int ammo)=>ammo==1?UnityEngine.Color.green:ammo==2?new UnityEngine.Color(1,.42f,.08f):ammo==3?new UnityEngine.Color(.72f,.24f,.88f):UnityEngine.Color.gray;
    internal static bool IsStatus(string id)=>id=="BHW1"||id=="BHW2"||id=="BHPs"||id=="BHCr"||
        id=="BHTx"||id=="BHJu"||id=="BHSl";
    public const int Race = 17;
    // The prototype shares the human bootstrap art on both platforms.
    static readonly int[] art = {0,11,14,1,12,0,11,4,15,5,5,2,2,13,13,6,4,10,8,9};
    public static int ArtRow(int row) => art[row];

    public static JObject Catalog(string human) {
        var data = JObject.Parse(human);
        var templates = (JArray)data["lines"];
        var lines = new JArray();
        data["race"] = "Black Hand";
        data["aspects"] = new JArray(
            new JObject {["id"]="blackhand_iron",["title"]="Железный устав",["tag"]="Стойкость",
                ["description"]="Один раз за жизнь при падении здоровья ниже 50% боец получает на 5 секунд 20% снижения урона от обычных атак."},
            new JObject {["id"]="blackhand_siege",["title"]="Осадная доктрина",["tag"]="Осада",
                ["description"]="Бойцы наносят на 15% больше урона обычными атаками по зданиям и замку. Радиус шрапнели всех камнемётов увеличивается со 180 до 210."});
        data["lines"] = lines;
        void Troop(string building,string fighter,int gold,string key,string skill,string description,params int[] upgrades) {
            int row = lines.Count;
            var line = (JObject)templates[ArtRow(row)].DeepClone();
            var stats = line["stats"];
            line["building"] = "bH"+row.ToString("00");
            line["unit"] = "bU"+row.ToString("00");
            line["category"] = "producer";
            line["name"] = building;
            line["unitName"] = fighter;
            line["cost"] = line["woodReward"] = gold;
            line["woodCost"] = line["slots"] = 0;
            line["base"] = key.Length != 0;
            line["hotkey"] = key;
            line["upgrades"] = new JArray(upgrades.Select(x=>"bH"+x.ToString("00")));
            line["abilities"] = new JArray();
            line["buildingStats"]["abilities"] = new JArray();
            line["buildingStats"]["weapons"] = new JArray();
            string id = "BH"+row.ToString("00");
            data["abilities"][id] = new JObject {["id"]=id,["name"]=skill,["kind"]="passive",["description"]=description,
                ["icon"]=row>=11?"ReplaceableTextures\\CommandButtons\\BTNHumanMissileUpThree.blp":"ReplaceableTextures\\CommandButtons\\BTNBlacksmith.blp"};
            stats["abilities"] = new JArray(id);
            stats["ability"] = id;
            line.Remove("production");
            line["seconds"]=15+gold/20f;
            if(row==7||row==8||row==9||row==10){
                data["abilities"][id]["cd"]=row==7?10:row==8?8:12;
                if(row>=9){
                    data["abilities"][id]["mana"]=row==9?35:45;
                    data["abilities"][id]["kind"]="blackhand_tithe";
                    data["abilities"][id]["animation"]="Attack";
                    data["abilities"][id]["cast_pause"]=.4f;
                }
            }
            lines.Add(line);
        }
        Troop("Караульный двор","Щитоносец",100,"Q","Щит караула","Блокирует 40% одного удара ближнего боя раз в 8 с.",1);
        Troop("Двор ветеранов","Ветеран",120,"","Щит ветерана","Блокирует 55% одного удара ближнего боя раз в 7 с.",2);
        Troop("Двор маршала","Чёрный маршал",220,"","Удар щитом","Сохраняет блок ветерана. Раз в 8 с бьёт до трёх бойцов перед собой на 65.");
        Troop("Арбалетная галерея","Стрелок",145,"W","Сквозной болт","Каждый четвёртый болт пробивает строй на 40% урона.",4);
        Troop("Тяжёлая галерея","Тяжёлый арбалетчик",145,"","Тяжёлый болт","Каждый третий болт пробивает строй на 65% урона.");
        Troop("Двор пик","Пикинёр",125,"E","Зазубренная пика","Первый удар +25 раз в 8 с. Шанс 25% вызвать кровотечение на 5 с по 9 урона.",6);
        Troop("Двор алебард","Чёрный алебардщик",135,"","Кровавая алебарда","Первый удар +40 раз в 7 с. Шанс 35% вызвать кровотечение на 5 с по 14 урона.");
        Troop("Дом палача","Палач",185,"R","Казнь","Раз в 10 с добивает при остатке до 15% здоровья и до 120 HP.",8);
        Troop("Эшафот","Старший палач",175,"","Высшая мера","Раз в 8 с добивает при остатке до 20% здоровья и до 200 HP.");
        Troop("Часовня скорби","Чернокнижник",230,"A","Железная десятина","65 магического урона и по 30 за следующие четыре атаки за 6 с.",10);
        Troop("Часовня инквизиции","Инквизитор",170,"","Железная десятина","90 магического урона и по 45 за следующие пять атак за 7 с.");
        Troop("Осадный двор","Камнемёт",225,"S","Раздробление","55 ядром и три тика осколками по 10 осадного урона.",12,13,14);
        Troop("Ядовитая мастерская","Ядовитый камнемёт",155,"","Ядовитая шрапнель","12 колющего урона в секунду и яд на 3 с по 7.");
        Troop("Огненная мастерская","Огненный камнемёт",155,"","Огненная шрапнель","24 магического урона каждую секунду и ещё 18 на третьем тике.");
        Troop("Проклятая мастерская","Проклятый камнемёт",155,"","Проклятая шрапнель","12 урона хаосом в секунду. Ослабляет обычные атаки на 20% на 3 с.");
        for(int i=0;i<lines.Count;i++){
            var s=lines[i]["stats"]; var w=s["weapons"][0];
            if(i==3||i==4){w["weapon_type"]="missile";w["missile_speed"]=42;w["missile_homing"]=true;w["missile"]="Abilities\\Weapons\\Arrow\\ArrowMissile.mdl";}
            if(i==6)s["range"]=w["range"]=4.5;
            if(i==10){s["hp"]=430;s["damage"]=w["damage"]=42;s["armor"]=3;}
            if(i>=12)s["hp"]=390;
            if(i==2){lines[i]["woodCost"]=250;lines[i]["slots"]=1;}
            if(i==9||i==10){s["max_mana"]=120;s["start_mana"]=80;s["mana_regen"]=2;}
            if(i>=11){var stone=templates[2]["stats"]["weapons"][0].DeepClone();stone["damage"]=55;stone["cooldown"]=3.5;stone["splash"]=new JObject();s["weapons"]=new JArray(stone);s["damage"]=55;s["cooldown"]=3.5;}
        }
        void Status(string id,string name,int dps,string type,string description) {
            data["abilities"][id]=new JObject {["id"]=id,["name"]=name,["kind"]="debuff",["dps"]=dps,["damage_type"]=type,["interval"]=1,["description"]=description};
        }
        Status("BHW1","Кровотечение",9,"normal","Теряет 9 HP в секунду до учёта защиты.");
        Status("BHW2","Глубокая рана",14,"normal","Теряет 14 HP в секунду до учёта защиты.");
        Status("BHPs","Яд шрапнели",7,"pierce","Теряет 7 HP в секунду до учёта защиты.");
        Status("BHCr","Проклятые осколки",0,"chaos","Урон обычного оружия снижен на 20%.");
        Status("BHTx","Железная десятина",0,"magic","Получает магический урон за каждую выпущенную атаку, пока есть заряды.");
        void Structure(string raw,string name,string category,int price,int wood,string key,string description){
            int row=lines.Count;
            var line=(JObject)templates[ArtRow(row)].DeepClone();
            line["building"]=raw;line["name"]=name;line["unit"]=line["unitName"]="";
            line["category"]=category;line["base"]=true;line["hotkey"]=key;
            line.Remove("production");
            line["cost"]=price;line["woodCost"]=wood;line["woodReward"]=0;line["slots"]=category=="legendary"?1:0;
            line["upgrades"]=new JArray();
            line["stats"]=new JObject {["abilities"]=new JArray(),["weapons"]=new JArray()};
            string skill="BH"+row;
            data["abilities"][skill]=new JObject{["id"]=skill,["kind"]="passive",["name"]=name,["description"]=description};
            if(row<18)data["abilities"][skill]["cd"]=row==15?12:row==16?5:30;
            line["abilities"]=new JArray(skill);line["buildingStats"]["abilities"]=new JArray();
            if(category!="tower")line["buildingStats"]["weapons"]=new JArray();
            lines.Add(line);
        }
        Structure("bH15","Колокол немоты","special",220,120,"D","Прерывает подготовку одного вражеского заклинания в радиусе 600 раз в 12 с.");
        Structure("bH16","Лазарет ветеранов","special",200,100,"F","Раз в 5 с восстанавливает 75 HP самому тяжело раненному союзнику в радиусе 600.");
        Structure("bH17","Палата последнего суда","legendary",550,350,"Z","Раз в 30 с поражает до пяти бойцов в плотной группе на 100 магического урона. Получаемое лечение снижено вдвое на 6 с.");
        Structure("bH18","Вороновая вышка","tower",195,100,"X","Обстреливает землю и воздух. Каждый третий выстрел замедляет цель на 35% на 3 с.");
        data["abilities"]["BHIr"]=new JObject {["id"]="BHIr",["name"]="Железный устав",["kind"]="buff",["duration"]=5};
        Status("BHJu","Последний суд",0,"magic","Получаемое лечение снижено на 50%.");
        Status("BHSl","Вороновы путы",0,"magic","Скорость передвижения снижена на 35%.");
        lines.Add(templates[9].DeepClone());
        return data;
    }
}
}
