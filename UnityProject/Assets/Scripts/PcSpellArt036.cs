using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    // Presentation-only state; keep the serialized scene and old player layouts compatible.
    sealed class SpellArtState036 {
        public static readonly ConditionalWeakTable<PcGame,SpellArtState036> ByGame=new ConditionalWeakTable<PcGame,SpellArtState036>();
        public AssetBundle bundle;
        public readonly Dictionary<string,GameObject> prefabs=new Dictionary<string,GameObject>();
        public readonly Dictionary<int,ClassicProjectileModel> casts=new Dictionary<int,ClassicProjectileModel>();
        public readonly Dictionary<int,int> hutTeams=new Dictionary<int,int>();
        public readonly Dictionary<int,int> unitKinds=new Dictionary<int,int>();
        public readonly HashSet<int> rendered=new HashSet<int>();
        public readonly List<int> expired=new List<int>();
    }
    SpellArtState036 Art036=>SpellArtState036.ByGame.GetValue(this,_=>new SpellArtState036());
    IEnumerator LoadSpellArt036(){
        if(MobileContent.Active){yield return MobileContent.Current.EnsurePrefix("SpellArt036","Классические эффекты заклинаний");yield break;}
        string path=MobileContent.Media("Frontend/spell-art-036.windows");if(!File.Exists(path))yield break;
        var request=AssetBundle.LoadFromFileAsync(path);yield return request;Art036.bundle=request.assetBundle;
    }
    bool ShowUnitArt036(HumanMatch.Unit unit,PcModel view)=>view&&view.gameObject.activeInHierarchy&&!match.Hidden(unit)&&CanSeeUnit(unit);
    static bool IsEnsnareArt036(HumanMatch.Effect effect)=>effect.left>0&&effect.root&&(effect.id=="A06H"||effect.id=="A0FC"||(string)effect.config?["base"]=="Aens");
    bool ShowPulse036(HumanMatch.Pulse pulse){
        if(!FogVisible(pulse.position))return false;
        if(pulse.visualUnitId<=0)return true;
        var unit=match.VisualUnit036(pulse.visualUnitId);
        return unit==null||!match.Hidden(unit)&&CanSeeUnit(unit);
    }
    bool ShowBeam036(HumanMatch.Shot shot){
        if(!FogVisible(shot.start)||!FogVisible(shot.end))return false;
        var source=match.VisualUnit036(shot.sourceId);var target=match.VisualUnit036(shot.target);
        return (source==null||!match.Hidden(source)&&CanSeeUnit(source))&&(target==null||!match.Hidden(target)&&CanSeeUnit(target));
    }
    void MarkPulse036(int id,HashSet<string> wanted,string key){if(wanted.Contains(key))Art036.rendered.Add(id);}
    static string ClassicCastKey036(HumanMatch.Pulse pulse){
        switch(PulseFx036(pulse)){
            case "blinkout":return "fx_abc46fd107a7";
            case "blinkin":return "fx_ef87a9d564e8";
            case "wisp":return "fx_04df853663a3";
            case "resurrect_caster":return "fx_403cdcbdb657";
            case "resurrect":return "fx_1a6e13ab3326";
            case "heal_direct":return "fx_a6d7f1b4d60a";
            default:return null;
        }
    }
    static string PulseFx036(HumanMatch.Pulse pulse)=>!string.IsNullOrEmpty(pulse.fx)?pulse.fx:pulse.text=="Скачок"?"blinkout":pulse.text=="Воскрешение"?"resurrect":null;
    GameObject SpellPrefab036(string key){
        var state=Art036;if(state.prefabs.TryGetValue(key,out var prefab))return prefab;
        prefab=MobileContent.Active?ContentResources.Load<GameObject>("SpellArt036/"+key):state.bundle?state.bundle.LoadAsset<GameObject>("Assets/SpellArt036/Prefabs/"+key+"-t0.prefab"):null;
        state.prefabs[key]=prefab;return prefab;
    }
    void RenderClassicSpells036(){
        var state=Art036;state.expired.Clear();
        if(!options.hdTextures)foreach(var pulse in match.Pulses){
            string key=ClassicCastKey036(pulse);
            if(key==null||!ShowPulse036(pulse))continue;
            if(!state.casts.TryGetValue(pulse.id,out var model)||!model){
                var prefab=SpellPrefab036(key);if(!prefab)continue;
                model=Instantiate(prefab,visuals.transform).GetComponent<ClassicProjectileModel>();
                model.PlayCast();state.casts[pulse.id]=model;
            }
            model.transform.position=World(pulse.position);
            if(pulse.visualUnitId>0&&units.TryGetValue(pulse.visualUnitId,out var actor)&&actor)
                model.transform.position=actor.transform.position;
            state.rendered.Add(pulse.id);
        }
        foreach(var pair in state.casts)if(!state.rendered.Contains(pair.Key)){if(pair.Value){pair.Value.gameObject.SetActive(false);Destroy(pair.Value.gameObject);}state.expired.Add(pair.Key);}
        foreach(int id in state.expired)state.casts.Remove(id);
    }
}
public sealed partial class HumanMatch {
    public Unit VisualUnit036(int id)=>FindUnitFast033(id);
}
}
