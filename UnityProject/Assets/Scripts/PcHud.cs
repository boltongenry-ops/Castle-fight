using System;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    GUIStyle hudText,hudSmall,hudTitle,iconStyle;string hudTip;bool hudTipTable,scoreExpanded=true;Vector2? testHudPointer;
    Vector2 HudPointer=>testing&&testHudPointer.HasValue?testHudPointer.Value:Event.current.mousePosition;
    bool minimapTerrain=true,minimapTeamColors=true;
    Camera portraitCamera;RenderTexture portraitTexture;PcModel portraitModel;GameObject portraitStage;
    void InitHud() {
        if(hudText!=null)return;
        hudText=new GUIStyle(label){fontSize=15,padding=new RectOffset(0,0,0,0),alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip,normal={textColor=new Color(.94f,.88f,.69f)}};
        hudSmall=new GUIStyle(hudText){fontSize=12,wordWrap=false,normal={textColor=new Color(.8f,.79f,.72f)}};
        hudTitle=new GUIStyle(hudText){fontSize=20,fontStyle=FontStyle.Bold};
        iconStyle=new GUIStyle {normal={textColor=Color.white},border=new RectOffset(),padding=new RectOffset()};
        if(buttonUp) {
            button.normal.background=buttonUp;button.hover.background=buttonHighlight;button.active.background=buttonDown;button.focused.background=buttonHighlight;button.border=new RectOffset(8,8,8,8);
            button.normal.textColor=new Color(.9f,.85f,.66f);button.hover.textColor=new Color(1,.95f,.76f);
            primary=new GUIStyle(button);primary.normal.textColor=new Color(1,.83f,.35f);primary.hover.textColor=new Color(1,.95f,.65f);
        }
    }
    void Fill(Rect r,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    void Border(Rect r,Color c,float width=1) {
        Fill(new Rect(r.x,r.y,r.width,width),c);Fill(new Rect(r.x,r.yMax-width,r.width,width),c);
        Fill(new Rect(r.x,r.y,width,r.height),c);Fill(new Rect(r.xMax-width,r.y,width,r.height),c);
    }
    void Source(Texture2D texture,Rect source,Rect target) {
        GUI.DrawTextureWithTexCoords(target,texture,new Rect(source.x/texture.width,1-source.yMax/texture.height,source.width/texture.width,source.height/texture.height));
    }
    void StoneFrame(Rect r) {
        if(BattleTheme is HudTheme theme){theme.DrawFrame(r);return;}
        GUI.DrawTexture(r,panel);
        if(hudTiles==null||hudTiles.Length==0||!hudTiles[0])return;
        var texture=hudTiles[0];float b=10;
        Source(texture,new Rect(0,204,18,18),new Rect(r.x,r.y,b,b));Source(texture,new Rect(286,204,18,18),new Rect(r.xMax-b,r.y,b,b));
        Source(texture,new Rect(0,494,18,18),new Rect(r.x,r.yMax-b,b,b));Source(texture,new Rect(286,494,18,18),new Rect(r.xMax-b,r.yMax-b,b,b));
        Source(texture,new Rect(18,204,268,12),new Rect(r.x+b,r.y,r.width-2*b,b));Source(texture,new Rect(18,500,268,12),new Rect(r.x+b,r.yMax-b,r.width-2*b,b));
        Source(texture,new Rect(0,222,12,272),new Rect(r.x,r.y+b,b,r.height-2*b));Source(texture,new Rect(292,222,12,272),new Rect(r.xMax-b,r.y+b,b,r.height-2*b));
    }
    bool Icon(string id,Rect r,int image,string key,string tip,bool selected=false) {
        return ArtButton(id,r,image>=0&&image<commandIcons.Length?CommandIcon(image):null,key,tip,selected);
    }
    bool MinimapButton(string id,int index,string tip) {
        var rect=new Rect(251,551+index*26,31,23);
        GUI.DrawTexture(rect,HdIcon(minimapIcons[index]));
        if(rect.Contains(Event.current.mousePosition)&&GUI.enabled){Border(rect,new Color(1,.85f,.3f));hudTip=tip;}
        return Button(id,rect,"",false,iconStyle);
    }
    // Use the same source-to-screen mapping as the console tiles, including tile
    // 04's continuation. Each button stays inside the painted black cell.
    static Rect ConsoleCell(float x,float y,float width,float height)=>new Rect(819.2f+x*.8f,520+(y-176)*200/336f,width*.8f,height*200/336f);
    static Rect CommandSlot(int column,int row,bool mobile)=>mobile?new Rect(970+column*76,492+row*76,70,70):ConsoleCell(209+column*87,245+row*87,80,80);
    Rect Slot(int column,int row)=>HudThemeIndex==0?CommandSlot(column,row,Application.isMobilePlatform):ThemedCommandSlot(column,row);
    Rect InventorySlot(int cell)=>CommandDeskHud?new Rect(823+cell%2*66,574+cell/2*43,56,39):
        ConsoleCell(5+cell%2*84,290+cell/2*73,64,62);
    void DrawWarcraftHud() {
        if(Event.current.type==EventType.Repaint){
            float shade=0;
            foreach(var pulse in match.Pulses){
                if(pulse.fx!="blackhand:judgment" || pulse.age>=1.35f || !ShowPulse036(pulse))continue;
                var point=Camera.main.WorldToViewportPoint(World(pulse.position));
                if(point.z<=0 || point.x<0 || point.x>1 || point.y<0 || point.y>1)continue;
                shade=Mathf.Max(shade,.17f*Mathf.SmoothStep(0,1,pulse.age/.14f)*Mathf.SmoothStep(0,1,(1.35f-pulse.age)/.85f));
            }
            if(shade>0)Fill(new Rect(-guiOrigin.x/guiScale,-guiOrigin.y/guiScale,Screen.width/guiScale,Screen.height/guiScale),new Color(.035f,.012f,.035f,shade));
        }
        InitHud();hudTip=null;hudTipTable=false;
        var theme=BattleTheme;
        if(theme==null){DrawHudConsole(null);return;}
        var savedText=hudText;var savedSmall=hudSmall;
        hudText=theme.Name(hudText);hudSmall=theme.Detail(hudSmall);
        try{DrawHudConsole(theme);}
        finally{hudText=savedText;hudSmall=savedSmall;}
    }
    void DrawHudConsole(HudTheme theme) {
        if(theme!=null)DrawThemedConsole(theme);
        else{
        Fill(new Rect(0,0,1280,28),new Color(.025f,.035f,.018f,.98f));
        Fill(new Rect(0,558,1280,162),Color.black);
        for(int i=0;i<4;i++) {
            float width=i==3?51.2f:409.6f;
            Source(hudTiles[i],new Rect(0,176,hudTiles[i].width,336),new Rect(i*409.6f,520,width,200));
            Source(hudTiles[i],new Rect(0,0,hudTiles[i].width,48),new Rect(i*409.6f,0,width,28));
        }
        Source(hudTiles[1],new Rect(170,0,180,110),new Rect(546,0,144,66));
        GUI.DrawTexture(new Rect(619,5,42,42),timeIndicator);
        }
        var topButton=theme?.Top(button)??new GUIStyle(button){fontSize=Application.isMobilePlatform?15:13,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(3,3,0,0)};float topHeight=Application.isMobilePlatform?40:24;
        if(Button("match.help",new Rect(5,1,132,topHeight),Application.isMobilePlatform?"Справка":"Справка (F9)",false,topButton))OpenBattleHelp();
        float builderTop=Application.isMobilePlatform?46:31;var builderFrame=new Rect(5,builderTop,52,52);StoneFrame(builderFrame);
        if(Icon("hud.builder",new Rect(9,builderTop+4,44,44),0,"F1","Строитель (F1)\nВыделить и перейти к строителю",builderSelected))FocusBuilder();
        if(Button("match.pause",new Rect(142,1,131,topHeight),Application.isMobilePlatform?"Меню":"Меню (F10)",false,topButton))page=Page.Pause;
        if(Button("match.teams",new Rect(278,1,131,topHeight),Application.isMobilePlatform?"Стороны":"Стороны (F11)",false,topButton))page=Page.Teams;
        bool inputEnabled=GUI.enabled;GUI.enabled=inputEnabled&&onlineMatch;
        if(Button("match.chat",new Rect(414,1,131,topHeight),Application.isMobilePlatform?"Чат":"Чат (F12)",false,topButton))ToggleBattleChat();GUI.enabled=inputEnabled;
        GUI.DrawTexture(new Rect(741,2,24,24),HdIcon(goldIcon));
        GUI.Label(new Rect(774,0,78,28),Mathf.FloorToInt(match.AvailableGold(0)).ToString(),new GUIStyle(hudText){alignment=TextAnchor.MiddleRight});
        GUI.DrawTexture(new Rect(878,2,24,24),HdIcon(woodIcon));
        GUI.Label(new Rect(913,0,75,28),match.AvailableWood(0).ToString(),new GUIStyle(hudText){alignment=TextAnchor.MiddleRight});
        GUI.DrawTexture(new Rect(1015,2,24,24),HdIcon(armyIcon));
        GUI.Label(new Rect(1047,0,81,28),match.LegendaryUsed(0)+" / "+match.LegendaryLimit[0],new GUIStyle(hudText){alignment=TextAnchor.MiddleRight});
        GUI.Label(new Rect(1156,0,117,28),"Castle Fight 1×1",new GUIStyle(hudSmall){alignment=TextAnchor.MiddleCenter});
        if(new Rect(738,0,125,28).Contains(Event.current.mousePosition))hudTip=GoldTooltip();
        if(new Rect(875,0,125,28).Contains(Event.current.mousePosition))hudTip="Древесина: "+match.Wood[0]+"\nРезерв очереди: "+match.ReservedWood(0)+"\nДоступно: "+match.AvailableWood(0);
        if(new Rect(1012,0,125,28).Contains(Event.current.mousePosition))hudTip="Легендарные места: "+match.LegendaryUsed(0)+" / "+match.LegendaryLimit[0]+"\nГотовые здания: "+(match.LegendaryUsed(0)-match.ReservedLegendary(0))+"\nРезерв очереди: "+match.ReservedLegendary(0);
        StoneFrame(new Rect(948,31,327,scoreExpanded?112:29));
        GUI.Label(new Rect(966,37,270,18),RaceName+" · Раунд "+series.Round+"/"+series.Rounds+" · "+series.Wins[0]+":"+series.Wins[1],hudSmall);
        if(Button("hud.score-toggle",new Rect(1243,34,27,22),scoreExpanded?"−":"+",false,topButton))scoreExpanded=!scoreExpanded;
        if(scoreExpanded){
        GUI.Label(new Rect(966,60,65,18),"Сторона",hudSmall);
        GUI.Label(new Rect(1035,60,45,18),"Постр.",hudSmall);GUI.Label(new Rect(1080,60,48,18),"Армия",hudSmall);
        GUI.Label(new Rect(1133,60,48,18),"Доход",hudSmall);GUI.Label(new Rect(1190,60,65,18),"Замок",hudSmall);
        DrawTeamRow(0,78,"Запад");DrawTeamRow(1,96,"Восток");
        float incomeIn=onlineMatch?onlineIncomeIn??(10-match.Elapsed%10):match.IncomeIn;
        GUI.Label(new Rect(966,116,290,18),"Время: "+TimeSpan.FromSeconds(match.Elapsed).ToString(@"mm\:ss")+" · Доход через "+Mathf.CeilToInt(incomeIn)+" с",new GUIStyle(hudSmall){fontSize=11});}
        DrawMinimap();
        if(MinimapButton("hud.worker",0,"Показать рабочего (F1)"))FocusBuilder();
        if(MinimapButton("hud.terrain",1,"Ландшафт миникарты"))minimapTerrain=!minimapTerrain;
        if(MinimapButton("hud.battle",2,"Показать бой у реки"))controls.Focus("river");
        if(MinimapButton("hud.colors",3,"Цвета сторон на миникарте"))minimapTeamColors=!minimapTeamColors;
        if(Application.isMobilePlatform&&theme==null){Fill(new Rect(960,482,320,238),new Color(.02f,.025f,.025f,.94f));Border(new Rect(960,482,320,238),new Color(.42f,.36f,.25f));}DrawHumanSelection();DrawItemInventory();DrawSelectionMarquee();
        foreach(var u in match.Units)if(u.hp>0&&!match.Hidden(u)&&CanSeeUnit(u)&&match.Lines[u.kind].unit!="n02E"&&units.TryGetValue(u.id,out var view)) {
            var p=Camera.main.WorldToScreenPoint(view.transform.position+Vector3.up*(view.godotHeight>0?view.godotHeight+.4f:(view.raw.StartsWith("cU",StringComparison.Ordinal)?view.ClassicHeight+.4f:FinalMarkerHeight(view.raw))));if(p.z<=0)continue;
            float x=(p.x-guiOrigin.x)/guiScale,y=(Screen.height-p.y-guiOrigin.y)/guiScale;
            if(y<42||y>516||x<0||x>1280)continue;
            if(Event.current.type==EventType.Repaint && y>=80 && u.effects.Count>0){
                var judgment=u.effects.Find(e=>e.id=="BHJu" && e.left>0);
                if(judgment!=null){
                    var sigil=BlackHandUi.JudgmentSigil();
                    if(sigil){
                        float entry=Mathf.Clamp01((6-judgment.left)/.28f);
                        float size=Mathf.Lerp(35,27,entry);
                        var tint=GUI.color;
                        GUI.color=new Color(tint.r,tint.g,tint.b,tint.a*Mathf.Clamp01(judgment.left/.45f));
                        GUI.DrawTexture(new Rect(x-size*.5f,y-size-7,size,size),sigil,ScaleMode.ScaleToFit,true);
                        GUI.color=tint;
                    }
                }
            }
            Fill(new Rect(x-17,y,34,4),new Color(.02f,.02f,.02f,.95f));Fill(new Rect(x-16,y+1,32*u.hp/u.maxHp,2),u.team==0?new Color(.2f,.85f,.25f):new Color(.95f,.2f,.14f));float debt=Mathf.Min(u.hp,match.PandaStagger(u));if(debt>0)Fill(new Rect(x-16+32*(u.hp-debt)/u.maxHp,y+1,32*debt/u.maxHp,2),new Color(1,.67f,.16f));DrawUnitDebuffs(u,x,y);if(match.CursedHorde)DrawHordeUnitBadge(u,x,y);
        }
        foreach(var h in match.Huts)if(h.paid&&CanInspectHut(h)&&huts.TryGetValue(h.id,out var buildingView)&&(HumanMatch.F(match.Lines[h.kind].buildingStats,"max_mana")>0||h.id==selectedHut)){
            var point=Camera.main.WorldToScreenPoint(buildingView.transform.position+Vector3.up*Mathf.Max(3,buildingView.ClassicHeight+.4f));if(point.z<=0)continue;float x=(point.x-guiOrigin.x)/guiScale,y=(Screen.height-point.y-guiOrigin.y)/guiScale;if(y<42||y>510||x<0||x>1280)continue;
            Fill(new Rect(x-24,y,48,5),Color.black);Fill(new Rect(x-23,y+1,46*Mathf.Clamp01(h.hp/h.maxHp),3),h.team==0?new Color(.2f,.85f,.25f):new Color(.95f,.2f,.14f));DrawManaBar(new Rect(x-24,y+6,48,4),h.mana,HumanMatch.F(match.Lines[h.kind].buildingStats,"max_mana"),false);
        }
        foreach(var p in match.Pulses)if(!string.IsNullOrEmpty(p.text)&&ShowPulse036(p)) {
            var point=Camera.main.WorldToScreenPoint(World(p.position)+Vector3.up*(3+p.age));if(point.z<=0)continue;
            float x=(point.x-guiOrigin.x)/guiScale,y=(Screen.height-point.y-guiOrigin.y)/guiScale;
            if(y<40||y>480)continue;GUI.Label(new Rect(x-90,y,180,24),p.text,new GUIStyle(hudSmall){alignment=TextAnchor.MiddleCenter,normal={textColor=p.color}});
        }
        if(noticeLeft>0){
            float alpha=Mathf.Min(1,noticeLeft);var message=new GUIStyle(hudText){fontSize=15,alignment=TextAnchor.LowerLeft,normal={textColor=new Color(0,0,0,alpha)}};
            GUI.Label(new Rect(showBattleChat?506:336,467,showBattleChat?440:610,32),notice,message);message.normal.textColor=new Color(1,.83f,.32f,alpha);GUI.Label(new Rect(showBattleChat?505:335,466,showBattleChat?440:610,32),notice,message);
        }
        if(!string.IsNullOrEmpty(hudTip)){
            var tipStyle=new GUIStyle(hudTipTable?hudSmall:hudText){fontSize=hudTipTable?14:15,alignment=TextAnchor.UpperLeft,wordWrap=!hudTipTable,padding=new RectOffset(0,0,0,0)};
            const float width=306,pad=11;float content=tipStyle.CalcHeight(new GUIContent(hudTip),width-pad*2);
            float height=Mathf.Clamp(content+pad*2,40,450),top=Mathf.Max(30,490-height);
            StoneFrame(new Rect(962,top,width,height));GUI.Label(new Rect(962+pad,top+pad,width-pad*2,height-pad*2),hudTip,tipStyle);
        }
    }
    string GoldTooltip()=>"Золото: "+Mathf.FloorToInt(match.Gold[0])+"\nРезерв очереди: "+match.ReservedGold(0)+"\nДоступно: "+Mathf.FloorToInt(match.AvailableGold(0))+"\nДоход: "+match.Income(0).ToString("F2")+" / 10 с\nНачисление через: "+Mathf.CeilToInt(match.IncomeIn)+" с";
    void DrawTeamRow(int team,float y,string name) {
        var stats=TeamOverview(team);
        GUI.Label(new Rect(966,y,60,19),name,new GUIStyle(hudSmall){normal={textColor=team==0?new Color(.4f,.7f,1):new Color(1,.38f,.3f)}});
        GUI.Label(new Rect(1035,y,40,19),stats.buildings,hudSmall);
        GUI.Label(new Rect(1080,y,40,19),stats.army,hudSmall);
        GUI.Label(new Rect(1133,y,48,19),stats.income,hudSmall);
        GUI.Label(new Rect(1190,y,65,19),stats.castle,hudSmall);
    }

    (string buildings,string army,string income,string castle) TeamOverview(int team){
        string castle=Mathf.Max(0,match.CastleHp[team]).ToString("F0");
        if(onlineMatch){
            if(onlineTeamOverview?.Count!=2||networkSlot<0||networkSlot>1)
                return ("—","—","—",castle);
            var row=onlineTeamOverview[team==0?networkSlot:1-networkSlot];
            return (((int?)row["buildings"]??0).ToString(),((int?)row["army"]??0).ToString(),
                ((float?)row["income"]??0).ToString("F1"),castle);
        }
        return (match.Huts.Count(h=>h.team==team&&h.paid&&h.hp>0).ToString(),match.ArmyCount(team).ToString(),match.Income(team).ToString("F1"),castle);
    }
    HumanMatch helpMatch;
    bool HelpOpen => page==Page.Match && match!=null && helpMatch==match;
    int[] helpKinds=Array.Empty<int>();
    Texture2D[] helpIcons=Array.Empty<Texture2D>();
    bool helpBuildings;
    int helpSelected;
    string helpBody;
    Vector2 helpScroll;
    const int HelpPageSize=15;
    static readonly Rect HelpTextArea=new Rect(700,224,480,334);
    void CloseBattleHelp(){
        helpMatch=null;
        helpKinds=Array.Empty<int>();
        helpIcons=Array.Empty<Texture2D>();
        helpBody=null;
    }
    void OpenBattleHelp(){
        if(page!=Page.Match||match==null||aspectWaiting)return;
        helpMatch=match;
        selectionPending=selectionDragging=false;
        SetHelpCategory(false);
    }
    void SetHelpCategory(bool buildings){
        helpBuildings=buildings;
        var seen=new System.Collections.Generic.HashSet<string>();
        helpKinds=Enumerable.Range(0,match.Lines.Length).Where(k=>{
            var line=match.Lines[k];
            if(!match.PlayerHasLine(k)||line.category=="helper")return false;
            return buildings ? !line.Producer && line.cost>0 : line.Producer && seen.Add(line.unit);
        }).ToArray();
        helpIcons=new Texture2D[helpKinds.Length];
        for(int i=0;i<helpKinds.Length;i++){
            int kind=helpKinds[i];
            if(buildings){
                string raw=match.Lines[kind].building;
                if(raceIndices[matchRace].TryGetValue(raw,out int row))helpIcons[i]=HdIcon(CatalogBuildingIcon(matchRace,row,raw));
            }else helpIcons[i]=GroupIcon(kind,matchRace);
        }
        SelectHelpEntry(0);
    }
    void SelectHelpEntry(int index){
        helpSelected=index;
        helpScroll=Vector2.zero;
        if(helpKinds.Length==0){helpBody="Нет доступных записей.";return;}
        int kind=helpKinds[index];
        var line=match.Lines[kind];
        var stats=helpBuildings?line.buildingStats:line.stats;
        var body=new System.Text.StringBuilder();
        body.Append("Здоровье  ").Append(SkillNumber(HumanMatch.F(stats,"hp")));
        float mana=HumanMatch.F(stats,"max_mana");
        if(mana>0)body.Append("     Мана  ").Append(SkillNumber(mana));
        body.Append("\nБроня  ").Append(SkillNumber(HumanMatch.F(stats,"armor"))).Append(" · ").Append(ArmorLabel((string)stats["armor_type"]??(helpBuildings?"fortified":"normal")));
        foreach(var w in helpBuildings?line.buildingWeapons:line.weapons)
            body.Append("\n").Append(AttackLabel(w.attack)).Append(" атака  ").Append(SkillNumber(w.damage)).Append(" · каждые ").Append(SkillNumber(w.cooldown)).Append(" с · дальность ").Append(Mathf.RoundToInt(w.range*30));
        if(helpBuildings)body.Append("\nСтоимость  ").Append(match.GoldPrice(0,kind)).Append(" золота · ").Append(line.woodCost).Append(" дерева");
        else body.Append("\nСкорость  ").Append(Mathf.RoundToInt(line.speed*30));
        var ids=helpBuildings?line.abilities:HumanMatch.Strings(stats["abilities"]);
        if(helpBuildings&&line.building=="h061")ids=ids.Concat(new[]{"A0DC"}).ToArray();
        if(helpBuildings&&line.building=="h09T")ids=ids.Concat(new[]{"A09S"}).ToArray();
        string raw=helpBuildings?line.building:line.unit;
        foreach(string id in ids.Distinct()){
            var cfg=match.Abilities[id];
            if(cfg==null||(bool?)cfg["hide_card"]==true||!ContextAbilityVisible(raw,id,cfg))continue;
            if((string)cfg["kind"]=="marker"&&(bool?)cfg["card_description"]!=true&&id!="A09S")continue;
            var ranks=cfg["ranks"] as Newtonsoft.Json.Linq.JArray;
            int rank=(int?)stats["ability_ranks"]?[id]??1;
            var shown=ranks!=null&&ranks.Count>0?ranks[Mathf.Clamp(rank-1,0,ranks.Count-1)]:cfg;
            string tip=ContextSkillTip(raw,id,shown).Trim();
            if(tip.Length>0)body.Append("\n\n").Append(tip);
        }
        if(helpBuildings){
            string summary=BuildingSummary(raw);
            if(summary!="Оборонительная башня")body.Append("\n\n").Append(summary);
        }
        helpBody=body.ToString();
    }
    void DrawBattleHelp(){
        InitHud();
        StoneFrame(new Rect(66,72,1148,562));
        Text(102,88,845,50,"Справка · "+RaceName,heading);
        if(Button("info.close",new Rect(1020,92,160,44),"Закрыть")){CloseBattleHelp();return;}
        if(Button("help.units",new Rect(102,150,250,48),"Бойцы",!helpBuildings)&&helpBuildings)SetHelpCategory(false);
        if(Button("help.buildings",new Rect(364,150,270,48),"Спецздания",helpBuildings)&&!helpBuildings)SetHelpCategory(true);
        int first=helpSelected/HelpPageSize*HelpPageSize;
        for(int i=first;i<Math.Min(first+HelpPageSize,helpKinds.Length);i++){
            int n=i-first;
            var rect=new Rect(102+(n%5)*108,217+(n/5)*110,101,104);
            var line=match.Lines[helpKinds[i]];
            if(Button("help.entry."+i,rect,"",i==helpSelected))SelectHelpEntry(i);
            if(helpIcons[i])GUI.DrawTexture(new Rect(rect.x+23,rect.y+4,55,55),helpIcons[i],ScaleMode.ScaleToFit);
            GUI.Label(new Rect(rect.x+2,rect.y+60,rect.width-4,42),helpBuildings?line.name:line.unitName,HelpCaption);
            if(i==helpSelected)Border(rect,new Color(.94f,.74f,.32f),2);
        }
        if(helpKinds.Length>HelpPageSize){
            if(first>0&&Button("help.prev",new Rect(102,558,120,44),"Назад"))SelectHelpEntry(first-HelpPageSize);
            Text(258,559,100,40,(1+first/HelpPageSize)+" / "+Mathf.CeilToInt(helpKinds.Length/(float)HelpPageSize),label);
            if(first+HelpPageSize<helpKinds.Length&&Button("help.next",new Rect(445,558,186,44),"Следующая"))SelectHelpEntry(first+HelpPageSize);
        }
        if(helpKinds.Length>0){
            var selected=match.Lines[helpKinds[helpSelected]];
            Text(700,146,478,74,helpBuildings?selected.name:selected.unitName,HelpTitle);
        }
        float width=HelpTextArea.width-26;
        float height=Mathf.Max(HelpTextArea.height,HelpParagraph.CalcHeight(new GUIContent(helpBody),width)+8);
        helpScroll=GUI.BeginScrollView(HelpTextArea,helpScroll,new Rect(0,0,width,height));
        GUI.Label(new Rect(0,0,width,height),helpBody,HelpParagraph);
        GUI.EndScrollView();
    }
    GUIStyle helpCaption,helpTitle,helpParagraph;
    GUIStyle HelpCaption=>helpCaption??(helpCaption=new GUIStyle(hudSmall){fontSize=13,alignment=TextAnchor.MiddleCenter,wordWrap=true});
    GUIStyle HelpTitle=>helpTitle??(helpTitle=new GUIStyle(hudTitle){fontSize=24,wordWrap=true});
    GUIStyle HelpParagraph=>helpParagraph??(helpParagraph=new GUIStyle(label){fontSize=19,wordWrap=true,alignment=TextAnchor.UpperLeft});

    System.Collections.IEnumerator ExerciseBattleHelp(){
        MainMenu();raceChoice=0;botRaceChoice=1;aspectsOn=false;NewMatch();
        yield return Action("match.help");
        Check(HelpOpen&&page==Page.Match,"Help uses the live match");
        Check(helpKinds.Length>1&&helpKinds.All(k=>match.PlayerHasLine(k)&&match.Lines[k].Producer),"Only player fighters");
        Check(match.Lines.Any(l=>!match.PlayerHasLine(match.Kind(l.building))),"Opponent has distinct entries");
        float before=match.Elapsed;
        yield return Action("help.entry.1");
        Check(helpSelected==1&&helpBody.Contains("Здоровье"),"Fighter details selectable");
        yield return Action("help.buildings");
        Check(helpBuildings&&helpKinds.Length>0&&helpKinds.All(k=>match.PlayerHasLine(k)&&!match.Lines[k].Producer),"Only player special buildings");
        yield return new WaitForSecondsRealtime(.35f);
        Check(match.Elapsed>before&&controls.InputBlocked,"Match advances and field input is blocked");
        yield return Action("info.close");
        Check(!HelpOpen&&page==Page.Match,"Close returns to same match");
        Debug.Log("BATTLE_HELP_RESULT failures="+testFailures);
        Application.Quit(testFailures==0?0:1);
    }

    void DrawInfoPanel() {
        GUI.enabled=false;DrawMatch();GUI.enabled=true;
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.6f));StoneFrame(new Rect(340,155,600,390));
        GUI.Label(new Rect(375,180,530,40),page==Page.Help?"Управление":"Стороны матча",heading);
        if(page==Page.Help)GUI.Label(new Rect(375,240,530,220),
            Application.isMobilePlatform ? "Коснитесь рабочего, бойца или здания, чтобы выбрать.\nВыберите постройку или команду на панели, затем коснитесь цели.\nКоснитесь пустого места, чтобы отправить выбранного рабочего.\nПроведите пальцем по полю для перемещения камеры.\nСведите или разведите два пальца для изменения масштаба.\nКоснитесь миникарты для быстрого перехода.\nЮниты из зданий сражаются автоматически." :
            "ЛКМ — выбрать рабочего, бойца или здание. ПКМ — идти или продолжить стройку.\nB — доступные постройки; затем клавиша на значке. U — улучшить здание.\nЛКМ — место постройки. Esc / ПКМ — отмена выбора.\nM — движение, S — стоп, D — скачок, R — ремонт, V — спасение.\nF1 — строитель. Стрелки / край окна — камера.\nКолесо — масштаб. Щелчок по миникарте — обзор.\nЮниты из зданий сражаются автоматически.",hudText);
        else DrawTeamOverview();
        if(Button("info.close",new Rect(500,472,280,46),"Вернуться в матч",true))page=Page.Match;
    }
    void DrawTeamOverview(){
        var header=new GUIStyle(hudSmall){fontSize=14};
        GUI.Label(new Rect(375,243,196,30),"Сторона",header);
        GUI.Label(new Rect(590,243,112,30),"Золото / 10 с",header);
        GUI.Label(new Rect(717,243,98,30),"Постройки",header);
        GUI.Label(new Rect(830,243,78,30),"HP замка",header);
        for(int team=0;team<2;team++){
            float y=280+team*78;
            var color=team==0?new Color(.49f,.73f,1):new Color(1,.48f,.37f);
            Fill(new Rect(375,y,530,68),new Color(.045f,.066f,.071f,.93f));
            Fill(new Rect(375,y,2,68),color);
            string name=team==0?"Вы":onlineMatch?"Соперник":"Бот";
            if(onlineMatch&&networkRoom?["players"] is Newtonsoft.Json.Linq.JArray players&&players.Count==2&&networkSlot>=0&&networkSlot<=1)
                name=(string)players[team==0?networkSlot:1-networkSlot]?["name"]??name;
            GUI.Label(new Rect(390,y+8,189,26),name,new GUIStyle(hudText){fontStyle=FontStyle.Bold,normal={textColor=color}});
            GUI.Label(new Rect(390,y+35,189,20),team==0?RaceName:BotRaceName,hudSmall);
            var stats=TeamOverview(team);
            GUI.Label(new Rect(590,y,112,68),stats.income,hudText);
            GUI.Label(new Rect(717,y,94,68),stats.buildings,hudText);
            GUI.Label(new Rect(830,y,70,68),stats.castle,hudText);
        }
    }
    void DrawMinimap() {
        var rect=HudMap;Fill(rect,new Color(.025f,.035f,.025f));if(minimapTerrain)Source(hordeMinimap?hordeMinimap:winterMinimap?winterMinimap:minimapTexture,new Rect(0,52,256,153),rect);
        DrawMinimapFog(rect);
        Func<Vector2,Vector2> point=p=>new Vector2(rect.x+(p.x+204.8f)/409.6f*rect.width,rect.y+(p.y+3584f/30)/(7168f/30)*rect.height);
        foreach(var hut in match.Huts.Where(h=>!FogActive||h.team==0||fogKnownHuts.Contains(h.id))){var p=point(hut.position);Fill(new Rect(p.x-2,p.y-2,4,4),hut.team==0?new Color(.25f,.55f,1):Color.red);}
        foreach(var unit in match.Units)if(unit.hp>0&&!match.Hidden(unit)&&CanSeeUnit(unit)){var p=point(unit.position);Fill(new Rect(p.x-1,p.y-1,3,3),unit.team==0?(minimapTeamColors?new Color(.35f,.65f,1):Color.green):new Color(1,.2f,.1f));}
        var worker=point(match.Builders[0].position);Border(new Rect(worker.x-3,worker.y-3,7,7),Color.green,1);
        var camera=Camera.main;var plane=new Plane(Vector3.up,new Vector3(0,12,0));var ray=camera.ScreenPointToRay(new Vector3(guiOrigin.x+640*guiScale,Screen.height-guiOrigin.y-274*guiScale));
        if(plane.Raycast(ray,out float distance)){var p=ray.GetPoint(distance);var c=point(new Vector2(p.x,-p.z));Border(new Rect(Mathf.Clamp(c.x-15,rect.x,rect.xMax-30),Mathf.Clamp(c.y-9,rect.y,rect.yMax-18),30,18),Color.white);}
        if(GUI.enabled&&Event.current.type==EventType.MouseDown&&Event.current.button==0&&rect.Contains(Event.current.mousePosition)) {
            var m=Event.current.mousePosition;controls.PanToWorld(World(new Vector2((m.x-rect.x)/rect.width*409.6f-204.8f,(m.y-rect.y)/rect.height*(7168f/30)-3584f/30)));Event.current.Use();
        }
    }
    void LateUpdate() {
        UpdateWorkshopPreview();
        if(match==null)return;
        var prefab=PortraitPrefab(out var raw);if(!prefab)return;
        if(!portraitCamera) {
            portraitStage=new GameObject("Selection portrait stage");portraitStage.transform.position=new Vector3(0,-1000,0);
            portraitCamera=new GameObject("Selection portrait camera").AddComponent<Camera>();portraitCamera.transform.SetParent(portraitStage.transform);
            portraitCamera.fieldOfView=28;portraitCamera.nearClipPlane=.05f;portraitCamera.farClipPlane=100;portraitCamera.cullingMask=1<<31;
            portraitCamera.clearFlags=CameraClearFlags.SolidColor;portraitCamera.backgroundColor=new Color(.055f,.043f,.025f);portraitCamera.enabled=false;
            portraitTexture=new RenderTexture(256,256,16);portraitCamera.targetTexture=portraitTexture;Camera.main.cullingMask&=~(1<<31);
        }
        if(portraitRaw!=prefab.name) {
            if(portraitModel){portraitModel.gameObject.SetActive(false);Destroy(portraitModel.gameObject);}
            portraitModel=Instantiate(prefab,portraitStage.transform).GetComponent<PcModel>();portraitRaw=prefab.name;
            foreach(var tr in portraitModel.GetComponentsInChildren<Transform>())tr.gameObject.layer=31;
            portraitModel.transform.localRotation=Quaternion.identity;
            portraitModel.SamplePose();var head=portraitModel.GetComponentsInChildren<Transform>().FirstOrDefault(t=>string.Equals(t.name,"Bone_Head",StringComparison.OrdinalIgnoreCase));
            if(ClassicWispBody040.IsWisp(portraitModel.raw))FrameWisp040(portraitModel);
            else if(portraitModel.reforged&&portraitModel.raw=="h016") {
                // The rider's frontal camera is occluded by the Gryphon's wing.
                // Frame the mounted actor until a dedicated rider portrait is imported.
                var bounds=portraitModel.BodyBounds();float size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
                var riderHead=portraitModel.GetComponentsInChildren<Transform>().FirstOrDefault(t=>string.Equals(t.name,"Head Ref",StringComparison.OrdinalIgnoreCase));
                var point=riderHead?riderHead.position:bounds.center;
                portraitCamera.transform.position=point+new Vector3(size*.7f,size*.15f,size*1.65f);portraitCamera.transform.LookAt(point);
            }
            else if(SelectedUnit!=null&&new[]{"n00I","n00J","n00K"}.Contains(portraitModel.raw)) {
                // Murloc heads and spines extend well outside the humanoid head framing.
                var bounds=portraitModel.BodyBounds();float size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
                var point=bounds.center+Vector3.up*(bounds.size.y*.08f);portraitCamera.transform.position=point+new Vector3(size*.75f,size*.22f,Mathf.Max(2,size*2.5f));portraitCamera.transform.LookAt(point);
            }
            else if(portraitModel.reforged&&(builderSelected||SelectedUnit!=null)&&portraitModel.HeadBounds(out var face)) {
                var anchor=portraitModel.GetComponentsInChildren<Transform>().FirstOrDefault(t=>string.Equals(t.name,"Head Ref",StringComparison.OrdinalIgnoreCase));
                float span=Mathf.Clamp(Mathf.Max(face.size.x,face.size.y),.28f,.7f);var point=anchor?anchor.position+Vector3.up*span*.2f:face.center;float distance=Mathf.Max(1.6f,span*3.3f);
                // The Reforged Footman holds his shield in front of the frontal portrait camera.
                var offset=portraitModel.raw=="hfoo"?new Vector3(-distance*.625f,span*.6f,distance):new Vector3(0,span*.03f,distance);
                portraitCamera.transform.position=point+offset;portraitCamera.transform.LookAt(point);
                Debug.Log("CF_REFORGED_PORTRAIT raw="+portraitModel.raw+" head="+face.center+" size="+face.size+" camera="+portraitCamera.transform.position);
            }
            else if(!portraitModel.reforged&&raw=="h00P"&&head){var point=head.position+Vector3.up*.04f;portraitCamera.transform.position=point+new Vector3(0,.02f,Mathf.Max(.8f,portraitModel.ClassicHeight*.65f));portraitCamera.transform.LookAt(point);}
            else if(!portraitModel.reforged&&raw=="h018"&&head) {
                var point=head.position+Vector3.up*.08f;portraitCamera.transform.position=point+new Vector3(0,.05f,1.9f);portraitCamera.transform.LookAt(point);
            }
            else if(!portraitModel.reforged&&new[]{"h00P","h089","n00L","n02K","n02L","n017","n012","n014","n015","n011","n010","n018","n013","o00A","n02I","n016","n01U","z002","z003","n01T","n02J","n01V","h06Q","h06R","n01X","n024","h06T","h06U","h099","h06S","h098","h06P","h018","u00D","u009","u005","u001","u000","h01U","h05H","n00E","n00B","n01K","u007","u002","n00D","u00A","n00C","u00B","n01L","u003","n01M","h01V","u006","n01I","n01H","n027","h04I","n01G","n01F","n01E","n01D","n01B","n019","n01A","n01C"}.Contains(raw)) {
                // These classic imports expose no legacy per-vertex head weights.
                // Use the source silhouette height, including its Godot scale correction.
                float height=Mathf.Max(.5f,portraitModel.ClassicHeight),span=height*.4f;
                var point=portraitModel.transform.position+Vector3.up*(height*.78f);
                portraitCamera.transform.position=point+new Vector3(0,span*.03f,span*3.3f);portraitCamera.transform.LookAt(point);
            }
            else if(builderSelected&&!portraitModel.reforged&&new[]{"h075","hUAv","hPBu"}.Contains(raw)) {
                var bounds=portraitModel.BodyBounds();float height=Mathf.Max(.5f,bounds.size.y);
                var point=head?head.position+Vector3.up*height*.025f:bounds.center+Vector3.up*height*.22f;
                float distance=Mathf.Max(.9f,height*(raw=="hUAv"?.95f:.72f));
                portraitCamera.transform.position=point+new Vector3(0,height*.015f,distance);portraitCamera.transform.LookAt(point);
            }
            else if(builderSelected&&!portraitModel.reforged&&raw=="cBld"){
                var bounds=portraitModel.BodyBounds();var point=portraitModel.HeadBounds(out var centaurFace)?centaurFace.center:bounds.center+Vector3.up*bounds.size.y*.22f;
                float distance=Mathf.Max(1.6f,bounds.size.y*.58f);portraitCamera.transform.position=point+new Vector3(0,.04f,distance);portraitCamera.transform.LookAt(point);
            }
            else if(builderSelected&&!portraitModel.reforged){portraitCamera.transform.localPosition=new Vector3(0,1.4f,1.4f);portraitCamera.transform.LookAt(portraitStage.transform.position+new Vector3(0,1.35f,0));}
            else if(SelectedUnit!=null&&!portraitModel.reforged&&new[]{"h034","h03N","o00C","o00D","o00E","h02Z","h030","h03P","h03R","h02W","h02X","h02Y","h03V"}.Contains(raw)){
                var bounds=portraitModel.BodyBounds();float size=Mathf.Max(.5f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)));var centre=bounds.center;
                portraitCamera.transform.position=centre+new Vector3(0,size*.08f,size*2.8f);portraitCamera.transform.LookAt(centre);
            }
            else if((SelectedUnit!=null||builderSelected)&&head){var point=head.position+Vector3.up*.08f;portraitCamera.transform.position=point+new Vector3(0,.04f,1.6f);portraitCamera.transform.LookAt(point);}
            else {var bounds=portraitModel.BodyBounds();float size=Mathf.Max(bounds.size.x,bounds.size.y);var centre=bounds.center;portraitCamera.transform.position=centre+new Vector3(0,size*.07f,Mathf.Max(2,size*1.9f));portraitCamera.transform.LookAt(centre);}
        }
        if(HdArt040&&portraitModel&&portraitModel.reforged)DrawHdActor040(portraitModel,false,Time.unscaledTime,portraitCamera,31);
        ClassicWispBody040.FacePortrait(portraitModel,portraitCamera);
        portraitCamera.Render();
    }
    void DestroyHud(){HudTheme.Dispose();if(mythicCrown)Destroy(mythicCrown);if(portraitStage)Destroy(portraitStage);if(portraitTexture){portraitTexture.Release();Destroy(portraitTexture);}}
}
}
