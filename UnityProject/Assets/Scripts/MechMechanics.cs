using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    public sealed class MechJob {public string kind;public int team,source;public Vector2 point;public float left;public JObject config;}
    public readonly List<MechJob> MechJobs=new List<MechJob>();
    public int MechMines,MechExplosions,MechGrenades,MechRepairs,MechShields,MechCleanses,MechEmpowered,MechChains,MechHeatBursts;
    public readonly double[] MechRepairGold=new double[2],MechRepairWood=new double[2];
    JObject mechRepairValues,mechPayloads;float mechCleanseClock;public float MechCleanseRemaining=>4-mechCleanseClock;
    public bool MechMine(Unit u)=>Lines[u.kind].unit=="n025";
    void RebuildMechPowerCache() {
        var s=Opt(this);s.mechPos0.Clear();s.mechPos1.Clear();
        for(int i=0;i<Huts.Count;i++) {var p=Huts[i];if(p.hp<=0||!p.paid)continue;if(Lines[p.kind].building!="h09T")continue;(p.team==0?s.mechPos0:s.mechPos1).Add(p.position);}
        s.mechTick=TickCount;
    }
    public bool MechPowered(Hut building) {
        float r=F(Abilities["A09S"],"area",6);float rsqr=r*r;
        var s=Opt(this);
        if(s.mechTick==TickCount) {
            var list=building.team==0?s.mechPos0:s.mechPos1;
            for(int i=0;i<list.Count;i++)if((list[i]-building.position).sqrMagnitude<=rsqr)return true;
            return false;
        }
        for(int i=0;i<Huts.Count;i++) {var p=Huts[i];if(p.hp<=0||!p.paid||p.team!=building.team)continue;if(Lines[p.kind].building!="h09T")continue;if((p.position-building.position).sqrMagnitude<=rsqr)return true;}
        return false;
    }
    public bool MechCastlePowered(int team) {
        float r=F(Abilities["A09S"],"area",6);float rsqr=r*r;Vector2 c=CastlePosition(team);
        var s=Opt(this);
        if(s.mechTick==TickCount) {
            var list=team==0?s.mechPos0:s.mechPos1;
            for(int i=0;i<list.Count;i++)if((list[i]-c).sqrMagnitude<=rsqr)return true;
            return false;
        }
        for(int i=0;i<Huts.Count;i++) {var p=Huts[i];if(p.hp<=0||!p.paid||p.team!=team)continue;if(Lines[p.kind].building!="h09T")continue;if((p.position-c).sqrMagnitude<=rsqr)return true;}
        return false;
    }
    bool MechFighter(Unit u)=>u!=null&&u.hp>0&&!Hidden(u)&&!Invulnerable(u)&&!ElementalIllusion(u)&&Lines[u.kind].unit!="n02E"&&Lines[u.kind].unit!="YTct";
    JObject Ranked(Unit u,string id) {var cfg=(JObject)Abilities[id];var ranks=cfg?["ranks"] as JArray;return ranks!=null&&ranks.Count>0?(JObject)ranks[Mathf.Clamp(u.abilityLevels.TryGetValue(id,out int level)?level-1:0,0,ranks.Count-1)]:cfg;}
    public float CastleArmorFor(int team)=>CastleArmor+(MechCastlePowered(team)?F(Abilities["A09S"]?["fields"],"Had1"):0);
    public float MechBuildingArmor(Hut h)=>MechPowered(h)?F(Abilities["A09S"]?["fields"],"Had1"):0;
    float MechBuildingDamage(Hut h)=>MechPowered(h)?1+F(Abilities["A09V"]?["fields"],"Akb1"):1;
    void MechCreated(Unit u,bool production,Hut producer) {
        if(MechMine(u)){u.noBounty=true;u.noCorpse=true;u.onRoad=true;u.pose="Stand";}
        if(Lines[u.kind].unit=="n01U"){u.summonLeft=120;u.summoned=true;u.noBounty=true;}
        if(!production)return;if(Lines[u.kind].unit=="h06U")u.mana=Roll()*50;
        if(producer!=null&&MechPowered(producer))EmpowerMech(u);
    }
    public void EmpowerMech(Unit u) {if(u==null||u.hp<=0||u.mechEmpowered)return;u.mechEmpowered=true;u.maxHp+=100;u.hp+=100;Grant(u,"A09Q");Grant(u,"A09R");MechEmpowered++;}
    void TickMech(float dt) {
        var pending=MechJobs.ToArray();mechCleanseClock+=dt;while(mechCleanseClock>=4){mechCleanseClock-=4;MechCleanse();}
        foreach(var u in Units.ToArray())if(u.hp>0&&MechMine(u)) {
            u.mechMineAge+=dt;if(u.mechMineAge<F(Abilities["A098"]?["fields"],"Min1",5))continue;
            if(Units.Any(v=>v!=u&&MechTrigger(v,u.team)&&Vector2.Distance(v.position,u.position)<=F(Abilities["A098"],"range")))MechSuicide(u);
        }
        foreach(var job in pending){job.left-=dt;if(job.left>.000001f)continue;MechJobs.Remove(job);
            if(job.kind=="mine"){int kind=Kind("n025");if(kind>=0){Spawn(job.team,kind,job.point);MechMines++;}}
            else if(job.kind=="grenade")MechGrenade(job.team,job.point);
            else if(job.kind=="blast")MechBlast(job.team,job.point,job.config);
            else if(job.kind=="sapper"){var source=FindUnit(job.source);if(source!=null){MechBlast(source.team,source.position,(JObject)Abilities["A099"]);source.mechActiveExplosion=true;MechSuicide(source);}}
        }
    }
    bool MechTrigger(Unit u,int team,bool grenade=false)=>u.hp>0&&u.team!=team&&!Hidden(u)&&!Flying(u)&&(!grenade||!u.abilities.Contains("A08H"))&&!Invulnerable(u)&&((string)Lines[u.kind].stats["unit_types"]??"").ToLowerInvariant().Contains("sapper");
    public void MechCleanse() {
        foreach(var plant in Huts.Where(p=>p.hp>0&&p.paid&&Lines[p.kind].building=="h09T").ToArray())foreach(var h in Huts.ToArray())if(h.hp>0&&h.paid&&Lines[h.kind].building!="h09T"&&Vector2.Distance(h.position,plant.position)<=252f/30) {
            int removed=NorthernFreezes.RemoveAll(v=>v.target==HutTarget(h));removed+=HellCurses.RemoveAll(v=>v.target==h.id);
            if(removed>0){MechCleanses++;PulseAt(h.position,2,Color.cyan,"Очищение");}
        }
    }
    bool MechUnitReady(Unit u,JToken cfg)=>((string)cfg["kind"]=="mech_mines")||Units.Any(v=>Valid(v,u.team,cfg,false)&&((string)cfg["kind"]=="auto_fire"||!IsNagaHero(v))&&Vector2.Distance(v.position,u.position)<=F(cfg,"range"));
    bool CastMechUnit(Unit source,JToken cfg,Unit suggested) {
        string kind=(string)cfg["kind"];Vector2 point;
        if(kind=="mech_mines"){float angle=Roll()*Mathf.PI*2,radius=(70+Roll()*230)/30;point=source.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;}
        else {var targets=Units.Where(v=>Valid(v,source.team,cfg,false)&&!IsNagaHero(v)&&Vector2.Distance(v.position,source.position)<=F(cfg,"range")).ToArray();if(targets.Length==0)return false;point=(targets.Contains(suggested)?suggested:targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))]).position;}
        string raw=kind=="mech_mines"?"h09M":"h09Q";var weapon=Weapon.Read(mechPayloads[raw]);float duration=Vector2.Distance(source.position,point)/weapon.speed;
        var shot=AddShot(source.team,source.kind,source.id,-1000000,source.position,point,0,weapon,0,3);shot.homing=false;shot.startHeight=100f/30;shot.chaosSpell=kind=="mech_mines"?"mech_mine_payload":"mech_grenade_payload";
        MechJobs.Add(new MechJob {kind=kind=="mech_mines"?"mine":"grenade",team=source.team,source=source.id,point=point,left=duration});return true;
    }
    public bool CastMechBuilding(Hut h,string id,int suggested=-1) {
        var cfg=Abilities[id];if(h==null||!Huts.Contains(h)||!h.paid||h.paused||h.hp<=0||BuildingDisabled(h)>0||!Lines[h.kind].abilities.Contains(id)||(string)cfg?["kind"]!="mech_shield"||h.mana<F(cfg,"mana")||h.spellTimers.TryGetValue(id,out float wait)&&wait>0)return false;
        var targets=Units.Where(u=>u.hp>0&&u.team==h.team&&!Invulnerable(u)&&!Hidden(u)&&((string)Lines[u.kind].stats["unit_types"]??"").ToLowerInvariant().Contains("sapper")).ToArray();var fresh=targets.Where(u=>!u.abilities.Contains("A09L")).ToArray();if(id=="A09N"||fresh.Length>0)targets=fresh;if(targets.Length==0)return false;
        var target=targets.FirstOrDefault(u=>u.id==suggested)??targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];Grant(target,"A10A");Grant(target,"A09L");target.abilityLevels["A10A"]=2;target.abilityLevels["A09L"]=id=="A09O"?2:1;
        h.mana-=F(cfg,"mana");h.spellTimers[id]=BuildingCooldown(h,F(cfg,"cd"));h.attackPoseLeft=.3f;h.attackSerial++;MechShields++;PulseAt(target.position,1.5f,Color.cyan,"Щит");return true;
    }
    void TickMechBuilding(Hut h) {foreach(string id in Lines[h.kind].abilities)if((string)Abilities[id]?["kind"]=="mech_shield"&&(!h.spellTimers.TryGetValue(id,out float t)||t<=0))h.spellTimers[id]=CastMechBuilding(h,id)?BuildingCooldown(h,F(Abilities[id],"cd")):.25f;}
    public bool MechBuildingChannel(Hut h)=>Lines[h.kind].abilities.Any(id=>(string)Abilities[id]?["kind"]=="mech_shield")&&h.attackPoseLeft>0;
    void MechGrenade(int team,Vector2 point) {
        var cfg=Abilities["A09E"];foreach(var u in Units.ToArray())if(u.hp>0&&u.team!=team&&Vector2.Distance(u.position,point)<=F(cfg,"area")) {
            if(Valid(u,team,cfg,false)){DamageUnit(u,F(cfg,"power"),team);if(u.hp>0){var status=(JObject)cfg.DeepClone();status["power"]=0;ApplyEffectData(u,status,team);}}
            if(!MechTrigger(u,team,true))continue;float drained=Mathf.Min(100,u.mana);u.mana-=drained;if(drained>0)DamageUnit(u,drained,team,"chaos",false);
        }MechGrenades++;PulseAt(point,F(cfg,"area"),new Color(.5f,.75f,1),"Импульсная граната");
    }
    void MechSuicide(Unit u){u.noBounty=true;DamageUnit(u,u.maxHp*1000,-1,"chaos",false,true);}
    void MechDied(Unit u) {if(ElementalIllusion(u)||u.mechActiveExplosion)return;foreach(string id in u.abilities){var cfg=Ranked(u,id);if((string)cfg?["kind"]=="mech_death_blast")MechJobs.Add(new MechJob {kind="blast",team=u.team,point=u.position,left=F(cfg,"duration"),config=(JObject)cfg.DeepClone()});}}
    public void MechBlast(int team,Vector2 point,JObject cfg) {
        var fields=cfg["fields"];float full=F(fields,"Dda1")/30,outer=F(fields,"Dda3")/30;string flags=(string)cfg["targets"]??"";
        foreach(var u in Units.ToArray())if(Valid(u,team,cfg,false)){float d=Vector2.Distance(u.position,point);if(d<=Mathf.Max(full,outer))DamageUnit(u,F(fields,d<=full?"Dda2":"Dda4"),team);}
        if(flags.Contains("structure")) {
            float modifier=(string)cfg["id"]=="A099"?F(fields,"Sds1",1):1;
            foreach(var h in Huts.ToArray())if(h.paid&&h.hp>0&&h.team!=team){float d=Vector2.Distance(h.position,point);if(d<=Mathf.Max(full,outer))DamageHut(h,F(fields,d<=full?"Dda2":"Dda4")*modifier,"magic",team,true);}
            float castle=Vector2.Distance(CastlePosition(1-team),point);if(castle<=Mathf.Max(full,outer))DamageCastle(1-team,F(fields,castle<=full?"Dda2":"Dda4")*modifier,"magic",true);
        }MechExplosions++;PulseAt(point,Mathf.Max(full,outer),new Color(1,.55f,.1f),"Взрыв");
    }
    bool TickMechUnit(Unit u,float dt) {
        if(MechMine(u)){u.pose="Stand";return true;}if(Lines[u.kind].unit=="n01U")return MechRepair(u,dt);if(Lines[u.kind].unit!="n01V")return false;
        if(u.mechFuse){u.pose="Stand";return true;}
        bool near=Units.Any(v=>MechTrigger(v,u.team)&&Vector2.Distance(v.position,u.position)<=5)||Huts.Any(h=>h.hp>0&&h.paid&&h.team!=u.team&&Vector2.Distance(h.position,u.position)<=5+F(Lines[h.kind].buildingStats,"collision"))||Vector2.Distance(u.position,CastlePosition(1-u.team))<=5+CastleCollision;
        if(!near)return false;u.mechFuse=true;u.strikeLeft=-1;MechJobs.Add(new MechJob {kind="sapper",source=u.id,team=u.team,left=F(Abilities["A099"],"duration")});return true;
    }
    bool MechRepair(Unit u,float dt) {
        var target=FindUnit(u.mechRepairTarget);var hut=FindHut(u.mechRepairTarget);bool castle=u.mechRepairTarget==-1&&CastleHp[u.team]<CastleMax;
        if(!castle&&(target==null||target.team!=u.team||target.hp>=target.maxHp)&&(hut==null||hut.team!=u.team||hut.hp>=hut.maxHp)) {
            var candidates=Huts.Where(h=>h.paid&&h.hp>0&&h.team==u.team&&h.maxHp-h.hp>100).Select(HutTarget).ToList();if(CastleMax-CastleHp[u.team]>100)candidates.Add(-1);
            if(candidates.Count==0)candidates.AddRange(Units.Where(v=>v!=u&&v.hp>0&&v.team==u.team&&!Organic(v)&&v.maxHp-v.hp>100).Select(v=>v.id));
            u.mechRepairTarget=candidates.Count>0?candidates[Mathf.Min(candidates.Count-1,(int)(Roll()*candidates.Count))]:-1000000;target=FindUnit(u.mechRepairTarget);hut=FindHut(u.mechRepairTarget);castle=u.mechRepairTarget==-1;
        }
        if(target==null&&hut==null&&!castle){u.pose="Stand";return true;}
        Vector2 point=target?.position??hut?.position??CastlePosition(u.team),delta=point-u.position;u.direction=delta.normalized;
        if(delta.magnitude>F(Abilities["A09K"],"range")){u.position=Vector2.MoveTowards(u.position,point,MoveSpeed(u)*dt);u.pose="Walk";return true;}
        float max=target?.maxHp??hut?.maxHp??CastleMax,hp=target?.hp??hut?.hp??CastleHp[u.team];string raw=target!=null?Lines[target.kind].unit:hut!=null?Lines[hut.kind].building:"hcas";var values=mechRepairValues[raw];if(values==null){u.pose="Stand";return true;}
        float rate=max/Mathf.Max(1,F(values,"seconds")*F(Abilities["A09K"]?["fields"],"Rep2",1));float healed=PayMechRepair(u.team,raw,max,Mathf.Min(rate*dt,max-hp));
        if(target!=null)target.hp+=healed;else if(hut!=null)hut.hp+=healed;else CastleHp[u.team]+=healed;if(healed>0)MechRepairs++;u.pose="Attack";return true;
    }
    public float PayMechRepair(int team,string raw,float max,float desired) {
        var values=mechRepairValues[raw];if(values==null||max<=0)return 0;double ratio=F(Abilities["A09K"]?["fields"],"Rep1"),gold=F(values,"gold")*ratio/max,wood=F(values,"wood")*ratio/max;
        if(gold>0)desired=Mathf.Min(desired,(float)(Math.Max(0,AvailableGold(team)-MechRepairGold[team])/gold));if(wood>0)desired=Mathf.Min(desired,(float)(Math.Max(0,AvailableWood(team)-MechRepairWood[team])/wood));
        MechRepairGold[team]+=desired*gold;MechRepairWood[team]+=desired*wood;int payGold=(int)Math.Floor(MechRepairGold[team]+.000001),payWood=(int)Math.Floor(MechRepairWood[team]+.000001);MechRepairGold[team]=Math.Max(0,MechRepairGold[team]-payGold);MechRepairWood[team]=Math.Max(0,MechRepairWood[team]-payWood);Gold[team]-=payGold;Wood[team]-=payWood;return desired;
    }
    public void MechTowerHit(int owner,int team,Unit target,float dealt,int roll=-1) {
        var source=Huts.FirstOrDefault(h=>h.id==owner&&h.hp>0&&h.paid&&Lines[h.kind].building=="h05L");if(source==null||dealt<=0||target==null||target.hp<=0||(roll<0?(int)(Roll()*100):roll)>=15)return;
        Chain(new Unit {team=team,kind=source.kind,position=source.position},target,Abilities["A09T"]);MechChains++;
    }
    void TickMechPassiveFire(Unit source){if(source.abilities.Contains("A09F")&&!Silenced(source)&&HasSpell(source,"A09F"))Cast(source,"A09F");}
    bool CastMechAutoFire(Unit source,JToken cfg,Unit suggested) {
        var targets=Units.Where(v=>Valid(v,source.team,cfg,false)&&Vector2.Distance(v.position,source.position)<=F(cfg,"range")&&!v.effects.Any(e=>e.id==(string)cfg["id"]&&e.left>0)).ToArray();if(targets.Length==0)return false;
        var target=targets.Contains(suggested)?suggested:targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];var w=new Weapon {type="missile",attack="magic",speed=F(cfg["fields"],"amsp")/30,homing=F(cfg["fields"],"amho")!=0,air=true};
        var shot=AddShot(source.team,source.kind,source.id,target.id,source.position,target.position,F(cfg,"power"),w,0,3);shot.chaosSpell="mech_auto_fire";return true;
    }
    bool ImpactMechShot(Shot shot) {
        if(shot.chaosSpell=="mech_miss"){if(shot.mechHeat)MechHeatBurst(shot.team,shot.end,shot.target);return true;}
        if(shot.chaosSpell=="mech_mine_payload"||shot.chaosSpell=="mech_grenade_payload")return true;
        if(shot.chaosSpell!="mech_auto_fire")return false;var victim=FindUnit(shot.target);var cfg=Abilities["A09F"];if(Valid(victim,shot.team,cfg,false)){DamageUnit(victim,F(cfg,"power"),shot.team);if(victim.hp>0){var status=(JObject)cfg.DeepClone();status["power"]=0;ApplyEffectData(victim,status,shot.team);}}return true;
    }
    void MechBuildingAuras() {
        foreach(var h in Huts)if(h.paid&&h.hp>0&&Lines[h.kind].building=="h100")foreach(var u in Units)if(u.hp>0&&u.team==h.team&&!Invulnerable(u)&&!Organic(u)&&Vector2.Distance(u.position,h.position)<=F(Abilities["A10C"],"area")) {
            u.auraArmor=Mathf.Max(u.auraArmor,F(Abilities["A10C"]?["profile"]?["effects"],"armor"));u.auraRegen=Mathf.Max(u.auraRegen,F(Abilities["A10D"]?["profile"]?["effects"],"regen"));u.auraIds.Add("A10C");u.auraIds.Add("A10D");
        }
    }
    public bool TryBlockSpell(Unit unit)=>NagaShieldBlock(unit);
    bool MechHeatReleased(Unit source,int target) {
        var victim=FindUnit(target);if(!MechFighter(source)||!TeamMech[source.team]||Aspects[source.team]!=1||victim==null||victim.team==source.team||!MechFighter(victim))return false;
        return ++source.mechHeatAttacks%5==0;
    }
    void MechHeatBurst(int team,Vector2 point,int primary) {
        var targets=Units.Where(u=>MechFighter(u)&&u.team!=team&&Vector2.Distance(u.position,point)<=200f/30).OrderBy(u=>u.id==primary?0:1).ThenBy(u=>(u.position-point).sqrMagnitude).Take(6).ToArray();foreach(var u in targets)DamageUnit(u,80,team);MechHeatBursts++;PulseAt(point,200f/30,new Color(1,.5f,.1f),"Перегрев");
    }
}
}
