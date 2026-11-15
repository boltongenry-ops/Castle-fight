using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;
namespace CastleFight {
// Godot startup_intro.gd parity: the source intro movie plays once per launch,
// covering the screen in 16:9, then the menu soundtrack continues from the frame
// the movie stopped at. Skipping is allowed only after one complete viewing.
public sealed partial class PcGame {
    const float IntroMusicOffset=70.52f;
    VideoPlayer introPlayer;RenderTexture introTexture;AudioSource introAudio;
    bool introPlaying,introLeaving,introCaptureDone;
    float introFade,introStall,introFurthest,introPlayedAt;
    string introCapturePath;

    bool IntroWanted() {
        if(testing)return false;
        bool forced=BattleLab.Arg("--pc-intro")!="";
        if(!forced&&(BattleLab.Arg("--pc-mode")!=""||BattleLab.Arg("--map-shot")!=""||BattleLab.Arg("--map-view")!=""||BattleLab.Arg("--pc-startup-shot")!=""))return false;
        return File.Exists(Path.Combine(MobileContent.MediaRoot,"Frontend/startup_intro.mp4"))||ContentResources.Load<VideoClip>("PcIntro")!=null;
    }
    void StartIntro(string capturePath) {
        string video=Path.Combine(MobileContent.MediaRoot,"Frontend/startup_intro.mp4");var clip=File.Exists(video)?null:ContentResources.Load<VideoClip>("PcIntro");
        if(!clip&&!File.Exists(video)){Debug.Log("CF_PC_INTRO_MISSING");return;}
        if(menuAudio)menuAudio.Stop();introLeaving=false;controls.InputBlocked=true;
        introCapturePath=capturePath;introPlaying=true;introFurthest=0;introStall=0;introFade=0;introCaptureDone=false;
        introTexture=new RenderTexture(1280,720,0){name="Startup intro"};introTexture.Create();
        var host=new GameObject("Startup intro");host.transform.SetParent(transform,false);
        introAudio=host.AddComponent<AudioSource>();introAudio.playOnAwake=false;introAudio.volume=options.musicVolume;introAudio.spatialBlend=0;introAudio.ignoreListenerPause=true;
        introPlayer=host.AddComponent<VideoPlayer>();
        introPlayer.playOnAwake=false;introPlayer.source=clip?VideoSource.VideoClip:VideoSource.Url;if(clip)introPlayer.clip=clip;else introPlayer.url=video;
        introPlayer.renderMode=VideoRenderMode.RenderTexture;introPlayer.targetTexture=introTexture;
        introPlayer.audioOutputMode=VideoAudioOutputMode.AudioSource;introPlayer.controlledAudioTrackCount=1;
        introPlayer.EnableAudioTrack(0,true);introPlayer.SetDirectAudioMute(0,false);introPlayer.SetDirectAudioVolume(0,options.musicVolume);
        introPlayer.SetTargetAudioSource(0,introAudio);
        introPlayer.isLooping=false;introPlayer.skipOnDrop=true;introPlayer.waitForFirstFrame=true;introPlayer.playbackSpeed=1;
        introPlayer.loopPointReached+=OnIntroFinished;
        introPlayer.errorReceived+=(player,error)=>{Debug.LogError("CF_PC_INTRO_DECODER "+error);FinishIntro(false);};
        introPlayer.prepareCompleted+=player=>{player.Play();introStall=0;Debug.Log("CF_PC_INTRO_PREPARED length="+player.length);};
        introPlayer.Prepare();introPlayedAt=Time.realtimeSinceStartup;
        Debug.Log("CF_PC_INTRO_START source=startup_intro.mp4 duration=36.40 url="+(!clip));
    }
    void OnIntroFinished(VideoPlayer player) {
        if(!introPlaying||introLeaving)return;
        Debug.Log("CF_PC_INTRO_LOOP_END time="+player.time+" frame="+player.frame+" furthest="+introFurthest);FinishIntro(introFurthest>=35.65f);
    }
    void RequestIntroSkip() {
        if(!introPlaying||introLeaving||BattleLab.Arg("--pc-intro-lock")=="1")return;
        Debug.Log("CF_PC_INTRO_SKIP_REQUEST");if(PlayerPrefs.GetInt("cf-intro-completed",0)==0)return;
        FinishIntro(false);
    }
    void FinishIntro(bool completed) {
        if(introLeaving)return;introLeaving=true;
        float played=Mathf.Max(0,introFurthest);
        if(introPlayer)introPlayer.loopPointReached-=OnIntroFinished;
        if(completed){PlayerPrefs.SetInt("cf-intro-completed",1);PlayerPrefs.Save();}
        if(introPlayer){introPlayer.Stop();introPlayer.targetTexture=null;Destroy(introPlayer.gameObject);introPlayer=null;introAudio=null;}
        if(introTexture){introTexture.Release();Destroy(introTexture);introTexture=null;}
        introResumeMusic=IntroMusicOffset+played;
        Debug.Log("CF_PC_INTRO_END completed="+completed+" played="+played.ToString("F2")+" music="+introResumeMusic.ToString("F2"));
    }
    float introResumeMusic;
    void UpdateIntro() {
        if(!introPlaying)return;
        if(introPlayer) {
            if(Input.GetKeyDown(KeyCode.Escape))RequestIntroSkip();
            bool advanced=false;
            if(introPlayer.isPlaying) {
                float position=(float)introPlayer.time;
                if(position>introFurthest+.001f){introFurthest=position;advanced=true;}
            }
            if(advanced)introStall=0;
            else {
                introStall+=Time.unscaledDeltaTime;
                if(introStall>(introPlayer.isPrepared?20f:60f)){Debug.Log("CF_PC_INTRO_STALL");FinishIntro(false);}
            }
            if(!introCaptureDone&&!string.IsNullOrEmpty(introCapturePath)&&introFurthest>6f) {
                introCaptureDone=true;StartCoroutine(CaptureIntro(introCapturePath));
            }
        }
        if(introLeaving) {
            introFade=Mathf.Clamp01(introFade+Time.unscaledDeltaTime/.18f);
            if(introFade>=1f) {
                introPlaying=false;page=Page.Main;BeginMenuMusic(introResumeMusic);
                Debug.Log("CF_PC_INTRO_MENU_MUSIC offset="+introResumeMusic.ToString("F2"));
                if(!introCaptureDone&&!string.IsNullOrEmpty(introCapturePath)){introCaptureDone=true;StartCoroutine(CaptureIntro(introCapturePath));}
            }
        }
    }
    System.Collections.IEnumerator CaptureIntro(string path) {
        yield return new WaitForEndOfFrame();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log("CF_PC_INTRO_SHOT page="+page+" playing="+introPlaying);
        yield return null;
        Application.Quit(0);
    }
    void DrawIntroScreen() {
        Fill(new Rect(0,0,Screen.width,Screen.height),Color.black);
        var previous=GUI.matrix;GUI.matrix=Matrix4x4.identity;
        if(introTexture!=null&&introTexture.IsCreated())GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),introTexture,ScaleMode.ScaleAndCrop);
        if(introLeaving)Fill(new Rect(0,0,Screen.width,Screen.height),new Color(0,0,0,introFade));
        GUI.matrix=Matrix4x4.TRS(new Vector3(guiOrigin.x,guiOrigin.y,0),Quaternion.identity,Vector3.one*guiScale);

        if(!introLeaving&&PlayerPrefs.GetInt("cf-intro-completed",0)==1&&Button("intro.skip",new Rect(1080,638,170,52),"Пропустить  ›",true))RequestIntroSkip();
        GUI.matrix=previous;
    }
}
}