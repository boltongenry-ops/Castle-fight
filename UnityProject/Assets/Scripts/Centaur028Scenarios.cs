using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public static class Centaur028Scenarios {
    public static JArray Run(JObject catalog,JObject human,Action<bool,string> check){
        var result=new JArray();HumanMatch m=null;
        void C(bool ok,string text){check(ok,text);result.Add(new JObject{["ok"]=ok,["name"]=text});}
        void Call(string method,params object[] args)=>typeof(HumanMatch).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(m,args);
        void Reset(int aspect=-1){m=new HumanMatch((JObject)catalog.DeepClone(),10000,1,aspect,-1,(JObject)human.DeepClone());typeof(HumanMatch).GetField("botAge",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(m,100000f);m.Wood[0]=10000;}
        HumanMatch.Hut H(string raw,int team=0){int k=m.Kind(raw);var h=new HumanMatch.Hut{id=9000+m.Huts.Count,kind=k,team=team,position=new Vector2(team==0?-145:145,18+m.Huts.Count*3),hp=1100,maxHp=1100,paid=true,spawnLeft=.01f,spent=m.Lines[k].cost,baseCost=m.Lines[k].cost};m.Huts.Add(h);return h;}
        HumanMatch.Unit U(string actor,int team=0,Vector2? at=null){int k=Array.FindIndex(m.Lines,l=>l.unit==actor);var u=m.Spawn(team,k,at??new Vector2(-40,0));u.onRoad=true;return u;}
        HumanMatch.Unit D(bool air=false,bool immune=false){int k=Array.FindIndex(m.Lines,l=>l.Producer&&((bool?)l.stats["flying"]??false)==air&&!(l.unit??"").StartsWith("cU")&&l.weapons.Any(w=>w.type!="normal"));var u=m.Spawn(1,k,new Vector2(-35,0));u.hp=u.maxHp=100000;u.castPause=u.cooldown=100000;if(immune)u.spellResist=1;return u;}
        void Time(float seconds){for(int i=0;i<(int)Math.Ceiling(seconds/.05);i++)m.Step(.05f);}
        Reset();C(m.BaseKinds.Length==11&&catalog["lines"].Count()==19,"Centaur catalog has eleven base buildings and eight upgrades");
        C(m.IsCentaur(0)&&!m.IsCentaur(1)&&m.Lines.Select(l=>l.building).Distinct().Count()==m.Lines.Length,"Mixed race identities and unique raw codes");
        C(m.BaseKinds.SelectMany(k=>m.Lines[k].upgrades).All(raw=>m.Kind(raw)>=0),"Every centaur upgrade resolves");
        C(m.Lines.Where(l=>(l.unit??"").StartsWith("cU")).SelectMany(l=>HumanMatch.Strings(l.stats["abilities"])).All(id=>m.Abilities[id]!=null),"Every fighter ability has a description and rule record");
        C(m.TryBuild(0,m.Kind("c001"),18,12,out _),"Ordinary construction accepts the centaur opening camp");var queued=m.Huts.Last();float gold=m.Gold[0];C(!m.TryBuild(0,m.Kind("c001"),18,12,out _)&&m.Gold[0]==gold,"Occupied construction does not spend gold");
        m.Builders[0].position=queued.position+new Vector2(0,-2.4f);Time(1.6f);C(queued.paid&&m.Gold[0]==gold-100&&m.Wood[0]==10100&&Mathf.Abs(m.Income(0)-7)<.001f,"Construction pays native economy and grants exact wood and income");
        C(m.Upgrade(0,queued.id,m.Kind("c011"),out _)&&queued.spent==280&&Mathf.Abs(m.Interval(queued)-29)<.001f,"Upgrade pays cumulative investment and lengthens the production interval");
        Reset();var first=H("c001");Time(.1f);C(m.Units.Count==1,"Camp produces automatically with no gathering command or delay");
        Reset();first=H("c001");m.PauseHut(0,first.id,true);Time(2);C(m.Units.Count==0,"Ordinary pause stops automatic centaur production");m.PauseHut(0,first.id,false);Time(.1f);C(m.Units.Count==1,"Ordinary resume releases the next fighter once");
        Reset();first=H("c001");for(int i=0;i<160;i++){var u=U("cU01",0,new Vector2(-150,0));u.castPause=100000;}Time(.1f);C(m.ArmyCount(0)==160&&first.spawnLeft==0,"Normal production obeys the army cap");m.Units[0].hp=0;Time(.1f);C(m.ArmyCount(0)==160,"One free slot allows exactly one automatic fighter");
        Reset(1);var guarded=U("cU01");var threat=D();Call("TickCentaur",.25f);C(guarded.effects.Any(e=>e.id=="CT09"&&e.armor==3),"Caravan aspect automatically guards the first encounter");guarded.effects.Clear();Call("TickCentaur",.3f);C(!guarded.effects.Any(e=>e.id=="CT09"),"Caravan armor cannot retrigger after the first encounter");
        Reset();var totem=H("c00B");totem.position=new Vector2(-40,0);totem.mana=45;var sandVictim=D();Call("TickCentaur",.25f);C(m.CentaurSands.Count==1&&totem.mana==0&&sandVictim.effects.Any(e=>e.id=="CT16"),"Totem spends mana and slows a nearby ground enemy");Call("TickCentaur",.3f);C(m.CentaurSandCasts==1,"Totem respects its twelve-second cooldown");
        Reset();totem=H("c01B");totem.position=new Vector2(-40,0);totem.mana=45;sandVictim=D();Call("TickCentaur",.25f);C(sandVictim.effects.Any(e=>e.id=="CT17"&&e.speed==-.4f),"Upgraded totem applies forty-percent slow");var airVictim=D(true);var immuneVictim=D(false,true);Call("TickCentaur",.3f);C(!airVictim.effects.Any(e=>e.id=="CT17")&&!immuneVictim.effects.Any(e=>e.id=="CT17"),"Quicksand excludes flyers and magic immunity");
        Reset();totem=H("c00B");totem.position=new Vector2(-40,0);totem.mana=45;totem.paused=true;sandVictim=D();Call("TickCentaur",.25f);C(m.CentaurSandCasts==0&&totem.mana==45,"Paused totem cannot cast or spend mana");totem.paused=false;m.NetworkReplica=true;Call("TickCentaur",.3f);C(m.CentaurSandCasts==0,"Replica cannot run centaur spell authority");m.NetworkReplica=false;m.Winner=1;Call("TickCentaur",.3f);C(m.CentaurSandCasts==0,"Finished match cannot run centaur spell authority");
        Reset();totem=H("c00B");totem.position=new Vector2(-40,0);totem.mana=45;D();m.VisionTest=(team,point)=>false;Call("TickCentaur",.25f);C(m.CentaurSandCasts==0&&totem.mana==45,"Totem cannot target an enemy hidden by fog or spend mana on it");
        Reset();var runner=U("cU01");for(int i=0;i<62;i++)m.TickUnit(runner,.05f);C(m.CentaurChargeProgress(runner)>=1,"Continuous movement charges the centaur opening");float damage=m.Lines[runner.kind].weapons[0].damage;Call("BeginAttack",runner,m.Lines[runner.kind].weapons[0],-1);C(Mathf.Abs(runner.shotDamage-damage*1.4f)<.01f&&m.CentaurCharges==1,"Charged opening deals forty percent more weapon damage once");Call("BeginAttack",runner,m.Lines[runner.kind].weapons[0],-1);C(Mathf.Abs(runner.shotDamage-damage)<.01f&&m.CentaurCharges==1,"An immediate second attack gets no free charge");
        Reset(0);runner=U("cU01");for(int i=0;i<62;i++)m.TickUnit(runner,.05f);Call("BeginAttack",runner,m.Lines[runner.kind].weapons[0],-1);C(Mathf.Abs(runner.shotDamage-m.Lines[runner.kind].damage*1.6f)<.01f,"Spear aspect raises the opening bonus to sixty percent");
        Reset();runner=U("cU02");for(int i=0;i<62;i++)m.TickUnit(runner,.05f);Call("BeginAttack",runner,m.Lines[runner.kind].weapons[0],-1);C(runner.effects.Any(e=>e.id=="CT13"&&e.armor==4),"A guard gains its temporary armor from a real charge");
        Reset();runner=U("cU01");for(int i=0;i<62;i++)m.TickUnit(runner,.05f);runner.stun=1;m.TickUnit(runner,.05f);C(m.CentaurChargeProgress(runner)==0,"Stun cancels the accumulated opening charge");
        Reset();runner=U("cU01");for(int i=0;i<62;i++)m.TickUnit(runner,.05f);m.ApplyEffect(runner,"CT12",1);m.TickUnit(runner,.05f);C(m.CentaurChargeProgress(runner)==0,"Root also cancels the charge");
        Reset();var hunter=U("cU04");var victim=D(true);Call("CentaurAfterWeapon",hunter,victim,20f);C(!m.Flying(victim)&&m.Rooted(victim)&&m.CentaurNets==1,"Hunter hit grounds and roots an airborne enemy");var air=D(true);Call("CentaurAfterWeapon",hunter,air,20f);C(m.Flying(air)&&m.CentaurNets==1,"Harpoon retains its eight-second internal cooldown");
        Reset();hunter=U("cU04");victim=D(true,true);Call("CentaurAfterWeapon",hunter,victim,20f);C(m.Flying(victim)&&victim.effects.Count==0,"Harpoon respects magic immunity");
        Reset();var witch=U("cU05");victim=D();float mana=witch.mana;Call("TickCentaur",.25f);C(m.CentaurClouds.Count==1&&witch.mana==mana-35&&m.MissChance(victim)==.3f,"Witch pays thirty-five mana and impairs enemy ranged attacks");
        Reset();witch=U("cU05");victim=D(true,true);Call("TickCentaur",.25f);C(victim.effects.Count==0,"Sand does not affect magic-immune enemies");
        Reset();var drum=U("cU06");runner=U("cU01");Call("UpdateAuras");C(runner.auraIds.Contains("CT05")&&Mathf.Abs(runner.auraHaste-.1f)<.001f,"Drummer grants the described nonstacking attack rhythm");for(int i=0;i<42;i++)m.TickUnit(runner,.05f);C(m.CentaurChargeProgress(runner)>=1,"Rhythm shortens continuous run-up to two seconds");
        Reset();U("cU06");U("cU16");U("cU16");runner=U("cU01");Call("UpdateAuras");C(Mathf.Abs(runner.auraHaste-.15f)<.001f,"Senior drummer grants fifteen percent; overlapping rhythms use the strongest");
        Reset();witch=U("cU05");victim=D();U("cU05");Call("TickCentaur",.25f);C(victim.effects.Count(e=>e.id=="CT11")==1&&Mathf.Abs(m.MissChance(victim)-.3f)<.001f,"Overlapping sand clouds apply one thirty-percent penalty");Time(5.6f);C(!victim.effects.Any(e=>e.id=="CT11"),"Sand penalty expires after leaving or outlasting the cloud");
        Reset();runner=U("cU01");m.ApplyEffect(runner,"CT11",1);var miss=(float)typeof(HumanMatch).GetMethod("CentaurMissChance",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(m,new object[]{runner,m.Lines[runner.kind].weapons[0]});C(miss==0,"Sand does not impose a ranged miss penalty on a melee strike");
        Reset();var khan=U("cU0A");runner=U("cU01",0,new Vector2(-5,0));for(int i=0;i<62;i++)m.TickUnit(khan,.05f);Call("BeginAttack",khan,m.Lines[khan.kind].weapons[0],-1);C(runner.effects.Any(e=>e.id=="CT10"),"Khan charge grants the nearby army a temporary battle cry");
        Reset();H("c009");H("c009");runner=U("cU01",0,new Vector2(-145,0));runner.hp=100;Call("TickCentaur",.25f);C(runner.hp==108&&m.CentaurHeals==1,"Multiple supply buildings cannot double-heal one fighter in a pulse");
        // Actual adaptive bot, ordinary gold and construction, both teams use the new catalog.
        m=new HumanMatch((JObject)catalog.DeepClone(),250,1);for(int i=0;i<1800;i++){m.Winner=-2;m.CastleHp[0]=m.CastleHp[1]=HumanMatch.CastleMax;m.Step(.5f);}C(m.Huts.Where(h=>h.team==1&&h.paid).Select(h=>m.Lines[h.kind].building).Distinct().Count()>=3&&m.Upgrades>0,"Centaur bot develops several building families and upgrades on ordinary income");C(m.Gold.All(g=>g>=-.001f)&&m.Wood.All(w=>w>=0),"Centaur bot never spends unavailable resources");
        return result;
    }
}
}
