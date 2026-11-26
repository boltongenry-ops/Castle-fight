using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    readonly Vector3[][] protocolCablePaths=new Vector3[2][];
    readonly LineRenderer[] protocolCableGlow=new LineRenderer[2],protocolCablePulse=new LineRenderer[2];
    readonly LineRenderer[] protocolArcs=new LineRenderer[4];
    readonly Vector3[] protocolArcPoints=new Vector3[25];
    readonly Vector3[] protocolPulsePoints=new Vector3[10];

    void SetupProtocolCable(int index,Vector2 from,Vector2 to){
        float bank=from.y*1.55f;
        var path=new Vector3[33];
        for(int i=0;i<path.Length;i++){
            float t=i/(float)(path.Length-1);
            float bend=Mathf.SmoothStep(0,1,Mathf.Min(t,1-t)*5);
            var position=Vector2.Lerp(from,to,t);
            position.y=Mathf.Lerp(position.y,bank,bend)+Mathf.Sin(t*Mathf.PI)*.7f;
            path[i]=World(position)+Vector3.up*Mathf.Lerp(.28f,1.02f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.91f,1,t)));
        }
        protocolCablePaths[index]=path;
        protocolWires[index]=ProtocolLine("Оплётка силового кабеля",path,.64f,false);
        protocolCableGlow[index]=ProtocolLine("Голубая жила",path,.17f,false);
        protocolCablePulse[index]=ProtocolLine("Бегущий заряд",protocolPulsePoints,.27f,false);
    }

    void RenderProtocolCable(int index,bool seen,bool live){
        ProtocolTint(protocolWires[index],seen,new Color(.11f,.23f,.28f,.94f));
        ProtocolTint(protocolCableGlow[index],seen&&live,new Color(.18f,.71f,1,.86f));
        var pulse=protocolCablePulse[index];
        ProtocolTint(pulse,seen&&live,new Color(.58f,.95f,1));
        if(!pulse.enabled)return;
        float head=Mathf.Repeat(Time.unscaledTime*.72f+index*.34f,1.3f);
        var path=protocolCablePaths[index];
        for(int i=0;i<protocolPulsePoints.Length;i++){
            float u=Mathf.Clamp01(head-.19f*i/(protocolPulsePoints.Length-1))* (path.Length-1);
            int sample=Mathf.Min(path.Length-2,Mathf.FloorToInt(u));
            protocolPulsePoints[i]=Vector3.Lerp(path[sample],path[sample+1],u-sample)+Vector3.up*.08f;
        }
        pulse.SetPositions(protocolPulsePoints);
    }

    void SetupProtocolArcs(){
        protocolBeam.positionCount=protocolArcPoints.Length;
        for(int i=0;i<protocolArcs.Length;i++)
            protocolArcs[i]=ProtocolLine(i==0?"Белое ядро залпа":"Боковой разряд",protocolArcPoints,i==0?.32f:.12f,false);
    }

    void RenderProtocolArcs(bool flash){
        ProtocolTint(protocolBeam,flash,new Color(.10f,.71f,1,.68f));
        for(int i=0;i<protocolArcs.Length;i++)
            ProtocolTint(protocolArcs[i],flash,i==0?new Color(.82f,.99f,1):new Color(.22f,.83f,1,.87f));
        if(!flash)return;
        float life=Mathf.Clamp01((match.ProtocolOpenFor-HumanMatch.ProtocolVulnerable+.38f)/.38f);
        protocolBeam.widthMultiplier=Mathf.Lerp(.22f,1.75f,life);
        float flicker=Mathf.Floor(Time.unscaledTime*24)*.83f;
        SetProtocolArc(protocolBeam,0,1,0,flicker);
        SetProtocolArc(protocolArcs[0],0,1,0,flicker);
        for(int i=1;i<protocolArcs.Length;i++){
            float sign=i%2==0?1:-1;
            SetProtocolArc(protocolArcs[i],.18f*i,.18f*i+.27f,sign*(1.8f+.9f*i),flicker+i*1.1f);
        }
    }

    void SetProtocolArc(LineRenderer line,float start,float end,float fork,float flicker){
        float from=match.ProtocolBeamX,to=HumanMatch.ProtocolBeamStart;
        for(int i=0;i<protocolArcPoints.Length;i++){
            float t=i/(float)(protocolArcPoints.Length-1);
            float along=Mathf.Lerp(start,end,t);
            float zigzag=Mathf.Sin(along*77+flicker)+Mathf.Sin(along*171-flicker)*.41f;
            float y=zigzag*.64f*Mathf.Sin(along*Mathf.PI)+fork*t;
            protocolArcPoints[i]=World(new Vector2(Mathf.Lerp(from,to,along),y))+
                Vector3.up*(Mathf.Lerp(2.1f,.55f,along)+.31f*zigzag*Mathf.Sin(t*Mathf.PI));
        }
        line.SetPositions(protocolArcPoints);
    }
}
}
