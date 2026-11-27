using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    // The settings draft and saved options each have their own preferences.
    sealed class HudThemePreference {
        static readonly ConditionalWeakTable<Options,HudThemePreference> Preferences=new ConditionalWeakTable<Options,HudThemePreference>();
        public int Choice;
        public bool Picker;
        public static HudThemePreference For(Options options)=>Preferences.GetValue(options,_=>new HudThemePreference());
    }

    sealed class HudTheme {
        public static readonly string[] Keys={"original","royal","forge","moon","field","command-desk"};
        public static readonly string[] Names={"Оригинальный","Королевский бастион","Чёрная кузня","Лунный дозор","Полевой штаб","Штабной"};
        public static readonly string[] Descriptions={
            "Знакомые рамки с листвой, камнем и деревом.",
            "Синяя сталь, золотая окантовка и гранёные углы.",
            "Тёмный металл, медные кромки и заклёпки.",
            "Серебристые рамки, мягкое свечение и круглая миникарта.",
            "Тонкие панели и компактная сетка команд.",
            "Сталь и бронза, крупный портрет и полосы здоровья и способностей."
        };
        static readonly HudTheme[] Styles={
            null,
            new HudTheme(0x0b1423,0x243c5a,0xaa854f,0xf2d294,9,false),
            new HudTheme(0x171414,0x433028,0xb47751,0xffc28d,7,false),
            new HudTheme(0x18162a,0x34345b,0x938aba,0xe8dfff,12,true),
            new HudTheme(0x141d18,0x2d382d,0x748574,0xe4eacb,3,false),
            new HudTheme(0x17181a,0x2d2d2e,0xa78354,0xf6deb1,8,false,true)
        };

        public readonly Color Edge, Accent, Base;
        readonly Color light;
        readonly int corner;
        readonly bool rounded,brushedSteel;
        Texture2D frameTexture, radarTexture, steelTexture;
        GUIStyle frame, top, name, detail;

        HudTheme(int ink,int light,int edge,int accent,int corner,bool rounded,bool brushedSteel=false){
            Base=Rgb(ink);this.light=Rgb(light);Edge=Rgb(edge);Accent=Rgb(accent);
            this.corner=corner;this.rounded=rounded;this.brushedSteel=brushedSteel;
        }
        static Color Rgb(int color)=>new Color32((byte)(color>>16),(byte)(color>>8),(byte)color,255);
        public static HudTheme Get(int choice)=>Styles[choice];
        public static int Parse(string key)=>Mathf.Max(0,Array.IndexOf(Keys,key));
        public static void Dispose(){
            for(int i=1;i<Styles.Length;i++){
                var theme=Styles[i];
                if(theme.frameTexture)UnityEngine.Object.Destroy(theme.frameTexture);
                if(theme.radarTexture)UnityEngine.Object.Destroy(theme.radarTexture);
                if(theme.steelTexture)UnityEngine.Object.Destroy(theme.steelTexture);
                theme.frameTexture=theme.radarTexture=theme.steelTexture=null;
                theme.frame=theme.top=theme.name=theme.detail=null;
            }
        }
        public void DrawFrame(Rect rect){
            if(Event.current.type!=EventType.Repaint)return;
            if(!frameTexture){
                frameTexture=MakeSurface(false);
                int margin=corner+3;
                frame=new GUIStyle{border=new RectOffset(margin,margin,margin,margin),normal={background=frameTexture}};
            }
            frame.Draw(rect,false,false,false,false);
        }
        public void DrawRadar(Rect rect){
            if(Event.current.type!=EventType.Repaint)return;
            if(!radarTexture)radarTexture=MakeSurface(true);
            GUI.DrawTexture(rect,radarTexture);
        }
        Texture2D MakeSurface(bool radar){
            const int size=64;
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++){
                float edge=Mathf.Min(Mathf.Min(x,y),Mathf.Min(size-x-1,size-y-1));
                if(radar)edge=31-Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f));
                else {
                    float cx=Mathf.Max(corner-Mathf.Min(x,size-x-1),0);
                    float cy=Mathf.Max(corner-Mathf.Min(y,size-y-1),0);
                    edge=Mathf.Min(edge,corner-(rounded?Mathf.Sqrt(cx*cx+cy*cy):cx+cy));
                }
                Color color=Color.Lerp(Base,light,Mathf.SmoothStep(0,1,y/(size-1f)));
                if(Edge.r>Edge.b&&corner==7&&((x+y)%5)==0)color=Color.Lerp(color,Edge,.12f);
                if(edge<1)color=Edge;
                else if(edge<2)color=Color.Lerp(Base,Color.black,.65f);
                else if(edge<3)color=Color.Lerp(Edge,Base,.45f);
                else if(brushedSteel&&edge<4)color=Color.Lerp(Edge,Accent,.35f);
                color.a=Mathf.Clamp01(edge+.65f)*.98f;
                pixels[y*size+x]=color;
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply(false,true);
            return texture;
        }
        public void DrawDock(Rect rect){
            DrawFrame(rect);
            if(brushedSteel&&Event.current.type==EventType.Repaint){
                if(!steelTexture)steelTexture=MakeSteel();
                const float inset=7;
                var surface=new Rect(rect.x+inset,rect.y+inset,rect.width-inset*2,rect.height-inset*2);
                GUI.DrawTextureWithTexCoords(surface,steelTexture,new Rect(0,0,surface.width/128f,surface.height/128f));
                var bronze=Color.Lerp(Edge,Accent,.23f);
                DrawRivet(rect.x+9,rect.y+9,bronze);
                DrawRivet(rect.xMax-9,rect.y+9,bronze);
                DrawRivet(rect.x+9,rect.yMax-9,bronze);
                DrawRivet(rect.xMax-9,rect.yMax-9,bronze);
            }else if(corner==7){
                var pin=Color.Lerp(Edge,Accent,.45f);
                GUI.DrawTexture(new Rect(rect.x+8,rect.y+6,3,3),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,pin,0,1.5f);
                GUI.DrawTexture(new Rect(rect.xMax-11,rect.y+6,3,3),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,pin,0,1.5f);
            }
        }
        Texture2D MakeSteel(){
            const int size=128;
            var pixels=new Color32[size*size];
            uint grain=5172;
            for(int y=0;y<size;y++){
                float brush=.06f*Mathf.Sin(y*Mathf.PI/2)+.07f*Mathf.Sin(y*Mathf.PI/8);
                for(int x=0;x<size;x++){
                    grain^=grain<<13;grain^=grain>>17;grain^=grain<<5;
                    float speck=(grain&255)/255f-.5f;
                    float pits=Mathf.PerlinNoise((x+15)*.15f,(y+41)*.23f);
                    float fade=Mathf.Min(1,Mathf.Min(Mathf.Min(x,y),Mathf.Min(size-1-x,size-1-y))/8f);
                    float tone=.38f+fade*((pits-.5f)*.64f+brush+speck*.32f);
                    pixels[y*size+x]=Color.Lerp(Base,light,tone);
                }
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){
                hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Point
            };
            texture.SetPixels32(pixels);texture.Apply(false,true);
            return texture;
        }
        static void DrawRivet(float x,float y,Color bronze){
            var rect=new Rect(x-2,y-2,4,4);
            GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,bronze,0,2);
            GUI.DrawTexture(new Rect(x-1,y,2,1),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.29f,.21f,.14f),0,0);
        }
        public GUIStyle Top(GUIStyle basis){
            if(top!=null)return top;
            top=new GUIStyle(basis){
                border=new RectOffset(),alignment=TextAnchor.MiddleCenter,fontSize=14,padding=new RectOffset(2,2,0,0),
                normal={background=null,textColor=Accent},hover={background=null,textColor=Color.white},
                active={background=null,textColor=Color.white},focused={background=null,textColor=Accent}
            };
            return top;
        }
        public GUIStyle Name(GUIStyle basis)=>name??(name=new GUIStyle(basis){normal={textColor=Accent}});
        public GUIStyle Detail(GUIStyle basis)=>detail??(detail=new GUIStyle(basis){normal={textColor=Color.Lerp(Accent,Color.white,.58f)}});
    }

    int HudThemeIndex=>HudThemePreference.For(options).Choice;
    const int CommandDeskTheme=5;
    bool CommandDeskHud=>HudThemeIndex==CommandDeskTheme;
    HudTheme BattleTheme=>HudTheme.Get(HudThemeIndex);
    bool ThemesOpen {get=>HudThemePreference.For(draft).Picker;set=>HudThemePreference.For(draft).Picker=value;}
    int DraftTheme {get=>HudThemePreference.For(draft).Choice;set=>HudThemePreference.For(draft).Choice=value;}
    bool WideCommandGrid=>HudThemeIndex==4&&!Application.isMobilePlatform;
    Rect HudCommandPanel=>CommandDeskHud?CommandDeskCommands(SettingsLayout035.Mobile):
        Application.isMobilePlatform?new Rect(960,482,315,236):new Rect(976,WideCommandGrid?568:543,299,175);
    Rect HudMinimapPanel=>CommandDeskHud?new Rect(5,540,282,176):
        HudThemeIndex==3?new Rect(27,530,186,186):new Rect(5,543,282,175);
    static Rect ThemedMinimapButtons=>new Rect(245,545,42,111);
    static Rect ThemedCard=>new Rect(325,561,482,157);
    static Rect ThemedInventory=>new Rect(814,555,145,163);
    Rect HudMap=>CommandDeskHud?new Rect(15,551,224,151):
        HudThemeIndex==3?new Rect(49,584,142,79):new Rect(14,577,224,107);
    bool ThemedHudBottomContains(Vector2 point)=>HudMinimapPanel.Contains(point)||ThemedMinimapButtons.Contains(point)||
        ThemedCard.Contains(point)||ThemedInventory.Contains(point)||HudCommandPanel.Contains(point)||
        CommandDeskHud&&CommandDeskBase.Contains(point);

    static Rect CommandDeskBase=>new Rect(0,536,1280,184);
    static Rect CommandDeskInventory=>new Rect(811,540,147,176);
    static Rect CommandDeskInventoryTitle=>new Rect(820,545,128,23);
    static Rect CommandDeskCommands(bool mobile)=>new Rect(961,mobile?492:540,315,mobile?225:177);
    static Rect CommandDeskSlot(int column,int row,bool mobile){
        var panel=CommandDeskCommands(mobile);
        int size=mobile?62:48;
        return new Rect(panel.x+12+column*73,panel.y+(mobile?24:23)+row*(mobile?65:50),size,size);
    }

    void DrawDeskTitle(string title){
        var rect=new Rect(492,548,304,29);
        var style=new GUIStyle(hudTitle){fontSize=19,wordWrap=false,normal={textColor=BattleTheme.Accent}};
        var content=new GUIContent(title);
        while(style.fontSize>13&&style.CalcSize(content).x>rect.width)style.fontSize--;
        GUI.Label(rect,content,style);
    }

    void DrawDeskMeter(Rect rect,float fraction,Color color,string caption){
        Fill(rect,new Color(.022f,.022f,.025f,.98f));
        var inside=new Rect(rect.x+2,rect.y+2,(rect.width-4)*Mathf.Clamp01(fraction),rect.height-4);
        Fill(inside,color);
        Fill(new Rect(inside.x,inside.y,inside.width,2),Color.Lerp(color,Color.white,.24f));
        Border(rect,BattleTheme.Edge);
        GUI.Label(rect,caption,new GUIStyle(hudSmall){
            alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,normal={textColor=Color.white}
        });
    }

    void DrawDeskHealth(float health,float maximum){
        DrawDeskMeter(new Rect(492,582,302,18),health/Mathf.Max(1,maximum),new Color(.22f,.57f,.10f),
            Mathf.CeilToInt(health)+" / "+Mathf.CeilToInt(maximum));
    }

    void DrawThemeSettings(){
        AtlasSurface(new Rect(250,44,780,632),true);
        Text(290,68,620,46,"Тема боевого интерфейса",atlasTitle);Accent(290,118,685);
        AtlasSurface(new Rect(645,144,342,426));
        for(int i=0;i<HudTheme.Names.Length;i++){
            var row=new Rect(290,145+75*i,338,59);
            if(Button("settings.theme."+HudTheme.Keys[i],row,HudTheme.Names[i],DraftTheme==i))DraftTheme=i;
        }
        var chosenTheme=HudTheme.Get(DraftTheme);
        Text(662,152,300,39,HudTheme.Names[DraftTheme],new GUIStyle(heading){fontSize=22});
        Text(662,202,296,59,HudTheme.Descriptions[DraftTheme],label);
        var previewRect=new Rect(662,294,300,154);
        AtlasTile(previewRect,false,false);
        if(minimapTexture)Source(minimapTexture,new Rect(0,52,256,153),previewRect);
        if(chosenTheme==null){
            for(int i=0;i<3;i++)Source(hudTiles[i],new Rect(0,176,hudTiles[i].width,336),new Rect(662+i*100,385,100,63));
        }else{
            var map=new Rect(668,378,69,62);
            if(DraftTheme==3)chosenTheme.DrawRadar(new Rect(671,373,69,69));else chosenTheme.DrawDock(map);
            chosenTheme.DrawDock(new Rect(742,389,121,51));
            chosenTheme.DrawDock(new Rect(868,385,87,55));
            GUI.DrawTexture(new Rect(750,398,31,31),CommandIcon(0));
            Fill(new Rect(789,410,64,6),new Color(.25f,.61f,.31f));
            Fill(new Rect(789,422,42,5),DraftTheme==CommandDeskTheme?new Color(.88f,.53f,.21f):new Color(.25f,.46f,.72f));
            for(int i=0;i<3;i++)GUI.DrawTexture(new Rect(874+25*i,403,22,22),CommandIcon(i+4));
        }
        if(Button("settings.theme.back",new Rect(290,603,240,46),"К настройкам"))ThemesOpen=false;
        if(Button("settings.theme.apply",new Rect(675,603,300,46),"Применить",true)){ThemesOpen=false;PreviewSettings();}
    }

    void DrawThemedConsole(HudTheme theme){
        if(CommandDeskHud){
            theme.DrawFrame(new Rect(0,0,1280,Application.isMobilePlatform?44:29));
            theme.DrawFrame(new Rect(549,2,181,50));
            theme.DrawFrame(CommandDeskBase);
        }else{
            theme.DrawFrame(new Rect(2,1,546,Application.isMobilePlatform?42:28));
            theme.DrawFrame(new Rect(736,1,540,28));
            theme.DrawFrame(new Rect(549,1,181,53));
        }
        var font=theme.Top(button);
        GUI.Label(new Rect(555,4,49,20),match.CastleHp[0].ToString("F0"),new GUIStyle(font){normal={textColor=new Color(.61f,.79f,1)}});
        GUI.Label(new Rect(672,4,50,20),match.CastleHp[1].ToString("F0"),new GUIStyle(font){normal={textColor=new Color(1,.64f,.51f)}});
        GUI.Label(new Rect(606,4,63,20),"Раунд "+series.Round,font);
        GUI.Label(new Rect(556,24,167,23),TimeSpan.FromSeconds(match.Elapsed).ToString(@"mm\:ss"),font);
        if(HudThemeIndex==3){theme.DrawRadar(HudMinimapPanel);theme.DrawDock(ThemedMinimapButtons);}
        else theme.DrawDock(HudMinimapPanel);
        var map=HudMap;
        Border(new Rect(map.x-1,map.y-1,map.width+2,map.height+2),theme.Edge);
        theme.DrawDock(CommandDeskHud?new Rect(292,540,516,176):ThemedCard);
        theme.DrawDock(CommandDeskHud?CommandDeskInventory:ThemedInventory);
        theme.DrawDock(HudCommandPanel);
        GUI.Label(new Rect(986,HudCommandPanel.y+3,212,16),selectedCastle?"Магазин":buildMenu?"Строительство":"Команды",hudSmall);
        if(CommandDeskHud){
            if(selectedCastle||builderSelected||SelectedHut!=null||SelectedUnit!=null){
                var portrait=BattlePortraitRect;
                theme.DrawFrame(new Rect(portrait.x-5,portrait.y-5,portrait.width+10,portrait.height+10));
            }
        }else if(builderSelected||SelectedHut!=null||SelectedUnit!=null)theme.DrawFrame(new Rect(343,586,119,132));
        for(int i=0;i<6;i++)DrawThemedCell(InventorySlot(i));
        for(int i=0;i<12;i++)DrawThemedCell(Slot(i%4,i/4));
    }

    void DrawThemedCell(Rect cell){
        Fill(cell,new Color(.018f,.023f,.028f,.9f));
        Border(cell,BattleTheme.Edge);
    }

    Rect ThemedCommandSlot(int column,int row){
        if(CommandDeskHud)return CommandDeskSlot(column,row,SettingsLayout035.Mobile);
        if(Application.isMobilePlatform)return CommandSlot(column,row,true);
        if(WideCommandGrid){
            int index=row*4+column;
            return new Rect(990+index%6*45,595+index/6*53,42,42);
        }
        return new Rect(991+column*67,564+row*49,48,46);
    }

    IEnumerator ExerciseHudThemes(){
        var folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"theme-validation"));
        Directory.CreateDirectory(folder);
        Check(HudThemeIndex==0,"A settings file without hudTheme keeps the original interface");
        MainMenu();
        yield return Shot(folder,"10-campaign-menu");
        Check(campaignMap&&campaignCards,"Campaign atlas and three mode illustrations loaded");
        Check(campaignChrome&&campaignIcons&&campaignPanel,"The campaign uses generated chrome, heraldic icons and settings art");
        Check(campaignScenes&&campaignScenes.width==1536&&campaignScenes.height==1024,
            "Campaign illustration atlas loads from the player");
        Check(loadingBackground&&loadingSiege,"The loading landscape and transparent siege atlas are available");
        var lowSmoke=CastleLoadingMotion.SmokePuff(0,1.5);
        var highSmoke=CastleLoadingMotion.SmokePuff(0,3.0);
        var loopedSmoke=CastleLoadingMotion.SmokePuff(0,1.5+CastleLoadingMotion.LoopSeconds);
        Check(highSmoke.y<lowSmoke.y&&highSmoke.width>lowSmoke.width&&loopedSmoke.y==lowSmoke.y&&
            CastleLoadingMotion.SmokePuff(0,0).opacity==0,
            "Smoke rises, spreads and loops without a visible reset");
        float glow=CastleLoadingMotion.WindowGlow(0,.32).opacity;
        Check(CastleLoadingMotion.WindowGlow(0,.72).opacity!=glow&&
            Mathf.Approximately(CastleLoadingMotion.WindowGlow(0,.32+CastleLoadingMotion.LoopSeconds).opacity,glow),
            "Window light changes continuously and repeats on a fixed castle");
        polishLoadingPreview=true;
        loadingPreviewTime=.3f;
        yield return Shot(folder,"28-loading-smoke-early");
        loadingPreviewTime=4.1f;
        yield return Shot(folder,"29-loading-smoke-late");
        polishLoadingPreview=false;
        loadingPreviewTime=null;
        showUpdatePanel=true;
        yield return Shot(folder,"30-update-local-notes");
        var notes=new JObject{["version"]="0.49.1",["changes_ru"]=new JArray("",5,"Сводка первой правки.","Сводка второй правки."),
            ["android"]=new JObject{["url"]="https://example.invalid/CastleFight.apk",["size"]=100,["sha256"]=new string('0',64)}};
        SetAvailableRelease(notes);
        Check(publishedChangeNotes.Length==2&&publishedChangeNotes[0]=="Сводка первой правки.",
            "The update window reads published notes as plain text and ignores invalid rows");
        updateStatus="Доступна версия 0.49.1";
        yield return Shot(folder,"31-update-remote-notes");
        SettingsLayout035.PreviewMobile=true;
        yield return Shot(folder,"32-update-android");
        downloadingUpdate=true;updateProgress=.61f;updateStatus="Скачиваем обновление · 61%";
        yield return Shot(folder,"33-update-android-progress");
        downloadingUpdate=false;
        SettingsLayout035.PreviewMobile=false;
        yield return Action("updates.close");
        Check(!showUpdatePanel,"Closing the update window returns to the current page");
        SetAvailableRelease(null);
        var battleCard=new Rect(88,346,352,265);
        Check(!AtlasCardHighlighted(battleCard,new Vector2(640,300),true)&&
            AtlasCardHighlighted(battleCard,battleCard.center,true)&&
            !AtlasCardHighlighted(battleCard,battleCard.center,false),
            "Cards start neutral, highlight under a pointer and stay neutral without hover");
        Check(AtlasAccountName=="","The signed-out header uses the profile button");
        networkSessionEndpoint=directoryEndpoint;
        networkSessionToken="header-test-token";
        ApplyAccountProfile(new JObject{["name"]="Крепостной Командир"});
        float nameWidth=AtlasSignedInButton.width-AtlasNamePadding;
        Check(AtlasAccountName=="Крепостной Командир"&&ProfileAvatar()&&
            FitAtlasAccountName(AtlasAccountName,nameWidth)==AtlasAccountName,
            "Signed-in header fits the account name and resolves the profile avatar");
        yield return Shot(folder,"13-campaign-signed-in");
        ApplyAccountProfile(new JObject{["name"]="WWWWWWWWWWWWWWWWWWWWWWWW"});
        Check(AtlasAccountName=="WWWWWWWWWWWWWWWWWWWWWWWW"&&
            FitAtlasAccountName(AtlasAccountName,nameWidth).EndsWith("…"),
            "A changed nickname updates the header and long names fit");
        networkProfile=null;
        Check(AtlasAccountName.Length>0,"A temporary directory disconnect preserves the saved account label");
        networkGuest=true;
        Check(AtlasAccountName=="","Guest mode hides the account identity");
        networkGuest=false;
        ClearAccountIdentity();
        networkName="";
        Check(AtlasAccountName==""&&!HasNetworkProfile,"Signing out restores the anonymous profile button");
        yield return Action("main.play");
        Check(page==Page.Setup,"The battle card opens the usual race setup");
        yield return Action("setup.blackhand");
        yield return Action("setup.enemy");
        yield return Action("setup.enemy.2");
        Check(raceChoice==BlackHand.Race&&botRaceChoice==2,"Each race grid changes only its own side");
        yield return Shot(folder,"14-campaign-races");
        yield return Action("setup.enemy.same");
        Check(botRaceChoice==-1,"The opponent can reuse the player's race");
        MainMenu();
        yield return Action("main.challenges");
        Check(page==Page.Progression,"The challenge card opens the campaign list");
        yield return Shot(folder,"15-campaign-challenges");
        yield return Action("progress.siege");
        Check(Workshop.siegeOnly,"The siege filter is available");
        yield return Action("progress.mythic");
        yield return Action("challenge."+HumanMatch.ChoirTrial);
        Check(selectedMythicId==HumanMatch.ChoirTrial,"Choosing another boss changes the leaderboard key");
        yield return Shot(folder,"16-campaign-mythic");
        yield return Action("mythic.play."+HumanMatch.ChoirTrial);
        Check(MythicRacePicker,"Play opens the selected challenge and its race chooser");
        var mythicCell=MythicRaceCell(2);
        var mythicName=MythicRaceNameRect(mythicCell);
        var mythicLabel=MythicRaceNameStyle();
        Check(MythicRacePanel.xMax-mythicCell.xMax>=27&&mythicCell.xMax-mythicName.xMax>=14,
            "The boss race grid and the labels have right padding");
        Check(AllRaceNames.All(name=>FitMythicRaceName(mythicLabel,new GUIContent(name),mythicName)<=mythicName.height),
            "Every race name fits in the padded boss chooser cell");
        yield return Shot(folder,"17-campaign-boss-races");
        yield return Action("progress.back");
        yield return Action("progress.shop");
        Check(WorkshopVisible,"Workshop still opens from Challenges");
        yield return Shot(folder,"18-campaign-workshop");
        MainMenu();
        yield return Action("main.network");
        Check(page==Page.Network,"The multiplayer card opens the directory");
        directoryReady=directoryCanCreate=true;
        directoryStatus="Подключено · открытых игр: 1";
        networkServerRaces=networkRaceNames;
        directoryRooms=new JArray(new JObject{["host"]="Орден Рассвета",["players"]=1,["capacity"]=2,["joinable"]=true});
        yield return Shot(folder,"19-campaign-network");
        ChangeRace();
        yield return Shot(folder,"20-campaign-network-races");
        yield return Action("network.race.select.2");
        Check(networkRace==2&&!showNetworkRacePicker,"The network picker keeps server race selection");
        networkSlot=0;
        networkRoom=new JObject{["code"]="ATLAS",
            ["config"]=new JObject{["gold"]=500,["rounds"]=3,["fog"]=true,["aspects"]=true},
            ["players"]=new JArray(
                new JObject{["name"]="Орден Рассвета",["ready"]=true,["race"]=networkRaceNames[2]},
                new JObject{["name"]="Страж рубежа",["ready"]=true,["race"]=networkRaceNames[1]})};
        roomMessages=new JArray(new JObject{["name"]="Страж рубежа",["text"]="Готов к бою."});
        yield return Shot(folder,"25-campaign-network-room");
        yield return Action("network.change-race");
        Check(showNetworkRacePicker,"A room can change its race without leaving");
        yield return Action("network.race.close");
        StopDirectory();
        MainMenu();
        yield return Action("main.profile");
        Check(page==Page.Profile,"The header opens the player profile");
        Check(ProfileAccountButton.y==ProfileBackButton.y&&ProfileBackButton.x-ProfileAccountButton.xMax>=16,
            "Profile actions share one row with a gap between them");
        yield return Shot(folder,"21-campaign-profile");
        yield return Action("account-profile.challenges");
        Check(page==Page.Progression,"The empty profile links to the challenge selection");
        MainMenu();
        yield return Action("main.profile");
        yield return Action("account-profile.all");
        Check(showAllProfileChallenges,"Profile still switches between awards and all trials");
        yield return Shot(folder,"26-campaign-profile-medals");
        MainMenu();
        yield return Action("main.guide");
        Check(page==Page.Guide,"Campaign menu opens the original interactive guide");
        yield return Shot(folder,"27-campaign-guide");
        yield return Action("guide.menu");
        MainMenu();
        yield return Action("main.viewer");
        yield return Action("view.top");
        yield return Shot(folder,"22-campaign-map-viewer");
        yield return Action("viewer.map-settings");
        Check(settingsReturn==Page.Viewer&&MapArt.Panel,"Map viewer opens map edition settings");
        yield return Action("map.back");
        yield return Action("settings.cancel");
        Check(page==Page.Viewer,"Settings returns to the map viewer");
        MainMenu();
        yield return Action("main.settings");
        yield return Shot(folder,"11-campaign-settings");
        int originalFps=options.fps;
        yield return Action("settings.fps");
        Check(draft.fps!=originalFps&&options.fps==originalFps,"Display settings are edited in the draft");
        yield return Action("settings.tab.1");
        Check(campaignSettingsTab==1,"The sound tab opens");
        yield return Action("settings.tab.2");
        Check(campaignSettingsTab==2,"The controls tab opens");
        yield return Action("settings.tab.3");
        Check(campaignSettingsTab==3,"The resource tab opens");
        yield return Action("settings.models");
        yield return Action("models.reforged");
        Check(HdModels.Draft&&!HdModels.Preferred&&!HdModels.Busy,"Choosing HD models only changes the draft");
        yield return Shot(folder,"23-campaign-hd-models");
        yield return Action("models.map");
        yield return Action("map.reforged");
        Check(MapArt.Draft&&!MapArt.Preferred&&!MapArt.Busy,"Choosing Reforged map only changes the draft");
        yield return Shot(folder,"24-campaign-hd-map");
        yield return Action("map.models");
        yield return Action("models.classic");
        Check(!HdModels.Draft&&MapArt.Draft,"The model and map styles are independent");
        yield return Action("models.back");
        yield return Action("settings.map");
        yield return Action("map.classic");
        yield return Action("map.back");
        Check(!MapArt.Draft&&!HdModels.Draft,"Both styles can return to classic");
        SettingsLayout035.PreviewMobile=true;
        yield return Shot(folder,"12-campaign-resources-mobile");
        SettingsLayout035.PreviewMobile=false;
        yield return Action("settings.tab.0");
        yield return Action("settings.theme");
        yield return Action("settings.theme.royal");
        Check(DraftTheme==1&&HudThemeIndex==0,"Theme selection stays in the draft");
        yield return Shot(folder,"00-theme-settings");
        yield return Action("settings.theme.back");
        yield return Action("settings.cancel");
        Check(HudThemeIndex==0&&options.fps==originalFps,"Cancel leaves the original theme and frame-rate setting active");
        yield return Action("main.settings");
        yield return Action("settings.tab.3");
        yield return Action("settings.map");
        yield return Action("map.reforged");
        yield return Action("map.back");
        yield return Action("settings.apply");
        var saved=JObject.Parse(File.ReadAllText(settingsPath));
        Check((string)saved["mapStyle"]=="reforged"&&(string)saved["modelStyle"]=="classic",
            "Applying settings writes independent map and model preferences to disk");
        yield return Action("main.settings");
        yield return Action("settings.tab.3");
        yield return Action("settings.map");
        Check(MapArt.Draft&&!HdModels.Draft,"Reopening settings restores the chosen map and classic models");
        yield return Action("map.classic");
        yield return Action("map.back");
        yield return Action("settings.apply");
        raceChoice=0;botRaceChoice=1;aspectsOn=false;NewMatch();testHoldAuthority=true;
        yield return Shot(folder,"01-original");
        for(int i=1;i<HudTheme.Keys.Length;i++){
            page=Page.Pause;
            yield return Action("pause.settings");
            yield return Action("settings.theme");
            yield return Action("settings.theme."+HudTheme.Keys[i]);
            yield return Action("settings.theme.apply");
            Check(HudThemeIndex==i&&page==Page.Pause,"Theme applies from pause: "+HudTheme.Keys[i]);
            Check((string)JObject.Parse(File.ReadAllText(settingsPath))["hudTheme"]==HudTheme.Keys[i],"Settings saved: "+HudTheme.Keys[i]);
            LoadSettings();
            Check(HudThemeIndex==i,"Theme reloads from disk: "+HudTheme.Keys[i]);
            yield return Action("pause.resume");
            yield return Shot(folder,(i+1).ToString("00")+"-"+HudTheme.Keys[i]);
            yield return Action("hud.builder");
            yield return Action("builder.build");
            Check(builderSelected&&buildMenu&&HudContains(Slot(3,2).center),"Construction remains interactive: "+HudTheme.Keys[i]);
            yield return Action("builder.cancel");
        }
        if(CommandDeskHud){
            Check(HudContains(new Vector2(802,705))&&!HudContains(new Vector2(641,490)),"Desk panels consume input and leave the battlefield clickable");
            Check(BattlePortraitRect.width==BattlePortraitRect.height&&HudContains(BattlePortraitRect.center),"The enlarged portrait stays inside the HUD");
            var panel=CommandDeskCommands(true);
            bool mobileGrid=true;
            for(int row=0;row<3;row++)for(int col=0;col<4;col++){
                var cell=CommandDeskSlot(col,row,true);
                mobileGrid&=cell.width>=60&&cell.height>=60&&panel.Contains(cell.min)&&panel.Contains(cell.max);
                if(row>0)mobileGrid&=CommandDeskSlot(col,row-1,true).yMax<cell.yMin;
                if(col>0)mobileGrid&=CommandDeskSlot(col-1,row,true).xMax<cell.xMin;
            }
            Check(mobileGrid,"All 12 mobile command targets fit the panel without touching");
            Check(CommandDeskInventoryTitle.yMax<InventorySlot(0).yMin&&
                CommandDeskInventory.Contains(InventorySlot(0).min)&&CommandDeskInventory.Contains(InventorySlot(5).max)&&
                InventorySlot(0).yMin<590&&InventorySlot(5).yMax<700,
                "The inventory title and all slots fit with clear top and bottom padding");
            raceChoice=9;botRaceChoice=0;matchMode=0;NewMatch();testHoldAuthority=true;
            var elder=match.Spawn(0,match.Kind("h046"),match.Builders[0].position+new Vector2(8,2),true);
            elder.hp=elder.maxHp*.77f;
            elder.elementalLavaHits=12;
            elder.stun=3;
            SetSelectedGroup(new[]{elder.id});
            RenderMatch();
            yield return Action("portrait.focus");
            yield return Shot(folder,"07-desk-unit");
            SettingsLayout035.PreviewMobile=true;
            yield return Shot(folder,"08-desk-mobile-layout");
            SettingsLayout035.PreviewMobile=false;
            SelectCastle(false);
            yield return Shot(folder,"09-desk-shop");
        }
        page=Page.Pause;OpenSettings();
        yield return Action("settings.theme");
        yield return Action("settings.theme.original");
        yield return Action("settings.theme.apply");
        Check(HudThemeIndex==0&&BattleTheme==null,"Switch back to the original interface");
        File.WriteAllText(Path.Combine(folder,"verification.json"),new JObject{
            ["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["build"]=Version
        }.ToString());
        Debug.Log("CF_HUD_THEMES checks="+testChecks.Count+" failures="+testFailures);
        Application.Quit(testFailures==0?0:1);
    }
}
}
