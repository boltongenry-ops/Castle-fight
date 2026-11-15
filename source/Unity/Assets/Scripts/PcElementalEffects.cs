using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    JObject elementalFxIndex;JArray elementalArtAspects;
    readonly Dictionary<string,GameObject> elementalFxPrefabs=new Dictionary<string,GameObject>();
    readonly Dictionary<string,PcModel> elementalAttachments=new Dictionary<string,PcModel>();
    void LoadElementalArt(){if(elementalFxIndex!=null)return;var asset=ContentResources.Load<TextAsset>("elemental-fx-index");elementalFxIndex=asset?JObject.Parse(asset.text):new JObject();elementalArtAspects=elementalPack?(JArray)JObject.Parse(elementalPack.catalog.text)["aspects"]:new JArray();}
    GameObject ElementalFx(string key,int team){if(string.IsNullOrEmpty(key))return null;string id=key+"-"+team;if(!elementalFxPrefabs.TryGetValue(id,out var prefab)){prefab=ContentResources.Load<GameObject>("ElementalFx/"+id);elementalFxPrefabs[id]=prefab;}return prefab;}
    void ResolveElementalDefaultColors(){LoadElementalArt();if(!(elementalFxIndex["defaultWhite"] is JObject defaults))return;foreach(var p in defaults.Properties())for(int team=0;team<2;team++){var prefab=ElementalFx(p.Name,team);if(!prefab)continue;foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(int i in (JArray)p.Value){var m=skin.sharedMaterials[i];if(m&&!m.mainTexture)m.mainTexture=Texture2D.whiteTexture;}}}
    GameObject ElementalMissile(HumanMatch.Shot shot){if(match==null)return null;LoadElementalArt();string key=null;if((shot.weapon?.missile??"").StartsWith("elemental:"))key=(string)elementalFxIndex["visuals"]?[shot.weapon.missile.Substring(10)]?["missile"]?[0]?["key"];else {string raw=shot.sourceId>=0?match.Units.FirstOrDefault(u=>u.id==shot.sourceId) is HumanMatch.Unit u?match.Lines[u.kind].unit:null:null;key=(string)elementalFxIndex["weapons"]?[raw+"_1"];if(key==null&&shot.line>=0&&shot.line<match.Lines.Length)key=(string)elementalFxIndex["weapons"]?[match.Lines[shot.line].building+"_1"]??(string)elementalFxIndex["weapons"]?[match.Lines[shot.line].unit+"_1"];}return ElementalFx(key,shot.team);}
    void PlaceElemental(HashSet<string> wanted,string id,string key,int team,Vector3 point,Vector2 direction=default){var prefab=ElementalFx(key,team);if(!prefab)return;wanted.Add(id);if(!elementalAttachments.TryGetValue(id,out var model)||!model){model=Instantiate(prefab,visuals.transform).GetComponent<PcModel>();if(!model.particleOnly)model.Play(model.clips.Any(c=>c.name.StartsWith("Stand"))?"Stand":model.clips[0].name);elementalAttachments[id]=model;}model.transform.position=point;if(direction.sqrMagnitude>0)model.Face(direction);}
    Vector3 ElementalAnchor(PcModel view,string attachment){LoadElementalArt();var tokens=(attachment??"origin").ToLowerInvariant().Split(new[]{',',' '},StringSplitOptions.RemoveEmptyEntries);var point=(elementalFxIndex["attachments"]?[view.raw]?["points"] as JArray??new JArray()).FirstOrDefault(p=>tokens.All(t=>((string)p["name"]).ToLowerInvariant().Contains(t)));if(point==null)return view.transform.position;var offset=(JArray)point["offset"];var v=new Vector3(-(float)offset[0],(float)offset[1],(float)offset[2]);string bone=(string)point["bone"];var node=view.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==bone);return node?node.TransformPoint(v):view.animatedRoot.transform.TransformPoint(v);}
    void ElementalBuff(HashSet<string> wanted,string prefix,PcModel view,int team,string ability){int i=0;foreach(var art in elementalFxIndex["visuals"]?[ability]?["buff"] as JArray??new JArray()){string id=prefix+"-"+ability+"-"+i++;PlaceElemental(wanted,id,(string)art["key"],team,ElementalAnchor(view,(string)art["attach"]));}}

    void RenderElementalEffects(float alpha){LoadElementalArt();var wanted=new HashSet<string>();
        foreach(var u in match.Units)if(u.hp>0&&units.TryGetValue(u.id,out var view)){

            foreach(string ability in u.abilities.Where(id=>NativePersistentArt(u,id)))ElementalBuff(wanted,"innate-"+u.id,view,u.team,ability);
            foreach(string ability in u.effects.Where(e=>e.left>0).Select(e=>e.id).Concat(u.auraIds).Distinct())ElementalBuff(wanted,"status-"+u.id,view,u.team,ability);
            if(selectedUnit==u.id&&match.TeamElemental[u.team]&&match.Aspects[u.team]>=0){var aspect=(JObject)elementalArtAspects[match.Aspects[u.team]];PlaceElemental(wanted,"aspect-"+u.id,(string)aspect["visual"]?["buff"]?["key"],u.team,view.transform.position);}
        }
        foreach(var h in match.Huts)if(huts.TryGetValue(h.id,out var view)){

            int i=0;foreach(var row in elementalFxIndex["decorations"]?[match.Lines[h.kind].building] as JArray??new JArray()){string raw=(string)row[0];foreach(var art in elementalFxIndex["visuals"]?["elemental_decor_"+raw]?["effect"] as JArray??new JArray()){var point=view.transform.TransformPoint(new Vector3((float)row[2]/30,0,-(float)row[3]/30));string id="decor-"+h.id+"-"+i++;PlaceElemental(wanted,id,(string)art["key"],h.team,point);if(elementalAttachments.TryGetValue(id,out var effect)){effect.transform.rotation=view.transform.rotation*Quaternion.Euler(0,90-(float)row[1],0);var profile=elementalFxIndex["decorProfiles"]?[raw];effect.transform.localScale=Vector3.one*HumanMatch.F(profile,"scale",1);var tint=(JArray)profile?["tint"];if(tint!=null)foreach(var renderer in effect.GetComponentsInChildren<SkinnedMeshRenderer>())for(int m=0;m<renderer.sharedMaterials.Length;m++){var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,m);block.SetColor("_Tint",new Color((float)tint[0],(float)tint[1],(float)tint[2],(float)tint[3]));renderer.SetPropertyBlock(block,m);}}}}
        }
        foreach(var shot in match.ElementalRays.Concat(match.ElementalSpheres)){
            string raw=match.ElementalRays.Contains(shot)?"h03H":"h068";string key=(string)elementalFxIndex["visuals"]?["elemental_decor_"+raw]?["effect"]?[0]?["key"];
            var point=Vector2.Lerp(shot.previous,shot.point,alpha);PlaceElemental(wanted,"projectile-"+shot.id,key,shot.team,World(point)+Vector3.up*HumanMatch.F(elementalFxIndex["decorProfiles"]?[raw],"height",1),new Vector2(Mathf.Cos(shot.angle),Mathf.Sin(shot.angle)));
            if(elementalAttachments.TryGetValue("projectile-"+shot.id,out var model))model.transform.localScale=Vector3.one*HumanMatch.F(elementalFxIndex["decorProfiles"]?[raw],"scale",1);
        }
        foreach(var wave in match.ElementalWaves)PlaceElemental(wanted,"wave-"+wave.id,(string)elementalFxIndex["visuals"]?["A0CN"]?["missile"]?[0]?["key"],wave.team,World(wave.origin+wave.direction*wave.distance)+Vector3.up,wave.direction);
        foreach(var pulse in match.Pulses){if(!(pulse.fx??"").StartsWith("elemental:"))continue;var split=pulse.fx.Split(':');if(split.Length!=3)continue;int i=0;JArray art=elementalFxIndex["visuals"]?[split[1]]?[split[2]] as JArray??new JArray();foreach(var row in art)PlaceElemental(wanted,"pulse-"+pulse.id+"-"+i++,(string)row["key"],0,World(pulse.position));}
        foreach(string id in elementalAttachments.Keys.Where(id=>!wanted.Contains(id)).ToArray()){if(elementalAttachments[id])Destroy(elementalAttachments[id].gameObject);elementalAttachments.Remove(id);}
    }
    void AuditElementalParticles(){int count=0;foreach(var prefab in ContentResources.LoadAll<GameObject>("ElementalFx"))foreach(var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>()){var material=renderer.sharedMaterial;if(!material||!material.shader||!material.shader.isSupported||!material.mainTexture)throw new InvalidOperationException("Invalid Elemental particle material: "+prefab.name);count++;}Debug.Log("CF_ELEMENTAL_PARTICLE_AUDIT_PASS materials="+count);}
    void ClearElementalViews(){elementalAttachments.Clear();}
}
}
