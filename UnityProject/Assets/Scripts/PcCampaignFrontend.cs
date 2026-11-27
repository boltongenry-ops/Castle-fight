using System.IO;
using System.Globalization;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    static readonly Color AtlasInk=new Color(.069f,.104f,.085f);
    static readonly Color AtlasGold=new Color(.76f,.64f,.39f);
    static readonly Color AtlasIvory=new Color(.94f,.88f,.72f);
    static readonly Color AtlasPaper=new Color(.83f,.73f,.54f);
    static readonly Color AtlasEdge=new Color(.41f,.36f,.24f);
    static readonly Color AtlasMuted=new Color(.72f,.72f,.59f);
    static readonly Color AtlasWell=new Color(.075f,.109f,.086f);
    static readonly Rect AtlasSignedInButton=new Rect(932,6,322,44);
    const float AtlasNamePadding=73;
    static readonly string[] AtlasTabs={"Экран","Звук","Управление","Ресурсы"};
    static readonly string[] AtlasQuality={"Низкое","Среднее","Высокое"};
    Texture2D campaignMap,campaignCards,campaignChrome,campaignIcons,campaignPanel,campaignScenes;
    bool campaignArtAttempted,campaignArtOnDisk;
    GUIStyle atlasButton,atlasDarkButton,atlasTitle,atlasCardText,atlasProfileName,atlasCompact,atlasDisabled,atlasCompactDark;
    int campaignSettingsTab;

    void PrepareCampaignArtwork(){
        if(campaignArtAttempted)return;
        campaignArtAttempted=true;
        if(!Application.isMobilePlatform){
            string folder=Path.Combine(Application.streamingAssetsPath,"Frontend");
            campaignMap=ReadCampaignPainting(folder,"CampaignMap");
            campaignCards=ReadCampaignPainting(folder,"CampaignCards");
            if(campaignMap&&campaignCards){
                campaignChrome=ReadCampaignTexture(Path.Combine(folder,"CampaignChrome.png"));
                campaignIcons=ReadCampaignTexture(Path.Combine(folder,"CampaignIcons.png"));
                campaignPanel=ReadCampaignTexture(Path.Combine(folder,"CampaignPanel.png"));
                campaignScenes=ReadCampaignPainting(folder,"CampaignScenes");
                campaignArtOnDisk=true;
                return;
            }
            if(campaignMap)Destroy(campaignMap);
            if(campaignCards)Destroy(campaignCards);
        }
        campaignMap=Resources.Load<Texture2D>("CampaignMenu/Map");
        campaignCards=Resources.Load<Texture2D>("CampaignMenu/Cards");
        campaignChrome=Resources.Load<Texture2D>("CampaignMenu/Chrome");
        campaignIcons=Resources.Load<Texture2D>("CampaignMenu/Icons");
        campaignPanel=Resources.Load<Texture2D>("CampaignMenu/Panel");
        campaignScenes=Resources.Load<Texture2D>("CampaignMenu/Scenes");
    }

    static Texture2D ReadCampaignPainting(string folder,string name){
        string jpg=Path.Combine(folder,name+".jpg");
        return ReadCampaignTexture(File.Exists(jpg)?jpg:Path.Combine(folder,name+".png"));
    }

    static Texture2D ReadCampaignTexture(string path){
        if(!File.Exists(path))return null;
        var art=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try{
            if(art.LoadImage(File.ReadAllBytes(path),true)){
                art.wrapMode=TextureWrapMode.Clamp;
                art.filterMode=FilterMode.Bilinear;
                return art;
            }
        }catch(IOException error){Debug.LogWarning("CF_CAMPAIGN_ART "+error.Message);}
        Destroy(art);
        return null;
    }

    void DisposeCampaignArtwork(){
        if(campaignArtOnDisk){
            if(campaignMap)Destroy(campaignMap);
            if(campaignCards)Destroy(campaignCards);
            if(campaignChrome)Destroy(campaignChrome);
            if(campaignIcons)Destroy(campaignIcons);
            if(campaignPanel)Destroy(campaignPanel);
            if(campaignScenes)Destroy(campaignScenes);
        }
        campaignMap=campaignCards=campaignChrome=campaignIcons=campaignPanel=campaignScenes=null;
        campaignArtAttempted=false;
    }

    void EnsureCampaignStyles(){
        PrepareCampaignArtwork();
        if(atlasButton!=null)return;
        atlasButton=new GUIStyle(label){fontSize=18,fontStyle=FontStyle.Bold,padding=new RectOffset(),
            alignment=TextAnchor.MiddleCenter,wordWrap=false,normal={textColor=AtlasIvory}};
        atlasDarkButton=new GUIStyle(atlasButton){normal={textColor=AtlasInk}};
        atlasCardText=new GUIStyle(atlasDarkButton){fontSize=25};
        atlasProfileName=new GUIStyle(atlasButton){alignment=TextAnchor.MiddleLeft,richText=false,clipping=TextClipping.Clip};
        atlasCompact=new GUIStyle(atlasButton){fontSize=16,richText=false,clipping=TextClipping.Clip};
        atlasCompactDark=new GUIStyle(atlasCompact){normal={textColor=AtlasInk}};
        atlasDisabled=new GUIStyle(atlasCompact){normal={textColor=AtlasMuted}};
        atlasTitle=new GUIStyle(heading){fontSize=34,wordWrap=false,padding=new RectOffset(),
            alignment=TextAnchor.MiddleLeft,normal={textColor=AtlasIvory}};
    }

    void AtlasPanel(Rect rect,Color background,Color edge){
        Fill(new Rect(rect.x+4,rect.y+5,rect.width,rect.height),new Color(.016f,.02f,.014f,.7f));
        Fill(rect,background);
        Border(rect,edge,2);
        var inset=new Rect(rect.x+4,rect.y+4,rect.width-8,rect.height-8);
        Border(inset,new Color(edge.r,edge.g,edge.b,.33f));
    }

    void AtlasSurface(Rect rect,bool ornate=false){
        EnsureCampaignStyles();
        if(ornate&&campaignPanel)
            DrawAtlasPart(campaignPanel,AtlasUv(new Rect(20,33,1595,895),1635,962),rect);
        else{
            AtlasPanel(rect,AtlasInk,AtlasEdge);
            if(campaignPanel){
                var inside=new Rect(rect.x+5,rect.y+5,rect.width-10,rect.height-10);
                DrawAtlasPart(campaignPanel,new Rect(.50f,.39f,.44f,.15f),inside);
                Fill(inside,new Color(.029f,.046f,.033f,.40f));
            }
        }
    }

    void AtlasScene(int tile,Rect rect){
        if(!campaignScenes)return;
        float availableWidth=campaignScenes.width*.5f-2, availableHeight=campaignScenes.height*.5f-2;
        float ratio=rect.width/rect.height;
        float width=Mathf.Min(availableWidth,availableHeight*ratio);
        float height=width/ratio;
        float x=(tile%2)*campaignScenes.width*.5f+1+(availableWidth-width)*.5f;
        float y=(tile/2)*campaignScenes.height*.5f+1+(availableHeight-height)*.5f;
        Source(campaignScenes,new Rect(x,y,width,height),rect);
    }

    bool AtlasControl(string id,Rect rect,string caption,bool primary=false,bool dropdown=false,Rect? hit=null){
        EnsureCampaignStyles();
        bool enabled=GUI.enabled;
        bool hover=enabled&&!Application.isMobilePlatform&&rect.Contains(Event.current.mousePosition);
        var tint=GUI.color;
        if(!enabled)GUI.color=new Color(1,1,1,.48f);
        else if(hover)GUI.color=new Color(1.16f,1.12f,1.01f);
        if(campaignChrome)DrawAtlasBar(primary&&enabled?4:3,rect);
        else AtlasPanel(rect,primary&&enabled?AtlasPaper:AtlasWell,hover||primary?AtlasGold:AtlasEdge);
        GUI.color=tint;
        var style=!enabled?atlasDisabled:primary?atlasCompactDark:atlasCompact;
        var contentRect=new Rect(rect.x+9,rect.y,rect.width-(dropdown?37:18),rect.height);
        GUI.Label(contentRect,FitAtlasLabel(caption,contentRect.width,style),style);
        if(dropdown)DrawLobbyChevron(new Rect(rect.xMax-22,rect.center.y-3,10,8),enabled);
        return Button(id,hit??rect,"",false,GUIStyle.none)&&enabled;
    }

    void AtlasTile(Rect rect,bool selected,bool hover){
        Fill(rect,selected?new Color(.25f,.221f,.124f):hover?new Color(.14f,.18f,.113f):AtlasWell);
        Border(rect,selected||hover?AtlasGold:AtlasEdge,selected?2:1);
        Fill(new Rect(rect.x+5,rect.y+3,rect.width-10,1),new Color(.68f,.60f,.36f,.22f));
    }

    bool AtlasEdition(string id,Rect rect,string title,int illustration,bool selected){
        AtlasTile(rect,selected,GUI.enabled&&rect.Contains(Event.current.mousePosition));
        var picture=new Rect(rect.x+6,rect.y+6,rect.width-12,rect.height-50);
        AtlasScene(illustration,picture);
        return AtlasControl(id,new Rect(rect.x+6,picture.yMax+4,rect.width-12,36),title,selected,hit:rect);
    }

    static readonly Rect[] AtlasChromeCuts={
        new Rect(19,101,591,390),new Rect(635,121,415,350),new Rect(1110,84,383,412),
        new Rect(24,696,497,141),new Rect(554,695,487,143),new Rect(1086,559,435,386)
    };
    static readonly Rect[] AtlasIconCuts={
        new Rect(110,115,224,220),new Rect(492,118,250,213),new Rect(853,132,275,177),
        new Rect(1258,102,241,248),new Rect(1652,105,242,240),new Rect(97,481,255,194),
        new Rect(502,471,229,207),new Rect(885,481,214,190),new Rect(1248,489,256,159),new Rect(1656,467,229,209)
    };
    static Rect AtlasUv(Rect pixels,float width,float height)=>
        new Rect(pixels.x/width,pixels.y/height,pixels.width/width,pixels.height/height);

    void DrawAtlasPart(Texture2D texture,Rect bounds,Rect target){
        if(texture)Source(texture,new Rect(bounds.x*texture.width,bounds.y*texture.height,
            bounds.width*texture.width,bounds.height*texture.height),target);
    }

    void DrawAtlasChrome(int tile,Rect rect)=>DrawAtlasPart(campaignChrome,AtlasUv(AtlasChromeCuts[tile],1536,1024),rect);

    void DrawAtlasBar(int tile,Rect rect){
        if(rect.width<=rect.height*8){
            DrawAtlasChrome(tile,rect);
            return;
        }
        var source=AtlasChromeCuts[tile];
        float cap=source.height*.29f;
        float end=Mathf.Min(rect.width*.22f,rect.height*.31f);
        var left=new Rect(source.x,source.y,cap,source.height);
        var middle=new Rect(source.x+cap,source.y,source.width-2*cap,source.height);
        var right=new Rect(source.xMax-cap,source.y,cap,source.height);
        DrawAtlasPart(campaignChrome,AtlasUv(middle,1536,1024),new Rect(rect.x+end-1,rect.y,rect.width-2*end+2,rect.height));
        DrawAtlasPart(campaignChrome,AtlasUv(left,1536,1024),new Rect(rect.x,rect.y,end,rect.height));
        DrawAtlasPart(campaignChrome,AtlasUv(right,1536,1024),new Rect(rect.xMax-end,rect.y,end,rect.height));
    }

    void DrawAtlasCardPhoto(Rect area,Rect crop){
        // Crop the illustration to the opening under the curved top corners.
        const float radius=30;
        const int bands=6;
        float vertical=crop.height/area.height,horizontal=crop.width/area.width;
        for(int i=0;i<bands;i++){
            float top=i*radius/bands,step=radius/bands;
            float across=radius-top;
            float inset=radius-Mathf.Sqrt(radius*radius-across*across);
            Source(campaignCards,new Rect(crop.x+inset*horizontal,crop.y+top*vertical,
                crop.width-2*inset*horizontal,step*vertical),new Rect(area.x+inset,area.y+top,
                area.width-2*inset,step));
        }
        Source(campaignCards,new Rect(crop.x,crop.y+radius*vertical,crop.width,crop.height-radius*vertical),
            new Rect(area.x,area.y+radius,area.width,area.height-radius));
    }

    void DrawAtlasIcon(int tile,Rect rect,bool dark=false){
        var tint=GUI.color;
        if(dark)GUI.color=new Color(.28f,.22f,.10f);
        var source=AtlasIconCuts[tile];
        float ratio=Mathf.Min(rect.width/source.width,rect.height/source.height);
        float width=source.width*ratio,height=source.height*ratio;
        DrawAtlasPart(campaignIcons,AtlasUv(source,1983,793),new Rect(rect.center.x-width*.5f,
            rect.center.y-height*.5f,width,height));
        GUI.color=tint;
    }

    bool AtlasButton(string id,Rect rect,string caption,bool primary=false,int icon=-1,bool parchment=false){
        bool hover=GUI.enabled&&rect.Contains(Event.current.mousePosition);
        Color bg=primary?(hover?new Color(.94f,.83f,.61f):AtlasPaper):
            hover?new Color(.18f,.25f,.19f):new Color(.087f,.133f,.103f);
        if(!GUI.enabled)bg=Color.Lerp(bg,AtlasInk,.56f);
        if(campaignChrome){
            var tint=GUI.color;
            if(!GUI.enabled)GUI.color=new Color(.7f,.72f,.65f,.75f);
            else if(hover||primary&&!parchment)GUI.color=new Color(1.13f,1.10f,.99f);
            DrawAtlasBar(parchment?4:3,rect);
            GUI.color=tint;
        }else AtlasPanel(rect,bg,hover||primary?AtlasGold:new Color(.39f,.36f,.26f));
        var labelRect=rect;
        if(icon>=0&&campaignIcons){
            DrawAtlasIcon(icon,new Rect(rect.x+13,rect.center.y-14,28,28),parchment);
            labelRect=new Rect(rect.x+38,rect.y,rect.width-40,rect.height);
        }
        GUI.Label(labelRect,caption,parchment?atlasDarkButton:atlasButton);
        return Button(id,rect,"",false,GUIStyle.none);
    }

    static bool AtlasCardHighlighted(Rect rect,Vector2 pointer,bool acceptsHover)=>
        acceptsHover&&rect.Contains(pointer);

    bool AtlasCard(string id,Rect rect,int artIndex,string caption){
        bool hover=AtlasCardHighlighted(rect,Event.current.mousePosition,GUI.enabled&&!Application.isMobilePlatform);
        if(!campaignChrome)AtlasPanel(rect,new Color(.07f,.095f,.081f),hover?AtlasGold:AtlasPaper);
        var art=new Rect(rect.x+12,rect.y+12,rect.width-24,rect.height-48);
        if(campaignCards){
            float tileWidth=campaignCards.width/3f;
            float fittedHeight=Mathf.Min(campaignCards.height,tileWidth*art.height/art.width);
            var crop=new Rect(tileWidth*artIndex+2,(campaignCards.height-fittedHeight)*.5f,tileWidth-4,fittedHeight);
            if(campaignChrome)DrawAtlasCardPhoto(art,crop);
            else Source(campaignCards,crop,art);
        }else if(campaignMap)GUI.DrawTexture(art,campaignMap,ScaleMode.ScaleAndCrop);
        if(campaignChrome)DrawAtlasChrome(hover?0:1,rect);
        var plate=new Rect(rect.x+14,rect.yMax-64,rect.width-28,55);
        if(campaignChrome)DrawAtlasBar(4,plate);
        else Fill(plate,AtlasPaper);
        DrawAtlasIcon(artIndex,new Rect(plate.x+20,plate.center.y-21,42,42),true);
        GUI.Label(new Rect(plate.x+62,plate.y,plate.width-70,plate.height),caption,atlasCardText);
        return Button(id,rect,"",false,GUIStyle.none);
    }

    string AtlasAccountName=>!HasEndpointToken?"":rememberedAccountName;

    string FitAtlasAccountName(string name,float width)=>FitAtlasLabel(name,width,atlasProfileName);

    static string FitAtlasLabel(string name,float width,GUIStyle style){
        if(string.IsNullOrEmpty(name))return "";
        var content=new GUIContent(name);
        if(style.CalcSize(content).x<=width)return name;
        var elements=StringInfo.ParseCombiningCharacters(name);
        for(int i=elements.Length-1;i>0;i--){
            content.text=name.Substring(0,elements[i])+"…";
            if(style.CalcSize(content).x<=width)return content.text;
        }
        return "…";
    }

    bool DrawAtlasAccount(){
        string name=AtlasAccountName;
        if(string.IsNullOrEmpty(name))
            return AtlasButton("main.profile",new Rect(1069,6,185,44),"Войти",icon:3);
        var rect=new Rect(AtlasSignedInButton.x,AtlasSignedInButton.y,262,AtlasSignedInButton.height);
        if(campaignChrome)DrawAtlasBar(3,rect);
        else AtlasPanel(rect,AtlasInk,AtlasGold);
        DrawProfileBadge(new Rect(rect.x+12,rect.y+5,34,34));
        var nameRect=new Rect(rect.x+57,rect.y,rect.width-AtlasNamePadding,rect.height);
        GUI.Label(nameRect,FitAtlasAccountName(name,nameRect.width),atlasProfileName);
        return Button("main.profile",rect,"",false,GUIStyle.none);
    }

    void DrawAtlasHeader(){
        var header=new Rect(0,0,1280,55);
        if(campaignPanel)DrawAtlasPart(campaignPanel,new Rect(.35f,.4f,.62f,.14f),header);
        else Fill(header,AtlasInk);
        Fill(new Rect(0,54,1280,1),AtlasGold);
        if(campaignChrome)DrawAtlasChrome(2,new Rect(25,0,74,77));
        else DrawProfileBadge(new Rect(27,7,48,48));
        GUI.Label(new Rect(112,0,480,52),"Castle Fight",atlasTitle);
        var rule=new Color(AtlasGold.r,AtlasGold.g,AtlasGold.b,.6f);
        Fill(new Rect(438,27,177,1),rule);
        Fill(new Rect(649,27,177,1),rule);
        for(int y=-4;y<=4;y++){
            int half=5-Mathf.Abs(y);
            Fill(new Rect(632-half,27+y,half*2+1,1),rule);
        }
        bool enabled=GUI.enabled;
        GUI.enabled=enabled&&!confirmDisplay;
        if(DrawAtlasAccount()){
            OpenAccountProfile();
            if(!HasNetworkProfile&&!HasEndpointToken)OpenAccountSignIn();
        }
        if(HasNetworkProfile)DrawFriendsShortcut(new Rect(1203,6,51,44));
        GUI.enabled=enabled;
    }

    void DrawCampaignMenu(){
        PrepareCampaignArtwork();
        EnsureCampaignStyles();
        Fill(new Rect(0,0,1280,720),new Color(.025f,.048f,.033f,.12f));
        DrawAtlasHeader();
        DrawAtlasChrome(5,new Rect(-34,584,205,140));
        if(AtlasCard("main.play",new Rect(88,346,352,265),0,"Сражение")){choosingBotRace=false;page=Page.Setup;}
        if(AtlasCard("main.challenges",new Rect(464,346,352,265),1,"Испытания"))OpenProgression(false);
        if(AtlasCard("main.network",new Rect(840,346,352,265),2,"Сетевая игра"))OpenNetwork();

        if(AtlasButton("main.viewer",new Rect(132,652,109,43),"Карта")){page=Page.Viewer;controls.Focus("west");}
        if(AtlasButton("main.intro",new Rect(248,652,109,43),"Ролик"))StartIntro(null);
        if(AtlasButton("main.quit",new Rect(364,652,109,43),"Выход"))Application.Quit();
        if(AtlasButton("main.guide",new Rect(891,651,174,46),"Учебник",icon:5))OpenGuide();
        if(AtlasButton("main.settings",new Rect(1077,651,176,46),"Настройки",icon:4))OpenSettings();
        GUI.Label(new Rect(532,689,217,18),(Application.isMobilePlatform?"ANDROID":"WINDOWS")+"  "+Version,
            new GUIStyle(small){padding=new RectOffset(),alignment=TextAnchor.MiddleCenter,fontSize=12,normal={textColor=AtlasInk}});
    }

    const float AtlasRowTop=164,AtlasRowPitch=51;
    static Rect AtlasOption(int row)=>new Rect(809,AtlasRowTop+row*AtlasRowPitch+4,302,43);

    void AtlasSettingLabel(int row,string name){
        float y=AtlasRowTop+row*AtlasRowPitch;
        Fill(new Rect(408,y+50,704,1),new Color(.58f,.51f,.35f,.35f));
        Text(415,y,373,48,name,label);
    }

    bool AtlasSetting(int row,string id,string name,string value){
        AtlasSettingLabel(row,name);
        return AtlasButton(id,AtlasOption(row),value);
    }

    bool AtlasToggle(int row,string id,string text,bool selected){
        AtlasSettingLabel(row,text);
        var option=AtlasOption(row);
        Text(829,option.y,167,option.height,selected?"Включено":"Выключено",small);
        var rail=new Rect(1037,option.y+5,74,31);
        GUI.DrawTexture(rail,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,
            selected?new Color(.33f,.45f,.19f):new Color(.16f,.22f,.17f),0,15);
        var knob=new Rect(selected?1084:1040,rail.y+3,24,25);
        GUI.DrawTexture(knob,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,
            selected?AtlasIvory:new Color(.55f,.59f,.48f),0,12);
        return Button(id,option,"",false,GUIStyle.none);
    }

    float AtlasSlider(int row,string name,float value,float minimum,float maximum,string amount){
        AtlasSettingLabel(row,name);
        var option=AtlasOption(row);
        Text(1040,option.y,68,option.height,amount,small);
        return GUI.HorizontalSlider(new Rect(option.x,option.y+13,217,30),value,minimum,maximum);
    }

    void DrawCampaignSettings(){
        EnsureCampaignStyles();
        if(settingsReturn==Page.Main)DrawAtlasHeader();
        if(campaignPanel)DrawAtlasPart(campaignPanel,AtlasUv(new Rect(20,33,1595,895),1635,962),new Rect(97,76,1085,612));
        else AtlasPanel(new Rect(104,76,1072,610),new Color(.064f,.103f,.076f,.98f),AtlasGold);
        GUI.Label(new Rect(140,90,680,55),"Настройки",atlasTitle);
        Fill(new Rect(140,147,985,1),AtlasGold);
        Fill(new Rect(374,164,1,412),new Color(.67f,.56f,.37f,.43f));

        GUI.enabled=!confirmDisplay;
        for(int i=0;i<AtlasTabs.Length;i++)
            if(AtlasButton("settings.tab."+i,new Rect(140,164+i*58,208,46),AtlasTabs[i],campaignSettingsTab==i,
                6+i,campaignSettingsTab==i))
                campaignSettingsTab=i;

        switch(campaignSettingsTab){
            case 0:DrawCampaignScreenOptions();break;
            case 1:
                draft.volume=AtlasSlider(0,"Общая громкость",draft.volume,0,1,(draft.volume*100).ToString("F0")+"%");
                draft.musicVolume=AtlasSlider(1,"Музыка в меню",draft.musicVolume,0,1,(draft.musicVolume*100).ToString("F0")+"%");
                break;
            case 2:
                draft.cameraSpeed=AtlasSlider(0,"Скорость камеры",draft.cameraSpeed,.5f,2,draft.cameraSpeed.ToString("F1")+"×");
                Text(415,231,670,52,SettingsLayout035.Mobile?"Камера · проведите по полю одним пальцем":"Камера · подведите курсор к краю экрана",small);
                break;
            case 3:
                if(AtlasSetting(0,"settings.models","Модели бойцов",HdModels.Draft?"Reforged":"Классические")){
                    MapArt.Panel=false;HdModels.Panel=true;
                }
                if(AtlasSetting(1,"settings.map","Оформление карты",MapArt.Draft?"Reforged":"Классическая")){
                    MapArt.Panel=true;HdModels.Panel=true;
                }
                if(MobileContent.Active||SettingsLayout035.PreviewMobile){
                    if(AtlasSetting(2,"settings.downloads","Ресурсы игры","Загрузки"))showResourceDownloads=true;
                }
                if(AtlasSetting(MobileContent.Active||SettingsLayout035.PreviewMobile?3:2,"settings.updates","Обновления игры","Проверить")){
                    showUpdatePanel=true;
                    StartCoroutine(CheckGameRelease());
                }
                break;
        }

        Fill(new Rect(140,591,985,1),new Color(.64f,.54f,.37f,.65f));
        if(AtlasButton("settings.cancel",new Rect(409,611,248,46),"Назад")){
            if(confirmDisplay)RevertDisplay();
            page=settingsReturn;
        }
        if(AtlasButton("settings.apply",new Rect(861,611,250,46),"Применить",true))PreviewSettings();
        GUI.enabled=true;
    }

    void DrawCampaignScreenOptions(){
        bool enabled=GUI.enabled;
        bool mobile=SettingsLayout035.Mobile;
        GUI.enabled=enabled&&!mobile;
        if(AtlasSetting(0,"settings.resolution","Разрешение",mobile?"По экрану устройства":sizes[draft.resolution]))
            draft.resolution=(draft.resolution+1)%sizes.Length;
        if(AtlasSetting(1,"settings.fullscreen","Режим экрана",mobile?"На весь экран":draft.fullscreen?"Без рамки на весь экран":"В окне"))
            draft.fullscreen=!draft.fullscreen;
        GUI.enabled=enabled;
        if(AtlasSetting(2,"settings.quality","Качество изображения",AtlasQuality[draft.quality]))draft.quality=(draft.quality+1)%3;
        if(AtlasSetting(3,"settings.fps","Ограничение кадров",draft.fps==0?"Без ограничения":draft.fps+" FPS"))
            draft.fps=draft.fps==30?60:draft.fps==60?90:draft.fps==90?120:draft.fps==120?0:30;
        if(AtlasToggle(4,"settings.vsync","Вертикальная синхронизация",draft.vsync))draft.vsync=!draft.vsync;
        GUI.enabled=enabled&&activeChallenge==null;
        if(activeChallenge!=null)AtlasSetting(5,"settings.fog","Туман войны","Включён в испытании");
        else if(AtlasToggle(5,"settings.fog","Туман войны",draft.fogOfWar))draft.fogOfWar=!draft.fogOfWar;
        GUI.enabled=enabled;
        if(AtlasToggle(6,"settings.softfog","Простое затемнение",draft.softFog))draft.softFog=!draft.softFog;
        if(AtlasSetting(7,"settings.theme","Тема боевого интерфейса",HudTheme.Names[DraftTheme]))ThemesOpen=true;
    }
}
}
