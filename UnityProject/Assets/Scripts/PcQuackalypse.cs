using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    const string QuackTitle="Великий Квакалипсис";
    static readonly Color QuackGreen=new Color(.59f,.94f,.46f);
    static readonly Rect QuackHud=new Rect(268,28,294,79);
    const float QuackFaceStart=6.05f,QuackFaceEnd=7.12f,QuackFlyoverStart=8f,QuackOpeningEnd=11.35f;
    const float QuackSwampCue=7.73f,QuackFaceDistance=7.5f,QuackPullbackDistance=28f;
    const float QuackFirstCrack=.86f,QuackBreak=1.93f,QuackMeme=2.40f,QuackRitual=4.06f;
    const float QuackCall=QuackRitual+.78f,QuackPoolsShot=QuackRitual+2.15f,QuackReversalEnd=9.3f;
    static readonly float[] QuackCrackCues={QuackFirstCrack,1.18f,1.55f,QuackBreak};
    static readonly Vector2[] QuackCrackRays={new Vector2(0,143),new Vector2(322,0),new Vector2(885,0),
        new Vector2(1280,237),new Vector2(1278,570),new Vector2(890,720),new Vector2(409,720),new Vector2(0,625)};
    enum QuackScene { None, Opening, Reversal }
    QuackScene quackScene;
    float quackSceneAge,quackEmojiAt=-100;
    int quackEmojiSeen,quackSpeechCue,quackGlassCue;
    bool quackLighting,quackCast,quackVersus,quackSkip,quackFacePose;
    Color quackDay;
    Light quackSun;
    float quackSunIntensity;
    Texture2D quackReveal,quackEmoji,quackDrizzle,quackDownpour,quackPainting,quackHudFrame;
    RenderTexture quackVictorySnapshot;
    Material quackGlassMaterial;
    AudioClip quackCroak,quackCrackSound,quackGlassSound;
    AudioSource quackSound;
    PcModel quackKing;
    Vector3 quackKingHead;
    readonly PcModel[] quackGuards=new PcModel[3];
    Transform quackRisingCastle;
    GameObject quackRitualEffect;
    Vector3 quackCastleOrigin;

    static JObject QuackDefinition()=>new JObject{
        ["id"]=HumanMatch.QuackTrial,["title"]=QuackTitle,["opponent"]="Pepe Brotherhood",
        ["summary"]="Ква???",["description"]="Ква???",
        ["start_gold"]=500,
        ["opponent_castle_hp"]=HumanMatch.QuackCastleHealth,["difficulty"]=2,["reward"]=0,["xp"]=0
    };

    void DrawQuackPainting(Rect area){
        Fill(area,new Color(.061f,.112f,.063f));
        if(!quackPainting)quackPainting=LoadProtocolTexture("QuackalypseBoss","jpg",TextureFormat.RGB24,false);
        if(!quackPainting)return;
        float height=Mathf.Min(1,area.height*quackPainting.width/(area.width*quackPainting.height));
        float width=Mathf.Min(1,area.width*quackPainting.height/(area.height*quackPainting.width));
        GUI.DrawTextureWithTexCoords(area,quackPainting,new Rect((1-width)*.68f,(1-height)*.66f,width,height),false);
    }

    void BeginQuackPresentation(){
        quackScene=QuackScene.Opening;quackSceneAge=0;quackCast=quackSkip=false;quackEmojiSeen=quackSpeechCue=0;quackEmojiAt=-100;
        quackFacePose=false;
        quackDay=RenderSettings.ambientLight;quackLighting=true;
        quackSun=RenderSettings.sun;
        if(!quackSun){
            foreach(var light in FindObjectsByType<Light>())
                if(light.type==LightType.Directional){quackSun=light;break;}
        }
        if(quackSun)quackSunIntensity=quackSun.intensity;
        if(!quackReveal)quackReveal=LoadProtocolTexture("SpeedrunnersReveal","jpg",TextureFormat.RGB24,false);
        if(!quackEmoji)quackEmoji=LoadProtocolTexture("PepeSabotage","png",TextureFormat.RGBA32,false);
        if(!quackHudFrame)quackHudFrame=LoadProtocolTexture("QuackHudFrame","png",TextureFormat.RGBA32,false);
        if(!quackDrizzle)quackDrizzle=MakeQuackRain(false);
        if(!quackDownpour)quackDownpour=MakeQuackRain(true);
        if(!quackCroak)quackCroak=LoadQuackSound("QuackCroak","Голос Великого Квака");
        if(!quackCrackSound)quackCrackSound=LoadQuackSound("QuackCrack","Растущие трещины");
        if(!quackGlassSound)quackGlassSound=LoadQuackSound("QuackGlass","Разбитое стекло");
        quackSound=visuals.AddComponent<AudioSource>();
        quackSound.playOnAwake=false;quackSound.spatialBlend=0;
        controls.InputBlocked=true;
        controls.GameplayViewport(false);
        PrepareQuackSwamp();
        quackKing=CreateQuackActor(HumanMatch.QuackKingBuilding,QuackKingPoint,1.8f);
        CacheQuackKingHead();
        quackGuards[0]=CreateQuackActor("pB31",QuackGuardPoint(0),1);
        quackGuards[1]=CreateQuackActor("pB12",QuackGuardPoint(1),1);
        quackGuards[2]=CreateQuackActor("pB14",QuackGuardPoint(2),1);
    }

    void BeginQuackReversal(){
        quackScene=QuackScene.Reversal;quackSceneAge=0;quackCast=quackSkip=false;
        quackGlassCue=0;
        StartCoroutine(CaptureQuackVictory());
        controls.InputBlocked=true;controls.EdgePan=false;
        controls.GameplayViewport(false);
        if(preview)preview.SetActive(false);
        if(localGrid)localGrid.SetActive(false);
    }

    Vector2 QuackKingPoint=>HumanMatch.CastlePosition(1)+new Vector2(-15,1);
    Vector2 QuackGuardPoint(int index)=>QuackKingPoint+new Vector2(-5.5f-index*.35f,(index-1)*3.4f);
    PcModel CreateQuackActor(string code,Vector2 point,float scale){
        int kind=match.Kind(code);
        if(kind<0)return null;
        var prefab=RaceModel(1,kind,false);
        if(!prefab)return null;
        var actor=Instantiate(prefab,visuals.transform).GetComponent<PcModel>();
        if(!actor)return null;
        actor.transform.position=World(point);
        actor.transform.localScale*=scale;
        actor.Face(Vector2.left,true);
        return actor;
    }

    void UpdateQuackScene(){
        controls.InputBlocked=true;controls.EdgePan=false;
        quackSceneAge=quackCaptureTime??quackSceneAge+Time.unscaledDeltaTime;
        bool opening=quackScene==QuackScene.Opening;
        float time=quackSceneAge;
        if(!opening)UpdateQuackGlass(time);
        if(!opening&&time>=QuackRitual&&match.QuackRitualPending){
            match.OpenQuackDownpour();
            ClearGroupSelection();
            selectedHut=selectedUnit=selectedItem=chosen=-1;builderSelected=true;selectedCastle=buildMenu=moveOrder=false;
            targetedSpell=null;CloseBattleHelp();
            RenderSettings.ambientLight=Color.Lerp(quackDay,new Color(.14f,.22f,.26f),.76f);
            if(quackSun)quackSun.intensity=quackSunIntensity*.36f;
            quackKing=CreateQuackActor(HumanMatch.QuackKingBuilding,QuackKingPoint,1.8f);
            CacheQuackKingHead();
        }
        if(opening)UpdateQuackGuards(time);
        if(!opening&&!quackCast&&time>=QuackCall){
            quackCast=true;
            PlayQuackCroak(.82f,.86f);
            PlayQuackRitualEffect();
        }
        if(quackKing){
            bool speaking=opening?time>=1.15f&&time<4.9f:quackCast;
            if(opening&&time>=QuackFaceStart)HoldQuackFacePose();
            else quackKing.Play(speaking?"Attack":"Stand",.85f,opening?quackSpeechCue:1);
        }
        bool jump=(quackSkip||Input.GetKeyDown(KeyCode.Escape))&&(opening||match.QuackDownpour);
        float end=opening?QuackOpeningEnd:QuackReversalEnd;
        if(opening&&(time>=QuackSwampCue||jump))RevealQuackSwamp();
        if(opening)FrameQuackOpening(jump?end:time);
        else if(time>=QuackRitual)FrameQuackReversal(time);
        RenderMatch();
        if(!jump&&time<end)return;
        quackScene=QuackScene.None;
        RestoreQuackCastle();ClearQuackGlass();
        ClearQuackActors();
        FocusBuilder();
    }

    void CacheQuackKingHead(){
        quackKingHead=World(QuackKingPoint)+Vector3.up*3.5f;
        if(!quackKing)return;
        var bounds=quackKing.BodyBounds();
        float ground=quackKing.transform.position.y;
        quackKingHead.y=Mathf.Clamp(bounds.max.y-bounds.size.y*.22f,ground+2.1f,ground+5.4f);
    }

    void UpdateQuackGuards(float time){
        int cue=time>=4.18f?3:time>=3.15f?2:time>=1.15f?1:0;
        if(cue>quackSpeechCue){
            quackSpeechCue=cue;
            PlayQuackCroak(.85f,cue==2?1.09f:cue==3?.91f:1f);
        }
        for(int i=0;i<quackGuards.Length;i++){
            var guard=quackGuards[i];
            if(!guard)continue;
            var point=QuackGuardPoint(i);
            float timeToJump=5.05f+i*.14f;
            if(time<timeToJump){
                guard.Face((QuackKingPoint-point).normalized,true);
                float nod=time>=4.05f&&time<4.9f?Mathf.Sin((time-4.05f)*20)*9:0;
                guard.transform.rotation*=Quaternion.Euler(nod,0,0);
                guard.Play("Stand");
            }else{
                float hop=Mathf.Clamp01((time-timeToJump)/1.4f);
                var direction=new Vector2(-1,(i-1)*.95f).normalized;
                point+=direction*hop*15;
                guard.Face(direction,true);
                guard.transform.position=World(point)+Vector3.up*(Mathf.Abs(Mathf.Sin(hop*Mathf.PI*3))*1.35f);
                guard.Play("Walk",1.4f);
            }
        }
    }

    void HoldQuackFacePose(){
        if(quackFacePose)return;
        quackFacePose=true;
        var clip=quackKing.StandClip();
        var animation=quackKing.animatedRoot.GetComponent<Animation>();
        if(!clip||!animation)return;
        animation.Play(clip.name);
        var state=animation[clip.name];
        state.normalizedTime=.25f;
        state.speed=0;
        animation.Sample();
    }

    static float QuackEase(float start,float end,float time)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,time));
    static float QuackHandoff(float time)=>QuackEase(QuackOpeningEnd-.7f,QuackOpeningEnd,time);

    void FrameQuackOpening(float time){
        var king=World(QuackKingPoint)+Vector3.up*2.3f;
        var face=quackKingHead+Vector3.up*.55f+Vector3.left*.88f+Vector3.forward*.35f;
        if(time<QuackFaceStart){
            float approach=QuackEase(5.3f,QuackFaceStart,time);
            FrameWinterCamera(Vector3.Lerp(king,face,approach),Mathf.Lerp(32,QuackFaceDistance+.65f,approach),
                Mathf.Lerp(48,93,approach),Mathf.Lerp(45,17,approach));
        }else if(time<QuackFaceEnd){
            float close=QuackEase(QuackFaceStart,QuackFaceEnd,time);
            FrameWinterCamera(face,QuackFaceDistance+.65f*(1-close),93,17);
        }else if(time<QuackFlyoverStart){
            float pullback=QuackEase(QuackFaceEnd,QuackFlyoverStart,time);
            FrameWinterCamera(Vector3.Lerp(face,king,pullback),Mathf.Lerp(QuackFaceDistance,QuackPullbackDistance,pullback),
                Mathf.Lerp(93,68,pullback),Mathf.Lerp(17,43,pullback));
        }else{
            float travel=QuackEase(QuackFlyoverStart,QuackOpeningEnd,time);
            float rise=Mathf.Sin(travel*Mathf.PI)*24;
            FrameWinterCamera(Vector3.Lerp(king,World(match.Builders[0].position),travel)+Vector3.up*rise,
                Mathf.Lerp(QuackPullbackDistance,40,travel)+rise,68*(1-travel),Mathf.Lerp(43,56,travel));
            controls.GameplayViewport(true,QuackHandoff(time));
        }
    }

    void FrameQuackReversal(float time){
        var king=World(QuackKingPoint)+Vector3.up*2.3f;
        float sweep=QuackEase(QuackPoolsShot-.35f,QuackPoolsShot+1.15f,time);
        king=Vector3.Lerp(king,World(HumanMatch.QuackPoolPoint(0)),sweep);
        float pan=QuackEase(QuackReversalEnd-1.5f,QuackReversalEnd,time);
        var target=Vector3.Lerp(king,World(match.Builders[0].position),pan);
        float distance=Mathf.Lerp(32,45,pan)+Mathf.Sin(pan*Mathf.PI)*42;
        FrameWinterCamera(target,distance,48*(1-pan),45+pan*24);
    }

    void RenderQuackCastle(GameObject castle){
        if(quackScene!=QuackScene.Reversal)return;
        castle.SetActive(!match.QuackRitualPending);
        if(match.QuackRitualPending)return;
        if(quackRisingCastle!=castle.transform){
            RestoreQuackCastle();
            quackRisingCastle=castle.transform;quackCastleOrigin=quackRisingCastle.position;
        }
        float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(QuackRitual,QuackRitual+1.6f,quackSceneAge));
        quackRisingCastle.position=quackCastleOrigin-Vector3.up*(1-rise)*14;
    }

    void RestoreQuackCastle(){
        if(quackRisingCastle)quackRisingCastle.position=quackCastleOrigin;
        quackRisingCastle=null;
    }

    void PlayQuackRitualEffect(){
        // Reuse the installed classic golden resurrection cast supplied by SpellArt036.
        var prefab=SpellPrefab036("fx_403cdcbdb657");
        if(!prefab)return;
        quackRitualEffect=Instantiate(prefab,World(QuackKingPoint),Quaternion.identity,visuals.transform);
        if(quackRitualEffect.TryGetComponent<ClassicProjectileModel>(out var effect))effect.PlayCast();
    }

    IEnumerator CaptureQuackVictory(){
        while(quackScene==QuackScene.Reversal&&quackSceneAge<QuackBreak-.22f)yield return null;
        yield return new WaitForEndOfFrame();
        if(quackScene!=QuackScene.Reversal||quackSceneAge>=QuackBreak)yield break;
        ClearQuackGlass();
        int width=Mathf.Min(1024,Screen.width);
        int height=Mathf.Max(1,Mathf.RoundToInt(Screen.height*width/(float)Screen.width));
        quackVictorySnapshot=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32){
            name="Стекло ложной победы",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp
        };
        ScreenCapture.CaptureScreenshotIntoRenderTexture(quackVictorySnapshot);
    }

    void UpdateQuackGlass(float time){
        while(quackGlassCue<QuackCrackCues.Length&&time>=QuackCrackCues[quackGlassCue]){
            int index=quackGlassCue++;
            if(time-QuackCrackCues[index]>.17f||!quackSound)continue;
            bool impact=index==QuackCrackCues.Length-1;
            var sound=impact?quackGlassSound:quackCrackSound;
            if(!sound)continue;
            quackSound.pitch=impact?1:1.18f-index*.115f;
            quackSound.PlayOneShot(sound,options.volume*(impact?.92f:.31f+index*.105f));
        }
    }

    void ClearQuackGlass(){
        if(quackGlassMaterial)quackGlassMaterial.mainTexture=null;
        if(!quackVictorySnapshot)return;
        quackVictorySnapshot.Release();
        Destroy(quackVictorySnapshot);
        quackVictorySnapshot=null;
    }

    void PlayQuackCroak(float gain,float pitch=1f){
        if(!quackSound||!quackCroak)return;
        quackSound.pitch=pitch;
        quackSound.PlayOneShot(quackCroak,options.volume*gain);
    }

    void UpdateQuackEmoji(){
        int quackTricks=match.QuackInfiltrations+match.QuackRescueBackfires;
        if(quackTricks==quackEmojiSeen)return;
        quackEmojiSeen=quackTricks;quackEmojiAt=Time.unscaledTime;
        PlayQuackCroak(.83f);
    }

    Rect QuackScreenRect()=>new Rect(-guiOrigin.x/guiScale,-guiOrigin.y/guiScale,Screen.width/guiScale,Screen.height/guiScale);

    void DrawQuackScene(){
        var screen=QuackScreenRect();
        if(quackScene==QuackScene.Reversal&&quackSceneAge<QuackRitual){
            if(quackSceneAge<QuackBreak){
                Fill(screen,new Color(.015f,.022f,.015f,.73f));
                AtlasSurface(new Rect(410,145,460,445),true);Accent(450,187,50);
                Text(450,215,380,60,"Победа",heading);
                Text(450,290,380,75,"Серия завершена · счёт 1:0\nВремя: "+TimeSpan.FromSeconds(match.Elapsed).ToString(@"mm\:ss"),label);
                if(quackSceneAge>=QuackFirstCrack)DrawQuackCracks(screen);
                return;
            }
            var area=new Rect(0,0,1280,720);
            Fill(screen,Color.black);
            if(quackSceneAge<QuackMeme){
                DrawQuackShards(screen);
                return;
            }
            float fade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(QuackMeme,QuackMeme+.27f,quackSceneAge));
            var old=GUI.color;
            GUI.color=new Color(1,1,1,fade);
            if(quackReveal)GUI.DrawTexture(area,quackReveal,ScaleMode.ScaleToFit);
            else Text(200,430,880,120,"I hate speedrunners",title);
            GUI.color=old;
            return;
        }
        bool opening=quackScene==QuackScene.Opening;
        float handoff=opening?QuackHandoff(quackSceneAge):0;
        float bandTop=Mathf.Lerp(65,screen.y,handoff);
        float bandBottom=Mathf.Lerp(611,screen.yMax,handoff);
        if(opening){
            if(quackSceneAge>=1.15f&&quackSceneAge<4.85f)DrawQuackSpeech();
            float mist=Mathf.SmoothStep(0,1,1-Mathf.Abs(quackSceneAge-QuackSwampCue)/.42f);
            if(mist>0)Fill(new Rect(screen.x,bandTop,screen.width,bandBottom-bandTop),new Color(.45f,.64f,.49f,mist*.48f));
        }
        Fill(new Rect(screen.x,screen.y,screen.width,bandTop-screen.y),Color.black);
        Fill(new Rect(screen.x,bandBottom,screen.width,screen.yMax-bandBottom),Color.black);
        var tint=GUI.color;
        GUI.color=new Color(tint.r,tint.g,tint.b,tint.a*(1-handoff));
        string caption=quackScene==QuackScene.Opening
            ?(quackSceneAge<5?"Дождь Болотного колокола":"Братство выходит на охоту")
            :(quackSceneAge<QuackPoolsShot?"Великий Квак взывает к ливню":"Ливень накрывает болото");
        FitBossText(new Rect(49,628,910,52),caption,new GUIStyle(heading){normal={textColor=QuackGreen}},26);
        if(LobbyButton("quack.skip",new Rect(1061,642,167,42),"Пропустить"))quackSkip=true;
        GUI.color=tint;
    }

    void DrawQuackSpeech(){
        if(!quackKing||!Camera.main)return;
        var position=Camera.main.WorldToScreenPoint(quackKingHead+Vector3.up*.8f);
        float x=Mathf.Clamp((position.x-guiOrigin.x)/guiScale-112,30,1030);
        float y=Mathf.Clamp((Screen.height-position.y-guiOrigin.y)/guiScale-88,75,500);
        var rect=new Rect(x,y,212,84);
        GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.92f,.91f,.76f),0,19);
        var mark=AbilityArt("PTmp");
        var shield=AbilityArt("PGSh");
        if(mark)GUI.DrawTexture(new Rect(x+12,y+16,52,52),mark,ScaleMode.ScaleToFit);
        if(quackSpeechCue>=2&&shield)GUI.DrawTexture(new Rect(x+80,y+16,52,52),shield,ScaleMode.ScaleToFit);
        if(quackSpeechCue>=3&&quackEmoji)GUI.DrawTexture(new Rect(x+146,y+14,56,56),quackEmoji,ScaleMode.ScaleToFit);
        GUI.DrawTexture(new Rect(x+77,y+76,21,21),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.92f,.91f,.76f),0,11);
    }

    void DrawQuackCracks(Rect screen){
        float age=Mathf.InverseLerp(QuackFirstCrack,QuackBreak,quackSceneAge);
        var origin=QuackCrackOrigin;
        for(int i=0;i<QuackCrackRays.Length;i++){
            var tip=QuackFractureEdge(screen,i);
            var ray=tip-origin;
            var knee=origin+ray*.42f+new Vector2(i%2==0?29:-25,-19);
            float reach=Mathf.Clamp01((age-(i*3%8)*.047f)*1.54f);
            if(reach<=0)continue;
            DrawQuackCrack(origin,Vector2.Lerp(origin,knee,reach*2.2f),.84f,2f);
            if(reach<=.42f)continue;
            float growth=Mathf.InverseLerp(.42f,1,reach);
            DrawQuackCrack(knee,Vector2.Lerp(knee,tip,growth),.80f,1.5f);
            DrawQuackCrack(knee,knee+(ray*.23f+new Vector2(i%2==0?-53:51,26))*growth,.64f,1.25f);
        }
    }

    static readonly Vector2 QuackCrackOrigin=new Vector2(667,341);
    Vector2 QuackFractureEdge(Rect screen,int index){
        var point=QuackCrackRays[index];
        return new Vector2(screen.x+point.x/1280*screen.width,screen.y+point.y/720*screen.height);
    }

    void DrawQuackCrack(Vector2 start,Vector2 end,float alpha,float width){
        var saved=GUI.matrix;
        var step=end-start;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(step.y,step.x)*Mathf.Rad2Deg,start);
        Fill(new Rect(start.x,start.y,step.magnitude,width),new Color(.87f,.98f,1,alpha));
        GUI.matrix=saved;
    }

    void DrawQuackShards(Rect screen){
        if(Event.current.type!=EventType.Repaint)return;
        float age=Mathf.InverseLerp(QuackBreak,QuackMeme,quackSceneAge);
        float flash=Mathf.Clamp01(1-age*7);
        if(flash>0)Fill(screen,new Color(.67f,.84f,.88f,flash*.29f));
        if(!quackVictorySnapshot)return;
        if(!quackGlassMaterial){
            var shader=Shader.Find("Sprites/Default");
            if(!shader)return;
            quackGlassMaterial=new Material(shader);
        }
        quackGlassMaterial.mainTexture=quackVictorySnapshot;
        if(!quackGlassMaterial.SetPass(0))return;
        GL.PushMatrix();
        GL.LoadPixelMatrix(0,Screen.width,Screen.height,0);
        GL.Begin(GL.TRIANGLES);
        for(int i=0;i<QuackCrackRays.Length;i++){
            var a=QuackFractureEdge(screen,i);
            var b=QuackFractureEdge(screen,(i+1)%QuackCrackRays.Length);
            var center=(QuackCrackOrigin+a+b)/3;
            var hinge=QuackCrackOrigin+(Vector2.Lerp(a,b,.49f)-QuackCrackOrigin)*.42f;
            var exit=(center-QuackCrackOrigin).normalized;
            var shift=exit*age*(95+29*i)+Vector2.up*(age*age*260);
            var turn=Quaternion.Euler(0,0,age*(i%2==0?1:-1)*(14+3*i));
            GL.Color(new Color(.89f,.96f,1,Mathf.Pow(1-age,.63f)));
            QuackShardVertex(QuackCrackOrigin,center,shift,turn,screen);
            QuackShardVertex(a,center,shift,turn,screen);
            QuackShardVertex(hinge,center,shift,turn,screen);
            QuackShardVertex(a,center,shift,turn,screen);
            QuackShardVertex(b,center,shift,turn,screen);
            QuackShardVertex(hinge,center,shift,turn,screen);
            QuackShardVertex(hinge,center,shift,turn,screen);
            QuackShardVertex(b,center,shift,turn,screen);
            QuackShardVertex(QuackCrackOrigin,center,shift,turn,screen);
        }
        GL.End();
        GL.PopMatrix();
    }

    void QuackShardVertex(Vector2 point,Vector2 center,Vector2 shift,Quaternion turn,Rect screen){
        var moved=center+shift+(Vector2)(turn*(point-center));
        float v=(point.y-screen.y)/screen.height;
        GL.TexCoord2((point.x-screen.x)/screen.width,SystemInfo.graphicsUVStartsAtTop?v:1-v);
        GL.Vertex3(guiOrigin.x+moved.x*guiScale,guiOrigin.y+moved.y*guiScale,0);
    }

    void DrawQuackRain(bool? spectatorDownpour=null){
        if(!quackSwampRevealed||Event.current.type!=EventType.Repaint)return;
        bool storm=spectatorDownpour??match.QuackDownpour;
        var rain=storm?quackDownpour:quackDrizzle;
        if(!rain)return;
        var screen=QuackScreenRect();
        if(storm)Fill(screen,new Color(.016f,.038f,.075f,.36f));
        var previous=GUI.color;
        float time=Time.unscaledTime;
        GUI.color=new Color(.77f,.94f,1,storm?.74f:.48f);
        GUI.DrawTextureWithTexCoords(screen,rain,new Rect(time*(storm?.54f:.13f),time*(storm?1.7f:.87f),screen.width/256,screen.height/240));
        GUI.color=previous;
    }

    void DrawQuackHud(){
        if(quackHudFrame)GUI.DrawTexture(QuackHud,quackHudFrame,ScaleMode.StretchToFill);
        else AtlasPanel(QuackHud,new Color(.027f,.077f,.041f,.98f),new Color(.49f,.65f,.27f));
        var caption=new Rect(QuackHud.x+82,QuackHud.y+31,174,31);
        var captionStyle=new GUIStyle(heading){alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.91f,.95f,.75f)}};
        FitBossText(caption,(SpectatorSceneReady?spectatorStorm:match.QuackDownpour)?"КВАА!":"Ква-ква??",captionStyle,23);
        float age=Time.unscaledTime-quackEmojiAt;
        if(age>=2f||!quackEmoji)return;
        float scale=Mathf.Lerp(.76f,1,Mathf.SmoothStep(0,1,Mathf.Clamp01(age*5)));
        float size=124*scale;
        var old=GUI.color;GUI.color=new Color(1,1,1,Mathf.Clamp01((2-age)*3.5f));
        GUI.DrawTexture(new Rect(640-size*.5f,96+(124-size)*.5f,size,size),quackEmoji,ScaleMode.ScaleToFit);
        GUI.color=old;
    }

    static Texture2D MakeQuackRain(bool storm){
        const int size=256;
        var pixels=new Color32[size*size];
        var random=new System.Random(storm?4914:4913);
        int drops=storm?103:58;
        for(int i=0;i<drops;i++){
            int x=random.Next(size),y=random.Next(size),length=storm?random.Next(13,29):random.Next(4,9);
            for(int n=0;n<length;n++){
                byte alpha=(byte)((storm?184:156)*Mathf.Sin(Mathf.PI*(n+.5f)/length));
                int index=((y-n+size)%size)*size+(x+n/(storm?3:6))%size;
                if(alpha>pixels[index].a)pixels[index]=new Color32(208,238,233,alpha);
            }
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Дождь ряски",wrapMode=TextureWrapMode.Repeat};
        texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
    }

    static AudioClip LoadQuackSound(string name,string title){
        var asset=Resources.Load<TextAsset>("WinterBattle/"+name);
        string path=MobileContent.Media("Frontend/"+name+".pcm");
        var bytes=asset?asset.bytes:File.Exists(path)?File.ReadAllBytes(path):null;
        if(bytes==null||bytes.Length<2||bytes.Length%2!=0)return null;
        var samples=new float[bytes.Length/2];
        for(int i=0;i<samples.Length;i++)
            samples[i]=unchecked((short)(bytes[i*2]|bytes[i*2+1]<<8))/32768f;
        var clip=AudioClip.Create(title,samples.Length,1,24000,false);
        clip.SetData(samples,0);
        return clip;
    }

    void ClearQuackActors(){
        quackFacePose=false;
        if(quackRitualEffect)Destroy(quackRitualEffect);
        quackRitualEffect=null;
        if(quackKing)Destroy(quackKing.gameObject);
        quackKing=null;
        for(int i=0;i<quackGuards.Length;i++){
            if(quackGuards[i])Destroy(quackGuards[i].gameObject);
            quackGuards[i]=null;
        }
    }

    void ClearQuack(){
        if(quackLighting){RenderSettings.ambientLight=quackDay;if(quackSun)quackSun.intensity=quackSunIntensity;}
        quackLighting=false;quackSun=null;quackScene=QuackScene.None;quackSound=null;
        RestoreQuackCastle();ClearQuackGlass();ClearQuackActors();ClearQuackSwamp();
    }

    void DisposeQuack(){
        ClearQuack();
        ClearGreatQuackArt();
        if(quackReveal)Destroy(quackReveal);
        if(quackEmoji)Destroy(quackEmoji);
        if(quackDrizzle)Destroy(quackDrizzle);
        if(quackDownpour)Destroy(quackDownpour);
        if(quackHudFrame)Destroy(quackHudFrame);
        if(quackPainting)Destroy(quackPainting);
        if(quackCroak)Destroy(quackCroak);
        if(quackCrackSound)Destroy(quackCrackSound);
        if(quackGlassSound)Destroy(quackGlassSound);
        if(quackGlassMaterial)Destroy(quackGlassMaterial);
    }
}
}
