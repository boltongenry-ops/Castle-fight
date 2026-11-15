using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace CastleFight {
public sealed partial class PcGame {
    [Serializable] sealed class FxPoint {public float X,Y,Z;}
    [Serializable] sealed class FxInfo {public int Columns,Rows;public float Duration,HalfSize;public bool Loops,Static,Additive;public FxPoint Centre;}
    sealed class FxArt {public FxInfo info;public Texture2D texture;public Material material;}
    readonly Dictionary<string,FxArt> reforgedFx=new Dictionary<string,FxArt>();Mesh reforgedFxQuad;MaterialPropertyBlock fxBlock;bool reforgedFxLoaded;
    void LoadReforgedFx(){if(reforgedFxLoaded)return;reforgedFxLoaded=true;fxBlock=new MaterialPropertyBlock();var shader=Shader.Find("CastleFight/ReforgedEffect");if(!shader){Debug.LogError("CF_REFORGED_FX_SHADER_MISSING");return;}
        foreach(var data in ContentResources.LoadAll<TextAsset>("RefFx")){try{var info=JsonUtility.FromJson<FxInfo>(data.text);var texture=ContentResources.Load<Texture2D>("RefFx/"+data.name);if(info==null||!texture||info.Columns<1||info.Rows<1||info.Duration<=0)continue;var material=new Material(shader);material.mainTexture=texture;material.SetFloat("_DstBlend",info.Additive?(float)BlendMode.One:(float)BlendMode.OneMinusSrcAlpha);reforgedFx[data.name]=new FxArt{info=info,texture=texture,material=material};}catch(Exception ex){Debug.LogError("CF_REFORGED_FX_INVALID "+data.name+" "+ex.Message);}}
        reforgedFxQuad=new Mesh{name="Reforged effects billboard"};reforgedFxQuad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};reforgedFxQuad.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};reforgedFxQuad.triangles=new[]{0,1,2,0,2,3};reforgedFxQuad.RecalculateBounds();Debug.Log("CF_REFORGED_FX_READY count="+reforgedFx.Count);
    }
    void DrawRefFx(string name,Vector3 position,float time,float modelScale=1,float angle=0){if(!reforgedFx.TryGetValue(name,out var art)||!Camera.main)return;var viewport=Camera.main.WorldToViewportPoint(position);if(viewport.z<=0||viewport.x<-.3f||viewport.x>1.3f||viewport.y<-.3f||viewport.y>1.3f)return;var cfg=art.info;var centre=cfg.Centre;position+=new Vector3(centre.X,centre.Z,-centre.Y)*(modelScale/30f);float size=2*cfg.HalfSize*modelScale/30f;var matrix=Matrix4x4.TRS(position,Camera.main.transform.rotation*Quaternion.AngleAxis(angle,Vector3.forward),Vector3.one*size);
        if(cfg.Loops&&!cfg.Static){float a=Mathf.Repeat(time/cfg.Duration,1);float weight=.5f-.5f*Mathf.Cos(a*2*Mathf.PI);DrawFxFrame(art,matrix,a,weight);DrawFxFrame(art,matrix,Mathf.Repeat(a+.5f,1),1-weight);}
        else DrawFxFrame(art,matrix,cfg.Static?0:Mathf.Clamp01(time/cfg.Duration),1);
    }
    void DrawFxFrame(FxArt art,Matrix4x4 matrix,float progress,float alpha){if(alpha<=.001f)return;var cfg=art.info;int n=cfg.Columns*cfg.Rows;int frame=Mathf.Min(n-1,Mathf.FloorToInt(progress*n));int column=frame%cfg.Columns,row=frame/cfg.Columns;fxBlock.SetVector("_MainTex_ST",new Vector4(1f/cfg.Columns,1f/cfg.Rows,column/(float)cfg.Columns,1f-(row+1)/(float)cfg.Rows));fxBlock.SetColor("_Color",new Color(1,1,1,alpha));Graphics.DrawMesh(reforgedFxQuad,matrix,art.material,0,Camera.main,0,fxBlock,ShadowCastingMode.Off,false);}
    string NativeMissileFx(HumanMatch.Shot s){
        if(s.kind==5)return "net_missile";if(s.kind==6)return "steal_missile";if(s.kind==7)return "acid_missile";string path=(s.weapon?.missile??"").ToLowerInvariant();
        if(path.Contains("mortarmissile"))return "mortar_missile";if(path.Contains("demolishermissile"))return "catapult_missile";if(path.Contains("cannontowermissile"))return "cannon_missile";if(path.Contains("gryphonridermissile"))return "gryphon_missile";if(path.Contains("vengeancemissile"))return "vengeance_missile";if(path.Contains("bloodelfball"))return "bloodelf_missile";if(path.Contains("axemissile"))return "axe_missile";return null;
    }
    bool ReplaceMissile(HumanMatch.Shot s){var name=NativeMissileFx(s);return s.kind!=5&&name!="axe_missile"&&name!=null&&reforgedFx.ContainsKey(name);}
    readonly Dictionary<string,GameObject> missilePrefabs=new Dictionary<string,GameObject>();
    GameObject NativeMissilePrefab(HumanMatch.Shot s){if(s.weapon?.missile=="centaur:axe")return ContentResources.Load<GameObject>("CentaurFx/cAxe-"+s.team);var classic=ClassicWeaponMissile(s);if(classic)return classic;var final=FinalMissile(s);if(final)return final;var elven=ElvenMissile(s);if(elven)return elven;var night=NightMissile(s);if(night)return night;var elemental=ElementalMissile(s);if(elemental)return elemental;var corrupted=CorruptedMissile(s);if(corrupted)return corrupted;var undead=UndeadMissile(s);if(undead)return undead;var mech=MechMissile(s);if(mech)return mech;var northern=NorthernMissile(s);if(northern)return northern;if(!options.hdTextures)return null;string path=(s.weapon?.missile??"").ToLowerInvariant();string name=s.kind==5?"net_missile":path.Contains("axemissile")?"axe_missile":path.Contains("wyvernspearmissile")?"wyvern_missile":path.Contains("poisonarrowmissile")?"poison_arrow":path.Contains("illidanmissile")?"illidan_missile":path.Contains("guardtowermissile")?"guard_missile":path.Contains("mortarmissile")?"mortar_missile":path.Contains("demolishermissile")?"catapult_missile":path.Contains("cannontowermissile")?"cannon_missile":path.Contains("gryphonridermissile")?"gryphon_missile":null;if(name==null)return null;if(!missilePrefabs.TryGetValue(name,out var prefab)){prefab=ContentResources.Load<GameObject>("RefFx/proj_"+name);missilePrefabs[name]=prefab;}return prefab;}
    readonly Dictionary<int,Transform[]> effectAnchors=new Dictionary<int,Transform[]>();
    Vector3 EffectAnchor(HumanMatch.Unit u,PcModel view,int slot){if(!effectAnchors.TryGetValue(u.id,out var anchors)||anchors.All(a=>!a)){var all=view.GetComponentsInChildren<Transform>(true);string[] names={"Head Ref","Hand Left Ref","Hand Right Ref","Chest Ref","OverHead Ref"};anchors=names.Select(name=>all.FirstOrDefault(t=>string.Equals(t.name,name,StringComparison.OrdinalIgnoreCase))).ToArray();effectAnchors[u.id]=anchors;}return anchors[slot]?anchors[slot].position:(slot==4&&anchors[0]?anchors[0].position+Vector3.up*.5f:view.transform.position+Vector3.up*(slot==0||slot==4?2:1));}
    string PulseFx(HumanMatch.Pulse p)=>!string.IsNullOrEmpty(p.fx)?p.fx:(p.text??"").StartsWith("+")?"heal":p.text=="Воскрешение"?"resurrect":p.text=="Кислота"?"acid":p.text=="Похищение магии"?"spellsteal":p.text=="Скачок"?"blinkout":p.text=="Стазис"?"stomp":p.text=="Молитва"?"prayer":null;
    void RenderReforgedEffects(){if(!options.hdTextures||page!=Page.Match)return;LoadReforgedFx();
        foreach(var u in match.Units){if(u.hp<=0||!units.TryGetValue(u.id,out var view))continue;var pos=view.transform.position;float t=match.Elapsed+(u.id%19)*.037f;float scale=Mathf.Clamp(HumanMatch.F(match.Lines[u.kind].stats,"model_scale",1),.3f,3);
            if(u.auraIds.Contains("A034")||u.auraIds.Contains("A03J")||u.auraIds.Contains("A0HZ")||u.auraIds.Contains("Aakb"))DrawRefFx("general_aura",pos,t,scale);if(u.auraIds.Contains("A00F"))DrawRefFx("mana_aura",EffectAnchor(u,view,4),t,scale);
            if(u.effects.Any(e=>e.left>0&&(e.id=="A03I"||e.id=="A03M"||e.id=="A0I2")))DrawRefFx("innerfire",EffectAnchor(u,view,4),t,scale);
            if(u.effects.Any(e=>e.left>0&&e.id=="A02M"))DrawRefFx("bloodlust",EffectAnchor(u,view,0),t,scale);
            if(u.effects.Any(e=>e.left>0&&e.id=="A016"))DrawRefFx("devour",EffectAnchor(u,view,1),t,scale);
            if(u.effects.Any(e=>e.left>0&&(e.id=="A020"||e.id=="A038"||e.id=="A00C"||e.id=="A09W"||e.id=="A0AO"||e.id=="A0AP"||e.id=="A0AT"||e.id=="A0AV")))DrawRefFx("poison",pos,t,scale);
            if(u.effects.Any(e=>e.left>0&&(e.id=="A0AW"||e.id=="A0AX")))DrawRefFx("heal",pos,Mathf.Repeat(t,1),scale);
            if(u.effects.Any(e=>e.left>0&&e.id=="A030"))DrawRefFx("acid",EffectAnchor(u,view,3),t,scale);
            if(u.effects.Any(e=>e.left>0&&e.id=="A02I")){DrawRefFx("frenzy_left",EffectAnchor(u,view,1),t,scale);DrawRefFx("frenzy_right",EffectAnchor(u,view,2),t,scale);}
            if(u.effects.Any(e=>e.left>0&&e.id=="A02T"))DrawRefFx("naga_purge",pos,t,scale);
            if(u.effects.Any(e=>e.left>0&&e.speed<0&&e.id!="A020"&&e.id!="A038"&&e.id!="A030"&&e.id!="A02T"&&e.id!="A00C"&&e.id!="A09W"))DrawRefFx("slow",pos,t,scale);
            if(u.stun>0)DrawRefFx(u.stasisLeft>0?"incinerate":"stun",EffectAnchor(u,view,0),t,scale);if(u.abilities.Contains("A08P"))DrawRefFx("shield",pos,t,scale);
        }
        float waveAlpha=onlineMatch?1:page==Page.Match&&(!testing||!testHoldAuthority)?Mathf.Clamp01(accumulator/.05f):1;
        foreach(var wave in match.ActiveNagaWaves){var point=wave.origin+wave.direction*Mathf.Lerp(wave.previousDistance,wave.distance,waveAlpha);var a=Camera.main.WorldToScreenPoint(World(point));var b=Camera.main.WorldToScreenPoint(World(point+wave.direction*3));float angle=Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg+180;DrawRefFx("naga_wave",World(point)+Vector3.up, wave.age,1.35f,angle);}
        foreach(var shot in match.Shots){if(shot.kind==4){DrawRefFx("lightning_missile",PcShotView.Position(shot,1,World),shot.age);DrawRefFx("boltimpact",PcShotView.Position(shot,1,World),shot.age);continue;}if(shot.kind==1){DrawRefFx("rifle_impact",PcShotView.Position(shot,1,World),shot.age);continue;}if(shot.hit)continue;string fx=NativeMissileFx(shot);if(fx!=null)DrawRefFx(fx,PcShotView.Position(shot,Mathf.Clamp01(shot.age/shot.duration),World),shot.age);}
        foreach(var pulse in match.Pulses){string name=PulseFx(pulse);if(name==null)continue;var target=match.Units.FirstOrDefault(u=>u.id==pulse.visualUnitId&&u.hp>0);var point=target!=null&&units.TryGetValue(target.id,out var targetView)?EffectAnchor(target,targetView,4):World(pulse.position);DrawRefFx(name,point,pulse.age);}
    }
    void DisposeReforgedFx(){foreach(var art in reforgedFx.Values)if(art.material)Destroy(art.material);reforgedFx.Clear();if(reforgedFxQuad)Destroy(reforgedFxQuad);}
}
}
