using UnityEngine;

namespace CastleFight {
public static class ContentLoadingScreen {
    static readonly Color Gold=new Color(.82f,.65f,.36f);
    static readonly Color Paper=new Color(.97f,.89f,.72f);
    static Texture2D embeddedBackdrop,embeddedSiege;
    static Texture2D smokeMask,windowMask;
    static bool artLoaded;
    static GUIStyle brand,title,text,percent,button;
    static Font styleFont;

    internal static void LoadEmbeddedArt(){
        if(artLoaded)return;
        artLoaded=true;
        embeddedBackdrop=Resources.Load<Texture2D>("CampaignMenu/SiegeBackdrop");
        embeddedSiege=Resources.Load<Texture2D>("CampaignMenu/CastleBody");
    }

    static void EnsureStyles(Font font){
        if(title!=null&&styleFont==font)return;
        styleFont=font;
        text=new GUIStyle(GUI.skin.label){font=font?font:GUI.skin.font,fontSize=18,wordWrap=true,
            padding=new RectOffset(),normal={textColor=Paper}};
        title=new GUIStyle(text){fontSize=41,fontStyle=FontStyle.Bold,wordWrap=false};
        brand=new GUIStyle(text){fontSize=27,fontStyle=FontStyle.Bold,wordWrap=false,normal={textColor=Gold}};
        percent=new GUIStyle(text){alignment=TextAnchor.MiddleRight,fontSize=21,fontStyle=FontStyle.Bold};
        button=new GUIStyle(text){alignment=TextAnchor.MiddleCenter,wordWrap=false,fontStyle=FontStyle.Bold};
    }

    static void Fill(Rect area,Color color){
        var previous=GUI.color;GUI.color=color;GUI.DrawTexture(area,Texture2D.whiteTexture);GUI.color=previous;
    }

    static Texture2D MakeCastleMask(bool smoke){
        const int size=CastleLoadingMotion.MaskSize;
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){
            wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,
            name=smoke?"Loading castle smoke":"Loading castle window light"
        };
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            pixels[y*size+x]=smoke?new Color32(143,136,123,CastleLoadingMotion.SmokeMask(x,size-1-y)):
                new Color32(255,205,112,CastleLoadingMotion.GlowMask(x,y));
        texture.SetPixels32(pixels);
        texture.Apply(false,true);
        return texture;
    }

    static void DrawCastleSprite(Texture2D texture,Rect area,CastleLoadingMotion.Sprite sprite,float opacity){
        float width=area.width*sprite.width,height=area.height*sprite.height;
        GUI.color=new Color(1,1,1,sprite.opacity*opacity);
        GUI.DrawTexture(new Rect(area.x+area.width*sprite.x-width*.5f,
            area.y+area.height*sprite.y-height*.5f,width,height),texture);
    }

    internal static void DrawLoadingCastle(Texture2D atlas,Rect target,float elapsed){
        if(!atlas)return;
        if(!smokeMask)smokeMask=MakeCastleMask(true);
        if(!windowMask)windowMask=MakeCastleMask(false);
        var tint=GUI.color;
        for(int i=0;i<CastleLoadingMotion.PuffCount;i++)
            DrawCastleSprite(smokeMask,target,CastleLoadingMotion.SmokePuff(i,elapsed),tint.a);
        GUI.color=tint;
        GUI.DrawTexture(target,atlas);
        for(int i=0;i<CastleLoadingMotion.WindowCount;i++)
            DrawCastleSprite(windowMask,target,CastleLoadingMotion.WindowGlow(i,elapsed),tint.a);
        GUI.color=tint;
    }

    internal static void DrawProgress(Rect bar,float progress,float time){
        Fill(bar,new Color(.19f,.20f,.13f));
        if(progress>=0){
            Fill(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(progress),bar.height),Gold);
        }else{
            float length=bar.width*.20f;
            float x=bar.x+(bar.width-length)*Mathf.PingPong(time*.65f,1);
            Fill(new Rect(x,bar.y,length,bar.height),Gold);
        }
    }

    static bool ActionButton(Rect rect,string caption,bool primary=false){
        bool hover=rect.Contains(Event.current.mousePosition)&&!Application.isMobilePlatform;
        Fill(rect,primary?new Color(.27f,.22f,.11f):new Color(.07f,.11f,.08f));
        Fill(new Rect(rect.x,rect.y,rect.width,1),hover?Paper:Gold);
        return GUI.Button(rect,caption,button);
    }

    public static int Draw(Texture2D backdrop,Texture2D siege,Font font,string status,float progress,string error,
        bool bulk,bool startup=false,Texture2D fallback=null,float? previewTime=null){
        LoadEmbeddedArt();
        EnsureStyles(font);
        if(!backdrop)backdrop=embeddedBackdrop?embeddedBackdrop:fallback;
        if(!siege)siege=embeddedSiege;
        float time=previewTime??Time.realtimeSinceStartup;
        var matrix=GUI.matrix;var color=GUI.color;
        GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;
        var screen=new Rect(0,0,Screen.width,Screen.height);
        if(backdrop)GUI.DrawTexture(screen,backdrop,ScaleMode.ScaleAndCrop);
        else Fill(screen,new Color(.035f,.053f,.043f));
        var safe=Screen.safeArea;
        if(safe.width<=0||safe.height<=0)safe=screen;
        float s=Mathf.Min(safe.width/1280f,safe.height/720f);
        GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x+(safe.width-1280*s)*.5f,
            Screen.height-safe.yMax+(safe.height-720*s)*.5f,0),Quaternion.identity,Vector3.one*s);

        GUI.Label(new Rect(60,52,720,48),"Castle Fight",brand);
        Fill(new Rect(61,104,178,1),Gold);
        if(Event.current.type==EventType.Repaint)DrawLoadingCastle(siege,new Rect(816,391,421,281),time);

        Fill(new Rect(48,442,737,220),new Color(.029f,.046f,.035f,.92f));
        Fill(new Rect(48,442,737,1),Gold);
        GUI.Label(new Rect(72,459,603,61),bulk?"Загрузка ресурсов":startup?"Подготовка к битве":"Подготовка ресурсов",title);
        if(progress>=0)GUI.Label(new Rect(662,477,98,31),Mathf.RoundToInt(100*Mathf.Clamp01(progress))+"%",percent);
        bool failed=!string.IsNullOrEmpty(error);
        GUI.Label(new Rect(74,530,681,64),failed?error:status,text);
        DrawProgress(new Rect(74,604,681,6),progress,time);
        int action=0;
        if(failed&&ActionButton(new Rect(74,620,189,34),"Повторить",true))action|=1;
        if(bulk&&ActionButton(new Rect(533,620,222,34),"Продолжить позже"))action|=2;
        GUI.matrix=matrix;GUI.color=color;
        return action;
    }
}
}
