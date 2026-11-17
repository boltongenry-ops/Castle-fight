using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    int selectedHut=-1,selectedUnit=-1;string targetedSpell,portraitRaw;bool showRange;
    GameObject rangeRing;readonly Dictionary<int,LineRenderer> humanEffects=new Dictionary<int,LineRenderer>();
    HumanMatch.Hut SelectedHut=>match?.Huts.FirstOrDefault(h=>h.id==selectedHut&&CanInspectHut(h));
    HumanMatch.Unit SelectedUnit=>match?.Units.FirstOrDefault(u=>u.id==selectedUnit&&u.hp>0&&CanSeeUnit(u));
    static string Clean(string s)=>Regex.Replace(s??"",@"\|c[0-9a-fA-F]{8}|\|r","").Replace("|n","\n");
    void SelectAtCursor() {
        ClearGroupSelection();
        float scale=guiScale;Vector2 pointer=ActivePointer;
        var worker=Camera.main.WorldToScreenPoint(World(match.Builders[0].position)+Vector3.up*1.4f);
        selectedCastle=false;selectedItem=-1;builderSelected=worker.z>0&&Vector2.Distance(pointer,worker)<25*scale;selectedHut=selectedUnit=-1;showRange=false;buildMenu=false;chosen=-1;moveOrder=false;targetedSpell=null;
        if(builderSelected)return;
        var castlePoint=Camera.main.WorldToScreenPoint(World(HumanMatch.CastlePosition(0))+Vector3.up*6);if(castlePoint.z>0&&Vector2.Distance(pointer,castlePoint)<65*scale){SelectCastle(false);return;}
        float best=32*scale;
        foreach(var u in match.Units.Where(u=>u.hp>0&&!match.Hidden(u)&&CanSeeUnit(u)&&match.Lines[u.kind].unit!="n02E")){float center=units.TryGetValue(u.id,out var actor)&&actor.godotHeight>0?actor.godotHeight*.5f:1.3f;var p=Camera.main.WorldToScreenPoint(World(u.position)+Vector3.up*(match.Height(u)+center));float d=Vector2.Distance(pointer,p);if(p.z>0&&d<best){best=d;selectedUnit=u.id;}}
        if(selectedUnit>=0)return;
        best=55*scale;
        foreach(var h in match.Huts.Where(CanInspectHut)){var p=Camera.main.WorldToScreenPoint(World(h.position)+Vector3.up*2.5f);float d=Vector2.Distance(pointer,p);if(p.z>0&&d<best){best=d;selectedHut=h.id;}}
    }
    void HandleCommandKeys() {
        for(int slot=0;slot<6;slot++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1+slot)))BeginItem(slot);
        if(selectedCastle)return;
        if(builderSelected) {
            if(Input.GetKeyDown(KeyCode.B)){selectedItem=-1;constructionPage=0;buildMenu=true;targetedSpell=null;moveOrder=false;}
            if(buildMenu) {
                if(Input.GetKeyDown(KeyCode.Tab)&&ConstructionKinds().Length>11)constructionPage=1-constructionPage;
                foreach(int i in match.BaseKinds)if(Enum.TryParse<KeyCode>(match.Lines[i].hotkey,true,out var key)&&Input.GetKeyDown(key)){Select(i);int position=Array.IndexOf(ConstructionKinds(),i);constructionPage=ConstructionKinds().Length>11?position/10:0;}
            } else {
                if(Input.GetKeyDown(KeyCode.M)){selectedItem=-1;moveOrder=true;chosen=-1;targetedSpell=null;Notice("Укажите точку движения ЛКМ");}
                if(Input.GetKeyDown(KeyCode.S)||Input.GetKeyDown(KeyCode.H))StopBuilder();
                if(Input.GetKeyDown(KeyCode.D))BeginBuilderSpell("blink");
                if(Input.GetKeyDown(KeyCode.V))BeginBuilderSpell("rescue");
                if(Input.GetKeyDown(KeyCode.R))RepairOrder();
                if(Input.GetKeyDown(KeyCode.P))SetProductionPaused(true);
                if(Input.GetKeyDown(KeyCode.O))SetProductionPaused(false);
            }
        } else if(SelectedHut is HumanMatch.Hut h&&h.team==0) {
            if(Input.GetKeyDown(KeyCode.P))ToggleProduction(h);
            if(Input.GetKeyDown(KeyCode.U)&&match.Lines[h.kind].upgrades.Length>0)UpgradeSelected(match.Kind(match.Lines[h.kind].upgrades[0]));
            if(Input.GetKeyDown(KeyCode.Delete))SellSelected();
        }
    }
    bool RescueVisible=>match!=null&&match.RescueCharges[0]>0;
    void BeginBuilderSpell(string spell){if(spell=="rescue"&&!RescueVisible){Notice("Купите спасительный удар в магазине замка (F2)");return;}selectedItem=-1;chosen=-1;buildMenu=false;moveOrder=false;targetedSpell=spell;Notice(spell=="blink"?"Скачок: укажите точку ЛКМ":"Спасительный удар: укажите область ЛКМ");}
    void ExecuteBuilderSpell(Vector2 p){if(onlineMatch){try{NetworkCommand(PcServerReplica.BuilderSpell(targetedSpell,p));targetedSpell=null;}catch(ArgumentOutOfRangeException){Notice("Точка вне карты");}return;}bool ok=targetedSpell=="blink"?match.Blink(0,p):match.Rescue(0,p);Notice(ok?"Приказ выполнен":"Недоступно: проверьте область, заряд и перезарядку");if(ok)targetedSpell=null;}
    void RepairOrder(){if(onlineMatch){NetworkCommand(PcServerReplica.Command("repair"));selectedItem=chosen=-1;buildMenu=moveOrder=false;targetedSpell=null;return;}if(match.RepairCastle(0)){selectedItem=chosen=-1;buildMenu=moveOrder=false;targetedSpell=null;Notice("Рабочий отправляется ремонтировать замок");}else Notice(match.ChallengeBuilderRepairAllowed(0)?"Замок полностью восстановлен":"В испытании строитель не может ремонтировать замок");}
    void SetProductionPaused(bool paused){if(onlineMatch)NetworkCommand(PcServerReplica.PauseAll(paused));else match.PauseAll(0,paused);Notice(paused?"Выпуск бойцов остановлен":"Выпуск бойцов возобновлён");}
    void ToggleProduction(HumanMatch.Hut h){if(onlineMatch)NetworkCommand(PcServerReplica.BuildingAction("pause",h.id));else match.PauseHut(0,h.id,!h.paused);}
    void UpgradeSelected(int kind){var h=SelectedHut;if(h==null||kind<0||kind>=match.Lines.Length)return;if(onlineMatch){NetworkCommand(PcServerReplica.BuildingAction("upgrade",h.id,match.Lines[kind].building));return;}Notice(match.Upgrade(0,h.id,kind,out var error)?"Улучшено: "+match.Lines[kind].name:error);}
    void SellSelected(){var h=SelectedHut;if(h==null)return;if(onlineMatch){NetworkCommand(PcServerReplica.BuildingAction("sell",h.id));selectedHut=-1;return;}if(match.Sell(0,h.id)){selectedHut=-1;Notice(h.paid?"Здание продано: 20% начальной цены возвращено":"Стройка отменена: резерв освобождён");}}
    void UpdateSelectionRing() {
        UpdateGroupRings();
        var h=SelectedHut;var u=SelectedUnit;bool active=builderSelected||selectedCastle||h!=null||u!=null;
        selectedRing.SetActive(active&&page==Page.Match);if(active){selectedRing.transform.position=builderSelected?builders[0].transform.position+Vector3.up*.12f:World(selectedCastle?HumanMatch.CastlePosition(0):h!=null?h.position:u.position)+Vector3.up*.12f;selectedRing.transform.localScale=Vector3.one*(selectedCastle?5.8f:h!=null?2.2f:1);}
        if(!rangeRing){rangeRing=Ring("Selected ability radius",visuals.transform);}
        bool targeting=(!string.IsNullOrEmpty(targetedSpell)||selectedItem>=0)&&page==Page.Match&&!MouseOverHud()&&PointerOnTerrain(out _);
        rangeRing.SetActive((showRange&&h!=null||targeting)&&page==Page.Match);
        if(targeting&&PointerOnTerrain(out var hit)){rangeRing.transform.position=hit+Vector3.up*.2f;rangeRing.transform.localScale=Vector3.one*(selectedItem>=0?HumanMatch.F(match.ItemCatalog[match.Inventories[0].slots[selectedItem].id],"area",2):targetedSpell=="rescue"?1000f/30:2);}
        else if(showRange&&h!=null){rangeRing.transform.position=World(h.position)+Vector3.up*.2f;var l=match.Lines[h.kind];rangeRing.transform.localScale=Vector3.one*(l.building=="h010"?16.667f:l.buildingWeapons.FirstOrDefault()?.range??1);}
    }
    void RenderHumanEffects() {
        RenderOrcNets();
        foreach(var p in match.Pulses) {
            if(!ShowPulse036(p)||Art036.rendered.Contains(p.id)||options.hdTextures&&((reforgedFxLoaded&&reforgedFx.ContainsKey(PulseFx(p)??""))||p.fx=="wisp")){if(humanEffects.TryGetValue(p.id,out var hidden))hidden.gameObject.SetActive(false);continue;}
            if(humanEffects.TryGetValue(p.id,out var restored))restored.gameObject.SetActive(true);
            if(!humanEffects.TryGetValue(p.id,out var line)){line=new GameObject("Human spell "+p.text).AddComponent<LineRenderer>();line.transform.SetParent(visuals.transform);line.sharedMaterial=impactMaterial;line.positionCount=49;line.useWorldSpace=true;line.startWidth=line.endWidth=.09f;humanEffects[p.id]=line;}
            float radius=p.radius*Mathf.Lerp(.3f,1,Mathf.Clamp01(p.age/p.duration));
            for(int i=0;i<49;i++){float angle=i*Mathf.PI*2/48;line.SetPosition(i,World(p.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius)+Vector3.up*.3f);}
            var color=p.color;color.a*=1-Mathf.Clamp01(p.age/p.duration);var block=new MaterialPropertyBlock();block.SetColor("_Color",color);line.SetPropertyBlock(block);
        }
        foreach(var id in humanEffects.Keys.Where(id=>!match.Pulses.Any(p=>p.id==id)).ToArray()){Destroy(humanEffects[id].gameObject);humanEffects.Remove(id);}
    }
    void ClearHumanEffects(){ClearGroupSelection();humanEffects.Clear();netViews.Clear();netSizes.Clear();reforgedNets.Clear();reforgedAirNets.Clear();wispBursts.Clear();orcDecorModels.Clear();orcDecorAnchors.Clear();effectAnchors.Clear();rangeRing=null;selectedHut=selectedUnit=-1;targetedSpell=null;showRange=false;}
    Texture2D Extra(string name){int i=Array.IndexOf(extraCommandIds,name);return i>=0?HdIcon(extraCommandIcons[i]):CommandIcon(8);}
    // Native A03Y has an empty aart; share the original Bfro frost-attack icon.
    Texture2D AbilityArt(string id){var ultimateArt=UltimateAbility034(id);if(ultimateArt)return HdIcon(ultimateArt);if(id=="ANDB")return ContentResources.Load<Texture2D>("CombatType/attack-piercing");if(id=="A064")id="A03C";if(id=="A03Y")id="A03C";foreach(var pack in new[]{centaurPack,ultimatePack,nerubianPack,pandarenPack,elvenPack,nightPack,elementalPack,corruptedPack,undeadPack,mechPack,northernPack,chaosPack,naturePack,nagaPack,orcPack}){if(!pack)continue;int index=Array.IndexOf(pack.abilityIds,id);if(index>=0&&pack.abilityIcons[index])return HdIcon(pack.abilityIcons[index]);}int i=Array.IndexOf(abilityIds,id);return i>=0?HdIcon(abilityIcons[i]):null;}
    bool ArtButton(string id,Rect r,Texture2D art,string key,string tip,bool selected=false) {
        bool hover=r.Contains(HudPointer)&&GUI.enabled;
        GUI.BeginGroup(r);var local=new Rect(0,0,r.width,r.height);
        Fill(local,new Color(.025f,.025f,.015f));if(art)GUI.DrawTexture(local,art,ScaleMode.StretchToFill);
        if(selected)Border(local,new Color(1,.85f,.26f),2);
        if(hover){Border(local,new Color(1,.95f,.64f),2);hudTip=tip;}
        if(!string.IsNullOrEmpty(key)){
            var style=new GUIStyle(hudSmall){fontSize=11,alignment=TextAnchor.MiddleRight};
            float width=Mathf.Min(r.width-4,Mathf.Max(16,style.CalcSize(new GUIContent(key)).x+4));
            var badge=new Rect(r.width-width-2,r.height-17,width,15);Fill(badge,new Color(.02f,.02f,.01f,.9f));GUI.Label(badge,key,style);
        }
        GUI.EndGroup();
        return Button(id,r,"",false,iconStyle);
    }
    Texture2D productionToggleArt;
    Texture2D ProductionToggleArt() {
        if(productionToggleArt)return productionToggleArt;
        var art=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Production toggle",filterMode=FilterMode.Bilinear};
        for(int y=0;y<64;y++)for(int x=0;x<64;x++) {
            float dx=x-31.5f,dy=y-31.5f,r=Mathf.Sqrt(dx*dx+dy*dy);
            bool arc=r>=13&&r<=21&&((dy>2&&dx>-18)||(dy< -2&&dx<18));
            bool arrow=(dx>=10&&dx<=23&&dy>=-5&&dy<=8&&dy<=18-dx)||(dx<=-10&&dx>=-23&&dy>=-8&&dy<=5&&dy>=-18-dx);
            float shade=Mathf.Clamp01((dy+32)/64f);art.SetPixel(x,y,arc||arrow?new Color(.72f+.25f*shade,.03f+.12f*shade,.025f,1):Color.clear);
        }
        art.Apply();productionToggleArt=art;return art;
    }
    string Cost(HumanMatch.Line l,int team=0)=>match.GoldPrice(team,match.Kind(l.building))+" зол."+(l.woodCost>0?" · "+l.woodCost+" дер.":"")+(l.slots>0?" · 1 легенд.":"");
    void DrawHumanSelection() {
        if(selectedCastle){aspectInfoOpen=false;DrawCastleCard();return;}
        var h=SelectedHut;var u=SelectedUnit;string title=builderSelected?(matchRace==15?"Кочевник-строитель":matchRace==12?"Высший строитель":matchRace==13?"Рабочий роя":matchRace==14?"Пандарен-строитель":matchRace==1?"Батрак":matchRace==2?"Мурлок-рабочий":matchRace==3?"Энт":matchRace==4?"Батрак Хаоса":matchRace==5?"Северный рабочий":matchRace==11?"Эльф-рабочий":matchRace==10?"Ночная бегунья":matchRace==9?"Светлячок":matchRace==8?"Тень-строитель":matchRace==7?"Работник нежити":matchRace==6?"Гоблин-инженер":"Крестьянин"):h!=null?match.Lines[h.kind].name:u!=null?match.Lines[u.kind].unitName:"Нет выделения";
        if(!builderSelected&&u==null||selectedGroup.Count>1)aspectInfoOpen=false;
        if(builderSelected||h!=null||u!=null) {
            if(portraitTexture)GUI.DrawTexture(new Rect(348,590,109,93),portraitTexture);
            float hp=builderSelected?220:h!=null?h.hp:u.hp,maxHp=builderSelected?220:h!=null?h.maxHp:u.maxHp;
            Fill(new Rect(348,685,109,16),new Color(.07f,.14f,.03f));Fill(new Rect(350,687,105*Mathf.Clamp01(hp/maxHp),12),new Color(.2f,.75f,.08f));GUI.Label(new Rect(348,684,109,18),Mathf.CeilToInt(hp)+" / "+Mathf.CeilToInt(maxHp),new GUIStyle(hudSmall){alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}});
            if(!builderSelected)DrawManaBar(new Rect(348,703,109,13),h!=null?h.mana:u.mana,h!=null?HumanMatch.F(match.Lines[h.kind].buildingStats,"max_mana"):u.maxMana,true);
        }
        if(selectedGroup.Count>1)title="Группа: "+selectedGroup.Count;GUI.Label(new Rect(491,578,306,31),title,new GUIStyle(hudText){fontSize=title.Length>24?13:15,alignment=TextAnchor.MiddleCenter});
        
        if(selectedGroup.Count>1)DrawGroupCard();else if(builderSelected)DrawBuilderCard();else if(h!=null)DrawBuildingCard(h);else if(u!=null)DrawUnitCard(u);else GUI.Label(new Rect(504,630,285,55),Application.isMobilePlatform?"Коснитесь бойца или здания\nВыберите рабочего значком слева":"ЛКМ — выбрать бойца или здание\nF1 — вернуться к рабочему",hudSmall);
    }
    void DrawBuilderCard() {
        if(skillCardUnit!=-1){skillCardUnit=-1;aspectInfoOpen=false;}
        GUI.DrawTexture(new Rect(504,623,36,36),CombatIcon(true,"divine"),ScaleMode.ScaleToFit);GUI.Label(new Rect(550,622,245,42),"Броня: неуязвимость\nРабочий · "+RaceName,hudSmall);
        var b=match.Builders[0];var hut=b.jobs.Count>0?match.Huts.FirstOrDefault(v=>v.id==b.jobs[0]):null;
        if(hut!=null){GUI.Label(new Rect(504,665,286,21),match.Lines[hut.kind].name+" · очередь: "+b.jobs.Count,hudSmall);Fill(new Rect(504,685,279,5),new Color(.08f,.07f,.04f));Fill(new Rect(504,685,279*(1-hut.buildLeft/match.Lines[hut.kind].buildSeconds),5),new Color(.74f,.57f,.2f));}
        else GUI.Label(new Rect(504,665,286,23),Application.isMobilePlatform?"Коснитесь поля — идти":"ПКМ — идти · B — строительство",hudSmall);
        GUI.Label(new Rect(504,690,286,22),b.repairing?"Ремонт замка: 15 здоровья / сек.":b.pose=="Walk"?"Выполняет приказ движения":b.pose=="Stand Work"?"Строит здание":"Ожидает приказа",hudSmall);
        if(buildMenu) {
            int slot=0;foreach(int i in ConstructionPageKinds()){var l=match.Lines[i];string tip=l.name+" ("+l.hotkey+")\n"+Cost(l)+"\n"+(l.Producer?l.unitName+" · выпуск "+l.interval.ToString("F1")+" сек.":BuildingSummary(l.building));
                if(ArtButton("build."+i,Slot(slot%4,slot/4),HdIcon(MatchBuildingIcon(i)),l.hotkey,tip,chosen==i))Select(i);slot++;}
            if(ConstructionKinds().Length>11&&ArtButton("builder.next-page",Slot(2,2),CommandIcon(4),"Tab","Следующая страница построек · "+(constructionPage+1)+" / 2 (Tab)"))constructionPage=1-constructionPage;
            if(ArtButton("builder.cancel",Slot(3,2),CommandIcon(8),"Esc","Закрыть строительство")){selectedItem=-1;chosen=-1;buildMenu=false;}
            return;
        }
        if(Icon("builder.move",Slot(0,0),4,"M","Двигаться (M)",moveOrder)){chosen=-1;moveOrder=true;targetedSpell=null;}
        if(Icon("builder.stop",Slot(1,0),5,"S","Стоп (S)"))StopBuilder();
        if(Icon("builder.hold",Slot(2,0),7,"H","Удерживать позицию (H)"))StopBuilder();
        if(Icon("builder.build",Slot(0,2),6,"B","Строительство (B)")){selectedItem=-1;constructionPage=0;buildMenu=true;moveOrder=false;targetedSpell=null;}
        if(ArtButton("builder.blink",Slot(1,2),Extra("blink"),"D","Скачок (D)\nМгновенное перемещение в своей базе",targetedSpell=="blink"))BeginBuilderSpell("blink");
        if(ArtButton("builder.repair",Slot(1,1),Extra("repair"),"R","Ремонтировать замок (R)\n15 здоровья в секунду · бесплатно",match.Builders[0].repairing))RepairOrder();
        if(RescueVisible&&ArtButton("builder.rescue",Slot(3,2),Extra("rescue"),"V","Спасительный удар (V)\nЗарядов: "+match.RescueCharges[0]+" · готов через "+Mathf.CeilToInt(match.RescueCooldown[0])+" сек.",targetedSpell=="rescue"))BeginBuilderSpell("rescue");
        DrawPassiveAspect(0,Slot(2,2));
        bool productionPaused=match.Huts.Any(h=>h.team==0&&h.paid&&match.Lines[h.kind].Producer)&&!match.Huts.Any(h=>h.team==0&&h.paid&&match.Lines[h.kind].Producer&&!h.paused);
        if(ArtButton(productionPaused?"builder.resume-all":"builder.pause-all",Slot(0,1),ProductionToggleArt(),productionPaused?"O":"P",productionPaused?"Возобновить выпуск бойцов (O)":"Остановить выпуск бойцов (P)\nБашни и специальные здания продолжают работать"))SetProductionPaused(!productionPaused);
    }
    string BuildingSummary(string raw)=>raw=="pK01"?"Легендарная бочка · 180 урона, до 8 целей":raw=="pF01"?"Источник · глобальное лечение трёх бойцов":raw=="pG01"?"Боевой гонг · ускорение союзников":raw=="c01B"?"Легендарная песчаная буря · замедление и урон":raw=="c00B"||raw=="c01B"?"Зыбучие пески · замедление наземных врагов":raw=="c009"?"Походные припасы · лечение бойцов в своей базе":raw=="h059"?"Благодать · возвращение павших":raw=="h00Z"?"Сглаз · превращение в животное":raw=="h014"?"Башня · до пяти целей":raw=="h005"?"Обелиск света · глобальный снаряд":raw=="h07H"?"Обелиск чащи · постоянное благословение":raw=="h08P"?"Лунные заряды для защиты армии":raw=="h07I"?"Звездопад · неподвижная область":raw=="h073"?"Фонарь Элуны · сжигание маны":raw=="h05Z"?"Книга стихий · выберите вид дождя улучшением":raw=="h06E"?"Метеоритный дождь по летунам":raw=="h06F"?"Буран или гроза по наземным врагам":raw=="h060"?"Постоянное зачарование союзных бойцов":raw=="h062"?"Генератор маны · радиус 500":raw=="h063"?"Связь стихий · лечение при гибели союзника":raw=="h061"?"Страж стихий · камень с оглушением":raw=="h04T"?"Взор порчи · глобальное снижение брони":raw=="h04R"?"Тюрьма щупалец":raw=="h04K"?"Касание боли":raw=="h01P"||raw=="h056"?"Поднятие скелетов из тел":raw=="h00A"?"Внезапная смерть":raw=="h01Q"?"Разложение · глобальная аура":raw=="h01O"?"Башня душ · похищение души":raw=="h09T"?"Энергостанция · усиление и очищение":raw=="h100"?"Ремонтная станция · броня и регенерация":raw=="h06J"||raw=="h05R"?"Магические щиты для союзных бойцов":raw=="h05L"?"Энергобашня · цепная молния":raw=="h05I"?"Дар Хаоса · случайный исход":raw=="h01M"||raw=="h01W"||raw=="h01X"?"Стирание всех врагов одного типа":raw=="h01F"?"Извержение · огонь по области":raw=="h01E"?"Кулак ада · отключение здания":raw=="h010"?"Святая сила · +50% скорости атаки":raw=="h05G"?"17% шанс дополнительного бойца":raw=="h008"?"Особая постройка · доход 1,4%":raw=="h001"?"Обстрел базы врага и горючая смесь":raw=="h02R"?"Ритуальное благословение":raw=="h02O"?"Глобальный стазис":raw=="h02N"?"Кислотные бомбы":raw=="o000"?"Башня · яд по воздуху и земле":raw=="n02D"?"Древний защитник · оглушение":raw=="h02A"?"Вихрь · магический урон и замедление":raw=="h02C"?"Астрал · глобальное изгнание":raw=="h02D"?"Зов природы · шесть духов":"Оборонительная башня";
    string[] BuildingCardAbilities(HumanMatch.Hut h) {
        var line=match.Lines[h.kind];var ids=line.abilities.AsEnumerable();if(line.building=="h061")ids=ids.Concat(new[]{"A0DC"});if(line.building=="h09T")ids=ids.Concat(new[]{"A09S"});
        return ids.Distinct().Where(id=>{var cfg=match.Abilities[id];return cfg!=null&&ContextAbilityVisible(line.building,id,cfg)&&(bool?)cfg["hide_card"]!=true&&((string)cfg["kind"]!="marker"||(bool?)cfg["card_description"]==true||id=="A09S")&&AbilityArt(id);}).ToArray();
    }
    void DrawBuildingCard(HumanMatch.Hut h) {
        var l=match.Lines[h.kind];DrawBuildingCombat(h);
        string info=BuildingStatus(h);
        GUI.Label(new Rect(504,665,286,22),info,hudSmall);GUI.Label(new Rect(504,690,286,22),"Вложено: "+h.spent+" · Доход: "+h.income.ToString("F2")+" / 10 с",new GUIStyle(hudSmall){fontSize=11});
        if(h.paid&&l.Producer){Fill(new Rect(504,686,279,4),new Color(.1f,.1f,.05f));Fill(new Rect(504,686,279*Mathf.Clamp01(1-h.spawnLeft/match.Interval(h)),4),new Color(.75f,.6f,.2f));}
        int skill=0;foreach(var id in BuildingCardAbilities(h)){DrawSpellIcon("building.ability."+id,Slot(2+skill%2,skill/2),id,RaceSkillTip(id,match.Abilities[id]),null,h);if(++skill>=4)break;}
        if(h.team!=0)return;
        if(!h.paid){if(ArtButton("building.resume",Slot(0,0),Extra("repair"),"","Продолжить незавершённое строительство")){match.ResumeBuilding(0,h.id);FocusBuilder();}if(ArtButton("building.cancel",Slot(3,2),CommandIcon(8),"Del","Отменить строительство\nОсвободить клетку и резерв золота, дерева и легендарного места"))SellSelected();return;}
        for(int i=0;i<l.upgrades.Length;i++){int k=match.Kind(l.upgrades[i]);var next=match.Lines[k];string tip="Улучшить: "+next.name+"\n"+Cost(next,h.team)+(next.Producer?"\nВыпуск после улучшения: "+(15+(h.spent+next.cost)/20f).ToString("F1")+" сек.":"\n"+BuildingSummary(next.building)+"\nМана: +"+HumanMatch.F(next.buildingStats,"mana_regen").ToString("F2")+" / с");if(ArtButton("upgrade."+next.building,Slot(i,0),HdIcon(MatchBuildingIcon(k)),i==0?"U":"",tip))UpgradeSelected(k);}
        if(l.Producer&&ArtButton("building.pause",Slot(0,1),Extra(h.paused?"resume_all":"pause"),"P",h.paused?"Возобновить производство":"Приостановить производство",h.paused))ToggleProduction(h);
        if(l.abilities.Contains("A09A")&&ArtButton("building.range",Slot(1,1),Extra("attack"),"","Показать радиус действия",showRange))showRange=!showRange;
        if(ArtButton("building.sell",Slot(3,2),Extra("sell"),"Del","Продать здание\nВернётся "+(int)(h.baseCost*.2f)+" золота (20% начальной цены)"))SellSelected();
    }
    string BuildingStatus(HumanMatch.Hut h) {
        var l=match.Lines[h.kind];
        if(!h.paid)return (match.Builders[h.team].jobs.Contains(h.id)?"Стройка: ":"Стройка прервана: ")+Mathf.CeilToInt(h.buildLeft)+" сек.";
        if(match.FrostDisabled(h)>0)return "Заморожено: "+Mathf.CeilToInt(match.FrostDisabled(h))+" с";if(match.HellDisabled(h)>0)return "Кулак ада: отключено "+Mathf.CeilToInt(match.HellDisabled(h))+" с";if(h.paused)return "Производство остановлено";
        if(l.Producer)return h.spawnLeft<=0&&match.ArmyCount(h.team)>=HumanMatch.ArmyLimit?"Ждёт места в армии ("+match.ArmyCount(h.team)+"/"+HumanMatch.ArmyLimit+")":l.unitName+": "+Mathf.CeilToInt(h.spawnLeft)+" сек.";
        if(HumanMatch.F(l.buildingStats,"max_mana")>0||new[]{"h05I","h01M","h01W","h01X","h01F","h01E","h047","h048","h03L","h03O","h06J","h05R","h01P","h056","h00A","h04K","h04R"}.Contains(l.building))return "Мана: "+Mathf.FloorToInt(h.mana)+" / "+Mathf.FloorToInt(HumanMatch.F(l.buildingStats,"max_mana"))+" · +"+HutManaRegen(h).ToString("0.##")+" / с";
        if(new[]{"h02R","h02O","h02N","h02A","h02C","h02D"}.Contains(l.building))return BuildingSummary(l.building)+" · мана "+Mathf.FloorToInt(h.mana)+" / "+Mathf.FloorToInt(HumanMatch.F(l.buildingStats,"max_mana"));
        return l.building=="h010"?"Святая сила · мана "+Mathf.FloorToInt(h.mana)+" / 10":BuildingSummary(l.building);
    }
    void DrawUnitCard(HumanMatch.Unit u) {
        var l=match.Lines[u.kind];var weapons=CardWeapons(u);var attacks=CardAttacks(weapons);
        float damage=((u.attackOverride>=0?u.attackOverride:u.elementalWeapons?.FirstOrDefault()?.damage??l.damage)+u.abilities.Sum(id=>(string)match.Abilities[id]?["kind"]=="self_passive"?HumanMatch.F(match.Abilities[id],"damage_bonus"):0)+match.ChaosSealBonus(u)+match.NightFlat(u))*Mathf.Max(0,1+match.Stat(u,"damage"))*Mathf.Max(0,1+u.auraDamage+u.auraDamageNegative)*match.InventoryDamage(u)*match.ElvenWeaponFactor(u.team,false);
        DrawCombatStat(504,false,attacks,attacks.Length==0?"":"Урон: "+NetworkCardStat(u.id,"damage",damage).ToString("F0"),AttackTable(weapons));
        DrawCombatStat(650,true,new[]{l.armorType},"Броня: "+NetworkCardStat(u.id,"armor",u.armor+match.Stat(u,"armor")).ToString("F0"),ArmorTable(l.armorType));
        GUI.Label(new Rect(504,665,286,22),u.maxMana>0?"Мана: "+Mathf.FloorToInt(u.mana)+" / "+Mathf.FloorToInt(u.maxMana)+" · +"+UnitManaRegen(u).ToString("0.##")+" / с":"Автоматический бой",hudSmall);
        GUI.Label(new Rect(504,690,286,22),match.PandaStagger(u)>0?"Отложенный урон: "+match.PandaStagger(u).ToString("F0"):match.Rooted(u)?(u.effects.Any(e=>e.left>0&&e.ground)?"Обездвижен и приземлён":"Обездвижен"):u.effects.Any(e=>e.id=="A02I")?"Безумие: "+Mathf.CeilToInt(u.effects.First(e=>e.id=="A02I").left)+" сек.":u.effects.Any(e=>e.id=="A02M")?"Духовное пламя: "+Mathf.CeilToInt(u.effects.First(e=>e.id=="A02M").left)+" сек.":u.stun>0?"Оглушён":u.nightInvisible?"Невидимость · первый удар +"+u.nightBonus:u.nightFade>0?"Уход в тень: "+u.nightFade.ToString("F1")+" с":u.nightVengeance>0?"Месть леса: "+u.nightVengeance+" / 10":u.effects.Count>0?"Эффектов: "+u.effects.Count:match.ChaosSealBonus(u)>0?"Печать демона: "+u.chaosSeal+" / 10":u.warlockPhase==1?"Отступает после взрыва":u.warlockPhase==2?"Удерживает позицию":u.abilities.Contains("CT01")?"Натиск: "+Mathf.RoundToInt(match.CentaurChargeProgress(u)*100)+"%":(u.team==0?RaceName:BotRaceName),hudSmall);
        DrawUnitSkillGrid(u);
    }
    static readonly string[] ArmorTypes={"unarmored","light","medium","heavy","fortified","normal","hero","divine"};
    static readonly string[] ArmorLabels={"Без брони","Лёгкая","Средняя","Тяжёлая","Укреплённая","Обычная","Геройская","Божественная"};
    static readonly string[] AttackTypes={"normal","pierce","siege","magic","hero","spells","chaos"};
    static readonly string[] AttackLabels={"Обычная","Колющая","Осадная","Магическая","Геройская","Заклинания","Хаос"};
    string AttackLabel(string id){int i=Array.IndexOf(AttackTypes,id);return i<0?id:AttackLabels[i];}
    string ArmorLabel(string id){int i=Array.IndexOf(ArmorTypes,id);return i<0?id:ArmorLabels[i];}
    string AttackTable(HumanMatch.Line l)=>AttackTable(l.weapons);
    string AttackTable(System.Collections.Generic.IEnumerable<HumanMatch.Weapon> weapons){var attacks=CardAttacks(weapons);if(attacks.Length==0)return "Нет атаки";
        var b=new System.Text.StringBuilder("Атака: "+string.Join(" / ",attacks.Select(AttackLabel))+"\nУрон по типу брони:\n");
        for(int i=0;i<ArmorTypes.Length;i++)b.Append(ArmorLabels[i]).Append(":  ").Append(string.Join(" / ",attacks.Select(a=>Mathf.RoundToInt(100*match.Multiplier(a,ArmorTypes[i]))+"%"))).Append('\n');
        return b.Append("Без учёта числовой брони.").ToString();}
    string ArmorTable(HumanMatch.Line l)=>ArmorTable(l.armorType);
    string ArmorTable(string armor){var b=new System.Text.StringBuilder("Защита: "+ArmorLabel(armor)+"\nПолучаемый урон по типу атаки:\n");
        for(int i=0;i<AttackTypes.Length;i++)b.Append(AttackLabels[i]).Append(":  ").Append(Mathf.RoundToInt(100*match.Multiplier(AttackTypes[i],armor))).Append("%\n");
        return b.Append("Без учёта числовой брони.").ToString();}
    GameObject PortraitPrefab(out string raw) {
        raw="";if(builderSelected){raw=BuilderRaw;return BuilderModels[0];}
        var h=SelectedHut;if(h!=null){raw=match.Lines[h.kind].building;return RaceModel(h.team,h.kind,true);}
        var u=SelectedUnit;if(u!=null){raw=match.Lines[u.kind].unit;return RaceModel(u.team,u.kind,false);}return null;
    }
}
}

