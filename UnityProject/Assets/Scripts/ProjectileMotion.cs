using UnityEngine;
namespace CastleFight {
// Presentation history only. The server remains responsible for hits and damage.
public sealed class ProjectileMotion {
    public const float Delay=.12f,MaxExtrapolation=.10f;
    public struct Pose {public Vector3 position;public Quaternion rotation;public float time;}
    readonly Pose[] samples=new Pose[8];int count;
    public int Count=>count;
    public float LastTime=>count==0?0:samples[count-1].time;
    public void Push(Vector3 position,Quaternion rotation,float time){
        if(count>0&&time<LastTime-.001f)count=0;
        if(count>0&&time<=LastTime)count--;
        if(count==samples.Length){for(int i=1;i<count;i++)samples[i-1]=samples[i];count--;}
        samples[count++]=new Pose{position=position,rotation=rotation,time=time};
    }
    public Pose Sample(float time){
        if(count==0)return default(Pose);
        if(time<=samples[0].time)return samples[0];
        for(int i=1;i<count;i++)if(time<=samples[i].time){
            var a=samples[i-1];var b=samples[i];float t=Mathf.Clamp01((time-a.time)/Mathf.Max(.0001f,b.time-a.time));
            return new Pose{position=Vector3.Lerp(a.position,b.position,t),rotation=Quaternion.Slerp(a.rotation,b.rotation,t),time=time};
        }
        var last=samples[count-1];
        if(count>1){var before=samples[count-2];last.position+=(last.position-before.position)/Mathf.Max(.0001f,last.time-before.time)*Mathf.Clamp(time-last.time,0,MaxExtrapolation);}
        return last;
    }
}
}
