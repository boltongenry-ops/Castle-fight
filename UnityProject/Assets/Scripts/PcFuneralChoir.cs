using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace CastleFight {
public sealed partial class PcGame {
    static JObject ChoirDefinition()=>new JObject{
        ["id"]=HumanMatch.ChoirTrial,["title"]="Погребальный хор",["opponent"]="Undead Forces",
        ["summary"]="Восстание мёртвых и охота банши.",
        ["description"]="На 4:00 начинается Восстание. Из могил в дымке выходят гули. После подготовки десятина отнимает 6% текущего здоровья бойцов каждые 15 секунд. По призрачному некрополю промахиваются 20% атак. При 50% здоровья босса могилы откроются у вашего замка. Убейте банши за 12 секунд, иначе боец станет вражеским скелетом.",
        ["start_gold"]=500,["difficulty"]=2,["xp"]=0,["reward"]=0
    };
    static readonly string[] ChoirTracks={"ChoirVerse","ChoirFirst","ChoirSecond","ChoirWail","ChoirRitual","ChoirToll"};
    readonly AudioClip[] choirClips=new AudioClip[6];
    readonly bool[] choirOwned=new bool[6];
    readonly List<GameObject> choirGraves=new List<GameObject>();
    Texture2D choirPainting;
    AudioSource choirMusic;
    PcModel choirQueen,choirGolem;
    GameObject sacrificeOrb;
    ClassicProjectileModel sacrificeMissile;
    readonly List<LineRenderer> choirFaults=new List<LineRenderer>();
    readonly List<Vector3[]> choirFaultPoints=new List<Vector3[]>();
    Material choirFaultMaterial;
    GUIStyle whisperStyle;
    static readonly string[] firstWhisper={"To","quench","this","earthly","spark"};
    static readonly string[] secondWhisper={"So","the","five","monks","knelt","in","fear"};
    static readonly float[] firstWhisperTimes={2f,2.16f,2.46f,2.74f,3.08f};
    static readonly float[] secondWhisperTimes={4f,4.28f,4.48f,4.88f,5.36f,5.84f,6.24f};
    Light choirSun;
    float daylight;
    Color dayAmbient,daySun;
    bool choirHasLighting,choirCast,choirDeath;
    const float SacrificeTime=4.25f,MusicEntrance=9.4f,ChoirIntroLength=14f;
    bool choirIntro,choirVersus,gravesShown;
    float choirIntroAge;
    int choirTrack=-1;
    HumanMatch.ChoirMovement choirLastMovement;
    void DrawChoirPainting(Rect r){
        if(!choirPainting){
            var embedded=Resources.Load<TextAsset>("WinterBattle/FuneralChoir");
            string path=MobileContent.Media("Frontend/FuneralChoir.jpg");
            byte[] data=embedded?embedded.bytes:File.Exists(path)?File.ReadAllBytes(path):null;
            if(data!=null){
                choirPainting=new Texture2D(2,2,TextureFormat.RGB24,false);
                choirPainting.LoadImage(data,true);
            }
        }
        Fill(r,new Color(.025f,.06f,.045f));
        if(!choirPainting)return;
        float h=Mathf.Min(1,r.height*choirPainting.width/(r.width*choirPainting.height));
        float w=Mathf.Min(1,r.width*choirPainting.height/(r.height*choirPainting.width));
        GUI.DrawTextureWithTexCoords(r,choirPainting,new Rect((1-w)*.8f,(1-h)*.78f,w,h));
    }
    IEnumerator PrepareChoirMusic(int generation){
        for(int i=0;i<ChoirTracks.Length;i++){
            var load=Resources.LoadAsync<AudioClip>("WinterBattle/"+ChoirTracks[i]);yield return load;
            if(generation!=versusSerial)yield break;
            choirClips[i]=load.asset as AudioClip;
            if(choirClips[i]||Application.isMobilePlatform)continue;
            string path=MobileContent.Media("Frontend/"+ChoirTracks[i]+".ogg");
            if(!File.Exists(path))continue;
            using(var req=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,AudioType.OGGVORBIS)){
                yield return req.SendWebRequest();
                if(generation!=versusSerial)yield break;
                if(req.result==UnityWebRequest.Result.Success){
                    choirClips[i]=DownloadHandlerAudioClip.GetContent(req);choirOwned[i]=true;
                }
            }
        }
    }
    LineRenderer bellBolt;
    int lastChoirToll;
    float boltAt=-10;
    void StrikeChoirBell(Vector3 target){
        boltAt=Time.unscaledTime;
        if(!bellBolt){
            bellBolt=new GameObject("Некрополь — разряд колокола").AddComponent<LineRenderer>();
            bellBolt.transform.SetParent(visuals.transform,false);bellBolt.sharedMaterial=impactMaterial;
            bellBolt.positionCount=13;bellBolt.startWidth=.33f;bellBolt.endWidth=.12f;
            bellBolt.startColor=Color.white;bellBolt.endColor=new Color(.62f,1,.81f);
        }
        bellBolt.enabled=true;
        for(int i=0;i<13;i++){
            float f=i/12f;
            var jitter=Mathf.Sin(f*Mathf.PI)*new Vector3(Mathf.Sin(i*1.89f+lastChoirToll)*2,0,Mathf.Cos(i*3.1f)*1.3f);
            bellBolt.SetPosition(i,target+new Vector3(-6*f,29*f,4*f)+jitter);
        }
    }
    void DrawBellFlash(){
        float age=Time.unscaledTime-boltAt;
        if(age>.20f)return;
        Fill(new Rect(-guiOrigin.x/guiScale,-guiOrigin.y/guiScale,Screen.width/guiScale,Screen.height/guiScale),
            new Color(.80f,.95f,.91f,.38f*(1-Mathf.Clamp01(age/.20f))));
    }
    void BeginChoirIntro(){
        lastChoirToll=0;boltAt=-10;
        choirIntro=true;choirIntroAge=0;choirTrack=-1;gravesShown=false;
        choirMusic=visuals.AddComponent<AudioSource>();choirMusic.playOnAwake=false;choirMusic.spatialBlend=0;
        Vector2 keep=HumanMatch.CastlePosition(1);
        choirQueen=RitualActor("u00D",keep+new Vector2(-8,5),Vector2.left);
        choirGolem=RitualActor("n00E",keep+new Vector2(-15,4),Vector2.right);
        choirCast=choirDeath=false;
        var at=choirGolem?choirGolem.transform.position:World(keep+new Vector2(-15,4));
        GatherRitualGround(at);
        choirSun=FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional&&l.isActiveAndEnabled&&(l.cullingMask&1)!=0);
        dayAmbient=RenderSettings.ambientLight;choirHasLighting=true;
        if(choirSun){daylight=choirSun.intensity;daySun=choirSun.color;}
        var missile=ClassicProjectile("fx_e67f4eb60198",1); // WC3 DeathCoilMissile
        if(missile){
            sacrificeOrb=Instantiate(missile,visuals.transform);
            sacrificeMissile=sacrificeOrb.GetComponent<ClassicProjectileModel>();
            sacrificeOrb.SetActive(false);
        }
        controls.InputBlocked=true;controls.GameplayViewport(false);
    }
    PcModel RitualActor(string raw,Vector2 point,Vector2 look){
        int kind=Array.FindIndex(match.Lines,l=>l.unit==raw);
        var prefab=kind>=0?RaceModel(1,kind,false):null;if(!prefab)return null;
        var actor=Instantiate(prefab,visuals.transform).GetComponent<PcModel>();
        actor.transform.position=World(point);actor.Face(look,true);actor.Play("Stand");return actor;
    }
    void ChoirNight(float night){
        RenderSettings.ambientLight=Color.Lerp(dayAmbient,new Color(.25f,.27f,.33f),night);
        if(choirSun){
            choirSun.intensity=daylight*Mathf.Lerp(1,.53f,night);
            choirSun.color=Color.Lerp(daySun,new Color(.66f,.74f,.90f),night);
        }
    }
    void ChoirTrack(int index,float time,bool loop){
        choirTrack=index;if(!choirMusic||!choirClips[index])return;
        choirMusic.Stop();choirMusic.clip=choirClips[index];choirMusic.loop=loop;
        choirMusic.time=Mathf.Repeat(time,choirMusic.clip.length);choirMusic.Play();
    }
    void UpdateChoirIntro(){
        choirIntroAge+=testing&&Time.captureFramerate>0?Time.deltaTime:Time.unscaledDeltaTime;
        float t=choirIntroAge;
        if(choirTrack<0)ChoirTrack(4,0,false);
        var keep=World(HumanMatch.CastlePosition(1))+Vector3.up*3;
        var lich=choirQueen?choirQueen.transform.position+Vector3.up*2.6f:keep;
        var heart=choirGolem?choirGolem.transform.position+Vector3.up*2.4f:keep;
        var pair=Vector3.Lerp(lich,heart,.5f);
        if(t>=1.65f&&!choirCast){
            choirCast=true;if(choirQueen)choirQueen.Play("Spell",.8f,1);
        }
        if(t>=SacrificeTime&&!choirDeath){
            StrikeChoirBell(heart);choirDeath=true;if(choirGolem)choirGolem.Play("Death",1,1);
        }
        if(sacrificeOrb&&t>=1.65f&&t<4.85f){
            float fly=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.8f,SacrificeTime,t));
            sacrificeOrb.SetActive(true);sacrificeOrb.transform.position=Vector3.Lerp(lich,heart,fly)+Vector3.up*(.55f+Mathf.Sin(fly*Mathf.PI));
            float radius=t<SacrificeTime?Mathf.Lerp(.12f,1.55f,Mathf.Clamp01((t-1.65f)/1.2f)):1.55f*(1-Mathf.InverseLerp(SacrificeTime,4.65f,t));
            sacrificeOrb.transform.localScale=Vector3.one*Mathf.Max(.55f,radius)*1.7f;
            sacrificeMissile.Face(heart-lich);sacrificeMissile.Play(t>=SacrificeTime);
        }else if(sacrificeOrb)sacrificeOrb.SetActive(false);
        float faultAge=t-SacrificeTime-.38f;
        if(faultAge>=0&&t<MusicEntrance){
            if(choirFaults.Count==0)OpenChoirFaults(heart);
            AdvanceChoirFaults(faultAge);
        }
        float spread=Mathf.InverseLerp(SacrificeTime+.6f,9.2f,t);
        SpreadRitualGround(spread);ChoirNight(Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.35f,9.4f,t)));
        if(t<2.8f)FrameWinterCamera(Vector3.Lerp(keep,pair,Mathf.SmoothStep(0,1,t/2.8f)),39-t*4,-14+t*3,37);
        else if(t<5.55f)FrameWinterCamera(pair,20.5f,9+t,32);
        else if(t<MusicEntrance)FrameWinterCamera(Vector3.Lerp(heart,keep,.23f),26+(t-5.55f)*4,-24+(t-5.55f)*6,48);
        else{
            if(choirTrack!=0){ChoirTrack(0,0,true);choirMusic.volume=.68f;}
            float travel=Mathf.SmoothStep(0,1,Mathf.InverseLerp(MusicEntrance,ChoirIntroLength,t));
            FrameWinterCamera(Vector3.Lerp(heart,World(match.Builders[0].position),travel),37+19*Mathf.Sin(travel*Mathf.PI),12*(1-travel),48);
        }
        if(bellBolt&&Time.unscaledTime-boltAt>.16f)bellBolt.enabled=false;
        RenderMatch();
        if(t<ChoirIntroLength&&!Input.GetKeyDown(KeyCode.Escape))return;
        SpreadRitualGround(1);ChoirNight(1);
        choirIntro=false;if(choirQueen)Destroy(choirQueen.gameObject);if(choirGolem)Destroy(choirGolem.gameObject);
        choirQueen=choirGolem=null;Destroy(sacrificeOrb);sacrificeOrb=null;ClearChoirFaults();
        UpdateFog(true);ApplyFogViews();controls.InputBlocked=false;controls.FocusWorld(World(match.Builders[0].position),40);
        if(choirTrack!=0)ChoirTrack(0,0,true);accumulator=0;
    }
    void DrawChoirIntro(){
        DrawBellFlash();
        Fill(new Rect(0,0,1280,50),Color.black);Fill(new Rect(0,633,1280,87),Color.black);
        Text(48,645,710,49,"Погребальный хор",heading);
        if(LobbyButton("choir.skip",new Rect(1000,648,227,46),"Пропустить"))choirIntroAge=ChoirIntroLength;
    }
    const float GraveFogRadius=600f/30f;
    readonly List<GameObject> graveFogZones=new List<GameObject>();
    void ShowGraveFog(Vector2 site){
        if(!graveMist)return;
        var fog=Instantiate(graveMist,visuals.transform);
        fog.name="Дымка над могилой";
        fog.transform.position=World(site)+Vector3.up*.45f;
        fog.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=fog.main;main.startSize=new ParticleSystem.MinMaxCurve(7,10);
        main.maxParticles=Application.isMobilePlatform?16:26;
        var shape=fog.shape;shape.radius=GraveFogRadius-5;shape.radiusThickness=1;shape.scale=new Vector3(1,.045f,1);
        var emission=fog.emission;emission.rateOverTime=main.maxParticles/4f;
        fog.Play();fog.Emit(main.maxParticles);
        // Ground veil fills the entire footprint immediately under the drifting wisps.
        var veil=GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(veil.GetComponent<Collider>());
        veil.transform.SetParent(fog.transform,false);veil.transform.localRotation=Quaternion.Euler(90,0,0);
        veil.transform.localScale=new Vector3(2*GraveFogRadius,2*GraveFogRadius,1);
        var draw=veil.GetComponent<Renderer>();draw.sharedMaterial=graveMistMaterial;
        var color=new MaterialPropertyBlock();color.SetColor("_Color",new Color(.47f,.77f,.60f,.88f));draw.SetPropertyBlock(color);
        graveFogZones.Add(fog.gameObject);
    }
    void ClearChoirGraves(){foreach(var fog in graveFogZones)if(fog)Destroy(fog);graveFogZones.Clear();foreach(var obj in choirGraves)if(obj)Destroy(obj);choirGraves.Clear();gravesShown=false;}
    void UpdateChoirBattle(){
        if(match.ChoirTolls!=lastChoirToll){
            lastChoirToll=match.ChoirTolls;
            StrikeChoirBell(World(HumanMatch.CastlePosition(1))+Vector3.up*6);
            if(choirMusic&&choirClips[5])choirMusic.PlayOneShot(choirClips[5],.95f);
        }
        if(bellBolt&&(Time.unscaledTime-boltAt>.16f||!FogVisible(HumanMatch.CastlePosition(1))))bellBolt.enabled=false;
        var phase=match.Movement;
        int track=phase==HumanMatch.ChoirMovement.First?1:phase==HumanMatch.ChoirMovement.Second?2:phase==HumanMatch.ChoirMovement.Wail?3:0;
        if(track!=choirTrack||phase!=choirLastMovement)ChoirTrack(track,match.MovementAge,track==0);
        choirLastMovement=phase;
        if(choirMusic){
            choirMusic.volume=track==0?.52f:track==3?.8f:match.MovementAge<HumanMatch.ChoirLead?.62f:1;
            if(page==Page.Match){
                if(!choirMusic.isPlaying&&choirMusic.clip)choirMusic.UnPause();
            }else choirMusic.Pause();
        }
        if(match.ChoirRevealed&&!gravesShown){
            gravesShown=true;
            var prefab=RaceModel(1,match.Kind("h01P"),true);
            foreach(var position in match.GraveSites){
                ShowGraveFog(position);
                if(!prefab)continue;
                var grave=Instantiate(prefab,visuals.transform);grave.transform.position=World(position);
                grave.transform.localScale*=.35f;choirGraves.Add(grave);
            }
        }else if(!match.ChoirRevealed&&gravesShown)ClearChoirGraves();
        if(gravesShown){
            for(int i=0;i<graveFogZones.Count;i++){
                bool visible=FogVisible(match.GraveSites[i]);
                if(graveFogZones[i].activeSelf!=visible)graveFogZones[i].SetActive(visible);
            }
            float t=match.MovementAge-HumanMatch.ChoirLead;
            if(t<1.2f){
                float rise=Mathf.SmoothStep(0,1,t/1.2f);
                for(int i=0;i<choirGraves.Count;i++)
                    choirGraves[i].transform.position=World(match.GraveSites[i])+new Vector3(Mathf.Sin(34*t+i)*.22f*(1-rise),-1.3f*(1-rise),0);
            }
        }
    }
    void DrawChoirHud(){
        DrawBellFlash();
        float t=match.MovementAge-HumanMatch.ChoirLead;
        bool chorus=match.Movement==HumanMatch.ChoirMovement.First||match.Movement==HumanMatch.ChoirMovement.Second;
        string caption="Могильная десятина",signal="До удара колокола";
        float left=match.TollIn,span=8*HumanMatch.ChoirBar;
        if(match.Movement==HumanMatch.ChoirMovement.Verse){
            caption="Подготовка";signal="До восстания";span=HumanMatch.ChoirPreparation;left=span-match.Elapsed;
        }else if(chorus){
            bool heavy=match.Movement==HumanMatch.ChoirMovement.Second;
            caption=t<0?"Шёпот под землёй":heavy?"Прорыв у замка":"Открытые могилы";
            signal=t<0?(heavy?"Трещины у трона":"Трещины у передовой"):"До конца восстания";
            left=t<0?-t:HumanMatch.ChoirPass-match.MovementAge;
            span=t<0?HumanMatch.ChoirLead:HumanMatch.ChoirPass-HumanMatch.ChoirLead;
        }else if(match.Movement==HumanMatch.ChoirMovement.Wail){
            caption="Плач банши";signal="До погребения";span=12;left=span-match.MovementAge;
        }
        DrawBossMeter(false,caption,signal,left,span);
        if(chorus){
            bool second=match.Movement==HumanMatch.ChoirMovement.Second;
            float clock=choirMusic&&choirMusic.clip==choirClips[second?2:1] ? choirMusic.time : match.MovementAge;
            DrawChoirWhisper(second,clock);
        }
    }
    Transform spectralKeep;
    ParticleSystem graveMist;
    Texture2D graveCloud;
    Material graveMistMaterial;
    readonly Dictionary<Renderer,Material[]> spectralOriginals=new Dictionary<Renderer,Material[]>();
    readonly Dictionary<Material,Material> spectralMats=new Dictionary<Material,Material>();
    float spectralPower=-1;

    void RenderChoirCastle(){
        float power=choirIntro?Mathf.SmoothStep(0,1,Mathf.InverseLerp(SacrificeTime+.6f,8.2f,choirIntroAge)):1;
        if(power<=0)return;
        var keep=castleViews[1]?castleViews[1].transform:castleObjects[1].transform;
        if(spectralKeep!=keep){
            ClearChoirCastle();
            spectralKeep=keep;
            foreach(var renderer in keep.GetComponentsInChildren<Renderer>(true)){
                var original=renderer.sharedMaterials;
                Material[] replacements=null;
                for(int i=0;i<original.Length;i++){
                    var source=original[i];
                    if(!source||source.shader.name!="CastleFight/NativeMapProp")continue;
                    if(!spectralMats.TryGetValue(source,out var ghost)){
                        ghost=new Material(source);
                        ghost.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        ghost.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        // Keep the keep's native depth writers: the courtyard otherwise paints over the front towers.
                        ghost.renderQueue=3000+Mathf.Clamp(source.renderQueue-2450,-32,32);
                        spectralMats.Add(source,ghost);
                    }
                    if(replacements==null)replacements=(Material[])original.Clone();
                    replacements[i]=ghost;
                }
                if(replacements!=null){spectralOriginals.Add(renderer,original);renderer.sharedMaterials=replacements;}
            }
            CreateGraveMist();
        }
        if(!Mathf.Approximately(power,spectralPower)){
            spectralPower=power;
            foreach(var pair in spectralMats){
                pair.Value.SetColor("_Color",Color.Lerp(pair.Key.GetColor("_Color"),new Color(.53f,1.08f,.86f,.51f),power));
                pair.Value.SetColor("_TeamColor",Color.Lerp(pair.Key.GetColor("_TeamColor"),new Color(.28f,.78f,.57f),power));
                pair.Value.SetFloat("_Unlit",Mathf.Lerp(pair.Key.GetFloat("_Unlit"),.78f,power));
            }
            var emission=graveMist.emission;emission.rateOverTime=power*(Application.isMobilePlatform?5:9);
        }
        var cam=Camera.main;
        bool visible=keep.gameObject.activeInHierarchy&&(choirIntro||FogVisible(HumanMatch.CastlePosition(1)))&&cam&&(cam.transform.position-keep.position).sqrMagnitude<10000;
        if(visible&&!graveMist.isPlaying)graveMist.Play();
        if(!visible&&graveMist.isPlaying)graveMist.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    void CreateGraveMist(){
        const int side=48;
        graveCloud=new Texture2D(side,side,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color32[side*side];
        for(int y=0;y<side;y++)for(int x=0;x<side;x++){
            float u=(x+.5f)/side,v=(y+.5f)/side;
            float edge=Mathf.Clamp01(1-2*Vector2.Distance(new Vector2(u,v),new Vector2(.5f,.5f)));
            float smoke=Mathf.PerlinNoise(6*u,6*v)*.7f+Mathf.PerlinNoise(13*u,13*v)*.3f;
            pixels[y*side+x]=new Color(1,1,1,edge*edge*smoke);
        }
        graveCloud.SetPixels32(pixels);graveCloud.Apply(false,true);
        graveMistMaterial=new Material(Shader.Find("Sprites/Default")){mainTexture=graveCloud,renderQueue=3010};
        var mist=new GameObject("Могильная дымка");mist.transform.SetParent(visuals.transform,false);
        mist.transform.position=World(HumanMatch.CastlePosition(1))+Vector3.up*.8f;
        graveMist=mist.AddComponent<ParticleSystem>();
        graveMist.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=graveMist.main;main.playOnAwake=false;main.startLifetime=new ParticleSystem.MinMaxCurve(3f,5f);
        main.startSize=new ParticleSystem.MinMaxCurve(6,10);main.startSpeed=0;main.startRotation=new ParticleSystem.MinMaxCurve(-3,3);
        main.startColor=new Color(.50f,.89f,.74f,.87f);main.maxParticles=Application.isMobilePlatform?24:46;
        var shape=graveMist.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=7;shape.radiusThickness=.27f;shape.scale=new Vector3(1,.15f,1);
        var velocity=graveMist.velocityOverLifetime;velocity.enabled=true;velocity.orbitalY=.24f;velocity.y=.52f;
        var life=graveMist.colorOverLifetime;life.enabled=true;
        var fade=new Gradient();fade.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.24f),new GradientAlphaKey(.8f,.62f),new GradientAlphaKey(0,1)});life.color=fade;
        graveMist.GetComponent<ParticleSystemRenderer>().sharedMaterial=graveMistMaterial;
    }
    void ClearChoirCastle(){
        foreach(var pair in spectralOriginals)if(pair.Key)pair.Key.sharedMaterials=pair.Value;
        foreach(var ghost in spectralMats.Values)if(ghost)Destroy(ghost);
        spectralOriginals.Clear();spectralMats.Clear();
        if(graveMist)Destroy(graveMist.gameObject);
        if(graveMistMaterial)Destroy(graveMistMaterial);
        if(graveCloud)Destroy(graveCloud);
        spectralKeep=null;graveMist=null;spectralPower=-1;
    }
    void DrawChoirWhisper(bool second,float clock){
        var times=second?secondWhisperTimes:firstWhisperTimes;
        float fade=Mathf.SmoothStep(0,1,clock-times[0]+1);
        if(fade<=0||clock>=HumanMatch.ChoirLead+.85f)return;
        // Extend into the letterbox area on ultrawide phones.
        var screen=new Rect(-guiOrigin.x/guiScale,-guiOrigin.y/guiScale,Screen.width/guiScale,Screen.height/guiScale);
        if(clock>=HumanMatch.ChoirLead){
            Fill(screen,new Color(.19f,.86f,.32f,.24f*(1-(clock-HumanMatch.ChoirLead)/.85f)));
            return;
        }
        Fill(screen,new Color(.007f,.015f,.012f,fade*.97f));
        if(clock<times[0])return;
        if(whisperStyle==null)whisperStyle=new GUIStyle(heading){fontSize=46,fontStyle=FontStyle.Italic,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(),clipping=TextClipping.Overflow};
        var words=second?secondWhisper:firstWhisper;
        float total=(words.Length-1)*14;
        foreach(var word in words)total+=whisperStyle.CalcSize(new GUIContent(word)).x;
        float x=(1280-total)*.5f;
        for(int i=0;i<words.Length;i++){
            if(clock<times[i])break;
            float appear=Mathf.SmoothStep(0,1,(clock-times[i])/.12f);
            float w=whisperStyle.CalcSize(new GUIContent(words[i])).x;
            float y=319+6*(1-appear);
            whisperStyle.normal.textColor=new Color(.12f,.55f,.32f,.56f*appear);
            Text(x+2,y+3,w+5,74,words[i],whisperStyle);
            whisperStyle.normal.textColor=new Color(.83f,.94f,.81f,appear);
            Text(x,y,w+5,74,words[i],whisperStyle);
            x+=w+14;
        }
    }
    void OpenChoirFaults(Vector3 center){
        choirFaultMaterial=new Material(Shader.Find("Sprites/Default"));
        var rng=new System.Random(145);
        var origin=new Vector2(center.x,-center.z);
        for(int ray=0;ray<8;ray++){
            float angle=(ray+(float)rng.NextDouble()*.38f)*Mathf.PI/4;
            Vector2 p=origin;
            var points=new Vector3[9];
            for(int k=0;k<points.Length;k++){
                points[k]=World(p)+Vector3.up*.14f;
                float turn=angle+((float)rng.NextDouble()-.5f)*.91f;
                p+=new Vector2(Mathf.Cos(turn),Mathf.Sin(turn))*(1.6f+(float)rng.NextDouble());
            }
            foreach(bool core in new[]{false,true}){
                var line=new GameObject(core?"Fel seam":"Broken soil").AddComponent<LineRenderer>();
                line.transform.SetParent(visuals.transform,false);line.sharedMaterial=choirFaultMaterial;
                line.useWorldSpace=true;line.alignment=LineAlignment.TransformZ;
                line.transform.rotation=Quaternion.Euler(90,0,0);
                line.startWidth=core?.09f:.52f;line.endWidth=core?.025f:.10f;
                var path=(Vector3[])points.Clone();
                if(core)for(int k=0;k<path.Length;k++)path[k].y+=.018f;
                line.positionCount=0;choirFaults.Add(line);choirFaultPoints.Add(path);
            }
        }
    }
    void AdvanceChoirFaults(float age){
        for(int i=0;i<choirFaults.Count;i++){
            var path=choirFaultPoints[i];var line=choirFaults[i];
            float front=Mathf.Clamp(age*(4.8f+i/2%3),0,path.Length-1);
            int tip=Mathf.Min((int)front+1,path.Length-1);
            line.positionCount=tip+1;
            for(int j=0;j<tip;j++)line.SetPosition(j,path[j]);
            line.SetPosition(tip,Vector3.Lerp(path[tip-1],path[tip],front-tip+1));
            bool core=(i&1)!=0;
            var color=core?new Color(.39f,.91f,.30f):new Color(.067f,.036f,.024f);
            color.a=1-Mathf.InverseLerp(core?2.4f:3.4f,4.65f,age);
            line.startColor=line.endColor=color;
        }
    }
    void ClearChoirFaults(){
        foreach(var line in choirFaults)if(line)Destroy(line.gameObject);
        choirFaults.Clear();choirFaultPoints.Clear();
        if(choirFaultMaterial)Destroy(choirFaultMaterial);
        choirFaultMaterial=null;
    }
    void ClearChoir(){
        if(bellBolt)Destroy(bellBolt.gameObject);bellBolt=null;boltAt=-10;
        if(choirHasLighting){ChoirNight(0);choirHasLighting=false;}
        ClearChoirFaults();
        ClearChoirGraves();
        if(choirMusic){choirMusic.Stop();choirMusic.clip=null;}
        for(int i=0;i<choirClips.Length;i++){
            if(choirOwned[i]&&choirClips[i])Destroy(choirClips[i]);
            choirOwned[i]=false;choirClips[i]=null;
        }
        choirMusic=null;choirQueen=choirGolem=null;choirIntro=false;choirTrack=-1;
    }
}
}
