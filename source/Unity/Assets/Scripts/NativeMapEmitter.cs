using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed class NativeMapEmitter : MonoBehaviour {
    public float density=.25f;public ParticleSystem emitter;public string sourceProfile,clipInterval;
    float elapsed,timer,maxRate;JObject rate,visibility;JArray interval;
    JArray requestedInterval;bool repeatClip=true;
    public void SetClip(JArray clip,bool repeat){requestedInterval=clip;interval=clip;repeatClip=repeat;elapsed=timer=0;if(rate!=null){Apply();emitter.Play();}}
    public bool Emitting=>emitter&&emitter.emission.enabled;
    void Start() {
        var data=JObject.Parse(sourceProfile);rate=(JObject)data["rate_track"]??new JObject { ["alpha"]=data["rate"],["frames"]=new JArray(),["values"]=new JArray() };
        visibility=(JObject)data["visibility_track"]??new JObject { ["alpha"]=1,["frames"]=new JArray(),["values"]=new JArray() };
        interval=requestedInterval??JArray.Parse(clipInterval);maxRate=(float)data["rate"];
        // Box surface normals must not steer the velocity. WC3 uses a separate UP direction.
        var main=emitter.main;float speedMin=main.startSpeed.constantMin,speedMax=main.startSpeed.constantMax;
        main.startSpeed=0;
        float latitude=Mathf.Clamp((float)data["latitude"],0,180)*Mathf.Deg2Rad;
        float lateral=speedMax*Mathf.Sin(Mathf.Min(latitude,Mathf.PI*.5f));
        var velocity=emitter.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;
        velocity.x=new ParticleSystem.MinMaxCurve(-lateral,lateral);velocity.y=new ParticleSystem.MinMaxCurve(-lateral,lateral);
        velocity.z=new ParticleSystem.MinMaxCurve(speedMin*Mathf.Cos(latitude),speedMax);
        var shape=emitter.shape;shape.randomDirectionAmount=0;
        foreach(var value in (JArray)rate["values"]??new JArray())maxRate=Mathf.Max(maxRate,(float)value);
        var emission=emitter.emission;emission.rateOverTime=maxRate*density;
        Apply();emitter.Play();emitter.Simulate(Mathf.Min(2,emitter.main.startLifetime.constant),true,false);emitter.Play();
    }
    void Update(){elapsed+=Time.deltaTime;timer-=Time.deltaTime;if(timer<=0){timer=.05f;Apply();}}
    void Apply() {
        float duration=Mathf.Max(1,(float)interval[1]-(float)interval[0]);
        float standTime=repeatClip?Mathf.Repeat(elapsed*1000,duration):Mathf.Min(elapsed*1000,duration);
        bool on=(repeatClip||elapsed*1000<duration)&&NativeAppearance.Sample(rate,interval,standTime)>0&&NativeAppearance.Sample(visibility,interval,standTime)>.01f;
        var emission=emitter.emission;emission.enabled=on;
    }
}
}
