using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace CastleFight {
public sealed partial class PcGame {
    bool HdArt040=>HdModels.ArtEffects!=null;
    bool HdParticleAssets040=>HdArt040||options.hdTextures;
    GameObject HdFxModel040(string key)=>HdModels.Asset<GameObject>("fx-model/"+key)??ContentResources.Load<GameObject>("RefFx/"+key);
    Texture2D HdFxTexture040(string key)=>HdModels.Asset<Texture2D>("fx-texture/"+key)??ContentResources.Load<Texture2D>("RefFx/"+key);
    Shader HdFlameShader040()=>HdModels.Asset<Shader>("fx-040-flame-shader")??Shader.Find("CastleFight/ReforgedFlame");

    void MarkHdPulse040(string id){
        if(!id.StartsWith("pulse-",StringComparison.Ordinal))return;
        int separator=id.IndexOf('-',6);
        if(separator>6&&int.TryParse(id.Substring(6,separator-6),out int pulse))Art036.rendered.Add(pulse);
    }

    bool EnsureHdSheet040(string key){
        if(string.IsNullOrEmpty(key))return false;
        if(reforgedFx.ContainsKey(key))return true;
        if(!HdArt040||!HdModels.MissingSheets.Add(key))return false;
        var texture=HdModels.Asset<Texture2D>("fx-sheet/"+key);
        var layout=HdModels.Asset<TextAsset>("fx-layout/"+key);
        var shader=HdModels.Asset<Shader>("fx-040-shader");
        if(!texture||!layout||!shader||!shader.isSupported)return false;
        var info=JsonUtility.FromJson<FxInfo>(layout.text);
        if(info==null||info.Centre==null||info.Columns<1||info.Rows<1||info.Duration<=0||info.HalfSize<=0)return false;
        var material=new Material(shader){mainTexture=texture};
        material.SetFloat("_DstBlend",info.Additive?(float)BlendMode.One:(float)BlendMode.OneMinusSrcAlpha);
        reforgedFx.Add(key,new FxArt{texture=texture,info=info,material=material});
        EnsureReforgedFxQuad();
        return true;
    }

    bool DrawHdSheet040(string key,Vector3 point,float elapsed,float scale=1,float angle=0){
        if(!EnsureHdSheet040(key))return false;
        DrawRefFx(key,point,elapsed,scale,angle);
        return true;
    }

    bool DrawHdLayers040(JArray layers,Vector3 point,float elapsed,float scale=1,float angle=0,bool flight=false){
        if(layers==null)return false;
        bool drawn=false;
        foreach(var layer in layers){
            var emitter=(string)layer["emitter"]??"";
            if(flight&&(emitter.IndexOf("death",StringComparison.OrdinalIgnoreCase)>=0||emitter.IndexOf("birth",StringComparison.OrdinalIgnoreCase)>=0))continue;
            drawn|=DrawHdSheet040((string)layer["key"],point,elapsed,scale,angle);
        }
        return drawn;
    }

    bool TryPlaceHdArt040(string id,string key,int team,Vector3 point,Vector2 direction=default,float elapsed=-1){
        if(!HdArt040||string.IsNullOrEmpty(key)||id.StartsWith("decor-",StringComparison.Ordinal))return false;
        var location=new Vector2(point.x,-point.z);
        bool visible=FogVisible(location);
        var forward=new Vector3(direction.x,0,-direction.y);
        if(visible&&DrawHdLayers040(HdLayers040(key),point,elapsed<0?match.Elapsed:elapsed,angle:HdShotAngle040(point,forward),flight:forward.sqrMagnitude>.001f))return true;
        var prefab=HdGeometry040(key,team);
        if(!prefab)return false;
        if(!visible)return true;
        string instance=key+"/"+team+"/"+id;
        if(!HdModels.LiveEffects.TryGetValue(instance,out var model)||!model){
            model=Instantiate(prefab,visuals.transform).GetComponent<ClassicProjectileModel>();
            HdModels.LiveEffects[instance]=model;
            if(id.StartsWith("pulse-",StringComparison.Ordinal))model.PlayCast();
            else model.Play(false);
        }
        HdModels.CurrentEffects.Add(instance);
        model.transform.position=point;
        model.Face(forward);
        return true;
    }

    GameObject HdGeometry040(string key,int team){
        if(!HdArt040||string.IsNullOrEmpty(key))return null;
        string address="fx-geometry/"+key+"/"+team;
        var entries=HdModels.Geometry;
        if(!entries.TryGetValue(address,out var prefab))entries[address]=prefab=HdModels.Asset<GameObject>(address);
        return prefab;
    }

    void SweepHdEffects040(){
        var state=HdModels;
        if(state.LiveEffects.Count==0)return;
        state.ExpiredEffects.Clear();
        foreach(var pair in state.LiveEffects)
            if(!state.CurrentEffects.Contains(pair.Key)){
                if(pair.Value){pair.Value.gameObject.SetActive(false);Destroy(pair.Value.gameObject);}
                state.ExpiredEffects.Add(pair.Key);
            }
        foreach(string key in state.ExpiredEffects)state.LiveEffects.Remove(key);
    }

    JArray HdLayers040(string key)=>string.IsNullOrEmpty(key)?null:HdModels.ArtEffects?["modelEffects"]?[key] as JArray;

    void RenderHdActorArt040(){
        var catalog=HdModels.ArtEffects;
        if(catalog==null||Camera.main==null)return;
        for(int team=0;team<builders.Length;team++){
            var builder=builders[team];
            if(!builder||!builder.reforged||!builder.gameObject.activeInHierarchy||!FogVisible(match.Builders[team].position))continue;
            var pose=match.Builders[team].pose;
            bool working=!string.IsNullOrEmpty(pose)&&pose.IndexOf("Work",StringComparison.OrdinalIgnoreCase)>=0;
            DrawHdActor040(builder,working,match.Elapsed+team*.17f);
        }
        foreach(var unit in match.Units){
            if(unit.hp<=0||!units.TryGetValue(unit.id,out var view)||!view||!view.reforged||!ShowUnitArt036(unit,view))continue;
            DrawHdActor040(view,false,match.Elapsed+(unit.id%11)*.09f);
        }
    }

    void DrawHdActor040(PcModel view,bool working,float elapsed,Camera camera=null,int layer=0){
        var catalog=HdModels.ArtEffects;
        var special=catalog?["specialActors"]?[view.raw];
        var point=view.transform.position;
        var viewport=(camera?camera:Camera.main).WorldToViewportPoint(point);
        if(viewport.z<=0||viewport.x<-.3f||viewport.x>1.3f||viewport.y<-.3f||viewport.y>1.3f)return;
        if(special!=null&&view.raw!="h030"){
            string key=(string)special[working?"work":"idle"]??(string)special["idle"];
            if(EnsureHdSheet040(key)){
                var info=reforgedFx[key].info;
                bool wisp=view.raw=="h051"||view.raw=="h06B";
                float size=wisp?.65f:.9f;
                float scale=Mathf.Clamp(view.ClassicHeight*30f*size/(2f*info.HalfSize),.12f,3f);
                point+=Vector3.up*view.ClassicHeight*(wisp?.45f:.16f);
                DrawRefFx(key,point,elapsed,scale,targetCamera:camera,layer:layer,opacity:wisp?.6f:1);
                return;
            }
        }
        if(view.raw=="n02E"||!(catalog?["actors"]?[view.raw] is JArray layers))return;
        float factor=Mathf.Clamp(view.visualScale,.12f,3f);
        foreach(var emitter in layers){
            string key=(string)emitter["key"];
            if(!EnsureHdSheet040(key))continue;
            var origin=point;
            if(emitter["pivot"] is JArray p&&p.Count==3)
                origin+=view.transform.rotation*new Vector3((float)p[1],(float)p[2],(float)p[0])*(factor/30f);
            DrawRefFx(key,origin,elapsed,factor,targetCamera:camera,layer:layer);
        }
    }

    void FrameWisp040(PcModel model){
        if(!model.reforged&&model.TryGetComponent<ClassicWispBody040>(out var wisp)){
            Vector3 core=wisp.Center;
            float distance=Mathf.Max(2.8f,model.ClassicHeight*1.9f);
            portraitCamera.transform.position=core+new Vector3(0,distance*.08f,distance);
            portraitCamera.transform.LookAt(core);
            return;
        }
        float size=Mathf.Max(.5f,model.ClassicHeight);
        Vector3 point=model.transform.position+Vector3.up*(size*.45f);
        portraitCamera.transform.position=point+new Vector3(size*.12f,size*.08f,size*1.65f);
        portraitCamera.transform.LookAt(point);
    }

    void RenderHdPulses040(){
        var catalog=HdModels.ArtEffects;
        if(catalog==null)return;
        foreach(var pulse in match.Pulses){
            if(!ShowPulse036(pulse))continue;
            string name=PulseFx(pulse);
            if(name==null)continue;
            var point=World(pulse.position);
            if(units.TryGetValue(pulse.visualUnitId,out var target)&&target)point=target.transform.position;
            bool drawn=false;
            if(name.IndexOf(':')<0&&name!="wisp"){
                var key=ClassicCastKey036(pulse);
                drawn=TryPlaceHdArt040("pulse-"+pulse.id+"-classic",key,0,point,elapsed:pulse.age)
                    ||DrawHdSheet040(name=="heal_direct"?"heal":name,point,pulse.age);
            }
            if(drawn)Art036.rendered.Add(pulse.id);
        }
    }

    string HdMissileKey040(string path){
        if(string.IsNullOrEmpty(path))return null;
        if(path.StartsWith("native:",StringComparison.Ordinal))return path.Substring(7);
        var catalog=HdModels.ArtEffects;
        int separator=path.LastIndexOf(':');
        if(separator>=0&&path.Length-separator==5){
            var art=catalog?["spellVisuals"]?[path.Substring(separator+1)]?["missile"] as JArray;
            if(art!=null&&art.Count>0)return (string)art[0]["key"];
        }
        return (string)catalog?["missilePaths"]?[ProjectilePath(path)];
    }

    static float HdShotAngle040(Vector3 point,Vector3 direction){
        var camera=Camera.main;
        if(!camera||direction.sqrMagnitude<.001f)return 0;
        var from=camera.WorldToScreenPoint(point);
        var to=camera.WorldToScreenPoint(point+direction);
        return Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg;
    }

    bool DrawHdShot040(HumanMatch.Shot shot,PcShotView view){
        if(!HdArt040||view.VisualHit)return false;
        var point=view.transform.position;
        if(!FogVisible(new Vector2(point.x,-point.z)))return false;
        string key=ClassicWeaponKey(shot)??HdMissileKey040(shot.weapon?.missile);
        if(shot.kind==1||shot.kind==4){
            if(!ShowBeam036(shot))return false;
            point=PcShotView.Position(shot,1,World);
        }
        var direction=shot.end-shot.start;
        if(DrawHdLayers040(HdLayers040(key),point,shot.age,angle:HdShotAngle040(point,new Vector3(direction.x,0,-direction.y)),flight:shot.kind!=1&&shot.kind!=4))return true;
        var alias=NativeMissileFx(shot);
        if(shot.kind==1)alias="rifle_impact";
        if(shot.kind==4)alias="boltimpact";
        return DrawHdSheet040(alias,point,shot.age);
    }
}
}
