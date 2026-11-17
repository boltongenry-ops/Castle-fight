using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    // effect_runtime.gd sync uses an explicit innate whitelist. Channel markers
    // inherit DeathPact target art but never maintain it on living actors.
    static readonly HashSet<string> nativePersistentIds=new HashSet<string>{"A05O","A06O","A06G","A013","A08P","A06W","A06X","A06Y","A08F","A07Y","A053","A058","A0E5","A0E6"};
    bool NativePersistentArt(HumanMatch.Unit unit,string id)=>nativePersistentIds.Contains(id)||unit.nightInvisible&&(string)match.Abilities[id]?["kind"]=="wind_walk";
    JObject nightFxIndex;JArray nightArtAspects;
    readonly Dictionary<string,GameObject> nightFxPrefabs=new Dictionary<string,GameObject>();
    readonly Dictionary<string,PcModel> nightAttachments=new Dictionary<string,PcModel>();
    void LoadNightArt(){if(nightFxIndex!=null)return;var asset=ContentResources.Load<TextAsset>("night-fx-index");nightFxIndex=asset?JObject.Parse(asset.text):new JObject();nightArtAspects=nightPack?(JArray)JObject.Parse(nightPack.catalog.text)["aspects"]:new JArray();}
    GameObject NightFx(string key,int team){if(string.IsNullOrEmpty(key))return null;string id=key+"-"+team;if(!nightFxPrefabs.TryGetValue(id,out var prefab)){prefab=ContentResources.Load<GameObject>("NightFx/"+id);nightFxPrefabs[id]=prefab;}return prefab;}
    void ResolveNightDefaultColors(){LoadNightArt();if(!(nightFxIndex["defaultWhite"] is JObject defaults))return;foreach(var p in defaults.Properties())for(int team=0;team<2;team++){var prefab=NightFx(p.Name,team);if(!prefab)continue;foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(int i in (JArray)p.Value){var m=skin.sharedMaterials[i];if(m&&!m.mainTexture)m.mainTexture=Texture2D.whiteTexture;}}}
    GameObject NightMissile(HumanMatch.Shot shot){if(match==null)return null;LoadNightArt();string key=null;if((shot.weapon?.missile??"").StartsWith("night:"))key=(string)nightFxIndex["visuals"]?[shot.weapon.missile.Substring(6)]?["missile"]?[0]?["key"];else {string raw=shot.sourceId>=0?match.Units.FirstOrDefault(u=>u.id==shot.sourceId) is HumanMatch.Unit u?match.Lines[u.kind].unit:null:null;key=(string)nightFxIndex["weapons"]?[raw+"_1"];if(key==null&&shot.line>=0&&shot.line<match.Lines.Length)key=(string)nightFxIndex["weapons"]?[match.Lines[shot.line].building+"_1"]??(string)nightFxIndex["weapons"]?[match.Lines[shot.line].unit+"_1"];}return NightFx(key,shot.team);}
    void PlaceNight(HashSet<string> wanted,string id,string key,int team,Vector3 point,Vector2 direction=default){var prefab=NightFx(key,team);if(!prefab)return;wanted.Add(id);if(!nightAttachments.TryGetValue(id,out var model)||!model){model=Instantiate(prefab,visuals.transform).GetComponent<PcModel>();if(!model.particleOnly&&model.clips.Length>0)model.Play(model.clips.Any(c=>c.name.StartsWith("Stand"))?"Stand":model.clips[0].name);nightAttachments[id]=model;}model.transform.position=point;if(direction.sqrMagnitude>0)model.Face(direction);}
    Vector3 NightAnchor(PcModel view,string attachment){LoadNightArt();var tokens=(attachment??"origin").ToLowerInvariant().Split(new[]{',',' '},StringSplitOptions.RemoveEmptyEntries);var point=(nightFxIndex["attachments"]?[view.raw]?["points"] as JArray??new JArray()).FirstOrDefault(p=>tokens.All(t=>((string)p["name"]).ToLowerInvariant().Contains(t)));if(point==null)return view.transform.position;var offset=(JArray)point["offset"];var v=new Vector3(-(float)offset[0],(float)offset[1],(float)offset[2]);string bone=(string)point["bone"];var node=view.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==bone);return node?node.TransformPoint(v):view.animatedRoot.transform.TransformPoint(v);}
    void NightBuff(HashSet<string> wanted,string prefix,PcModel view,int team,string ability){int i=0;foreach(var art in nightFxIndex["visuals"]?[ability]?["buff"] as JArray??new JArray()){string id=prefix+"-"+ability+"-"+i++;PlaceNight(wanted,id,(string)art["key"],team,NightAnchor(view,(string)art["attach"]));}}

    void RenderNightEffects(){LoadNightArt();var wanted=new HashSet<string>();
        foreach(var u in match.Units)if(u.hp>0&&units.TryGetValue(u.id,out var view)&&ShowUnitArt036(u,view)){

            foreach(string ability in u.abilities.Where(id=>NativePersistentArt(u,id)))NightBuff(wanted,"innate-"+u.id,view,u.team,ability);
            foreach(string ability in u.effects.Where(e=>e.left>0).Select(e=>e.id).Concat(u.auraIds).Distinct())NightBuff(wanted,"status-"+u.id,view,u.team,ability);
            if(selectedUnit==u.id&&match.TeamNight[u.team]&&match.Aspects[u.team]>=0){var aspect=(JObject)nightArtAspects[match.Aspects[u.team]];PlaceNight(wanted,"aspect-"+u.id,(string)aspect["visual"]?["buff"]?["key"],u.team,view.transform.position);}
        }
        foreach(var h in match.Huts)if(huts.TryGetValue(h.id,out var view)){

            int i=0;foreach(var row in nightFxIndex["decorations"]?[match.Lines[h.kind].building] as JArray??new JArray()){string raw=(string)row[0];foreach(var art in nightFxIndex["visuals"]?["night_decor_"+raw]?["effect"] as JArray??new JArray()){var point=view.transform.TransformPoint(new Vector3((float)row[2]/30,0,-(float)row[3]/30));string id="decor-"+h.id+"-"+i++;PlaceNight(wanted,id,(string)art["key"],h.team,point);if(nightAttachments.TryGetValue(id,out var effect)){effect.transform.rotation=view.transform.rotation*Quaternion.Euler(0,90-(float)row[1],0);var profile=nightFxIndex["decorProfiles"]?[raw];effect.transform.localScale=Vector3.one*HumanMatch.F(profile,"scale",1);var tint=(JArray)profile?["tint"];if(tint!=null)foreach(var renderer in effect.GetComponentsInChildren<SkinnedMeshRenderer>())for(int m=0;m<renderer.sharedMaterials.Length;m++){var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,m);block.SetColor("_Tint",new Color((float)tint[0],(float)tint[1],(float)tint[2],(float)tint[3]));renderer.SetPropertyBlock(block,m);}}}}
        }
        foreach(var zone in match.NightZones)if(zone.age<8)PlaceNight(wanted,"starfall-"+zone.id,(string)nightFxIndex["visuals"]?["night_starfall_caster"]?["effect"]?[0]?["key"],zone.team,World(zone.caster));
        foreach(var pulse in match.Pulses){if(!ShowPulse036(pulse))continue;if(!(pulse.fx??"").StartsWith("night:"))continue;var split=pulse.fx.Split(':');if(split.Length!=3)continue;int i=0;JArray art=nightFxIndex["visuals"]?[split[1]]?[split[2]] as JArray??new JArray();foreach(var row in art){string id="pulse-"+pulse.id+"-"+i++;PlaceNight(wanted,id,(string)row["key"],0,World(pulse.position));MarkPulse036(pulse.id,wanted,id);}}
        foreach(string id in nightAttachments.Keys.Where(id=>!wanted.Contains(id)).ToArray()){if(nightAttachments[id])Destroy(nightAttachments[id].gameObject);nightAttachments.Remove(id);}
    }
    void AuditNightParticles(){int count=0;foreach(var prefab in ContentResources.LoadAll<GameObject>("NightFx"))foreach(var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>()){var material=renderer.sharedMaterial;if(!material||!material.shader||!material.shader.isSupported||!material.mainTexture)throw new InvalidOperationException("Invalid Night particle material: "+prefab.name);count++;}Debug.Log("CF_NIGHT_PARTICLE_AUDIT_PASS materials="+count);}
    void ClearNightViews(){nightAttachments.Clear();}
}
}
