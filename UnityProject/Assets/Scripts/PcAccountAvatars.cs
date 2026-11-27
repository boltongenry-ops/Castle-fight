using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    static readonly string[] AvatarTemplates={"hfoo","hrif","ugho","ogru","earc","n000","edry","uske"};
    static readonly int[] AvatarTemplateRaces={0,5,1,4,10,8,3,7};
    sealed class AccountAvatarState {
        public bool open,picking;
        public string error="";
        public readonly Dictionary<string,Texture2D> images=new Dictionary<string,Texture2D>();
        public readonly Dictionary<string,float> nextRequest=new Dictionary<string,float>();
        public readonly Queue<JToken> pending=new Queue<JToken>();
    }
    static readonly ConditionalWeakTable<PcGame,AccountAvatarState> AvatarStates=new ConditionalWeakTable<PcGame,AccountAvatarState>();
    AccountAvatarState Avatars=>AvatarStates.GetValue(this,_=>new AccountAvatarState());

    Texture2D AccountAvatar(JToken profile){
        if(profile==null)return null;
        string revision=(string)profile["avatar_ref"]??"";
        var state=Avatars;
        if(revision!=""){
            if(state.images.TryGetValue(revision,out var known))return known;
            float now=Time.realtimeSinceStartup;
            if((!state.nextRequest.TryGetValue(revision,out float next)||now>=next)&&state.pending.Count<12){
                state.pending.Enqueue(profile);state.nextRequest[revision]=now+12;
            }
        }
        return TemplateAvatar((string)profile["avatar"]);
    }

    Texture2D TemplateAvatar(string id){
        int template=Array.IndexOf(AvatarTemplates,id);
        return template<0?null:RaceEmblem(AvatarTemplateRaces[template]);
    }

    bool RequestAccountAvatar(){
        var state=Avatars;
        if(state.pending.Count==0)return false;
        var profile=state.pending.Dequeue();
        SocialRequest("avatar",new JObject{["peer"]=(string)profile["id"]});
        return true;
    }

    void ReceiveAccountAvatar(JObject reply){
        string revision=(string)reply["avatar_ref"]??"";
        string image=(string)reply["avatar_image"]??"";
        var state=Avatars;
        if(revision==""||image==""||image.Length>21848||state.images.ContainsKey(revision))return;
        byte[] bytes;
        try{bytes=Convert.FromBase64String(image);}catch(FormatException){return;}
        if(!AvatarDimensions(bytes,out int width,out int height)||width!=128||height!=128)return;
        var texture=new Texture2D(2,2,TextureFormat.RGB24,false);
        if(!texture.LoadImage(bytes,true)){Destroy(texture);return;}
        if(state.images.Count>=24){
            using(var entries=state.images.GetEnumerator()){
                entries.MoveNext();var previous=entries.Current;
                Destroy(previous.Value);state.images.Remove(previous.Key);state.nextRequest.Remove(previous.Key);
            }
        }
        state.images.Add(revision,texture);
    }

    void DisposeAccountAvatars(){
        foreach(var texture in Avatars.images.Values)Destroy(texture);
        Avatars.images.Clear();
    }

    void DrawAvatarEditor(){
        var state=Avatars;
        if(!HasNetworkProfile){state.open=false;return;}
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.65f));
        AtlasSurface(new Rect(325,120,630,477),true);
        Text(356,146,537,49,"Аватар профиля",atlasTitle);
        if(LobbyButton("profile.avatar.close",new Rect(880,146,45,41),"×")){state.open=false;return;}
        DrawProfileBadge(new Rect(358,210,92,92));
        Text(468,214,442,40,"Текущий аватар",label);
        Text(468,256,442,35,FitAtlasLabel((string)networkProfile["name"],429,small),small);
        Text(358,315,565,37,"Готовые портреты",label);
        bool enabled=GUI.enabled;
        GUI.enabled=enabled&&!state.picking&&AccountHub.operation==null&&AccountHub.changes.Count==0;
        for(int i=0;i<AvatarTemplates.Length;i++){
            var area=new Rect(358+i*70,358,64,64);
            AtlasTile(area,(string)networkProfile["avatar_ref"]==""&&(string)networkProfile["avatar"]==AvatarTemplates[i],false);
            var image=TemplateAvatar(AvatarTemplates[i]);
            if(image)GUI.DrawTexture(new Rect(area.x+5,area.y+5,54,54),image,ScaleMode.ScaleToFit);
            if(Button("profile.avatar.template."+i,area,"",false,GUIStyle.none))
                QueueAccountChange(new JObject{["type"]="profile_update",["avatar"]=AvatarTemplates[i]});
        }
        if(LobbyButton("profile.avatar.upload",new Rect(358,451,564,49),
            state.picking?"Выбираем изображение…":"Загрузить изображение с устройства",true))
            StartCoroutine(UploadAvatar());
        GUI.enabled=enabled;
        Text(358,513,564,53,FitAtlasLabel(state.error!=""?state.error:AccountHub.result,546,small),small);
    }

    IEnumerator UploadAvatar(){
        var state=Avatars;
        state.picking=true;state.error="";
        string owner=AccountHub.owner,image="";
#if UNITY_ANDROID && !UNITY_EDITOR
        var callback=new AndroidAvatarCallback();
        try{
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var picker=new AndroidJavaClass("com.castlefight.profile.AvatarPicker"))
                picker.CallStatic("pick",activity,callback);
        }catch(Exception){callback.complete("","Не удалось открыть выбор изображения.");}
        float until=Time.realtimeSinceStartup+300;
        while(!callback.done&&Time.realtimeSinceStartup<until)yield return null;
        image=callback.image;state.error=callback.error;
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        yield return null;
        try{
            string path=PickWindowsAvatar();
            if(path!=""){
                var file=new FileInfo(path);
                if(file.Length>24*1024*1024)throw new IOException();
                image=EncodeAvatar(File.ReadAllBytes(path));
                if(image=="")state.error="Выберите читаемое изображение JPG или PNG до 24 МБ.";
            }
        }catch(Exception){state.error="Не удалось прочитать JPG или PNG.";}
#else
        state.error="Выбор файла недоступен на этой платформе.";
        yield return null;
#endif
        if(image!=""&&HasNetworkProfile&&owner==AccountHub.owner){
            AccountHub.result="Сохраняем аватар…";
            QueueAccountChange(new JObject{["type"]="profile_update",["avatar_image"]=image});
        }
        state.picking=false;
    }

    static bool AvatarDimensions(byte[] data,out int width,out int height){
        width=height=0;
        if(data.Length<24)return false;
        if(data[0]==137&&data[1]==80&&data[2]==78&&data[3]==71&&data[12]==73&&data[13]==72&&data[14]==68&&data[15]==82){
            for(int i=16;i<20;i++){width=(width<<8)|data[i];height=(height<<8)|data[i+4];}
        }else if(data[0]==255&&data[1]==216){
            int p=2;
            while(p+7<data.Length&&data[p]==255){
                int marker=data[p+1],length=(data[p+2]<<8)|data[p+3];
                if(marker==0xda||length<2||length>data.Length-p-2)break;
                if((marker==0xc0||marker==0xc2)&&length>=8&&p+8<data.Length){
                    height=(data[p+5]<<8)|data[p+6];width=(data[p+7]<<8)|data[p+8];break;
                }
                p+=2+length;
            }
        }
        return width>0&&height>0&&width<=8192&&height<=8192&&(long)width*height<=24000000;
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct AvatarOpenFile {
        public int size;
        public IntPtr owner,instance;
        public string filter;
        public IntPtr customFilter;
        public int customFilterLength,filterIndex;
        public IntPtr filename;
        public int filenameLength;
        public IntPtr titleBuffer;
        public int titleBufferLength;
        public string directory,title;
        public int flags;
        public short offset,extension;
        public string defaultExtension;
        public IntPtr customData,hook,template,reserved;
        public int reservedLength,extendedFlags;
    }
    [DllImport("comdlg32.dll",CharSet=CharSet.Unicode)]
    static extern bool GetOpenFileName(ref AvatarOpenFile data);
    [DllImport("user32.dll")]static extern IntPtr GetActiveWindow();

    static string PickWindowsAvatar(){
        const int capacity=32768;
        IntPtr buffer=Marshal.AllocHGlobal(capacity*2);
        try{
            Marshal.WriteInt16(buffer,0);
            var data=new AvatarOpenFile{size=Marshal.SizeOf<AvatarOpenFile>(),owner=GetActiveWindow(),
                filter="Изображения JPG и PNG\0*.jpg;*.jpeg;*.png\0\0",filename=buffer,filenameLength=capacity,
                title="Выберите аватар",flags=0x00080000|0x00001000|0x00000800|0x00000008};
            return GetOpenFileName(ref data)?Marshal.PtrToStringUni(buffer)??"":"";
        }finally{Marshal.FreeHGlobal(buffer);}
    }

    string EncodeAvatar(byte[] bytes){
        if(!AvatarDimensions(bytes,out _,out _))return "";
        var photo=new Texture2D(2,2,TextureFormat.RGBA32,false);
        var result=new Texture2D(128,128,TextureFormat.RGB24,false);
        RenderTexture previous=RenderTexture.active;
        var target=RenderTexture.GetTemporary(128,128,0);
        try{
            if(!photo.LoadImage(bytes))return "";
            float side=Mathf.Min(photo.width,photo.height);
            var scale=new Vector2(side/photo.width,side/photo.height);
            Graphics.Blit(photo,target,scale,(Vector2.one-scale)*.5f);
            RenderTexture.active=target;
            result.ReadPixels(new Rect(0,0,128,128),0,0);
            result.Apply(false);
            for(int quality=90;quality>=40;quality-=10){
                var jpg=result.EncodeToJPG(quality);
                if(jpg.Length<=16384)return Convert.ToBase64String(jpg);
            }
            return "";
        }finally{
            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Destroy(photo);Destroy(result);
        }
    }
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
    sealed class AndroidAvatarCallback : AndroidJavaProxy {
        public volatile bool done;
        public string image="",error="";
        public AndroidAvatarCallback():base("com.castlefight.profile.AvatarPicker$Callback"){}
        [UnityEngine.Scripting.Preserve]
        public void complete(string value,string problem){image=value??"";error=problem??"";done=true;}
    }
#endif
}
}
