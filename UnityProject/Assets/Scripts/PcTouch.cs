using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    int tapFinger=-1,scrollFinger=-1,scrollTarget;Vector2 tapOrigin,touchPointer;bool tapMoved,tapOverUi,tapMulti,explicitTouchPointer;
    static readonly Rect MobileTalkRect=new Rect(236,48,170,48);
    Vector2 ActivePointer=>explicitTouchPointer?touchPointer:(Vector2)Input.mousePosition;
    void UpdateGuiMetrics(){Rect safe=Application.isMobilePlatform?Screen.safeArea:new Rect(0,0,Screen.width,Screen.height);if(safe.width<1||safe.height<1)safe=new Rect(0,0,Screen.width,Screen.height);guiScale=Mathf.Max(.01f,Mathf.Min(safe.width/1280f,safe.height/720f));guiOrigin=new Vector2(safe.x+(safe.width-1280*guiScale)*.5f,Screen.height-safe.yMax+(safe.height-720*guiScale)*.5f);}
    Vector2 GuiPoint(Vector2 screen)=>new Vector2((screen.x-guiOrigin.x)/guiScale,(Screen.height-screen.y-guiOrigin.y)/guiScale);
    bool TouchOverUi(Vector2 screen) {
        if(versusActive||showVoicePanel||showNetworkProfile||showUpdatePanel||showStickerPicker&&page!=Page.Match)return true;
        var p=GuiPoint(screen);
        if(AccountOverlayOpen||SocialNotificationAt(p))return true;
        if(page==Page.Viewer)return p.y<80;
        if(page==Page.Spectator)return SpectatorStrip.Contains(p);
        if(page!=Page.Match)return true;
        if(Application.isMobilePlatform&&(p.y<42||new Rect(5,46,52,52).Contains(p)||new Rect(960,482,320,238).Contains(p)))return true;
        if(onlineMatch&&(new Rect(68,Application.isMobilePlatform?48:33,160,48).Contains(p)||(Application.isMobilePlatform&&MobileTalkRect.Contains(p))))return true;
        if(match!=null&&match.CursedHorde&&HordeHud.Contains(p))return true;
        if(match!=null&&match.LastProtocol&&ProtocolHud.Contains(p))return true;
        if(match!=null&&match.EternalWinter&&WinterHud.Contains(p))return true;
        return BattleSocialContains(p)||HudContains(p);
    }
    void DismissBuildMenuOnEmptyClick() {
        if(versusActive||page!=Page.Match||!builderSelected||!buildMenu||showVoicePanel||showNetworkProfile||showStickerPicker||showBattleChat||!GUI.enabled)return;
        var e=Event.current;
        // Buttons consume their release event; only an empty-area click reaches here.
        if(e.type!=EventType.MouseUp||GUIUtility.hotControl!=0||(Application.isMobilePlatform&&(tapMoved||tapMulti)))return;
        // A selected building still uses a world tap to choose its construction site.
        if(chosen>=0&&!MouseOverHud())return;
        buildMenu=false;chosen=-1;e.Use();
    }
    void HandleTouchWorld() {
        if(Input.touchCount>=2)tapMulti=true;
        for(int i=0;i<Input.touchCount;i++){
            var touch=Input.GetTouch(i);
            if(touch.phase==TouchPhase.Began&&tapFinger<0){tapFinger=touch.fingerId;tapOrigin=touch.position;tapMoved=false;tapOverUi=TouchOverUi(touch.position);tapMulti=Input.touchCount>1;}
            if(touch.fingerId!=tapFinger)continue;
            if(Vector2.Distance(touch.position,tapOrigin)>Mathf.Max(12,Screen.dpi>0?Screen.dpi*.06f:12))tapMoved=true;
            if(touch.phase==TouchPhase.Canceled){tapFinger=-1;continue;}
            if(touch.phase!=TouchPhase.Ended)continue;
            bool valid=!tapMoved&&!tapMulti&&!tapOverUi&&!TouchOverUi(touch.position);tapFinger=-1;
            if(!valid)continue;touchPointer=touch.position;explicitTouchPointer=true;
            try{HandleWorldTap();}finally{explicitTouchPointer=false;}
        }
    }
    void HandleWorldTap() {
        if(!PointerOnTerrain(out Vector3 hit))return;Vector2 point=new Vector2(hit.x,-hit.z);
        if(chosen>=0){float wx=hit.x*30,wy=-hit.z*30;int x=Mathf.FloorToInt((wx+6144)/128),y=HumanMatch.BuildRow(wy,true);Place(x,y);return;}
        if(selectedItem>=0){ExecuteItem(point);return;}if(!string.IsNullOrEmpty(targetedSpell)){ExecuteBuilderSpell(point);return;}if(moveOrder){OrderBuilder(point);moveOrder=false;return;}
        bool hadBuilder=builderSelected;SelectAtCursor();
        if(hadBuilder&&!builderSelected&&!selectedCastle&&selectedHut<0&&selectedUnit<0){builderSelected=true;OrderBuilder(point);}
    }
    int textTouch=-1,textTouchFrame=-1;
    Rect textTouchArea;
    void ScrollTextByTouch(Rect viewport,ref Vector2 scroll,float max){
        if(!Application.isMobilePlatform||!GUI.enabled||Event.current.type!=EventType.Repaint||max<=0)return;
        for(int i=0;i<Input.touchCount;i++){
            var touch=Input.GetTouch(i);
            if(textTouch<0&&touch.phase==TouchPhase.Began&&viewport.Contains(GuiPoint(touch.position))){
                textTouch=touch.fingerId;textTouchArea=viewport;
            }
            if(textTouch!=touch.fingerId||textTouchArea!=viewport||textTouchFrame==Time.frameCount)continue;
            textTouchFrame=Time.frameCount;
            if(touch.phase==TouchPhase.Moved)scroll.y=Mathf.Clamp(scroll.y+touch.deltaPosition.y/guiScale,0,max);
            if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled)textTouch=-1;
        }
    }
    void UpdateTouchScroll(){
        if(Input.touchCount==0)textTouch=-1;
        if(page==Page.Guide&&!AccountOverlayOpen&&guideRace>=0){
            for(int i=0;i<Input.touchCount;i++){
                var touch=Input.GetTouch(i);
                if(touch.phase==TouchPhase.Began){
                    socialTouchMoved=false;
                    var pointer=GuiPoint(touch.position);
                    if(GuideListArea.Contains(pointer)){
                        scrollFinger=touch.fingerId;socialTouchOrigin=touch.position;
                    }
                }
                if(touch.fingerId!=scrollFinger)continue;
                if(touch.phase==TouchPhase.Moved&&Vector2.Distance(touch.position,socialTouchOrigin)>14){
                    socialTouchMoved=true;
                    float delta=touch.deltaPosition.y/guiScale;
                    guideGridScroll.y=Mathf.Max(0,guideGridScroll.y+delta);
                }
                if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled){
                    if(socialTouchMoved)guideGridScroll.y=Mathf.Round(guideGridScroll.y/GuideRowStep)*GuideRowStep;
                    scrollFinger=-1;
                }
            }
            return;
        }
        if(AccountOverlayOpen){
            for(int i=0;i<Input.touchCount;i++){
                var touch=Input.GetTouch(i);
                if(touch.phase==TouchPhase.Began){
                    socialTouchMoved=false;
                    if(scrollFinger<0&&AccountHub.selected==null&&AccountOverlayScroll.Contains(GuiPoint(touch.position))){
                        scrollFinger=touch.fingerId;socialTouchOrigin=touch.position;
                    }
                }
                if(touch.fingerId!=scrollFinger)continue;
                if(touch.phase==TouchPhase.Moved&&Vector2.Distance(touch.position,socialTouchOrigin)>14){
                    socialTouchMoved=true;
                    float delta=touch.deltaPosition.y/guiScale;
                    if(AccountHub.overlay==AccountOverlay.Messages)messageScroll.y=Mathf.Max(0,messageScroll.y+delta);
                    else AccountHub.listScroll.y=Mathf.Max(0,AccountHub.listScroll.y+delta);
                }
                if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled)scrollFinger=-1;
            }
            return;
        }
        UpdateNotesTouch();
        UpdateProgressionTouch();
        UpdatePassiveAspectTouch();UpdateSpellScroll();
        if(page!=Page.Network||showNetworkProfile||showStickerPicker||showVoicePanel){scrollFinger=-1;return;}
        for(int i=0;i<Input.touchCount;i++){var t=Input.GetTouch(i);var p=GuiPoint(t.position);
            if(t.phase==TouchPhase.Began&&scrollFinger<0&&p.y>=150&&p.y<=675&&(socialTab!=0||(networkSlot<0?new Rect(62,249,775,384):new Rect(648,183,572,278)).Contains(p))){scrollFinger=t.fingerId;socialTouchOrigin=t.position;socialTouchMoved=false;scrollTarget=socialTab==0?0:!string.IsNullOrEmpty(socialViewed)||socialTab==3?5:socialTab==2&&!string.IsNullOrEmpty(socialPeer)?4:3;}
            if(t.fingerId!=scrollFinger)continue;
            if(t.phase==TouchPhase.Moved){if(Vector2.Distance(t.position,socialTouchOrigin)>14)socialTouchMoved=true;float d=t.deltaPosition.y/guiScale;if(scrollTarget==0){if(networkSlot<0)directoryScroll.y=Mathf.Max(0,directoryScroll.y+d);else chatScroll.y=Mathf.Max(0,chatScroll.y+d);}else if(scrollTarget==3)socialScroll.y=Mathf.Max(0,socialScroll.y+d);else if(scrollTarget==4)messageScroll.y=Mathf.Max(0,messageScroll.y+d);else matchScroll.y=Mathf.Max(0,matchScroll.y+d);}
            if(t.phase==TouchPhase.Ended||t.phase==TouchPhase.Canceled)scrollFinger=-1;
        }
    }
    void DrawMobileTalk(){if(!Application.isMobilePlatform||!voice||!voice.MicrophoneEnabled)return;Fill(MobileTalkRect,voice.TouchTalking?new Color(.48f,.32f,.15f,.95f):new Color(.06f,.07f,.07f,.95f));Border(MobileTalkRect,new Color(.75f,.62f,.4f));GUI.Label(MobileTalkRect,"ГОВОРИТЬ",new GUIStyle(menuButton){alignment=TextAnchor.MiddleCenter,normal={background=null,textColor=Color.white}});}
}
}
