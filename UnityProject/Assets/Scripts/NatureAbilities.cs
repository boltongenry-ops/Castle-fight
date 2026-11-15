using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    bool NatureDebuffTarget(int team,Unit target,string id,JToken cfg) {
        if(!Valid(target,team,cfg,false)||target.effects.Any(e=>e.left>0&&e.id==id))return false;
        return (id=="A0AR"||id=="A0AS")?Flying(target):!IsNagaHero(target);
    }
    bool CastNatureDebuff(Unit source,Unit target,string id,JToken cfg) {
        if(!NatureDebuffTarget(source.team,target,id,cfg)||Vector2.Distance(source.position,target.position)>F(cfg,"range"))return false;
        if(id=="A0AV"){ApplyEffect(target,id,source.team);PulseAt(target.position,1.5f,Color.green,"Паразит");}
        else {
            var weapon=new Weapon {type="missile",attack="normal",homing=true,speed=F(cfg["fields"],"amsp",1500)/30,air=true};
            AddShot(source.team,source.kind,source.id,target.id,source.position,target.position,0,weapon,0,5).effectId=id;
        }
        return true;
    }
    void NatureDragonArmor(Unit target) {
        int rank=target.abilityLevels.TryGetValue("A0C9",out int previous)?Mathf.Min(3,previous+1):1;
        target.abilityLevels["A0C9"]=rank;
        target.effects.RemoveAll(e=>e.id=="A0C9");
        target.effects.Add(new Effect {id="A0C9",group="A0C9",left=float.PositiveInfinity,lifetime=float.PositiveInfinity,armor=-2*rank,sourceTeam=1-target.team,config=new JObject {
            ["kind"]="debuff",["name"]="Разъедание брони · "+rank,["description"]="Броня снижена на "+(rank*2)+". Постоянно; максимум три уровня.",["armor"]=-2*rank,["undispellable"]=true
        }});
    }
}
}
