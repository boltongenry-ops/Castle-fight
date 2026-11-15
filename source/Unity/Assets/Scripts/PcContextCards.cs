using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
namespace CastleFight {
public sealed partial class PcGame {
    static bool ContextAbilityVisible(string raw,string id,JToken cfg){
        if((string)cfg?["kind"]=="pending"||(string)cfg?["kind"]=="native_helper")return false;
        if(id=="A07K")return raw=="h01U"||raw=="h05H"||raw=="e00A"||raw=="e00B";
        if(id=="A07M")return raw=="n01U";
        if(id=="A0C5")return raw=="n029"||raw=="n02B"||raw=="o00E";
        // The working range toggle already has its own command below the skills.
        if(id=="A09A")return false;
        return true;
    }
    string ContextSkillTip(string raw,string id,JToken cfg){
        if(id=="A0C5"&&raw=="o00E")return "Перегрузка бури\n680 фактически полученного урона заряжают самоподрыв. Скорость становится 450. Взрыв: 375 урона до радиуса 250, 225 — до 350. Поражает летунов; здания получают 75% урона. Боец расходуется при взрыве.";
        if(id=="A07K"&&(raw=="e00A"||raw=="e00B"))return "Поглощение души\nПри убийстве живого бойца восстанавливает "+(raw=="e00A"?"20":"14")+"% своего максимального здоровья."+(raw=="e00A"?"\nЕсли дух погибает от живого бойца, наносит убийце 350 хаотического урона.":"");
        var tip=RaceSkillTip(id,cfg);if(id=="A07B")tip=tip.Replace("Горный великан становится","Боец становится");return ReadableSkillNumbers(tip);
    }
    static string SkillNumber(float value)=>value.ToString("0.##",CultureInfo.GetCultureInfo("ru-RU"));
    static string ReadableSkillNumbers(string text)=>Regex.Replace(text,@"\d+[.,]\d{5,}",m=>float.TryParse(m.Value.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out float n)?SkillNumber(n):m.Value);
    JToken UnitSkillConfig(HumanMatch.Unit unit,string id,JToken cfg){var ranks=cfg?["ranks"] as JArray;return ranks!=null&&ranks.Count>0?ranks[UnityEngine.Mathf.Clamp(unit.abilityLevels.TryGetValue(id,out int level)?level-1:0,0,ranks.Count-1)]:cfg;}
    string StandardSkillTip(string id,JToken cfg){
        string kind=(string)cfg?["kind"],name=Clean((string)cfg?["name"]),desc=Clean((string)cfg?["description"]);var profile=cfg?["profile"];
        float F(string key)=>HumanMatch.F(cfg,key);string N(float value)=>SkillNumber(value);
        if(kind=="resist")return "Сопротивление магии\nСнижает магический урон на "+N(F("power")*100)+"%. Одинаковые сопротивления не складываются; действует сильнейшее.";
        if(kind=="shield")return "Магический щит\nБлокирует направленное в бойца вражеское заклинание. Восстановление: "+N(F("cd"))+" с.";
        if(kind=="hardened")return "Защитная броня\nС шансом "+N(HumanMatch.F(profile,"chance")*100)+"% блокирует "+N(HumanMatch.F(profile,"block"))+" урона оружия. Минимальный остаток: "+N(HumanMatch.F(profile,"minimum"))+". Урон ниже этого остатка не увеличивается.";
        if(id=="A0BK")return "Шипастая кора\nПри ближней атаке по энту возвращает атакующему "+N(F("reflect")*100)+"% урона.";
        if(id=="A0AQ"||id=="A0AU")return "Последний выводок\nПри смерти создаёт "+N(F("count"))+(id=="A0AQ"?" лесных пауков.":" гигантского паука.");
        if(id=="A05N")return "Тёмная стая\n"+desc;
        if(id=="A09W")return "Сильный яд\nПопадание отравляет: "+N(F("dps"))+" урона/с на "+N(F("duration"))+" с. Движение замедляется на "+N(F("slow")*100)+"%, скорость атаки — на "+N(-F("haste")*100)+"%.";
        if(id=="A0C2")return "Выжигающий удар\nС шансом "+N(F("chance")*100)+"% при попадании сжигает до 150 маны цели и наносит столько же магического урона.";
        if(id=="A0CM")return "Огненное дыхание\nС шансом "+N(F("chance")*100)+"% при попадании выпускает волну на 100 магического урона. Длина 800, ширина от 300 до 800, скорость 1050; поражает наземных и воздушных врагов.";
        if(id=="A0CQ"||id=="A0CR")return "Встречный ветер\nВражеские летуны в радиусе "+N(HumanMatch.F(profile,"radius")*30)+" теряют "+N(-HumanMatch.F(profile?["effects"],"speed")*100)+"% скорости движения и атаки. Союзников и наземных бойцов не замедляет.";
        if(id=="A0FR")return "Аура разложения\nВражеские органические бойцы в радиусе "+N(HumanMatch.F(profile,"radius")*30)+" получают "+N(HumanMatch.F(profile?["effects"],"damage_tick"))+" магического урона/с.";
        if(id=="Aasl")return "Аура замедления\nВраги в радиусе "+N(HumanMatch.F(profile,"radius")*30)+" теряют 25% скорости движения и атаки.";
        return name+"\n"+desc;
    }
}
}
