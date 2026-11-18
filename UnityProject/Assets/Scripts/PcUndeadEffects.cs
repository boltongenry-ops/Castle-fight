using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    JObject undeadFxIndex;
    readonly Dictionary<string,GameObject> undeadFxPrefabs=new Dictionary<string,GameObject>();
    readonly Dictionary<string,PcModel> undeadAttachments=new Dictionary<string,PcModel>();
    void LoadUndeadArt(){if(undeadFxIndex!=null)return;var asset=ContentResources.Load<TextAsset>("undead-fx-index");undeadFxIndex=asset?JObject.Parse(asset.text):new JObject();}
    GameObject UndeadFx(string key,int team){if(string.IsNullOrEmpty(key))return null;string id=key+"-"+team;if(!undeadFxPrefabs.TryGetValue(id,out var prefab)){prefab=ContentResources.Load<GameObject>("UndeadFx/"+id);undeadFxPrefabs[id]=prefab;}return prefab;}
    void ResolveUndeadDefaultColors(){LoadUndeadArt();if(!(undeadFxIndex["defaultWhite"] is JObject defaults))return;foreach(var p in defaults.Properties())for(int team=0;team<2;team++){var prefab=UndeadFx(p.Name,team);if(!prefab)continue;foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(int i in (JArray)p.Value){var m=skin.sharedMaterials[i];if(m&&!m.mainTexture)m.mainTexture=Texture2D.whiteTexture;}}}
    GameObject UndeadMissile(HumanMatch.Shot shot){if(match==null)return null;LoadUndeadArt();string raw=shot.sourceId>=0?match.Units.FirstOrDefault(u=>u.id==shot.sourceId) is HumanMatch.Unit u?match.Lines[u.kind].unit:null:null;string key=(string)undeadFxIndex["weapons"]?[raw+"_1"];if(key==null&&shot.line>=0&&shot.line<match.Lines.Length){string id=match.Lines[shot.line].building;key=(string)undeadFxIndex["weapons"]?[id+"_1"]??(string)undeadFxIndex["weapons"]?[match.Lines[shot.line].unit+"_1"];}return UndeadFx(key,shot.team);}
    void PlaceUndead(HashSet<string> wanted,string id,string key,int team,Vector3 point,Vector2 direction=default,float elapsed=-1){if(TryPlaceHdArt040(id,key,team,point,direction,elapsed)){MarkHdPulse040(id);return;}var prefab=UndeadFx(key,team);if(!prefab)return;wanted.Add(id);if(!undeadAttachments.TryGetValue(id,out var model)||!model){model=Instantiate(prefab,visuals.transform).GetComponent<PcModel>();if(!model.particleOnly&&model.clips.Length>0)model.Play(model.clips.Any(c=>c.name.StartsWith("Stand"))?"Stand":model.clips[0].name);undeadAttachments[id]=model;}model.transform.position=point;if(direction.sqrMagnitude>0)model.Face(direction);}
    Vector3 UndeadAnchor(PcModel view,string attachment){var all=view.GetComponentsInChildren<Transform>(true);string text=(attachment??"origin").ToLowerInvariant();string[] names=text.Contains("left")?new[]{"Hand Left Ref","Bone_Hand_L","Bone_Weapon_L"}:text.Contains("right")?new[]{"Hand Right Ref","Bone_Hand_R","Bone_Weapon_R"}:text.Contains("head")?new[]{"Head Ref","Bone_Head"}:text.Contains("chest")?new[]{"Chest Ref","Bone_Chest"}:new string[0];var node=all.FirstOrDefault(v=>names.Any(n=>v.name.Equals(n,StringComparison.OrdinalIgnoreCase)));return node?node.position:view.transform.position+Vector3.up*(text.Contains("head")||text.Contains("overhead")?view.ClassicHeight:text.Contains("chest")?view.ClassicHeight*.55f:0);}
    void RenderUndeadEffects(){LoadUndeadArt();var wanted=new HashSet<string>();
        foreach(var u in match.Units)if(u.hp>0&&units.TryGetValue(u.id,out var view)&&ShowUnitArt036(u,view)){
            foreach(string ability in u.abilities.Where(id=>id=="A05O"||id=="A06O")){int i=0;foreach(var art in undeadFxIndex["visuals"]?[ability]?["buff"] as JArray??new JArray())PlaceUndead(wanted,"innate-"+u.id+"-"+i++,(string)art["key"],u.team,UndeadAnchor(view,(string)art["attach"]));}
            var seen=new HashSet<string>();foreach(string ability in u.effects.Where(e=>e.left>0).Select(e=>e.id.StartsWith("aspect_plague_")?"A05R":e.id).Concat(u.auraIds))if(seen.Add(ability)){int i=0;foreach(var art in undeadFxIndex["visuals"]?[ability]?["buff"] as JArray??new JArray())PlaceUndead(wanted,"status-"+u.id+"-"+ability+"-"+i++,(string)art["key"],u.team,UndeadAnchor(view,(string)art["attach"]));}
        }
        foreach(var zone in match.UndeadZones)PlaceUndead(wanted,"zone-"+zone.id,zone.kind=="plague"?"fx_c71c2a84f3cc":"fx_0815f19865ac",zone.team,World(zone.point));
        foreach(var wave in match.UndeadWaves)PlaceUndead(wanted,"wave-"+wave.id,"fx_4f5795f2a2a4",wave.team,World(wave.origin+wave.direction*wave.distance)+Vector3.up,wave.direction);
        foreach(var pulse in match.Pulses){if(!ShowPulse036(pulse))continue;string tag=pulse.fx=="undead_raise"?"A01Y":pulse.fx=="undead_execute"?"undead_execute":pulse.fx=="undead_soul"?"undead_soul":null;if(tag==null)continue;int i=0;foreach(var art in undeadFxIndex["visuals"]?[tag]?["effect"] as JArray??new JArray()){string id="pulse-"+pulse.id+"-"+i++;PlaceUndead(wanted,id,(string)art["key"],0,World(pulse.position),elapsed:pulse.age);MarkPulse036(pulse.id,wanted,id);}}
        foreach(string id in undeadAttachments.Keys.Where(id=>!wanted.Contains(id)).ToArray()){if(undeadAttachments[id])Destroy(undeadAttachments[id].gameObject);undeadAttachments.Remove(id);}
    }
    void AuditUndeadParticles(){int count=0;foreach(var prefab in ContentResources.LoadAll<GameObject>("UndeadFx"))foreach(var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>()){var material=renderer.sharedMaterial;if(!material||!material.shader||!material.shader.isSupported||!material.mainTexture)throw new InvalidOperationException("Invalid Undead particle material: "+prefab.name);count++;}Debug.Log("CF_UNDEAD_PARTICLE_AUDIT_PASS materials="+count);}
    void ClearUndeadViews(){undeadAttachments.Clear();}
}
}
