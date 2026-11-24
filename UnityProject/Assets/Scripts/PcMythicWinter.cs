using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    const int NorthernOpponent=5;
    const float WinterIntroDuration=11.4f, WinterOpeningDistance=35, WinterStormCue=2.5f;
    const float WinterCastleScale=1.11f;
    static readonly Color WinterBlue=new Color(.61f,.84f,.97f);
    bool mythicTab,winterIntro,winterSkip,winterCast;
    JObject mythicSelection;
    bool MythicRacePicker=>mythicSelection!=null;
    int mythicRace;
    float winterIntroAge;
    PcModel winterQueen;
    ParticleSystem winterGusts;
    ParticleSystem winterCastleAura;
    Transform winterCastleBody;
    Vector3 winterCastleBaseScale;
    readonly List<(Renderer renderer,MaterialPropertyBlock original)> winterCastleTint=
        new List<(Renderer,MaterialPropertyBlock)>();
    bool winterStrongGusts;
    Material winterGustMaterial;
    Texture2D winterGustTexture,winterMinimap;
    readonly Dictionary<Texture,Texture2D> winterAtlases=new Dictionary<Texture,Texture2D>();
    readonly List<(MeshRenderer renderer,MaterialPropertyBlock original)> winterOverrides=
        new List<(MeshRenderer,MaterialPropertyBlock)>();
    MeshRenderer[] winterGround;
    int winterGroundIndex;

    static JObject WinterTrialDefinition()=>new JObject{
        ["id"]=HumanMatch.WinterTrial,["title"]="Вечная зима",["opponent"]="Northern Realms",
        ["description"]="Ледяная королева накрывает поле боя метелью. Ваши бойцы замедлены, а каждые 10 секунд одного сковывает лёд. При 2500 HP замка босса павшие в оковах переходят к Северянам.",
        ["start_gold"]=500,["difficulty"]=2,["reward"]=0,["xp"]=0
    };

    void ChooseMythicRace(JObject trial){
        mythicSelection=trial;
        mythicRace=RaceStaged(raceChoice)?raceChoice:0;
    }

    void DrawMythicRacePicker(){
        var card=new Rect(42,176,1196,500);
        DrawWinterPainting(card,true);
        Fill(new Rect(42,176,641,500),new Color(.025f,.047f,.065f,.99f));
        WinterGradient(new Rect(683,176,282,500),true,new Color(.025f,.047f,.065f,.98f));
        WinterGradient(new Rect(682,337,556,339),false,new Color(.019f,.035f,.052f,.99f));
        Border(card,WinterLine,1);
        Text(66,194,700,49,(string)mythicSelection["title"],raceTitleStyle);
        Text(69,249,705,31,"Выберите свою обычную расу",label);
        for(int i=0;i<AllRaceUnity.Length;i++){
            int race=AllRaceUnity[i];
            var cell=new Rect(70+i%3*207,294+i/3*54,197,47);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&RaceStaged(race);
            if(LobbyButton("mythic.race."+race,cell,"",race==mythicRace))mythicRace=race;
            var emblem=RaceEmblem(race);
            if(emblem)GUI.DrawTexture(new Rect(cell.x+5,cell.y+5,36,36),emblem,ScaleMode.ScaleToFit);
            Text(cell.x+43,cell.y+2,150,43,AllRaceNames[i],
                new GUIStyle(small){fontSize=13,wordWrap=true,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(),
                    normal={textColor=race==mythicRace?WinterBlue:Color.white}});
            GUI.enabled=enabled;
        }
        const float x=718,width=493;
        Fill(new Rect(x-12,389,width+22,217),new Color(.023f,.042f,.061f,.91f));
        Text(x,389,width,41,"Ледяная королева",heading);
        Text(x,431,width,32,"Движение −50%  •  Скорость атаки −30%",label);
        Text(x,468,width,28,"Оковы одного бойца каждые 10 с",label);
        Text(x,501,width,28,"Стадия II · "+HumanMatch.WinterPhaseTwoHp+" HP замка босса",label);
        Text(x,534,width,57,"Павшие в оковах переходят к боссу.\nСеверяне невосприимчивы к метели и оковам.",small);
        if(LobbyButton("mythic.begin",new Rect(x,609,285,49),"Начать испытание",true)){
            var trial=mythicSelection;
            mythicSelection=null;
            BeginChallenge(trial,mythicRace);
        }
    }

    void BeginWinterIntro(){
        winterIntro=true;winterIntroAge=0;winterCast=winterSkip=false;
        int kind=Array.FindIndex(match.Lines,line=>line.unit=="n014");
        if(kind>=0&&RaceModel(1,kind,false) is GameObject prefab&&prefab){
            winterQueen=Instantiate(prefab,visuals.transform).GetComponent<PcModel>();
            if(winterQueen){
                winterQueen.transform.position=World(HumanMatch.CastlePosition(1)+new Vector2(-9,11));
                winterQueen.Face(new Vector2(.12f,1),true);
                winterQueen.Play("Stand");
            }
        }
        FrameWinterOpening();
        var terrainRoot=controls.sceneryRoot.transform.parent;
        winterGround=terrainRoot.Cast<Transform>().Where(t=>t.name.StartsWith("Terrain",StringComparison.Ordinal))
            .SelectMany(t=>t.GetComponentsInChildren<MeshRenderer>())
            .Where(r=>r.name.StartsWith("Ground ",StringComparison.Ordinal)&&r.sharedMaterial?.mainTexture!=null)
            .OrderByDescending(r=>r.bounds.center.x).ToArray();
        winterGroundIndex=0;
        Debug.Log("CF_WINTER_INTRO queen="+(winterQueen!=null)+" ground="+winterGround.Length);
    }

    IEnumerator PrepareWinterPresentation(int generation){
        // The versus screen keeps the camera and the match clock paused.
        foreach(var renderer in winterGround){
            if(generation!=versusSerial)yield break;
            var source=renderer.sharedMaterial.mainTexture;
            if(!source||winterAtlases.ContainsKey(source))continue;
            winterAtlases.Add(source,FrostAtlas(source));
            yield return null;
        }
        if(generation!=versusSerial)yield break;
        if(minimapTexture){
            winterMinimap=FrostAtlas(minimapTexture);
            yield return null;
        }
        if(generation!=versusSerial)yield break;
        PrepareWinterGusts();
        PrepareWinterCastleAura();
        yield return null;
        if(generation!=versusSerial)yield break;
        PrepareWinterAudio();
        yield return PrepareWinterBossMusic(generation);
        if(generation!=versusSerial)yield break;
        Debug.Log("CF_WINTER_PREPARED atlases="+winterAtlases.Count+" minimap="+(winterMinimap!=null));
    }

    static readonly Rect WinterHud=new Rect(229,36,328,53);
    Vector3 WinterQueenFocus()=>winterQueen? winterQueen.transform.position+Vector3.up*2 :
        World(HumanMatch.CastlePosition(1)+new Vector2(-9,11))+Vector3.up*2;

    Vector3 WinterOpeningTarget()=>Vector3.Lerp(WinterQueenFocus(),
        World(HumanMatch.CastlePosition(1))+Vector3.up*7,.32f);
    Vector3 WinterCastleFocus()=>World(HumanMatch.CastlePosition(1))+Vector3.up*7;

    void FrameWinterOpening(){
        controls.InputBlocked=true;
        controls.GameplayViewport(false);
        FrameWinterShot();
    }

    void FrameWinterCamera(Vector3 target,float distance,float yaw,float pitch){
        controls.FocusWorld(target,distance);
        var camera=Camera.main;
        if(!camera)return;
        float radians=pitch*Mathf.Deg2Rad;
        var offset=Quaternion.Euler(0,yaw,0)*new Vector3(0,distance*Mathf.Sin(radians),-distance*Mathf.Cos(radians));
        camera.transform.position=target+offset;
        camera.transform.LookAt(target);
    }

    float WinterShotProgress(float start,float end)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,winterIntroAge));

    void FrameWinterShot(){
        if(winterIntroAge<3.25f){
            float t=WinterShotProgress(0,3.25f);
            FrameWinterCamera(Vector3.Lerp(WinterOpeningTarget(),WinterQueenFocus(),t),
                Mathf.Lerp(WinterOpeningDistance,23,t),Mathf.Lerp(-19,-8,t),36);
        }else if(winterIntroAge<5.3f){
            float t=WinterShotProgress(3.25f,5.3f);
            FrameWinterCamera(WinterCastleFocus(),Mathf.Lerp(35,30,t),Mathf.Lerp(-76,-86,t),42);
        }else if(winterIntroAge<7.35f){
            float t=WinterShotProgress(5.3f,7.35f);
            FrameWinterCamera(WinterCastleFocus(),Mathf.Lerp(46,53,t),Mathf.Lerp(22,28,t),67);
        }else{
            float t=WinterShotProgress(7.35f,WinterIntroDuration);
            float rise=Mathf.Sin(t*Mathf.PI)*15;
            FrameWinterCamera(Vector3.Lerp(WinterCastleFocus(),World(match.Builders[0].position),t)+Vector3.up*rise,
                Mathf.Lerp(53,40,t)+rise,Mathf.Lerp(28,0,t),Mathf.Lerp(67,56,t));
        }
    }

    void UpdateWinterIntro(){
        controls.InputBlocked=true;controls.EdgePan=false;controls.GameplayViewport(false);
        winterSkip|=Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.Space);
        winterIntroAge=winterSkip?WinterIntroDuration:Mathf.Min(WinterIntroDuration,winterIntroAge+Time.unscaledDeltaTime);
        if(!winterCast&&winterIntroAge>=1.3f){
            winterCast=true;
            if(winterQueen)winterQueen.Play("Spell",1,1);
        }
        int snowTarget=Mathf.CeilToInt(winterGround.Length*Mathf.InverseLerp(2.7f,4.6f,winterIntroAge));
        if(snowTarget>winterGroundIndex)AdvanceWinterGround(Mathf.Min(4,snowTarget-winterGroundIndex));
        FrameWinterShot();
        RenderMatch();
        if(winterIntroAge<WinterIntroDuration||winterGroundIndex<winterGround.Length)return;
        winterIntro=false;accumulator=0;
        if(winterQueen){winterQueen.gameObject.SetActive(false);Destroy(winterQueen.gameObject);}
        winterQueen=null;
        controls.FocusWorld(World(match.Builders[0].position),40);
        UpdateFog(true);
        Debug.Log("CF_WINTER_BATTLE elapsed="+match.Elapsed+" snow="+winterOverrides.Count);
    }

    void DrawWinterIntro(){
        var letterbox=new Color(.012f,.018f,.024f,.98f);
        Fill(new Rect(0,0,1280,59),letterbox);Fill(new Rect(0,626,1280,94),letterbox);
        Fill(new Rect(0,625,1280,1),new Color(.45f,.65f,.76f,.85f));
        Text(42,646,520,47,"Вечная зима",heading);
        if(LobbyButton("mythic.skip",new Rect(992,646,247,50),"Пропустить вступление"))winterSkip=true;
    }

    void DrawWinterHud(){
        StoneFrame(WinterHud);
        Text(WinterHud.x+11,WinterHud.y+5,302,20,match.WinterPhaseTwo?"Вечная зима · Стадия II":"Вечная зима",hudText);
        string status=match.TeamNorthern[0]?"Северяне · иммунитет":
            "Ледяные оковы через "+Mathf.CeilToInt(match.WinterChainIn)+" с";
        Text(WinterHud.x+11,WinterHud.y+26,302,20,status,new GUIStyle(hudSmall){normal={textColor=WinterBlue}});
    }

    void RenderWinterWind(Vector3 center,bool falling){
        if(!match.EternalWinter||!falling)return;
        float surge=Mathf.Pow(.5f+.5f*Mathf.Sin(Time.unscaledTime*1.1f),4);
        if(winterIntro){
            float howl=Mathf.InverseLerp(WinterStormCue,3.08f,winterIntroAge)*(1-WinterShotProgress(3.08f,5.3f));
            surge=Mathf.Max(surge,howl);
        }
        UpdateWinterAudio(surge);
        if(!winterGusts)return;
        if(match.WinterPhaseTwo&&!winterStrongGusts){
            winterStrongGusts=true;
            var wind=winterGusts.velocityOverLifetime;
            wind.x=new ParticleSystem.MinMaxCurve(44,62);
            wind.z=new ParticleSystem.MinMaxCurve(7,13);
            winterGusts.GetComponent<ParticleSystemRenderer>().lengthScale=7.5f;
            Notice("Стадия II · Ледяное перерождение");
        }
        winterGusts.transform.position=center+new Vector3(-21,8,-5);
        if(!winterGusts.isPlaying){
            winterGusts.Play();
            winterGusts.Emit(winterIntro?21:10);
        }
        var emission=winterGusts.emission;
        emission.rateOverTime=(winterIntro?19:match.WinterPhaseTwo?10:5)+(winterIntro?16:match.WinterPhaseTwo?18:11)*surge;
    }

    void PrepareWinterGusts(){
        var snow=ContentResources.Load<Material>("NorthernSnow");
        if(!snow)return;
        var obj=new GameObject("Eternal winter gusts");obj.transform.SetParent(visuals.transform);
        winterGusts=obj.AddComponent<ParticleSystem>();
        winterGustTexture=GustTexture();
        winterGustMaterial=new Material(snow){mainTexture=winterGustTexture};
        winterGustMaterial.SetColor("_Tint",new Color(.84f,.93f,1,.8f));
        var main=winterGusts.main;
        main.playOnAwake=false;main.maxParticles=Application.isMobilePlatform?32:56;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(.8f,1.5f);
        main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(.6f,1.8f);
        var shape=winterGusts.shape;shape.shapeType=ParticleSystemShapeType.Box;
        shape.scale=new Vector3(65,10,44);
        var velocity=winterGusts.velocityOverLifetime;velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.World;
        velocity.x=new ParticleSystem.MinMaxCurve(30,44);
        velocity.y=new ParticleSystem.MinMaxCurve(-2,-.25f);
        velocity.z=new ParticleSystem.MinMaxCurve(5,9);
        var renderer=winterGusts.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial=winterGustMaterial;renderer.renderMode=ParticleSystemRenderMode.Stretch;
        renderer.lengthScale=6;renderer.velocityScale=.25f;
        winterGusts.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void PrepareWinterCastleAura(){
        if(!winterGustMaterial)return;
        var obj=new GameObject("Snow around the Northern castle");
        obj.transform.SetParent(visuals.transform);
        obj.transform.position=WinterCastleFocus()-Vector3.up*2;
        winterCastleAura=obj.AddComponent<ParticleSystem>();
        var main=winterCastleAura.main;
        main.playOnAwake=false;
        main.maxParticles=Application.isMobilePlatform?20:34;
        main.startLifetime=new ParticleSystem.MinMaxCurve(1.2f,2.4f);
        main.startSpeed=0;
        main.startSize=new ParticleSystem.MinMaxCurve(3.4f,7);
        main.startColor=new Color(.86f,.96f,1,.88f);
        var shape=winterCastleAura.shape;
        shape.shapeType=ParticleSystemShapeType.Sphere;
        shape.radius=6.6f;
        shape.scale=new Vector3(1,.55f,1);
        var wind=winterCastleAura.velocityOverLifetime;
        wind.enabled=true;
        wind.y=2.7f;
        wind.orbitalY=.7f;
        var shade=winterCastleAura.colorOverLifetime;
        shade.enabled=true;
        var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(WinterBlue,1)},
            new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.28f),new GradientAlphaKey(0,1)});
        shade.color=gradient;
        var draw=winterCastleAura.GetComponent<ParticleSystemRenderer>();
        draw.sharedMaterial=winterGustMaterial;
        draw.renderMode=ParticleSystemRenderMode.Billboard;
        winterCastleAura.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void RenderWinterCastle(){
        if(!match.EternalWinter||winterIntro&&winterIntroAge<WinterStormCue)return;
        var castle=castleViews[1]?castleViews[1].transform:castleObjects[1].transform;
        if(winterCastleBody!=castle){
            RestoreWinterCastle();
            winterCastleBody=castle;
            winterCastleBaseScale=castle.localScale;
            foreach(var renderer in castle.GetComponentsInChildren<Renderer>(true)){
                var material=renderer.sharedMaterial;
                if(!material||material.shader.name!="CastleFight/NativeMapProp")continue;
                var original=new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original);
                winterCastleTint.Add((renderer,original));
                var frost=new MaterialPropertyBlock();
                renderer.GetPropertyBlock(frost);
                frost.SetColor("_Color",new Color(1.16f,1.28f,1.34f,1));
                frost.SetColor("_TeamColor",new Color(.55f,.67f,.83f,1));
                renderer.SetPropertyBlock(frost);
            }
        }
        float power=winterIntro?WinterShotProgress(WinterStormCue,4.3f):1;
        castle.localScale=winterCastleBaseScale*Mathf.Lerp(1,WinterCastleScale,power);
        if(!winterCastleAura)return;
        var camera=Camera.main;
        bool nearby=camera&&(camera.transform.position-winterCastleAura.transform.position).sqrMagnitude<10000;
        if(!castle.gameObject.activeInHierarchy||!nearby||page==Page.Result){
            if(winterCastleAura.isPlaying)winterCastleAura.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }
        var emission=winterCastleAura.emission;
        emission.rateOverTime=Mathf.Lerp(6,14,power);
        if(!winterCastleAura.isPlaying)winterCastleAura.Play();
    }

    void RestoreWinterCastle(){
        if(winterCastleBody)winterCastleBody.localScale=winterCastleBaseScale;
        foreach(var entry in winterCastleTint)
            if(entry.renderer)entry.renderer.SetPropertyBlock(entry.original.isEmpty?null:entry.original);
        winterCastleTint.Clear();
        winterCastleBody=null;
    }

    static Texture2D GustTexture(){
        const int size=32;
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            float u=(x+.5f)*2/size-1,v=(y+.5f)*2/size-1;
            float fade=Mathf.Clamp01(1-u*u-v*v);
            float noise=.58f+.42f*Mathf.PerlinNoise(x*.18f,y*.29f);
            pixels[y*size+x]=new Color(.87f,.96f,1,fade*fade*noise*.44f);
        }
        texture.SetPixels32(pixels);texture.Apply(false,true);
        return texture;
    }

    void AdvanceWinterGround(int budget){
        for(int i=0;i<budget&&winterGroundIndex<winterGround.Length;i++){
            var renderer=winterGround[winterGroundIndex++];
            var source=renderer.sharedMaterial.mainTexture;
            if(!source||!winterAtlases.TryGetValue(source,out var snow))continue;
            var original=new MaterialPropertyBlock();renderer.GetPropertyBlock(original);
            var painted=new MaterialPropertyBlock();renderer.GetPropertyBlock(painted);
            painted.SetTexture("_MainTex",snow);renderer.SetPropertyBlock(painted);
            winterOverrides.Add((renderer,original));
        }
    }

    static Texture2D FrostAtlas(Texture source){
        float scale=Mathf.Min(1,512f/Mathf.Max(source.width,source.height));
        int w=Mathf.Max(1,Mathf.RoundToInt(source.width*scale)),h=Mathf.Max(1,Mathf.RoundToInt(source.height*scale));
        var rt=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous=RenderTexture.active;
        var atlas=new Texture2D(w,h,TextureFormat.RGBA32,true);
        try{
            Graphics.Blit(source,rt);RenderTexture.active=rt;
            atlas.ReadPixels(new Rect(0,0,w,h),0,0,false);
        }finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
        var pixels=atlas.GetPixels32();
        for(int i=0;i<pixels.Length;i++){
            var pixel=pixels[i];
            float detail=(pixel.r*.213f+pixel.g*.715f+pixel.b*.072f)/255f;
            float snow=Mathf.Lerp(.34f,.79f,Mathf.Pow(detail,.75f));
            pixels[i]=new Color(snow*.91f,snow*.97f,snow*1.045f,pixel.a/255f);
        }
        atlas.name="Eternal winter terrain · "+source.name;
        atlas.wrapMode=source.wrapMode;atlas.filterMode=FilterMode.Bilinear;
        atlas.SetPixels32(pixels);atlas.Apply(true,true);
        return atlas;
    }

    void ClearWinterTrial(){
        ClearWinterAudio();
        RestoreWinterCastle();
        winterCastleAura=null;
        winterIntro=winterSkip=winterCast=winterStrongGusts=false;winterIntroAge=0;winterQueen=null;
        foreach(var layer in winterOverrides)
            if(layer.renderer)layer.renderer.SetPropertyBlock(layer.original.isEmpty?null:layer.original);
        foreach(var texture in winterAtlases.Values)if(texture)Destroy(texture);
        if(winterGustTexture)Destroy(winterGustTexture);
        if(winterGustMaterial)Destroy(winterGustMaterial);
        if(winterMinimap)Destroy(winterMinimap);
        winterGusts=null;winterGustTexture=winterMinimap=null;winterGustMaterial=null;
        winterOverrides.Clear();winterAtlases.Clear();winterGround=null;winterGroundIndex=0;
    }

    IEnumerator ExerciseWinterTrial(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"winter-validation"));
        Directory.CreateDirectory(folder);
        OpenProgression(false);
        yield return Action("progress.mythic");
        var sharedArt=WinterArtwork();
        Check(sharedArt&&sharedArt.width>800,"Epochal tile loads the embedded or local boss painting");
        yield return Shot(folder,"00-epochal-list");
        yield return Action("challenge."+HumanMatch.WinterTrial);
        Check(MythicRacePicker&&match==null,"The epochal list opens a race picker before starting the fight");
        yield return Action("mythic.race.0");
        yield return Shot(folder,"01-winter-race-selection");
        testHoldAuthority=true;
        yield return Action("mythic.begin");
        Check(versusWinter&&WinterArtwork()==sharedArt&&winterVersusReveal<.8f,
            "Boss VS starts its reveal with the same painting as the trial detail");
        yield return RecoveryWait(()=>versusActive&&winterAtlases.Count>0,"Versus prepares snow before revealing the introduction",12);
        Check(versusActive&&winterIntroAge==0&&winterOverrides.Count==0&&match.Elapsed==0&&
            !winterCast&&!northernSnow,"Loading creates textures while the map and snowfall remain untouched");
        yield return Shot(folder,"01b-winter-preparation");
        yield return RecoveryWait(()=>versusActive&&versusReadyDrawFrame>=0,"The ready VS screen actually renders",15);
        Check(versusActive&&versusReadyVisible<.25f&&winterIntroAge==0,
            "Resource loading time does not consume the visible VS hold");
        yield return RecoveryWait(()=>!versusActive&&winterIntro,"The introduction opens after winter assets are ready",15);
        Check(versusReadyVisible>=VersusReadyDuration,"The ready VS screen remains visible for the full hold");
        var queenOnScreen=Camera.main.WorldToViewportPoint(WinterQueenFocus());
        Check(winterQueen&&winterQueen.gameObject.activeInHierarchy&&!winterCast&&
            queenOnScreen.z>0&&queenOnScreen.x>.2f&&queenOnScreen.x<.8f&&queenOnScreen.y>.2f&&queenOnScreen.y<.8f,
            "Jaina stands in the opening frame before casting");
        yield return Shot(folder,"01c-jaina-opening");
        var atlasReferences=winterAtlases.Values.ToArray();
        var preparedMinimap=winterMinimap;
        var preparedGusts=winterGusts;
        var preparedWind=winterWindClip;
        var soundtrack=winterBossMusic;
        Check(soundtrack&&soundtrack.clip&&soundtrack.clip.name==WinterBossTrack&&soundtrack.clip.length>170&&
            soundtrack.isPlaying&&soundtrack.loop&&soundtrack.volume==1,
            "Frozen Final Stand starts at full source volume when the introduction opens");
        Check(winterGround.Length>0&&winterGround.All(r=>winterAtlases.ContainsKey(r.sharedMaterial.mainTexture))&&
            preparedMinimap&&preparedGusts&&!preparedGusts.isPlaying&&preparedWind&&!winterWindAudio.isPlaying,
            "All terrain, minimap, gust and audio assets are prepared before the cast");
        Check(winterIntro&&match.EternalWinter&&winterQueen&&match.Elapsed==0,"Winter opens with the real Ice Queen and stops the match clock");
        yield return new WaitForSecondsRealtime(3.1f);
        Check(winterIntro&&winterCast&&northernSnow&&winterGusts&&match.Elapsed==0,"The Queen casts and starts a blizzard with wind gusts before the battle");
        Check(winterWindAudio && winterWindAudio.isPlaying && winterWindAudio.loop && winterWindAudio.volume > 0,
            "The blizzard starts looping wind audio during the cast");
        Check(winterCastleAura&&winterCastleAura.isPlaying&&winterCastleBody.localScale.x>winterCastleBaseScale.x,
            "The wind cue awakens a visible aura and enlarges the boss castle");
        yield return Shot(folder,"02-queen-casts");
        Check(soundtrack&&soundtrack.isPlaying&&soundtrack.volume==1,"The soundtrack stays at full source volume during the cast");
        var musicSamples=new float[256];
        bool audibleTrack=false;
        for(int i=0;i<6&&soundtrack&&!audibleTrack;i++){
            soundtrack.GetOutputData(musicSamples,0);
            audibleTrack=musicSamples.Any(v=>Mathf.Abs(v)>.00001f);
            if(!audibleTrack)yield return new WaitForSecondsRealtime(.04f);
        }
        Check(audibleTrack,"The soundtrack emits decoded audio during the introduction");
        float musicBeforeSkip=soundtrack?soundtrack.time:0;
        yield return Action("mythic.skip");
        yield return RecoveryWait(()=>!winterIntro,"Skip reaches the same finished winter scene",5);
        Check(winterOverrides.Count==winterGround.Length&&winterGround.Length>0&&match.Elapsed==0,"Skip covers every terrain layer before starting the clock");
        Check(atlasReferences.SequenceEqual(winterAtlases.Values)&&winterMinimap==preparedMinimap&&
            winterGusts==preparedGusts&&winterWindClip==preparedWind,"The cast and skip reuse the prepared assets");
        Check(soundtrack&&winterBossMusic==soundtrack&&soundtrack.isPlaying&&soundtrack.time>=musicBeforeSkip,
            "Taking control keeps the same soundtrack and playback position");
        yield return new WaitForSecondsRealtime(.34f);
        Check(soundtrack&&soundtrack.volume<1&&soundtrack.volume>WinterBattleMusicVolume,
            "Music fades smoothly while the builder is controllable");
        yield return new WaitForSecondsRealtime(WinterMusicFadeSeconds);
        Check(soundtrack&&Mathf.Abs(soundtrack.volume-WinterBattleMusicVolume)<.001f,
            "The soundtrack reaches its quieter combat volume");
        if(soundtrack)soundtrack.time=soundtrack.clip.length-.25f;
        yield return RecoveryWait(()=>soundtrack&&soundtrack.isPlaying&&soundtrack.time<2,
            "The real soundtrack loops back to the beginning during combat",4);
        yield return Shot(folder,"03-snowy-battlefield");
        var at=Vector2.zero;
        var player=match.Spawn(0,match.Kind("h000"),at);
        var player2=match.Spawn(0,match.Kind("h000"),at+Vector2.left*2);
        int northKind=Array.FindIndex(match.Lines,line=>line.unit=="n014");
        var enemy=match.Spawn(1,northKind,at+Vector2.right*50);
        enemy.stun=1000;
        var normal=new HumanMatch(JObject.Parse(RaceCatalog(0)),500,2);
        var normalFighter=normal.Spawn(0,normal.Kind("h000"),at);
        Check(Mathf.Abs(match.MoveSpeed(player)-normal.MoveSpeed(normalFighter)*.5f)<.001f&&
            Mathf.Abs(match.AttackSpeed(player)-.7f)<.001f,"Winter halves movement speed and reduces attack speed by thirty percent");
        Check(!match.WinterAffects(enemy)&&Mathf.Abs(match.AttackSpeed(enemy)-1)<.001f,"The enemy Northern army ignores the blizzard");
        match.PlayerVersusPlayer=true;
        foreach(var unit in new[]{player,player2,enemy})unit.cooldown=1000;
        for(int i=0;i<199;i++)Tick(.05f);
        Check(!player.effects.Any(e=>e.group==HumanMatch.WinterChain)&&!player2.effects.Any(e=>e.group==HumanMatch.WinterChain),
            "The tenth second has not elapsed");
        Tick(.05f);
        var frozen=new[]{player,player2}.Where(u=>u.effects.Any(e=>e.group==HumanMatch.WinterChain)).ToArray();
        Check(frozen.Length==1&&match.Disarmed(frozen[0])&&match.MoveSpeed(frozen[0])==0&&
            !enemy.effects.Any(e=>e.group==HumanMatch.WinterChain),"The tenth second freezes exactly one eligible unit");
        if(frozen.Length==1){builderSelected=false;selectedUnit=frozen[0].id;controls.FocusWorld(World(frozen[0].position),30);}
        yield return Shot(folder,"04-frozen-fighter");
        for(int i=0;i<63;i++)Tick(.05f);
        Check(frozen.Length==1&&!frozen[0].effects.Any(e=>e.group==HumanMatch.WinterChain)&&
            !match.Disarmed(frozen[0])&&match.MoveSpeed(frozen[0])>0,"The ice thaws after three seconds");
        for(int i=0;i<137;i++)Tick(.05f);
        Check(new[]{player,player2}.Count(u=>u.effects.Any(e=>e.group==HumanMatch.WinterChain))==1,
            "The next ten-second cycle freezes one fighter again");
        var deathPoint=new Vector2(12,18);
        int testKind=match.Kind("h000");
        match.CastleHp[0]=HumanMatch.WinterPhaseTwoHp-100;
        match.CastleHp[1]=HumanMatch.WinterPhaseTwoHp+100;
        var beforePhase=match.Spawn(0,testKind,deathPoint);
        match.ApplyEffect(beforePhase,"A04H",1);
        match.DamageUnit(beforePhase,10000,1,execute:true);
        Tick(.05f);
        Check(!match.WinterPhaseTwo&&match.Corpses.Any(c=>c.unit==beforePhase),
            "Only the boss castle can trigger stage two, and deaths in stage one are normal");

        match.CastleHp[1]=HumanMatch.WinterPhaseTwoHp;
        Tick(.05f);
        RenderMatch();
        Check(match.WinterPhaseTwo&&winterStrongGusts&&winterGusts.velocityOverLifetime.x.constantMax==62,
            "At exactly 2500 boss castle HP, the stage changes and the existing wind accelerates");
        yield return new WaitForSecondsRealtime(.6f);
        Check(soundtrack&&soundtrack.isPlaying&&soundtrack.pitch>1.15f,
            "Stage two accelerates the same looping soundtrack");
        match.CastleHp[1]=HumanMatch.WinterPhaseTwoHp+500;
        Tick(.05f);
        Check(match.WinterPhaseTwo,"Repairing the boss castle cannot end the second stage");

        var marked=new[]{player,player2}.FirstOrDefault(u=>u.effects.Any(e=>e.group==HumanMatch.WinterChain));
        var birthPoint=marked!=null?marked.position:Vector2.zero;
        if(marked!=null)match.DamageUnit(marked,10000,1,execute:true);
        Tick(.05f);
        Check(marked!=null&&match.Units.Count(u=>u.team==1&&u.kind==testKind&&u.hp>0)==1&&
            match.Units.Any(u=>u.team==1&&u.kind==testKind&&Vector2.Distance(u.position,birthPoint)<1)&&
            !match.Corpses.Any(c=>c.unit==marked),
            "A fighter killed while inside a mythic ice block rises once on the boss side without a duplicate corpse");

        var iceVictim=match.Spawn(0,testKind,deathPoint+Vector2.up*3);
        match.ApplyEffect(iceVictim,"A04H",1);
        var ice=iceVictim.effects.First(e=>e.group=="northern_ice_chains");
        iceVictim.hp=.05f;
        ice.left=.05f;
        Tick(.05f);
        Check(iceVictim.hp<=0&&match.Units.Count(u=>u.team==1&&u.kind==testKind&&u.hp>0)==2&&
            !match.Corpses.Any(c=>c.unit==iceVictim),
            "The final damage tick of a Northern ice block also turns its victim");

        var unmarked=match.Spawn(0,testKind,deathPoint+Vector2.down*3);
        match.ApplyEffect(unmarked,"A03Y",1);
        match.DamageUnit(unmarked,10000,1,execute:true);
        Tick(.05f);
        Check(match.Units.Count(u=>u.team==1&&u.kind==testKind&&u.hp>0)==2&&match.Corpses.Any(c=>c.unit==unmarked),
            "Normal deaths and ordinary chill do not change sides");
        yield return Shot(folder,"05-winter-stage-two");

        var mirror=new HumanMatch(JObject.Parse(RaceCatalog(NorthernOpponent)),500,2);
        mirror.BeginChallengeRules(WinterTrialDefinition());
        var northAlly=mirror.Spawn(0,mirror.Kind(match.Lines[northKind].building),Vector2.zero);
        Check(!mirror.WinterAffects(northAlly)&&Mathf.Abs(mirror.AttackSpeed(northAlly)-1)<.001f,
            "A player who selects Northern also receives immunity");
        int coins=progression.Coins,xp=progression.Experience;
        match.Surrender(1);SettleChallenge();
        Check(soundtrack&&!soundtrack.isPlaying,"Ending combat stops the boss soundtrack");
        if(MythicLeaderboardUrl().Length>0){
            page=Page.Result;
            Check(mythicGuestPrompt&&pendingMythicScore!=null&&pendingMythicScore.race==0,
                "Victory opens nickname entry for a guest with the race used in battle");
            mythicNickname="Тестовый рыцарь";
            yield return Shot(folder,"06-guest-record");
            yield return Action("mythic.score.save");
            yield return RecoveryWait(()=>mythicPlace>0&&!mythicGuestPrompt,"Guest result is saved and ranked on the test server",12);
            Check(mythicLeaderboard.Any(row=>(string)row["nickname"]=="Тестовый рыцарь"&&(int?)row["race"]==0),
                "Leaderboard includes nickname, battle time and the played race");
        }
        Check(progression.Completed(HumanMatch.WinterTrial)&&progression.Best(HumanMatch.WinterTrial)>0&&
            progression.Coins==coins&&progression.Experience==xp,"Winning records the time without granting coins or experience");
        var wind = winterWindAudio;
        var covered=winterOverrides.Select(layer=>layer.renderer).ToArray();
        MainMenu();
        Check(wind && !wind.isPlaying && !winterWindAudio && !winterWindClip, "Leaving stops and releases the wind audio");
        Check(!winterIntro&&winterOverrides.Count==0&&winterAtlases.Count==0&&covered.All(r=>!r.HasPropertyBlock()),
            "Leaving restores the map and releases its snow textures");
        BeginChallenge(progression.Challenge(HumanMatch.WinterTrial),0);
        yield return RecoveryWait(()=>!versusActive&&winterIntro,"A replay prepares its own winter assets",15);
        yield return RecoveryWait(()=>winterIntroAge>=3.6f,"The camera cuts from the Queen to the castle",6);
        var castleAngle=Camera.main.transform.rotation;
        yield return Shot(folder,"07-castle-low-angle");
        yield return RecoveryWait(()=>winterIntroAge>=5.65f,"The camera cuts to a higher angle from the other side",4);
        Check(Quaternion.Angle(castleAngle,Camera.main.transform.rotation)>65,"Castle shots use visibly different angles");
        Check(Mathf.Abs(winterCastleBody.localScale.x/winterCastleBaseScale.x-WinterCastleScale)<.002f,
            "The castle reaches its final visual scale during the storm");
        yield return Shot(folder,"08-castle-high-angle");
        yield return RecoveryWait(()=>!winterIntro,"The complete introduction returns control automatically",9);
        var builderOnScreen=Camera.main.WorldToViewportPoint(World(match.Builders[0].position));
        Check(builderOnScreen.z>0&&Mathf.Abs(builderOnScreen.x-.5f)<.06f,
            "The last shot hands the camera over to the player builder");
        Check(match.Elapsed==0&&!match.WinterPhaseTwo&&winterOverrides.Count==winterGround.Length&&winterGusts,
            "The full introduction ends with the same snow, wind and initial clock");
        var replaySoundtrack=winterBossMusic;
        Check(replaySoundtrack&&replaySoundtrack.isPlaying&&replaySoundtrack.time>=6,
            "A full introduction carries the entire opening of the song into combat");
        MainMenu();
        Check(replaySoundtrack&&!replaySoundtrack.isPlaying&&!winterBossMusic,
            "Leaving the battle releases its soundtrack");
        BeginChallenge(WinterTrialDefinition(),0);
        yield return RecoveryWait(()=>versusActive&&winterAtlases.Count>0,"A new preparation can be interrupted",12);
        MainMenu();
        yield return null;
        yield return null;
        Check(!versusActive&&match==null&&winterAtlases.Count==0&&!winterMinimap&&!winterWindClip,
            "Leaving during preparation releases the cache and cancels the pending introduction");
        File.WriteAllText(Path.Combine(folder,"winter-checks.json"),new JObject{
            ["checks"]=testChecks,["failures"]=testFailures
        }.ToString());
        Debug.Log("CF_MYTHIC_WINTER_RESULT failures="+testFailures);
        Application.Quit(testFailures==0?0:1);
    }
}
}
