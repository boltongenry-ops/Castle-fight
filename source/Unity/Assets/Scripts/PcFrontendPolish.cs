using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
namespace CastleFight {
public sealed partial class PcGame {
    TouchScreenKeyboard accountKeyboard;string accountKeyboardId,accountBefore;
    string AccountText(string id,Rect rect,string value,int limit,bool password,string placeholder){
        if(!Application.isMobilePlatform){GUI.SetNextControlName(id);return password?GUI.PasswordField(rect,value,'●',limit,menuInput):GUI.TextField(rect,value,limit,menuInput);}
        if(accountKeyboardId==id&&accountKeyboard!=null){value=accountKeyboard.text??"";if(accountKeyboard.status!=TouchScreenKeyboard.Status.Visible){accountKeyboard=null;accountKeyboardId=null;}}
        string shown=value.Length==0?placeholder:password?new string('●',value.Length):value;
        if(GUI.Button(rect,shown,menuInput)){if(accountKeyboard!=null)accountKeyboard.active=false;accountBefore=value;accountKeyboardId=id;TouchScreenKeyboard.hideInput=false;accountKeyboard=TouchScreenKeyboard.Open(value,TouchScreenKeyboardType.Default,false,false,password,false,placeholder,limit);}
        return value;
    }
    void CloseAccountKeyboard(){if(accountKeyboard!=null)accountKeyboard.active=false;accountKeyboard=null;accountKeyboardId=null;}
    readonly Dictionary<string,AudioClip> memeAudio=new Dictionary<string,AudioClip>();readonly HashSet<string> memeAudioLoading=new HashSet<string>();int memeSoundSerial;
    IEnumerator PlayMemeAudio(string key){
        int serial=++memeSoundSerial;
        if(memeAudioLoading.Contains(key)){while(memeAudioLoading.Contains(key))yield return null;}
        if(!memeAudio.TryGetValue(key,out var clip)||!clip){
            memeAudioLoading.Add(key);string relative="Frontend/MemeAudio/"+key+".mp3";
            if(MobileContent.Active)yield return MobileContent.Current.EnsureMedia(relative,false);
            string path=MobileContent.Media(relative);
            if(File.Exists(path))using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,AudioType.MPEG)){yield return request.SendWebRequest();if(request.result==UnityWebRequest.Result.Success){clip=DownloadHandlerAudioClip.GetContent(request);clip.name="Meme voice · "+key;memeAudio[key]=clip;}}
            memeAudioLoading.Remove(key);
        }
        if(serial!=memeSoundSerial||!clip)yield break;
        if(!stickerAudio){stickerAudio=gameObject.AddComponent<AudioSource>();stickerAudio.playOnAwake=false;stickerAudio.spatialBlend=0;}stickerAudio.Stop();stickerAudio.volume=.75f;stickerAudio.clip=clip;stickerAudio.Play();Debug.Log("CF_MEME_AUDIO_PLAY key="+key+" seconds="+clip.length);
    }
    bool showResourceDownloads;long resourceRemaining;float resourceNextRefresh;
    void DrawResourceDownloads(){
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.75f));Box(new Rect(280,140,720,430));Text(320,173,640,45,"РЕСУРСЫ ИГРЫ",heading);Accent(320,228,640);
        var loader=MobileContent.Current;long total=loader.TotalContentBytes;if(Time.unscaledTime>=resourceNextRefresh){resourceRemaining=loader.RemainingContentBytes;resourceNextRefresh=Time.unscaledTime+1;}long remaining=resourceRemaining;
        Text(320,251,630,100,"На устройстве: "+((total-remaining)/1048576f).ToString("F0")+" / "+(total/1048576f).ToString("F0")+" МБ\n"+(remaining==0?"Все ресурсы уже скачаны.":"Докачать: "+(remaining/1048576f).ToString("F0")+" МБ"),label);
        Text(320,354,630,66,"Скачайте все расы, оформление и озвучку заранее. Загрузка продолжается после обрыва; модели не загружаются в память.",small);
        GUI.enabled=remaining>0;if(Button("resources.all",new Rect(320,438,640,48),remaining==0?"Всё скачано":"Скачать всё",true)){showResourceDownloads=false;StartCoroutine(loader.DownloadAll());}GUI.enabled=true;
        if(Button("resources.close",new Rect(320,501,640,40),"← Назад"))showResourceDownloads=false;
    }
}
}
