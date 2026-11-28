using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    sealed class NatureJob {public string kind,id;public int team,source,count;public float left;public Vector2 center;public readonly List<int> trees=new List<int>();}
    public sealed class NatureCyclone {public Unit actor;public Vector2 origin,destination;public float left,wander;public int target=-1;}
    readonly List<NatureJob> natureJobs=new List<NatureJob>();
    // Source n02E has umvs=0; Unity effect movement uses an explicit 150 WC3/s fallback.
    public readonly List<NatureCyclone> NatureCyclones=new List<NatureCyclone>();
    public int NatureDispels,NatureEnts,NatureSpirits,NatureTaunts,NatureBanishes;
    public bool Ethereal(Unit unit){foreach(var e in unit.effects)if(e.left>0&&(bool?)e.config?["ethereal"]==true)return true;return false;}
    public bool Disabled(Unit unit){foreach(var e in unit.effects)if(e.left>0&&(bool?)e.config?["disable"]==true)return true;return false;}
    bool NatureEligible(Unit unit,int team)=>unit.hp>0&&unit.team!=team&&(bool?)Lines[unit.kind].stats["invulnerable"]!=true&&!unit.abilities.Contains("A08H")&&(((string)Lines[unit.kind].stats["unit_types"]??"").Contains("sapper")||unit.natureSpirit);
    int UnitKind(string raw){int kind=Kind(raw);return kind>=0?kind:Array.FindIndex(Lines,l=>l.unit==raw);}
    Unit NatureSummon(int team,string raw,Vector2 at,float lifetime) {
        int kind=UnitKind(raw);if(kind<0)return null;var unit=Spawn(team,kind,at);unit.summoned=true;unit.noBounty=raw=="n02E";unit.summonLeft=lifetime;unit.onRoad=true;unit.leaveCastleLane=false;return unit;
    }
    public void NatureDispel(Unit unit) {
        unit.effects.RemoveAll(e=>((string)e.config?["kind"]=="buff"||(string)e.config?["kind"]=="regen")&&(bool?)e.config?["undispellable"]!=true);
        NatureDispels++;PulseAt(unit.position,1.6f,Color.cyan,"Рассеивание");
    }
    void NatureOnHit(Unit source,Unit target) {
        string raw=Lines[source.kind].unit;
        if(raw=="n02B")source.abilities.Remove("A0C8");
        float chance=raw=="e006"?.15f:raw=="e009"?.2f:raw=="e00C"?.25f:0;
        if(target.hp<=0||chance==0||Roll()>=chance)return;
        if(raw=="e00C"){foreach(var u in Units)if(NatureEligible(u,source.team)&&Vector2.Distance(u.position,target.position)<=100f/30)NatureDispel(u);}
        else NatureDispel(target);
    }
    void NatureOnDamage(Unit unit) {
        string raw=Lines[unit.kind].unit;if(unit.hp<=0||(raw!="n029"&&raw!="n02B")||!unit.abilities.Contains("A0C5"))return;
        bool elder=raw=="n02B";
        if(Roll()<(elder?.2f:.15f)){ApplyEffect(unit,elder?"A0B8":"A0B7");ApplyEffect(unit,elder?"A0B6":"A0B5");}
        if(unit.hp<255&&unit.abilities.Remove("A0C4")){unit.bearPhase=0;unit.bearLeft=5;unit.strikeLeft=-1;unit.pendingSpell=null;unit.castLeft=-1;unit.combatTarget=-1;}
        else if(unit.bearPhase==1)WakeNatureBear(unit);
    }
    void WakeNatureBear(Unit unit){unit.effects.RemoveAll(e=>e.id=="Asla");unit.abilities.Remove(Lines[unit.kind].unit=="n02B"?"A0C7":"A0C6");unit.bearPhase=2;}
    bool TickNatureBear(Unit unit,float dt) {
        if(unit.bearPhase<0)return false;unit.bearLeft-=dt;
        if(unit.bearPhase==0) {
            if(unit.bearLeft>0){if(unit.stun<=0&&!Rooted(unit)&&!Disabled(unit)){var goal=BuilderWaypoint(unit.position,CastlePosition(unit.team));unit.direction=(goal-unit.position).normalized;unit.position=Vector2.MoveTowards(unit.position,goal,370f/30*dt);unit.pose="Walk";}return true;}
            unit.effects.Clear();unit.effects.Add(new Effect {id="Asla",group="Asla",left=10,lifetime=10,sourceTeam=unit.team,config=new JObject {["kind"]="debuff",["name"]="Лечебный сон",["disable"]=true,["undispellable"]=true}});
            Grant(unit,Lines[unit.kind].unit=="n02B"?"A0C7":"A0C6");if(Lines[unit.kind].unit=="n02B")Grant(unit,"A0C8");unit.bearPhase=1;unit.bearLeft=Mathf.Max(0,10+unit.bearLeft);
        }
        if(unit.bearLeft<=0){WakeNatureBear(unit);unit.bearPhase=-1;return false;}
        if(unit.bearPhase==1){unit.pose="Stand";return true;}return false;
    }
    public bool CastNatureTrees(Unit source) {
        var cfg=Abilities["A0BX"];if(!HasSpell(source,"A0BX"))return false;
        using(var snapshot035_1=SimulationBuffers035.Take(Units))foreach(var unit in snapshot035_1)if(unit!=source&&unit.hp>0&&!Flying(unit)&&((string)Lines[unit.kind].stats["unit_types"]??"").Contains("sapper")&&Vector2.Distance(unit.position,source.position)<=144f/30){float angle=(135+Roll()*99+180*unit.team)*Mathf.Deg2Rad;unit.position=unit.previous=source.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*((160+Roll()*58)/30);UnitMoved033(unit);PulseAt(unit.position,1,Color.green,"Перемещение");}
        var job=new NatureJob {kind="trees",team=source.team,source=source.id,id="A0BX",center=source.position,left=F(cfg,"delay",3)};
        for(int i=0;i<5;i++){float angle=i*Mathf.PI*2/5;var tree=NatureSummon(source.team,"YTct",source.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*4,0);if(tree!=null){tree.noBounty=true;job.trees.Add(tree.id);}}
        natureJobs.Add(job);PulseAt(source.position,4,Color.green,"Пробуждение рощи",3);return true;
    }
    bool CastNatureTaunt(Unit source,string id) {
        var cfg=Abilities[id];var targets=Units.Where(u=>Valid(u,source.team,cfg,false)&&Vector2.Distance(u.position,source.position)<=F(cfg,"area")).Take((int?)cfg["count"]??10).ToArray();if(targets.Length==0)return false;
        foreach(var u in targets)u.combatTarget=source.id;NatureTaunts++;PulseAt(source.position,F(cfg,"area"),Color.yellow,"Оскорбление");return true;
    }
    bool CastNatureEnts(Unit source) {
        var cfg=Abilities["A0BU"];if(!HasSpell(source,"A0BU"))return false;
        for(int i=0;i<((int?)cfg["count"]??2);i++)if(NatureSummon(source.team,(string)cfg["summon"],source.position+Vector2.up*(i==0?1:-1),F(cfg,"duration"))!=null)NatureEnts++;
        source.castPause=Mathf.Max(source.castPause,1);PulseAt(source.position,3,Color.green,"Призыв энтов");return true;
    }
    void NatureAfterHeal(Unit source,Unit target,string id) {
        var cfg=Abilities[id];float armor=F(cfg,"permanent_armor");if(armor>0&&target.permanentHeals.Add(id)){target.armor+=armor;PulseAt(target.position,1,Color.yellow,"+"+armor+" брони");}
        if(Lines[source.kind].unit=="e009")natureJobs.Add(new NatureJob {kind="keeper",source=source.id,left=.6f});
    }
    void TickNatureBuilding(Hut hut) {
        foreach(var id in Lines[hut.kind].abilities){var cfg=Abilities[id];string kind=(string)cfg?["kind"];if(kind!="nature_banish"&&kind!="nature_cyclone"&&kind!="nature_spirits")continue;
            if(hut.mana<F(cfg,"mana")||hut.spellTimers.TryGetValue(id,out float wait)&&wait>0)continue;
            var targets=Units.Where(u=>NatureEligible(u,hut.team)).ToArray();if(kind!="nature_spirits"&&targets.Length==0)continue;
            if(kind=="nature_banish") {var target=targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];if(!NagaShieldBlock(target)&&Valid(target,hut.team,cfg,false)){ApplyEffect(target,(string)cfg["effect_id"],hut.team);NatureBanishes++;PulseAt(target.position,2,new Color(.5f,.8f,1),"Астрал",2);}}
            if(kind=="nature_cyclone") {var target=targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];var actor=NatureSummon(hut.team,"n02E",target.position,0);if(actor!=null)NatureCyclones.Add(new NatureCyclone {actor=actor,origin=actor.position,destination=actor.position,left=F(cfg,"duration",7)});}
            if(kind=="nature_spirits"){natureJobs.Add(new NatureJob {kind="spirits",id=id,team=hut.team,left=0});TickNatureJobs(0);}
            hut.mana-=F(cfg,"mana");hut.spellTimers[id]=BuildingCooldown(hut,F(cfg,"cd"));hut.attackSerial++;hut.attackPoseLeft=.6f;SpellsCast++;
        }
    }
    void TickNatureJobs(float dt) {
        using(var snapshot035_2=SimulationBuffers035.Take(natureJobs))foreach(var job in snapshot035_2) {
            job.left-=dt;if(job.left>0)continue;
            if(job.kind=="spirits") {
                var cfg=Abilities[job.id];while(job.left<=.00001f&&job.count<((int?)cfg["count"]??6)) {float x=(job.team==0?-1:1)*(2048+Roll()*(7680-2048))/30;var spirit=NatureSummon(job.team,(string)cfg["summon"],new Vector2(x,job.count%2==0?-64:64),F(cfg,"duration",42));if(spirit!=null){spirit.natureSpirit=true;spirit.noBounty=false;spirit.abilities.Remove("Awha");NatureSpirits++;}job.count++;job.left+=F(cfg,"delay",.3f);}if(job.count>=((int?)cfg["count"]??6))natureJobs.Remove(job);
            } else {
                natureJobs.Remove(job);var source=FindUnit(job.source);
                if(job.kind=="keeper"){if(source!=null&&source.stun<=0&&!Disabled(source)&&Cast(source,"A0BU"))source.spellTimers["A0BU"]=SpellCooldown(source,"A0BU");}
                else for(int i=0;i<job.trees.Count;i++){var tree=Units.FirstOrDefault(u=>u.id==job.trees[i]);if(tree==null)continue;if(tree.hp>0&&source!=null){float angle=i*Mathf.PI*2/5;if(NatureSummon(job.team,"e00D",job.center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(112f/30),F(Abilities[job.id],"duration",20))!=null)NatureEnts++;}tree.hp=0;tree.deathProcessed=true;tree.deadAge=100;}
            }
        }
        using(var snapshot035_3=SimulationBuffers035.Take(NatureCyclones))foreach(var cyclone in snapshot035_3) {
            float step=Mathf.Min(dt,cyclone.left);cyclone.left-=dt;cyclone.wander-=step;var actor=cyclone.actor;
            var target=FindUnit(cyclone.target);if(cyclone.wander<=0||target==null||!NatureEligible(target,actor.team)){target=Units.Where(u=>NatureEligible(u,actor.team)).OrderBy(u=>(u.position-actor.position).sqrMagnitude).FirstOrDefault();cyclone.target=target?.id??-1;cyclone.wander=F(Abilities["Atwa"],"duration",2);if(target==null){float angle=Roll()*Mathf.PI*2,range=Mathf.Sqrt(Roll())*F(Abilities["Atwa"],"range");cyclone.destination=cyclone.origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*range;cyclone.destination=new Vector2(Mathf.Clamp(cyclone.destination.x,-256,256),Mathf.Clamp(cyclone.destination.y,-64,64));}}
            if(target!=null)cyclone.destination=target.position;actor.previous=actor.position;actor.position=Vector2.MoveTowards(actor.position,cyclone.destination,(Lines[actor.kind].speed>0?Lines[actor.kind].speed:150f/30)*step);actor.pose="Stand";
            using(var snapshot035_4=SimulationBuffers035.Take(Units))foreach(var unit in snapshot035_4)if(unit.hp>0&&unit.team!=actor.team&&(bool?)Lines[unit.kind].stats["invulnerable"]!=true&&Lines[unit.kind].unit!="YTct") {float gap=Vector2.Distance(unit.position,actor.position);var slow=Abilities["Aasl"];if(gap<=F(slow,"area")&&unit.spellResist<1){unit.effects.RemoveAll(e=>e.id=="Aasl");unit.effects.Add(new Effect {id="Aasl",group="Aasl",left=.3f,lifetime=.3f,sourceTeam=actor.team,speed=F(slow?["profile"]?["effects"],"speed"),haste=F(slow?["profile"]?["effects"],"haste"),config=new JObject {["kind"]="debuff",["name"]="Вихрь"}});}if(gap<=F(Abilities["A0CE"],"area"))DamageUnit(unit,F(Abilities["A0CE"]?["fields"],"Tdg1")*step,actor.team);}
            var building=Abilities["Atdg"];using(var snapshot035_5=SimulationBuffers035.Take(Huts))foreach(var hut in snapshot035_5)if(hut.team!=actor.team&&hut.paid&&hut.hp>0&&Vector2.Distance(hut.position,actor.position)<=F(building,"area"))DamageHut(hut,F(building?["fields"],"Tdg1")*step,"magic",actor.team,true,sourceId:actor.id);
            if(Vector2.Distance(CastlePosition(1-actor.team),actor.position)<=F(building,"area"))DamageCastle(1-actor.team,F(building?["fields"],"Tdg1")*step,"magic",true,actor.id);
            if(cyclone.left<=0||actor.hp<=0){actor.hp=0;actor.deadAge=100;actor.deathProcessed=true;NatureCyclones.Remove(cyclone);}
        }
    }
}
}
