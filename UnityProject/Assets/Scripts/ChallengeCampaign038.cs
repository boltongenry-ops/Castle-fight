using System.Linq;
using Newtonsoft.Json.Linq;

namespace CastleFight {
internal static class ChallengeCampaign038 {
    const string Siege="siege";
    internal static bool IsSiege(JToken row)=>(string)row["chapter"]==Siege;

    internal static void Extend(JObject catalog){
        var challenges=(JArray)catalog["challenges"];
        foreach(var row in Challenges()){
            string id=(string)row["id"];
            if(!challenges.Any(old=>(string)old["id"]==id))challenges.Add(row);
        }
    }

    static JObject Entry(string id,string title,string player,string opponent,string description,int reward,JObject conditions){
        conditions["id"]=id;conditions["title"]=title;conditions["race"]=player;conditions["opponent"]=opponent;
        conditions["description"]=description;conditions["reward"]=reward;
        conditions["xp"]=reward;conditions["chapter"]=Siege;
        return conditions;
    }

    static JObject[] Challenges()=>new[]{
        Entry("siege_iron_marathon","Великая осада","Human Alliance","Orcish Horde",
            "У обоих замков по 40 000 HP. На старте у каждой стороны 1600 золота и 600 дерева. Разрушите вражескую крепость.",300,
            new JObject{["castle_hp"]=40000,["start_gold"]=1600,["start_wood"]=600}),
        Entry("siege_deep_bastion","Глубинный бастион","Naga Faction","Northern Realms",
            "Замок наг имеет 9000 HP, замок северян — 26 000. Победите с пятью производителями или меньше. Покупки в замке закрыты.",280,
            new JObject{["player_castle_hp"]=9000,["opponent_castle_hp"]=26000,["start_gold"]=1400,["start_wood"]=450,
                ["max_producers"]=5,["no_shop"]=true}),
        Entry("siege_no_rest","Лёд без передышки","Northern Realms","Elementals' Universe",
            "У замков 12 000 и 16 000 HP. Они сами не восстанавливаются, ваш строитель не может чинить замок. Победите за 22 минуты.",270,
            new JObject{["player_castle_hp"]=12000,["opponent_castle_hp"]=16000,["start_gold"]=1200,["start_wood"]=350,
                ["no_castle_regen"]=true,["no_builder_repair"]=true,["max_seconds"]=1320}),
        Entry("siege_broken_gate","Трещина в воротах","Nerubian Swarm","Elven Union",
            "Замок нерубов начинает с 3600 из 8000 HP, у эльфов — 10 000. К победе нужно три действующих улучшенных здания.",250,
            new JObject{["player_castle_hp"]=8000,["opponent_castle_hp"]=10000,["player_castle_start_fraction"]=.45,
                ["start_gold"]=1300,["start_wood"]=400,["min_upgraded_buildings"]=3}),
        Entry("siege_steel_watch","Стальной дозор","Mech Coalition","Chaos Legions",
            "Удержите замок с 8000 HP шесть минут и сохраните не меньше 65% здоровья. Максимум шесть ваших построек, без спасительного взрыва.",280,
            new JObject{["player_castle_hp"]=8000,["opponent_castle_hp"]=20000,["start_gold"]=1100,["opponent_start_gold"]=1600,
                ["start_wood"]=420,["opponent_start_wood"]=650,["survive_seconds"]=360,["min_castle_fraction"]=.65,["max_buildings"]=6,["no_rescue"]=true}),
        Entry("siege_living_wall","Живая стена","Nature Force","Night Elfs",
            "Продержитесь семь минут против ночных эльфов. У обоих замков по 12 000 HP, к концу у вашего должно остаться не меньше 60%. Башни закрыты.",255,
            new JObject{["castle_hp"]=12000,["start_gold"]=1100,["start_wood"]=450,["survive_seconds"]=420,["min_castle_fraction"]=.6,["no_towers"]=true}),
        Entry("siege_steppe_reserve","Резерв степи","Centaur Khanates","Pandaren Order",
            "Пандарены начинают с 2000 золота, кентавры — с 1100. Победите максимум с четырьмя производителями и тремя действующими ветками армии.",235,
            new JObject{["start_gold"]=1100,["opponent_start_gold"]=2000,["start_wood"]=450,["opponent_start_wood"]=550,
                ["max_producers"]=4,["min_distinct_producers"]=3,["difficulty"]=2}),
        Entry("siege_before_dawn","До рассвета","Night Elfs","Corrupted Conglomerate",
            "Разрушьте замок Осквернённых с 10 000 HP за 14 минут. Не больше пяти своих построек, без покупок в замке.",270,
            new JObject{["opponent_castle_hp"]=10000,["start_gold"]=1600,["start_wood"]=500,["max_seconds"]=840,
                ["max_buildings"]=5,["no_shop"]=true}),
        Entry("siege_prism","Призма четырёх стихий","Elementals' Universe","Human Alliance",
            "У обоих замков по 14 000 HP. К победе нужны четыре работающие ветки армии. Не больше семи своих построек и без башен.",270,
            new JObject{["castle_hp"]=14000,["start_gold"]=1700,["start_wood"]=650,["max_buildings"]=7,
                ["min_distinct_producers"]=4,["no_towers"]=true}),
        Entry("siege_jade_stock","Нефритовый запас","Pandaren Order","Mech Coalition",
            "Пандарены получают 2000 золота и 1000 дерева на старте. У противника замок с 18 000 HP. Спасительный взрыв отключён у обеих сторон.",240,
            new JObject{["player_castle_hp"]=12000,["opponent_castle_hp"]=18000,["start_gold"]=2000,["start_wood"]=1000,
                ["opponent_start_gold"]=900,["opponent_start_wood"]=250,["no_rescue"]=true}),
        Entry("siege_lanterns","Фонари на стенах","Elven Union","Undead Forces",
            "Ваш замок начинает с 3600 из 6000 HP, замок нежити имеет 18 000 HP. К победе восстановите не меньше 65% здоровья своей крепости.",260,
            new JObject{["player_castle_hp"]=6000,["opponent_castle_hp"]=18000,["player_castle_start_fraction"]=.6,
                ["start_gold"]=1500,["start_wood"]=550,["min_castle_fraction"]=.65}),
        Entry("siege_ultimate_keep","Последняя цитадель","Ultimate Builder","Nerubian Swarm",
            "Удержите цитадель с 24 000 HP восемь минут, сохранив хотя бы половину здоровья. Одновременно разрешено не больше семи построек.",290,
            new JObject{["player_castle_hp"]=24000,["opponent_castle_hp"]=20000,["start_gold"]=1700,["start_wood"]=650,
                ["opponent_start_gold"]=1600,["opponent_start_wood"]=700,["survive_seconds"]=480,["min_castle_fraction"]=.5,["max_buildings"]=7,["difficulty"]=2}),
    };
}
}
