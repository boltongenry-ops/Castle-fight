using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    PcRacePack elvenPack;
    JObject elvenFxIndex;JArray elvenArtAspects;
    readonly Dictionary<string,GameObject> elvenFxPrefabs=new Dictionary<string,GameObject>();
    readonly Dictionary<string,PcModel> elvenAttachments=new Dictionary<string,PcModel>();
    void LoadElvenArt(){if(elvenFxIndex!=null)return;var asset=ContentResources.Load<TextAsset>("elven-fx-index");elvenFxIndex=asset?JObject.Parse(asset.text):new JObject();elvenArtAspects=elvenPack?(JArray)JObject.Parse(elvenPack.catalog.text)["aspects"]:new JArray();}
    GameObject ElvenFx(string key,int team){if(string.IsNullOrEmpty(key))return null;string id=key+"-"+team;if(!elvenFxPrefabs.TryGetValue(id,out var prefab)){prefab=ContentResources.Load<GameObject>("ElvenFx/"+id);elvenFxPrefabs[id]=prefab;}return prefab;}
    void ResolveElvenDefaultColors(){LoadElvenArt();if(!(elvenFxIndex["defaultWhite"] is JObject defaults))return;foreach(var p in defaults.Properties())for(int team=0;team<2;team++){var prefab=ElvenFx(p.Name,team);if(!prefab)continue;foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(int i in (JArray)p.Value){var m=skin.sharedMaterials[i];if(m&&!m.mainTexture)m.mainTexture=Texture2D.whiteTexture;}}}
    GameObject ElvenMissile(HumanMatch.Shot shot){if(match==null)return null;LoadElvenArt();string key=null;if((shot.weapon?.missile??"").StartsWith("elven:"))key=(string)elvenFxIndex["visuals"]?[shot.weapon.missile.Substring(6)]?["missile"]?[0]?["key"];else {string raw=shot.sourceId>=0?match.Units.FirstOrDefault(u=>u.id==shot.sourceId) is HumanMatch.Unit u?match.Lines[u.kind].unit:null:null;key=(string)elvenFxIndex["weapons"]?[raw+"_1"];if(key==null&&shot.line>=0&&shot.line<match.Lines.Length)key=(string)elvenFxIndex["weapons"]?[match.Lines[shot.line].building+"_1"]??(string)elvenFxIndex["weapons"]?[match.Lines[shot.line].unit+"_1"];}return ElvenFx(key,shot.team);}
    void PlaceElven(HashSet<string> wanted,string id,string key,int team,Vector3 point,Vector2 direction=default){var prefab=ElvenFx(key,team);if(!prefab)return;wanted.Add(id);if(!elvenAttachments.TryGetValue(id,out var model)||!model||model.raw!=key){if(model){model.gameObject.SetActive(false);Destroy(model.gameObject);}model=Instantiate(prefab,visuals.transform).GetComponent<PcModel>();if(!model.particleOnly&&model.clips.Length>0)model.Play(model.clips.Any(c=>c.name.StartsWith("Stand"))?"Stand":model.clips[0].name);elvenAttachments[id]=model;}model.transform.position=point;if(direction.sqrMagnitude>0)model.Face(direction);}
    Vector3 ElvenAnchor(PcModel view,string attachment){LoadElvenArt();var tokens=(attachment??"origin").ToLowerInvariant().Split(new[]{',',' '},StringSplitOptions.RemoveEmptyEntries);var point=(elvenFxIndex["attachments"]?[view.raw]?["points"] as JArray??new JArray()).FirstOrDefault(p=>tokens.All(t=>((string)p["name"]).ToLowerInvariant().Contains(t)));if(point==null)return view.transform.position;var offset=(JArray)point["offset"];var v=new Vector3(-(float)offset[0],(float)offset[1],(float)offset[2]);string bone=(string)point["bone"];var node=view.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==bone);return node?node.TransformPoint(v):view.animatedRoot.transform.TransformPoint(v);}
    void ElvenBuff(HashSet<string> wanted,string prefix,PcModel view,int team,string ability){int i=0;foreach(var art in elvenFxIndex["visuals"]?[ability]?["buff"] as JArray??new JArray()){string id=prefix+"-"+ability+"-"+i++;PlaceElven(wanted,id,(string)art["key"],team,ElvenAnchor(view,(string)art["attach"]));}}

    void RenderElvenEffects(){LoadElvenArt();RenderElvenBeams();var wanted=new HashSet<string>();
        foreach(var dead in match.Units.Where(u=>u.hp<=0))if(units.TryGetValue(dead.id,out var deadView))ElvenHexGeometry(deadView,false);
        foreach(var u in match.Units)if(u.hp>0&&units.TryGetValue(u.id,out var view)&&ShowUnitArt036(u,view)){

            bool hexed=u.effects.Any(e=>e.left>0&&new[]{"AOhx","AOhex"}.Contains((string)e.config?["code"]));string hexId=match.Flying(u)?"hex_form_air":"hex_form";if(hexed)ElvenBuff(wanted,"hex-"+u.id,view,u.team,hexId);ElvenHexGeometry(view,hexed&&wanted.Contains("hex-"+u.id+"-"+hexId+"-0"));
            foreach(string ability in u.abilities.Where(id=>NativePersistentArt(u,id)))ElvenBuff(wanted,"innate-"+u.id,view,u.team,ability);
            foreach(string ability in u.effects.Where(e=>e.left>0).Select(e=>e.id).Concat(u.auraIds).Distinct())ElvenBuff(wanted,"status-"+u.id,view,u.team,ability);
            if(selectedUnit==u.id&&match.TeamElven[u.team]&&match.Aspects[u.team]>=0){var aspect=(JObject)elvenArtAspects[match.Aspects[u.team]];PlaceElven(wanted,"aspect-"+u.id,(string)aspect["visual"]?["buff"]?["key"],u.team,view.transform.position);}
        }
        foreach(var h in match.Huts)if(huts.TryGetValue(h.id,out var view)){
            if(new[]{"h08X","h08Y"}.Contains(match.Lines[h.kind].building))foreach(var renderer in view.GetComponentsInChildren<SkinnedMeshRenderer>())renderer.enabled=false;

            int i=0;foreach(var row in elvenFxIndex["decorations"]?[match.Lines[h.kind].building] as JArray??new JArray()){string raw=(string)row[0];foreach(var art in elvenFxIndex["visuals"]?[raw]?["effect"] as JArray??new JArray()){var point=view.transform.TransformPoint(new Vector3((float)row[2]/30,0,-(float)row[3]/30));string id="decor-"+h.id+"-"+i++;PlaceElven(wanted,id,(string)art["key"],h.team,point);if(elvenAttachments.TryGetValue(id,out var effect)){effect.transform.rotation=view.transform.rotation*Quaternion.Euler(0,90-(float)row[1],0);var profile=elvenFxIndex["decorProfiles"]?[raw];effect.transform.localScale=Vector3.one*(row.Count()>4?(float)row[4]:HumanMatch.F(profile,"scale",1));var tint=(JArray)profile?["tint"];if(tint!=null)foreach(var renderer in effect.GetComponentsInChildren<SkinnedMeshRenderer>())for(int m=0;m<renderer.sharedMaterials.Length;m++){var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,m);block.SetColor("_Tint",new Color((float)tint[0],(float)tint[1],(float)tint[2],(float)tint[3]));renderer.SetPropertyBlock(block,m);}}}}
        }
        foreach(var pulse in match.Pulses){if(!ShowPulse036(pulse))continue;if(!(pulse.fx??"").StartsWith("elven:"))continue;var split=pulse.fx.Split(':');if(split.Length!=3)continue;int i=0;JArray art=elvenFxIndex["visuals"]?[split[1]]?[split[2]] as JArray??new JArray();foreach(var row in art){string id="pulse-"+pulse.id+"-"+i++;PlaceElven(wanted,id,(string)row["key"],0,World(pulse.position));MarkPulse036(pulse.id,wanted,id);}}
        foreach(string id in elvenAttachments.Keys.Where(id=>!wanted.Contains(id)).ToArray()){if(elvenAttachments[id])Destroy(elvenAttachments[id].gameObject);elvenAttachments.Remove(id);}
    }
    readonly Dictionary<Renderer,bool> elvenHexRenderers=new Dictionary<Renderer,bool>();
    readonly Dictionary<int,MeshRenderer> elvenBeams=new Dictionary<int,MeshRenderer>();
    void ElvenHexGeometry(PcModel view,bool hexed){
        foreach(var renderer in view.GetComponentsInChildren<SkinnedMeshRenderer>()){
            if(hexed){if(!elvenHexRenderers.ContainsKey(renderer))elvenHexRenderers[renderer]=renderer.enabled;renderer.enabled=false;}
            else if(elvenHexRenderers.TryGetValue(renderer,out bool old)){renderer.enabled=old;elvenHexRenderers.Remove(renderer);}
        }
    }
    void RenderElvenBeams(){
        var wanted=new HashSet<int>();
        foreach(var shot in match.Shots.Where(s=>s.kind==4&&s.age<s.duration&&(s.weapon?.missile??"").StartsWith("elven_beam:"))){
            string code=shot.weapon.missile.Substring(11);var cfg=elvenFxIndex["lightning"]?[code];if(cfg==null)continue;
            if(!ShowBeam036(shot))continue;
            var saved=ContentResources.Load<Material>("ElvenFxMaterials/Lightning-"+code);if(!saved)continue;
            wanted.Add(shot.id);if(!elvenBeams.TryGetValue(shot.id,out var renderer)||!renderer){
                var host=new GameObject("Native healing beam "+code);host.transform.SetParent(visuals.transform,false);var filter=host.AddComponent<MeshFilter>();filter.sharedMesh=new Mesh();renderer=host.AddComponent<MeshRenderer>();
                var material=new Material(saved);renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;elvenBeams[shot.id]=renderer;
            }
            var start=PcShotView.Position(shot,0,World);var end=PcShotView.Position(shot,1,World);var direction=end-start;var toward=Camera.main.transform.position-(start+end)*.5f;var side=Vector3.Cross(direction,toward).normalized*HumanMatch.F(cfg,"width")*.5f;if(side.sqrMagnitude<.00001f)side=Vector3.right*HumanMatch.F(cfg,"width")*.5f;
            float repeat=Mathf.Max(1,direction.magnitude/HumanMatch.F(cfg,"segment_length")*HumanMatch.F(cfg,"texcoord_scale"));var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;mesh.Clear();mesh.vertices=new[]{start-side,start+side,end+side,end-side};mesh.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(repeat,1),new Vector2(repeat,0)};mesh.triangles=new[]{0,1,2,0,2,3,2,1,0,3,2,0};mesh.RecalculateBounds();renderer.sharedMaterial.mainTextureOffset=new Vector2(shot.age*2,0);
        }
        foreach(int id in elvenBeams.Keys.Where(id=>!wanted.Contains(id)).ToArray()){var renderer=elvenBeams[id];if(renderer){Destroy(renderer.sharedMaterial);Destroy(renderer.GetComponent<MeshFilter>().sharedMesh);Destroy(renderer.gameObject);}elvenBeams.Remove(id);}
    }
    void AuditElvenParticles(){int count=0;foreach(var prefab in ContentResources.LoadAll<GameObject>("ElvenFx"))foreach(var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>()){var material=renderer.sharedMaterial;if(!material||!material.shader||!material.shader.isSupported||!material.mainTexture)throw new InvalidOperationException("Invalid Elven particle material: "+prefab.name);count++;}Debug.Log("CF_ELVEN_PARTICLE_AUDIT_PASS materials="+count);}
    void ClearElvenViews(){elvenAttachments.Clear();elvenHexRenderers.Clear();foreach(var beam in elvenBeams.Values)if(beam){Destroy(beam.sharedMaterial);Destroy(beam.GetComponent<MeshFilter>().sharedMesh);}elvenBeams.Clear();}
}
}
