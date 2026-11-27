using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
// Godot race_select.gd parity: the campaign race grid with the staged races
// selectable, the battle rules panel on the right and the saved match rules.
// Race names, tags and lore are copied from the Godot menu_style.gd tables.
public sealed partial class PcGame {
    static readonly string[] AllRaceIds={"chaos","elf","corrupted","human","orc","naga","northern","nightelf","undead","mechanical","nature","elementals","builder","nerubian","pandaren","centaur","pepe","blackhand"};
    static readonly string[] AllRaceNames={"Легионы Хаоса","Союз эльфов","Осквернённые","Альянс людей","Орочья Орда","Наги","Северные королевства","Ночные эльфы","Силы нежити","Коалиция механизмов","Силы природы","Мир элементалей","Высший строитель","Рой нерубов","Орден Нефритового Хмеля","Ханства Пустошей","Болотное братство Пепе","Клан Чёрной Руки"};
    static readonly string[] AllRaceTags={"Демоны · разрушение","Стрелки · чародеи","Скверна · проклятия","Пехота · артиллерия","Воины · шаманы","Глубины · морская магия","Лёд · северные воины","Охотницы · лунная магия","Некромантия · нежить","Машины · взрывчатка","Друиды · лесные создания","Четыре стихии","Случайные линии всех рас","Паутина · панцирь · яд","Выдержка · настои · три духа","Разгон · пески · поддержка","Ряска · щиты · засада","Пехота · проклятия · осада"};
    static readonly string[] AllRaceLore={
        "Демоны, огонь и разрушительная магия Хаоса.",
        "Стрелки, чародеи и древнее мастерство эльфов.",
        "Порождения скверны и тёмные заклинания.",
        "Пехота, рыцари, артиллерия и небесные грифоны.",
        "Сила воинов, шаманская магия и крылатые виверны.",
        "Воины глубин и магия морских ведьм.",
        "Северные воины и сковывающая сила льда.",
        "Лунная магия, охотницы и хранители леса.",
        "Армии мёртвых, некромантия и проклятия.",
        "Механизмы, взрывчатка и военные изобретения.",
        "Лесные создания, друиды и сила живой природы.",
        "Стихии камня, пламени, воздуха и воды.",
        "Каждый раунд — случайная смесь рас: по одной линии каждого тира, особые здания, башня и легендарная постройка. Набор сохраняется до конца раунда.",
        "Панцирный передний ряд, паутина против летунов и разъедающий яд. Хранители лечат рой, матриарх укрепляет строй.",
        "Выдержка, очищающие настои и хмельная магия. Монахи поддерживают передний ряд, а три духа меняют ход сражения.",
        "Лагеря автоматически выпускают кентавров. После разгона они усиливают первый удар. Тотемы замедляют врагов, барабанщицы поддерживают армию, а обоз лечит раненых. Новая раса: пока доступна в игре с ботом. Модели: Maximal, Ujimasa Hojo, Mephestrial и Filip95; иконки: Villagerino.",
        "Лучники помечают врагов Ряской, латники получают щит за удар по метке. Стрекозы атакуют с воздуха, баллисты замедляют группу, знахари лечат бойцов.","Пехота удерживает строй. Палачи добивают раненых, чернокнижники карают быстрых стрелков, камнемёты обстреливают площадь."};
    // Unity race index (0 humans, 1 orcs, 2 naga, 3 nature) where the race is staged.
    static readonly int[] AllRaceUnity={4,11,8,0,1,2,5,10,7,6,3,9,12,13,14,15,PepeContent.Race,BlackHand.Race};
    int matchMode;bool aspectsOn;Vector2 raceLoreScroll,raceRulesScroll;
    bool choosingBotRace;
    readonly JArray[] raceAspects=new JArray[MobileContent.RaceResources.Length];
    readonly Dictionary<string,Texture2D> raceEmblemCache=new Dictionary<string,Texture2D>();
    readonly Dictionary<string,Texture2D> aspectIconCache=new Dictionary<string,Texture2D>();
    GUIStyle raceCardStyle,raceNameStyle,raceNameLocked,raceTagStyle,raceTitleStyle,raceSetupTitleStyle,raceOptionStyle,raceSummaryStyle,raceEyebrowStyle,raceHeadingStyle;

    int RequiredWins=>roundsChoice/2+1;
    bool RaceStaged(int race)=>race>=0&&race<MobileContent.RaceResources.Length&&(race!=PepeContent.Race||PepeContent.Available);
    int RaceOfUnity(int unity)=>Array.IndexOf(AllRaceUnity,unity);

    JArray AspectOptions(int race) {
        if(race<0||race>=raceAspects.Length)return null;
        if(raceAspects[race]!=null)return raceAspects[race];
        try {
            string text=RaceCatalog(race);
            raceAspects[race]=text==null?new JArray():(JArray)(JObject.Parse(text)["aspects"]??new JArray());
        } catch(Exception error) {Debug.LogWarning("Aspect catalog: "+error.Message);raceAspects[race]=new JArray();}
        return raceAspects[race];
    }
    int AspectCount(int race)=>AspectOptions(race)?.Count??0;
    string AspectField(int race,int index,string field)=>index>=0&&index<AspectCount(race)?(string)AspectOptions(race)[index][field]??"":"";
    string AspectId(int race,int index)=>AspectField(race,index,"id");
    Texture2D AspectIcon(int race,int index) {
        if(race==BlackHand.Race&&index>=0&&index<2)return BlackHandUi.Aspect(index);
        string id=AspectId(race,index);if(string.IsNullOrEmpty(id))return null;
        var hd=HdModels.Icon(ReforgedModelPack.IconAddress("Aspect-"+id));
        if(hd)return hd;
        if(!aspectIconCache.TryGetValue(id,out var texture)){texture=ContentResources.Load<Texture2D>("Aspect-"+id);aspectIconCache[id]=texture;}
        return texture;
    }
    Texture2D RaceEmblemAt(int godot) {
        if(godot<0||godot>=AllRaceIds.Length)return null;
        if(AllRaceUnity[godot]==BlackHand.Race)return BlackHandUi.Emblem();
        string id=AllRaceIds[godot];
        if(!raceEmblemCache.TryGetValue(id,out var texture)){texture=ContentResources.Load<Texture2D>("RaceIcon-"+id);raceEmblemCache[id]=texture;}
        return texture;
    }
    Texture2D RaceEmblem(int race) {
        int godot=race>=0&&race<MobileContent.RaceResources.Length?RaceOfUnity(race):-1;
        return RaceEmblemAt(godot);
    }
    string RaceTitle(int race){int godot=race>=0&&race<MobileContent.RaceResources.Length?RaceOfUnity(race):-1;return godot<0?"Раса":AllRaceNames[godot];}

    void LoadMatchRules() {
        if(testing)return;
        raceChoice=Mathf.Clamp(PlayerPrefs.GetInt("cf-race",raceChoice),0,MobileContent.RaceResources.Length-1);
        botRaceChoice=Mathf.Clamp(PlayerPrefs.GetInt("cf-bot-race",botRaceChoice),-1,MobileContent.RaceResources.Length-1);
        matchMode=Mathf.Clamp(PlayerPrefs.GetInt("cf-match-mode",0),0,2);
        aspectsOn=PlayerPrefs.GetInt("cf-aspects",0)==1;
        roundsChoice=PlayerPrefs.GetInt("cf-rounds",1);
        if(roundsChoice!=1&&roundsChoice!=3&&roundsChoice!=5&&roundsChoice!=7)roundsChoice=1;
        startGold=PlayerPrefs.GetInt("cf-gold",250);if(startGold!=250&&startGold!=500&&startGold!=1000)startGold=250;
        difficulty=Mathf.Clamp(PlayerPrefs.GetInt("cf-difficulty",1),0,2);
    }
    void SaveMatchRules() {
        if(testing)return;
        PlayerPrefs.SetInt("cf-race",raceChoice);PlayerPrefs.SetInt("cf-bot-race",botRaceChoice);
        PlayerPrefs.SetInt("cf-match-mode",matchMode);PlayerPrefs.SetInt("cf-aspects",aspectsOn?1:0);
        PlayerPrefs.SetInt("cf-rounds",roundsChoice);PlayerPrefs.SetInt("cf-gold",startGold);
        PlayerPrefs.SetInt("cf-difficulty",difficulty);PlayerPrefs.Save();
    }
    int RandomStagedRace(int avoid) {
        var pool=Enumerable.Range(0,MobileContent.RaceResources.Length).Where(r=>RaceStaged(r)&&r!=12&&r!=avoid).ToArray();
        if(pool.Length==0)pool=Enumerable.Range(0,MobileContent.RaceResources.Length).Where(r=>RaceStaged(r)&&r!=12).ToArray();
        return pool.Length==0?0:pool[UnityEngine.Random.Range(0,pool.Length)];
    }

    void EnsureRaceStyles() {
        if(raceCardStyle!=null)return;
        var blank=new GUIStyle(GUI.skin.button){font=label.font,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(0,0,0,0),border=new RectOffset(),clipping=TextClipping.Clip};
        blank.normal.background=null;blank.hover.background=null;blank.active.background=null;blank.focused.background=null;
        raceCardStyle=blank;
        raceNameStyle=new GUIStyle(label){fontSize=15,wordWrap=true,clipping=TextClipping.Clip,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.93f,.91f,.84f)}};
        raceNameLocked=new GUIStyle(raceNameStyle){normal={textColor=new Color(.55f,.58f,.56f)}};
        raceTagStyle=new GUIStyle(small){fontSize=11,wordWrap=false,clipping=TextClipping.Clip,normal={textColor=new Color(.55f,.66f,.66f)}};
        raceTitleStyle=new GUIStyle(label){fontSize=22,fontStyle=FontStyle.Bold,wordWrap=true,clipping=TextClipping.Clip,normal={textColor=new Color(.85f,.73f,.47f)}};
        raceSetupTitleStyle=new GUIStyle(raceTitleStyle){fontSize=18,padding=new RectOffset()};
        raceOptionStyle=new GUIStyle(small){fontSize=15,wordWrap=false,clipping=TextClipping.Clip};
        raceEyebrowStyle=new GUIStyle(small){fontSize=12,wordWrap=false,clipping=TextClipping.Clip,normal={textColor=new Color(.62f,.70f,.70f)}};
        raceHeadingStyle=new GUIStyle(heading){normal={textColor=new Color(.85f,.73f,.47f)}};
        raceSummaryStyle=new GUIStyle(small){fontSize=13,wordWrap=true};
    }

    void DrawRaceSelect() {
        EnsureRaceStyles();
        EnsureCampaignStyles();
        DrawMenuBackground();
        AtlasSurface(new Rect(20,20,1240,670));
        GUI.Label(new Rect(38,33,620,51),"Выбор расы",atlasTitle);
        if(Button("setup.back",new Rect(1075,36,160,43),"← Назад")){MainMenu();return;}
        if(matchMode!=0)choosingBotRace=false;
        if(Button("setup.army",new Rect(38,94,173,43),"Ваша армия",!choosingBotRace))choosingBotRace=false;
        bool enabled=GUI.enabled;GUI.enabled=enabled&&matchMode==0;
        if(Button("setup.enemy",new Rect(220,94,173,43),"Противник",choosingBotRace))choosingBotRace=true;
        GUI.enabled=enabled;
        if(choosingBotRace&&Button("setup.enemy.same",new Rect(402,94,203,43),"Та же раса",botRaceChoice<0))botRaceChoice=-1;
        const float gridX=38,gridTop=150,gridWidth=816,gap=8,cardHeight=75;
        float cardWidth=(gridWidth-gap*2)/3f;
        var pointer=Event.current.mousePosition;
        for(int i=0;i<AllRaceIds.Length;i++) {
            int column=i%3,row=i/3;var rect=new Rect(gridX+column*(cardWidth+gap),gridTop+row*(cardHeight+gap),cardWidth,cardHeight);
            int unity=AllRaceUnity[i];bool available=RaceStaged(unity);
            bool selected=available&&unity==(choosingBotRace?botRaceChoice:raceChoice);
            bool hovered=rect.Contains(pointer)&&GUI.enabled;
            AtlasTile(rect,selected,hovered&&available);
            var emblem=RaceEmblemAt(i);
            var artRect=new Rect(rect.x+12,rect.y+12,48,48);
            if(emblem) {GUI.color=available?Color.white:new Color(1,1,1,.38f);GUI.DrawTexture(artRect,emblem,ScaleMode.ScaleToFit);GUI.color=Color.white;}
            else {Fill(artRect,new Color(.10f,.13f,.14f,.9f));Border(artRect,new Color(.24f,.27f,.25f));}
            GUI.Label(new Rect(rect.x+72,rect.y+8,rect.width-84,40),AllRaceNames[i],available?raceNameStyle:raceNameLocked);
            GUI.Label(new Rect(rect.x+72,rect.y+50,rect.width-84,18),available?AllRaceTags[i]:"Перенос впереди",raceTagStyle);
            string id=choosingBotRace?"setup.enemy."+unity:available?"setup."+SetupRaceKeys[unity]:"setup.race-"+AllRaceIds[i];
            if(Button(id,rect,"",false,raceCardStyle)) {
                if(available&&choosingBotRace){botRaceChoice=unity;raceLoreScroll=Vector2.zero;}
                else if(available){raceChoice=unity;raceLoreScroll=raceRulesScroll=Vector2.zero;if(matchMode==2)botRaceChoice=-1;}
                else Notice("Эта раса ещё не перенесена в Unity");
            }
        }
        DrawRacePanel();
    }
    static readonly string[] SetupRaceKeys={"human","orc","naga","nature","chaos","northern","mech","undead",
        "corrupted","elemental","night","elven","ultimate","nerubian","pandaren","centaur","pepe","blackhand"};

    void DrawRacePanel() {
        var panelRect=new Rect(876,106,368,562);
        AtlasSurface(panelRect);
        int previewRace=SetupPreviewRace;
        var frame=new Rect(892,122,180,120);
        Fill(frame,AtlasInk);
        var portrait=RaceArtworkFor(previewRace);
        if(!portrait)portrait=RaceEmblem(previewRace);
        if(portrait)GUI.DrawTexture(frame,portrait,ScaleMode.ScaleToFit);
        Border(frame,AtlasEdge);
        Text(1084,118,144,65,RaceTitle(previewRace),raceSetupTitleStyle);
        int godot=RaceOfUnity(previewRace);
        DrawRaceDescription(new Rect(1084,184,144,76),godot>=0?AllRaceLore[godot]:"",small,ref raceLoreScroll);
        Accent(892,268,336);
        GUI.Label(new Rect(892,276,300,20),"ПАРАМЕТРЫ БИТВЫ",raceEyebrowStyle);
        string[] modes={"Выбор расы","Случайные расы","Зеркальная битва"};
        if(OptionRow("setup.mode",300,"Режим",modes[Mathf.Clamp(matchMode,0,2)]))matchMode=(matchMode+1)%3;
        bool aspectRace=AspectCount(raceChoice)==2;
        if(OptionRow("setup.rules",344,"Правила",aspectsOn?"Аспекты":"Классика",aspectRace))aspectsOn=!aspectsOn;
        if(OptionRow("setup.rounds",388,"Раунды",roundsChoice.ToString()))roundsChoice=roundsChoice==1?3:roundsChoice==3?5:roundsChoice==5?7:1;
        bool opponent=matchMode==0;
        if(OptionRow("setup.bot-race",432,"Противник",OpponentLabel(),opponent))botRaceChoice=NextOpponent();
        if(OptionRow("setup.gold",476,"Золото",startGold.ToString()))startGold=startGold==250?500:startGold==1000?250:1000;
        if(OptionRow("setup.difficulty",520,"Сложность",new[]{"Спокойный","Обычный","Активный"}[difficulty]))difficulty=(difficulty+1)%3;
        string summary="До "+RequiredWins+" "+(roundsChoice<3?"победы":"побед")+" · армия до 160\n1 спасательный удар на сторону за раунд.";
        if(matchMode==1)summary+="\nСлучайная раса для каждой стороны в каждом раунде.";
        if(aspectsOn&&aspectRace)summary+="\nАспекты: "+AspectField(raceChoice,0,"title")+" / "+AspectField(raceChoice,1,"title");
        else if(aspectRace==false)summary+="\nАспекты этой расы ещё не перенесены.";
        DrawRaceDescription(new Rect(892,558,336,52),summary,raceSummaryStyle,ref raceRulesScroll);
        if(Button("setup.start",new Rect(892,616,336,46),"Начать битву  →",true))BeginOfflineVersus();
    }
    int SetupPreviewRace=>choosingBotRace&&botRaceChoice>=0?botRaceChoice:raceChoice;
    void DrawRaceDescription(Rect viewport,string text,GUIStyle style,ref Vector2 scroll) {
        var wrapped=new GUIStyle(style){wordWrap=true,clipping=TextClipping.Clip,padding=new RectOffset(),
            alignment=TextAnchor.UpperLeft};
        float width=viewport.width-20;
        float height=Mathf.Max(viewport.height,wrapped.CalcHeight(new GUIContent(text),width)+5);
        ScrollTextByTouch(viewport,ref scroll,height-viewport.height);
        scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,width,height),false,height>viewport.height);
        GUI.Label(new Rect(0,0,width,height),text,wrapped);GUI.EndScrollView();
    }
    bool OptionRow(string id,float y,string caption,string value,bool enabled=true) {
        GUI.Label(new Rect(892,y+7,104,24),caption,raceOptionStyle);
        bool was=GUI.enabled;GUI.enabled=was&&enabled;
        bool clicked=AtlasControl(id,new Rect(1000,y,228,34),value,dropdown:true);
        GUI.enabled=was;return clicked;
    }
    string OpponentLabel() {
        if(matchMode==2)return RaceTitle(raceChoice);
        if(botRaceChoice<0)return "Та же раса";
        return RaceTitle(botRaceChoice);
    }
    int NextOpponent() {
        for(int next=botRaceChoice+1;next<MobileContent.RaceResources.Length;next++)if(RaceStaged(next))return next;
        return -1;
    }

    static GameObject FirstModel(GameObject[] models)=>models!=null&&models.Length>0?models[0]:null;
}
}
