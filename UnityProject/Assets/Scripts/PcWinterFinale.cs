using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GLTFast;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace CastleFight {
public sealed partial class PcGame {
    const float WinterFinaleDuration=3.9f;
    GameObject winterQueenTemplate,winterIceCastle;
    GltfImport winterQueenImport;
    Material[] winterQueenMaterials;
    GameObject greatQuackTemplate;
    GltfImport greatQuackImport;
    Material[] greatQuackMaterials;
    Material winterIceMaterial;
    Mesh winterIceMesh;
    ParticleSystem winterQueenStorm;
    bool winterFinale,winterFinaleCast,winterFinaleShattered;
    float winterFinaleAge;
    Vector3 winterFinaleField;

    IEnumerator LoadWinterQueenArt(int generation){
        if(winterQueenTemplate)yield break;
        byte[] bytes=null;
        var asset=Resources.Load<TextAsset>("WinterBattle/WinterQueen");
        if(asset){bytes=asset.bytes;Resources.UnloadAsset(asset);}
        else if(!Application.isMobilePlatform){
            var path=MobileContent.Media("Frontend/WinterQueen.glb");
            if(File.Exists(path))bytes=File.ReadAllBytes(path);
        }
        if(bytes==null){Debug.LogWarning("CF_WINTER_QUEEN fallback: model asset missing");yield break;}
        var world=match;
        var load=ImportRitualActor(bytes,HumanMatch.WinterQueenRaw,"Jaina · Ice Queen",1.30f,4.9f,0,true);
        while(!load.IsCompleted)yield return null;
        if(load.IsFaulted){Debug.LogWarning("CF_WINTER_QUEEN fallback: "+load.Exception.GetBaseException().Message);yield break;}
        var (template,importer,materials)=load.Result;
        if(generation!=versusSerial||match!=world){
            ReleaseRitualArt(template,importer,materials);
            yield break;
        }
        winterQueenTemplate=template;winterQueenImport=importer;winterQueenMaterials=materials;
        Debug.Log("CF_WINTER_QUEEN_READY clips="+template.GetComponent<PcModel>().clips.Length);
    }

    IEnumerator LoadGreatQuackArt(int generation){
        if(greatQuackTemplate)yield break;
        var asset=Resources.Load<TextAsset>("WinterBattle/GreatQuack");
        byte[] bytes=null;
        if(asset){bytes=asset.bytes;Resources.UnloadAsset(asset);}
        else if(!Application.isMobilePlatform){
            var path=MobileContent.Media("Frontend/GreatQuack.glb");
            if(File.Exists(path))bytes=File.ReadAllBytes(path);
        }
        if(bytes==null){Debug.LogWarning("CF_GREAT_QUACK fallback: model asset missing");yield break;}
        var world=match;
        var load=ImportRitualActor(bytes,"pU13","Great Quack",.97f,4.5f,90,false);
        while(!load.IsCompleted)yield return null;
        if(load.IsFaulted){Debug.LogWarning("CF_GREAT_QUACK fallback: "+load.Exception.GetBaseException().Message);yield break;}
        var (template,importer,materials)=load.Result;
        if(generation!=versusSerial||match!=world){
            ReleaseRitualArt(template,importer,materials);
            yield break;
        }
        greatQuackTemplate=template;greatQuackImport=importer;greatQuackMaterials=materials;
        if(quackKing){
            quackKing.gameObject.SetActive(false);
            Destroy(quackKing.gameObject);
            quackKing=CreateQuackActor(HumanMatch.QuackKingBuilding,QuackKingPoint,1.8f);
            CacheQuackKingHead();
        }
        Debug.Log("CF_GREAT_QUACK_READY clips="+template.GetComponent<PcModel>().clips.Length);
    }

    async Task<(GameObject,GltfImport,Material[])> ImportRitualActor(byte[] bytes,string raw,string title,
        float scale,float height,float yaw,bool spellPose){
        var importer=new GltfImport();
        var materials=new List<Material>();
        var library=new GameObject(title+" model template");
        library.SetActive(false);
        try{
            if(!await importer.LoadGltfBinary(bytes,importSettings:new ImportSettings{AnimationMethod=AnimationMethod.Legacy}))
                throw new InvalidDataException("GLB import failed");
            var animated=new GameObject("Rig");
            animated.transform.SetParent(library.transform,false);
            if(!await importer.InstantiateMainSceneAsync(animated.transform))throw new InvalidDataException("Missing scene");
            var root=new GameObject(title);
            root.transform.SetParent(library.transform,false);
            animated.transform.SetParent(root.transform,false);
            var clips=importer.GetAnimationClips();
            if(new[]{"Stand","Walk","Attack","Death"}.Any(name=>!clips.Any(clip=>clip.name==name))||
                spellPose&&!clips.Any(clip=>clip.name=="Spell"))
                throw new InvalidDataException("Missing combat animation");
            var json=JObject.Parse(Encoding.UTF8.GetString(bytes,20,BitConverter.ToInt32(bytes,12)));
            var slots=((JArray)json["meshes"][0]["primitives"]).Select(p=>(int)p["material"]).ToArray();
            var shader=Shader.Find("CastleFight/NativeUnit");
            int slot=0;
            foreach(var skin in animated.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
                var assigned=skin.sharedMaterials;
                for(int i=0;i<assigned.Length;i++){
                    var spec=json["materials"][slots[slot++]];
                    var pbr=spec["pbrMetallicRoughness"];
                    int? texture=(int?)pbr?["baseColorTexture"]?["index"];
                    var rgba=pbr?["baseColorFactor"] as JArray;
                    var tint=rgba?.Count==4?new Color((float)rgba[0],(float)rgba[1],(float)rgba[2],(float)rgba[3]):Color.white;
                    bool blend=(string)spec["alphaMode"]=="BLEND";
                    var material=new Material(shader){name=(string)spec["name"],renderQueue=blend?3000:2450,
                        mainTexture=texture.HasValue?importer.GetTexture(texture.Value):Texture2D.whiteTexture};
                    material.SetColor("_Tint",tint);
                    material.SetInt("_SrcBlend",(int)(blend?BlendMode.SrcAlpha:BlendMode.One));
                    material.SetInt("_DstBlend",(int)(blend?BlendMode.OneMinusSrcAlpha:BlendMode.Zero));
                    material.SetInt("_ZWrite",blend?0:1);
                    material.SetFloat("_Cutoff",(float?)spec["alphaCutoff"]??.02f);
                    if(spec["emissiveFactor"]!=null)material.SetFloat("_Unlit",.45f);
                    materials.Add(material);assigned[i]=material;
                }
                skin.sharedMaterials=assigned;skin.updateWhenOffscreen=true;
                skin.shadowCastingMode=ShadowCastingMode.Off;
            }
            if(slot!=slots.Length)throw new InvalidDataException("Unexpected animated submeshes");
            root.transform.localScale=Vector3.one*scale;
            animated.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var view=root.AddComponent<PcModel>();
            view.raw=raw;view.appearanceKey="pepe"; // This rig has no WC3 geoset tracks.
            view.animatedRoot=animated;view.clips=clips;view.godotHeight=height;
            return (root,importer,materials.ToArray());
        }catch{
            Destroy(library);
            foreach(var material in materials)Destroy(material);
            importer.Dispose();
            throw;
        }
    }

    void ReleaseRitualArt(GameObject template,GltfImport importer,Material[] materials){
        if(template)Destroy(template.transform.parent.gameObject);
        if(materials!=null)foreach(var material in materials)if(material)Destroy(material);
        importer?.Dispose();
    }

    GameObject WinterRoyalModel(int team){
        if(winterQueenTemplate)return winterQueenTemplate;
        int kind=Array.FindIndex(match.Lines,line=>line.unit=="n014");
        return kind>=0?RaceModel(team,kind,false):null;
    }

    void ReplaceWinterOpeningQueen(){
        if(!winterQueenTemplate||!winterQueen)return;
        var point=winterQueen.transform.position;
        var facing=winterQueen.transform.rotation;
        winterQueen.gameObject.SetActive(false);
        Destroy(winterQueen.gameObject);
        winterQueen=Instantiate(winterQueenTemplate,point,facing,visuals.transform).GetComponent<PcModel>();
        winterQueen.Play("Stand");
    }

    void BeginWinterFinale(){
        if(!match.BeginWinterAwakening())return;
        winterFinale=true;winterFinaleCast=winterFinaleShattered=false;winterFinaleAge=0;
        var queen=match.WinterBoss;
        var army=match.Units.Where(unit=>unit.team==0&&unit.hp>0&&!match.Hidden(unit))
            .OrderBy(unit=>(unit.position-queen.position).sqrMagnitude).FirstOrDefault();
        winterFinaleField=World(army?.position??match.Builders[0].position);
        controls.InputBlocked=true;controls.EdgePan=false;controls.GameplayViewport(false);
        CloseBattleHelp();ClearGroupSelection();
        chosen=selectedItem=selectedHut=selectedUnit=-1;
        targetedSpell=null;buildMenu=moveOrder=builderSelected=selectedCastle=false;
        if(preview)preview.SetActive(false);
        if(localGrid)localGrid.SetActive(false);
        controls.buildGrid.SetActive(false);
        FrameWinterCamera(World(queen.position)+Vector3.up*4,33,-25,40);
        Debug.Log("CF_WINTER_FINALE_BEGIN");
    }

    void UpdateWinterFinale(){
        winterFinaleAge+=Time.unscaledDeltaTime;
        if(!winterFinaleCast&&winterFinaleAge>=.65f){
            winterFinaleCast=true;match.FreezeWinterArmy();
        }
        if(!winterFinaleShattered&&winterFinaleAge>=2.2f){
            winterFinaleShattered=true;match.ReleaseWinterQueen();
        }
        var queen=World(match.WinterBoss.position)+Vector3.up*3;
        if(winterFinaleAge<1.32f)
            FrameWinterCamera(Vector3.Lerp(queen,WinterCastleFocus(),.30f),Mathf.Lerp(38,26,winterFinaleAge/1.32f),-32,40);
        else{
            float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.65f,WinterFinaleDuration,winterFinaleAge));
            FrameWinterCamera(Vector3.Lerp(winterFinaleField+Vector3.up*2,World(match.Builders[0].position),t),
                Mathf.Lerp(32,43,t),Mathf.Lerp(-12,0,t),Mathf.Lerp(48,56,t));
        }
        RenderMatch();
        if(winterFinaleAge<WinterFinaleDuration)return;
        winterFinale=false;accumulator=0;page=Page.Match;
        controls.FocusWorld(World(match.Builders[0].position),43);
        UpdateFog(true);
        Notice("Остановите Джайну, чтобы снять лёд с замка");
    }

    void DrawWinterFinale(){
        Fill(new Rect(0,0,1280,63),Color.black);
        Fill(new Rect(0,644,1280,76),Color.black);
        float flash=Mathf.Max(0,1-Mathf.Abs(winterFinaleAge-.74f)/.22f)*.38f;
        if(flash>0)Fill(new Rect(0,63,1280,581),new Color(.69f,.88f,1,flash));
        Text(43,650,1160,53,winterFinaleShattered?"Джайна идёт на вашу базу":"Ледяная королева · Последний приговор",heading);
    }

    bool WinterIceVisible=>SpectatorSceneReady?(bool?)spectatorBoss?["frozen"]==true:match.WinterCastleFrozen;
    bool WinterBossDefeated=>SpectatorSceneReady?(bool?)spectatorBoss?["defeated"]==true:match.WinterQueenFelled;

    void RenderWinterFinale(){
        bool ice=WinterIceVisible&&(winterFinale||!FogActive||fog.Known(HumanMatch.CastlePosition(1)));
        if(ice&&!winterIceCastle)MakeWinterCastleIce();
        if(winterIceCastle){
            winterIceCastle.SetActive(ice);
            if(ice&&winterFinale)winterIceCastle.transform.localScale=Vector3.one*Mathf.Lerp(.38f,1,Mathf.SmoothStep(0,1,winterFinaleAge/.65f));
            else winterIceCastle.transform.localScale=Vector3.one;
        }
        int queenId=SpectatorSceneReady?(int?)spectatorBoss?["queen_id"]??-1:match.WinterBoss?.id??-1;
        bool storm=SpectatorSceneReady?(bool?)spectatorBoss?["storm"]==true:match.WinterStormActive;
        if(storm&&units.TryGetValue(queenId,out var actor)&&actor.gameObject.activeInHierarchy){
            if(!winterQueenStorm)MakeWinterFinaleStorm();
            winterQueenStorm.transform.position=actor.transform.position;
            if(!winterQueenStorm.isPlaying)winterQueenStorm.Play();
        }else if(winterQueenStorm&&winterQueenStorm.isPlaying)
            winterQueenStorm.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void MakeWinterCastleIce(){
        winterIceCastle=new GameObject("Frozen Northern castle");
        winterIceCastle.transform.SetParent(visuals.transform,false);
        winterIceCastle.transform.position=World(HumanMatch.CastlePosition(1));
        var vertices=new List<Vector3>();var triangles=new List<int>();
        const int facets=5,shards=17;
        for(int i=0;i<shards;i++){
            float angle=i*(2*Mathf.PI/shards);
            var edge=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            var center=edge*(i%3==0?4.2f:6.7f);
            int index=vertices.Count;
            float height=5.5f+(i*19%11)*.63f;
            float width=1.1f+(i*11%5)*.27f;
            for(int j=0;j<facets;j++){
                float a=angle+j*(2*Mathf.PI/facets);
                var rim=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*width;
                vertices.Add(center+rim);
                vertices.Add(center-edge*.65f+rim*.83f+Vector3.up*height*.67f);
            }
            int tip=vertices.Count;
            vertices.Add(center-edge*(1.8f+(i%3)*.48f)+Vector3.up*height);
            for(int j=0;j<facets;j++){
                int a=index+2*j,b=index+2*((j+1)%facets);
                triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b,a+1,tip,b+1});
            }
        }
        winterIceMesh=new Mesh{name="Faceted castle ice"};
        winterIceMesh.SetVertices(vertices);winterIceMesh.SetTriangles(triangles,0);winterIceMesh.RecalculateNormals();
        winterIceCastle.AddComponent<MeshFilter>().sharedMesh=winterIceMesh;
        winterIceMaterial=new Material(Shader.Find("CastleFight/NativeUnit")){
            name="Translucent glacial ice",mainTexture=Texture2D.whiteTexture,renderQueue=2996};
        winterIceMaterial.SetColor("_Tint",new Color(.34f,.75f,1,.70f));
        winterIceMaterial.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);
        winterIceMaterial.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
        winterIceMaterial.SetInt("_ZWrite",0);
        winterIceMaterial.SetFloat("_Unlit",.35f);
        var renderer=winterIceCastle.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=winterIceMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;
    }

    void MakeWinterFinaleStorm(){
        var obj=new GameObject("Ice Queen storm");
        obj.transform.SetParent(visuals.transform,false);
        winterQueenStorm=obj.AddComponent<ParticleSystem>();
        winterQueenStorm.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=winterQueenStorm.main;
        main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.maxParticles=Application.isMobilePlatform?32:52;
        main.startLifetime=1.1f;main.startSpeed=0;
        main.startSize=new ParticleSystem.MinMaxCurve(.8f,2.1f);
        main.startColor=new ParticleSystem.MinMaxGradient(new Color(.72f,.9f,1,.7f),new Color(.8f,.98f,1,.95f));
        var shape=winterQueenStorm.shape;shape.shapeType=ParticleSystemShapeType.Circle;
        shape.rotation=new Vector3(90,0,0);shape.radius=HumanMatch.WinterStormRadius;shape.radiusThickness=.38f;
        var movement=winterQueenStorm.velocityOverLifetime;
        movement.enabled=true;movement.y=3.4f;movement.orbitalY=2.5f;
        var emission=winterQueenStorm.emission;emission.rateOverTime=main.maxParticles;
        var renderer=winterQueenStorm.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial=winterGustMaterial?winterGustMaterial:ContentResources.Load<Material>("NorthernSnow");
        renderer.shadowCastingMode=ShadowCastingMode.Off;
    }

    void ClearWinterFinale(){
        winterFinale=false;
        if(winterIceCastle)Destroy(winterIceCastle);
        if(winterIceMesh)Destroy(winterIceMesh);
        if(winterIceMaterial)Destroy(winterIceMaterial);
        winterIceCastle=null;winterIceMesh=null;winterIceMaterial=null;winterQueenStorm=null;
        ReleaseRitualArt(winterQueenTemplate,winterQueenImport,winterQueenMaterials);
        winterQueenTemplate=null;winterQueenImport=null;winterQueenMaterials=null;
    }

    void ClearGreatQuackArt(){
        ReleaseRitualArt(greatQuackTemplate,greatQuackImport,greatQuackMaterials);
        greatQuackTemplate=null;greatQuackImport=null;greatQuackMaterials=null;
    }
}
}
