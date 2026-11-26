using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    const float ProtocolMarchIntroLength=8.4f;
    bool protocolMarchIntro,protocolMarchSeen,protocolLiftCue;
    float protocolMarchIntroAge;
    GameObject protocolChassis;
    ParticleSystem protocolLaunchSparks;
    LineRenderer protocolDriveGlow;

    void BeginProtocolMarchIntro(){
        if(protocolMarchSeen||!protocolProps||match.Finished)return;
        protocolMarchIntro=protocolMarchSeen=true;
        protocolMarchIntroAge=0;protocolLiftCue=false;
        controls.InputBlocked=true;controls.EdgePan=false;
        if(localGrid)localGrid.SetActive(false);
        if(preview)preview.SetActive(false);
        BuildProtocolChassis();
        BuildProtocolLaunchSparks();
        if(protocolAudio&&protocolChargeSound)protocolAudio.PlayOneShot(protocolChargeSound,.65f);
        RenderMatch();
    }

    void BuildProtocolChassis(){
        protocolChassis=new GameObject("Шасси ходячей крепости");
        protocolChassis.transform.SetParent(protocolProps.transform,false);
        ProtocolChassisPlate("Станина",new Vector3(0,.46f,0),new Vector3(13,.85f,13));
        for(int side=-1;side<=1;side+=2){
            ProtocolChassisPlate("Гусеница",new Vector3(0,1,side*6.2f),new Vector3(14,1.9f,3.05f));
            for(int i=-2;i<=2;i++)
                ProtocolChassisPlate("Броневой трак",new Vector3(i*2.7f,1.33f,side*7.78f),new Vector3(.6f,1.27f,.28f));
        }
        // The glowing drive seam tracks the chassis instead of lighting the entire map.
        protocolDriveGlow=ProtocolLine("Охлаждение ходовой части",new Vector3[4],.19f,true);
    }

    void ProtocolChassisPlate(string name,Vector3 center,Vector3 scale){
        var plate=GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name=name;
        plate.transform.SetParent(protocolChassis.transform,false);
        plate.transform.localPosition=center;
        plate.transform.localScale=scale;
        plate.GetComponent<Renderer>().sharedMaterial=protocolGateMaterial;
        Destroy(plate.GetComponent<Collider>());
    }

    void BuildProtocolLaunchSparks(){
        var node=new GameObject("Разряд при подъёме");
        node.transform.SetParent(protocolProps.transform,false);
        node.transform.position=World(match.ProtocolMarchPosition)+Vector3.up*.6f;
        protocolLaunchSparks=node.AddComponent<ParticleSystem>();
        protocolLaunchSparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=protocolLaunchSparks.main;
        main.loop=false;main.duration=2;main.startLifetime=new ParticleSystem.MinMaxCurve(.45f,1.2f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(4,11);main.startSize=new ParticleSystem.MinMaxCurve(.08f,.23f);
        main.startColor=new ParticleSystem.MinMaxGradient(ProtocolGold,ProtocolCyan);main.gravityModifier=.55f;main.maxParticles=72;
        var emission=protocolLaunchSparks.emission;emission.rateOverTime=0;
        var shape=protocolLaunchSparks.shape;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=5.5f;
        node.transform.rotation=Quaternion.Euler(-65,0,0);
        var renderer=protocolLaunchSparks.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial=protocolMaterial;renderer.renderMode=ParticleSystemRenderMode.Stretch;
        renderer.lengthScale=1.8f;renderer.velocityScale=.18f;
    }

    Vector2 ProtocolMarchViewPosition(float interpolation){
        var position=Vector2.Lerp(match.ProtocolMarchPrevious,match.ProtocolMarchPosition,interpolation);
        if(!protocolMarchIntro)return position;
        float travel=Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.3f,ProtocolMarchIntroLength,protocolMarchIntroAge));
        return new Vector2(position.x-5*travel,Mathf.Lerp(position.y,0,travel));
    }

    float ProtocolCastleLift(){
        if(!protocolMarchIntro)return 1.75f;
        float t=protocolMarchIntroAge;
        float lift=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.05f,3.45f,t));
        float settle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.45f,4.45f,t));
        return 2.85f*lift-1.10f*settle;
    }

    void RenderProtocolChassis(Vector2 center,float lift){
        if(!protocolChassis)return;
        var origin=World(center);
        protocolChassis.transform.position=origin+Vector3.up*Mathf.Max(-1.8f,(lift-1.75f)*.55f);
        bool visible=protocolMarchIntro||FogVisible(center);
        protocolChassis.SetActive(visible);
        float y=.18f+Mathf.Min(1.75f,lift);
        protocolDriveGlow.SetPosition(0,origin+new Vector3(-6,y,-5.8f));
        protocolDriveGlow.SetPosition(1,origin+new Vector3(-6,y,5.8f));
        protocolDriveGlow.SetPosition(2,origin+new Vector3(6,y,5.8f));
        protocolDriveGlow.SetPosition(3,origin+new Vector3(6,y,-5.8f));
        ProtocolTint(protocolDriveGlow,visible,new Color(.21f,.81f,1,.82f));
    }

    void UpdateProtocolMarchIntro(){
        controls.InputBlocked=true;controls.EdgePan=false;
        protocolMarchIntroAge+=Time.unscaledDeltaTime;
        float t=protocolMarchIntroAge;
        if(!protocolLiftCue&&t>=2.0f){
            protocolLiftCue=true;
            if(protocolLaunchSparks){protocolLaunchSparks.Play();protocolLaunchSparks.Emit(64);}
            if(protocolAudio&&protocolShotSound)protocolAudio.PlayOneShot(protocolShotSound,.56f);
        }
        var center=World(ProtocolMarchViewPosition(1))+Vector3.up*(2+ProtocolCastleLift());
        if(t<3.4f){
            float pan=Mathf.InverseLerp(0,3.4f,t);
            FrameWinterCamera(center,47-12*pan,-62+17*pan,37);
        }else if(t<5.6f){
            float pan=Mathf.InverseLerp(3.4f,5.6f,t);
            FrameWinterCamera(center,33+4*pan,18+32*pan,28+12*pan);
        }else{
            float pan=Mathf.SmoothStep(0,1,Mathf.InverseLerp(5.6f,ProtocolMarchIntroLength,t));
            FrameWinterCamera(Vector3.Lerp(center,World(match.Builders[0].position),pan),
                40+38*Mathf.Sin(pan*Mathf.PI),-12*(1-pan),55);
        }
        RenderProtocol();
        if(t>=ProtocolMarchIntroLength||Input.GetKeyDown(KeyCode.Escape))EndProtocolMarchIntro();
    }

    void EndProtocolMarchIntro(){
        if(!protocolMarchIntro)return;
        protocolMarchIntro=false;
        match.CompleteProtocolLaunch();
        if(protocolLaunchSparks){Destroy(protocolLaunchSparks.gameObject);protocolLaunchSparks=null;}
        accumulator=0;
        controls.InputBlocked=false;
        controls.FocusWorld(World(match.Builders[0].position),40);
        UpdateFog(true);RenderMatch();
    }

    void DrawProtocolMarchIntro(){
        Fill(new Rect(0,0,1280,63),new Color(0,0,0,.96f));
        Fill(new Rect(0,610,1280,110),new Color(0,0,0,.96f));
        FitBossText(new Rect(52,628,916,53),protocolMarchIntroAge<3.45f?"Трон поднимается":
            "Терминатор · ходячая крепость",new GUIStyle(heading),33);
        if(LobbyButton("protocol.march.skip",new Rect(1061,642,167,42),"Пропустить"))EndProtocolMarchIntro();
    }

    void ClearProtocolMarchPresentation(){
        protocolMarchIntro=protocolMarchSeen=protocolLiftCue=false;
        protocolChassis=null;protocolLaunchSparks=null;protocolDriveGlow=null;
    }
}
}
