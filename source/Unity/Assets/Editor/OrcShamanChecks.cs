using System;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEngine;
public static class OrcShamanChecks {
    static HumanMatch Fresh(JObject c){var m=new HumanMatch((JObject)c.DeepClone(),100000);m.Gold[1]=0;foreach(var l in m.Lines){l.speed=0;l.regen=0;}return m;}
    static HumanMatch.Unit Unit(HumanMatch m,int team,string raw,Vector2 p){var u=m.Spawn(team,m.Kind(raw),p);u.onRoad=true;u.cooldown=10000;u.manaRegen=0;u.abilities.Clear();return u;}
    static void Step(HumanMatch m,float seconds){for(int i=0;i<(int)Math.Ceiling(seconds/.05f);i++)m.Step(.05f);}
    public static void Run(JObject c,Action<bool,string> check) {
        var m=Fresh(c);int shaman=m.Kind("h02B"),stealer=m.Kind("h06I");
        check(shaman==8&&stealer==9&&m.Lines[shaman].hotkey=="W"&&m.Lines[shaman].upgrades.SequenceEqual(new[]{"h06I"}),"Shaman branch appends stable indices and retains source construction hotkey W");
        check(m.TryBuild(0,shaman,7,15,out _),"Shaman lodge is a base building");var hut=m.Huts.Single();m.Builders[0].position=hut.position-Vector2.up*2.4f;Step(m,2);
        check(hut.paid&&hut.spent==170&&m.Wood[0]==170&&Math.Abs(m.Interval(hut)-23.5f)<.001f,"Shaman lodge charges 170 grants 170 wood and produces every 23.5 seconds");
        hut.hp=hut.maxHp*.5f;check(m.Upgrade(0,hut.id,stealer,out _)&&hut.spent==340&&m.Wood[0]==340&&Math.Abs(hut.spawnLeft-32)<.001f&&Math.Abs(hut.hp/hut.maxHp-.5f)<.001f,"Stealer upgrade charges another 170 preserves health and uses 32 second production");
        Step(m,32.1f);check(m.Units.Any(u=>u.kind==stealer)&&m.LegendaryUsed(0)==0,"upgraded lodge produces a nonlegendary magic stealer");

        m=Fresh(c);var caster=Unit(m,0,"h02B",Vector2.zero);caster.abilities.Add("A02M");var ally=Unit(m,0,"h029",Vector2.right*4);ally.hp=ally.maxHp*.3f;var other=Unit(m,0,"h02H",Vector2.right*6);
        check(caster.maxMana==200&&caster.mana==120&&caster.hp==340,"Shaman has source health and starting mana");
        Step(m,.8f);check(ally.effects.Any(e=>e.id=="A02M")&&!other.effects.Any()&&Math.Abs(caster.mana-85)<.01f,"autocast prioritizes the lowest health ally and charges 35 mana");
        var effect=ally.effects.Single();check(Math.Abs(effect.armor-4)<.001f&&Math.Abs(effect.power-.33f)<.001f&&effect.left>59&&effect.left<=60,"spiritual flame gives 33 percent damage and four armor for 60 seconds");
        check(!m.Cast(caster,"A02M",other.id)&&!m.Cast(caster,"A02M",ally.id),"spiritual flame cannot bypass cooldown or stack on the same ally");
        caster.abilities.Clear();Step(m,6.1f);caster.abilities.Add("A02M");check(m.Cast(caster,"A02M",other.id)&&m.OrcInnerFires==2,"after cooldown spiritual flame can buff a second ally");
        caster.spellTimers.Clear();caster.mana=34;check(!m.Cast(caster,"A02M",caster.id)&&caster.mana==34,"insufficient mana does not spend or apply a buff");caster.mana=100;other.position=Vector2.right*21;other.effects.Clear();check(!m.Cast(caster,"A02M",other.id),"spiritual flame respects 600 source unit range");

        foreach(bool negative in new[]{false,true}) {
            m=Fresh(c);caster=Unit(m,0,"h06I",Vector2.zero);caster.abilities.Add("A0A6");var donor=Unit(m,negative?0:1,"h029",Vector2.right*10);var recipient=Unit(m,negative?1:0,"h02H",Vector2.right*16);
            m.Abilities["QA_STATUS"]=new JObject{["id"]="QA_STATUS",["kind"]=negative?"debuff":"buff",["duration"]=20,["hero_duration"]=20,["targets"]="air,ground",["armor"]=negative?-4:4,["power"]=negative?-.2f:.33f};m.ApplyEffect(donor,"QA_STATUS");donor.effects[0].left=12;var original=donor.effects[0];
            check(m.Cast(caster,"A0A6",donor.id)&&m.OrcStealShots==1&&donor.effects.Contains(original)&&recipient.effects.Count==0&&caster.mana==160,"steal launches without moving the status before impact "+negative);
            check(!m.Cast(caster,"A0A6",donor.id)&&caster.spellTimers["A0A6"]==10,"steal charges 40 mana and sets a ten second cooldown "+negative);caster.abilities.Clear();caster.hp=0;Step(m,.25f);
            var moved=recipient.effects.SingleOrDefault(e=>e.id=="QA_STATUS");check(m.OrcStealHits==1&&!donor.effects.Contains(original)&&moved!=null&&moved.left<12&&moved.left>11&&moved.lifetime==20,"steal survives caster death and preserves remaining and original lifetime "+negative);
            check(moved!=null&&moved.armor==(negative?-4:4)&&Math.Abs(m.Stat(recipient,"damage")-(negative?-.2f:.33f))<.001f,"stolen status retains positive or negative modifiers "+negative);
            Step(m,12);check(recipient.effects.Count==0,"stolen status expires on its original schedule "+negative);
        }
        foreach(string invalid in new[]{"physical","permanent","aura","kill_frenzy","duplicate","range","immune","dead","empty"}) {
            m=Fresh(c);caster=Unit(m,0,"h06I",Vector2.zero);caster.abilities.Add("A0A6");var donor=Unit(m,1,"h029",Vector2.right*10);
            var cfg=new JObject{["id"]="QA_STATUS",["kind"]="buff",["duration"]=20,["targets"]="air,ground",["armor"]=4};m.Abilities["QA_STATUS"]=cfg;
            if(invalid=="physical")cfg["physical_control"]=true;if(invalid=="permanent")cfg["undispellable"]=true;if(invalid=="aura"||invalid=="kill_frenzy")cfg["kind"]=invalid;
            if(invalid!="empty")m.ApplyEffect(donor,"QA_STATUS");if(invalid=="duplicate")m.ApplyEffect(caster,"QA_STATUS");if(invalid=="range")donor.position=Vector2.right*24;if(invalid=="immune")donor.spellResist=1;if(invalid=="dead")donor.hp=0;
            check(!m.Cast(caster,"A0A6",donor.id)&&caster.mana==200&&m.OrcStealShots==0,"steal excludes invalid choice without spending mana: "+invalid);
        }
        foreach(string loss in new[]{"expired","refreshed","dispelled","donor_dead","recipient_dead"}) {
            m=Fresh(c);caster=Unit(m,0,"h06I",Vector2.zero);caster.abilities.Add("A0A6");var donor=Unit(m,1,"h029",Vector2.right*10);m.ApplyEffect(donor,"A02M");check(m.Cast(caster,"A0A6"),"steal fixture launches "+loss);caster.abilities.Clear();
            if(loss=="expired")donor.effects[0].left=.01f;if(loss=="refreshed")m.ApplyEffect(donor,"A02M");if(loss=="dispelled")donor.effects.Clear();if(loss=="donor_dead")donor.hp=0;if(loss=="recipient_dead")caster.hp=0;Step(m,.5f);
            check(m.OrcStealHits==0,"in-flight transfer cancels when original status or a participant disappears: "+loss);
        }
    }
}
