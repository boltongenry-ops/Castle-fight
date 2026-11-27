using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    const float ProtocolIntroLength=10.5f;
    static readonly Color ProtocolGold=new Color(.97f,.62f,.23f);
    static readonly Color ProtocolCyan=new Color(.32f,.88f,1);
    static readonly Rect ProtocolHud=new Rect(277,34,325,85);
    bool protocolIntro,protocolVersus;
    float protocolIntroAge,protocolLastOpening;
    GameObject protocolProps;
    PcModel protocolActor;
    Material protocolMaterial;
    Texture2D protocolPainting,protocolHudFrame,protocolHudLight;
    GUIStyle protocolHudTitle,protocolHudDetail;
    Texture2D protocolArmorAtlas,protocolChassisAtlas;
    int protocolReskinnedMaterials;
    AudioSource protocolAudio;
    AudioClip protocolChargeSound,protocolShotSound;
    Vector3 protocolCastleOrigin;
    readonly List<LineRenderer> protocolCircles=new List<LineRenderer>(9);
    readonly LineRenderer[] protocolWires=new LineRenderer[2];
    readonly Transform[] protocolGates=new Transform[2];
    Material protocolGateMaterial;
    LineRenderer protocolRail,protocolShield,protocolBeam;

    static JObject ProtocolDefinition()=>new JObject{
        ["id"]=HumanMatch.ProtocolTrial,["title"]="Последний протокол",["opponent"]="Mech Coalition",
        ["summary"]="Минное поле, две электростанции и орудие Терминатора.",
        ["description"]="Мины и зенитки прикрывают две электростанции. Каждая даёт замку 35% защиты. Разрушьте обе и доведите замок до 40% HP. После залпа ворота открываются на 12 с. На 7-й минуте Терминатор выдвинет трон к вашему замку.",
        ["start_gold"]=600,["opponent_start_gold"]=750,["opponent_castle_hp"]=HumanMatch.ProtocolCastleHealth,
        ["difficulty"]=2,["reward"]=0,["xp"]=0
    };

    bool ActiveProtocolTrial=>(string)activeChallenge?["id"]==HumanMatch.ProtocolTrial;
    bool CurrentMythic=>match!=null&&(match.EternalWinter||match.FuneralChoir||match.CursedHorde||match.LastProtocol||match.AbyssTrialActive||match.QuackActive);

    void DrawProtocolPainting(Rect area){
        if(!protocolPainting)protocolPainting=LoadProtocolPainting();
        Fill(area,new Color(.07f,.085f,.092f));
        if(protocolPainting){
            if(area.width>area.height*3){
                float height=area.height*protocolPainting.width/(area.width*protocolPainting.height);
                GUI.DrawTextureWithTexCoords(area,protocolPainting,new Rect(0,(1-height)*.78f,1,height));
            }else GUI.DrawTexture(area,protocolPainting,ScaleMode.ScaleAndCrop);
        }
        WinterGradient(new Rect(area.x,area.y,area.width*.54f,area.height),true,new Color(.036f,.025f,.013f,.86f));
    }

    static Texture2D LoadProtocolPainting(){
        var painting=LoadProtocolTexture("LastProtocol","jpg",TextureFormat.RGB24,false);
        return painting?painting:MobileVersusPicture(6);
    }

    static Texture2D LoadProtocolTexture(string name,string extension,TextureFormat format,bool mipmaps){
        var asset=Resources.Load<TextAsset>("WinterBattle/"+name);
        string path=MobileContent.Media("Frontend/"+name+"."+extension);
        byte[] data=asset?asset.bytes:File.Exists(path)?File.ReadAllBytes(path):null;
        if(data==null)return null;
        var texture=new Texture2D(2,2,format,mipmaps){name=name,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        if(texture.LoadImage(data,true))return texture;
        Destroy(texture);
        return null;
    }

    void BeginProtocolIntro(){
        protocolIntro=true;protocolIntroAge=0;protocolLastOpening=0;
        protocolCastleOrigin=castleObjects[1].transform.position;
        protocolProps=new GameObject("Последний протокол");
        protocolProps.transform.SetParent(visuals.transform,false);
        protocolMaterial=new Material(Shader.Find("Sprites/Default")??shellMaterial.shader);
        PrepareProtocolGround();
        protocolHudFrame=LoadProtocolTexture("ProtocolMeter","png",TextureFormat.RGBA32,false);
        protocolHudLight=MakeProtocolHudLight();
        protocolAudio=protocolProps.AddComponent<AudioSource>();
        protocolAudio.playOnAwake=false;
        protocolChargeSound=ProtocolSound(false);
        protocolShotSound=ProtocolSound(true);
        var units=Pack(6)?.units;
        GameObject prefab=null;
        if(units!=null&&raceIndices[6].TryGetValue("h09J",out int row)&&row<units.Length/2)
            prefab=units[units.Length/2+row];
        if(prefab){
            protocolActor=Instantiate(prefab,protocolProps.transform).GetComponent<PcModel>();
            if(protocolActor){
                protocolActor.transform.position=World(new Vector2(match.ProtocolBeamX+1,0));
                protocolActor.transform.localScale*=2.5f;
                protocolActor.Face(Vector2.left,true);
                protocolActor.Play("Stand");
                DressProtocolActor();
            }
        }
        foreach(var mine in match.ProtocolMines){
            var circle=ProtocolLine("Мина",Outline(mine.position,HumanMatch.ProtocolMineRadius),.11f,true);
            protocolCircles.Add(circle);
        }
        for(int i=0;i<protocolWires.Length;i++){
            var origin=HumanMatch.ProtocolStationPoint(i);
            var target=new Vector2(match.ProtocolBeamX+2,Mathf.Sign(origin.y)*3.5f);
            SetupProtocolCable(i,origin,target);
        }
        float from=HumanMatch.ProtocolBeamStart,to=match.ProtocolBeamX,w=HumanMatch.ProtocolBeamHalfWidth;
        protocolRail=ProtocolLine("Контур залпа",new[]{new Vector2(from,-w),new Vector2(from,w),new Vector2(to,w),new Vector2(to,-w)},.23f,true);
        protocolShield=ProtocolLine("Оборонительный контур",Outline(HumanMatch.CastlePosition(1),10),.22f,true);
        protocolBeam=ProtocolLine("Плазменный залп",new[]{
            World(new Vector2(to,0))+Vector3.up*2,
            World(new Vector2(to-8,0))+Vector3.up*1.2f,
            World(new Vector2(from,0))+Vector3.up*.8f
        },1.4f,false);
        SetupProtocolArcs();
        protocolGateMaterial=new Material(shellMaterial){mainTexture=protocolFloorTile,color=new Color(.48f,.55f,.60f)};
        for(int i=0;i<protocolGates.Length;i++){
            var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name="Створка "+(i+1);
            panel.transform.SetParent(protocolProps.transform,false);
            panel.transform.localScale=new Vector3(.85f,4.1f,3.8f);
            panel.GetComponent<Renderer>().sharedMaterial=protocolGateMaterial;
            Destroy(panel.GetComponent<Collider>());
            protocolGates[i]=panel.transform;
        }
        UpdateProtocolIntro(false);
    }

    void DressProtocolActor(){
        protocolReskinnedMaterials=0;
        protocolArmorAtlas=LoadProtocolTexture("TerminatorArmor","png",TextureFormat.RGBA32,true);
        protocolChassisAtlas=LoadProtocolTexture("TerminatorChassis","png",TextureFormat.RGBA32,true);
        if(!protocolArmorAtlas||!protocolChassisAtlas)return;
        var block=new MaterialPropertyBlock();
        int main=Shader.PropertyToID("_MainTex");
        foreach(var renderer in protocolActor.GetComponentsInChildren<Renderer>(true)){
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++){
                var material=materials[i];
                if(!material||!material.name.StartsWith("h06U-",StringComparison.Ordinal)||!material.HasProperty(main))continue;
                int marker=material.name.LastIndexOf("-s",StringComparison.Ordinal);
                if(marker<0||!int.TryParse(material.name.Substring(marker+2),out int slot))continue;
                var atlas=slot<=3||slot==13?protocolArmorAtlas:
                    slot>=4&&slot<=9||slot==15||slot==16?protocolChassisAtlas:null;
                if(!atlas)continue;
                block.Clear();
                renderer.GetPropertyBlock(block,i);
                block.SetTexture(main,atlas);
                renderer.SetPropertyBlock(block,i);
                protocolReskinnedMaterials++;
            }
        }
    }

    Vector3[] Outline(Vector2 center,float radius){
        var ring=new Vector3[32];
        for(int i=0;i<ring.Length;i++){
            float angle=i*(Mathf.PI*2/ring.Length);
            ring[i]=World(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius)+Vector3.up*.24f;
        }
        return ring;
    }

    LineRenderer ProtocolLine(string name,Vector2[] positions,float width,bool loop){
        var points=new Vector3[positions.Length];
        for(int i=0;i<points.Length;i++)points[i]=World(positions[i])+Vector3.up*.22f;
        return ProtocolLine(name,points,width,loop);
    }

    LineRenderer ProtocolLine(string name,Vector3[] points,float width,bool loop){
        var line=new GameObject(name).AddComponent<LineRenderer>();
        line.transform.SetParent(protocolProps.transform,false);
        line.sharedMaterial=protocolMaterial;
        line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows=false;
        line.startWidth=line.endWidth=width;
        line.loop=loop;
        line.positionCount=points.Length;
        line.SetPositions(points);
        return line;
    }

    static void ProtocolTint(LineRenderer line,bool visible,Color color){
        if(!line)return;
        line.enabled=visible;
        if(visible)line.startColor=line.endColor=color;
    }

    void RenderProtocol(float interpolation=1){
        if(!protocolProps||!protocolProps.activeInHierarchy||match==null)return;
        for(int i=0;i<protocolCircles.Count;i++){
            var mine=match.ProtocolMines[i];
            bool armed=mine.detonatesAt>=0;
            bool lit=!mine.spent&&(protocolIntro||FogVisible(mine.position));
            ProtocolTint(protocolCircles[i],lit,armed?ProtocolGold:new Color(.73f,.47f,.22f,.4f));
        }
        for(int i=0;i<protocolWires.Length;i++){
            var station=HumanMatch.ProtocolStationPoint(i);
            bool seen=protocolIntro||FogVisible(station)&&FogVisible(HumanMatch.CastlePosition(1));
            bool powered=!match.ProtocolMarch&&match.ProtocolStationAlive(i)&&(!protocolIntro||protocolIntroAge>1.4f+i*1.2f);
            RenderProtocolCable(i,seen,powered);
        }
        bool castleVisible=protocolIntro||FogVisible(match.ProtocolMarch?match.ProtocolMarchPosition:new Vector2(match.ProtocolBeamX,0));
        if(protocolActor)protocolActor.gameObject.SetActive(castleVisible);
        if(match.ProtocolMarch){
            var center=ProtocolMarchViewPosition(interpolation);
            var lift=Vector3.up*ProtocolCastleLift();
            if(castleViews[1])castleViews[1].transform.position=World(center)+lift;
            castleObjects[1].transform.position=protocolCastleOrigin+World(center)-World(HumanMatch.CastlePosition(1))+lift;
            RenderProtocolChassis(center,lift.y);
            if(protocolActor){
                protocolActor.transform.position=World(center+new Vector2(-9,-1))+lift*.68f;
                bool moves=protocolMarchIntro?protocolMarchIntroAge>4.3f:
                    (match.ProtocolMarchPosition-match.ProtocolMarchPrevious).sqrMagnitude>.0001f;
                protocolActor.Play(match.ProtocolMarchAttacking?"Attack":moves?"Walk":"Stand",1,match.ProtocolMarchStrike);
            }
        }
        bool flash=match.ProtocolGatesOpen&&match.ProtocolOpenFor>HumanMatch.ProtocolVulnerable-.38f;
        bool warning=match.ProtocolWarningActive;
        ProtocolTint(protocolRail,warning||flash,flash?ProtocolCyan:
            new Color(1,.62f,.16f,.48f+Mathf.PingPong(Time.unscaledTime*2,.5f)));
        RenderProtocolArcs(flash);
        ProtocolTint(protocolShield,castleVisible&&!match.ProtocolMarch&&!match.ProtocolGatesOpen&&(!protocolIntro||protocolIntroAge>4.5f),
            new Color(.20f,.66f,.98f,.65f));
        float gap=protocolIntro?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.1f,5.55f,protocolIntroAge)):
            match.ProtocolGatesOpen?Mathf.Min(1,Mathf.Min(HumanMatch.ProtocolVulnerable-match.ProtocolOpenFor,match.ProtocolOpenFor)*3):0;
        for(int i=0;i<protocolGates.Length;i++){
            var gate=protocolGates[i];
            gate.gameObject.SetActive(castleVisible&&!match.ProtocolMarch);
            gate.position=World(new Vector2(match.ProtocolBeamX+3.2f,2+(i==0?-1:1)*(1.9f+gap*4)))+Vector3.up*2.05f;
        }
        if(match.ProtocolGatesOpen&&match.ProtocolOpenFor>protocolLastOpening+.2f){
            if(protocolActor)protocolActor.Play("Attack",1,match.TickCount);
            if(protocolAudio&&protocolShotSound)protocolAudio.PlayOneShot(protocolShotSound,.45f);
        }
        protocolLastOpening=match.ProtocolOpenFor;
    }

    void UpdateProtocolIntro(bool advance=true){
        if(advance){
            if(protocolIntroAge==0&&protocolAudio&&protocolChargeSound)protocolAudio.PlayOneShot(protocolChargeSound,.42f);
            protocolIntroAge+=Time.unscaledDeltaTime;
        }
        controls.InputBlocked=true;controls.EdgePan=false;
        float t=protocolIntroAge;
        UpdateProtocolGround(Mathf.InverseLerp(3.25f,9.9f,t));
        var first=World(HumanMatch.ProtocolStationPoint(0));
        var keep=World(new Vector2(match.ProtocolBeamX,0))+Vector3.up*2;
        if(t<4.2f)FrameWinterCamera(Vector3.Lerp(first,keep,Mathf.SmoothStep(0,1,t/4.2f)),38,-35,42);
        else if(t<7f)FrameWinterCamera(keep,Mathf.Lerp(28,35,(t-4.2f)/2.8f),12,49);
        else{
            float travel=Mathf.SmoothStep(0,1,Mathf.InverseLerp(7,ProtocolIntroLength,t));
            FrameWinterCamera(Vector3.Lerp(keep,World(match.Builders[0].position),travel),40+40*Mathf.Sin(travel*Mathf.PI),12*(1-travel),52);
        }
        RenderProtocol();
        if(t>=ProtocolIntroLength||advance&&Input.GetKeyDown(KeyCode.Escape))EndProtocolIntro();
    }

    void EndProtocolIntro(){
        protocolIntro=false;
        UpdateProtocolGround(1);
        if(protocolAudio)protocolAudio.Stop();
        accumulator=0;
        controls.InputBlocked=false;
        UpdateFog(true);ApplyFogViews();
        controls.FocusWorld(World(match.Builders[0].position),40);
    }

    static AudioClip ProtocolSound(bool shot){
        const int rate=22050;
        var samples=new float[rate*(shot?1:3)];
        uint noise=104729;
        for(int i=0;i<samples.Length;i++){
            float t=i/(float)rate;
            noise=unchecked(noise*1664525+1013904223);
            float grit=(noise>>16)/32768f-1;
            float fade=shot?Mathf.Exp(-7*t):Mathf.Min(1,Mathf.Min(t*8,(3-t)*1.2f));
            float tone=Mathf.Sin(2*Mathf.PI*(shot?65*t-19*t*t:48*t+3*t*t));
            samples[i]=(tone*.43f+grit*(shot?.28f:.06f))*fade;
        }
        var clip=AudioClip.Create(shot?"Залп Терминатора":"Реактор литейной",samples.Length,1,rate,false);
        clip.SetData(samples,0);
        return clip;
    }

    void DrawProtocolIntro(){
        Fill(new Rect(0,0,1280,62),new Color(0,0,0,.95f));
        Fill(new Rect(0,612,1280,108),new Color(0,0,0,.95f));
        FitBossText(new Rect(52,630,910,55),protocolIntroAge<4.2f?"Литейная оживает":
            protocolIntroAge<7?"Терминатор · оборонительный протокол запущен":"Путь к крепости открыт",new GUIStyle(heading),32);
        if(LobbyButton("protocol.skip",new Rect(1061,642,167,42),"Пропустить"))EndProtocolIntro();
    }

    void DrawProtocolHud(){
        if(protocolHudTitle==null){
            protocolHudTitle=new GUIStyle(hudText){alignment=TextAnchor.MiddleLeft,padding=new RectOffset(),
                normal={textColor=new Color(.98f,.88f,.70f)}};
            protocolHudDetail=new GUIStyle(hudSmall){alignment=TextAnchor.MiddleLeft,padding=new RectOffset(),
                normal={textColor=new Color(.85f,.91f,.93f)}};
        }
        if(protocolHudFrame)GUI.DrawTexture(ProtocolHud,protocolHudFrame,ScaleMode.StretchToFill,true);
        else{
            Fill(ProtocolHud,new Color(.075f,.063f,.042f,.96f));
            Border(ProtocolHud,ProtocolGold,1);
        }
        var previous=GUI.color;
        int stationMask=SpectatorSceneReady?(int?)spectatorBoss?["mask"]??0:
            (match.ProtocolStationAlive(0)?1:0)|(match.ProtocolStationAlive(1)?2:0);
        for(int i=0;i<2;i++){
            if((stationMask&(1<<i))==0)continue;
            var lamp=new Rect(ProtocolHud.x+29+i*16,ProtocolHud.y+53,14,14);
            GUI.color=ProtocolCyan;
            if(protocolHudLight)GUI.DrawTexture(lamp,protocolHudLight);
        }
        GUI.color=previous;
        float x=ProtocolHud.x+83,y=ProtocolHud.y;
        FitBossText(new Rect(x,y+21,181,23),"Последний протокол",protocolHudTitle,17);
        bool march=SpectatorSceneReady?(bool?)spectatorBoss?["march"]==true:match.ProtocolMarch;
        bool second=SpectatorSceneReady?(bool?)spectatorBoss?["second"]==true:match.ProtocolSecondStage;
        bool open=SpectatorSceneReady?(bool?)spectatorBoss?["open"]==true:match.ProtocolGatesOpen;
        bool warning=SpectatorSceneReady?(bool?)spectatorBoss?["warning"]==true:match.ProtocolWarningActive;
        float? left=SpectatorSceneReady?(float?)spectatorBoss?["left"]:match.ProtocolTimeLeft;
        float openFor=SpectatorSceneReady?(float?)spectatorBoss?["open_for"]??0:match.ProtocolOpenFor;
        float shot=SpectatorSceneReady?(float?)spectatorBoss?["shot"]??0:match.ProtocolShotIn;
        int stations=(stationMask&1)+(stationMask>>1&1);
        if(!march&&left.HasValue){
            int seconds=Mathf.CeilToInt(Mathf.Max(0,left.Value));
            FitBossText(new Rect(x+184,y+21,43,23),(seconds/60)+":"+(seconds%60).ToString("00"),protocolHudDetail,16);
        }
        string signal=march?"Терминатор наступает":!left.HasValue?"Оборона технодрома":!second?
            "Электростанции "+stations+"/2 · "+(stations*35)+"% защиты":
            open?"Ворота открыты · "+Mathf.CeilToInt(openFor)+" с":
            (warning?"Залп через ":"Орудие заряжается · ")+Mathf.CeilToInt(shot)+" с";
        FitBossText(new Rect(x,y+42,228,21),signal,protocolHudDetail,14);
        float maxHp=SpectatorSceneReady?HumanMatch.F(spectatorChallenge,"opponent_castle_hp",HumanMatch.CastleMax):match.CastleHealthLimit(1);
        float meter=march?match.CastleHp[1]/maxHp:!left.HasValue?0:
            open?openFor/HumanMatch.ProtocolVulnerable:
            second?1-shot/(warning?HumanMatch.ProtocolWarning:HumanMatch.ProtocolCycle):(2-stations)*.5f;
        var glow=warning?ProtocolGold:ProtocolCyan;
        Fill(new Rect(ProtocolHud.x+94,y+71,206*Mathf.Clamp01(meter),3),glow);
    }

    static Texture2D MakeProtocolHudLight(){
        const int size=20;
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            float l=(new Vector2(x+.5f,y+.5f)-Vector2.one*(size*.5f)).magnitude/(size*.5f);
            float power=1-Mathf.SmoothStep(0,1,l);
            pixels[y*size+x]=new Color(1,1,1,power);
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
        texture.SetPixels32(pixels);texture.Apply(false,true);
        return texture;
    }

    void ClearProtocol(){
        protocolIntro=false;
        ClearProtocolMarchPresentation();
        ClearProtocolGround();
        if(protocolProps){
            if(castleObjects[1])castleObjects[1].transform.position=protocolCastleOrigin;
            Destroy(protocolProps);
        }
        if(protocolMaterial)Destroy(protocolMaterial);
        if(protocolGateMaterial)Destroy(protocolGateMaterial);
        if(protocolChargeSound)Destroy(protocolChargeSound);
        if(protocolShotSound)Destroy(protocolShotSound);
        if(protocolArmorAtlas)Destroy(protocolArmorAtlas);
        if(protocolChassisAtlas)Destroy(protocolChassisAtlas);
        if(protocolHudFrame)Destroy(protocolHudFrame);
        if(protocolHudLight)Destroy(protocolHudLight);
        protocolProps=null;protocolMaterial=null;protocolActor=null;protocolAudio=null;
        protocolGateMaterial=null;protocolChargeSound=protocolShotSound=null;protocolRail=protocolShield=protocolBeam=null;
        protocolArmorAtlas=protocolChassisAtlas=null;
        protocolHudFrame=protocolHudLight=null;
        protocolReskinnedMaterials=0;
        protocolCircles.Clear();
        Array.Clear(protocolWires,0,protocolWires.Length);
        Array.Clear(protocolGates,0,protocolGates.Length);
        Array.Clear(protocolCableGlow,0,protocolCableGlow.Length);
        Array.Clear(protocolCablePulse,0,protocolCablePulse.Length);
        Array.Clear(protocolArcs,0,protocolArcs.Length);
    }
}
}
