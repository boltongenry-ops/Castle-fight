using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public static class Nerubian033Scenarios {
    static HumanMatch m;static JObject own,human;static Action<bool,string> check;
    static void C(bool ok,string message){check(ok,message);if(!ok)throw new Exception(message);}
    static bool Near(float a,float b)=>Mathf.Abs(a-b)<.03f;
    static object Call(string method,params object[] args)=>typeof(HumanMatch).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(m,args);
    static void M(int aspect=-1){m=new HumanMatch((JObject)own.DeepClone(),100000,0,aspect,-1,(JObject)human.DeepClone());}
    static HumanMatch.Unit U(string raw,int side=0,Vector2? at=null){var u=m.Spawn(side,m.Kind(raw),at??Vector2.zero);u.onRoad=true;u.castPause=u.cooldown=10000;return u;}
    static HumanMatch.Unit E(Vector2? at=null){var u=U("h000",1,at??Vector2.right*3);u.maxHp=u.hp=10000;u.armor=u.spellResist=0;m.Lines[u.kind].armorType="normal";return u;}
    static HumanMatch.Hut H(string raw){var h=new HumanMatch.Hut{id=80000+m.Huts.Count,team=0,kind=m.Kind(raw),position=Vector2.zero,paid=true,started=true,hp=10000,maxHp=10000,mana=1000};m.Huts.Add(h);return h;}
    static void Hit(HumanMatch.Unit source,HumanMatch.Unit target,float damage=100)=>Call("ResolveAttack",source.id,source.team,target.id,damage,m.Lines[source.kind].weapons[0],0f,true);
    public static void Run(JObject catalog,JObject enemy,Action<bool,string> report){
        own=catalog;human=enemy;check=report;
        M();foreach(var pair in new[]{new[]{"hNV2","200","25"},new[]{"hNV3","340","32"},new[]{"hNW2","280","29"},new[]{"hNP2","280","29"},new[]{"hNA2","340","32"},new[]{"hNO1","520","41"}}){
            int total=int.Parse(pair[1]);var h=H(pair[0]);h.spent=total;C(Near(m.Interval(h),float.Parse(pair[2],System.Globalization.CultureInfo.InvariantCulture)),"Production uses actual cumulative spending / "+pair[0]);
            C(Near(HumanMatch.F(((JArray)catalog["lines"]).First(r=>(string)r["building"]==pair[0]),"seconds"),m.Interval(h)),"Catalog time matches real production / "+pair[0]);
        }
        M();var victim=E();m.ApplyEffect(victim,"AUN5",0,11);m.ApplyEffect(victim,"AUN5",0,12);Call("TickEffects",victim,1f);C(Near(victim.hp,10000-13.5f),"Two independent strong venoms deal full plus half DPS");C(victim.effects.Count(e=>e.group=="nerub_venom")==2,"Two layers retained");C(Near(m.Stat(victim,"armor"),-3)&&Near(m.Stat(victim,"speed"),-.15f),"Armor and slow never double with venom layers");
        m.ApplyEffect(victim,"AUN5",0,13);C(victim.effects.Count(e=>e.group=="nerub_venom")==2,"Third equal venom source does not add a layer");
        m.ApplyEffect(victim,"AUN4",0,11);C(victim.effects.All(e=>e.id=="AUN5"),"Weak refresh cannot replace a strong layer");
        M(1);victim=E();for(int i=0;i<8;i++){m.ApplyEffect(victim,"AUN4",0,5);Call("TickEffects",victim,.25f);}C(Near(victim.hp,9990),"Neurotoxin is 25 percent and refresh preserves two real ticks");C(victim.effects.Count==1,"One attacker cannot stack itself");
        M();victim=E();m.ApplyEffect(victim,"AUN5",0,1);m.ApplyEffect(victim,"AUN5",0,2);Call("TickEffects",victim,5f);C(Near(victim.hp,10000-67.5f),"Both final poison ticks keep full/half weighting");C(victim.effects.Count==0,"Expired layers removed");
        M();victim=E();victim.spellResist=1;m.ApplyEffect(victim,"AUN5",0,1);C(!victim.effects.Any(),"Venom respects magic immunity");
        M();victim=E();var first=U("hNV1");var second=U("hNV1");Call("NerubMark",victim,0,8f);Hit(first,victim);Hit(second,victim);C(Near(victim.hp,9784),"Two same-frame Nerubian hits each receive eight percent hunt bonus");var spider=U("nN04");float hp=victim.hp;Hit(spider,victim);C(Near(hp-victim.hp,102),"Spider hunt contribution is limited to two percent");hp=victim.hp;m.DamageUnit(victim,100,0);C(Near(hp-victim.hp,100),"Hunt never amplifies spell damage");
        M();var front=U("hNV1");var chamber=H("hNB1");for(int i=0;i<7;i++){chamber.mana=20;chamber.spellTimers.Clear();Call("TickFinalBuilding",chamber);m.FlushFinalBirths();}C(m.NerubBroodCount(0)==20,"All brood sources reserve a common cap of twenty");chamber.mana=20;chamber.spellTimers.Clear();Call("TickFinalBuilding",chamber);C(chamber.mana==20,"No mana lost at full brood cap");C(m.Units.Where(u=>m.Lines[u.kind].unit=="nN04").All(u=>u.noBounty&&u.noCorpse&&u.summoned&&Near(u.summonLeft,16)),"Spiderlings remain temporary, bountyless and corpseless");
        M(0);front=U("hNV1");chamber=H("hNB1");chamber.mana=20;Call("TickFinalBuilding",chamber);m.FlushFinalBirths();var child=m.Units.Last();C(m.NerubBroodCount(0)==3&&Near(child.maxHp,96)&&Near(child.summonLeft,20),"Queen preserves normal chamber spell and buffs all produced brood");Call("TickNerubQueen",100f);C(m.NerubBroodCount(0)==3,"Removed replacement aspect timer never generates extra brood");
        m.DamageUnit(child,10000,1,"chaos",true,true);C(!m.Corpses.Any(c=>c.unit==child),"Dead brood creates no resurrectable corpse");m.Corpses.Add(new HumanMatch.Corpse{unit=child,left=90});C(m.Raise(0,Vector2.zero,100,10,false)==0,"Legacy spider corpses also cannot resurrect as permanent units");
        M();var priest=U("hNS2");priest.castPause=0;priest.mana=30;front=U("hNV1",0,Vector2.right);front.hp=100;child=U("nN04",0,Vector2.up);child.hp=1;var extra=U("hNV1",0,Vector2.up*2);extra.hp=200;C(m.Cast(priest,"ANH2"),"Priest can cast for thirty mana");C(front.effects.Any(e=>e.group=="nerub_cocoon")&&!child.effects.Any(e=>e.group=="nerub_cocoon"),"Cocoon prioritizes primary fighters over a lower-health spider");C(Near(extra.hp,240),"Nurse heals one other primary ally by forty");C(Near(priest.mana,0),"Cocoon charges correct mana");
        M();var queen=U("hNO1");queen.castPause=0;queen.mana=40;victim=E();for(int i=0;i<6;i++)U("hNV1",0,Vector2.up*i);child=U("nN04",0,Vector2.right*4);C(m.Cast(queen,"AUC2"),"Matriarch casts at forty mana");C(m.Units.Count(u=>u.team==0&&u.effects.Any(e=>e.id=="ANCF"))==6&&!child.effects.Any(e=>e.id=="ANCF"),"Six primary fighters get command before spiderlings");C(m.Units.SelectMany(u=>u.effects).Where(e=>e.id=="ANCF").All(e=>Near(e.left,6)),"Command lasts six seconds");
        M();front=U("hNV1");front.hp=100;extra=U("hNV1",0,Vector2.up);extra.hp=100;var tomb=H("hNL1");victim=E();victim.hp=120;Call("TickFinalBuilding",tomb);Call("TickEffects",victim,6f);C(victim.hp==0&&Near(front.hp,160)&&Near(extra.hp,160),"Plague lethal last tick retains exactly one healing reward");m.FlushFinalBirths();C(m.NerubBroodCount(0)==1,"Organic plague death creates one spider");
        M();front=U("hNV1");tomb=H("hNL1");victim=E();m.Lines[victim.kind].stats["unit_types"]="Mechanical";victim.hp=120;Call("TickFinalBuilding",tomb);C(victim.effects.Any(e=>e.id=="AUPs"),"Plague damages mechanical targets");Call("TickEffects",victim,6f);m.FlushFinalBirths();C(m.NerubBroodCount(0)==0,"Mechanical plague death cannot hatch organic brood");
        M();front=U("hNV1");front.hp=1;tomb=H("hNL1");var crowd=Enumerable.Range(0,6).Select(i=>E(Vector2.right*3+Vector2.up*(i*.1f))).ToArray();Call("TickFinalBuilding",tomb);C(crowd.All(u=>u.effects.Any(e=>e.id=="AUPs")),"Tomb affects six different targets");foreach(var e in crowd)m.DamageUnit(e,100000,0,"chaos",true,true);m.FlushFinalBirths();C(m.NerubBroodCount(0)==3&&Near(front.hp,181),"Plague rewards at most three deaths per cast");
        foreach(string raw in new[]{"hNT1","hNT2"}){
            M();var tower=H(raw);tower.mana=0;var a=E(Vector2.right*5);var b=E(Vector2.right*6);var outside=E(Vector2.right*28);m.TickBuilding(tower,.05f);m.TickBuilding(tower,1f);C(m.Shots.Count==2&&m.Shots.Select(s=>s.target).Distinct().Count()==2,"Two distinct real tower projectiles / "+raw);C(m.Shots.All(s=>s.target!=outside.id),"Secondary projectile obeys range / "+raw);var shots=m.Shots.OrderByDescending(s=>s.damage).ToArray();C(Near(shots[1].damage/shots[0].damage,.6f),"Second target gets sixty percent damage / "+raw);Call("TickShots",2f);C(a.hp<10000&&b.hp<10000&&a.effects.Any(e=>e.group=="nerub_venom")&&b.effects.Any(e=>e.group=="nerub_venom"),"Both actual impacts apply tower poison / "+raw);
            M();tower=H(raw);a=E(Vector2.right*5);m.TickBuilding(tower,.05f);m.TickBuilding(tower,1f);C(m.Shots.Count==1,"Single target never receives a duplicate shot / "+raw);
        }
        M();var killer=U("hNP1");for(int i=0;i<3;i++){victim=E();victim.hp=1;Hit(killer,victim);}C(killer.nerubScatterHits==0,"Killing blows advance the three-hit scatter counter");
    }
}
}
