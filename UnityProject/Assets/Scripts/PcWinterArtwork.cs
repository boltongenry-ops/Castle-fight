using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    Texture2D winterArtwork;
    Texture2D winterShadeX,winterShadeY;
    bool winterArtTried,versusWinter;
    int winterVersusFrame;
    float winterVersusReveal;
    static readonly Color WinterInk=new Color(.027f,.045f,.067f);
    static readonly Color WinterLine=new Color(.42f,.66f,.78f,.82f);

    Texture2D WinterArtwork(){
        if(winterArtTried)return winterArtwork;
        winterArtTried=true;
        var embedded=Resources.Load<TextAsset>("WinterBattle/EternalWinter");
        string path=Path.Combine(Application.streamingAssetsPath,"Frontend","EternalWinter.jpg");
        byte[] bytes=embedded?embedded.bytes:!Application.isMobilePlatform&&File.Exists(path)?File.ReadAllBytes(path):null;
        if(bytes==null)return null;
        var texture=new Texture2D(2,2,TextureFormat.RGB24,false){wrapMode=TextureWrapMode.Clamp};
        if(texture.LoadImage(bytes,true))winterArtwork=texture;
        else Destroy(texture);
        return winterArtwork;
    }

    void ReleaseWinterArtwork(){
        if(winterArtwork)Destroy(winterArtwork);
        winterArtwork=null;
        winterArtTried=false;
    }

    void DisposeWinterArtwork(){
        ReleaseWinterArtwork();
        if(winterShadeX)Destroy(winterShadeX);
        if(winterShadeY)Destroy(winterShadeY);
    }

    void DrawWinterPainting(Rect rect,bool keepSubjects=false){
        Fill(rect,WinterInk);
        var art=WinterArtwork();
        if(!art)return;
        float horizontal=Mathf.Min(1,rect.width*art.height/(rect.height*art.width));
        float vertical=Mathf.Min(1,rect.height*art.width/(rect.width*art.height));
        GUI.DrawTextureWithTexCoords(rect,art,new Rect((1-horizontal)*(keepSubjects?.79f:.5f),(1-vertical)*.63f,horizontal,vertical),false);
    }

    void WinterGradient(Rect rect,bool horizontal,Color color){
        if(Event.current.type!=EventType.Repaint)return;
        if(!winterShadeX)winterShadeX=CreateWinterShade(true);
        if(!winterShadeY)winterShadeY=CreateWinterShade(false);
        var before=GUI.color;
        GUI.color=color;
        GUI.DrawTexture(rect,horizontal?winterShadeX:winterShadeY);
        GUI.color=before;
    }

    static Texture2D CreateWinterShade(bool horizontal){
        const int length=64;
        var shade=new Texture2D(horizontal?length:1,horizontal?1:length,TextureFormat.RGBA32,false){
            wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear
        };
        for(int i=0;i<length;i++)
            shade.SetPixel(horizontal?i:0,horizontal?0:i,new Color(1,1,1,1-i/(length-1f)));
        shade.Apply(false,true);
        return shade;
    }

    void DrawMythicTrialRow(JObject row,Rect tile){
        string id=(string)row["id"];
        if(id==HumanMatch.WinterTrial)DrawWinterPainting(tile,true);
        else Fill(tile,WinterInk);
        WinterGradient(tile,true,new Color(.01f,.02f,.03f,.99f));
        Border(tile,id==selectedMythicId?WinterBlue:WinterLine,id==selectedMythicId?2:1);
        Text(tile.x+14,tile.y+3,tile.width-125,32,(string)row["title"],raceTitleStyle);
        Text(tile.x+15,tile.y+36,tile.width-125,28,(string)(row["summary"]??row["description"]),small);
        var play=new Rect(tile.xMax-103,tile.y+12,92,tile.height-24);
        if(Button("challenge."+id,new Rect(tile.x,tile.y,play.x-tile.x,tile.height),"",false,GUIStyle.none))
            SelectMythicTrial(id);
        if(LobbyButton("mythic.play."+id,play,"Играть")){
            SelectMythicTrial(id);
            ChooseMythicRace(row);
        }
    }

    void DrawWinterVersus(){
        if(Event.current.type==EventType.Repaint&&winterVersusFrame!=Time.frameCount){
            if(winterVersusFrame>=0)winterVersusReveal+=Mathf.Min(.07f,Time.unscaledDeltaTime);
            winterVersusFrame=Time.frameCount;
        }
        DrawWinterPainting(new Rect(0,0,1280,720),true);
        WinterGradient(new Rect(0,0,565,637),true,new Color(.014f,.025f,.041f,.93f));
        WinterGradient(new Rect(0,464,1280,175),false,new Color(.012f,.022f,.039f,.98f));
        var portrait=new Rect(52,177,233,302);
        Fill(portrait,WinterInk);
        var playerArt=versusLeft?versusLeft:versusLeftFallback;
        if(playerArt)GUI.DrawTexture(portrait,playerArt,ScaleMode.ScaleAndCrop);
        else if(RaceEmblem(versusA) is Texture emblem&&emblem)GUI.DrawTexture(portrait,emblem,ScaleMode.ScaleToFit);
        Border(portrait,WinterLine,1);
        Text(45,495,350,44,VersusNames[versusA],new GUIStyle(label){wordWrap=true,fontSize=23});
        Text(294,278,134,89,"VS",new GUIStyle(title){fontSize=62,alignment=TextAnchor.MiddleCenter,normal={textColor=WinterBlue}});
        Text(588,495,570,65,"Вечная зима",new GUIStyle(raceTitleStyle){fontSize=48,normal={textColor=Color.white}});
        Text(594,570,605,44,"Ледяная королева · Северные королевства",label);
        Fill(new Rect(0,637,1280,83),new Color(.009f,.017f,.029f,.98f));
        DrawVersusLoading(WinterBlue);
        float fade=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(winterVersusReveal/.8f));
        if(fade>0)Fill(new Rect(0,0,1280,720),new Color(0,0,0,fade));
    }
}
}
