using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    Texture2D menuArtwork;
    AudioSource menuAudio;
    Texture2D menuMist;
    float landscapeMotion;

    void StartFrontend() {
        if(!FindAnyObjectByType<AudioListener>()){var host=Camera.main?Camera.main.gameObject:gameObject;host.AddComponent<AudioListener>();Debug.Log("CF_PC_AUDIO_LISTENER_RESTORED");}
        menuArtwork=ContentResources.Load<Texture2D>("PcMenuArtwork");
        var clip=ContentResources.Load<AudioClip>("PcMenuMusic");
        if(clip) { menuAudio=gameObject.AddComponent<AudioSource>(); menuAudio.clip=clip; menuAudio.loop=true; menuAudio.playOnAwake=false; menuAudio.volume=0;menuAudio.spatialBlend=0;menuAudio.ignoreListenerPause=true;clip.LoadAudioData(); }
        networkName=PlayerPrefs.GetString("cf-network-name","");if(networkName=="Игрок")networkName="";
        if(!testing&&BattleLab.Arg("--pc-test")=="")RestoreAccountMode(NetworkAccountStore.GuestMode);
    }
    // Godot MenuMusic.continue_after_intro: the soundtrack resumes at the frame the
    // intro movie stopped at instead of restarting from the beginning.
    void BeginMenuMusic(float offset) {
        if(!menuAudio||!menuAudio.clip)return;
        menuAudio.time=Mathf.Repeat(Mathf.Max(0f,offset),Mathf.Max(.01f,menuAudio.clip.length));
        menuAudio.volume=options.musicVolume;AudioListener.pause=false;menuAudio.Play();Debug.Log("CF_PC_MENU_MUSIC_PLAY state="+menuAudio.clip.loadState+" offset="+menuAudio.time);
    }
    void UpdateFrontend() {
        landscapeMotion=Mathf.MoveTowards(landscapeMotion,page==Page.Main?1f:0f,Time.unscaledDeltaTime*1.8f);
        if(menuAudio) {
            bool inMenu=page==Page.Guide||aspectWaiting||page==Page.Progression||page==Page.Profile||page==Page.Main||page==Page.Setup||page==Page.Network||(page==Page.Settings&&settingsReturn!=Page.Pause);
            menuAudio.volume=Mathf.MoveTowards(menuAudio.volume,inMenu?options.musicVolume:0,Time.unscaledDeltaTime*.5f);
        }
        PollLobby();
        UpdateAccountHub();
        UpdateSocialNotifications();
    }
    void DrawFrontendBleed() {
        if(Event.current.type!=EventType.Repaint)return;
        bool atlas=page==Page.Main||page==Page.Setup||page==Page.Progression||
            page==Page.Profile||page==Page.Network||page==Page.Guide||page==Page.Settings&&settingsReturn==Page.Main;
        if(atlas)PrepareCampaignArtwork();
        var backdrop=atlas&&campaignMap?campaignMap:menuArtwork;
        if(atlas&&campaignMap&&landscapeMotion>0)DrawLivingLandscape();
        else if(backdrop)GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),backdrop,ScaleMode.ScaleAndCrop);
        else {GUI.color=new Color(.015f,.025f,.03f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;}
    }
    static Rect LandscapeFrame(Vector2 viewport,Vector2 image,float time,float strength){
        float scale=Mathf.Max(viewport.x/image.x,viewport.y/image.y);
        var size=image*scale*(1+strength*(.040f+.008f*Mathf.Sin(time*.18f)));
        var shift=new Vector2(viewport.x*.012f*Mathf.Sin(time*.24f),viewport.y*.008f*Mathf.Sin(time*.16f))*strength;
        return new Rect((viewport-size)*.5f+shift,size);
    }
    void DrawLivingLandscape(){
        float time=Time.realtimeSinceStartup;
        var frame=LandscapeFrame(new Vector2(Screen.width,Screen.height),new Vector2(campaignMap.width,campaignMap.height),time,landscapeMotion);
        GUI.DrawTexture(frame,campaignMap,ScaleMode.StretchToFill);
        if(!menuMist)menuMist=CreateLandscapeMist();
        var oldColor=GUI.color;
        DrawLandscapeMist(frame,time,.14f,.27f,.43f,.13f,0f,.26f);
        DrawLandscapeMist(frame,time,.72f,.23f,.44f,.11f,2.1f,.23f);
        DrawLandscapeMist(frame,time,.57f,.43f,.32f,.09f,3.9f,.16f);
        GUI.color=oldColor;
    }
    void DrawLandscapeMist(Rect frame,float time,float x,float y,float width,float height,float phase,float opacity){
        float drift=Mathf.Sin(time*.115f+phase);
        var rect=new Rect(frame.x+frame.width*(x-width*.5f+.045f*drift),
            frame.y+frame.height*(y-height*.5f+.012f*Mathf.Cos(time*.14f+phase)),frame.width*width,frame.height*height);
        GUI.color=new Color(.91f,.94f,1f,opacity*landscapeMotion*(.8f+.2f*Mathf.Cos(time*.19f+phase)));
        GUI.DrawTexture(rect,menuMist,ScaleMode.StretchToFill);
    }
    static Texture2D CreateLandscapeMist(){
        const int width=128,height=64;
        var pixels=new Color32[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){
            float u=x/(width-1f),v=y/(height-1f);
            float envelope=Mathf.Clamp01(1-4*((u-.5f)*(u-.5f)+(v-.5f)*(v-.5f)));
            float noise=Mathf.PerlinNoise(u*5,v*3)*.65f+Mathf.PerlinNoise(u*11+7,v*7)*.35f;
            pixels[y*width+x]=new Color32(255,255,255,(byte)(255*envelope*envelope*noise));
        }
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name="Campaign mist",wrapMode=TextureWrapMode.Clamp};
        texture.SetPixels32(pixels);texture.Apply(false,true);
        return texture;
    }
    void DrawMenuBackground() {
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.18f));
    }
    void DisposeFrontend(){StopDirectory();DisposeAccountAvatars();DisposeStickers();DisposeCampaignArtwork();ReleaseRaceArtwork();DisposeLoadingArt();if(menuAudio)Destroy(menuAudio);if(menuMist)Destroy(menuMist);if(notificationChime)Destroy(notificationChime);DisposeWinterArtwork();DisposeHordeArtwork();}
    // Only the current pair is decoded; fifteen source paintings stay on disk.
    static readonly string[] VersusKeys={"human","orc","naga","nature","chaos","northern","mech","undead","corrupted","elemental","night","elven","ultimate","nerubian","pandaren","centaur","pepe","blackhand"};
    static readonly string[] VersusNames={"Альянс людей","Орочья Орда","Наги","Силы природы","Легионы Хаоса","Северные королевства","Коалиция механизмов","Силы нежити","Осквернённые","Мир элементалей","Ночные эльфы","Союз эльфов","Высший строитель","Рой нерубов","Орден пандаренов","Ханства Пустошей","Болотное братство Пепе","Клан Чёрной Руки"};
    const float VersusReadyDuration=1.5f;
    float versusReadyVisible;
    int versusReadyDrawFrame;
    Texture2D versusLeft,versusRight,versusLeftFallback,versusRightFallback;int versusA,versusB,versusSerial;float versusAt;bool versusActive,versusPrepared,versusNetwork;string versusNameA,versusNameB;
    void DisposeVersusArt(){
        if(versusLeft&&versusA!=PepeContent.Race&&versusA!=BlackHand.Race)Destroy(versusLeft);
        if(versusRight&&versusB!=PepeContent.Race&&versusB!=BlackHand.Race)Destroy(versusRight);
        if(versusRightFallback&&versusRightFallback!=versusLeftFallback)Destroy(versusRightFallback);
        if(versusLeftFallback)Destroy(versusLeftFallback);
        versusLeft=versusRight=versusLeftFallback=versusRightFallback=null;
    }
    void CloseVersus(){
        versusSerial++;versusActive=false;versusPrepared=false;mobileRoundPrepared=false;
        if(MobileContent.Active)MobileContent.Current.VersusLoading=false;
        DisposeVersusArt();
        versusWinter=choirVersus=hordeVersus=protocolVersus=abyssVersus=quackVersus=false;
    }
    void BeginVersus(int a,int b,bool online){
        DisposeVersusArt();versusSerial++;versusA=Mathf.Clamp(a,0,VersusKeys.Length-1);versusB=Mathf.Clamp(b,0,VersusKeys.Length-1);versusActive=true;versusPrepared=false;versusNetwork=online;versusAt=Time.realtimeSinceStartup;versusReadyVisible=0;versusReadyDrawFrame=-1;
        versusNameA=online?ResultPlayerName(networkSlot):"Вы";versusNameB=online?ResultPlayerName(1-networkSlot):"Компьютер";
        choirVersus=!online&&(string)activeChallenge?["id"]==HumanMatch.ChoirTrial;
        hordeVersus=!online&&(string)activeChallenge?["id"]==HumanMatch.HordeTrial;
        protocolVersus=!online&&ActiveProtocolTrial;
        abyssVersus=!online&&ActiveAbyssTrial;
        quackVersus=!online&&(string)activeChallenge?["id"]==HumanMatch.QuackTrial;
        versusWinter=quackVersus||abyssVersus||protocolVersus||choirVersus||hordeVersus||!online&&(string)activeChallenge?["id"]==HumanMatch.WinterTrial;
        winterVersusFrame=-1;winterVersusReveal=0;
        if(versusWinter&&!quackVersus&&!choirVersus&&!hordeVersus&&!protocolVersus&&!abyssVersus)WinterArtwork();
        if(MobileContent.Active){
            MobileContent.Current.VersusLoading=true;
            PrepareVersusFallbacks();
        }
    }
    void PrepareVersusFallbacks(){
        versusLeftFallback=MobileVersusPicture(versusA);
        versusRightFallback=versusWinter?null:versusB==versusA?versusLeftFallback:MobileVersusPicture(versusB);
    }

    static Texture2D MobileVersusPicture(int race){
        if(race==BlackHand.Race||race==PepeContent.Race)return null;
        var preview=Resources.Load<TextAsset>("MobileVersus/"+VersusKeys[race]);
        if(!preview)return null;
        var portrait=new Texture2D(2,2,TextureFormat.RGB24,false);
        if(portrait.LoadImage(preview.bytes,true))return portrait;
        Destroy(portrait);
        return null;
    }

    IEnumerator LoadVersusArt(int generation){
        for(int side=0;side<(versusWinter?1:2);side++){
            int race=side==0?versusA:versusB;
            if(race==PepeContent.Race||race==BlackHand.Race){
                var painting=race==BlackHand.Race?BlackHandUi.Versus():PepeContent.Load<Texture2D>("Pepe/Versus-pepe");
                if(painting){if(side==0)versusLeft=painting;else versusRight=painting;}
                continue;
            }
            string path=MobileContent.MediaRoot+"/Frontend/Versus/"+VersusKeys[race]+".png";
            if(MobileContent.Active){
                var fallback=side==0?versusLeftFallback:versusRightFallback;
                if(!File.Exists(path)){
                    if(fallback)continue;
                    yield return MobileContent.Current.EnsureMedia("Frontend/Versus/"+VersusKeys[race]+".png");
                }
                path=new Uri(path).AbsoluteUri;
            }
            byte[] data=null;
            if(Application.isMobilePlatform){using(var req=UnityEngine.Networking.UnityWebRequest.Get(path)){yield return req.SendWebRequest();if(req.result==UnityEngine.Networking.UnityWebRequest.Result.Success)data=req.downloadHandler.data;}}
            else if(System.IO.File.Exists(path))data=System.IO.File.ReadAllBytes(path);
            if(generation!=versusSerial||!versusActive)yield break;
            if(data!=null){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);if(t.LoadImage(data,true)){t.filterMode=FilterMode.Bilinear;if(side==0)versusLeft=t;else versusRight=t;}else Destroy(t);}
            yield return null;
        }
    }
    void BeginOfflineVersus(bool next=false){if(versusActive)return;if(testing&&(string)activeChallenge?["id"]!=HumanMatch.WinterTrial&&(string)activeChallenge?["id"]!=HumanMatch.ChoirTrial&&(string)activeChallenge?["id"]!=HumanMatch.HordeTrial&&!ActiveProtocolTrial&&!ActiveAbyssTrial&&(string)activeChallenge?["id"]!=HumanMatch.QuackTrial){if(next)StartRound();else NewMatch();return;}StartCoroutine(OfflineVersus(next));}
    IEnumerator OfflineVersus(bool next){
        if(matchMode==1){raceChoice=RandomStagedRace(-1);botRaceChoice=RandomStagedRace(raceChoice);mobileRoundPrepared=true;}
        BeginVersus(raceChoice,matchMode==2?raceChoice:botRaceChoice<0?raceChoice:botRaceChoice,false);int generation=versusSerial;
        yield return LoadVersusArt(generation);yield return new WaitForEndOfFrame();if(generation!=versusSerial)yield break;
        if(MobileContent.Active){yield return PrepareMobileMatch(versusA,versusB);yield return PrepareUltimateDraw034(versusA,versusB);MobileContent.Current.Ready();}
        else yield return PrepareDesktopMatch(versusA,versusB,generation);
        if(generation!=versusSerial){mobileRoundPrepared=false;yield break;}
        if(next)StartRound();else NewMatch();
        if(matchRace!=versusA||matchBotRace!=versusB){DisposeVersusArt();versusA=matchRace;versusB=matchBotRace;if(MobileContent.Active)PrepareVersusFallbacks();yield return LoadVersusArt(generation);}
        if(match.FuneralChoir){yield return PrepareRitualGround(generation,true);if(generation!=versusSerial)yield break;yield return PrepareChoirMusic(generation);if(generation!=versusSerial)yield break;}
        if(match.CursedHorde){
            yield return PrepareHordeGround(generation);
            if(generation!=versusSerial)yield break;
            yield return PrepareBossSoundtrack(generation);
            if(generation!=versusSerial)yield break;
        }
        if(match.LastProtocol||match.AbyssTrialActive||match.QuackActive){
            yield return PrepareBossSoundtrack(generation);
            if(generation!=versusSerial)yield break;
        }
        if(match.EternalWinter){
            yield return PrepareWinterPresentation(generation);
            if(generation!=versusSerial)yield break;
        }
        RenderMatch();versusPrepared=true;
        yield return HoldReadyVersus(generation);
        if(generation==versusSerial){
            if(winterIntro)FrameWinterOpening();
            if(protocolIntro)UpdateProtocolIntro(false);
            if(abyssIntro)UpdateAbyssIntro(false);
            CloseVersus();
            ReleaseWinterArtwork();
            if(winterIntro||hordeIntro||protocolIntro||abyssIntro||match.QuackActive)StartBossSoundtrack();
        }
    }

    IEnumerator HoldReadyVersus(int generation){
        while(versusActive&&generation==versusSerial&&versusReadyVisible<VersusReadyDuration)
            yield return null;
    }

    IEnumerator NetworkVersus(JObject packet){
        var races=packet["races"] as Newtonsoft.Json.Linq.JArray;
        int a=races?.Count==2&&networkSlot>=0?Array.IndexOf(networkRaceNames,(string)races[networkSlot]):-1;
        int b=races?.Count==2&&networkSlot>=0?Array.IndexOf(networkRaceNames,(string)races[1-networkSlot]):-1;
        if(a<0||b<0){NetworkLoadCore(packet);yield break;}
        BeginVersus(a,b,true);int generation=versusSerial;
        yield return LoadVersusArt(generation);yield return new WaitForEndOfFrame();if(generation!=versusSerial)yield break;
        if(MobileContent.Active){yield return PrepareMobileMatch(a,b);yield return PrepareUltimateDraw034(a,b,packet["ultimate_loadouts"] as JArray);MobileContent.Current.Ready();}
        else yield return PrepareDesktopMatch(a,b,generation);
        if(generation!=versusSerial)yield break;
        NetworkLoadCore(packet);if(!onlineMatch){CloseVersus();yield break;}RenderMatch();versusPrepared=true;
    }
    void DrawVersus(){
        if(versusPrepared&&Event.current.type==EventType.Repaint&&versusReadyDrawFrame!=Time.frameCount){
            if(versusReadyDrawFrame>=0)versusReadyVisible+=Mathf.Min(Time.unscaledDeltaTime,.1f);
            versusReadyDrawFrame=Time.frameCount;
        }
        if(versusWinter){DrawWinterVersus();return;}
        Fill(new Rect(0,0,1280,720),new Color(.012f,.018f,.024f));
        float entrance=Mathf.SmoothStep(0,1,(Time.realtimeSinceStartup-versusAt)/.55f);
        VersusPainting(new Rect(-24*(1-entrance),0,608,720),versusLeft?versusLeft:versusLeftFallback,false);
        VersusPainting(new Rect(672+24*(1-entrance),0,608,720),versusRight?versusRight:versusRightFallback,true);
        var gold=AtlasGold;for(int i=0;i<16;i++){float alpha=(1-i/16f)*.85f;Fill(new Rect(592-i*3,0,3,720),new Color(.012f,.018f,.024f,alpha));Fill(new Rect(685+i*3,0,3,720),new Color(.012f,.018f,.024f,alpha));}
        AtlasSurface(new Rect(0,0,1280,58));
        Text(240,12,800,36,"ПРОТИВОСТОЯНИЕ",new GUIStyle(heading){fontSize=27,alignment=TextAnchor.MiddleCenter,normal={textColor=gold}});
        Fill(new Rect(639,80,2,162),new Color(.66f,.51f,.28f,.5f));Fill(new Rect(639,401,2,160),new Color(.66f,.51f,.28f,.5f));
        Text(542,263,196,112,"VS",new GUIStyle(title){fontSize=82,alignment=TextAnchor.MiddleCenter,normal={textColor=gold}});
        Text(550,377,180,32,"1 × 1",new GUIStyle(label){alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.76f,.8f,.83f)}});
        for(int side=0;side<2;side++){
            float x=side==0?42:714;AtlasSurface(new Rect(x-12,521,548,109));
            int race=side==0?versusA:versusB;
            var emblem=RaceEmblem(race);
            if(emblem)GUI.DrawTexture(new Rect(x+8,545,57,57),emblem,ScaleMode.ScaleToFit);
            Text(x+64,534,432,40,VersusNames[race],new GUIStyle(heading){fontSize=29,alignment=TextAnchor.MiddleCenter,normal={textColor=gold}});
            Text(x,580,500,33,side==0?versusNameA:versusNameB,new GUIStyle(label){fontSize=22,alignment=TextAnchor.MiddleCenter});
        }
        AtlasSurface(new Rect(0,637,1280,83));
        DrawVersusLoading(gold);
    }
    void DrawVersusLoading(Color gold){
        var mobile=MobileContent.Current;
        bool loading=mobile&&mobile.Busy;
        string error=loading?mobile.LoadingError:"";
        bool retry=!string.IsNullOrEmpty(error);
        bool ready=versusPrepared;
        string phase=ready?(versusNetwork&&!onlineStarted?"Ресурсы готовы · ждём соперника":"Поле боя готово"):
            loading?mobile.LoadingStatus.Replace("\n"," · "):winterIntro&&!versusNetwork?"Готовим снег и метель":"Подготовка ресурсов";
        Text(275,646,730,38,retry?error:phase,new GUIStyle(retry?small:label){
            alignment=TextAnchor.MiddleCenter,wordWrap=true
        });
        Fill(new Rect(320,693,640,4),new Color(.12f,.16f,.18f));
        float progress=ready?1:loading?Mathf.Clamp01(mobile.LoadingProgress):0;
        if(progress>0)Fill(new Rect(320,693,640*progress,4),gold);
        else if(!retry){
            float x=320+Mathf.PingPong(Time.realtimeSinceStartup*.65f,1)*480;
            Fill(new Rect(x,693,160,4),gold);
        }
        if(retry&&LobbyButton("versus.retry",new Rect(1020,639,220,33),"Повторить"))mobile.RetryLoading();
        if(versusNetwork&&Time.realtimeSinceStartup-versusAt>12&&
            LobbyButton("versus.leave",new Rect(1020,retry?677:655,220,retry?34:48),"Выйти в меню")){
            CloseVersus();
            NetworkReturnToMenu();
        }
    }
    void VersusPainting(Rect rect,Texture2D texture,bool mirror){if(!texture)return;float visible=Mathf.Min(1,rect.height/(rect.width*texture.height/texture.width));var uv=mirror?new Rect(1,1-visible,-1,visible):new Rect(0,1-visible,1,visible);GUI.DrawTextureWithTexCoords(rect,texture,uv,false);}

}
}
