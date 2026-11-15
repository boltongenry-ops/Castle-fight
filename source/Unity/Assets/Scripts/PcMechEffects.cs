using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    JObject mechFxIndex;
    readonly Dictionary<string,GameObject> mechFxPrefabs=new Dictionary<string,GameObject>();
    readonly Dictionary<string,PcModel> mechAttachments=new Dictionary<string,PcModel>();
    // This original Godot GLB deliberately has no baseColorTexture on all eight
    // surfaces. glTF specifies opaque white as the material's default color.
    // Resolve only this audited untextured source; missing unit skins still fail.
    void ResolveMechDefaultColor() {
        for(int team=0;team<2;team++){var prefab=MechFx("fx_e740be2ceb81",team);if(!prefab)continue;
            foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(var material in skin.sharedMaterials)if(material&&!material.mainTexture)material.mainTexture=Texture2D.whiteTexture;
        }
    }
    void LoadMechArt(){if(mechFxIndex==null){var asset=ContentResources.Load<TextAsset>("mech-fx-index");mechFxIndex=asset?JObject.Parse(asset.text):new JObject();}}
    GameObject MechFx(string key,int team){if(string.IsNullOrEmpty(key))return null;string id=key+"-"+team;if(!mechFxPrefabs.TryGetValue(id,out var prefab)){prefab=ContentResources.Load<GameObject>("MechFx/"+id);mechFxPrefabs[id]=prefab;}return prefab;}
    GameObject MechMissile(HumanMatch.Shot shot) {
        LoadMechArt();string tag=shot.chaosSpell??"",key=tag=="mech_mine_payload"?(string)mechFxIndex["payloads"]?["h09M"]:tag=="mech_grenade_payload"?(string)mechFxIndex["payloads"]?["h09Q"]:tag=="mech_auto_fire"?"fx_e740be2ceb81":(string)mechFxIndex["weapons"]?[(shot.weapon?.missile??"").ToLowerInvariant()];return MechFx(key,shot.team);
    }
    void MechAttach(HashSet<string> wanted,string id,string key,int team,Transform parent,Vector3 position,float angle=0,float scale=1) {
        wanted.Add(id);if(!mechAttachments.TryGetValue(id,out var view)||!view){var prefab=MechFx(key,team);if(!prefab)return;view=Instantiate(prefab,parent).GetComponent<PcModel>();view.Play("Stand");mechAttachments[id]=view;}
        view.transform.localPosition=position;view.transform.localRotation=Quaternion.Euler(0,angle,0);view.transform.localScale=Vector3.one*scale;
    }
    void RenderMechEffects() {
        LoadMechArt();var wanted=new HashSet<string>();
        foreach(var h in match.Huts)if(h.hp>0&&huts.TryGetValue(h.id,out var view)&&mechFxIndex["decorations"]?[match.Lines[h.kind].building] is JArray parts) {
            int i=0;foreach(var part in parts){string key=(string)part["key"];MechAttach(wanted,"decor-"+h.id+"-"+match.Lines[h.kind].building+"-"+i++,key,h.team,view.transform,new Vector3((float)part["x"],0,-(float)part["z"]),90-(float)part["angle"],(float)part["scale"]);}
        }
        foreach(var u in match.Units)if(u.hp>0&&u.abilities.Contains("A09L")&&units.TryGetValue(u.id,out var body))MechAttach(wanted,"shield-"+u.id,"fx_a6559e2d21a6",u.team,body.transform,Vector3.zero);
        foreach(string id in mechAttachments.Keys.Where(id=>!wanted.Contains(id)).ToArray()){if(mechAttachments[id])Destroy(mechAttachments[id].gameObject);mechAttachments.Remove(id);}
    }
    void ClearMechViews(){mechAttachments.Clear();}
}
}
