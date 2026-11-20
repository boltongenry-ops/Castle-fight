using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    Texture2D menuArtwork;
    AudioSource menuAudio;

    void StartFrontend() {
        if(!FindAnyObjectByType<AudioListener>()){var host=Camera.main?Camera.main.gameObject:gameObject;host.AddComponent<AudioListener>();Debug.Log("CF_PC_AUDIO_LISTENER_RESTORED");}
        menuArtwork=ContentResources.Load<Texture2D>("PcMenuArtwork");
        var clip=ContentResources.Load<AudioClip>("PcMenuMusic");
        if(clip) { menuAudio=gameObject.AddComponent<AudioSource>(); menuAudio.clip=clip; menuAudio.loop=true; menuAudio.playOnAwake=false; menuAudio.volume=0;menuAudio.spatialBlend=0;menuAudio.ignoreListenerPause=true;clip.LoadAudioData(); }
        networkName=PlayerPrefs.GetString("cf-network-name","");if(networkName=="Игрок")networkName="";
    }
    // Godot MenuMusic.continue_after_intro: the soundtrack resumes at the frame the
    // intro movie stopped at instead of restarting from the beginning.
    void BeginMenuMusic(float offset) {
        if(!menuAudio||!menuAudio.clip)return;
        menuAudio.time=Mathf.Repeat(Mathf.Max(0f,offset),Mathf.Max(.01f,menuAudio.clip.length));
        menuAudio.volume=options.musicVolume;AudioListener.pause=false;menuAudio.Play();Debug.Log("CF_PC_MENU_MUSIC_PLAY state="+menuAudio.clip.loadState+" offset="+menuAudio.time);
    }
    void UpdateFrontend() {
        if(menuAudio) {
            bool inMenu=page==Page.Guide||aspectWaiting||page==Page.Progression||page==Page.Main||page==Page.Setup||page==Page.Network||(page==Page.Settings&&settingsReturn!=Page.Pause);
            menuAudio.volume=Mathf.MoveTowards(menuAudio.volume,inMenu?options.musicVolume:0,Time.unscaledDeltaTime*.5f);
        }
        PollLobby();
    }
    void DrawFrontendBleed() {
        if(menuArtwork)GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),menuArtwork,ScaleMode.ScaleAndCrop);
        else {GUI.color=new Color(.015f,.025f,.03f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;}
    }
    void DrawMenuBackground() {
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.18f));
    }
    void DisposeFrontend(){StopDirectory();DisposeStickers();if(menuAudio)Destroy(menuAudio);}
    // Only the current pair is decoded; fifteen source paintings stay on disk.
    static readonly string[] VersusKeys={"human","orc","naga","nature","chaos","northern","mech","undead","corrupted","elemental","night","elven","ultimate","nerubian","pandaren","centaur","pepe"};
    static readonly string[] VersusNames={"Альянс людей","Орочья Орда","Наги","Силы природы","Легионы Хаоса","Северные королевства","Коалиция механизмов","Силы нежити","Осквернённые","Мир элементалей","Ночные эльфы","Союз эльфов","Высший строитель","Рой нерубов","Орден пандаренов","Ханства Пустошей","Болотное братство Пепе"};
    Texture2D versusLeft,versusRight;int versusA,versusB,versusSerial;float versusAt;bool versusActive,versusPrepared,versusNetwork;string versusNameA,versusNameB;
    void DisposeVersusArt(){if(versusLeft&&versusA!=PepeContent.Race)Destroy(versusLeft);if(versusRight&&versusB!=PepeContent.Race)Destroy(versusRight);versusLeft=versusRight=null;}
    void CloseVersus(){versusSerial++;versusActive=false;versusPrepared=false;mobileRoundPrepared=false;DisposeVersusArt();}
    void BeginVersus(int a,int b,bool online){
        DisposeVersusArt();versusSerial++;versusA=Mathf.Clamp(a,0,VersusKeys.Length-1);versusB=Mathf.Clamp(b,0,VersusKeys.Length-1);versusActive=true;versusPrepared=false;versusNetwork=online;versusAt=Time.realtimeSinceStartup;
        versusNameA=online?ResultPlayerName(networkSlot):"Вы";versusNameB=online?ResultPlayerName(1-networkSlot):"Компьютер";
    }
    IEnumerator LoadVersusArt(int generation){
        for(int side=0;side<2;side++){
            int race=side==0?versusA:versusB;
            if(race==PepeContent.Race){
                var painting=PepeContent.Load<Texture2D>("Pepe/Versus-pepe");
                if(painting){if(side==0)versusLeft=painting;else versusRight=painting;}
                continue;
            }
            string path=MobileContent.MediaRoot+"/Frontend/Versus/"+VersusKeys[race]+".png";
            if(MobileContent.Active){yield return MobileContent.Current.EnsureMedia("Frontend/Versus/"+VersusKeys[side==0?versusA:versusB]+".png");path=new Uri(path).AbsoluteUri;}
            byte[] data=null;
            if(Application.isMobilePlatform){using(var req=UnityEngine.Networking.UnityWebRequest.Get(path)){yield return req.SendWebRequest();if(req.result==UnityEngine.Networking.UnityWebRequest.Result.Success)data=req.downloadHandler.data;}}
            else if(System.IO.File.Exists(path))data=System.IO.File.ReadAllBytes(path);
            if(generation!=versusSerial||!versusActive)yield break;
            if(data!=null){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);if(t.LoadImage(data,true)){t.filterMode=FilterMode.Bilinear;if(side==0)versusLeft=t;else versusRight=t;}else Destroy(t);}
            yield return null;
        }
    }
    void BeginOfflineVersus(bool next=false){if(versusActive)return;if(testing){if(next)StartRound();else NewMatch();return;}StartCoroutine(OfflineVersus(next));}
    IEnumerator OfflineVersus(bool next){
        if(matchMode==1){raceChoice=RandomStagedRace(-1);botRaceChoice=RandomStagedRace(raceChoice);mobileRoundPrepared=true;}
        BeginVersus(raceChoice,matchMode==2?raceChoice:botRaceChoice<0?raceChoice:botRaceChoice,false);int generation=versusSerial;
        yield return LoadVersusArt(generation);yield return new WaitForEndOfFrame();if(generation!=versusSerial)yield break;
        if(MobileContent.Active){yield return PrepareMobileMatch(versusA,versusB);yield return PrepareUltimateDraw034(versusA,versusB);MobileContent.Current.Ready();}
        else yield return PrepareDesktopMatch(versusA,versusB,generation);
        if(generation!=versusSerial){mobileRoundPrepared=false;yield break;}
        if(next)StartRound();else NewMatch();
        if(matchRace!=versusA||matchBotRace!=versusB){DisposeVersusArt();versusA=matchRace;versusB=matchBotRace;yield return LoadVersusArt(generation);}
        RenderMatch();versusPrepared=true;yield return new WaitForEndOfFrame();
        while(generation==versusSerial&&Time.realtimeSinceStartup-versusAt<1.5f)yield return null;
        if(generation==versusSerial)CloseVersus();
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
        Fill(new Rect(0,0,1280,720),new Color(.012f,.018f,.024f));
        float entrance=Mathf.SmoothStep(0,1,(Time.realtimeSinceStartup-versusAt)/.55f);
        VersusPainting(new Rect(-24*(1-entrance),0,608,720),versusLeft,false);VersusPainting(new Rect(672+24*(1-entrance),0,608,720),versusRight,true);
        var gold=new Color(.85f,.70f,.40f);for(int i=0;i<16;i++){float alpha=(1-i/16f)*.85f;Fill(new Rect(592-i*3,0,3,720),new Color(.012f,.018f,.024f,alpha));Fill(new Rect(685+i*3,0,3,720),new Color(.012f,.018f,.024f,alpha));}
        Fill(new Rect(0,0,1280,58),new Color(.008f,.014f,.022f,.68f));
        Text(240,12,800,36,"ПРОТИВОСТОЯНИЕ",new GUIStyle(heading){fontSize=27,alignment=TextAnchor.MiddleCenter,normal={textColor=gold}});
        Fill(new Rect(639,80,2,162),new Color(.66f,.51f,.28f,.5f));Fill(new Rect(639,401,2,160),new Color(.66f,.51f,.28f,.5f));
        Text(542,263,196,112,"VS",new GUIStyle(title){fontSize=82,alignment=TextAnchor.MiddleCenter,normal={textColor=gold}});
        Text(550,377,180,32,"1 × 1",new GUIStyle(label){alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.76f,.8f,.83f)}});
        for(int side=0;side<2;side++){
            float x=side==0?42:714;Fill(new Rect(x-12,521,548,109),new Color(.008f,.016f,.024f,.83f));Fill(new Rect(x,521,500,1),new Color(.75f,.59f,.32f,.7f));
            int race=side==0?versusA:versusB;
            var emblem=RaceEmblem(race);
            if(emblem)GUI.DrawTexture(new Rect(x+8,545,57,57),emblem,ScaleMode.ScaleToFit);
            Text(x+64,534,432,40,VersusNames[race],new GUIStyle(heading){fontSize=29,alignment=TextAnchor.MiddleCenter,normal={textColor=gold}});
            Text(x,580,500,33,side==0?versusNameA:versusNameB,new GUIStyle(label){fontSize=22,alignment=TextAnchor.MiddleCenter});
        }
        Fill(new Rect(0,637,1280,83),new Color(.008f,.014f,.021f,.97f));
        bool ready=versusPrepared;string phase=!ready?"Подготовка ресурсов":versusNetwork&&!onlineStarted?"Ресурсы готовы · ждём соперника":"Поле боя готово";
        Text(275,648,730,31,phase+new string('.',(int)(Time.realtimeSinceStartup*2)%4),new GUIStyle(label){alignment=TextAnchor.MiddleCenter});
        Fill(new Rect(320,693,640,4),new Color(.12f,.16f,.18f));
        if(ready)Fill(new Rect(320,693,640,4),gold);else {float x=320+Mathf.PingPong(Time.realtimeSinceStartup*.65f,1)*480;Fill(new Rect(x,693,160,4),gold);}
        if(versusNetwork&&Time.realtimeSinceStartup-versusAt>12&&LobbyButton("versus.leave",new Rect(1020,655,220,48),"Выйти в меню")){CloseVersus();NetworkReturnToMenu();}
    }
    void VersusPainting(Rect rect,Texture2D texture,bool mirror){if(!texture)return;float visible=Mathf.Min(1,rect.height/(rect.width*texture.height/texture.width));var uv=mirror?new Rect(1,1-visible,-1,visible):new Rect(0,1-visible,1,visible);GUI.DrawTextureWithTexCoords(rect,texture,uv,false);}

}
}
