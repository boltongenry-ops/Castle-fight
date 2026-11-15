using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    sealed class NagaSplit {public int team,kind,count;public Vector2 position;}
    readonly List<NagaSplit> nagaSplits=new List<NagaSplit>();
    public int NagaSplits {get;private set;}
    public int NagaPurges {get;private set;}
    public int NagaSummons {get;private set;}
    public int NagaSeaSummons {get;private set;}
    void NagaOnDeath(Unit dead) {
        foreach(var e in dead.effects)if(e.left>0&&(string)e.config?["death_spawn"]!="nN04"&&!string.IsNullOrEmpty((string)e.config?["death_spawn"])){int child=Kind((string)e.config["death_spawn"]);if(child>=0)nagaSplits.Add(new NagaSplit {team=e.sourceTeam,kind=child,count=(int?)e.config["count"]??1,position=dead.position});}
        foreach(var id in dead.abilities){var cfg=Abilities[id];if((string)cfg?["kind"]!="split"||(string)cfg["summon"]=="nN04")continue;
            int kind=Kind((string)cfg["summon"]);if(kind<0)kind=System.Array.FindIndex(Lines,l=>l.unit==(string)cfg["summon"]);if(kind<0)continue;
            nagaSplits.Add(new NagaSplit {team=dead.team,kind=kind,count=(int?)cfg["count"]??2,position=dead.position});NagaSplits++;
        }
    }
    bool CastNagaPurge(Unit source,JToken cfg,Unit target) {
        if(target==null||!NagaCandidate(target,source.team)||!Valid(target,source.team,cfg,false)||IsNagaHero(target)||Vector2.Distance(source.position,target.position)>F(cfg,"range"))return false;
        target.effects.RemoveAll(e=>(bool?)e.config?["undispellable"]!=true&&(bool?)e.config?["physical_control"]!=true);
        if(target.summoned)DamageUnit(target,F(Abilities["A02T"]?["fields"],"Prg3",400),source.team);
        if(target.hp>0){ApplyEffect(target,"A02T",source.team);var status=target.effects.LastOrDefault(e=>e.id=="A02T");if(status!=null){status.power=0;status.speed=-.8f;status.root=true;}}
        source.sirenFollowup=1;NagaPurges++;PulseAt(target.position,1.5f,new Color(.35f,.7f,1),"Очищение",2f,"naga_purge");return true;
    }
    void TickNagaJobs(float dt) {
        foreach(var job in nagaSplits.ToArray()){
            nagaSplits.Remove(job);
            for(int i=0;i<job.count;i++){var child=Spawn(job.team,job.kind,job.position+new Vector2(i==0?.8f:-.8f,0));child.summoned=true;child.onRoad=true;}
        }
        foreach(var unit in Units.ToArray()) {
            if(unit.sirenFollowup<0)continue;unit.sirenFollowup-=dt;if(unit.sirenFollowup>0)continue;unit.sirenFollowup=-1;
            if(unit.hp<=0||unit.stun>0||!HasSpell(unit,"A02S"))continue;
            var cfg=Abilities["A02S"];int kind=Kind((string)cfg["summon"]);if(kind<0)kind=System.Array.FindIndex(Lines,l=>l.unit==(string)cfg["summon"]);if(kind<0)continue;
            unit.mana-=F(cfg,"mana");unit.spellTimers["A02S"]=F(cfg,"cd");
            for(int i=0;i<((int?)cfg["count"]??2);i++){var summon=Spawn(unit.team,kind,unit.position+new Vector2(i==0?.8f:-.8f,0));summon.summoned=true;summon.summonLeft=F(cfg,"duration",45);summon.onRoad=true;NagaSummons++;}
            PulseAt(unit.position,2,Color.cyan,"Призыв лобстроков",3.3f,"naga_charm");
        }
    }
    void TickNagaSea(Hut hut,float dt) {
        hut.seaTimer-=dt;if(hut.seaTimer>0)return;hut.seaTimer=20;
        if(Units.Count(u=>u.hp>0&&u.team==hut.team&&u.seaSummon)>=6)return;
        int kind=Kind(Roll()<.5f?"h020":"n00W");if(kind<0)return;
        var unit=Spawn(hut.team,kind,hut.position+new Vector2(hut.team==0?2:-2,1));
        unit.hp=unit.maxHp=250;unit.armor=0;unit.abilities.Clear();unit.critChance=0;unit.evasion=0;unit.spellResist=0;unit.attackOverride=20;unit.cooldownOverride=1.4f;
        unit.summoned=true;unit.noBounty=true;unit.seaSummon=true;unit.summonLeft=30;NagaSeaSummons++;PulseAt(unit.position,2,Color.cyan,"Морские твари");
    }
}
}
