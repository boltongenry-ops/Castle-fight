using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    public int ChaosRoars;
    bool ChaosRoarReady(Unit source) {
        float range=F(Abilities["A03S"],"range");return Units.Any(u=>NatureEligible(u,source.team)&&!Flying(u)&&Vector2.Distance(u.position,source.position)<=range)||Huts.Any(h=>h.paid&&h.hp>0&&h.team!=source.team&&Vector2.Distance(h.position,source.position)<=range);
    }
    bool CastChaosRoar(Unit source) {
        if(!ChaosRoarReady(source))return false;var cfg=Abilities["A03R"];
        foreach(var ally in Units)if(Valid(ally,source.team,cfg,true)&&Vector2.Distance(source.position,ally.position)<=F(cfg,"area"))ApplyEffect(ally,"A03R",source.team);
        ChaosRoars++;PulseAt(source.position,F(cfg,"area"),new Color(1,.2f,.05f),"Боевой рёв");return true;
    }
}
}
