using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public static class Infernal033Scenarios {
    public static void Run(JObject chaos,JObject human,Action<bool,string> report){
        Action<bool,string> c=(ok,text)=>{report(ok,text);if(!ok)throw new Exception(text);};
        var m=new HumanMatch((JObject)chaos.DeepClone(),100000,0,-1,-1,(JObject)human.DeepClone());
        var infernal=m.Spawn(0,m.Kind("h007"),Vector2.zero);infernal.onRoad=true;
        var dragon=m.Spawn(1,m.Kind("h009"),Vector2.right*3);dragon.onRoad=true;dragon.hp=dragon.maxHp=10000;
        var soldier=m.Spawn(1,m.Kind("h000"),Vector2.up*3);soldier.onRoad=true;soldier.hp=soldier.maxHp=10000;
        c(m.Flying(dragon)&&m.WeaponFor(infernal,dragon)==null,"Infernal ordinary melee rejects a flying red dragon");
        float hp=dragon.hp;typeof(HumanMatch).GetMethod("UpdateAuras",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(m,null);
        c(dragon.hp<hp&&dragon.auraIds.Contains("A012"),"Infernal burn still damages a nearby flyer independently");
        infernal.combatTarget=dragon.id;infernal.cooldown=0;m.TickUnit(infernal,.05f);
        c(infernal.combatTarget==soldier.id&&infernal.attackTarget==soldier.id,"Stale flyer aggro is discarded and the nearest legal ground enemy is attacked");
        c(infernal.strikeLeft>=0,"Infernal starts a real ground weapon attack");
        m.TickUnit(infernal,1f);c(soldier.hp<10000,"Ground target receives Infernal melee damage");
        // Exact collinearity used to cancel avoidance behind a stationary allied front unit.
        m.Units.Clear();var blocker=m.Spawn(0,m.Kind("h007"),Vector2.zero);blocker.onRoad=true;blocker.stun=10000;
        var follower=m.Spawn(0,m.Kind("h000"),Vector2.left*2);follower.onRoad=true;
        for(int i=0;i<120;i++){follower.cooldown=10000;m.TickUnit(follower,.05f);}
        c(follower.position.x>blocker.position.x+2,"A collinear allied follower passes the stationary Infernal");
        c(Mathf.Abs(follower.position.y)<=2.21f,"Sidestep stays within the road boundary");
        m.Units.Clear();blocker=m.Spawn(1,m.Kind("h007"),Vector2.zero);blocker.onRoad=true;blocker.stun=10000;follower=m.Spawn(1,m.Kind("h000"),Vector2.right*2);follower.onRoad=true;
        for(int i=0;i<120;i++){follower.cooldown=10000;m.TickUnit(follower,.05f);}
        c(follower.position.x<blocker.position.x-2,"Mirrored team also passes its allied Infernal");
    }
}
}
