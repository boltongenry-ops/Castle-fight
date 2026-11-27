using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    enum GuideTab { Buildings, Units, Specials, Aspects }

    int guideRace=-1,guideEntry=-1,guideActorIndex,guideArtworkRace=-1;
    HumanMatch guideAuthority;
    Vector2 guideGridScroll,guideTextScroll;
    readonly Vector2[] guideAspectScroll=new Vector2[2];
    string guideQuery="",guideDescription="";
    bool guideUnit,guideLoading;
    GuideTab guideTab;
    Texture2D guideArtwork;
    GUIStyle guideCardName,guideCardInfo,guideLore;
    readonly Stack<int> guideHistory=new Stack<int>();
    readonly Dictionary<int,int[]> guideActors=new Dictionary<int,int[]>();
    static readonly Rect GuideListArea=new Rect(62,233,497,420);
    static readonly Rect GuideTextArea=new Rect(612,351,572,236);
    const int GuideRowStep=80;

    string GuideRaceName(int race)=>RaceTitle(race);

    void OpenGuide(){
        MainMenu();page=Page.Guide;
        guideRace=guideEntry=-1;
        guideQuery="";guideHistory.Clear();
    }

    void CloseGuide(){
        if(guideArtwork)Destroy(guideArtwork);
        guideArtwork=null;guideArtworkRace=-1;
        guideAuthority=null;guideRace=guideEntry=-1;guideLoading=false;guideActors.Clear();
    }

    void UpdateGuide(){
        if(page!=Page.Guide&&(guideAuthority!=null||guideArtwork||guideLoading))CloseGuide();
    }

    IEnumerator OpenBlackHandGuide(){
        yield return BlackHandArt.Open();
        guideLoading=false;
        if(page==Page.Guide&&BlackHandArt.Ready)GuideChooseRace(BlackHand.Race);
        else if(page==Page.Guide)Notice("Не удалось открыть материалы Клана Чёрной Руки");
    }

    void GuideChooseRace(int race){
        if(!RaceStaged(race)||guideLoading)return;
        if(race==BlackHand.Race&&!BlackHandArt.Ready){
            guideLoading=true;StartCoroutine(OpenBlackHandGuide());return;
        }
        if(MobileContent.Active&&race!=0&&race!=BlackHand.Race&&!Pack(race)){
            guideLoading=true;StartCoroutine(PrepareMobileGuide(race));return;
        }
        if(!MobileContent.Active){
            if(DiscardRaceAssets(race)){ClearRacePrefabCaches();SweepRaceAssets();}
            LoadDesktopRace(race);
        }
        guideRace=race;guideEntry=-1;guideTab=GuideTab.Buildings;
        guideGridScroll=guideTextScroll=Vector2.zero;guideQuery="";guideHistory.Clear();
        guideActors.Clear();
        guideAuthority=new HumanMatch(JObject.Parse(RaceCatalog(race)));
    }

    IEnumerable<int> GuideEntries(){
        for(int i=0;i<guideAuthority.Lines.Length;i++){
            var line=guideAuthority.Lines[i];
            if(line.category=="summon"||line.category=="helper"||line.cost<=0)continue;
            if(guideTab==GuideTab.Units&&GuideUnitKinds(i).Length==0)continue;
            if(guideTab==GuideTab.Specials&&line.Producer&&line.slots==0)continue;
            if((line.name+" "+line.unitName).IndexOf(guideQuery,StringComparison.OrdinalIgnoreCase)<0)continue;
            yield return i;
        }
    }

    int[] GuideUnitKinds(int kind){
        if(kind<0||kind>=guideAuthority.Lines.Length)return Array.Empty<int>();
        if(guideActors.TryGetValue(kind,out var actors))return actors;
        var line=guideAuthority.Lines[kind];
        actors=line.unit=="e00E"?Enumerable.Range(0,guideAuthority.Lines.Length)
            .Where(k=>guideAuthority.Lines[k].unit=="n02K"||guideAuthority.Lines[k].unit=="n02L").ToArray():
            !line.Producer||line.hp<=0?Array.Empty<int>():new[]{kind};
        guideActors[kind]=actors;
        return actors;
    }

    int GuideActorKind=>guideUnit?GuideUnitKinds(guideEntry)[guideActorIndex]:guideEntry;
    Texture2D GuideIcon(int kind)=>HdIcon(CatalogBuildingIcon(guideRace,kind,guideAuthority.Lines[kind].building));
    Texture2D GuideUnitIcon(int kind){
        var actor=guideAuthority.Lines[kind].unit;
        var icon=HdModels.Icon(ReforgedModelPack.IconAddress("Unit-"+actor));
        if(icon)return icon;
        icon=ContentResources.Load<Texture2D>("NerubianUnitIcon/"+actor);
        if(icon)return icon;
        if(guideRace==0&&kind<unitIcons.Length)icon=unitIcons[kind];
        if(guideRace==PepeContent.Race)icon=PepeContent.FighterIcon(actor);
        if(guideRace==15)icon=CentaurUnitIcon(actor);
        if(guideRace==BlackHand.Race)icon=BlackHandUi.Fighter(actor);
        return icon?icon:GuideIcon(kind);
    }

    void GuideSelectEntry(int kind,bool remember=false){
        if(kind<0||kind>=guideAuthority.Lines.Length)return;
        if(remember&&guideEntry>=0)guideHistory.Push(guideEntry);
        else if(!remember)guideHistory.Clear();
        guideEntry=kind;guideTextScroll=Vector2.zero;guideActorIndex=0;
        guideUnit=guideTab!=GuideTab.Specials&&GuideUnitKinds(kind).Length>0;
        guideDescription=GuideEntryText();
    }

    void GuideBack(){
        CloseAccountKeyboard();
        if(guideEntry>=0&&guideHistory.Count>0){
            int previous=guideHistory.Pop();guideEntry=previous;guideActorIndex=0;
            guideUnit=guideTab!=GuideTab.Specials&&GuideUnitKinds(previous).Length>0;
            guideTextScroll=Vector2.zero;guideDescription=GuideEntryText();return;
        }
        if(guideEntry>=0){guideEntry=-1;return;}
        if(guideRace>=0){CloseGuide();ReleaseIdleRaceAssets(true);return;}
        MainMenu();
    }

    int GuideInvestment(int kind,HashSet<int> seen=null){seen=seen??new HashSet<int>();if(!seen.Add(kind))return 0;var line=guideAuthority.Lines[kind];int parent=Array.FindIndex(guideAuthority.Lines,l=>l.upgrades.Contains(line.building));return line.cost+(parent>=0?GuideInvestment(parent,seen):0);}
    JToken GuideSkillConfig(int kind,string id,JToken cfg){
        var ranks=cfg?["ranks"] as JArray;if(ranks==null||ranks.Count==0)return cfg;
        int level=(int?)guideAuthority.Lines[kind].stats["ability_ranks"]?[id]??1;
        return ranks[Mathf.Clamp(level-1,0,ranks.Count-1)];
    }
    string GuideEntryText(){
        var line=guideAuthority.Lines[guideEntry];var actor=guideAuthority.Lines[GuideActorKind];var stats=guideUnit?actor.stats:line.buildingStats;var text=new StringBuilder();
        text.Append("Характеристики\n");
        text.Append("Здоровье: ").Append(HumanMatch.F(stats,"hp").ToString("F0"));float mana=HumanMatch.F(stats,"max_mana");if(mana>0)text.Append("  ·  Мана: ").Append(mana.ToString("F0"));
        text.Append("\nБроня: ").Append(HumanMatch.F(stats,"armor").ToString("F0")).Append(" · ").Append(ArmorLabel((string)stats["armor_type"]??"fortified"));
        float speed=HumanMatch.F(stats,"speed");if(speed>0)text.Append("\nСкорость: ").Append((speed*30).ToString("F0"));
        var weapons=guideUnit?actor.weapons:line.buildingWeapons;foreach(var weapon in weapons)text.Append("\n\n").Append(AttackLabel(weapon.attack)).Append(" атака: ").Append(weapon.damage.ToString("F1")).Append("\nИнтервал: ").Append(weapon.cooldown.ToString("F2")).Append(" с · дальность: ").Append((weapon.range*30).ToString("F0"));
        text.Append("\n\n").Append(line.basic?"Постройка: ":"Улучшение: ").Append(line.cost).Append(" золота");if(line.woodCost>0)text.Append(" · ").Append(line.woodCost).Append(" дерева");
        if(line.slots>0)text.Append("\nЗанимает легендарное место");if(line.woodReward>0)text.Append("\nПри покупке: +").Append(line.woodReward).Append(" дерева");
        if(GuideUnitKinds(guideEntry).Length>0){var hut=new HumanMatch.Hut{kind=guideEntry,team=0,spent=GuideInvestment(guideEntry)};text.Append("\nВыпускает: ").Append(string.Join(" / ",GuideUnitKinds(guideEntry).Select(k=>guideAuthority.Lines[k].unitName))).Append("\nЦикл при полном вложении: ").Append(guideAuthority.Interval(hut).ToString("F1")).Append(" с");}
        var abilities=guideUnit?HumanMatch.Strings(stats["abilities"]):line.abilities;
        foreach(string id in abilities.Distinct()){var cfg=guideAuthority.Abilities[id];if(cfg==null||(bool?)cfg["hide_card"]==true||!ContextAbilityVisible(guideUnit?actor.unit:line.building,id,cfg)||(string)cfg["kind"]=="marker"&&(bool?)cfg["card_description"]!=true)continue;var shown=guideUnit?GuideSkillConfig(GuideActorKind,id,cfg):cfg;string desc=ContextSkillTip(guideUnit?actor.unit:line.building,id,shown).Trim();if(desc.Length>0)text.Append("\n\n").Append(desc);}
        return text.ToString();
    }
    void DrawGuide(){
        PrepareCampaignArtwork();EnsureCampaignStyles();EnsureRaceStyles();DrawMenuBackground();DrawAtlasHeader();
        if(page!=Page.Guide)return;
        if(guideCardName==null){
            guideCardName=new GUIStyle(label){fontSize=17,alignment=TextAnchor.MiddleLeft,wordWrap=true,padding=new RectOffset()};
            guideCardInfo=new GUIStyle(small){fontSize=14,alignment=TextAnchor.MiddleLeft,padding=new RectOffset()};
            guideLore=new GUIStyle(label){fontSize=18,wordWrap=true,padding=new RectOffset(),alignment=TextAnchor.UpperLeft};
        }
        AtlasSurface(new Rect(35,73,1210,623),true);
        Text(63,87,860,56,guideRace<0?"Учебник рас":GuideRaceName(guideRace),atlasTitle);
        if(Button("guide.back",new Rect(1019,93,194,43),guideRace<0?"← В меню":guideEntry<0?"← К расам":"← Назад")){GuideBack();return;}
        Accent(65,148,1145);
        if(guideRace<0)DrawGuideRaces();else DrawGuideBook();
    }

    void DrawGuideRaces(){
        int featured=RaceStaged(raceChoice)?raceChoice:0;
        for(int i=0;i<AllRaceUnity.Length;i++){
            int race=AllRaceUnity[i];
            var card=new Rect(63+i%6*125,173+i/6*163,114,150);
            bool hovered=GUI.enabled&&card.Contains(Event.current.mousePosition);
            DrawGuideRaceCard(card,i,hovered);
            bool enabled=GUI.enabled;
            GUI.enabled=enabled&&!guideLoading&&RaceStaged(race);
            if(Button("guide.race."+race,card,"",false,GUIStyle.none)){GuideChooseRace(race);GUI.enabled=enabled;return;}
            GUI.enabled=enabled;
        }
        DrawGuideRaceFeature(new Rect(826,173,388,477),featured,true);
    }

    void DrawGuideRaceCard(Rect card,int index,bool hovered){
        AtlasTile(card,false,hovered);
        var emblem=RaceEmblemAt(index);
        if(emblem)GUI.DrawTexture(new Rect(card.center.x-38,card.y+10,76,76),emblem,ScaleMode.ScaleToFit);
        var name=new GUIStyle(guideCardName){fontSize=15,alignment=TextAnchor.MiddleCenter};
        var measure=new GUIStyle(name){wordWrap=false};
        string title=AllRaceNames[index];
        while(name.fontSize>12&&title.Split(' ').Any(word=>measure.CalcSize(new GUIContent(word)).x>card.width-14))
            measure.fontSize=--name.fontSize;
        Text(card.x+7,card.y+89,card.width-14,53,title,name);
    }

    Texture2D GuideArtworkFor(int race){
        if(guideArtworkRace==race)return guideArtwork;
        if(guideArtwork)Destroy(guideArtwork);
        guideArtwork=null;guideArtworkRace=race;
        string resource="GuidePortraits/"+VersusKeys[race];
        if(!MobileContent.Active)FrontendTexture(ref guideArtwork,resource+".jpg");
        if(!guideArtwork){
            var encoded=Resources.Load<TextAsset>(resource);
            if(encoded){
                var picture=new Texture2D(2,2,TextureFormat.RGB24,false){name=resource,wrapMode=TextureWrapMode.Clamp};
                if(picture.LoadImage(encoded.bytes,true))guideArtwork=picture;else Destroy(picture);
                Resources.UnloadAsset(encoded);
            }
        }
        return guideArtwork;
    }

    void DrawGuideRaceFeature(Rect rect,int race,bool openButton){
        AtlasSurface(rect);
        const float pictureWidth=366;
        var art=new Rect(rect.x+11,rect.y+11,pictureWidth,pictureWidth*2/3);
        var painting=GuideArtworkFor(race);
        Fill(art,AtlasInk);
        if(painting)GUI.DrawTexture(art,painting,ScaleMode.ScaleToFit);
        else AtlasScene(3,art);
        Border(art,AtlasEdge);
        var emblem=RaceEmblem(race);
        var details=openButton?new Rect(rect.x+23,art.yMax+8,rect.width-46,rect.yMax-art.yMax-73):
            new Rect(art.xMax+19,rect.y+22,rect.xMax-art.xMax-33,rect.height-45);
        float nameX=openButton?details.x+78:details.x;
        float nameY=openButton?details.y:details.y+77;
        if(emblem)GUI.DrawTexture(new Rect(details.x,details.y,69,69),emblem,ScaleMode.ScaleToFit);
        Text(nameX,nameY,details.xMax-nameX,71,GuideRaceName(race),raceTitleStyle);
        float summary=nameY+73;
        DrawRaceDescription(new Rect(details.x,summary,details.width,details.yMax-summary),
            AllRaceLore[RaceOfUnity(race)],guideLore,ref guideTextScroll);
        if(openButton&&Button("guide.feature",new Rect(rect.x+23,rect.yMax-55,rect.width-46,42),"Открыть записи",true))
            GuideChooseRace(race);
    }

    void DrawGuideBook(){
        string[] tabs={"Постройки","Существа","Особые","Аспекты"};
        float[] cuts={62,207,329,451,573};
        for(int i=0;i<tabs.Length;i++){
            if(!Button("guide.tab."+i,new Rect(cuts[i],169,cuts[i+1]-cuts[i]-9,44),tabs[i],(int)guideTab==i)||(int)guideTab==i)continue;
            CloseAccountKeyboard();guideTab=(GuideTab)i;guideEntry=-1;
            guideHistory.Clear();guideGridScroll=Vector2.zero;guideQuery="";Array.Clear(guideAspectScroll,0,guideAspectScroll.Length);
        }
        if(guideTab==GuideTab.Aspects){DrawGuideAspects();return;}
        string before=guideQuery;
        guideQuery=AccountText("guide.search",new Rect(583,169,629,44),guideQuery,48,false,"Поиск по названиям");
        if(!Application.isMobilePlatform&&guideQuery==""&&GUI.GetNameOfFocusedControl()!="guide.search")
            Text(600,170,595,41,"Поиск по названиям",small);
        if(before!=guideQuery)guideGridScroll=Vector2.zero;
        DrawGuideList();
        if(guideEntry<0)DrawGuideRaceFeature(new Rect(584,233,628,420),guideRace,false);
        else DrawGuideEntry();
    }

    void DrawGuideList(){
        var entries=GuideEntries().ToArray();
        var area=GuideListArea;
        AtlasSurface(area);
        if(entries.Length==0){Text(77,248,450,41,"Ничего не найдено",label);return;}
        var viewport=new Rect(area.x+9,area.y+10,area.width-18,area.height-20);
        guideGridScroll=GUI.BeginScrollView(viewport,guideGridScroll,new Rect(0,0,viewport.width-20,Mathf.Ceil(entries.Length/2f)*GuideRowStep));
        bool enabled=GUI.enabled;
        GUI.enabled=enabled&&!(Application.isMobilePlatform&&socialTouchMoved);
        for(int i=0;i<entries.Length;i++){
            int kind=entries[i];
            var line=guideAuthority.Lines[kind];
            bool units=guideTab==GuideTab.Units;
            int actor=units?GuideUnitKinds(kind)[0]:kind;
            var card=new Rect(i%2*230,i/2*GuideRowStep,222,73);
            AtlasTile(card,guideEntry==kind,GUI.enabled&&card.Contains(Event.current.mousePosition));
            var icon=units?GuideUnitIcon(actor):GuideIcon(kind);
            if(icon)GUI.DrawTexture(new Rect(card.x+8,card.y+14,44,44),icon,ScaleMode.ScaleToFit);
            var nameBox=new Rect(card.x+59,card.y+4,155,45);
            string name=units?guideAuthority.Lines[actor].unitName:line.name;
            var cardName=new GUIStyle(guideCardName);
            while(cardName.fontSize>12&&cardName.CalcHeight(new GUIContent(name),nameBox.width)>nameBox.height)cardName.fontSize--;
            Text(nameBox.x,nameBox.y,nameBox.width,nameBox.height,name,cardName);
            string detail=units?GuideRaceName(guideRace):line.cost+" зол."+(line.woodCost>0?" · "+line.woodCost+" дер.":"");
            Text(card.x+59,card.y+48,155,22,FitAtlasLabel(detail,150,guideCardInfo),guideCardInfo);
            if(Button("guide.entry."+line.building,card,"",false,GUIStyle.none)){CloseAccountKeyboard();GuideSelectEntry(kind);}
        }
        GUI.enabled=enabled;
        GUI.EndScrollView();
    }

    void DrawGuideEntry(){
        var line=guideAuthority.Lines[guideEntry];
        AtlasSurface(new Rect(583,233,629,420));
        var card=new Rect(606,249,82,82);
        AtlasTile(card,false,false);
        var icon=guideUnit?GuideUnitIcon(GuideActorKind):GuideIcon(guideEntry);
        if(icon)GUI.DrawTexture(new Rect(card.x+6,card.y+6,70,70),icon,ScaleMode.ScaleToFit);
        Text(705,245,475,58,guideUnit?guideAuthority.Lines[GuideActorKind].unitName:line.name,raceTitleStyle);
        var actors=GuideUnitKinds(guideEntry);
        bool enabled=GUI.enabled;
        GUI.enabled=enabled&&actors.Length>0;
        if(Button("guide.unit",new Rect(706,301,206,39),
            actors.Length>1?"Боец "+(guideActorIndex+1)+" / "+actors.Length:"Боец",guideUnit)){
            guideActorIndex=guideUnit?(guideActorIndex+1)%actors.Length:0;guideUnit=true;
            guideTextScroll=Vector2.zero;guideDescription=GuideEntryText();
        }
        GUI.enabled=enabled;
        if(Button("guide.building",new Rect(925,301,259,39),"Постройка",!guideUnit)){
            guideUnit=false;guideTextScroll=Vector2.zero;guideDescription=GuideEntryText();
        }
        var area=GuideTextArea;
        DrawRaceDescription(area,guideDescription,guideLore,ref guideTextScroll);
        var upgrades=line.upgrades.Select(guideAuthority.Kind).Where(kind=>kind>=0).ToArray();
        if(upgrades.Length==0)return;
        float buttonWidth=(area.width-(upgrades.Length-1)*8)/upgrades.Length;
        for(int i=0;i<upgrades.Length;i++){
            int kind=upgrades[i];
            var upgrade=guideAuthority.Lines[kind];
            string title=FitAtlasLabel(upgrade.name,buttonWidth-29,atlasCompact);
            if(LobbyButton("guide.upgrade."+upgrade.building,new Rect(area.x+i*(buttonWidth+8),600,buttonWidth,42),"↑ "+title,true))
                GuideSelectEntry(kind,true);
        }
    }

    void DrawGuideAspects(){
        int count=AspectCount(guideRace);
        if(count==0){Text(65,233,900,48,"У этой расы пока нет аспектов.",label);return;}
        for(int i=0;i<count;i++){
            var card=new Rect(61+i*579,233,561,399);
            AtlasSurface(card,true);
            var aspect=AspectIcon(guideRace,i);
            if(aspect)GUI.DrawTexture(new Rect(card.x+22,card.y+30,74,74),aspect,ScaleMode.ScaleToFit);
            Text(card.x+113,card.y+26,card.width-136,83,AspectField(guideRace,i,"title"),raceTitleStyle);
            var data=AspectOptions(guideRace)[i];
            string description=(string)(data["description"]??data["text"])??"";
            DrawRaceDescription(new Rect(card.x+26,card.y+125,card.width-52,247),description,guideLore,ref guideAspectScroll[i]);
        }
    }

    void DisposeGuide(){CloseGuide();}
}
}
