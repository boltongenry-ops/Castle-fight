using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator SampleMotion(string folder) {
        Vector2 start=match.Builders[0].position;
        match.MoveBuilder(0,new Vector2(start.x,-60));
        var speeds=new List<float>();var facing=new List<float>();var actorFacing=new List<float>();int stationaryFrames=0;
        for(int frame=0;frame<80;frame++) {
            Vector3 before=builders[0].transform.position;yield return null;
            Vector3 delta=builders[0].transform.position-before;delta.y=0;
            if(frame<24)continue;
            float speed=delta.magnitude/Mathf.Max(.001f,Time.unscaledDeltaTime);speeds.Add(speed);
            if(speed<.1f)stationaryFrames++;
            if(delta.sqrMagnitude>.00001f){facing.Add(Vector3.Dot(builders[0].animatedRoot.transform.forward,delta.normalized));actorFacing.Add(Vector3.Dot(builders[0].transform.forward,delta.normalized));}
        }
        Check(stationaryFrames<4,"rendered motion is not quantized to 20 Hz");
        Check(speeds.Count>20&&Mathf.Abs(speeds.OrderBy(v=>v).ElementAt(speeds.Count/2)-match.BuilderSpeed)<.5f,"rendered speed follows worker simulation");
        Check(facing.Count>20&&facing.Average()>.98f,"native model faces forward along movement");
        FocusBuilder();yield return Shot(folder,"11-forward-walk");
        yield return Action("builder.stop");Vector2 stopped=match.Builders[0].position;
        yield return new WaitForSecondsRealtime(.15f);Check(Vector2.Distance(stopped,match.Builders[0].position)<.01f,"stop command stops worker");
        match.MoveBuilder(0,start);for(int i=0;i<220;i++)Tick(.05f);
        var report=new JObject {["stationaryRenderFrames"]=stationaryFrames,["samples"]=speeds.Count,["medianMetresPerSecond"]=speeds.OrderBy(v=>v).ElementAt(speeds.Count/2),["meanActorForwardAlignment"]=actorFacing.Count>0?actorFacing.Average():0,["meanForwardAlignment"]=facing.Count>0?facing.Average():0};
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"motion-verification.json"),report.ToString());
    }
}
}
