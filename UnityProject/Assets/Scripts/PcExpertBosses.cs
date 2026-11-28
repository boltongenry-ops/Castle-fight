using System;
using System.Collections;
using System.IO;
using GLTFast;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    const float YurneroRevealAt=1.53f,YurneroSceneLength=4.8f;
    GameObject yurneroTemplate,yurneroCracks;
    LineRenderer[] yurneroFaults;
    GltfImport yurneroImport;
    Material[] yurneroMaterials;
    Material yurneroCrevice;
    bool yurneroScene,yurneroSkip;
    float yurneroAge;
    bool ExpertCastleHidden=>SpectatorSceneReady?(bool?)spectatorBoss?["fallen"]==true:match?.ExpertCastleFallen==true;
    float ExpertFinalBossHp=>SpectatorSceneReady?(float?)spectatorBoss?["final_hp"]??0:match?.ExpertFinalBoss?.hp??0;

    static bool HasExpertFinale(string id)=>HumanMatch.IsExpertTrial(id)&&HumanMatch.EpochalFamily(id) is HumanMatch.HordeTrial or HumanMatch.AbyssTrial;

    IEnumerator PrepareYurneroArt(int generation){
        if(yurneroTemplate)yield break;
        byte[] data=null;
        var asset=Resources.Load<TextAsset>("WinterBattle/Yurnero");
        if(asset){data=asset.bytes;Resources.UnloadAsset(asset);}
        else if(!Application.isMobilePlatform){
            string path=MobileContent.Media("Frontend/Yurnero.glb");
            if(File.Exists(path))data=File.ReadAllBytes(path);
        }
        if(data==null){Debug.LogWarning("CF_YURNERO: classic model missing");yield break;}
        var world=match;
        var load=ImportRitualActor(data,HumanMatch.YurneroRaw,"Yurnero · Crimson Blademaster",1.16f,6.6f,0,true);
        while(!load.IsCompleted)yield return null;
        if(load.IsFaulted){Debug.LogWarning("CF_YURNERO: "+load.Exception.GetBaseException().Message);yield break;}
        var (model,importer,materials)=load.Result;
        if(generation!=versusSerial||world!=match){ReleaseRitualArt(model,importer,materials);yield break;}
        yurneroTemplate=model;yurneroImport=importer;yurneroMaterials=materials;
    }

    GameObject ExpertBossModel(int team,string raw){
        if(raw==HumanMatch.YurneroRaw&&yurneroTemplate)return yurneroTemplate;
        int kind=Array.FindIndex(match.Lines,l=>l.unit==(raw==HumanMatch.HydraRaw?"n01P":"o005"));
        return kind>=0?RaceModel(team,kind,false):null;
    }

    void BeginYurneroScene(){
        yurneroScene=true;yurneroSkip=false;yurneroAge=0;
        controls.InputBlocked=true;controls.EdgePan=false;controls.GameplayViewport(false);
        CloseBattleHelp();ClearGroupSelection();
        chosen=selectedItem=selectedHut=selectedUnit=-1;
        targetedSpell=null;buildMenu=moveOrder=builderSelected=selectedCastle=false;
        if(preview)preview.SetActive(false);
        if(localGrid)localGrid.SetActive(false);
        controls.buildGrid.SetActive(false);
        if(hordeSignal&&hordeDrums)hordeSignal.PlayOneShot(hordeDrums,.8f);
        MakeYurneroFaults();
        FrameWinterCamera(World(HumanMatch.YurneroEmergence)+Vector3.up,34,-31,54);
    }

    void MakeYurneroFaults(){
        if(yurneroCracks)Destroy(yurneroCracks);
        yurneroCracks=new GameObject("Crimson ground faults");yurneroCracks.transform.SetParent(visuals.transform,false);
        if(!yurneroCrevice)yurneroCrevice=new Material(Shader.Find("Sprites/Default"));
        yurneroFaults=new LineRenderer[9];
        for(int i=0;i<yurneroFaults.Length;i++){
            var ray=new GameObject("Fault "+i);ray.transform.SetParent(yurneroCracks.transform,false);
            var line=ray.AddComponent<LineRenderer>();yurneroFaults[i]=line;
            line.sharedMaterial=yurneroCrevice;line.positionCount=6;line.startWidth=.24f;line.endWidth=.055f;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
        }
    }

    void UpdateYurneroFaults(Vector3 center,float progress){
        for(int i=0;i<yurneroFaults.Length;i++){
            var line=yurneroFaults[i];float angle=i*Mathf.PI*2/yurneroFaults.Length;
            var forward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            var sideways=Vector3.Cross(Vector3.up,forward);
            line.startColor=new Color(.98f,.25f,.055f,.94f);
            line.endColor=new Color(.27f,.049f,.021f,.12f);
            for(int j=0;j<line.positionCount;j++){
                float reach=j*(1.5f+.21f*(i%4))*progress;
                line.SetPosition(j,center+Vector3.up*.075f+forward*reach+sideways*Mathf.Sin(i*2.31f+j*3.6f)*.20f*reach);
            }
        }
    }

    void UpdateYurneroScene(){
        yurneroAge=yurneroSkip?YurneroSceneLength:yurneroAge+Time.unscaledDeltaTime;
        var center=World(HumanMatch.YurneroEmergence);
        float tremor=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(YurneroRevealAt,YurneroRevealAt+.6f,yurneroAge));
        var shake=new Vector3(Mathf.Sin(64*yurneroAge),Mathf.Sin(83*yurneroAge)*.67f,0)*tremor*.39f;
        UpdateYurneroFaults(center,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.34f,YurneroRevealAt+.25f,yurneroAge)));
        if(yurneroAge>=YurneroRevealAt&&match.ExpertFinalBoss==null){
            match.RevealYurnero();
            if(hordeSignal&&hordeHorn)hordeSignal.PlayOneShot(hordeHorn,.88f);
        }
        float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(YurneroRevealAt,YurneroRevealAt+.72f,yurneroAge));
        float pan=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.30f,YurneroSceneLength,yurneroAge));
        var face=center+Vector3.up*(1.4f+1.9f*rise);
        FrameWinterCamera(Vector3.Lerp(face,World(match.Builders[0].position),pan)+shake,
            Mathf.Lerp(37-12*rise,43,pan),Mathf.Lerp(-57+34*rise,0,pan),Mathf.Lerp(57-29*rise,55,pan));
        if(rise>=.90f&&match.ExpertFinalBoss!=null)match.ExpertFinalBoss.pose="Spell";
        RenderMatch();
        if(match.ExpertFinalBoss!=null&&units.TryGetValue(match.ExpertFinalBoss.id,out var view))
            view.transform.position-=Vector3.up*(1-rise)*6.5f;
        if(yurneroAge<YurneroSceneLength)return;
        match.ReleaseYurnero();
        if(yurneroCracks)Destroy(yurneroCracks);
        yurneroCracks=null;yurneroFaults=null;yurneroScene=false;accumulator=0;
        controls.FocusWorld(center,43);UpdateFog(true);
        Notice("Победите Юрнеро, чтобы спасти замок");
    }

    void DrawYurneroScene(){
        Fill(new Rect(0,0,1280,62),Color.black);
        Fill(new Rect(0,643,1280,77),Color.black);
        float flash=Mathf.Clamp01(1-Mathf.Abs(yurneroAge-YurneroRevealAt)/.23f)*.29f;
        if(flash>0)Fill(new Rect(0,62,1280,581),new Color(1,.22f,.065f,flash));
        Text(42,647,760,45,yurneroAge<YurneroRevealAt?"Земля содрогается":"Юрнеро · Багровый клинок",heading);
        if(LobbyButton("expert.yurnero.skip",new Rect(992,646,247,50),"Пропустить"))yurneroSkip=true;
    }

    void ClearExpertBossArt(){
        if(yurneroCracks)Destroy(yurneroCracks);
        if(yurneroCrevice)Destroy(yurneroCrevice);
        yurneroCracks=null;yurneroCrevice=null;yurneroFaults=null;yurneroScene=false;
        ReleaseRitualArt(yurneroTemplate,yurneroImport,yurneroMaterials);
        yurneroTemplate=null;yurneroImport=null;yurneroMaterials=null;
    }
}
}
