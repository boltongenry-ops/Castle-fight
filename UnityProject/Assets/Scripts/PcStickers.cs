using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    sealed class StickerItem {public string id,label,sound,resource;public Rect uv;public bool atlas;}
    sealed class StickerPack {public string name;public bool paid;public readonly List<StickerItem> items=new List<StickerItem>();}
    List<StickerPack> stickerPacks;int stickerPack,stickerSelected;bool showStickerPicker;
    readonly Dictionary<string,Texture2D> stickerTextures=new Dictionary<string,Texture2D>();
    AudioSource stickerAudio;
    void LoadStickerCatalog() {
        if(stickerPacks!=null)return;
        stickerPacks=new List<StickerPack>();var classic=new StickerPack {name="Классические"};
        string[] ids={"orc","knight","undead","naga","elf","goblin"},labels={"Ха!","Класс!","Ну всё…","Легко!","Хорошая игра","Бум!"};
        for(int i=0;i<ids.Length;i++)classic.items.Add(new StickerItem {id=ids[i],sound=ids[i],label=labels[i],resource="PcStickers/"+ids[i],uv=new Rect(0,0,1,1)});
        stickerPacks.Add(classic);
        try {
            var catalog=ContentResources.Load<TextAsset>("PcStickers/match_emotes");
            var packs=JArray.Parse(catalog ? catalog.text : File.ReadAllText(Path.Combine(MobileContent.MediaRoot,"SourceData","match_emotes.json")));
            foreach(JObject source in packs) {
                var pack=new StickerPack {name=(string)source["name"]??"Стикеры"};string name=(string)source["id"]??"";var regions=source["regions"] as JArray;
                foreach(JObject row in (JArray)source["items"]) {
                    int cell=(int?)row["cell"]??0;Rect uv=new Rect((cell%2)*.5f,1-(cell/2+1)*.5f,.5f,.5f);
                    if(regions!=null&&cell<regions.Count&&regions[cell] is JArray r&&r.Count==4){float x=(float)r[0],y=(float)r[1],w=(float)r[2],h=(float)r[3];uv=new Rect(x,1-y-h,w,h);}
                    pack.items.Add(new StickerItem {id=(string)row["id"],label=(string)row["label"]??"",sound=(string)row["sound"]??"orc",resource="PcStickers/packs/"+name,atlas=true,uv=uv});
                }
                if(pack.items.Count>0)stickerPacks.Add(pack);
            }
        } catch(Exception e){Debug.LogWarning("Sticker catalog: "+e.Message);}
        var memes=new StickerPack{name="Рофлопак · местные легенды",paid=true};
        string[] memeKeys={"naga","mushroom","strategy","ggwp"},memeLabels={"Моё блять","Отменяй нахуй","Это стратегия","ГГВП"};
        for(int i=0;i<4;i++)memes.items.Add(new StickerItem{id="local_meme_"+memeKeys[i],label=memeLabels[i],sound="@meme/"+memeKeys[i],resource="@meme/"+memeKeys[i],uv=new Rect(0,0,1,1)});
        stickerPacks.Add(memes);
    }
    const string MemePackId="stickers_local_legends";
    readonly HashSet<string> memeLoading=new HashSet<string>();
    bool CanSendSticker(string id)=>!id.StartsWith("local_meme_",StringComparison.Ordinal)||progression!=null&&(AccountOwns(MemePackId)||progression.Owned(MemePackId));
    Texture2D StickerTexture(string resource){
        if(stickerTextures.TryGetValue(resource,out var result)&&result)return result;
        if(!resource.StartsWith("@meme/",StringComparison.Ordinal)){result=ContentResources.Load<Texture2D>(resource);stickerTextures[resource]=result;return result;}
        string path=MobileContent.MediaRoot+"/Frontend/MemeStickers/"+resource.Substring(6)+".png";
        if(Application.isMobilePlatform){if(memeLoading.Add(resource))StartCoroutine(LoadMemeTexture(resource,path));return null;}
        if(File.Exists(path)){result=new Texture2D(2,2,TextureFormat.RGBA32,false);if(result.LoadImage(File.ReadAllBytes(path),true)){result.filterMode=FilterMode.Bilinear;stickerTextures[resource]=result;return result;}Destroy(result);}return null;
    }
    IEnumerator LoadMemeTexture(string key,string path){if(MobileContent.Active){yield return MobileContent.Current.EnsureMedia("Frontend/MemeStickers/"+key.Substring(6)+".png",false);path=new Uri(path).AbsoluteUri;}using(var request=UnityEngine.Networking.UnityWebRequest.Get(path)){yield return request.SendWebRequest();if(request.result==UnityEngine.Networking.UnityWebRequest.Result.Success){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);if(t.LoadImage(request.downloadHandler.data,true)){t.filterMode=FilterMode.Bilinear;stickerTextures[key]=t;}else Destroy(t);}}memeLoading.Remove(key);}
    void StickerImage(Rect rect,StickerItem item) {
        var texture=StickerTexture(item.resource);if(!texture)return;
        float sourceWidth=texture.width*item.uv.width,sourceHeight=texture.height*item.uv.height;if(sourceWidth<=0||sourceHeight<=0)return;
        float scale=Mathf.Min(rect.width/sourceWidth,rect.height/sourceHeight),width=sourceWidth*scale,height=sourceHeight*scale;
        var fitted=new Rect(rect.center.x-width*.5f,rect.center.y-height*.5f,width,height);
        float ix=.5f/texture.width,iy=.5f/texture.height;var clipped=new Rect(item.uv.x+ix,item.uv.y+iy,Mathf.Max(0,item.uv.width-2*ix),Mathf.Max(0,item.uv.height-2*iy));
        GUI.DrawTextureWithTexCoords(fitted,texture,clipped,true);
    }
    void PreviewSticker(StickerItem item) {
        memeSoundSerial++;
        if(item.sound.StartsWith("@meme/",StringComparison.Ordinal)){StartCoroutine(PlayMemeAudio(item.sound.Substring(6)));return;}
        var clip=ContentResources.Load<AudioClip>("PcStickers/"+item.sound);
        if(clip){if(!stickerAudio){stickerAudio=gameObject.AddComponent<AudioSource>();stickerAudio.playOnAwake=false;stickerAudio.volume=.25f;}stickerAudio.Stop();stickerAudio.volume=.25f;stickerAudio.PlayOneShot(clip);}
    }
    void DrawStickerPicker() {
        LoadStickerCatalog();Fill(new Rect(0,0,1280,720),new Color(0,0,0,.7f));Box(new Rect(220,65,840,590));
        Text(255,91,700,40,"СТИКЕРЫ CASTLE FIGHT",heading);Accent(255,143,770);
        if(Button("sticker.pack",new Rect(255,163,470,42),"Набор: "+stickerPacks[stickerPack].name)){stickerPack=(stickerPack+1)%stickerPacks.Count;stickerSelected=0;}
        var items=stickerPacks[stickerPack].items;
        for(int i=0;i<items.Count;i++) {
            var r=new Rect(255+(i%3)*155,224+(i/3)*160,145,145);
            if(Button("sticker."+i,r,"")){stickerSelected=i;if(!CanSendSticker(items[i].id)){PreviewSticker(items[i]);Notice("Рофлопак открывается в магазине за 300 жетонов");}else if(!string.IsNullOrEmpty(socialStickerPeer)){socialPeer=socialStickerPeer;SendDirect("sticker",items[i].id);socialStickerPeer="";showStickerPicker=false;}else if(onlineMatch&&onlineStarted){SendNetwork(new JObject { ["type"]="emote",["id"]=items[i].id });showStickerPicker=false;}else PreviewSticker(items[i]);}
            StickerImage(new Rect(r.x+13,r.y+8,119,108),items[i]);Text(r.x+5,r.y+118,135,24,items[i].label,small);
        }
        if(items.Count>0){StickerImage(new Rect(768,207,230,230),items[Mathf.Clamp(stickerSelected,0,items.Count-1)]);Text(765,445,235,50,items[stickerSelected].label,label);}
        Text(255,557,730,32,stickerPacks[stickerPack].paid&&!CanSendSticker("local_meme_naga")?"Предпросмотр · весь набор: 300 жетонов в магазине.":(onlineMatch||!string.IsNullOrEmpty(socialStickerPeer))?"Нажмите стикер, чтобы отправить сопернику.":"Предпросмотр. Отправка доступна во время сетевого боя.",small);
        if(Button("sticker.close",new Rect(255,598,250,40),"← Назад")){showStickerPicker=false;socialStickerPeer="";}
    }
    void DisposeStickers(){foreach(var clip in memeAudio.Values)if(clip)Destroy(clip);memeAudio.Clear();if(stickerAudio)Destroy(stickerAudio);foreach(var entry in stickerTextures.Where(k=>k.Key.StartsWith("@meme/")))if(entry.Value)Destroy(entry.Value);}
}
}
