using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    GameObject guideGarden,guideStage;readonly List<PcModel> guideBuilders=new List<PcModel>();
    int guideRace=-1,guideEntry=-1,guideActorIndex;HumanMatch guideAuthority;JObject guideCatalog;Vector2 guideGridScroll,guideTextScroll;string guideQuery="",guideDescription="";bool guideUnit=true,guideSpin=true;float guideOpened,guideAngle;
    readonly Stack<int> guideHistory=new Stack<int>();PcModel guideModel;Camera guideCamera;RenderTexture guideTexture;int guideMainMask;bool guideLayerReserved;
    readonly Vector2 guideClearing=new Vector2(-4600f/30,1450f/30);
    string GuideRaceName(int race)=>AllRaceNames[RaceOfUnity(race)];
    void OpenGuide(){
        MainMenu();page=Page.Guide;guideRace=guideEntry=-1;guideOpened=0;guideQuery="";guideHistory.Clear();
        if(!guideGarden){guideGarden=new GameObject("Guide clearing — classic builders");
            for(int race=0;race<MobileContent.RaceResources.Length;race++){
                if(!RaceStaged(race))continue;
                var prefab=MenuBuilder(race);var model=Instantiate(prefab?prefab:FirstModel(builderModels),guideGarden.transform).GetComponent<PcModel>();
                int column=race%5,row=race/5;if(race==15){column=3;row=-1;}if(race==PepeContent.Race){column=0;row=3;}if(column==4){column=row;row=-1;}Vector2 p=guideClearing+new Vector2((column-2)*20,(row-1)*13);var ground=World(p);model.transform.position=ground;model.Face(Vector2.up,true);model.Play("Stand");model.SamplePose();
                var bounds=model.BodyBounds();float scale=4.6f/Mathf.Max(.1f,bounds.size.y);model.transform.localScale*=scale;model.transform.position=ground+Vector3.up*(ground.y-bounds.min.y)*scale;guideBuilders.Add(model);
            }
        }
        guideGarden.SetActive(true);if(guideStage)guideStage.SetActive(false);if(guideCamera)guideCamera.enabled=false;guideOpened=0;controls.FocusWorld(World(guideClearing)+Vector3.up*75,155);Debug.Log("CF_GUIDE_OPEN builders="+guideBuilders.Count);
    }
    void CloseGuide(){
        if(guideCamera)guideCamera.enabled=false;
        if(guideGarden){guideGarden.SetActive(false);Destroy(guideGarden);}
        if(guideStage){guideStage.SetActive(false);Destroy(guideStage);}
        if(guideTexture){guideTexture.Release();Destroy(guideTexture);}
        if(guideLayerReserved&&Camera.main)Camera.main.cullingMask=guideMainMask;
        guideGarden=guideStage=null;guideCamera=null;guideModel=null;guideTexture=null;guideLayerReserved=false;
        guideBuilders.Clear();guideCatalog=null;guideAuthority=null;
    }
    void UpdateGuide(){
        if(page!=Page.Guide){CloseGuide();return;}if(guideRace<0){guideOpened=Mathf.Min(1.25f,guideOpened+Mathf.Min(Time.unscaledDeltaTime,.05f));float t=guideOpened/1.25f;float ease=t*t*(3-2*t);controls.FocusWorld(World(guideClearing)+Vector3.up*Mathf.Lerp(75,0,ease),Mathf.Lerp(155,80,ease));}
        if(guideModel&&guideEntry>=0){if(guideSpin)guideAngle+=Time.unscaledDeltaTime*18;guideModel.transform.localRotation=Quaternion.Euler(0,guideAngle,0);}
    }
    System.Collections.IEnumerator OpenBlackHandGuide(){yield return BlackHandArt.Open();if(page==Page.Guide)GuideChooseRace(BlackHand.Race);}
    void GuideChooseRace(int race){
        if(race==BlackHand.Race&&!BlackHandArt.Ready){StartCoroutine(OpenBlackHandGuide());return;}
        if(MobileContent.Active&&race!=0&&race!=BlackHand.Race&&!Pack(race)){StartCoroutine(PrepareMobileGuide(race));return;}
        if(!MobileContent.Active){
            if(DiscardRaceAssets(race)){ClearRacePrefabCaches();SweepRaceAssets();}
            LoadDesktopRace(race);
        }
        guideRace=race;guideEntry=-1;guideGridScroll=guideTextScroll=Vector2.zero;guideQuery="";guideHistory.Clear();guideGarden.SetActive(false);
        guideCatalog=JObject.Parse(RaceCatalog(race));guideAuthority=new HumanMatch(guideCatalog);if(guideCamera)guideCamera.enabled=false;if(guideStage)guideStage.SetActive(false);
    }
    IEnumerable<int> GuideEntries()=>Enumerable.Range(0,guideAuthority.Lines.Length).Where(i=>guideAuthority.Lines[i].category!="summon"&&guideAuthority.Lines[i].category!="helper"&&guideAuthority.Lines[i].cost>0&&((guideAuthority.Lines[i].name+" "+guideAuthority.Lines[i].unitName).IndexOf(guideQuery,StringComparison.OrdinalIgnoreCase)>=0));
    GameObject GuidePrefab(int kind,bool unit)=>CatalogModel(guideRace,0,kind,guideAuthority.Lines[kind].building,!unit);
    int[] GuideUnitKinds(int kind){if(GuidePrefab(kind,true))return new[]{kind};if(guideAuthority.Lines[kind].unit=="e00E")return Enumerable.Range(0,guideAuthority.Lines.Length).Where(k=>new[]{"n02K","n02L"}.Contains(guideAuthority.Lines[k].unit)&&GuidePrefab(k,true)).ToArray();return new int[0];}
    int GuideActorKind=>guideUnit?GuideUnitKinds(guideEntry)[guideActorIndex]:guideEntry;
    Texture2D GuideIcon(int kind)=>HdIcon(CatalogBuildingIcon(guideRace,kind,guideAuthority.Lines[kind].building));
    void GuideSelectEntry(int kind,bool remember=false){if(remember&&guideEntry>=0)guideHistory.Push(guideEntry);guideEntry=kind;guideTextScroll=Vector2.zero;guideActorIndex=0;guideUnit=GuideUnitKinds(kind).Length>0;GuideRefreshPreview();}
    void GuideBack(){
        if(guideEntry>=0&&guideHistory.Count>0){int previous=guideHistory.Pop();GuideSelectEntry(previous);return;}
        if(guideEntry>=0){guideEntry=-1;if(guideCamera)guideCamera.enabled=false;if(guideStage)guideStage.SetActive(false);return;}
        if(guideRace>=0){guideRace=-1;guideOpened=1.25f;if(guideModel){Destroy(guideModel.gameObject);guideModel=null;}guideCatalog=null;guideAuthority=null;guideGarden.SetActive(true);ReleaseIdleRaceAssets(true);return;}MainMenu();
    }
    void GuideRefreshPreview(){
        if(guideModel){guideModel.gameObject.SetActive(false);Destroy(guideModel.gameObject);}if(!guideStage)guideStage=new GameObject("Guide isolated preview");guideStage.SetActive(true);guideStage.transform.position=new Vector3(0,-5000,0);
        if(!guideTexture){guideTexture=new RenderTexture(640,560,16,RenderTextureFormat.ARGB32){name="Guide live classic model",antiAliasing=1};guideTexture.Create();}
        if(!guideCamera){var obj=new GameObject("Guide preview camera");obj.transform.SetParent(guideStage.transform,false);guideCamera=obj.AddComponent<Camera>();guideCamera.clearFlags=CameraClearFlags.SolidColor;guideCamera.backgroundColor=new Color(.035f,.052f,.06f);guideCamera.targetTexture=guideTexture;guideCamera.cullingMask=1<<30;guideCamera.fieldOfView=32;guideCamera.nearClipPlane=.05f;guideCamera.farClipPlane=2000;guideCamera.allowHDR=false;guideCamera.allowMSAA=false;
            var lightObj=new GameObject("Guide soft key light");lightObj.transform.SetParent(guideStage.transform,false);var light=lightObj.AddComponent<Light>();light.type=LightType.Directional;light.intensity=.8f;light.shadows=LightShadows.None;light.cullingMask=1<<30;lightObj.transform.rotation=Quaternion.Euler(35,-35,0);
            if(!guideLayerReserved){guideMainMask=Camera.main.cullingMask;Camera.main.cullingMask&=~(1<<30);guideLayerReserved=true;}
        }
        guideModel=Instantiate(GuidePrefab(GuideActorKind,guideUnit),guideStage.transform).GetComponent<PcModel>();guideModel.transform.localPosition=Vector3.zero;guideModel.transform.localRotation=Quaternion.identity;guideModel.Play("Stand");guideModel.SamplePose();
        guideAngle=guideRace==PepeContent.Race?142:guideRace==15?180:0;
        guideModel.transform.localRotation=Quaternion.Euler(0,guideAngle,0);guideModel.SamplePose();
        foreach(var t in guideModel.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
        var bounds=guideModel.BodyBounds();Vector3 anchor=guideStage.transform.position;guideModel.transform.position-=bounds.center-anchor;
        float radius=Mathf.Max(.4f,bounds.extents.magnitude);
        bool wingedRider=guideRace==PepeContent.Race&&(guideModel.raw=="pU04"||guideModel.raw=="pU17");
        float distance=radius/Mathf.Sin(guideCamera.fieldOfView*Mathf.Deg2Rad*.5f)*(wingedRider?1.75f:guideRace==PepeContent.Race?1.45f:1.12f);
        guideCamera.transform.position=anchor+new Vector3(0,wingedRider?distance*.38f:radius*.14f,-distance);
        guideCamera.transform.LookAt(anchor);guideCamera.farClipPlane=Mathf.Max(50,distance+radius*4);guideCamera.enabled=true;
        guideDescription=GuideEntryText();
    }
    int GuideInvestment(int kind,HashSet<int> seen=null){seen=seen??new HashSet<int>();if(!seen.Add(kind))return 0;var line=guideAuthority.Lines[kind];int parent=Array.FindIndex(guideAuthority.Lines,l=>l.upgrades.Contains(line.building));return line.cost+(parent>=0?GuideInvestment(parent,seen):0);}
    JToken GuideSkillConfig(int kind,string id,JToken cfg){
        var ranks=cfg?["ranks"] as JArray;if(ranks==null||ranks.Count==0)return cfg;
        int level=(int?)guideAuthority.Lines[kind].stats["ability_ranks"]?[id]??1;
        return ranks[Mathf.Clamp(level-1,0,ranks.Count-1)];
    }
    string GuideEntryText(){
        var line=guideAuthority.Lines[guideEntry];var actor=guideAuthority.Lines[GuideActorKind];var stats=guideUnit?actor.stats:line.buildingStats;var text=new StringBuilder();
        text.Append(guideUnit?actor.unitName:line.name).Append("\n\n");
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
        EnsureRaceStyles();AtlasSurface(new Rect(16,18,1248,76));
        if(Button("guide.back",new Rect(30,34,202,43),guideRace<0?"← В меню":guideEntry<0?"← К строителям":"← Назад"))GuideBack();
        Text(260,28,760,51,guideRace<0?"Учебник Castle Fight":GuideRaceName(guideRace),atlasTitle);
        if(Button("guide.menu",new Rect(1088,34,162,43),"Главное меню"))MainMenu();
        if(guideRace<0){
            Text(280,108,780,32,"Выберите строителя — откроется книга его расы",label);
            for(int race=0;race<guideBuilders.Count;race++){var model=guideBuilders[race];var p=Camera.main.WorldToScreenPoint(model.transform.position+Vector3.up*.2f);if(p.z<0)continue;Vector2 at=GuiPoint(new Vector2(p.x,p.y));
                if(Button("guide.race."+race,new Rect(at.x-89,at.y+12,178,39),GuideRaceName(race),false,new GUIStyle(menuButton){fontSize=14,wordWrap=true,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(4,4,2,2)})){GuideChooseRace(race);break;}
                var screen=Camera.main.WorldToScreenPoint(model.transform.position+Vector3.up*2.3f);var hit=GuiPoint(new Vector2(screen.x,screen.y));if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&new Rect(hit.x-32,hit.y-44,64,88).Contains(Event.current.mousePosition)){GuideChooseRace(race);Event.current.Use();break;}
            }
            Fill(new Rect(300,655,680,38),new Color(.025f,.04f,.045f,.85f));Text(320,659,640,28,guideBuilders.Count+" рас · классические модели · характеристики и улучшения",small);return;
        }
        if(guideEntry<0){DrawGuideGrid();return;}
        DrawGuideEntry();
    }
    void DrawGuideGrid(){
        AtlasSurface(new Rect(24,112,1232,584));Text(42,124,400,36,"Постройки и башни",raceTitleStyle);
        Text(680,128,100,30,"Поиск",small);guideQuery=GUI.TextField(new Rect(780,125,452,34),guideQuery,new GUIStyle(GUI.skin.textField){font=label.font,fontSize=18});
        var entries=GuideEntries().ToArray();guideGridScroll=GUI.BeginScrollView(new Rect(40,176,1200,504),guideGridScroll,new Rect(0,0,1176,Mathf.Ceil(entries.Length/6f)*153));
        for(int i=0;i<entries.Length;i++){int kind=entries[i];var line=guideAuthority.Lines[kind];float x=i%6*196,y=i/6*153;var card=new Rect(x,y,184,141);
            bool click=Button("guide.entry."+line.building,card,"");var icon=GuideIcon(kind);if(icon)GUI.DrawTexture(new Rect(x+62,y+9,60,60),icon,ScaleMode.ScaleToFit);
            GUI.Label(new Rect(x+8,y+73,168,41),line.name,new GUIStyle(small){alignment=TextAnchor.UpperCenter,wordWrap=true});GUI.Label(new Rect(x+8,y+117,168,20),line.cost+" зол."+(line.woodCost>0?" · "+line.woodCost+" дер.":""),new GUIStyle(small){alignment=TextAnchor.MiddleCenter});if(click){GuideSelectEntry(kind);break;}
        }
        GUI.EndScrollView();if(entries.Length==0)Text(64,230,1000,40,"По этому запросу ничего не найдено.",label);
    }
    void DrawGuideEntry(){
        var line=guideAuthority.Lines[guideEntry];AtlasSurface(new Rect(24,112,604,584));Text(44,128,564,42,line.name,raceTitleStyle);Text(44,173,564,27,guideUnit?"БОЕЦ  /  "+guideAuthority.Lines[GuideActorKind].unitName:"ПОСТРОЙКА",small);
        var descStyle=new GUIStyle(label){wordWrap=true,fontSize=18};float height=descStyle.CalcHeight(new GUIContent(guideDescription),536)+24;
        guideTextScroll=GUI.BeginScrollView(new Rect(44,212,564,390),guideTextScroll,new Rect(0,0,536,height));GUI.Label(new Rect(0,0,536,height),guideDescription,descStyle);GUI.EndScrollView();
        int n=0;foreach(string raw in line.upgrades){int kind=guideAuthority.Kind(raw);if(kind<0)continue;if(Button("guide.upgrade."+raw,new Rect(44+n*188,626,178,46),"Улучшить → "+(n+1),true))GuideSelectEntry(kind,true);n++;}
        if(n==0)Text(44,632,540,30,"Последняя ступень этой ветки",small);
        var view=new Rect(648,112,608,500);GUI.DrawTexture(view,guideTexture,ScaleMode.StretchToFill);Border(view,new Color(.36f,.31f,.22f));
        Text(674,124,560,32,guideUnit?guideAuthority.Lines[GuideActorKind].unitName:line.name,label);Text(674,570,560,28,"Потяните мышью, чтобы повернуть модель",small);
        if(Event.current.type==EventType.MouseDrag&&view.Contains(Event.current.mousePosition)){guideSpin=false;guideAngle+=Event.current.delta.x*.6f;Event.current.Use();}
        var actors=GuideUnitKinds(guideEntry);GUI.enabled=actors.Length>0;if(Button("guide.unit",new Rect(648,626,190,46),actors.Length>1?"Боец "+(guideActorIndex+1)+" / "+actors.Length:"Боец",guideUnit)){guideActorIndex=guideUnit?(guideActorIndex+1)%actors.Length:0;guideUnit=true;GuideRefreshPreview();}GUI.enabled=true;
        if(Button("guide.building",new Rect(848,626,190,46),"Постройка",!guideUnit)){guideUnit=false;GuideRefreshPreview();}
        if(Button("guide.spin",new Rect(1048,626,208,46),guideSpin?"Пауза вращения":"Вращать модель"))guideSpin=!guideSpin;
    }
    void DisposeGuide(){CloseGuide();}
}
}
