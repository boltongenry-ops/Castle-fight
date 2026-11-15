using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    sealed class ProjectileTrack {public readonly ProjectileMotion motion=new ProjectileMotion();public float born=float.NegativeInfinity,retire=float.PositiveInfinity;}
    readonly Dictionary<int,ProjectileTrack> projectileTracks=new Dictionary<int,ProjectileTrack>();
    readonly List<int> retiredProjectiles=new List<int>();
    float projectileLatest,projectileRender,projectileSince;bool projectileClock;
    Vector3 NetworkProjectilePoint(JArray p)=>new Vector3((networkSlot==1?-1:1)*(float)p[0],(float)p[1],-(float)p[2]);
    void ClearOnlineProjectiles(){foreach(var obj in onlineProjectiles.Values)if(obj)Destroy(obj);onlineProjectiles.Clear();projectileTracks.Clear();projectileClock=false;}
    void ProjectileStamp(float stamp){
        if(!projectileClock||stamp<projectileLatest-.001f){ClearOnlineProjectiles();projectileRender=stamp-ProjectileMotion.Delay;projectileClock=true;}
        projectileLatest=stamp;projectileSince=0;
    }
    void UpdateOnlineProjectiles(float delta){
        if(!projectileClock)return;
        projectileSince+=delta;float desired=projectileLatest+Mathf.Min(projectileSince,.2f)-ProjectileMotion.Delay;
        if(Mathf.Abs(desired-projectileRender)>.5f)projectileRender=desired;
        projectileRender=Mathf.Min(projectileLatest,projectileRender+delta*Mathf.Clamp(1+(desired-projectileRender)*2,.8f,1.2f));
        // No per-frame LINQ/list allocation for the live projectiles.
        retiredProjectiles.Clear();
        foreach(var pair in projectileTracks)if(onlineProjectiles.TryGetValue(pair.Key,out var obj)&&obj){
            var track=pair.Value;var pose=track.motion.Sample(Mathf.Min(projectileRender,track.retire));obj.transform.SetPositionAndRotation(pose.position,pose.rotation);
            obj.SetActive(projectileRender>=track.born&&(!onlineMatch||!onlineFog||FogVisible(new Vector2(pose.position.x,-pose.position.z))));
            if(projectileRender>=track.retire)retiredProjectiles.Add(pair.Key);
        }
        foreach(int id in retiredProjectiles){
            if(onlineProjectiles.TryGetValue(id,out var obj)&&obj){var classic=obj.GetComponent<ClassicProjectileModel>();if(classic&&obj.activeSelf)Destroy(obj,classic.Finish());else Destroy(obj);}
            onlineProjectiles.Remove(id);projectileTracks.Remove(id);
        }
    }
    bool ProjectileBorn(int id)=>!projectileTracks.TryGetValue(id,out var track)||projectileRender>=track.born;
}
}
