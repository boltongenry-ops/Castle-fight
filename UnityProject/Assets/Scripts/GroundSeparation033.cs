using System;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    Vector2 GroundSeparation(Unit u,Vector2 forward){
        Vector2 result=forward;Unit blocker=null;float best=float.MaxValue;float ownRadius=UnitConfig035(u.kind).collision;
        var spatial=SimulationState035.Get(this);
        if(UnitIndexReady033()&&Opt(this).unitBatchActive&&!spatial.reference){
            var candidates=spatial.neighbors.WithinOrdered(u.position,4);
            for(int i=0;i<candidates.Count;i++)AccumulateGround035(u,candidates[i].unit,forward,ownRadius,ref result,ref blocker,ref best);
        }else foreach(var other in Units)AccumulateGround035(u,other,forward,ownRadius,ref result,ref blocker,ref best);
        if(blocker!=null){
            Vector2 perpendicular=new Vector2(-forward.y,forward.x);float offset=Vector2.Dot(u.position-blocker.position,perpendicular);
            float sign=Mathf.Abs(offset)>.15f?Mathf.Sign(offset):((u.id&1)==0?1:-1);
            // Prefer room toward the lane interior; a wall clamp must not cancel the sidestep.
            if(Mathf.Abs(u.position.y)>1.5f&&Mathf.Abs(perpendicular.y)>.5f)sign=-Mathf.Sign(u.position.y*perpendicular.y);
            result=forward*.45f+perpendicular*sign*.9f;
        }
        var answer=result.sqrMagnitude<.001f?forward:result.normalized;
        if(spatial.auditGround&&!spatial.reference&&Opt(this).unitBatchActive){spatial.reference=true;var scalar=GroundSeparation(u,forward);spatial.reference=false;if(answer.x!=scalar.x||answer.y!=scalar.y)throw new System.Exception("Ground candidate mismatch unit="+u.id+" index="+answer+" full="+scalar);}

        return answer;
    }
    void AccumulateGround035(Unit u,Unit other,Vector2 forward,float ownRadius,ref Vector2 result,ref Unit blocker,ref float best){
            if(other==u||other.hp<=0||!other.onRoad)return;
            var separation=u.position-other.position;float distance=separation.sqrMagnitude;
            // Max ahead=3.2 and lateral=2.2: every possible blocker is within four units.
            // Reject distant actors before status/JSON queries; this preserves the exact local rule.
            if(distance>16||Flying(other)||Hidden(other))return;
            if(distance>.001f&&distance<1.5f)result+=separation.normalized*(1-Mathf.Sqrt(distance)/1.225f)*.6f;
            if(other.team!=u.team)return;
            Vector2 delta=other.position-u.position;float ahead=Vector2.Dot(delta,forward);
            float radius=Mathf.Clamp(ownRadius+UnitConfig035(other.kind).collision,.8f,2.2f);
            float lateral=Mathf.Abs(delta.x*forward.y-delta.y*forward.x);
            if(ahead>=-.01f&&ahead<radius+1&&lateral<radius&&distance<best){best=distance;blocker=other;}
        }

}
}
