using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    static JObject HordeDefinition()=>new JObject{
        ["id"]=HumanMatch.HordeTrial,["title"]="Проклятая Орда",["opponent"]="Orcish Horde",
        ["summary"]="Сорвите ритуал эликсира и переживите натиск.",
        ["description"]="Замок Орды: 12 000 HP. С 3:00 шаманы выходят каждые 40 с. Каждый ритуал усиливает до 8 бойцов. Эликсир даёт 12 с ярости и 6 с истощения. При 6 000 HP выходит вождь с иммунитетом к магии. Пока он жив, интервал сокращается до 25 с.",
        ["start_gold"]=500,["opponent_start_gold"]=700,["opponent_castle_hp"]=HumanMatch.HordeCastleHealth,
        ["difficulty"]=2,["xp"]=0,["reward"]=0
    };
    static readonly Color HordeAccent=new Color(1,.51f,.29f),HordeTint=new Color(1.55f,.40f,.32f);
    const float HordeRedScale=1.12f;
    static readonly int HordeColor=Shader.PropertyToID("_Color"),HordeBaseColor=Shader.PropertyToID("_BaseColor");
    static readonly int HordeNativeTint=Shader.PropertyToID("_Tint");
    Texture2D hordePainting,hordeElixirIcon;
    bool hordeArtTried,hordeIntro,hordeSkip,hordeVersus;
    float hordeIntroAge;
    int hordeIntroShot;
    PcModel hordeIntroChief,hordeIntroShaman;
    readonly List<PcModel> hordeIntroGuards=new List<PcModel>(4);
    Transform hordeCup;
    GameObject hordeIntroProps;
    Material hordeElixirMaterial,hordeIronMaterial,hordeBronzeMaterial,hordeCoreMaterial;
    Material hordeLiquidMaterial,hordeSmokeMaterial;
    Texture2D hordeLiquidTexture,hordeSmokeTexture;
    Mesh hordePotMesh,hordeRimMesh;
    Vector3 hordePotCenter;
    Transform hordeLiquid;
    Light hordeOrbLight;
    readonly Transform[] hordeOrbSparks=new Transform[9];
    readonly Transform[] hordeFireSparks=new Transform[7];
    readonly LineRenderer[] hordeSmoke=new LineRenderer[3];
    LineRenderer hordeChargeLine;
    AudioSource hordeMusic,hordeSignal;
    AudioClip hordeDrums,hordeHorn;
    int lastHordeBeat;
    bool hordeIntroTurned;
    readonly Dictionary<PcModel,(Vector3 scale,List<(Renderer renderer,MaterialPropertyBlock original)> blocks)> hordeTints=
        new Dictionary<PcModel,(Vector3,List<(Renderer,MaterialPropertyBlock)>)>();
    readonly Dictionary<PcModel,GameObject> hordeFlasks=new Dictionary<PcModel,GameObject>();
    readonly Dictionary<Texture,Texture2D> hordeAtlases=new Dictionary<Texture,Texture2D>();
    readonly List<(MeshRenderer renderer,MaterialPropertyBlock original)> hordeGround=
        new List<(MeshRenderer,MaterialPropertyBlock)>();
    GameObject hordeScorches;
    GameObject hordeRitualWave;
    Texture2D hordeScorchTexture,hordeMinimap;
    Material hordeScorchMaterial;
    Mesh hordeScorchMesh;

    void DrawHordePainting(Rect rect){
        Fill(rect,new Color(.11f,.025f,.016f));
        if(!hordeArtTried){
            hordeArtTried=true;
            var asset=Resources.Load<TextAsset>("WinterBattle/CursedHorde");
            string file=MobileContent.Media("Frontend/CursedHorde.jpg");
            var bytes=asset?asset.bytes:File.Exists(file)?File.ReadAllBytes(file):null;
            if(bytes!=null){
                hordePainting=new Texture2D(2,2,TextureFormat.RGB24,false);
                if(!hordePainting.LoadImage(bytes,true)){Destroy(hordePainting);hordePainting=null;}
            }
        }
        if(!hordePainting)return;
        float h=Mathf.Min(1,rect.height*hordePainting.width/(rect.width*hordePainting.height));
        float w=Mathf.Min(1,rect.width*hordePainting.height/(rect.height*hordePainting.width));
        GUI.DrawTextureWithTexCoords(rect,hordePainting,new Rect((1-w)*.75f,(1-h)*.62f,w,h));
    }

    void DisposeHordeArtwork(){
        if(hordePainting)Destroy(hordePainting);
        if(hordeElixirIcon)Destroy(hordeElixirIcon);
        hordePainting=hordeElixirIcon=null;hordeArtTried=false;
    }

    IEnumerator PrepareHordeGround(int generation){
        var root=controls.sceneryRoot.transform.parent;
        var renderers=root.Cast<Transform>().Where(t=>t.name.StartsWith("Terrain",StringComparison.Ordinal))
            .SelectMany(t=>t.GetComponentsInChildren<MeshRenderer>())
            .Where(r=>r.name.StartsWith("Ground ",StringComparison.Ordinal)&&r.sharedMaterial?.mainTexture!=null);
        foreach(var renderer in renderers){
            if(generation!=versusSerial)yield break;
            var source=renderer.sharedMaterial.mainTexture;
            if(!hordeAtlases.TryGetValue(source,out var atlas)){
                atlas=HordeAshAtlas(source);hordeAtlases.Add(source,atlas);
                yield return null;
                if(generation!=versusSerial)yield break;
            }
            var original=new MaterialPropertyBlock();renderer.GetPropertyBlock(original);
            var overrideBlock=new MaterialPropertyBlock();renderer.GetPropertyBlock(overrideBlock);
            overrideBlock.SetTexture("_MainTex",atlas);
            renderer.SetPropertyBlock(overrideBlock);
            hordeGround.Add((renderer,original));
        }
        if(minimapTexture)hordeMinimap=HordeAshAtlas(minimapTexture);
        MakeHordeScorches();
    }

    static Texture2D HordeAshAtlas(Texture source){
        float scale=Mathf.Min(1,512f/Mathf.Max(source.width,source.height));
        int width=Mathf.Max(1,Mathf.RoundToInt(source.width*scale));
        int height=Mathf.Max(1,Mathf.RoundToInt(source.height*scale));
        var target=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous=RenderTexture.active;
        var atlas=new Texture2D(width,height,TextureFormat.RGBA32,true){
            name="Выжженная земля · "+source.name,wrapMode=source.wrapMode,filterMode=FilterMode.Bilinear
        };
        try{
            Graphics.Blit(source,target);RenderTexture.active=target;
            atlas.ReadPixels(new Rect(0,0,width,height),0,0,false);
        }finally{
            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);
        }
        var pixels=atlas.GetPixels32();
        for(int i=0;i<pixels.Length;i++){
            var p=pixels[i];float light=(p.r*.24f+p.g*.68f+p.b*.08f)/255f;
            float grass=Mathf.Clamp01((p.g-Mathf.Max(p.r,p.b)+4)/38f);
            var basalt=new Color(.09f+.66f*light,.085f+.59f*light,.095f+.57f*light,p.a/255f);
            var cinder=new Color(.13f+.72f*light,.055f+.32f*light,.025f+.24f*light,p.a/255f);
            pixels[i]=Color.Lerp(basalt,cinder,grass);
        }
        atlas.SetPixels32(pixels);atlas.Apply(true,true);return atlas;
    }

    void MakeHordeScorches(){
        var shader=Shader.Find("Sprites/Default");
        if(!shader)return;
        const int mapSize=96;
        hordeScorchTexture=new Texture2D(mapSize,mapSize,TextureFormat.RGBA32,true){wrapMode=TextureWrapMode.Clamp};
        var colors=new Color32[mapSize*mapSize];
        var seams=new Vector2[8];
        for(int k=0;k<seams.Length;k++){
            float a=k*2.399f;
            seams[k]=new Vector2(Mathf.Sin(a),Mathf.Cos(a))*(.43f+.045f*k);
        }
        for(int y=0;y<mapSize;y++)for(int x=0;x<mapSize;x++){
            var pos=new Vector2(x/(mapSize-1f)*2-1,y/(mapSize-1f)*2-1);
            float fade=Mathf.Clamp01(1-pos.sqrMagnitude);
            float close=1;
            foreach(var tip in seams){
                float along=Mathf.Clamp01(Vector2.Dot(pos,tip)/tip.sqrMagnitude);
                var bend=new Vector2(tip.y,-tip.x)*(.12f*Mathf.Sin(along*19+tip.x*9)*along);
                close=Mathf.Min(close,(pos-tip*along-bend).magnitude);
            }
            float crack=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.015f,.038f,close));
            float flecks=Mathf.Clamp01((Mathf.PerlinNoise(x*1.24f,y*1.09f)-.72f)*5);
            Color tint=Color.Lerp(new Color(.035f,.018f,.016f),new Color(1,.40f,.095f),Mathf.Max(crack,flecks*.4f));
            tint.a=fade*fade*(.48f+.47f*crack);
            colors[x+y*mapSize]=tint;
        }
        hordeScorchTexture.SetPixels32(colors);hordeScorchTexture.Apply(true,true);
        hordeScorchMaterial=new Material(shader){mainTexture=hordeScorchTexture};
        hordeScorches=new GameObject("Угли на поле Орды");hordeScorches.transform.SetParent(visuals.transform,false);
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        var placements=new[]{
            new Vector3(144,10,7.5f),new Vector3(135,-7,6.5f),new Vector3(122,14,5),
            new Vector3(114,-15,8),new Vector3(93,17,7.5f),new Vector3(73,-13,6),
            new Vector3(55,16,8),new Vector3(33,-12,6),new Vector3(10,13,7.5f),
            new Vector3(-19,-15,6),new Vector3(-43,16,8),new Vector3(-67,-16,7),
            new Vector3(-97,17,6),new Vector3(-134,-13,7)
        };
        foreach(var placement in placements){
            int start=vertices.Count;
            for(int y=0;y<=4;y++)for(int x=0;x<=4;x++){
                var tex=new Vector2(x*.25f,y*.25f);
                var pos=new Vector2(placement.x,placement.y)+(tex-Vector2.one*.5f)*placement.z*2;
                vertices.Add(World(pos)+Vector3.up*.09f);uv.Add(tex);
                if(x==4||y==4)continue;
                int a=start+y*5+x;
                triangles.Add(a);triangles.Add(a+1);triangles.Add(a+5);
                triangles.Add(a+1);triangles.Add(a+6);triangles.Add(a+5);
            }
        }
        hordeScorchMesh=new Mesh{name="Угли · один слой"};
        hordeScorchMesh.SetVertices(vertices);hordeScorchMesh.SetUVs(0,uv);
        hordeScorchMesh.SetColors(Enumerable.Repeat(new Color32(255,255,255,255),vertices.Count).ToArray());
        hordeScorchMesh.SetTriangles(triangles,0);hordeScorchMesh.RecalculateBounds();
        hordeScorches.AddComponent<MeshFilter>().sharedMesh=hordeScorchMesh;
        var renderer=hordeScorches.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=hordeScorchMaterial;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
    }

    void RestoreHordeGround(){
        foreach(var layer in hordeGround)if(layer.renderer)
            layer.renderer.SetPropertyBlock(layer.original.isEmpty?null:layer.original);
        foreach(var atlas in hordeAtlases.Values)if(atlas)Destroy(atlas);
        hordeGround.Clear();hordeAtlases.Clear();
        if(hordeScorches)Destroy(hordeScorches);
        if(hordeScorchMesh)Destroy(hordeScorchMesh);
        if(hordeScorchMaterial)Destroy(hordeScorchMaterial);
        if(hordeScorchTexture)Destroy(hordeScorchTexture);
        if(hordeMinimap)Destroy(hordeMinimap);
        hordeScorches=null;hordeScorchMesh=null;hordeScorchMaterial=null;hordeScorchTexture=hordeMinimap=null;
    }

    void TintHordeActor(PcModel view,bool red){
        if(!red){
            if(!hordeTints.TryGetValue(view,out var saved))return;
            view.transform.localScale=saved.scale;
            if(!view.SetEffectTint(null))foreach(var item in saved.blocks)if(item.renderer)item.renderer.SetPropertyBlock(item.original);
            hordeTints.Remove(view);
            return;
        }
        if(hordeTints.ContainsKey(view))return;
        var scale=view.transform.localScale;
        view.transform.localScale=scale*HordeRedScale;
        var blocks=new List<(Renderer,MaterialPropertyBlock)>();
        if(view.SetEffectTint(HordeTint)){hordeTints[view]=(scale,blocks);return;}
        foreach(var renderer in view.GetComponentsInChildren<Renderer>(true)){
            var material=renderer.sharedMaterial;
            if(!material)continue;
            var original=new MaterialPropertyBlock();renderer.GetPropertyBlock(original);
            var tint=new MaterialPropertyBlock();renderer.GetPropertyBlock(tint);
            if(material.HasProperty(HordeColor))tint.SetColor(HordeColor,HordeTint);
            if(material.HasProperty(HordeBaseColor))tint.SetColor(HordeBaseColor,HordeTint);
            if(material.HasProperty(HordeNativeTint))tint.SetColor(HordeNativeTint,HordeTint);
            renderer.SetPropertyBlock(tint);blocks.Add((renderer,original));
        }
        hordeTints[view]=(scale,blocks);
    }

    Transform HordeProp(PrimitiveType shape,Vector3 position,Vector3 scale,Material material){
        var prop=GameObject.CreatePrimitive(shape);
        prop.transform.SetParent(hordeIntroProps.transform);
        prop.transform.position=position;prop.transform.localScale=scale;
        prop.GetComponent<Renderer>().sharedMaterial=material;
        Destroy(prop.GetComponent<Collider>());
        return prop.transform;
    }

    static Mesh RevolveHordeProfile(Vector2[] profile){
        const int segments=48;
        var vertices=new Vector3[profile.Length*(segments+1)];
        var triangles=new int[(profile.Length-1)*segments*6];
        for(int layer=0;layer<profile.Length;layer++)
            for(int i=0;i<=segments;i++){
                float angle=2*Mathf.PI*i/segments;
                vertices[layer*(segments+1)+i]=new Vector3(profile[layer].x*Mathf.Cos(angle),profile[layer].y,
                    profile[layer].x*Mathf.Sin(angle));
            }
        int index=0;
        for(int layer=0;layer<profile.Length-1;layer++)
            for(int i=0;i<segments;i++){
                int a=layer*(segments+1)+i,b=a+segments+1;
                triangles[index++]=a;triangles[index++]=b;triangles[index++]=a+1;
                triangles[index++]=a+1;triangles[index++]=b;triangles[index++]=b+1;
            }
        var mesh=new Mesh{name="Литой котёл",vertices=vertices,triangles=triangles};
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        return mesh;
    }

    void AddHordePotSurface(string name,Mesh mesh,Material material,float height=0){
        var surface=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
        surface.transform.SetParent(hordeIntroProps.transform,false);
        surface.transform.position=hordePotCenter+Vector3.up*height;
        surface.GetComponent<MeshFilter>().sharedMesh=mesh;
        surface.GetComponent<MeshRenderer>().sharedMaterial=material;
    }

    void MakeHordeVessel(Vector3 center){
        hordePotCenter=center;
        hordePotMesh=RevolveHordeProfile(new[]{
            new Vector2(.55f,.18f),new Vector2(1.28f,.31f),new Vector2(1.79f,.54f),
            new Vector2(2.17f,1.05f),new Vector2(2.29f,1.44f),new Vector2(2.22f,1.83f),
            new Vector2(2.08f,2.02f),new Vector2(2.04f,2.11f),new Vector2(1.90f,2.11f),
            new Vector2(1.91f,1.85f),new Vector2(1.71f,1.40f)
        });
        hordeRimMesh=RevolveHordeProfile(new[]{
            new Vector2(2.21f,1.84f),new Vector2(2.30f,1.86f),new Vector2(2.32f,1.96f),
            new Vector2(2.22f,2.08f),new Vector2(2.05f,2.09f)
        });
        AddHordePotSurface("Чугун",hordePotMesh,hordeIronMaterial);
        AddHordePotSurface("Бронзовый обод",hordeRimMesh,hordeBronzeMaterial);
        AddHordePotSurface("Кованый пояс",hordeRimMesh,hordeBronzeMaterial,-.75f);
        hordeLiquid=HordeProp(PrimitiveType.Cylinder,center+Vector3.up*1.92f,new Vector3(3.92f,.027f,3.92f),hordeLiquidMaterial);
        for(int i=0;i<3;i++){
            float yaw=i*120+30;
            var leg=Quaternion.Euler(0,yaw,0)*Vector3.forward*1.45f;
            var support=HordeProp(PrimitiveType.Cube,center+leg+Vector3.up*.27f,new Vector3(.37f,.83f,.41f),hordeIronMaterial);
            support.rotation=Quaternion.Euler(17,yaw,0);
        }
        for(int i=0;i<10;i++){
            var radial=Quaternion.Euler(0,i*36,0)*Vector3.forward;
            var rivet=HordeProp(PrimitiveType.Cube,center+radial*2.28f+Vector3.up*1.18f,
                new Vector3(.18f,.23f,.15f),hordeBronzeMaterial);
            rivet.rotation=Quaternion.LookRotation(radial)*Quaternion.Euler(0,0,45);
        }
        for(int i=-1;i<=1;i+=2){
            var handle=new GameObject("Кольцо котла").AddComponent<LineRenderer>();
            handle.transform.SetParent(hordeIntroProps.transform,false);
            handle.sharedMaterial=hordeBronzeMaterial;handle.loop=true;handle.positionCount=24;
            handle.startWidth=handle.endWidth=.18f;handle.numCornerVertices=3;
            for(int point=0;point<handle.positionCount;point++){
                float angle=point*2*Mathf.PI/handle.positionCount;
                handle.SetPosition(point,center+new Vector3(i*(2.24f+.38f*Mathf.Cos(angle)),1.35f+.51f*Mathf.Sin(angle),0));
            }
        }
        for(int i=0;i<hordeFireSparks.Length;i++)
            hordeFireSparks[i]=HordeProp(PrimitiveType.Sphere,center,Vector3.one*.08f,i%2==0?hordeCoreMaterial:hordeElixirMaterial);
        for(int i=0;i<hordeSmoke.Length;i++){
            var ribbon=new GameObject("Дым эликсира").AddComponent<LineRenderer>();
            ribbon.transform.SetParent(hordeIntroProps.transform,false);
            ribbon.sharedMaterial=hordeSmokeMaterial;ribbon.positionCount=12;
            ribbon.startWidth=.12f;ribbon.endWidth=.39f;
            ribbon.startColor=new Color(.95f,.30f,.12f,.30f);ribbon.endColor=new Color(.38f,.11f,.07f,0);
            hordeSmoke[i]=ribbon;
        }
    }

    void MakeHordeOrb(Vector3 center){
        hordeCup=new GameObject("Сгусток эликсира").transform;
        hordeCup.SetParent(hordeIntroProps.transform,false);hordeCup.position=center+Vector3.up*2.2f;
        var orb=HordeProp(PrimitiveType.Sphere,hordeCup.position,Vector3.one*.92f,hordeElixirMaterial);
        orb.SetParent(hordeCup,true);
        var jewel=HordeProp(PrimitiveType.Cube,hordeCup.position+Vector3.up*.40f,Vector3.one*.25f,hordeCoreMaterial);
        jewel.SetParent(hordeCup,true);jewel.localRotation=Quaternion.Euler(45,35,45);
        var orbit=new GameObject("Нить эликсира").AddComponent<LineRenderer>();
        orbit.transform.SetParent(hordeCup,false);orbit.sharedMaterial=hordeCoreMaterial;
        orbit.useWorldSpace=false;orbit.loop=true;orbit.positionCount=36;
        orbit.startWidth=orbit.endWidth=.035f;
        for(int i=0;i<orbit.positionCount;i++){
            float angle=2*Mathf.PI*i/orbit.positionCount;
            orbit.SetPosition(i,Quaternion.Euler(32,0,26)*new Vector3(.61f*Mathf.Cos(angle),0,.61f*Mathf.Sin(angle)));
        }
        hordeOrbLight=hordeCup.gameObject.AddComponent<Light>();
        hordeOrbLight.type=LightType.Point;hordeOrbLight.color=new Color(1,.18f,.05f);
        hordeOrbLight.range=8;hordeOrbLight.shadows=LightShadows.None;
        for(int i=0;i<hordeOrbSparks.Length;i++)
            hordeOrbSparks[i]=HordeProp(PrimitiveType.Sphere,hordeCup.position,Vector3.one*.15f,i%3==0?hordeCoreMaterial:hordeElixirMaterial);
    }

    void AnimateHordeVessel(float t,Vector3 hand,Vector3 mouth){
        if(hordeLiquid){
            hordeLiquid.position=hordePotCenter+Vector3.up*(1.94f+.018f*Mathf.Sin(t*4));
            hordeLiquid.Rotate(Vector3.up,Time.unscaledDeltaTime*13,Space.World);
        }
        for(int i=0;i<hordeSmoke.Length;i++){
            var ribbon=hordeSmoke[i];
            for(int point=0;point<ribbon.positionCount;point++){
                float rise=point/(ribbon.positionCount-1f),angle=i*2.1f+t*1.6f+rise*4.4f;
                ribbon.SetPosition(point,hordePotCenter+new Vector3(Mathf.Cos(angle)*(.32f+rise*.7f),
                    2.02f+3.1f*rise,Mathf.Sin(angle)*(.32f+rise*.46f)));
            }
        }
        for(int i=0;i<hordeFireSparks.Length;i++){
            float age=Mathf.Repeat(t*.57f+i*.142f,1),angle=i*2.39f+t*.7f;
            hordeFireSparks[i].position=hordePotCenter+
                new Vector3(Mathf.Cos(angle)*(1.45f-age*.6f),2.08f+age*2.4f,Mathf.Sin(angle)*(1.45f-age*.6f));
            hordeFireSparks[i].localScale=Vector3.one*(Mathf.Sin(age*Mathf.PI)*.13f+.02f);
        }
        float lift=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.9f,2.55f,t));
        float travel=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.55f,4.04f,t));
        Vector3 start=Vector3.Lerp(hordePotCenter+Vector3.up*2.15f,hand,lift);
        hordeCup.position=Vector3.Lerp(start,mouth,travel)+Vector3.up*(Mathf.Sin(travel*Mathf.PI)*1.25f);
        hordeCup.localScale=Vector3.one*Mathf.Lerp(.48f,1+.07f*Mathf.Sin(t*15),lift);
        hordeCup.Rotate(Vector3.up,Time.unscaledDeltaTime*120,Space.World);
        hordeOrbLight.intensity=.65f+.9f*lift;
        for(int i=0;i<hordeOrbSparks.Length;i++){
            float lag=Mathf.Clamp01(travel-i*.052f),angle=t*10+i*2.4f;
            Vector3 point=Vector3.Lerp(start,mouth,lag)+Vector3.up*(Mathf.Sin(lag*Mathf.PI)*1.25f);
            var spark=hordeOrbSparks[i];
            spark.position=point+new Vector3(Mathf.Cos(angle),.8f*Mathf.Sin(angle),Mathf.Sin(angle))*(.67f+i*.023f);
            spark.localScale=Vector3.one*(.18f-i*.012f);
            spark.gameObject.SetActive(t>=2.25f&&t<4.1f);
        }
    }

    void MakeHordeLiquidMaterials(){
        const int size=96;
        hordeLiquidTexture=new Texture2D(size,size,TextureFormat.RGB24,false){wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            float u=x/(size-1f)*2-1,v=y/(size-1f)*2-1;
            float radius=Mathf.Sqrt(u*u+v*v),angle=Mathf.Atan2(v,u);
            float swirl=.5f+.5f*Mathf.Sin(radius*23-angle*3+Mathf.PerlinNoise(u*4+8,v*4+6)*4);
            float hot=Mathf.Pow(swirl,6)*(.63f+.37f*Mathf.Cos(radius*4));
            pixels[x+y*size]=Color.Lerp(new Color(.42f,.017f,.009f),new Color(1,.77f,.14f),Mathf.Clamp01(.18f*swirl+hot));
        }
        hordeLiquidTexture.SetPixels(pixels);hordeLiquidTexture.Apply(false,true);
        hordeLiquidMaterial=new Material(shellMaterial){color=Color.white,mainTexture=hordeLiquidTexture};
        hordeLiquidMaterial.EnableKeyword("_EMISSION");
        hordeLiquidMaterial.SetTexture("_EmissionMap",hordeLiquidTexture);
        hordeLiquidMaterial.SetColor("_EmissionColor",new Color(.42f,.24f,.12f));
        var spriteShader=Shader.Find("Sprites/Default");
        hordeSmokeMaterial=spriteShader?new Material(spriteShader):new Material(impactMaterial);
        hordeSmokeMaterial.color=spriteShader?Color.white:new Color(.38f,.11f,.07f,.23f);
        hordeSmokeTexture=new Texture2D(8,32,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
        for(int y=0;y<32;y++)for(int x=0;x<8;x++)
            hordeSmokeTexture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Sin(y*Mathf.PI/31),2)));
        hordeSmokeTexture.Apply(false,true);hordeSmokeMaterial.mainTexture=hordeSmokeTexture;
    }

    void BeginHordeIntro(){
        hordeIntro=true;hordeIntroAge=0;hordeSkip=hordeIntroTurned=false;lastHordeBeat=0;
        hordeIntroShot=-1;
        var keep=HumanMatch.CastlePosition(1);
        hordeIntroChief=RitualActor("o005",keep+new Vector2(-22,10),new Vector2(-1,1));
        hordeIntroShaman=RitualActor("o003",keep+new Vector2(-25,13),new Vector2(1,-1));
        for(int i=0;i<4;i++){
            var guard=RitualActor("o002",keep+new Vector2(-17+i%2*4,9-i/2*4),Vector2.left);
            if(guard)hordeIntroGuards.Add(guard);
        }
        hordeIntroProps=new GameObject("Котёл эликсира");hordeIntroProps.transform.SetParent(visuals.transform,false);
        hordeElixirMaterial=new Material(impactMaterial);
        hordeElixirMaterial.color=new Color(1,.23f,.045f);
        hordeCoreMaterial=new Material(impactMaterial){color=new Color(1,.78f,.25f)};
        hordeIronMaterial=new Material(shellMaterial){color=new Color(.16f,.11f,.095f)};
        hordeBronzeMaterial=new Material(shellMaterial){color=new Color(.61f,.30f,.14f)};
        var center=World(keep+new Vector2(-27,10));
        MakeHordeLiquidMaterials();
        MakeHordeVessel(center);
        MakeHordeOrb(center);
        hordeMusic=visuals.AddComponent<AudioSource>();hordeMusic.playOnAwake=false;
        hordeSignal=visuals.AddComponent<AudioSource>();hordeSignal.playOnAwake=false;
        hordeHorn=MakeHordePercussion(false);hordeDrums=MakeHordePercussion(true);
        controls.InputBlocked=true;
        FrameWinterCamera(World(keep)+Vector3.up*5,58,-76,30);
    }

    static AudioClip MakeHordePercussion(bool loop){
        const int sampleRate=22050;
        int size=loop?sampleRate*4:sampleRate;
        var data=new float[size];
        uint noise=12839;
        float[] beats=loop?new[]{0f,.46f,1f,1.5f,2f,2.45f,2.72f,3.25f}:new[]{0f,.23f};
        for(int i=0;i<data.Length;i++){
            float time=i/(float)sampleRate;
            noise=unchecked(noise*1664525+1013904223);
            float grit=(noise>>16)/32768f-1;
            foreach(float beat in beats){
                float age=time-beat;if(age<0||age>.44f)continue;
                float tone=Mathf.Sin(2*Mathf.PI*(53*age+3*(1-Mathf.Exp(-age*15))));
                data[i]+=(tone*.53f+grit*.17f*Mathf.Exp(-age*22))*Mathf.Exp(-age*12);
            }
        }
        var clip=AudioClip.Create(loop?"Барабаны ярости":"Сигнал Орды",size,1,sampleRate,false);
        clip.SetData(data,0);return clip;
    }

    void UpdateHordeIntro(){
        hordeIntroAge+=Time.unscaledDeltaTime;
        float t=hordeIntroAge;
        var keep=World(HumanMatch.CastlePosition(1));
        var chief=hordeIntroChief?hordeIntroChief.transform.position+Vector3.up*2.2f:keep;
        var shaman=hordeIntroShaman?hordeIntroShaman.transform.position+Vector3.up*2:chief+Vector3.left*4;
        int shot=t<1.85f?0:t<4.1f?1:t<6.3f?2:t<8.25f?3:4;
        if(shot!=hordeIntroShot&&hordeSignal&&hordeHorn){
            hordeIntroShot=shot;
            if(shot==0||shot==3)hordeSignal.PlayOneShot(hordeHorn,shot==0?.39f:.58f);
        }
        if(hordeCup)AnimateHordeVessel(t,shaman,chief+Vector3.up*.18f);
        if(t>2.2f&&t<4.1f){
            if(hordeIntroShaman)hordeIntroShaman.Play("Spell");
            if(hordeIntroChief)hordeIntroChief.Play("Spell");
        }
        if(t>=4.1f&&!hordeIntroTurned){
            hordeIntroTurned=true;
            if(hordeCup)hordeCup.gameObject.SetActive(false);
            if(hordeIntroChief){TintHordeActor(hordeIntroChief,true);hordeIntroChief.Play("Attack",1,1);}
            if(hordeSignal&&hordeHorn)hordeSignal.PlayOneShot(hordeHorn,.76f);
            hordeRitualWave=MakeHordeRitualWave();
        }
        for(int i=0;i<hordeIntroGuards.Count;i++){
            var guard=hordeIntroGuards[i];
            if(!guard||t<4.25f+.16f*i||hordeTints.ContainsKey(guard))continue;
            TintHordeActor(guard,true);guard.Play("Attack",1,1);
        }
        if(hordeRitualWave){
            float pulse=Mathf.InverseLerp(4.1f,5.25f,t);
            hordeRitualWave.transform.position=chief-Vector3.up*1.99f;
            hordeRitualWave.transform.localScale=Vector3.one*Mathf.Lerp(1,14,pulse);
            var line=hordeRitualWave.GetComponent<LineRenderer>();
            line.startColor=line.endColor=new Color(1,.33f,.095f,.62f*(1-pulse));
            hordeRitualWave.SetActive(pulse<1);
        }
        if(shot==0){
            float p=Mathf.SmoothStep(0,1,t/1.85f);
            FrameWinterCamera(keep+Vector3.up*5+Vector3.left*4*p,57-9*p,-76+14*p,31);
        }else if(shot==1){
            float p=Mathf.InverseLerp(1.85f,4.1f,t);
            FrameWinterCamera(Vector3.Lerp(chief,shaman,.28f)+Vector3.down*.25f,23-4*p,75+4*p,35);
        }else if(shot==2){
            float p=Mathf.InverseLerp(4.1f,6.3f,t);
            float kick=Mathf.Sin(p*46)*Mathf.Max(0,1-p*8)*.27f;
            FrameWinterCamera(chief+Vector3.up*(.2f+kick),14+2*p,64-13*p,22+7*p);
        }else if(shot==3){
            float p=Mathf.InverseLerp(6.3f,8.25f,t);
            FrameWinterCamera(Vector3.Lerp(chief,keep,.38f)+Vector3.up*.2f,27+8*p,30-15*p,43);
        }
        else{
            float travel=Mathf.SmoothStep(0,1,Mathf.InverseLerp(8.25f,12,t));
            FrameWinterCamera(Vector3.Lerp(chief,World(match.Builders[0].position),travel),
                36+32*Mathf.Sin(travel*Mathf.PI),15*(1-travel),46+8*Mathf.Sin(travel*Mathf.PI));
        }
        if(t<12&&!hordeSkip&&!Input.GetKeyDown(KeyCode.Escape))return;
        EndHordeIntro();
    }

    GameObject MakeHordeRitualWave(){
        var line=new GameObject("Волна эликсира").AddComponent<LineRenderer>();
        line.transform.SetParent(hordeIntroProps.transform,false);
        line.sharedMaterial=hordeSmokeMaterial;line.loop=true;line.useWorldSpace=false;
        line.startWidth=line.endWidth=.07f;line.positionCount=64;
        for(int i=0;i<line.positionCount;i++){
            float angle=i*2*Mathf.PI/line.positionCount;
            line.SetPosition(i,new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)));
        }
        return line.gameObject;
    }

    void EndHordeIntro(){
        ClearHordeActors();
        hordeIntro=false;accumulator=0;
        controls.InputBlocked=false;
        UpdateFog(true);ApplyFogViews();controls.FocusWorld(World(match.Builders[0].position),40);
    }

    void ClearHordeActors(){
        foreach(var model in hordeIntroGuards){hordeTints.Remove(model);if(model)Destroy(model.gameObject);}
        hordeIntroGuards.Clear();
        if(hordeIntroChief){hordeTints.Remove(hordeIntroChief);Destroy(hordeIntroChief.gameObject);}
        if(hordeIntroShaman)Destroy(hordeIntroShaman.gameObject);
        hordeIntroChief=hordeIntroShaman=null;
        if(hordeIntroProps)Destroy(hordeIntroProps);
        hordeIntroProps=null;hordeCup=null;hordeLiquid=null;hordeOrbLight=null;hordeRitualWave=null;
        if(hordePotMesh)Destroy(hordePotMesh);
        if(hordeRimMesh)Destroy(hordeRimMesh);
        hordePotMesh=hordeRimMesh=null;
        if(hordeLiquidMaterial)Destroy(hordeLiquidMaterial);
        if(hordeSmokeMaterial)Destroy(hordeSmokeMaterial);
        if(hordeLiquidTexture)Destroy(hordeLiquidTexture);
        if(hordeSmokeTexture)Destroy(hordeSmokeTexture);
        hordeLiquidMaterial=hordeSmokeMaterial=null;hordeLiquidTexture=hordeSmokeTexture=null;
        Array.Clear(hordeOrbSparks,0,hordeOrbSparks.Length);
        Array.Clear(hordeFireSparks,0,hordeFireSparks.Length);
        Array.Clear(hordeSmoke,0,hordeSmoke.Length);
    }

    void RenderHordeFlask(PcModel view,HumanMatch.Unit unit){
        if(!match.IsHordeShaman(unit)||unit.hp<=0){
            if(hordeFlasks.Remove(view,out var old)&&old)Destroy(old);
            return;
        }
        if(!hordeFlasks.TryGetValue(view,out var flask)){
            flask=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flask.name="Фляга эликсира";
            flask.transform.SetParent(view.transform,false);
            flask.GetComponent<Renderer>().sharedMaterial=hordeElixirMaterial;
            Destroy(flask.GetComponent<Collider>());
            hordeFlasks[view]=flask;
        }
        var scale=view.transform.lossyScale;
        flask.transform.localScale=new Vector3(.5f/Mathf.Max(.01f,scale.x),.64f/Mathf.Max(.01f,scale.y),.5f/Mathf.Max(.01f,scale.z));
        flask.transform.position=view.transform.position+Vector3.up*Mathf.Max(1.2f,view.ClassicHeight*.72f)-view.transform.forward*.42f;
    }

    void ForgetHordeView(PcModel view){
        hordeTints.Remove(view);
        hordeFlasks.Remove(view);
    }

    void ClearHorde(){
        ClearHordeActors();RestoreHordeGround();hordeTints.Clear();hordeFlasks.Clear();hordeIntro=false;
        if(hordeElixirMaterial)Destroy(hordeElixirMaterial);
        hordeElixirMaterial=null;
        if(hordeIronMaterial)Destroy(hordeIronMaterial);
        if(hordeBronzeMaterial)Destroy(hordeBronzeMaterial);
        if(hordeCoreMaterial)Destroy(hordeCoreMaterial);
        hordeIronMaterial=hordeBronzeMaterial=hordeCoreMaterial=null;
        if(hordeDrums)Destroy(hordeDrums);
        if(hordeHorn)Destroy(hordeHorn);
        hordeDrums=hordeHorn=null;hordeMusic=hordeSignal=null;hordeChargeLine=null;
    }

    void RenderHordeBattle(){
        if(!match.CursedHorde||hordeIntro)return;
        if(hordeMusic){
            if(match.Finished){hordeMusic.Stop();if(hordeSignal)hordeSignal.Stop();}
            else{
                if(lastHordeBeat!=match.HordeDrumBeat){
                    lastHordeBeat=match.HordeDrumBeat;
                    if(hordeHorn&&hordeSignal)hordeSignal.PlayOneShot(hordeHorn,.72f);
                }
                if(match.HordeAnyRage){
                    if(!hordeMusic.loop&&hordeDrums){hordeMusic.clip=hordeDrums;hordeMusic.loop=true;hordeMusic.volume=.5f;hordeMusic.Play();}
                }else if(hordeMusic.loop){hordeMusic.loop=false;hordeMusic.Stop();}
            }
        }
        if(!match.HordeCharging||!FogVisible(match.HordeChargeStart)){
            if(hordeChargeLine)hordeChargeLine.enabled=false;
            return;
        }
        if(!hordeChargeLine){
            hordeChargeLine=new GameObject("Полоса прорыва").AddComponent<LineRenderer>();
            hordeChargeLine.transform.SetParent(visuals.transform,false);
            hordeChargeLine.sharedMaterial=impactMaterial;
            hordeChargeLine.positionCount=2;
            hordeChargeLine.startWidth=hordeChargeLine.endWidth=6.4f;
            hordeChargeLine.alignment=LineAlignment.TransformZ;
            hordeChargeLine.transform.rotation=Quaternion.Euler(90,0,0);
            hordeChargeLine.startColor=hordeChargeLine.endColor=new Color(1,.24f,.12f,.55f);
        }
        hordeChargeLine.enabled=true;
        hordeChargeLine.SetPosition(0,World(match.HordeChargeStart)+Vector3.up*.17f);
        hordeChargeLine.SetPosition(1,World(match.HordeChargeEnd)+Vector3.up*.17f);
    }

    void DrawHordeIntro(){
        Fill(new Rect(0,0,1280,61),new Color(.04f,.009f,.006f,.98f));
        Fill(new Rect(0,626,1280,94),new Color(.04f,.009f,.006f,.98f));
        float flash=hordeIntroTurned?.22f*(1-Mathf.Clamp01((hordeIntroAge-4.1f)/.36f)):0;
        if(flash>0)Fill(new Rect(0,61,1280,565),new Color(.94f,.17f,.04f,flash));
        float introFade=1-Mathf.Clamp01(hordeIntroAge/.58f);
        if(introFade>0)Fill(new Rect(0,61,1280,565),new Color(.045f,.012f,.008f,introFade));
        Text(42,647,550,45,"Проклятая Орда",heading);
        if(LobbyButton("horde.skip",new Rect(992,646,247,50),"Пропустить вступление"))hordeSkip=true;
    }

    static readonly Rect HordeHud=new Rect(286,38,280,63);
    void DrawHordeHud(){
        Border(HordeHud,HordeAccent,1);
        Fill(new Rect(HordeHud.x+1,HordeHud.y+1,HordeHud.width-2,HordeHud.height-2),new Color(.10f,.026f,.02f,.96f));
        var icon=HordeStatusArt(HumanMatch.HordeRageId);
        if(icon)GUI.DrawTexture(new Rect(HordeHud.x+7,HordeHud.y+8,45,45),icon,ScaleMode.ScaleToFit);
        bool second=SpectatorSceneReady?(bool?)spectatorBoss?["second"]==true:match.HordeSecondStage;
        Text(HordeHud.x+59,HordeHud.y+4,190,27,second?"Разбитый котёл":"Проклятая Орда",hudText);
        float? left=SpectatorSceneReady?(float?)spectatorBoss?["left"]:match.HordeNextWaveIn;
        int seconds=Mathf.CeilToInt(left??0),shamans=SpectatorSceneReady?(int?)spectatorBoss?["shamans"]??0:match.HordeShamansAlive;
        bool raging=SpectatorSceneReady?(bool?)spectatorBoss?["rage"]==true:match.HordeAnyRage;
        string state=shamans>0?"Шаманы с эликсиром · "+shamans+" из 3":
            raging?"Ярость эликсира":left.HasValue?"До выхода шаманов · "+(seconds/60)+":"+(seconds%60).ToString("00"):"Эликсир ярости";
        Text(HordeHud.x+59,HordeHud.y+29,213,24,state,hudSmall);
    }

    Texture2D HordeStatusArt(string id){
        if(id!=HumanMatch.HordeRageId&&id!=HumanMatch.HordeFatigueId)return null;
        if(!hordeElixirIcon)hordeElixirIcon=LoadBossFrame("HordeElixir");
        return hordeElixirIcon? hordeElixirIcon : Extra("debuff");
    }

    void DrawHordeUnitBadge(HumanMatch.Unit u,float x,float y){
        bool warchief=match.IsHordeWarchief(u),shaman=match.IsHordeShaman(u);
        if(!warchief&&!shaman&&!match.HordeRaging(u))return;
        var marker=new Rect(x+(warchief||shaman?-11:19),y+(warchief||shaman?-37:-7),warchief||shaman?22:12,warchief||shaman?22:12);
        Fill(marker,new Color(.15f,.025f,.02f,.93f));
        var icon=HordeStatusArt(HumanMatch.HordeRageId);
        if(icon)GUI.DrawTexture(marker,icon);
        Border(marker,HordeAccent,1);
        if(marker.Contains(HudPointer)&&GUI.enabled)hudTip=warchief?"Алый вождь\nИммунитет к заклинаниям и магическому урону":shaman?"Шаман с эликсиром":"Ярость эликсира";
    }

    IEnumerator ExerciseCursedHorde(){
        var folder=BattleLab.Arg("--pc-results",Application.persistentDataPath);
        Directory.CreateDirectory(folder);
        OpenProgression(false);
        yield return Action("progress.mythic");
        yield return Action("mythic.play."+HumanMatch.HordeTrial);
        yield return Action("mythic.race.0");
        yield return Action("mythic.begin");
        yield return RecoveryWait(()=>!versusActive&&hordeIntro,"VS leads to the Horde introduction",50);
        Check(match!=null&&match.CursedHorde&&hordeIntroChief&&hordeIntroShaman&&hordeIntroGuards.Count==4,
            "Intro has the chief, shaman, cauldron and four guards");
        var soundtrack=bossSoundtrack;
        Check(soundtrack&&soundtrack.clip&&soundtrack.clip.name==EpochalBossTrack&&soundtrack.clip.length>170&&
            soundtrack.isPlaying&&soundtrack.loop&&soundtrack.volume==1,
            "Frozen Final Stand starts at full volume for the Horde cinematic");
        Check(hordePainting,"Third trial loads its own key art");
        if(match==null){Application.Quit(1);yield break;}
        Check(hordeGround.Count>0&&hordeAtlases.Count>0&&hordeScorchMesh&&hordeMinimap,
            "VS prepares the scorched ground, layered embers and matching minimap");
        var restoredGround=hordeGround.Count>0?hordeGround[0]:default;
        yield return RecoveryWait(()=>hordeIntroAge>.72f,"The introduction opens on the Orc keep",4);
        yield return Shot(folder,"00-horde-keep");
        yield return RecoveryWait(()=>hordeIntroAge>3.15f,"The elixir rises and travels toward the chief",6);
        Check(hordePotMesh&&hordeRimMesh&&hordeOrbLight&&hordeOrbSparks.All(s=>s&&s.gameObject.activeSelf),
            "The forged vessel and lit elixir trail are visible during the handoff");
        yield return Shot(folder,"00-horde-elixir");
        yield return RecoveryWait(()=>hordeIntroTurned,"Elixir turns the chief and his escort red",8);
        Check(match.Elapsed==0&&controls.InputBlocked,"The battle clock waits for the cinematic");
        Check(hordeIntroShot==2&&hordeRitualWave,"The camera cuts in as a fire wave leaves the chief");
        yield return Shot(folder,"01-horde-intro");
        float skipTime=soundtrack?soundtrack.time:0;
        testHoldAuthority=true;
        yield return Action("horde.skip");
        yield return RecoveryWait(()=>!hordeIntro,"Skipping hands control to the builder",5);
        Check(soundtrack&&soundtrack==bossSoundtrack&&soundtrack.isPlaying&&soundtrack.time>=skipTime,
            "Taking control preserves the soundtrack playback position");
        yield return new WaitForSecondsRealtime(WinterMusicFadeSeconds+.15f);
        Check(soundtrack&&Mathf.Approximately(soundtrack.volume,WinterBattleMusicVolume),
            "The soundtrack fades from the intro to the normal battle volume");
        if(soundtrack)soundtrack.time=soundtrack.clip.length-.22f;
        yield return RecoveryWait(()=>soundtrack&&soundtrack.isPlaying&&soundtrack.time<2,
            "The normal battle soundtrack loops",4);
        var sim=match;
        testHoldAuthority=true;sim.PlayerVersusPlayer=true;
        Check(sim.CastleHp[0]==5000&&sim.CastleHp[1]==12000&&sim.Gold[1]>sim.Gold[0],
            "Only the Horde castle has 12000 HP and the boss receives extra opening gold");
        sim.Elapsed=179;sim.Step(.05f);
        Check(sim.HordeShamansAlive==0,"There are no ritual carriers before three minutes");
        sim.Elapsed=180;sim.Step(.05f);
        var shamans=sim.Units.Where(sim.IsHordeShaman).ToArray();
        Check(shamans.Length==3,"The first wave has three marked shamans");
        if(shamans.Length!=3){Application.Quit(1);yield break;}
        for(int frame=0;frame<1600&&shamans.Any(u=>u.pose!="Spell");frame++)
            foreach(var shaman in shamans)sim.TickUnit(shaman,.05f);
        Check(shamans.All(u=>u.pose=="Spell"&&Mathf.Abs(u.position.x)<=2),
            "All three carriers reach the central ritual sites through the road openings");
        int guard=Array.FindIndex(sim.Lines,line=>line.unit=="o002");
        var orcs=new List<HumanMatch.Unit>();
        for(int i=0;i<9;i++){
            var unit=sim.Spawn(1,guard,new Vector2(1+i*.6f,0));
            unit.stun=90;orcs.Add(unit);
        }
        var outside=sim.Spawn(1,guard,new Vector2(0,24));outside.stun=90;
        sim.Step(.05f);
        Check(!sim.HordeAnyRage,"Shamans take two seconds to finish a ritual");
        sim.DamageUnit(shamans[0],9999,0,execute:true);
        sim.DamageUnit(shamans[2],9999,0,execute:true);
        sim.Elapsed+=2;sim.Step(.05f);
        Check(sim.HordeShamansAlive==0&&orcs.Count(sim.HordeRaging)==8&&!sim.HordeRaging(outside),
            "Killing two shamans prevents their ritual and the survivor affects at most eight nearby Orcs");
        var rager=orcs.FirstOrDefault(sim.HordeRaging);
        var normal=orcs.FirstOrDefault(x=>!sim.HordeRaging(x));
        Check(rager!=null&&normal!=null&&Mathf.Approximately(sim.MoveSpeed(rager),sim.MoveSpeed(normal)*1.25f)&&
            Mathf.Approximately(sim.AttackSpeed(rager),1.35f)&&
            rager.effects.Any(e=>e.id==HumanMatch.HordeRageId&&e.WeaponDamageModifier==.2f),
            "Rage raises movement, attack speed and weapon damage by the advertised amounts");
        var scout=sim.Spawn(0,sim.Kind("h000"),Vector2.left*13);
        scout.stun=90;
        UpdateFog(true);RenderMatch();controls.FocusWorld(World(Vector2.zero),34);
        var ragerView=rager!=null&&units.TryGetValue(rager.id,out var view)?view:null;
        var baseScale=ragerView&&hordeTints.TryGetValue(ragerView,out var redState)?redState.scale:Vector3.zero;
        Check(ragerView&&baseScale!=Vector3.zero&&ragerView.transform.localScale==baseScale*HordeRedScale,
            "Raging units grow by 12 percent from their authored scale");
        yield return Shot(folder,"02-horde-rage");
        for(int i=0;i<241;i++)sim.Step(.05f);
        Check(!sim.HordeAnyRage&&rager!=null&&Mathf.Approximately(sim.AttackSpeed(rager),.75f),
            "Rage expires into attack fatigue");
        RenderMatch();
        Check(ragerView&&ragerView.transform.localScale==baseScale&&!hordeTints.ContainsKey(ragerView),
            "The model restores its exact base scale when rage wears off");
        for(int i=0;i<121;i++)sim.Step(.05f);
        Check(rager!=null&&Mathf.Approximately(sim.AttackSpeed(rager),1),
            "Fatigue also expires");
        sim.CastleHp[1]=6050;
        sim.Step(.05f);
        Check(!sim.HordeSecondStage,"The warchief waits until the boss castle loses half of its 12000 HP");
        sim.CastleHp[1]=5999;
        sim.Step(.05f);
        var chief=sim.Units.FirstOrDefault(sim.IsHordeWarchief);
        Check(sim.HordeSecondStage&&chief!=null&&sim.HordeWaveInterval==25&&sim.HordeAnyRage&&
            Mathf.Approximately(chief.maxHp,sim.Lines[chief.kind].hp*2.5f)&&
            sim.Units.Count(u=>sim.HordeRaging(u)&&u.kind==guard&&Mathf.Approximately(u.maxHp,sim.Lines[guard].hp*1.25f))==5,
            "At half health, the chief arrives with red guards and shortens the wave interval");
        yield return RecoveryWait(()=>soundtrack&&Mathf.Approximately(soundtrack.pitch,WinterPhaseTwoPitch),
            "The second Horde stage speeds up the battle soundtrack",3);
        if(chief!=null){
            float chiefHp=chief.hp;
            sim.Abilities["horde_qa_hex"]=new JObject{["id"]="horde_qa_hex",["kind"]="debuff",["root"]=true,["duration"]=4};
            sim.ApplyEffect(chief,"horde_qa_hex",0);
            Check(sim.DamageUnit(chief,1000,0,"magic")==0&&
                sim.DamageUnit(chief,1000,0,"magic",false)==0&&
                sim.DamageUnit(chief,1000,0,"spells")==0&&
                !chief.effects.Any(e=>e.id=="horde_qa_hex")&&chief.hp==chiefHp,
                "The Warchief ignores direct spells, magic weapons and hostile magical status");
            Check(sim.DamageUnit(chief,100,0,"normal",false)>0&&chief.hp<chiefHp,
                "Normal weapon hits still damage the Warchief");
            chief.position=Vector2.zero;
            var enemy=sim.Spawn(0,sim.Kind("h000"),Vector2.left*8);
            enemy.stun=90;
            float hp=enemy.hp;
            sim.Elapsed+=5;sim.TickUnit(chief,.05f);
            Check(sim.HordeCharging,"The warchief telegraphs a line charge");
            sim.Elapsed+=1.1f;sim.TickUnit(chief,.05f);
            Check(!sim.HordeCharging&&enemy.hp<hp&&chief.position.x<0&&chief.castPause>0,
                "Charge damages the line and opens the chief to a counterattack");
            sim.DamageUnit(chief,99999,0,execute:true);sim.Step(.05f);
            Check(sim.HordeWaveInterval==40&&sim.HordeNextWaveIn>39,"Killing the warchief restores the normal interval");
        }
        sim.Surrender(1);SettleChallenge();
        Check(soundtrack&&!soundtrack.isPlaying,"Ending the trial stops the soundtrack");
        MainMenu();Check(!hordeIntro&&!hordeMusic&&!hordeDrums&&!bossSoundtrack,
            "Leaving the trial clears its audio and cinematic");
        var restoredBlock=new MaterialPropertyBlock();
        if(restoredGround.renderer)restoredGround.renderer.GetPropertyBlock(restoredBlock);
        Check(hordeGround.Count==0&&hordeAtlases.Count==0&&!hordeMinimap&&!hordeScorches&&
            (!restoredGround.renderer||restoredBlock.GetTexture("_MainTex")==restoredGround.original.GetTexture("_MainTex")),
            "Leaving restores the base map and frees the Horde textures");
        OpenProgression(false);
        yield return Action("progress.mythic");
        yield return Action("mythic.play."+HumanMatch.HordeTrial);
        yield return Action("mythic.race.0");
        yield return Action("mythic.begin");
        yield return RecoveryWait(()=>!versusActive&&hordeIntro,"The Horde trial can be opened again",50);
        yield return RecoveryWait(()=>!hordeIntro&&match!=null&&match.CursedHorde,"The full cutscene hands control back automatically",16);
        Check(!controls.InputBlocked&&match!=null&&match.Elapsed<.3f&&hordeGround.Count>0,
            "The last camera shot reaches the builder before the battle clock starts");
        MainMenu();
        File.WriteAllText(Path.Combine(folder,"horde-checks.json"),new JObject{["checks"]=testChecks,["failures"]=testFailures}.ToString());
        Debug.Log("CF_HORDE_RESULT failures="+testFailures);
        Application.Quit(testFailures==0?0:1);
    }
}
}
