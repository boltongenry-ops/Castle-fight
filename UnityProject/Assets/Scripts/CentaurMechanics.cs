using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    sealed class CentaurStride {public int team;public float running,quiet,after;public Vector2 point;}
    public sealed class CentaurCloud {public int team;public Vector2 point;public float left=5;}
    public sealed class CentaurSand {public int team,rank;public Vector2 point;public float left=6;}
    readonly bool[] centaurTeam=new bool[2];
    readonly Dictionary<int,CentaurStride> centaurStrides=new Dictionary<int,CentaurStride>();
    readonly HashSet<int> centaurProtected=new HashSet<int>();
    float centaurTick,centaurHealing;
    public readonly List<CentaurCloud> CentaurClouds=new List<CentaurCloud>();
    public readonly List<CentaurSand> CentaurSands=new List<CentaurSand>();
    public int CentaurCharges,CentaurNets,CentaurCasts,CentaurHeals,CentaurSandCasts;
    void InitCentaur(string first,string second){centaurTeam[0]=first=="Centaur Khanates";centaurTeam[1]=second=="Centaur Khanates";}
    public bool IsCentaur(int team)=>team>=0&&team<2&&centaurTeam[team];
    public float CentaurChargeProgress(Unit u)=>u!=null&&centaurStrides.TryGetValue(u.id,out var s)&&s.team==u.team&&Elapsed>=s.after?Mathf.Clamp01(s.running/(u.auraIds.Contains("CT05")?2:3)):0;
    void ObserveCentaurStride(Unit u,Vector2 before,float dt){
        if(!u.abilities.Contains("CT01")||u.hp<=0)return;
        if(!centaurStrides.TryGetValue(u.id,out var s)||s.team!=u.team)centaurStrides[u.id]=s=new CentaurStride{team=u.team,point=before};
        bool controlled=u.stun>0||Disabled(u)||Rooted(u)||u.castPause>0||u.castLeft>=0;
        float traveled=Vector2.Distance(before,u.position);
        if(controlled){s.running=0;s.quiet=0;}
        else if(u.pose=="Walk"&&traveled>Mathf.Max(.0001f,dt*.5f)&&traveled<=Mathf.Max(1,MoveSpeed(u)*dt*1.5f)){s.running+=dt;s.quiet=0;}
        else if((s.quiet+=dt)>.35f)s.running=0;
        s.point=u.position;
    }
    void CentaurOpening(Unit u){
        if(CentaurChargeProgress(u)<1)return;var s=centaurStrides[u.id];s.running=0;s.quiet=0;s.after=Elapsed+6;
        u.shotDamage*=IsCentaur(u.team)&&Aspects[u.team]==0?1.6f:1.4f;CentaurCharges++;
        PulseAt(u.position,1.8f,new Color(.95f,.75f,.3f),"Натиск",.45f);
        if(u.abilities.Contains("CT02"))ApplyEffect(u,"CT13",u.team);
        if(u.abilities.Contains("CT07"))foreach(var ally in Units.Where(v=>v.hp>0&&v.team==u.team&&!Invulnerable(v)&&!Hidden(v)&&Vector2.Distance(v.position,u.position)<=15).OrderBy(v=>(v.position-u.position).sqrMagnitude).Take(8))ApplyEffect(ally,"CT10",u.team);
    }
    void CentaurAfterWeapon(Unit source,Unit target,float dealt){
        CentaurCombatHit(source,target,dealt);
        if(source==null||target==null||dealt<=0||source.hp<=0||target.hp<=0||!source.abilities.Contains("CT03")||!Flying(target)||target.spellResist>=1||Invulnerable(target)||!NightCanDetect(source.team,target))return;
        if(source.spellTimers.TryGetValue("CT03",out float left)&&left>0)return;
        source.spellTimers["CT03"]=8;if(NagaShieldBlock(target))return;
        ApplyEffect(target,"CT12",source.team);CentaurNets++;PulseAt(target.position,1.3f,Color.yellow,"Гарпун",.5f);
    }
    float CentaurMissChance(Unit source,Weapon weapon)=>weapon.type=="normal"?Mathf.Clamp01(source.effects.Where(e=>e.left>0&&e.id!="CT11").Sum(e=>F(e.config,"miss"))):MissChance(source);
    void TickCentaur(float dt){
        if(Finished||NetworkReplica||!centaurTeam[0]&&!centaurTeam[1])return;
        foreach(var cloud in CentaurClouds)cloud.left-=dt;CentaurClouds.RemoveAll(v=>v.left<=0);
        foreach(var sand in CentaurSands)sand.left-=dt;CentaurSands.RemoveAll(s=>s.left<=0);
        centaurTick-=dt;centaurHealing-=dt;if(centaurTick>0)return;centaurTick=.25f;
        foreach(var h in Huts.Where(h=>h.paid&&h.hp>0&&!h.paused&&BuildingDisabled(h)<=0&&Lines[h.kind].abilities.Contains("CT15")).ToArray()){
            if(h.spellTimers.TryGetValue("CT15",out float after)&&after>0||h.mana<45)continue;
            var victim=Units.Where(u=>u.team!=h.team&&u.hp>0&&!Flying(u)&&u.spellResist<1&&!Invulnerable(u)&&!Hidden(u)&&NightCanDetect(h.team,u)&&(VisionTest==null||VisionTest(h.team,u.position))&&(h.team!=1||BotSeesUnit(u))&&Vector2.Distance(u.position,h.position)<=30).OrderBy(u=>(u.position-h.position).sqrMagnitude).FirstOrDefault();
            if(victim==null)continue;h.mana-=45;h.spellTimers["CT15"]=12;
            CentaurSands.Add(new CentaurSand{team=h.team,rank=Lines[h.kind].building=="c01B"?2:1,point=victim.position});if(CentaurSands.Count>64)CentaurSands.RemoveAt(0);CentaurSandCasts++;
            PulseAt(victim.position,6,new Color(.68f,.48f,.19f),"Зыбучие пески",6);
        }
        var sandBurns=new HashSet<int>();
        foreach(var sand in CentaurSands.Where(s=>s.rank==2))foreach(var victim in Units.Where(u=>u.team!=sand.team&&u.hp>0&&!Flying(u)&&u.spellResist<1&&!Invulnerable(u)&&!Hidden(u)&&Vector2.Distance(sand.point,u.position)<=6).OrderBy(u=>(u.position-sand.point).sqrMagnitude).Take((int)F(Abilities["CT15"],"legendary_count",8)))sandBurns.Add(victim.id);
        foreach(var u in Units.Where(u=>u.hp>0&&!Invulnerable(u)&&!Hidden(u)).ToArray()){
            if(IsCentaur(u.team)&&Aspects[u.team]==1&&(Lines[u.kind].unit??"").StartsWith("cU")&&!centaurProtected.Contains(u.id)&&Units.Any(v=>v.team!=u.team&&v.hp>0&&!Hidden(v)&&NightCanDetect(u.team,v)&&Vector2.Distance(v.position,u.position)<=10)){
                centaurProtected.Add(u.id);ApplyEffect(u,"CT09",u.team);
            }
            if(Flying(u)||u.spellResist>=1)continue;
            int rank=CentaurSands.Where(s=>s.team!=u.team&&Vector2.Distance(s.point,u.position)<=6).Select(s=>s.rank).DefaultIfEmpty(0).Max();
            if(rank>0)ApplyEffect(u,rank==2?"CT17":"CT16",1-u.team);
            if(sandBurns.Contains(u.id))DamageUnit(u,F(Abilities["CT15"],"legendary_dps",24)*.25f,1-u.team,"magic",true);
        }
        centaurProtected.RemoveWhere(id=>!Units.Any(u=>u.id==id&&u.hp>0));
        foreach(int id in centaurStrides.Keys.Where(id=>!Units.Any(v=>v.id==id&&v.hp>0)).ToArray())centaurStrides.Remove(id);
        TickCentaurCombat();
        foreach(var witch in Units.Where(v=>CentaurCanCast(v)&&v.abilities.Contains("CT04")&&v.mana>=35).ToArray()){
            if(witch.spellTimers.TryGetValue("CT04",out float left)&&left>0)continue;
            var target=Units.Where(v=>CentaurVisible(witch,v)&&v.spellResist<1&&Lines[v.kind].weapons.Any(w=>w.type!="normal")&&Vector2.Distance(v.position,witch.position)<=18).OrderBy(v=>(v.position-witch.position).sqrMagnitude).FirstOrDefault();
            if(target==null)continue;witch.mana-=35;witch.spellTimers["CT04"]=10;witch.castPause=.5f;
            CentaurClouds.Add(new CentaurCloud{team=witch.team,point=target.position});if(CentaurClouds.Count>32)CentaurClouds.RemoveAt(0);CentaurCasts++;PulseAt(target.position,6,new Color(.78f,.62f,.32f),"Песчаная завеса",5);
        }
        foreach(var u in Units)if(u.hp>0&&u.spellResist<1&&!Invulnerable(u)&&Lines[u.kind].weapons.Any(w=>w.type!="normal")&&CentaurClouds.Any(c=>c.team!=u.team&&Vector2.Distance(c.point,u.position)<=6))ApplyEffect(u,"CT11",1-u.team);
        if(centaurHealing<=0){centaurHealing=2;for(int team=0;team<2;team++)if(IsCentaur(team)){
            int supplies=Huts.Count(h=>h.team==team&&h.paid&&h.hp>0&&!h.paused&&BuildingDisabled(h)<=0&&Lines[h.kind].building=="c009");
            foreach(var u in Units.Where(v=>v.team==team&&v.hp>0&&v.hp<v.maxHp&&!Invulnerable(v)&&!Hidden(v)&&InsideBase(team,v.position)).OrderBy(v=>v.hp/v.maxHp).Take(Math.Min(ArmyLimit,supplies*5))){u.hp=Mathf.Min(u.maxHp,u.hp+8);CentaurHeals++;}
        }}
    }
}
}
