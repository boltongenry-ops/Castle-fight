using System;
using System.Collections;
using System.IO;
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
        public static readonly string[] Keys={"original","royal","forge","moon","field"};
        public static readonly string[] Names={"Оригинальный","Королевский бастион","Чёрная кузня","Лунный дозор","Полевой штаб"};
        public static readonly string[] Descriptions={
            "Знакомые рамки с листвой, камнем и деревом.",
            "Синяя сталь, золотая окантовка и гранёные углы.",
            "Тёмный металл, медные кромки и заклёпки.",
            "Серебристые рамки, мягкое свечение и круглая миникарта.",
            "Тонкие панели и компактная сетка команд."
        };
        static readonly HudTheme[] Styles={
            null,
            new HudTheme(0x0b1423,0x243c5a,0xaa854f,0xf2d294,9,false),
            new HudTheme(0x171414,0x433028,0xb47751,0xffc28d,7,false),
            new HudTheme(0x18162a,0x34345b,0x938aba,0xe8dfff,12,true),
            new HudTheme(0x141d18,0x2d382d,0x748574,0xe4eacb,3,false)
        };

        public readonly Color Edge, Accent, Base;
        readonly Color light;
        readonly int corner;
        readonly bool rounded;
        Texture2D frameTexture, radarTexture;
        GUIStyle frame, top, name, detail;

        HudTheme(int ink,int light,int edge,int accent,int corner,bool rounded){
            Base=Rgb(ink);this.light=Rgb(light);Edge=Rgb(edge);Accent=Rgb(accent);
            this.corner=corner;this.rounded=rounded;
        }
        static Color Rgb(int color)=>new Color32((byte)(color>>16),(byte)(color>>8),(byte)color,255);
        public static HudTheme Get(int choice)=>Styles[choice];
        public static int Parse(string key)=>Mathf.Max(0,Array.IndexOf(Keys,key));
        public static void Dispose(){
            for(int i=1;i<Styles.Length;i++){
                var theme=Styles[i];
                if(theme.frameTexture)UnityEngine.Object.Destroy(theme.frameTexture);
                if(theme.radarTexture)UnityEngine.Object.Destroy(theme.radarTexture);
                theme.frameTexture=theme.radarTexture=null;
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
                color.a=Mathf.Clamp01(edge+.65f)*.98f;
                pixels[y*size+x]=color;
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply(false,true);
            return texture;
        }
        public void DrawDock(Rect rect){
            DrawFrame(rect);
            if(corner==7){
                var pin=Color.Lerp(Edge,Accent,.45f);
                GUI.DrawTexture(new Rect(rect.x+8,rect.y+6,3,3),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,pin,0,1.5f);
                GUI.DrawTexture(new Rect(rect.xMax-11,rect.y+6,3,3),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,pin,0,1.5f);
            }
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
    HudTheme BattleTheme=>HudTheme.Get(HudThemeIndex);
    bool ThemesOpen {get=>HudThemePreference.For(draft).Picker;set=>HudThemePreference.For(draft).Picker=value;}
    int DraftTheme {get=>HudThemePreference.For(draft).Choice;set=>HudThemePreference.For(draft).Choice=value;}
    bool WideCommandGrid=>HudThemeIndex==4&&!Application.isMobilePlatform;
    Rect HudCommandPanel=>Application.isMobilePlatform?new Rect(960,482,315,236):new Rect(976,WideCommandGrid?568:543,299,175);
    Rect HudMinimapPanel=>HudThemeIndex==3?new Rect(27,530,186,186):new Rect(5,543,282,175);
    static Rect ThemedMinimapButtons=>new Rect(245,545,42,111);
    static Rect ThemedCard=>new Rect(325,561,482,157);
    static Rect ThemedInventory=>new Rect(814,555,145,163);
    Rect HudMap=>HudThemeIndex==3?new Rect(49,584,142,79):new Rect(14,577,224,107);
    bool ThemedHudBottomContains(Vector2 point)=>HudMinimapPanel.Contains(point)||ThemedMinimapButtons.Contains(point)||
        ThemedCard.Contains(point)||ThemedInventory.Contains(point)||HudCommandPanel.Contains(point);

    void DrawThemeSettings(){
        Box(new Rect(250,44,780,632));
        Text(290,68,620,46,"Тема интерфейса",heading);Accent(290,118,685);
        for(int i=0;i<HudTheme.Names.Length;i++){
            var row=new Rect(290,145+75*i,338,59);
            if(Button("settings.theme."+HudTheme.Keys[i],row,HudTheme.Names[i],DraftTheme==i))DraftTheme=i;
        }
        var chosenTheme=HudTheme.Get(DraftTheme);
        Text(662,152,300,39,HudTheme.Names[DraftTheme],new GUIStyle(heading){fontSize=22});
        Text(662,202,296,59,HudTheme.Descriptions[DraftTheme],label);
        var previewRect=new Rect(662,294,300,154);
        Fill(previewRect,new Color(.035f,.055f,.068f));
        Border(previewRect,new Color(.32f,.32f,.26f));
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
            Fill(new Rect(789,422,42,5),new Color(.25f,.46f,.72f));
            for(int i=0;i<3;i++)GUI.DrawTexture(new Rect(874+25*i,403,22,22),CommandIcon(i+4));
        }
        if(Button("settings.theme.back",new Rect(290,603,240,46),"К настройкам"))ThemesOpen=false;
        if(Button("settings.theme.apply",new Rect(675,603,300,46),"Применить",true)){ThemesOpen=false;PreviewSettings();}
    }

    void DrawThemedConsole(HudTheme theme){
        theme.DrawFrame(new Rect(2,1,546,Application.isMobilePlatform?42:28));
        theme.DrawFrame(new Rect(736,1,540,28));
        theme.DrawFrame(new Rect(549,1,181,53));
        var font=theme.Top(button);
        GUI.Label(new Rect(555,4,49,20),match.CastleHp[0].ToString("F0"),new GUIStyle(font){normal={textColor=new Color(.61f,.79f,1)}});
        GUI.Label(new Rect(672,4,50,20),match.CastleHp[1].ToString("F0"),new GUIStyle(font){normal={textColor=new Color(1,.64f,.51f)}});
        GUI.Label(new Rect(606,4,63,20),"Раунд "+series.Round,font);
        GUI.Label(new Rect(556,24,167,23),TimeSpan.FromSeconds(match.Elapsed).ToString(@"mm\:ss"),font);
        if(HudThemeIndex==3){theme.DrawRadar(HudMinimapPanel);theme.DrawDock(ThemedMinimapButtons);}
        else theme.DrawDock(HudMinimapPanel);
        var map=HudMap;
        Border(new Rect(map.x-1,map.y-1,map.width+2,map.height+2),theme.Edge);
        theme.DrawDock(ThemedCard);
        theme.DrawDock(ThemedInventory);
        theme.DrawDock(HudCommandPanel);
        GUI.Label(new Rect(986,HudCommandPanel.y+3,212,16),selectedCastle?"Магазин":buildMenu?"Строительство":"Команды",hudSmall);
        if(builderSelected||SelectedHut!=null||SelectedUnit!=null)theme.DrawFrame(new Rect(343,586,119,132));
        for(int i=0;i<6;i++)DrawThemedCell(InventorySlot(i));
        for(int i=0;i<12;i++)DrawThemedCell(Slot(i%4,i/4));
    }

    void DrawThemedCell(Rect cell){
        Fill(cell,new Color(.018f,.023f,.028f,.9f));
        Border(cell,BattleTheme.Edge);
    }

    Rect ThemedCommandSlot(int column,int row){
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
        yield return Action("main.settings");
        yield return Action("settings.theme");
        yield return Action("settings.theme.royal");
        Check(DraftTheme==1&&HudThemeIndex==0,"Theme selection stays in the draft");
        yield return Shot(folder,"00-theme-settings");
        yield return Action("settings.theme.back");
        yield return Action("settings.cancel");
        Check(HudThemeIndex==0,"Cancel leaves the original theme active");
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
