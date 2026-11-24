using System.IO;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    Texture2D iceSeal,choirBell;
    GUIStyle bossCaption,bossSignal,bossTime;
    Texture2D LoadBossFrame(string key){
        var asset=Resources.Load<TextAsset>("WinterBattle/"+key);
        var file=MobileContent.Media("Frontend/"+key+".png");
        var bytes=asset?asset.bytes:File.Exists(file)?File.ReadAllBytes(file):null;
        if(bytes==null)return null;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
        if(texture.LoadImage(bytes,true))return texture;
        Destroy(texture);return null;
    }
    void FitBossText(Rect rect,string value,GUIStyle basis,int size,bool wrap=false){
        var content=new GUIContent(value);
        basis.fontSize=size;basis.wordWrap=wrap;basis.clipping=TextClipping.Clip;
        while(basis.fontSize>10&&(wrap?basis.CalcHeight(content,rect.width)>rect.height:basis.CalcSize(content).x>rect.width))
            basis.fontSize--;
        GUI.Label(rect,content,basis);
    }
    void DrawBossMeter(bool ice,string caption,string signal,float left,float span){
        var frame=ice?iceSeal:choirBell;
        if(!frame){
            frame=LoadBossFrame(ice?"IceSeal":"ChoirBell");
            if(ice)iceSeal=frame;else choirBell=frame;
        }
        if(bossCaption==null){
            bossCaption=new GUIStyle(hudText){fontSize=16,padding=new RectOffset(),alignment=TextAnchor.MiddleLeft};
            bossSignal=new GUIStyle(hudSmall){fontSize=12,padding=new RectOffset(),alignment=TextAnchor.MiddleLeft};
            bossTime=new GUIStyle(bossCaption){alignment=TextAnchor.MiddleRight};
        }
        var r=new Rect(218,22,350,101);
        var tint=ice?new Color(.57f,.85f,1):new Color(.61f,.93f,.55f);
        if(frame)GUI.DrawTexture(r,frame,ScaleMode.StretchToFill,true);
        else Fill(new Rect(r.x+82,r.y+35,243,45),new Color(.025f,.045f,.04f,.92f));
        float x=r.x+95,y=r.y+(ice?39:32);
        FitBossText(new Rect(x,y,159,20),caption,bossCaption,16);
        FitBossText(new Rect(x,y+18,208,16),signal,bossSignal,12);
        if(span>0){
            int seconds=Mathf.CeilToInt(Mathf.Max(0,left));
            string time=seconds>=60?(seconds/60)+":"+(seconds%60).ToString("00"):seconds+" с";
            FitBossText(new Rect(x+164,y,44,20),time,bossTime,16);
        }
        float fraction=span>0?1-Mathf.Clamp01(left/span):1;
        Fill(new Rect(r.x+95,r.y+(ice?77:67),218*fraction,3),tint);
    }
}
}
