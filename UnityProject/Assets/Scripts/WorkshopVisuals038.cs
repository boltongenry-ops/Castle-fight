using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace CastleFight {
internal sealed class WorkshopDye038 {
    readonly Dictionary<Renderer,Material[]> originals=new Dictionary<Renderer,Material[]>();
    readonly Dictionary<Material,Material> variants=new Dictionary<Material,Material>();
    PcModel model;
    string equipped;
    static readonly int Team=Shader.PropertyToID("_TeamColor"),Tint=Shader.PropertyToID("_Tint");

    internal void Apply(PcModel target,WorkshopCatalog038.Finish finish){
        string id=finish?.id;
        if(target==model&&equipped==id)return;
        Clear();
        model=target;equipped=id;
        if(!target||finish==null)return;
        Color cloth=finish.Tint,trim=finish.Highlight;
        foreach(var renderer in target.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
            var source=renderer.sharedMaterials;
            var dyed=(Material[])source.Clone();
            for(int i=0;i<source.Length;i++){
                var material=source[i];
                if(!material||!material.HasProperty(Team)||!material.HasProperty(Tint))continue;
                if(!variants.TryGetValue(material,out var variant)){
                    variant=new Material(material){name=material.name+" · "+finish.title};
                    variant.SetColor(Team,cloth);
                    Color old=material.GetColor(Tint);
                    Color glaze=Color.Lerp(Color.white,trim,.26f);
                    variant.SetColor(Tint,new Color(old.r*glaze.r,old.g*glaze.g,old.b*glaze.b,old.a));
                    variants.Add(material,variant);
                }
                dyed[i]=variant;
            }
            originals.Add(renderer,source);
            renderer.sharedMaterials=dyed;
        }
    }

    internal void Clear(){
        foreach(var pair in originals)if(pair.Key)pair.Key.sharedMaterials=pair.Value;
        foreach(var material in variants.Values)if(material)Object.Destroy(material);
        originals.Clear();variants.Clear();model=null;equipped=null;
    }
}

internal sealed class WorkshopFx038 {
    readonly Transform root;
    readonly int layer;
    readonly Material overlay;
    readonly bool preview;
    readonly Dictionary<int,Material> glows=new Dictionary<int,Material>();
    Material ringColor;
    Mesh ringMesh;
    Transform aura;
    ParticleSystem motes,bursts;
    string auraId;
    float nextStep,nextDemo;
    Vector3 lastFoot;
    bool footInitialized;

    internal WorkshopFx038(Transform parent,Material overlay,bool preview){
        this.overlay=overlay;this.preview=preview;layer=parent.gameObject.layer;
        var obj=new GameObject("Workshop cosmetic effects"){layer=layer};
        root=obj.transform;root.SetParent(parent,false);
    }

    internal bool HasAura=>aura&&aura.gameObject.activeSelf;
    internal int BurstCount=>bursts?bursts.particleCount:0;
    internal int ParticleCount=>(motes?motes.particleCount:0)+(bursts?bursts.particleCount:0);

    internal void Update(Vector3 foot,WorkshopCatalog038.Finish auraFinish,WorkshopCatalog038.Finish effect,bool walking){
        root.position=foot;
        DrawAura(auraFinish);
        if(!footInitialized){lastFoot=foot;footInitialized=true;}
        float now=Time.unscaledTime;
        bool moved=(lastFoot-foot).sqrMagnitude>.15f;
        if(effect!=null&&effect.pattern<3&&walking&&(preview||moved)&&now>=nextStep){
            Emit(foot+Vector3.up*.12f,effect,false);
            lastFoot=foot;nextStep=now+.16f;
        }
        if(preview&&effect!=null&&effect.pattern>=3&&now>=nextDemo){
            Emit(foot+Vector3.up*.15f,effect,true);
            nextDemo=now+1.65f;
        }
    }

    internal void BuildFlash(Vector3 point,WorkshopCatalog038.Finish effect){
        if(effect!=null&&effect.pattern>=3)Emit(point+Vector3.up*.18f,effect,true);
    }

    void DrawAura(WorkshopCatalog038.Finish finish){
        if(finish==null){if(aura)aura.gameObject.SetActive(false);return;}
        if(!aura||auraId!=finish.id)CreateAura(finish);
        aura.gameObject.SetActive(true);
        float time=Time.unscaledTime;
        aura.localRotation=Quaternion.Euler(0,time*(finish.pattern==6?49:25),0);
        aura.localScale=Vector3.one*(1+.055f*Mathf.Sin(time*2.2f));
    }

    void CreateAura(WorkshopCatalog038.Finish finish){
        if(aura)Object.Destroy(aura.gameObject);
        if(ringMesh)Object.Destroy(ringMesh);
        if(ringColor)Object.Destroy(ringColor);
        auraId=finish.id;
        var shape=new GameObject("Workshop moving sigil"){layer=layer};
        aura=shape.transform;aura.SetParent(root,false);aura.localPosition=Vector3.up*.16f;
        Color tint=finish.Tint;tint.a=.83f;
        ringColor=new Material(overlay){name="Workshop aura color",color=tint};
        ringMesh=MakeSigil(finish.pattern);
        shape.AddComponent<MeshFilter>().sharedMesh=ringMesh;
        var renderer=shape.AddComponent<MeshRenderer>();renderer.sharedMaterial=ringColor;renderer.shadowCastingMode=ShadowCastingMode.Off;
        motes=NewEmitter(aura,"Orbiting flecks",56,finish.pattern==2?1:finish.pattern==7?3:0);
        var main=motes.main;main.loop=true;main.startSpeed=finish.pattern==6?.55f:.27f;
        main.startLifetime=new ParticleSystem.MinMaxCurve(.48f,.95f);
        main.startSize=new ParticleSystem.MinMaxCurve(.11f,finish.pattern==2?.32f:.23f);
        main.startColor=new ParticleSystem.MinMaxGradient(finish.Tint,finish.Highlight);
        main.simulationSpace=preview?ParticleSystemSimulationSpace.Local:ParticleSystemSimulationSpace.World;
        var area=motes.shape;area.enabled=true;area.shapeType=ParticleSystemShapeType.Circle;area.radius=1.18f;
        area.radiusThickness=.15f;area.rotation=new Vector3(-90,0,0);
        var emission=motes.emission;emission.enabled=true;emission.rateOverTime=finish.pattern==4?28:19;
        motes.Play();
    }

    static Mesh MakeSigil(int pattern){
        const int segments=60;
        var vertices=new List<Vector3>();
        var indices=new List<int>();
        int rings=pattern==4?3:2;
        for(int band=0;band<rings;band++){
            float radius=1.35f-band*.28f;
            for(int i=0;i<segments;i++){
                if(band>0&&i%10>=7&&pattern!=6)continue;
                if(band==0&&pattern==3&&i%30>=17)continue;
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                float thickness=band==0?.072f:.045f;
                Vector3 Point(float angle,float radial){
                    if(pattern==2||pattern==7)radial+=.09f*Mathf.Cos(angle*(pattern==2?6:5));
                    return new Vector3(Mathf.Cos(angle)*radial,0,Mathf.Sin(angle)*radial);
                }
                int offset=vertices.Count;
                vertices.Add(Point(a,radius));vertices.Add(Point(b,radius));
                vertices.Add(Point(a,radius-thickness));vertices.Add(Point(b,radius-thickness));
                indices.Add(offset);indices.Add(offset+2);indices.Add(offset+1);
                indices.Add(offset+1);indices.Add(offset+2);indices.Add(offset+3);
            }
        }
        var mesh=new Mesh{name="Workshop aura sigil"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        return mesh;
    }

    ParticleSystem NewEmitter(Transform parent,string name,int limit,int pattern){
        var obj=new GameObject(name){layer=layer};obj.transform.SetParent(parent,false);
        var system=obj.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=system.main;main.useUnscaledTime=true;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=limit;
        var emission=system.emission;emission.enabled=false;
        var shape=system.shape;shape.enabled=false;
        var fade=system.colorOverLifetime;fade.enabled=true;
        var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(.8f,.35f),new GradientAlphaKey(0,1)});
        fade.color=gradient;
        var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Glow(pattern);
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        return system;
    }

    Material Glow(int pattern){
        if(glows.TryGetValue(pattern,out var existing))return existing;
        var mask=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Workshop particle mask",wrapMode=TextureWrapMode.Clamp};
        var colors=new Color32[32*32];
        for(int y=0;y<32;y++)for(int x=0;x<32;x++){
            float dx=(x-15.5f)/15.5f,dy=(y-15.5f)/15.5f;
            float alpha;
            switch(pattern){
                case 1:alpha=Mathf.Clamp01((.92f-Mathf.Abs(dx)*1.65f-Mathf.Abs(dy)) * 6);break;
                case 2:alpha=Mathf.Clamp01((.84f-Mathf.Sqrt(Mathf.Abs(dx*dy))*1.85f)*4)*Mathf.Clamp01(1-dx*dx-dy*dy);break;
                case 3:alpha=Mathf.Clamp01((.8f-dx*dx*1.95f-dy*dy-.6f*dx*dy)*5);break;
                default:alpha=Mathf.Pow(Mathf.Clamp01(1-dx*dx-dy*dy),2);break;
            }
            colors[y*32+x]=new Color32(255,255,255,(byte)(alpha*255));
        }
        mask.SetPixels32(colors);mask.Apply(false,true);
        var shader=Shader.Find("Legacy Shaders/Particles/Additive");
        var glow=shader?new Material(shader):new Material(overlay);
        glow.mainTexture=mask;
        if(glow.HasProperty("_TintColor"))glow.SetColor("_TintColor",Color.white);
        glows.Add(pattern,glow);
        return glow;
    }

    void Emit(Vector3 point,WorkshopCatalog038.Finish finish,bool building){
        int pattern=finish.pattern==1||finish.pattern==5?1:finish.pattern==2?2:finish.pattern==4?3:0;
        if(!bursts){bursts=NewEmitter(root,"Workshop sparks and footsteps",72,pattern);bursts.Play();}
        else bursts.GetComponent<ParticleSystemRenderer>().sharedMaterial=Glow(pattern);
        int count=building?finish.pattern==5?18:14:finish.pattern==0?5:7;
        float lift=finish.pattern==1?.35f:finish.pattern==4?.45f:1;
        for(int i=0;i<count;i++){
            float direction=(i*2.399963f)+Time.unscaledTime;
            var spread=new Vector3(Mathf.Cos(direction),0,Mathf.Sin(direction));
            float strength=.45f+(i%5)*.13f;
            var particle=new ParticleSystem.EmitParams{
                position=point+spread*(building?.34f:.17f),
                velocity=spread*strength*(building?2.25f:.68f)+Vector3.up*lift*(building?1.45f:.6f),
                startLifetime=(building?.53f:.39f)+(i%3)*.13f,
                startSize=(building?.20f:.12f)+(i%4)*.043f,
                startColor=i%3==0?finish.Highlight:finish.Tint
            };
            bursts.Emit(particle,1);
        }
    }

    internal void Dispose(){
        if(root){root.gameObject.SetActive(false);Object.Destroy(root.gameObject);}
        if(ringMesh)Object.Destroy(ringMesh);
        if(ringColor)Object.Destroy(ringColor);
        foreach(var material in glows.Values){
            if(material.mainTexture)Object.Destroy(material.mainTexture);
            Object.Destroy(material);
        }
        glows.Clear();
    }
}
}
