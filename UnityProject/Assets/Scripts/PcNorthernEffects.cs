using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    readonly Dictionary<string,GameObject> northernFxPrefabs=new Dictionary<string,GameObject>();
    readonly Dictionary<string,PcModel> northernAttachments=new Dictionary<string,PcModel>();
    readonly Dictionary<int,Transform> northernOrbViews=new Dictionary<int,Transform>();
    ParticleSystem northernSnow;Material northernOrbMaterial;
    GameObject NorthernFx(string key) {if(!northernFxPrefabs.TryGetValue(key,out var prefab)){prefab=ContentResources.Load<GameObject>("NorthernFx/"+key);northernFxPrefabs[key]=prefab;}return prefab;}
    GameObject NorthernMissile(HumanMatch.Shot shot) {
        string tag=shot.chaosSpell??"";
        if(tag=="north_barrage")return NorthernFx("fx_6d82ad02f70a");
        if(tag=="north_spell:A080")return NorthernFx("fx_0839d00968c3");
        if(tag=="north_spell:A0AK"||tag=="north_spell:A082")return NorthernFx("fx_b6599866d740");
        return null;
    }
    void PlaceNorthernFx(HashSet<string> wanted,string id,string art,Vector3 position) {
        wanted.Add(id);if(!northernAttachments.TryGetValue(id,out var model)||!model){var prefab=NorthernFx(art);if(!prefab)return;model=Instantiate(prefab,visuals.transform).GetComponent<PcModel>();northernAttachments[id]=model;model.Play("Stand");}
        model.transform.position=position;
    }
    void RenderNorthernEffects(float alpha) {
        var wanted=new HashSet<string>();
        foreach(var unit in match.Units)if(unit.hp>0&&units.TryGetValue(unit.id,out var view)) {
            if(unit.effects.Any(e=>e.left>0&&e.group=="northern_ice_chains"))PlaceNorthernFx(wanted,"ice-unit-"+unit.id,"fx_759aa9a57831",view.transform.position);
            if(unit.effects.Any(e=>e.left>0&&(bool?)e.config?["frost_armor"]==true))PlaceNorthernFx(wanted,"armor-"+unit.id,"fx_96b9f2d40527",view.transform.position+Vector3.up*Mathf.Max(.7f,view.ClassicHeight*.55f));
        }
        foreach(var hut in match.Huts)if(match.FrostDisabled(hut)>0)PlaceNorthernFx(wanted,"ice-hut-"+hut.id,"fx_759aa9a57831",World(hut.position));
        foreach(var freeze in match.NorthernFreezes)if(freeze.target==-1&&freeze.left>0)PlaceNorthernFx(wanted,"ice-castle-"+(1-freeze.team),"fx_759aa9a57831",World(HumanMatch.CastlePosition(1-freeze.team)));
        foreach(string id in northernAttachments.Keys.Where(id=>!wanted.Contains(id)).ToArray()){if(northernAttachments[id])Destroy(northernAttachments[id].gameObject);northernAttachments.Remove(id);}
        foreach(var orb in match.NorthernOrbs) {
            if(!northernOrbViews.TryGetValue(orb.id,out var body)||!body) {
                var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.name="Northern winter orb "+orb.id;sphere.transform.SetParent(visuals.transform);Destroy(sphere.GetComponent<Collider>());body=sphere.transform;body.localScale=Vector3.one*1.15f;
                if(!northernOrbMaterial){northernOrbMaterial=new Material(impactMaterial);northernOrbMaterial.SetColor("_Color",new Color(.35f,.75f,1));}
                sphere.GetComponent<Renderer>().sharedMaterial=northernOrbMaterial;
                var trail=sphere.AddComponent<TrailRenderer>();trail.sharedMaterial=northernOrbMaterial;trail.time=.5f;trail.startWidth=.7f;trail.endWidth=0;trail.minVertexDistance=.15f;northernOrbViews[orb.id]=body;
            }
            float blend=Mathf.Clamp01(orb.accumulator/.04f);var position=testing&&testHoldAuthority?orb.position:Vector2.Lerp(orb.previous,orb.position,blend);
            body.position=World(position)+Vector3.up*HumanMatch.F(match.Abilities["A04I"],"orbFlyHeight",10);
        }
        foreach(int id in northernOrbViews.Keys.Where(id=>!match.NorthernOrbs.Any(v=>v.id==id)).ToArray()){if(northernOrbViews[id])Destroy(northernOrbViews[id].gameObject);northernOrbViews.Remove(id);}
        RenderNorthernSnow();
    }
    void RenderNorthernSnow() {
        if(match.NorthernWeather>0&&!northernSnow) {
            var obj=new GameObject("Native northern heavy snow");obj.transform.SetParent(visuals.transform);northernSnow=obj.AddComponent<ParticleSystem>();
            var cfg=JObject.Parse(ContentResources.Load<TextAsset>("northern-weather").text)["native"];float speed=Mathf.Abs(HumanMatch.F(cfg,"veloc"))/30,lifetime=HumanMatch.F(cfg,"lifespan",2.5f);
            var main=northernSnow.main;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=lifetime;main.startSpeed=0;main.startSize=HumanMatch.F(cfg,"scaleStart")/30;main.maxParticles=400;main.gravityModifier=0;
            var emission=northernSnow.emission;emission.rateOverTime=400/lifetime;
            var shape=northernSnow.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(150,0,90);
            var velocity=northernSnow.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=.3f*speed;velocity.y=-speed;velocity.z=.3f*speed;
            northernSnow.GetComponent<ParticleSystemRenderer>().sharedMaterial=ContentResources.Load<Material>("NorthernSnow");northernSnow.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if(!northernSnow)return;
        var ray=Camera.main.ViewportPointToRay(new Vector3(.5f,.5f));var plane=new Plane(Vector3.up,new Vector3(0,World(Vector2.zero).y,0));Vector3 center=plane.Raycast(ray,out float distance)?ray.GetPoint(distance):Camera.main.transform.position;
        northernSnow.transform.position=center+Vector3.up*(768f/30);
        if(match.NorthernWeather>0&&!northernSnow.isPlaying)northernSnow.Play();else if(match.NorthernWeather<=0&&northernSnow.isEmitting)northernSnow.Stop(true,ParticleSystemStopBehavior.StopEmitting);
    }
    void ClearNorthernViews(){northernAttachments.Clear();northernOrbViews.Clear();northernSnow=null;if(northernOrbMaterial)Destroy(northernOrbMaterial);northernOrbMaterial=null;}
}
}
