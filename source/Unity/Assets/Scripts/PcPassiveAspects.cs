using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    int aspectInfoTeam,skillCardUnit=-1,skillCardPage,passiveAspectFinger=-1;Vector2 passiveAspectScroll;
    void UpdatePassiveAspectTouch(){
        if(!aspectInfoOpen||page!=Page.Match){passiveAspectFinger=-1;return;}
        foreach(var touch in Input.touches){var p=GuiPoint(touch.position);if(touch.phase==TouchPhase.Began&&new Rect(PassiveAspectPanel.x,PassiveAspectPanel.y+103,410,205).Contains(p))passiveAspectFinger=touch.fingerId;
            if(touch.fingerId!=passiveAspectFinger)continue;if(touch.phase==TouchPhase.Moved)passiveAspectScroll.y=Mathf.Max(0,passiveAspectScroll.y+touch.deltaPosition.y/guiScale);if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled)passiveAspectFinger=-1;
        }
    }
    int AspectPassiveChoice(int team){
        if(match==null||team<0||team>1||match.Finished)return -1;
        int race=team==0?matchRace:matchBotRace,index=match.Aspects[team];
        return index>=0&&index<AspectCount(race)?index:-1;
    }
    string PassiveAspectTip(int team){int choice=AspectPassiveChoice(team),race=team==0?matchRace:matchBotRace;return choice<0?"":AspectField(race,choice,"title")+"\nАспект расы · пассивно\n"+AspectField(race,choice,"description");}
    void DrawPassiveAspect(int team,Rect rect){
        int choice=AspectPassiveChoice(team);if(choice<0)return;int race=team==0?matchRace:matchBotRace;
        string tip=PassiveAspectTip(team);if(ArtButton("ability.aspect."+team,rect,AspectIcon(race,choice),"",tip,aspectInfoOpen&&aspectInfoTeam==team)){aspectInfoOpen=!aspectInfoOpen||aspectInfoTeam!=team;aspectInfoTeam=team;passiveAspectScroll=Vector2.zero;}
        if(aspectInfoOpen&&aspectInfoTeam==team&&hudTip==tip)hudTip=null;
        // A passive has no hotkey, resource cost or cooldown overlay.
        Border(new Rect(rect.x+2,rect.y+2,rect.width-4,rect.height-4),new Color(.58f,.49f,.29f),1);
    }
    Rect PassiveAspectPanel=>new Rect(700,148,410,326);
    void DrawPassiveAspectInfo(){
        if(!aspectInfoOpen||AspectPassiveChoice(aspectInfoTeam)<0||!string.IsNullOrEmpty(hudTip))return;
        EnsureAspectStyles();var rect=PassiveAspectPanel;StoneFrame(rect);int race=aspectInfoTeam==0?matchRace:matchBotRace,index=AspectPassiveChoice(aspectInfoTeam);
        var titleStyle=new GUIStyle(hudTitle){fontSize=18,wordWrap=true};
        GUI.Label(new Rect(rect.x+16,rect.y+12,330,52),AspectField(race,index,"title"),titleStyle);
        if(Button("ability.aspect.close",new Rect(rect.xMax-44,rect.y+10,30,30),"X",false,new GUIStyle(button){padding=new RectOffset(0,0,0,0),alignment=TextAnchor.MiddleCenter,fontSize=16})){aspectInfoOpen=false;return;}
        GUI.Label(new Rect(rect.x+16,rect.y+65,375,28),(aspectInfoTeam==0?"Ваш аспект":"Аспект соперника")+" · пассивно",hudSmall);
        var style=new GUIStyle(label){fontSize=15,wordWrap=true,padding=new RectOffset(0,0,0,0)};string text=AspectField(race,index,"description");float height=Mathf.Max(200,style.CalcHeight(new GUIContent(text),352));
        passiveAspectScroll=GUI.BeginScrollView(new Rect(rect.x+16,rect.y+103,378,205),passiveAspectScroll,new Rect(0,0,352,height));GUI.Label(new Rect(0,0,352,height),text,style);GUI.EndScrollView();
    }
    bool ElementalLinkReady(HumanMatch.Unit unit)=>match.Huts.Any(h=>h.team==unit.team&&h.hp>0&&h.paid&&match.Lines[h.kind].building=="h063");
    string UnitSkillTip(HumanMatch.Unit unit,string id,Newtonsoft.Json.Linq.JToken cfg){
        string tip=ContextSkillTip(match.Lines[unit.kind].unit,id,UnitSkillConfig(unit,id,cfg));if(id=="A0F5")tip+="\n\n"+(ElementalLinkReady(unit)?"Действует: союзная Связь стихий построена.":"Не действует: постройте здание «Связь стихий».");return tip;
    }
    bool VisibleUnitAbility(string id){var cfg=match.Abilities[id];return !((string)cfg?["kind"]=="marker"&&id!="A07J"&&(bool?)cfg?["card_description"]!=true||(bool?)cfg?["hide_card"]==true||(string)cfg?["kind"]=="native_helper");}
    void DrawUnitSkillGrid(HumanMatch.Unit unit){
        if(skillCardUnit!=unit.id){skillCardUnit=unit.id;skillCardPage=0;aspectInfoOpen=false;}
        var ids=UnitCardAbilities(unit).Where(VisibleUnitAbility).ToArray();bool aspect=AspectPassiveChoice(unit.team)>=0;
        int capacity=aspect?11:12,pageSize=ids.Length>capacity?capacity-1:capacity,pages=Mathf.Max(1,Mathf.CeilToInt(ids.Length/(float)pageSize));skillCardPage=Mathf.Clamp(skillCardPage,0,pages-1);
        int slot=0;foreach(string id in ids.Skip(skillCardPage*pageSize).Take(pageSize)){
            var cfg=match.Abilities[id];var color=GUI.color;if(id=="A0F5"&&!ElementalLinkReady(unit))GUI.color=new Color(color.r*.45f,color.g*.45f,color.b*.45f,color.a);
            DrawSpellIcon("ability."+id,Slot(slot%4,slot/4),id,UnitSkillTip(unit,id,cfg),unit);GUI.color=color;slot++;
        }
        if(pages>1){var rect=Slot((capacity-1)%4,(capacity-1)/4);StoneFrame(rect);GUI.Label(rect,(skillCardPage+1)+"/"+pages+"  >",new GUIStyle(hudSmall){alignment=TextAnchor.MiddleCenter});if(Button("ability.page",rect,"",false,iconStyle))skillCardPage=(skillCardPage+1)%pages;if(rect.Contains(HudPointer))hudTip="Другие способности · страница "+(skillCardPage+1)+" из "+pages;}
        if(aspect)DrawPassiveAspect(unit.team,Slot(3,2));
    }
}
}
