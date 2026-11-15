using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace CastleFight {
public sealed partial class PcGame {
    readonly Dictionary<string,PcModel> orcDecorModels=new Dictionary<string,PcModel>();
    readonly Dictionary<int,Transform[]> orcDecorAnchors=new Dictionary<int,Transform[]>();
    readonly Dictionary<string,GameObject> orcDecorPrefabs=new Dictionary<string,GameObject>();
    Material armorAuraMaterial,orcRuneMaterial;
    Transform BuildingEffectAnchor(HumanMatch.Hut h,PcModel view,int slot){
        if(!orcDecorAnchors.TryGetValue(h.id,out var anchors)||anchors[0]==null){
            var all=view.GetComponentsInChildren<Transform>(true);string[] names={"Origin Ref","Sprite second Ref","Sprite first Ref","Sprite third Ref"};
            anchors=names.Select(name=>all.FirstOrDefault(t=>string.Equals(t.name,name,StringComparison.OrdinalIgnoreCase))).ToArray();
            if(!anchors[0])anchors[0]=view.transform;orcDecorAnchors[h.id]=anchors;
        }
        return anchors[slot]?anchors[slot]:anchors[0];
    }
    GameObject OrcDecorPrefab(string key){if(!orcDecorPrefabs.TryGetValue(key,out var prefab)){prefab=ContentResources.Load<GameObject>("RefFx/"+key);orcDecorPrefabs[key]=prefab;}return prefab;}
    void ApplyOrcDecorMaterial(PcModel model,string name){
        if(name!="aura_armor"&&name!="orc_rune")return;
        foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()){
            Material mat=name=="aura_armor"?armorAuraMaterial:orcRuneMaterial;
            if(!mat){mat=new Material(skin.sharedMaterial);mat.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);mat.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);mat.SetInt("_ZWrite",0);mat.renderQueue=(int)RenderQueue.Transparent;if(name=="aura_armor")armorAuraMaterial=mat;else orcRuneMaterial=mat;}
            var materials=skin.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=mat;skin.sharedMaterials=materials;
        }
    }
    void OrcDecorModel(HashSet<string> live,HumanMatch.Hut h,PcModel actor,string name,int anchorSlot,float scale,float yaw=0){
        string key=h.id+"/"+name+"/"+anchorSlot;var prefab=OrcDecorPrefab(name);if(!prefab)return;live.Add(key);
        if(!orcDecorModels.TryGetValue(key,out var model)||!model){model=Instantiate(prefab,visuals.transform).GetComponent<PcModel>();orcDecorModels[key]=model;ApplyOrcDecorMaterial(model,name);}
        var anchor=BuildingEffectAnchor(h,actor,anchorSlot);model.transform.position=name=="orc_rune"?actor.transform.position:anchor.position;model.transform.rotation=(name=="orc_rune"?actor.transform.rotation:anchor.rotation)*Quaternion.Euler(0,yaw,0);
        model.transform.localScale=Vector3.one*(scale*actor.transform.localScale.x);model.Play("Stand");
    }
    void RenderOrcArmorAuras(){
        if(match==null)return;var live=new HashSet<string>();
        if(options.hdTextures){LoadReforgedFx();foreach(var h in match.Huts){
            if(h.hp<=0||!match.TeamOrc[h.team]||!huts.TryGetValue(h.id,out var actor))continue;
            string raw=match.Lines[h.kind].building;float t=match.Elapsed+(h.id%19)*.037f;float scale=Mathf.Clamp(HumanMatch.F(match.Lines[h.kind].buildingStats,"model_scale",1),.3f,3)*actor.transform.localScale.x;
            if(raw=="h035"||raw=="h092"){
                string fx=raw=="h035"?"orc_damage_aura":"magic_sentry";
                DrawRefFx(fx,BuildingEffectAnchor(h,actor,0).position,t,scale);DrawRefFx(fx,BuildingEffectAnchor(h,actor,1).position,t,scale);
            }
            if(raw=="h036"){OrcDecorModel(live,h,actor,"aura_armor",0,scale/Mathf.Max(.001f,actor.transform.localScale.x));OrcDecorModel(live,h,actor,"aura_armor",1,scale/Mathf.Max(.001f,actor.transform.localScale.x));}
            if(raw=="h06I"){
                OrcDecorModel(live,h,actor,"orc_rune",0,1.7f,180);
                var origin=BuildingEffectAnchor(h,actor,0).position;DrawRefFx("orc_rune_1",origin,t,1.7f);DrawRefFx("orc_rune_2",origin,t,1.7f);
            }
            if(raw=="h02N")OrcDecorModel(live,h,actor,"serpent_rock",0,scale/Mathf.Max(.001f,actor.transform.localScale.x));
        }}
        if(options.hdTextures)foreach(var h in match.Huts){
            if(h.hp<=0||!match.TeamNaga[h.team]||!huts.TryGetValue(h.id,out var actor))continue;
            string raw=match.Lines[h.kind].building;float t=match.Elapsed+(h.id%19)*.037f;float scale=Mathf.Clamp(HumanMatch.F(match.Lines[h.kind].buildingStats,"model_scale",1),.3f,3)*actor.transform.localScale.x;
            float modelScale=scale/Mathf.Max(.001f,actor.transform.localScale.x);
            if(raw=="h020"||raw=="h021"||raw=="h022")OrcDecorModel(live,h,actor,"naga_roots",0,modelScale);
            if(raw=="h022"){OrcDecorModel(live,h,actor,"naga_coral",0,modelScale);DrawRefFx("naga_decor_crystal",BuildingEffectAnchor(h,actor,2).position,t,scale);}
            if(raw=="h032")foreach(int slot in new[]{2,1,3})DrawRefFx("naga_decor_immolation",BuildingEffectAnchor(h,actor,slot).position,t,scale);
        }
        foreach(string key in orcDecorModels.Keys.Where(k=>!live.Contains(k)).ToArray()){if(orcDecorModels[key])Destroy(orcDecorModels[key].gameObject);orcDecorModels.Remove(key);}
        foreach(int id in orcDecorAnchors.Keys.Where(id=>!huts.TryGetValue(id,out var view)||!orcDecorAnchors[id][0]||!orcDecorAnchors[id][0].IsChildOf(view.transform)).ToArray())orcDecorAnchors.Remove(id);
    }
}
}
