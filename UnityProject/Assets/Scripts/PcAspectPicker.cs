using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
// Godot aspect_picker.gd parity: an irreversible, match-scoped choice of one of the
// two race aspects at the start of every round. The simulation is frozen while the
// player chooses, the bot picks immediately, and a timeout selects the left aspect.
public sealed partial class PcGame {
    bool aspectWaiting,aspectInfoOpen;
    float aspectRemaining;
    int aspectPickBot=-1,aspectRound=1,aspectOnlinePending=-1;
    GUIStyle aspectEyebrow,aspectHeading,aspectCardTitle,aspectStyle,aspectCardStyle,aspectTimer,aspectBadgeText,aspectInfoTitle,aspectInfoText;
    GUIStyle aspectEmpty;

    void AspectBeginRound(bool enabled) {
        aspectInfoOpen=false;aspectRound=series!=null?series.Round:1;
        aspectPickBot=enabled?UnityEngine.Random.Range(0,2):-1;
        aspectRemaining=30f;aspectWaiting=enabled;
        if(enabled&&match!=null)match.Aspects[1]=aspectPickBot;
        if(enabled)Debug.Log("CF_PC_ASPECT_BEGIN race="+RaceEnglish+" round="+aspectRound+" bot="+aspectPickBot);
    }
    void AspectTick(float delta) {
        if(!aspectWaiting)return;
        // In a network match the Godot server owns the countdown and the timeout pick,
        // so the client only renders the remaining seconds from its snapshots.
        if(onlineMatch)return;
        aspectRemaining=Mathf.Max(0,aspectRemaining-delta);
        if(aspectRemaining<=0f)AspectChoose(0,true);
    }
    void AspectChoose(int index,bool timeout=false) {
        if(!aspectWaiting||match==null||match.Aspects[0]>=0)return;
        if(onlineMatch) {
            if(aspectOnlinePending>=0)return;
            aspectOnlinePending=Mathf.Clamp(index,0,1);
            NetworkCommand(PcServerReplica.Aspect(aspectOnlinePending));
            Debug.Log("CF_PC_ASPECT_SENT index="+aspectOnlinePending);
            return;
        }
        match.Aspects[0]=Mathf.Clamp(index,0,1);
        if(aspectPickBot<0)aspectPickBot=UnityEngine.Random.Range(0,2);
        match.Aspects[1]=aspectPickBot;aspectWaiting=false;
        Debug.Log("CF_PC_ASPECT_PICK team=0 index="+match.Aspects[0]+" bot="+match.Aspects[1]+" left="+aspectRemaining.ToString("F1")+(timeout?" timeout":""));
    }
    bool AspectShown()=>match!=null&&page==Page.Match&&match.Aspects[0]>=0&&match.Aspects[1]>=0&&!match.Finished&&AspectCount(matchRace)==2;

    void EnsureAspectStyles() {
        if(aspectEyebrow!=null)return;
        aspectEyebrow=new GUIStyle(small){fontSize=12,normal={textColor=new Color(.62f,.70f,.70f)}};
        aspectHeading=new GUIStyle(heading){fontSize=34,normal={textColor=new Color(.85f,.73f,.47f)}};
        aspectCardTitle=new GUIStyle(title){fontSize=23,alignment=TextAnchor.MiddleLeft,wordWrap=false,clipping=TextClipping.Clip};
        aspectStyle=new GUIStyle(small){fontSize=15,wordWrap=false,clipping=TextClipping.Clip,normal={textColor=new Color(.80f,.68f,.42f)}};
        aspectCardStyle=new GUIStyle(label){fontSize=14,wordWrap=true,clipping=TextClipping.Clip};
        aspectTimer=new GUIStyle(small){fontSize=14,wordWrap=true};
        aspectBadgeText=new GUIStyle(hudSmall){fontSize=13,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.93f,.88f,.72f)}};
        aspectInfoTitle=new GUIStyle(hudSmall){fontSize=13,wordWrap=false,clipping=TextClipping.Clip,normal={textColor=new Color(.86f,.75f,.5f)}};
        aspectInfoText=new GUIStyle(hudSmall){fontSize=11,wordWrap=true,clipping=TextClipping.Clip};
        aspectEmpty=new GUIStyle(GUI.skin.button){border=new RectOffset(),padding=new RectOffset()};
        aspectEmpty.normal.background=null;aspectEmpty.hover.background=null;aspectEmpty.active.background=null;aspectEmpty.focused.background=null;
    }
    // Godot world_replica.apply_snapshot: aspects.state() is {enabled, choices, remaining}
    // in server slot order; the local side is choices[slot], the opponent 1-slot.
    void AspectOnlineApply(JObject state) {
        if(!onlineMatch||match==null)return;
        bool enabled=state!=null&&(bool?)state["enabled"]==true;
        var choices=state?["choices"] as JArray;
        if(!enabled||choices==null||choices.Count!=2){match.Aspects[0]=match.Aspects[1]=-1;aspectWaiting=false;aspectOnlinePending=-1;return;}
        int slot=Mathf.Clamp(networkSlot,0,1);
        int local=(int?)choices[slot]??-1,enemy=(int?)choices[1-slot]??-1;
        if(local<-1||local>1||enemy<-1||enemy>1)return;
        match.Aspects[0]=local;match.Aspects[1]=enemy;
        aspectRemaining=Mathf.Clamp((float?)state?["remaining"]??0f,0f,30f);
        aspectRound=series!=null?series.Round:1;
        aspectWaiting=local<0||enemy<0;
        if(local>=0)aspectOnlinePending=-1;
    }
    void DrawAspectUi() {
        if(match==null)return;
        if(aspectWaiting){DrawAspectModal();return;}
        DrawPassiveAspectInfo();
    }
    void DrawAspectModal() {
        EnsureAspectStyles();
        Fill(new Rect(0,0,1280,720),new Color(.01f,.02f,.03f,.85f));
        var panel=new Rect(190,80,900,566);
        Fill(panel,new Color(.063f,.118f,.145f,.99f));Border(panel,new Color(.48f,.44f,.33f),2);
        Fill(new Rect(panel.x+1,panel.y+1,panel.width-2,2),new Color(.62f,.49f,.30f,.85f));
        GUI.Label(new Rect(222,102,560,20),"АСПЕКТЫ  /  РАУНД "+aspectRound,aspectEyebrow);
        GUI.Label(new Rect(222,126,700,46),"Выберите путь своей расы",aspectHeading);
        GUI.Label(new Rect(222,176,700,26),RaceTitle(matchRace)+" · Один аспект на весь раунд",label);
        bool picked=match.Aspects[0]>=0||aspectOnlinePending>=0;
        for(int i=0;i<2;i++) {
            float x=222+i*426;
            var card=new Rect(x,222,410,330);
            bool chosen=picked&&match.Aspects[0]==i;
            Fill(card,chosen?new Color(.10f,.17f,.15f,.98f):new Color(.078f,.149f,.173f,.98f));
            Border(card,chosen?new Color(.80f,.66f,.38f):new Color(.235f,.333f,.341f),chosen?2:1);
            var icon=AspectIcon(matchRace,i);
            if(icon)GUI.DrawTexture(new Rect(x+16,238,52,52),icon,ScaleMode.ScaleToFit);
            GUI.Label(new Rect(x+80,232,314,58),AspectField(matchRace,i,"title"),aspectCardTitle);
            GUI.Label(new Rect(x+16,300,378,24),AspectField(matchRace,i,"style"),aspectStyle);
            GUI.Label(new Rect(x+16,328,378,152),AspectField(matchRace,i,"description"),aspectCardStyle);
            bool was=GUI.enabled;GUI.enabled=!picked&&was;
            string caption=match.Aspects[0]==i?"Аспект выбран":match.Aspects[0]>=0?"Выбор закрыт":aspectOnlinePending==i?"Выбор отправлен…":aspectOnlinePending>=0?"Выбор отправлен":"Выбрать этот аспект";
            if(Button("aspect.choose"+i,new Rect(x+16,490,378,46),caption,true))AspectChoose(i);
            GUI.enabled=was;
        }
        int seconds=Mathf.CeilToInt(aspectRemaining);
        string timer=picked?"Ваш выбор принят. Ждём соперника · "+seconds+" с":"Выбор необратим в этом раунде · "+seconds+" с. По истечении времени выбирается левый аспект.";
        GUI.Label(new Rect(222,552,880,24),timer,aspectTimer);
        if(Button("aspect.exit",new Rect(222,584,880,42),"Выйти в меню"))page=Page.Leave;
    }
}
}
