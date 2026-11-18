using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    readonly Dictionary<int,LineRenderer> netViews=new Dictionary<int,LineRenderer>();
    readonly Dictionary<int,Vector2> netSizes=new Dictionary<int,Vector2>();
    readonly Dictionary<int,PcModel> reforgedNets=new Dictionary<int,PcModel>(),reforgedAirNets=new Dictionary<int,PcModel>();GameObject reforgedNetPrefab,reforgedAirNetPrefab;
    void RenderOrcNets() {
        var netted=match.Units.Where(u=>u.hp>0&&CanSeeUnit(u)&&!match.Hidden(u)&&u.effects.Any(IsEnsnareArt036)).ToArray();
        if(HdParticleAssets040&&!reforgedNetPrefab)reforgedNetPrefab=HdFxModel040("net");if(HdParticleAssets040&&!reforgedAirNetPrefab)reforgedAirNetPrefab=HdFxModel040("net_air");
        foreach(var u in netted) {
            if(HdParticleAssets040&&reforgedNetPrefab){
                if(netViews.TryGetValue(u.id,out var old)){Destroy(old.gameObject);netViews.Remove(u.id);netSizes.Remove(u.id);}
                if(!reforgedNets.TryGetValue(u.id,out var fx)){fx=Instantiate(reforgedNetPrefab,visuals.transform).GetComponent<PcModel>();reforgedNets[u.id]=fx;fx.transform.position=World(u.position);fx.Play("Stand");fx.SamplePose();var native=fx.BodyBounds();var target=units.TryGetValue(u.id,out var actor)?actor.BodyBounds():new Bounds(Vector3.zero,new Vector3(3,3,3));float factor=Mathf.Clamp(Mathf.Max(target.size.x,target.size.z)*1.4f/Mathf.Max(.1f,Mathf.Max(native.size.x,native.size.z)),.2f,5);fx.transform.localScale=Vector3.one*factor;}
                fx.transform.position=World(u.position);fx.Play("Stand");
                if(reforgedAirNetPrefab&&units.TryGetValue(u.id,out var targetView)){
                    if(!reforgedAirNets.TryGetValue(u.id,out var air)){air=Instantiate(reforgedAirNetPrefab,visuals.transform).GetComponent<PcModel>();reforgedAirNets[u.id]=air;air.transform.position=EffectAnchor(u,targetView,3);air.Play("Stand");air.SamplePose();var native=air.BodyBounds();var target=targetView.BodyBounds();float factor=Mathf.Clamp(Mathf.Max(target.size.x,target.size.z)/Mathf.Max(.1f,Mathf.Max(native.size.x,native.size.z)),.2f,5);air.transform.localScale=Vector3.one*factor;}
                    air.transform.position=EffectAnchor(u,targetView,3);air.Play("Stand");
                }continue;
            }
            if(reforgedAirNets.TryGetValue(u.id,out var oldAir)){Destroy(oldAir.gameObject);reforgedAirNets.Remove(u.id);}
            if(reforgedNets.TryGetValue(u.id,out var oldFx)){Destroy(oldFx.gameObject);reforgedNets.Remove(u.id);}
            if(!netViews.TryGetValue(u.id,out var line)){
                line=new GameObject("Ensnare cage").AddComponent<LineRenderer>();line.transform.SetParent(visuals.transform);line.sharedMaterial=impactMaterial;line.positionCount=97;line.startWidth=line.endWidth=.09f;netViews[u.id]=line;
                var size=new Vector2(2,2.5f);if(units.TryGetValue(u.id,out var view)){var bounds=view.BodyBounds();size=new Vector2(Mathf.Clamp(Mathf.Max(bounds.size.x,bounds.size.z)*.6f,2,6),Mathf.Clamp(bounds.size.y,2.5f,7));}netSizes[u.id]=size;
            }
            var center=World(u.position)+Vector3.up*.4f;
            var dimensions=netSizes[u.id];
            for(int i=0;i<97;i++){float angle=i*Mathf.PI*2/16;line.SetPosition(i,center+new Vector3(Mathf.Cos(angle)*dimensions.x,(i%16)/15f*dimensions.y,Mathf.Sin(angle)*dimensions.x));}
            var block=new MaterialPropertyBlock();block.SetColor("_Color",new Color(.3f,.85f,1,.8f));line.SetPropertyBlock(block);
        }
        foreach(var id in reforgedAirNets.Keys.Where(id=>!netted.Any(u=>u.id==id)).ToArray()){Destroy(reforgedAirNets[id].gameObject);reforgedAirNets.Remove(id);}
        foreach(var id in reforgedNets.Keys.Where(id=>!netted.Any(u=>u.id==id)).ToArray()){Destroy(reforgedNets[id].gameObject);reforgedNets.Remove(id);}
        foreach(var id in netViews.Keys.Where(id=>!netted.Any(u=>u.id==id)).ToArray()){Destroy(netViews[id].gameObject);netViews.Remove(id);netSizes.Remove(id);}
    }
}
}
