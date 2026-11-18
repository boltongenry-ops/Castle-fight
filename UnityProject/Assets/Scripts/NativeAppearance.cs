using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed class NativeAppearance : MonoBehaviour {
    sealed class Track {
        public float fallback,global;public int interpolation;public float[] frames,values;
        public Track(JObject data) {fallback=(float?)data["alpha"]??1;global=(float?)data["global"]??0;interpolation=(int?)data["interpolation"]??0;frames=((JArray)data["frames"]).Select(v=>(float)v).ToArray();values=((JArray)data["values"]).Select(v=>(float)v).ToArray();}
        public float Evaluate(Vector2 interval,float positionMs) {
            if(frames.Length==0)return fallback;
            float start=interval.x,end=interval.y,time=start+positionMs;
            if(global>0){start=0;end=global;time=Mathf.Repeat(Time.time*1000,global);}
            int left=-1,right=-1;
            for(int i=0;i<frames.Length;i++){float frame=frames[i];if(frame<start||frame>end)continue;if(left<0||frame<=time)left=i;if(frame>time){right=i;break;}}
            if(left<0)return fallback;if(right<0||interpolation==0)return values[left];
            return Mathf.Lerp(values[left],values[right],Mathf.Clamp01((time-frames[left])/Mathf.Max(1,frames[right]-frames[left])));
        }
    }
    sealed class Model {
        public Dictionary<string,Vector2> clips=new Dictionary<string,Vector2>();public Track[] geosets,layers;public float height;
        public Model(JObject data) {
            height=(float?)data["height"]??2.2f;
            foreach(var p in ((JObject)data["clips"]).Properties())clips[p.Name]=new Vector2((float)p.Value[0],(float)p.Value[1]);
            geosets=((JArray)data["surfaces"]).Select(s=>new Track((JObject)s["geoset"])).ToArray();
            layers=((JArray)data["surfaces"]).Select(s=>new Track((JObject)s["layer"])).ToArray();
        }
    }
    static Dictionary<string,Model> database;
    Model config;Animation player; AnimationState playing;Vector2 clipInterval;
    SkinnedMeshRenderer skin; MaterialPropertyBlock block;float[] previous;
    float untilRefresh;
    public float AuthoredHeight=>config?.height??2.2f;
    static void LoadDatabase(){
        if(database==null)database=JObject.Parse(ContentResources.Load<TextAsset>("native-appearance").text).Properties().ToDictionary(p=>p.Name,p=>new Model((JObject)p.Value));
    }
    internal static string[] AddOptionalProfiles(string text){
        var catalog=JObject.Parse(text).Properties().ToDictionary(p=>p.Name,p=>new Model((JObject)p.Value));
        if(catalog.Count==0||catalog.Keys.Any(key=>!key.EndsWith("_ref",StringComparison.Ordinal)))throw new ArgumentException("Invalid Reforged appearance catalog");
        LoadDatabase();
        foreach(var entry in catalog)database[entry.Key]=entry.Value;
        return catalog.Keys.ToArray();
    }
    internal static void RemoveOptionalProfiles(IEnumerable<string> keys){
        if(database!=null)foreach(string key in keys)database.Remove(key);
    }
    public void Configure(string raw,Animation animation) {
        LoadDatabase();
        if(!database.TryGetValue(raw,out var model)){var extra=ContentResources.Load<TextAsset>("pc-centaur");var profile=extra?JObject.Parse(extra.text)["centaurAppearance"]?[raw] as JObject:null;if(profile==null)throw new Exception("Missing appearance "+raw);database[raw]=model=new Model(profile);}
        ConfigureModel(model,animation);
    }
    public void ConfigureProfile(string profile,Animation animation){ConfigureModel(new Model(JObject.Parse(profile)),animation);}
    void ConfigureModel(Model data,Animation animation){
        config=data;player=animation;skin=GetComponentInChildren<SkinnedMeshRenderer>();
        if(skin.sharedMesh.subMeshCount!=config.geosets.Length)throw new Exception("Native surface mapping mismatch");
        previous=Enumerable.Repeat(-1f,config.geosets.Length).ToArray();
        block=new MaterialPropertyBlock();Refresh();
    }
    public void SetClip(string name){playing=player[name];config.clips.TryGetValue(name,out clipInterval);untilRefresh=0;}
    void LateUpdate(){untilRefresh-=Time.deltaTime;if(untilRefresh<=0){untilRefresh=.05f;Refresh();}}
    void Refresh() {
        if(config==null||playing==null)return;
        float time=playing.wrapMode==WrapMode.Loop?Mathf.Repeat(playing.time,playing.length):Mathf.Min(playing.time,playing.length);
        for(int i=0;i<config.geosets.Length;i++) {
            float alpha=config.geosets[i].Evaluate(clipInterval,time*1000)*config.layers[i].Evaluate(clipInterval,time*1000);
            if(Mathf.Approximately(previous[i],alpha))continue;
            previous[i]=alpha;block.SetFloat("_NativeAlpha",alpha);skin.SetPropertyBlock(block,i);
        }
    }
    public bool SurfaceVisible(int index) {if(config==null||playing==null)return true;float ms=playing.time*1000;return config.geosets[index].Evaluate(clipInterval,ms)*config.layers[index].Evaluate(clipInterval,ms)>=.01f;}
    public static float Sample(JObject track,JArray interval,float positionMs) {
        float fallback=(float?)track["alpha"]??1;
        var frames=(JArray)track["frames"];var values=(JArray)track["values"];if(frames==null||frames.Count==0)return fallback;
        float start=(float)interval[0],end=(float)interval[1],time=start+positionMs;
        float duration=(float?)track["global"]??0;if(duration>0){start=0;end=duration;time=Mathf.Repeat(Time.time*1000,duration);}
        int left=-1,right=-1;
        for(int i=0;i<frames.Count;i++) {
            float frame=(float)frames[i];if(frame<start||frame>end)continue;
            if(left<0||frame<=time)left=i;if(frame>time){right=i;break;}
        }
        if(left<0)return fallback;
        if(right<0||(int?)track["interpolation"]==0)return (float)values[left];
        return Mathf.Lerp((float)values[left],(float)values[right],Mathf.Clamp01((time-(float)frames[left])/Mathf.Max(1,(float)frames[right]-(float)frames[left])));
    }
}
}
