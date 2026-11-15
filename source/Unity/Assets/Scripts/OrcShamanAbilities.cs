using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    public int OrcInnerFires {get;private set;}
    public int OrcStealShots {get;private set;}
    public int OrcStealHits {get;private set;}
    sealed class StealChoice {public Unit donor,recipient;public Effect status;}
    JToken StatusConfig(Effect e)=>e.config??Abilities[e.id];
    bool Transferable(Effect e) {
        var c=StatusConfig(e);string kind=(string)c?["kind"];
        return e.left>0&&(bool?)c?["undispellable"]!=true&&(bool?)c?["physical_control"]!=true&&(kind=="buff"||kind=="regen"||kind=="debuff");
    }
    bool Positive(Effect e) {
        var c=StatusConfig(e);string kind=(string)c?["kind"];
        if(kind=="regen")return true;if(kind=="debuff")return false;
        return e.power>=0&&e.armor>=0&&e.haste>=0&&e.speed>=0&&F(c,"regen")>=0&&F(c,"slow")<=0&&F(c,"miss")<=0;
    }
    StealChoice OrcStealChoice(Unit source,JToken cfg) {
        foreach(var donor in Units) {
            if(donor.hp<=0||donor.spellResist>=1||(bool?)Lines[donor.kind].stats["invulnerable"]==true||Vector2.Distance(source.position,donor.position)>F(cfg,"range"))continue;
            foreach(var status in donor.effects) {
                if(!Transferable(status)||Positive(status)==(donor.team==source.team))continue;
                var recipient=Units.Where(u=>Valid(u,source.team,StatusConfig(status),Positive(status))&&Vector2.Distance(donor.position,u.position)<=F(cfg,"area")&&!u.effects.Any(e=>e.id==status.id)).OrderBy(u=>(u.position-donor.position).sqrMagnitude).FirstOrDefault();
                if(recipient!=null)return new StealChoice{donor=donor,recipient=recipient,status=status};
            }
        }
        return null;
    }
    bool CastOrcSteal(Unit source,JToken cfg) {
        var choice=OrcStealChoice(source,cfg);if(choice==null)return false;
        var w=new Weapon{type="missile",homing=true,speed=F(cfg["fields"],"amsp",1000)/30,arc=F(cfg["fields"],"amac",.15f)};
        var shot=AddShot(source.team,source.kind,source.id,choice.recipient.id,choice.donor.position,choice.recipient.position,0,w,0,6);
        shot.startHeight=Height(choice.donor)+1;shot.donorId=choice.donor.id;shot.stolenEffect=choice.status;
        source.spellTimers[(string)cfg["id"]]=F(cfg,"cd");OrcStealShots++;return true;
    }
    void ImpactOrcSteal(Shot shot,Unit recipient) {
        var donor=FindUnit(shot.donorId);var original=shot.stolenEffect;
        if(donor==null||recipient==null||original.left<=0||!donor.effects.Contains(original))return;
        donor.effects.Remove(original);
        // Preserve the actual status instance's modifiers and remaining duration;
        // an expired, refreshed or removed donor status cannot be stolen in flight.
        var moved=original.Copy();moved.sourceTeam=shot.team;
        if(!recipient.effects.Any(e=>e.group==moved.group&&e.strength>moved.strength)) {
            recipient.effects.RemoveAll(e=>e.id==moved.id||e.group==moved.group);recipient.effects.Add(moved);
        }
        OrcStealHits++;PulseAt(recipient.position,2,new Color(.55f,.4f,1),"Похищение магии",.8f,"spellsteal",recipient.id);
    }
}
}
