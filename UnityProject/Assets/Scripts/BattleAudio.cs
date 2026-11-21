using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed class BattleAudio : MonoBehaviour {
    static readonly ConditionalWeakTable<HumanMatch,BattleAudio> sessions = new ConditionalWeakTable<HumanMatch,BattleAudio>();
    static readonly int[] steps={7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,80,88,97,107,118,130,143,157,173,190,209,230,253,279,307,337,371,408,449,494,544,598,658,724,796,876,963,1060,1166,1282,1411,1552,1707,1878,2066,2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,8630,9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,29794,32767};
    static readonly int[] offsets={-1,-1,-1,-1,2,4,6,8};
    readonly Dictionary<int,AudioClip> cache=new Dictionary<int,AudioClip>();
    readonly Queue<int> order=new Queue<int>();
    readonly Dictionary<string,float> next=new Dictionary<string,float>();
    readonly AudioSource[] voices=new AudioSource[10];
    readonly Dictionary<string,string> paths=new Dictionary<string,string>();
    Func<Vector2,Vector3> world;
    Func<Vector2,bool> visible;
    JObject catalog;
    byte[] block;
    int blockId=-1,serial;
    int frame=-1,startedInFrame;
    HumanMatch battle;
    public static BattleAudio For(HumanMatch match)=>match!=null&&sessions.TryGetValue(match,out var value)?value:null;
    public void Bind(HumanMatch match,Func<Vector2,Vector3> position,Func<Vector2,bool> sight){
        battle=match;world=position;visible=sight;sessions.Add(match,this);
        var asset=PepeContent.Load<TextAsset>("Pepe/BattleSounds");
        if(asset)catalog=JObject.Parse(asset.text);
    }
    void OnDestroy(){if(battle!=null)sessions.Remove(battle);foreach(var clip in cache.Values)if(clip)Destroy(clip);}
    AudioClip Sample(int id){
        if(cache.TryGetValue(id,out var clip))return clip;
        var row=catalog["clips"][id];int offset=(int)row[0],length=(int)row[1],hz=(int)row[2],part=(int)row[3];
        if(blockId!=part){
            var asset=PepeContent.Load<TextAsset>("Pepe/BattleSamples"+part);
            if(!asset)return null;
            block=asset.bytes;blockId=part;Resources.UnloadAsset(asset);
        }
        // Classic samples use high-nibble-first IMA ADPCM, with an independent predictor per take.
        var samples=new float[length*2];int predictor=0,index=0;
        for(int i=0;i<samples.Length;i++){
            int data=block[offset+i/2];int nibble=(i%2==0?data>>4:data&15),step=steps[index],delta=step>>3;
            if((nibble&4)!=0)delta+=step;if((nibble&2)!=0)delta+=step>>1;if((nibble&1)!=0)delta+=step>>2;
            predictor=Mathf.Clamp(predictor+((nibble&8)==0?delta:-delta),-32768,32767);
            index=Mathf.Clamp(index+offsets[nibble&7],0,88);samples[i]=predictor/32768f;
        }
        clip=AudioClip.Create("WC3 combat "+id,samples.Length,1,hz,false);clip.SetData(samples,0);
        if(order.Count>=48){int old=order.Dequeue();Destroy(cache[old]);cache.Remove(old);}
        cache.Add(id,clip);order.Enqueue(id);return clip;
    }
    string Normalize(string path){
        if(string.IsNullOrEmpty(path))return "";
        if(paths.TryGetValue(path,out string normalized))return normalized;
        normalized=path.Replace('/','\\').ToLowerInvariant();
        if(normalized.EndsWith(".mdl"))normalized=normalized.Substring(0,normalized.Length-4)+".mdx";
        else if(!normalized.EndsWith(".mdx"))normalized+=".mdx";
        paths[path]=normalized;return normalized;
    }
    string Field(string table,string key,string field)=>key==null?null:(string)catalog?[table]?[key]?[field];
    public void Death(string unit,Vector2 at,bool building=false)=>Play(Field("units",unit,"death")??(building?"DeathHumanLargeBuilding":"FootmanDeath"),at);
    public void Spell(string key,Vector2 at){if(key!=null)Play((string)catalog?["spells"]?[key]??(string)catalog?["fx"]?[key],at);}
    public void Swing(string unit,Vector2 at)=>Play(Field("units",unit,"swing"),at);
    public void LocalMissile(HumanMatch.Shot shot,bool hit){
        string path=shot.weapon?.missile;
        if(shot.line>=0 && shot.line<battle.Lines.Length && string.IsNullOrEmpty(shot.chaosSpell)
           && (string.IsNullOrEmpty(path)||path.IndexOf(':')<0)){
            var line=battle.Lines[shot.line];var shooter=battle.VisualUnit036(shot.sourceId);
            int buildingSlot=Array.IndexOf(line.buildingWeapons,shot.weapon);
            bool building=shooter==null && (buildingSlot>=0||!line.Producer);
            string raw=building?line.building:(shooter==null?line.unit:battle.Lines[shooter.kind].unit);
            int slot=building?buildingSlot:Array.IndexOf(line.weapons,shot.weapon);
            path=(string)catalog?["weapons"]?[raw+"_"+(Math.Max(0,slot)+1)]??path;
        }
        Missile(path,shot.kind,hit,hit?shot.end:shot.start);
    }
    public void Missile(string missile,int kind,bool hit,Vector2 at){
        string key=Field("missiles",Normalize(missile),hit?"hit":"launch");
        if(key!=null&&catalog?["groups"]?[key]==null)key=null;
        if(key==null)key=kind==2?(hit?"BallistaMissileHit":"MortarTeamAttack1"):kind==1?(hit?null:"RiflemanAttack1"):(hit?"ArrowHit":"ArrowLaunch");
        Play(key,at);
    }
    public void Contact(string source,string target,bool structure,Vector2 at){
        string weapon=Field("units",source,"weapon");
        string armor=structure?"Stone":Field("units",target,"armor");
        if(string.IsNullOrEmpty(weapon)||weapon=="_"||weapon=="-")weapon="MetalMediumSlice";
        if(string.IsNullOrEmpty(armor)||armor=="_")armor="Flesh";
        string key=weapon+armor;
        Play(catalog?["groups"]?[key]!=null?key:("MetalMediumSlice"+(structure?"Stone":"Flesh")),at);
    }
    void Play(string key,Vector2 at){
        if(key==null||catalog==null||AudioListener.volume<=0||(frame==Time.frameCount&&startedInFrame>=3)||
            next.TryGetValue(key,out var cooldown)&&Time.unscaledTime<cooldown||!visible(at))return;
        var variants=catalog["groups"][key] as JArray;if(variants==null||variants.Count==0)return;
        var cam=Camera.main;if(!cam)return;var point=cam.WorldToViewportPoint(world(at));
        if(point.z<=0||point.x<-.08f||point.x>1.08f||point.y<-.08f||point.y>1.08f)return;
        for(int i=0;i<voices.Length;i++){
            var voice=voices[i];if(voice&&voice.isPlaying)continue;
            var clip=Sample((int)variants[serial++%variants.Count]);if(!clip)return;
            if(!voice){voice=voices[i]=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;}
            voice.panStereo=Mathf.Clamp((point.x-.5f)*1.4f,-.8f,.8f);
            voice.volume=.35f*Mathf.Clamp01(1-.35f*Mathf.Abs(point.x-.5f));voice.clip=clip;voice.Play();
            float now=Time.unscaledTime;next[key]=now+.075f;if(frame!=Time.frameCount){frame=Time.frameCount;startedInFrame=0;}startedInFrame++;return;
        }
    }
}
}
