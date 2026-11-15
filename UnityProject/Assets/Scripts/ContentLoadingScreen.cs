using System;using UnityEngine;
namespace CastleFight {
public static class ContentLoadingScreen {
    public static int Draw(Texture2D background,Texture2D hammer,Font font,string status,float progress,string error,bool bulk){
        int action=0;var previous=GUI.matrix;var previousColor=GUI.color;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;
        if(background)GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),background,ScaleMode.ScaleAndCrop);
        float s=Mathf.Min(Screen.width/1280f,Screen.height/720f);Rect safe=Screen.safeArea;if(safe.width<1)safe=new Rect(0,0,Screen.width,Screen.height);
        float width=Mathf.Max(480*s,Mathf.Min(940*s,safe.width-285*s));float height=(error!=""?205:164)*s;
        var panel=new Rect(safe.x+32*s,Screen.height-safe.yMin-28*s-height,width,height);
        Action<Rect,Color> fill=(r,c)=>{GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);};
        fill(new Rect(panel.x+5*s,panel.y+6*s,panel.width,panel.height),new Color(0,0,0,.45f));fill(panel,new Color(.018f,.028f,.032f,.94f));
        var gold=new Color(.71f,.56f,.30f);fill(new Rect(panel.x,panel.y,panel.width,1*s),gold);fill(new Rect(panel.x,panel.y+panel.height-1*s,panel.width,1*s),new Color(.29f,.25f,.15f));
        fill(new Rect(panel.x,panel.y,3*s,panel.height),gold);GUI.color=Color.white;
        var text=new GUIStyle(GUI.skin.label){font=font,fontSize=Mathf.RoundToInt(21*s),wordWrap=true,normal={textColor=new Color(.78f,.81f,.78f)}};
        var title=new GUIStyle(text){fontSize=Mathf.RoundToInt(27*s),normal={textColor=new Color(.96f,.83f,.57f)}};
        GUI.Label(new Rect(panel.x+24*s,panel.y+14*s,panel.width-125*s,38*s),bulk?"ЗАПАСЫ ДЛЯ ВСЕХ СРАЖЕНИЙ":"ПОДГОТОВКА К БИТВЕ",title);
        GUI.Label(new Rect(panel.x+panel.width-100*s,panel.y+15*s,80*s,35*s),Mathf.RoundToInt(progress*100)+"%",new GUIStyle(title){alignment=TextAnchor.MiddleRight});
        GUI.Label(new Rect(panel.x+24*s,panel.y+57*s,panel.width-48*s,65*s),error!=""?error:status,text);
        var bar=new Rect(panel.x+24*s,panel.y+panel.height-33*s,panel.width-48*s,12*s);fill(new Rect(bar.x-2*s,bar.y-2*s,bar.width+4*s,bar.height+4*s),new Color(.24f,.25f,.20f));fill(bar,new Color(.03f,.045f,.045f));
        fill(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(progress),bar.height),new Color(.61f,.43f,.16f));fill(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(progress),4*s),new Color(.92f,.75f,.40f));GUI.color=Color.white;
        if(error!=""&&GUI.Button(new Rect(panel.x+24*s,panel.y+120*s,210*s,42*s),"Повторить",new GUIStyle(GUI.skin.button){font=font,fontSize=Mathf.RoundToInt(22*s)}))action|=1;
        if(bulk&&GUI.Button(new Rect(panel.x+panel.width-245*s,panel.y+panel.height-80*s,220*s,36*s),"Продолжить позже",new GUIStyle(GUI.skin.button){font=font,fontSize=Mathf.RoundToInt(19*s)})){action|=2;}
        var root=new Vector2(Screen.width+12*s,Screen.height+12*s);float phase=Mathf.Repeat(Time.realtimeSinceStartup,1.1f)/1.1f;float angle=phase<.65f?Mathf.SmoothStep(-12,24,phase/.65f):Mathf.SmoothStep(24,-12,(phase-.65f)/.35f);
        GUIUtility.RotateAroundPivot(angle,root);if(hammer)GUI.DrawTexture(new Rect(root.x-232*s,root.y-232*s,260*s,260*s),hammer,ScaleMode.ScaleToFit);GUI.matrix=Matrix4x4.identity;
        GUI.matrix=previous;GUI.color=previousColor;return action;
    }
}
}
