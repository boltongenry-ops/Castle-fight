using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    const int SelectionLimit=12;
    readonly List<int> selectedGroup=new List<int>();
    readonly Dictionary<int,GameObject> groupRings=new Dictionary<int,GameObject>();
    bool selectionPending,selectionDragging;Vector2 selectionStart,selectionEnd;
    bool CanMarquee=>page==Page.Match&&!aspectWaiting&&chosen<0&&selectedItem<0&&string.IsNullOrEmpty(targetedSpell)&&!moveOrder&&!showBattleChat&&!showVoicePanel&&!showStickerPicker;
    static Rect SelectionRect(Vector2 a,Vector2 b)=>Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
    void ClearGroupSelection(){selectedGroup.Clear();selectionPending=selectionDragging=false;foreach(var ring in groupRings.Values)if(ring)Destroy(ring);groupRings.Clear();}
    Vector3 UnitSelectionPoint(HumanMatch.Unit unit)=>Camera.main.WorldToScreenPoint(World(unit.position)+Vector3.up*(match.Height(unit)+1.3f));
    void HandleSelectionGesture(){
        if(!CanMarquee){selectionPending=selectionDragging=false;return;}
        if(Input.GetMouseButtonDown(1)){selectionPending=selectionDragging=false;return;}
        if(Input.GetMouseButtonDown(0)&&!MouseOverHud()){selectionPending=true;selectionDragging=false;selectionStart=selectionEnd=ActivePointer;}
        if(!selectionPending)return;
        selectionEnd=ActivePointer;selectionDragging|=Vector2.Distance(selectionStart,selectionEnd)>=6*guiScale;
        if(!Input.GetMouseButtonUp(0))return;
        bool dragging=selectionDragging;selectionPending=selectionDragging=false;
        bool additive=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
        if(dragging)SelectScreenGroup(SelectionRect(selectionStart,selectionEnd),additive);
        else if(!MouseOverHud()){
            var previous=selectedGroup.ToArray();SelectAtCursor();
            if(selectedUnit>=0){int picked=selectedUnit;SetSelectedGroup(additive?previous.Concat(new[]{picked}):new[]{picked});}
        }
    }
    void SetSelectedGroup(IEnumerable<int> ids){
        var actors=ids.Distinct().Select(id=>match.Units.FirstOrDefault(u=>u.id==id&&u.hp>0&&!match.Hidden(u)&&CanSeeUnit(u))).Where(u=>u!=null).ToArray();
        // A command group belongs to one side. Friendly actors win a mixed box.
        int side=actors.Any(u=>u.team==0)?0:actors.FirstOrDefault()?.team??0;
        selectedGroup.Clear();selectedGroup.AddRange(actors.Where(u=>u.team==side).Take(SelectionLimit).Select(u=>u.id));
        selectedUnit=selectedGroup.Count>0?selectedGroup[0]:-1;selectedHut=-1;selectedCastle=builderSelected=false;selectedItem=chosen=-1;targetedSpell=null;moveOrder=buildMenu=showRange=false;
    }
    void SelectScreenGroup(Rect rectangle,bool additive){
        var candidates=match.Units.Where(u=>u.hp>0&&!match.Hidden(u)&&CanSeeUnit(u)&&match.Lines[u.kind].unit!="n02E")
            .Where(u=>{var point=UnitSelectionPoint(u);return point.z>0&&rectangle.Contains(point);}).OrderBy(u=>u.id).Select(u=>u.id).ToArray();
        SetSelectedGroup(additive?selectedGroup.ToArray().Concat(candidates):candidates);
        if(selectedGroup.Count==0){var worker=Camera.main.WorldToScreenPoint(World(match.Builders[0].position)+Vector3.up*1.4f);if(worker.z>0&&rectangle.Contains(worker))builderSelected=true;}
    }
    void UpdateGroupRings(){
        if(match==null)return;
        selectedGroup.RemoveAll(id=>!match.Units.Any(u=>u.id==id&&u.hp>0&&CanSeeUnit(u)));
        if(selectedGroup.Count>0&&!selectedGroup.Contains(selectedUnit))selectedUnit=selectedGroup[0];
        foreach(var id in selectedGroup){var actor=match.Units.First(u=>u.id==id);if(!groupRings.TryGetValue(id,out var ring)||!ring){ring=Ring("Group selection "+id,visuals.transform);groupRings[id]=ring;}ring.SetActive(page==Page.Match&&id!=selectedUnit);ring.transform.position=World(actor.position)+Vector3.up*.13f;}
        foreach(int id in groupRings.Keys.Where(id=>!selectedGroup.Contains(id)).ToArray()){if(groupRings[id])Destroy(groupRings[id]);groupRings.Remove(id);}
    }
    void DrawSelectionMarquee(){if(!selectionDragging||!selectionPending)return;var rect=SelectionRect(GuiPoint(selectionStart),GuiPoint(selectionEnd));Fill(rect,new Color(.15f,.8f,.22f,.10f));Border(rect,new Color(.25f,1,.3f),1);}
    Texture2D GroupIcon(HumanMatch.Unit unit){var revised=ContentResources.Load<Texture2D>("NerubianUnitIcon/"+match.Lines[unit.kind].unit);if(revised)return revised;int race=unit.team==0?matchRace:matchBotRace;if(race==15)return CentaurUnitIcon(match.Lines[unit.kind].unit);var pack=Pack(race);string raw=match.Lines[unit.kind].building;if(!raceIndices[race].TryGetValue(raw,out int index))return CommandIcon(0);if(MobileContent.Active&&race==12)return UltimateIcon034(index);if(pack)return pack.buildingIcons[index];return index<unitIcons.Length?unitIcons[index]:CommandIcon(0);}
    void DrawGroupCard(){
        for(int i=0;i<selectedGroup.Count;i++){
            var actor=match.Units.FirstOrDefault(u=>u.id==selectedGroup[i]&&u.hp>0);if(actor==null)continue;
            var rect=new Rect(504+i%6*48,617+i/6*45,41,37);
            if(ArtButton("group.unit."+actor.id,rect,GroupIcon(actor),"",match.Lines[actor.kind].unitName+"\n"+Mathf.CeilToInt(actor.hp)+" / "+Mathf.CeilToInt(actor.maxHp)+" HP",selectedUnit==actor.id))selectedUnit=actor.id;
            Fill(new Rect(rect.x,rect.yMax,rect.width,3),Color.black);Fill(new Rect(rect.x,rect.yMax,rect.width*actor.hp/actor.maxHp,3),actor.team==0?Color.green:Color.red);
        }
        GUI.Label(new Rect(504,706,290,14),"Shift — добавить · выбор бойца — карточка",new GUIStyle(hudSmall){fontSize=10});
    }
}
}
