using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    public int OrcNetShots {get;private set;}
    public int OrcNetHits {get;private set;}
    public int OrcFrenzies {get;private set;}
    bool OrcNetTarget(int team,Unit target)=>target!=null&&target.hp>0&&target.team!=team&&Flying(target)&&Organic(target)&&((string)Lines[target.kind].stats["unit_types"]??"").ToLowerInvariant().Contains("sapper");
    bool CastOrcNets(Unit source,JToken cfg,Unit center) {
        if(!OrcNetTarget(source.team,center)||Vector2.Distance(source.position,center.position)>F(cfg,"range"))return false;
        string effect=(string)cfg["effect_id"];var child=Abilities[effect];
        var targets=Units.Where(u=>OrcNetTarget(source.team,u)&&Vector2.Distance(center.position,u.position)<=F(cfg,"area")&&Vector2.Distance(source.position,u.position)<=F(child,"range")).OrderBy(u=>(u.position-center.position).sqrMagnitude).Take((int)cfg["count"]).ToArray();
        if(targets.Length==0)return false;
        var weapon=new Weapon {type="missile",attack="normal",homing=true,speed=F(child?["fields"],"amsp",1200)/30,arc=F(child?["fields"],"amac"),air=true};
        foreach(var target in targets){var shot=AddShot(source.team,source.kind,source.id,target.id,source.position,target.position,0,weapon,0,5);shot.effectId=effect;OrcNetShots++;}
        source.spellTimers[(string)cfg["id"]]=F(cfg,"cd");
        return true;
    }
    void OrcKilled(Unit killer,Unit victim) {
        if(killer==null||killer.hp<=0||killer.team==victim.team)return;
        killer.kills++;
        if(TeamOrc[killer.team]&&Aspects[killer.team]==0&&(bool?)Lines[victim.kind].stats["summoned"]!=true&&!ElementalIllusion(victim)) {
            killer.effects.RemoveAll(e=>e.id=="aspect_orc_honor");killer.effects.Add(new Effect{id="aspect_orc_honor",group="aspect_orc_honor",left=5,lifetime=5,haste=.5f,incoming=-.1f,sourceTeam=killer.team,config=new JObject{["id"]="aspect_orc_honor",["kind"]="buff",["duration"]=5,["haste"]=.5f,["incoming"]=-.1f}});PulseAt(killer.position,1.7f,new Color(.9f,.6f,.3f),"Кровь и честь");
        }
        if(!((string)Lines[victim.kind].stats["unit_types"]??"").Contains("sapper"))return;
        foreach(var id in killer.abilities)if((string)Abilities[id]?["kind"]=="kill_frenzy"){
            ApplyEffect(killer,id);OrcFrenzies++;PulseAt(killer.position,1.7f,new Color(1,.3f,.08f),"Безумие");
        }
    }
}
}
