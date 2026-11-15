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
    Rect Slot(int column,int row)=>CommandSlot(column,row,Application.isMobilePlatform);
    static Rect InventorySlot(int cell)=>ConsoleCell(5+cell%2*84,290+cell/2*73,64,62);
    void DrawWarcraftHud() {
        InitHud();hudTip=null;hudTipTable=false;
        Fill(new Rect(0,0,1280,28),new Color(.025f,.035f,.018f,.98f));
        Fill(new Rect(0,558,1280,162),Color.black);
        for(int i=0;i<4;i++) {
            float width=i==3?51.2f:409.6f;
            Source(hudTiles[i],new Rect(0,176,hudTiles[i].width,336),new Rect(i*409.6f,520,width,200));
            Source(hudTiles[i],new Rect(0,0,hudTiles[i].width,48),new Rect(i*409.6f,0,width,28));
        }
        Source(hudTiles[1],new Rect(170,0,180,110),new Rect(546,0,144,66));
        GUI.DrawTexture(new Rect(619,5,42,42),timeIndicator);
        var topButton=new GUIStyle(button){fontSize=Application.isMobilePlatform?15:13,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(3,3,0,0)};float topHeight=Application.isMobilePlatform?40:24;
        if(Button("match.help",new Rect(5,1,132,topHeight),Application.isMobilePlatform?"Справка":"Справка (F9)",false,topButton))page=Page.Help;
        float builderTop=Application.isMobilePlatform?46:31;var builderFrame=new Rect(5,builderTop,52,52);StoneFrame(builderFrame);
        if(Icon("hud.builder",new Rect(9,builderTop+4,44,44),0,"F1","Строитель (F1)\nВыделить и перейти к строителю",builderSelected))FocusBuilder();
        if(Button("match.pause",new Rect(142,1,131,topHeight),Application.isMobilePlatform?"Меню":"Меню (F10)",false,topButton))page=Page.Pause;
        if(Button("match.teams",new Rect(278,1,131,topHeight),Application.isMobilePlatform?"Стороны":"Стороны (F11)",false,topButton))page=Page.Teams;
        bool inputEnabled=GUI.enabled;GUI.enabled=inputEnabled&&onlineMatch;
        if(Button("match.chat",new Rect(414,1,131,topHeight),Application.isMobilePlatform?"Чат":"Чат (F12)",false,topButton))showBattleChat=!showBattleChat;GUI.enabled=inputEnabled;
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
        GUI.Label(new Rect(966,116,290,18),"Время: "+TimeSpan.FromSeconds(match.Elapsed).ToString(@"mm\:ss")+" · Доход через "+Mathf.CeilToInt(match.IncomeIn)+" с",new GUIStyle(hudSmall){fontSize=11});}
        DrawMinimap();
        if(MinimapButton("hud.worker",0,"Показать рабочего (F1)"))FocusBuilder();
        if(MinimapButton("hud.terrain",1,"Ландшафт миникарты"))minimapTerrain=!minimapTerrain;
        if(MinimapButton("hud.battle",2,"Показать бой у реки"))controls.Focus("river");
        if(MinimapButton("hud.colors",3,"Цвета сторон на миникарте"))minimapTeamColors=!minimapTeamColors;
        if(Application.isMobilePlatform){Fill(new Rect(960,482,320,238),new Color(.02f,.025f,.025f,.94f));Border(new Rect(960,482,320,238),new Color(.42f,.36f,.25f));}DrawHumanSelection();DrawItemInventory();DrawSelectionMarquee();
        foreach(var u in match.Units)if(u.hp>0&&!match.Hidden(u)&&CanSeeUnit(u)&&match.Lines[u.kind].unit!="n02E"&&units.TryGetValue(u.id,out var view)) {
            var p=Camera.main.WorldToScreenPoint(view.transform.position+Vector3.up*(view.godotHeight>0?view.godotHeight+.4f:(view.raw.StartsWith("cU",StringComparison.Ordinal)?view.ClassicHeight+.4f:FinalMarkerHeight(view.raw))));if(p.z<=0)continue;
            float x=(p.x-guiOrigin.x)/guiScale,y=(Screen.height-p.y-guiOrigin.y)/guiScale;
            if(y<42||y>516||x<0||x>1280)continue;
            Fill(new Rect(x-17,y,34,4),new Color(.02f,.02f,.02f,.95f));Fill(new Rect(x-16,y+1,32*u.hp/u.maxHp,2),u.team==0?new Color(.2f,.85f,.25f):new Color(.95f,.2f,.14f));float debt=Mathf.Min(u.hp,match.PandaStagger(u));if(debt>0)Fill(new Rect(x-16+32*(u.hp-debt)/u.maxHp,y+1,32*debt/u.maxHp,2),new Color(1,.67f,.16f));DrawUnitDebuffs(u,x,y);DrawManaBar(new Rect(x-17,y+5,34,4),u.mana,u.maxMana,false);
        }
        foreach(var h in match.Huts)if(h.paid&&CanInspectHut(h)&&huts.TryGetValue(h.id,out var buildingView)&&(HumanMatch.F(match.Lines[h.kind].buildingStats,"max_mana")>0||h.id==selectedHut)){
            var point=Camera.main.WorldToScreenPoint(buildingView.transform.position+Vector3.up*Mathf.Max(3,buildingView.ClassicHeight+.4f));if(point.z<=0)continue;float x=(point.x-guiOrigin.x)/guiScale,y=(Screen.height-point.y-guiOrigin.y)/guiScale;if(y<42||y>510||x<0||x>1280)continue;
            Fill(new Rect(x-24,y,48,5),Color.black);Fill(new Rect(x-23,y+1,46*Mathf.Clamp01(h.hp/h.maxHp),3),h.team==0?new Color(.2f,.85f,.25f):new Color(.95f,.2f,.14f));DrawManaBar(new Rect(x-24,y+6,48,4),h.mana,HumanMatch.F(match.Lines[h.kind].buildingStats,"max_mana"),false);
        }
        foreach(var p in match.Pulses)if(!string.IsNullOrEmpty(p.text)&&FogVisible(p.position)) {
            var point=Camera.main.WorldToScreenPoint(World(p.position)+Vector3.up*(3+p.age));if(point.z<=0)continue;
            float x=(point.x-guiOrigin.x)/guiScale,y=(Screen.height-point.y-guiOrigin.y)/guiScale;
            if(y<40||y>480)continue;GUI.Label(new Rect(x-90,y,180,24),p.text,new GUIStyle(hudSmall){alignment=TextAnchor.MiddleCenter,normal={textColor=p.color}});
        }
        if(noticeLeft>0){
            float alpha=Mathf.Min(1,noticeLeft);var message=new GUIStyle(hudText){fontSize=15,alignment=TextAnchor.LowerLeft,normal={textColor=new Color(0,0,0,alpha)}};
            GUI.Label(new Rect(336,467,610,32),notice,message);message.normal.textColor=new Color(1,.83f,.32f,alpha);GUI.Label(new Rect(335,466,610,32),notice,message);
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
        GUI.Label(new Rect(966,y,60,19),name,new GUIStyle(hudSmall){normal={textColor=team==0?new Color(.4f,.7f,1):new Color(1,.38f,.3f)}});
        GUI.Label(new Rect(1035,y,40,19),match.Huts.Count(h=>h.team==team).ToString(),hudSmall);
        GUI.Label(new Rect(1080,y,40,19),match.ArmyCount(team).ToString(),hudSmall);
        GUI.Label(new Rect(1133,y,48,19),match.Income(team).ToString("F1"),hudSmall);
        GUI.Label(new Rect(1190,y,65,19),match.CastleHp[team].ToString("F0"),hudSmall);
    }
    void DrawInfoPanel() {
        GUI.enabled=false;DrawMatch();GUI.enabled=true;
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.6f));StoneFrame(new Rect(340,155,600,390));
        GUI.Label(new Rect(375,180,530,40),page==Page.Help?"Управление":"Стороны матча",heading);
        if(page==Page.Help)GUI.Label(new Rect(375,240,530,220),
            Application.isMobilePlatform ? "Коснитесь рабочего, бойца или здания, чтобы выбрать.\nВыберите постройку или команду на панели, затем коснитесь цели.\nКоснитесь пустого места, чтобы отправить выбранного рабочего.\nПроведите пальцем по полю для перемещения камеры.\nСведите или разведите два пальца для изменения масштаба.\nКоснитесь миникарты для быстрого перехода.\nЮниты из зданий сражаются автоматически." :
            "ЛКМ — выбрать рабочего, бойца или здание. ПКМ — идти или продолжить стройку.\nB — доступные постройки; затем клавиша на значке. U — улучшить здание.\nЛКМ — место постройки. Esc / ПКМ — отмена выбора.\nM — движение, S — стоп, D — скачок, R — ремонт, V — спасение.\nF1 — строитель. Стрелки / край окна — камера.\nКолесо — масштаб. Щелчок по миникарте — обзор.\nЮниты из зданий сражаются автоматически.",hudText);
        else GUI.Label(new Rect(375,240,530,190),
            "Запад — вы · "+RaceName+" / "+RaceEnglish+"\nВосток — "+(onlineMatch?"соперник":"бот")+" · "+BotRaceName+"\n\nОдинаковые цены, доход и правила строительства.\nЗадача: разрушить замок противника.\nРежим: "+(onlineMatch?"сетевой":"офлайн")+", 1 × 1.",hudText);
        if(Button("info.close",new Rect(500,472,280,46),"Вернуться в матч",true))page=Page.Match;
    }
    void DrawMinimap() {
        var rect=new Rect(14,577,224,107);Fill(rect,new Color(.025f,.035f,.025f));if(minimapTerrain)Source(minimapTexture,new Rect(0,52,256,153),rect);
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
        if(match==null){RenderMenuPortrait();return;}
        if(menuPortraitModel){menuPortraitModel.gameObject.SetActive(false);Destroy(menuPortraitModel.gameObject);menuPortraitModel=null;menuPortraitRaw=null;}
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
            if(portraitModel.reforged&&portraitModel.raw=="h016") {
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
            else if(!portraitModel.reforged&&raw=="h051"){var bounds=portraitModel.BodyBounds();float size=Mathf.Max(.75f,Mathf.Max(bounds.size.x,bounds.size.y));var point=bounds.center;portraitCamera.transform.position=point+new Vector3(0,size*.05f,size*2.5f);portraitCamera.transform.LookAt(point);}
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
        portraitCamera.Render();
    }
    void DestroyHud(){if(portraitStage)Destroy(portraitStage);if(portraitTexture){portraitTexture.Release();Destroy(portraitTexture);}}
}
}

