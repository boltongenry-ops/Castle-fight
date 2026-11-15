using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public static class Bot027Scenarios {
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Call(HumanMatch m,string method)=>typeof(HumanMatch).GetMethod(method,Private).Invoke(m,null);
    static void Seed(HumanMatch m,uint seed)=>typeof(HumanMatch).GetField("botRandom",Private).SetValue(m,seed);
    static JObject Playable(JObject c){if((string)c["race"]!="Ultimate Builder")return c;var draw=new UltimateLoadout((JArray)c["ultimateSlots"],n=>0);return draw.Playable(c,draw.Draw());}
    static HumanMatch New(Func<string,JObject> catalogs,string race="human",int gold=1000,int difficulty=1){var m=new HumanMatch(catalogs("human"),gold,difficulty,botCatalog:Playable(catalogs(race)));Seed(m,123456);return m;}
    static HumanMatch.Hut Hut(HumanMatch m,string raw,int x){int k=m.Kind(raw);var l=m.Lines[k];var h=new HumanMatch.Hut{id=10000+x,team=1,kind=k,x=x,y=11,position=HumanMatch.Plot(1,x,11),paid=true,hp=1000,maxHp=1000,spent=l.cost,baseCost=l.cost,income=l.cost*l.incomeRate,spawnLeft=100};m.Huts.Add(h);return h;}
    static HumanMatch.Unit Enemy(HumanMatch m,string building,Vector2 p){var u=m.Spawn(0,m.Kind(building),p);u.hp=u.maxHp=500;u.cooldown=u.castPause=10000;return u;}
    static string[] Composition(HumanMatch m)=>m.Huts.Where(h=>h.team==1).Select(h=>m.Lines[h.kind].building).ToArray();
    static HumanMatch.Line Chosen(HumanMatch m,string[] before){var newly=m.Huts.FirstOrDefault(h=>h.team==1&&!before.Contains(m.Lines[h.kind].building));return newly==null?null:m.Lines[newly.kind];}
    public static JArray Run(Func<string,JObject> catalogs,Action<bool,string> check){
        var result=new JArray();
        foreach(string race in new[]{"human","orcs","naga","nature","chaos","northern","mech","undead","corrupted","elemental","night","elven","ultimate","nerubian","pandaren"}){
            var m=New(catalogs,race,250);float minGold=250,minWood=0;int maxQueue=0;
            // Economy/development scenario: castles are invulnerable fixtures so
            // an empty opponent does not end the observation before fifteen minutes.
            for(int i=0;i<1800;i++){m.Winner=-2;m.CastleHp[0]=m.CastleHp[1]=HumanMatch.CastleMax;m.Step(.5f);minGold=Mathf.Min(minGold,m.Gold[1]);minWood=Mathf.Min(minWood,m.Wood[1]);maxQueue=Math.Max(maxQueue,m.Builders[1].jobs.Count);}
            var comp=Composition(m);check(comp.Distinct().Count()>=3,"Fifteen-minute "+race+" bot develops at least three building types");
            check(m.Upgrades>0,"Fifteen-minute "+race+" bot advances existing technology");
            check(minGold>=0&&minWood>=0&&maxQueue<=1,"Normal "+race+" bot obeys real resources and one-job queue");
            check(m.Huts.All(h=>HumanMatch.LegalCell(h.team,h.x,h.y))&&m.Huts.Select(h=>h.team+":"+h.x+":"+h.y).Distinct().Count()==m.Huts.Count,"Legal non-overlapping placements for "+race);
            check(m.Huts.Where(h=>h.team==1).All(h=>Vector2.Distance(h.position,HumanMatch.CastlePosition(1))>=m.CastleCollision+HumanMatch.Cell*.5f),"Bot buildings avoid the castle footprint for "+race);
            result.Add(new JObject{["race"]=race,["composition"]=new JArray(comp),["distinct"]=comp.Distinct().Count(),["upgrades"]=m.Upgrades,["income"]=m.Income(1),["minGold"]=minGold,["minWood"]=minWood,["maxQueue"]=maxQueue});
        }
        {
            var m=New(catalogs);Hut(m,"h000",17);Hut(m,"h004",18);Hut(m,"h037",19);var before=Composition(m);
            for(int i=0;i<4;i++)Enemy(m,"h015",new Vector2(95,i*3));Call(m,"Bot");var choice=Chosen(m,before);
            check(choice!=null&&(choice.weapons.Any(w=>w.air)||choice.buildingWeapons.Any(w=>w.air)),"Observed flying army triggers a purchase capable of attacking air");
            result.Add(new JObject{["airCounter"]=choice?.building,["plan"]=m.BotPlan});
        }
        {
            var m=New(catalogs);Hut(m,"h000",17);Hut(m,"h003",18);Hut(m,"h004",19);var before=Composition(m);
            for(int i=0;i<4;i++){var u=Enemy(m,"h000",new Vector2(95,i*3));u.spellResist=1;m.Lines[u.kind].armorType="heavy";}
            Call(m,"Bot");var choice=Chosen(m,before);check(choice!=null&&(choice.weapons.Concat(choice.buildingWeapons).Any(w=>w.ground&&w.attack!="magic"&&w.attack!="spells")),"Magic-immune visible army is countered by physical weapon damage");
        }
        {
            var control=New(catalogs);var hidden=New(catalogs);control.VisionTest=hidden.VisionTest=(t,p)=>false;
            for(int i=0;i<8;i++)Enemy(hidden,"h015",new Vector2(-150,i));Call(control,"Bot");Call(hidden,"Bot");
            check(Composition(control).SequenceEqual(Composition(hidden)),"Unseen enemy composition cannot alter the bot's purchase");
            var contacts=(System.Collections.IDictionary)typeof(HumanMatch).GetField("botContacts",Private).GetValue(hidden);check(contacts.Count==0,"Hidden enemies do not enter scouting memory");
            hidden.VisionTest=(t,p)=>true;var scout=hidden.Units[0];scout.position=new Vector2(100,0);Call(hidden,"ObserveBot");check(contacts.Count==1,"An actually visible enemy enters scouting memory");
            hidden.VisionTest=(t,p)=>false;hidden.Elapsed=121;Call(hidden,"ObserveBot");check(contacts.Count==0,"Old scouting information expires instead of becoming permanent omniscience");
        }
        {
            var m=New(catalogs,gold:120);Hut(m,"h000",17);Hut(m,"h003",18);int count=m.Huts.Count;Call(m,"Bot");
            check(m.Huts.Count==count&&m.BotPlan.StartsWith("Копит на"),"Bot saves for a stronger next choice instead of dumping spare gold into another cheap barracks");
            m.Gold[1]=1000;Call(m,"Bot");check(m.Huts.Count>count||m.Upgrades>0,"Saved plan is executed when its funds become available");
        }
        foreach(int difficulty in new[]{0,1,2}){
            var m=New(catalogs,gold:2000,difficulty:difficulty);for(int i=0;i<8;i++)Call(m,"Bot");
            check(m.Builders[1].jobs.Count<=(difficulty==2?2:1)&&m.ReservedGold(1)<=m.Gold[1],"Difficulty "+difficulty+" does not overqueue or overspend reserved funds");
        }
        {
            var m=New(catalogs);m.NetworkReplica=true;Call(m,"Bot");check(m.Huts.Count==0,"Local bot cannot issue commands in a network replica");
            m.NetworkReplica=false;m.Surrender(1);Call(m,"Bot");check(m.Huts.Count==0,"Finished match cannot receive new bot orders");
        }
        {
            var m=New(catalogs,"nature");m.Aspects[1]=0;for(int i=0;i<5;i++)Call(m,"Bot");
            check(m.Huts.Where(h=>h.team==1&&!h.paid).Sum(h=>m.GoldPrice(1,h.kind))<=m.Gold[1],"Nature surcharge is included in the bot's exact reserved budget");
        }
        {
            var m=New(catalogs);var alone=Enemy(m,"h000",new Vector2(190,60));alone.hp=10;var cluster=new List<HumanMatch.Unit>();for(int i=0;i<5;i++)cluster.Add(Enemy(m,"h000",new Vector2(105+i,0)));
            Call(m,"BotDefend");check(cluster.All(u=>u.hp<=0)&&alone.hp>0&&m.RescueCharges[1]==0,"Rescue targets the valuable cluster and leaves the isolated decoy alone");
            var quiet=New(catalogs);Enemy(quiet,"h000",new Vector2(90,0));Call(quiet,"BotDefend");check(quiet.RescueCharges[1]==1,"A lone distant raider does not waste the rescue charge");
            quiet.ChallengeRules=new JObject{["no_rescue"]=true};for(int i=0;i<5;i++)Enemy(quiet,"h000",new Vector2(105+i,0));Call(quiet,"BotDefend");check(quiet.RescueCharges[1]==1,"Bot respects challenge rescue restrictions");
        }
        {
            var openings=new HashSet<string>();foreach(uint seed in new uint[]{1,17,1223,123456,985513,1791791,0xFACE55,0xDEADBEEF}){var m=New(catalogs,gold:250);Seed(m,seed);Call(m,"Bot");foreach(string raw in Composition(m))openings.Add(raw);}
            check(openings.Count>=2,"Independent bot styles offer more than one legal opening");result.Add(new JObject{["openingChoices"]=new JArray(openings)});
        }
        {
            var m=New(catalogs,gold:5000);foreach(int x in Enumerable.Range(1,10))Hut(m,"h000",x);m.Elapsed=60;Call(m,"Bot");
            check(m.Inventories[1].slots.Any(s=>s.id=="I00A")&&m.Gold[1]==4400,"Developed bot buys the actual castle haste aura for its native price");
            m.Elapsed=1000;Call(m,"Bot");check(m.Inventories[1].slots.Where(s=>s.id=="I00A").Sum(s=>s.count)==1,"Bot does not stack another copy of its global haste item");
            var repair=New(catalogs);repair.CastleHp[1]=1800;Enemy(repair,"h000",new Vector2(100,0));Call(repair,"Bot");check(repair.Builders[1].repairing&&repair.Builders[1].jobs.Count==0,"Critical castle damage makes an idle builder repair instead of adding another factory");
            var limited=New(catalogs,gold:10000);limited.Wood[1]=10000;limited.ChallengeRules=new JObject{["no_legends"]=true};foreach(int x in Enumerable.Range(16,5))Hut(limited,"h000",x);Call(limited,"Bot");check(limited.Huts.All(h=>limited.Lines[h.kind].slots==0),"Bot does not bypass a challenge's legendary-building restriction");
        }
        return result;
    }
}
}
