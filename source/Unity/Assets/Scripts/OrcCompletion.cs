using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    public int OrcBlessings {get;private set;}
    public int OrcStasises {get;private set;}
    public int OrcAcidShots {get;private set;}
    public int OrcAcidImpacts {get;private set;}
    void TickOrcBuilding(Hut h) {
        string raw=Lines[h.kind].building,id=raw=="h02R"?"A039":raw=="h02O"?"A032":raw=="h02N"?"A02Z":null;
        if(id==null||h.mana<F(Abilities[id],"mana")||(h.spellTimers.TryGetValue(id,out float delay)&&delay>0))return;
        if(id=="A039") {
            var eligible=Units.Where(u=>u.hp>0&&u.team==h.team&&((string)Lines[u.kind].stats["unit_types"]??"").ToLowerInvariant().Contains("sapper")).ToArray();
            var preferred=eligible.Where(u=>!u.abilities.Contains("A0F1")).ToArray();if(preferred.Length>0)eligible=preferred;if(eligible.Length==0)return;
            var target=eligible[Mathf.Min(eligible.Length-1,(int)(Roll()*eligible.Length))];foreach(string ability in new[]{"A03B","A03A","A03D","A08L"})Grant(target,ability);
            int choice=Mathf.Min(2,(int)(Roll()*3));foreach(string ability in new[]{new[]{"A05I","A06W"},new[]{"A08M","A06Y"},new[]{"A08R","A06X"}}[choice])Grant(target,ability);
            OrcBlessings++;PulseAt(target.position,2,Color.yellow,"Благословение");
        } else if(id=="A032") {
            var victims=Units.Where(u=>Valid(u,h.team,Abilities[id],false)&&Vector2.Distance(u.position,h.position)<=F(Abilities[id],"area")).ToArray();if(victims.Length==0)return;
            foreach(var u in victims){float duration=F(Abilities[id],"stun");u.stun=Mathf.Max(u.stun,duration);u.stasisLeft=Mathf.Max(u.stasisLeft,duration);}OrcStasises++;PulseAt(h.position,8,new Color(.5f,.5f,1),"Стазис");
        } else {
            var cfg=Abilities["A030"];var victims=Units.Where(u=>Valid(u,h.team,cfg,false)).ToArray();if(victims.Length==0)return;var target=victims[Mathf.Min(victims.Length-1,(int)(Roll()*victims.Length))];
            var w=new Weapon{type="artillery",homing=false,speed=F(cfg["fields"],"amsp",1700)/30,arc=F(cfg["fields"],"amac",.15f)};
            AddShot(h.team,h.kind,h.id,target.id,h.position,target.position,0,w,0,7);OrcAcidShots++;
        }
        h.mana-=F(Abilities[id],"mana");h.spellTimers[id]=BuildingCooldown(h,F(Abilities[id],"cd"));h.attackSerial++;h.attackPoseLeft=.7f;
    }
    void ImpactOrcAcid(Shot shot) {
        var cfg=Abilities["A030"];foreach(var u in Units.ToArray())if(Valid(u,shot.team,cfg,false)&&Vector2.Distance(u.position,shot.end)<=F(cfg,"area"))ApplyEffect(u,"A030",shot.team);
        OrcAcidImpacts++;PulseAt(shot.end,F(cfg,"area"),new Color(.4f,1,.15f),"Кислота");
    }
}
}