using System;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEngine;
public static class OrcKodoChecks {
    static HumanMatch Fresh(JObject c){var m=new HumanMatch((JObject)c.DeepClone(),100000);m.Gold[1]=0;foreach(var l in m.Lines){l.speed=0;l.regen=0;}return m;}
    static HumanMatch.Unit Unit(HumanMatch m,int team,string raw,Vector2 p){var u=m.Spawn(team,m.Kind(raw),p);u.onRoad=true;u.cooldown=10000;return u;}
    static void Step(HumanMatch m,float seconds){for(int i=0;i<(int)Math.Ceiling(seconds/.05f);i++)m.Step(.05f);}
    public static void Run(JObject c,Action<bool,string> check) {
        var m=Fresh(c);check(m.Lines[m.Kind("h02K")].upgrades.SequenceEqual(new[]{"h092","h035","h036"}),"Kodo farm offers all three distinct source upgrades");
        foreach(string raw in new[]{"h092","h035","h036"}) {
            m=Fresh(c);m.TryBuild(0,m.Kind("h02K"),7,15,out _);var h=m.Huts.Single();m.Builders[0].position=h.position-Vector2.up*2.4f;Step(m,2);h.hp=h.maxHp*.4f;
            int next=m.Kind(raw);check(!m.TryBuild(0,next,8,15,out _)&&m.Upgrade(0,h.id,next,out _),"Kodo branch requires upgrading the initial farm: "+raw);
            check(h.spent==360&&m.Wood[0]==360&&Math.Abs(h.spawnLeft-33)<.001f&&Math.Abs(h.hp/h.maxHp-.4f)<.001f,"Kodo upgrade costs 100 grants wood retains health and uses total 360 investment: "+raw);
            check(m.Lines[h.kind].upgrades.Length==0&&m.LegendaryUsed(0)==0,"Kodo upgrade is a final mutually exclusive nonlegendary choice: "+raw);
            Step(m,33.1f);check(m.Units.Any(u=>u.kind==next),"upgraded Kodo farm releases its correct new unit: "+raw);
        }
        m=Fresh(c);var armored=Unit(m,0,"h036",Vector2.zero);var drummer=Unit(m,0,"h035",Vector2.up*2);var duplicate=Unit(m,0,"h035",Vector2.up*3);var ally=Unit(m,0,"h029",Vector2.right*29);var enemy=Unit(m,1,"h029",Vector2.right*5);var far=Unit(m,0,"h029",Vector2.right*31);
        Step(m,.6f);check(Math.Abs(m.Stat(ally,"armor")-4)<.001f&&Math.Abs(ally.auraDamage-.1f)<.001f,"nearby allies receive four armor and ten percent attack from the two distinct Kodo auras");
        check(Math.Abs(drummer.auraDamage-.1f)<.001f&&Math.Abs(duplicate.auraDamage-.1f)<.001f,"same Kodo damage auras do not stack and include their sources");
        check(m.Stat(enemy,"armor")==0&&enemy.auraDamage==0&&m.Stat(far,"armor")==0&&far.auraDamage==0,"auras exclude enemies and units beyond 900 source units");
        ally.spellResist=1;Step(m,.6f);check(ally.auraDamage==.1f&&m.Stat(ally,"armor")==4,"friendly Kodo auras apply through magic immunity");
        m.ApplyEffect(ally,"A02M");var target=Unit(m,1,"h029",ally.position+Vector2.right);ally.cooldown=0;ally.critChance=0;ally.combatTarget=-1;m.TickUnit(ally,.05f);check(Math.Abs(ally.shotDamage-m.Lines[ally.kind].damage*1.33f*1.1f)<.01f,"damage aura multiplies spiritual flame rather than adding its percentage");
        drummer.hp=duplicate.hp=armored.hp=0;Step(m,.6f);check(ally.auraDamage==0&&m.Stat(ally,"armor")==4,"auras disappear after source death while spiritual flame remains");
        m=Fresh(c);var enchanted=Unit(m,0,"h092",Vector2.zero);check(Math.Abs(enchanted.spellResist-.8f)<.001f,"enchanted Kodo has 80 percent source magic resistance");
        float before=enchanted.hp;m.DamageUnit(enchanted,100,1,"magic",true);check(enchanted.hp<before&&before-enchanted.hp<30,"enchanted Kodo resistance reduces actual spell damage");
        check((string)m.Abilities["A08P"]["kind"]=="marker","enchanted Kodo marker is visual rather than an invented slowing aura");
    }
}
