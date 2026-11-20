using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    sealed class SpellDisplay {public bool active,casting;public float mana,cost,regen,left,total,manaWait;public string blocked;public float Wait=>Mathf.Max(left,manaWait);}
    sealed class SpellTrace {public float left,mana,flash;}
    readonly Dictionary<string,SpellTrace> spellTraces=new Dictionary<string,SpellTrace>();
    readonly Texture2D[] spellSweeps=new Texture2D[33];
    HumanMatch spellTraceMatch;float networkSpellStamp;
    string spellDetailsId;int spellDetailsActor,spellScrollFinger=-1;bool spellDetailsBuilding;Vector2 spellDetailsScroll;
    void UpdateSpellScroll(){if(string.IsNullOrEmpty(spellDetailsId)){spellScrollFinger=-1;return;}foreach(var touch in Input.touches){if(touch.phase==TouchPhase.Began&&new Rect(716,208,378,246).Contains(GuiPoint(touch.position)))spellScrollFinger=touch.fingerId;if(spellScrollFinger!=touch.fingerId)continue;if(touch.phase==TouchPhase.Moved)spellDetailsScroll.y=Mathf.Max(0,spellDetailsScroll.y+touch.deltaPosition.y/guiScale);if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled)spellScrollFinger=-1;}}
    float UnitManaRegen(HumanMatch.Unit u)=>onlineMatch&&networkUnitCards.TryGetValue(u.id,out var row)?HumanMatch.F(row["spell_ui"],"regen",HumanMatch.F(match.Lines[u.kind].stats,"mana_regen")):u.manaRegen+u.auraMana;
    float HutManaRegen(HumanMatch.Hut h)=>h.paused||!h.paid?0:onlineMatch&&networkBuildingCards.TryGetValue(h.id,out var row)?HumanMatch.F(row["spell_ui"],"regen",HumanMatch.F(match.Lines[h.kind].buildingStats,"mana_regen")):match.BuildingManaRegen(h,HumanMatch.F(match.Lines[h.kind].buildingStats,"mana_regen"))+match.ElementalManaBonus(h)+(match.MechPowered(h)?HumanMatch.F(match.Abilities["A09P"]?["fields"],"Hab1"):0);
    SpellDisplay SpellState(string id,HumanMatch.Unit unit=null,HumanMatch.Hut hut=null){
        var cfg=unit!=null?UnitSkillConfig(unit,id,match.Abilities[id]):match.Abilities[id];var kind=(string)cfg?["kind"]??"";
        var timers=unit?.spellTimers??hut.spellTimers;float timer=timers.TryGetValue(id,out float t)?t:0;
        var s=new SpellDisplay{mana=unit?.mana??hut.mana,cost=HumanMatch.F(cfg,"mana"),regen=unit!=null?UnitManaRegen(unit):HutManaRegen(hut),total=HumanMatch.F(cfg,"cd"),blocked=""};
        s.active=(bool?)cfg?["custom_autocast"]==true||new[]{"CT23","CT24","CT26"}.Contains(id)||(s.cost>0||s.total>0)&&!new[]{"marker","aura","native_helper","pending","manual","human_artillery_fire","orc_artillery_fire"}.Contains(kind)||timer>.25f&&s.total>0;
        if(!s.active)return s;
        if(hut!=null){s.total=match.BuildingCooldown(hut,s.total);if(hut.paused)s.blocked="Постройка приостановлена";else if(!hut.paid)s.blocked="Идёт строительство";else if(match.BuildingDisabled(hut)>0)s.blocked="Постройка отключена";}
        else {if(match.TeamPandaren[unit.team]&&match.Aspects[unit.team]==1&&kind=="panda_brew")s.total*=.8f;if(match.TeamOrc[unit.team]&&match.Aspects[unit.team]==1&&id=="A02M")s.total/=1.35f;s.casting=unit.pendingSpell==id&&unit.castLeft>=0;if(match.Silenced(unit))s.blocked="Безмолвие";else if(unit.stun>0||match.Disabled(unit))s.blocked="Юнит оглушён или отключён";}
        if(onlineMatch){var table=unit!=null?networkUnitCards:networkBuildingCards;int actor=unit?.id??hut.id;if(table.TryGetValue(actor,out var row)&&row["spell_ui"] is JObject remote){timer=HumanMatch.F(remote["cooldowns"],id);timer=Mathf.Max(0,timer-Mathf.Clamp(Time.unscaledTime-networkSpellStamp,0,.2f));s.total=HumanMatch.F(remote["totals"],id,s.total);s.casting=(string)remote["casting"]==id;s.blocked=(string)remote["blocked"]??"";}}
        // .25 s is the authority's unsuccessful-target retry, not a spell cooldown.
        s.left=timer>.25f&&s.total>0?timer:0;
        s.manaWait=s.mana+.001f>=s.cost?0:s.regen>0?(s.cost-s.mana)/s.regen:float.PositiveInfinity;
        return s;
    }
    string SpellStatus(SpellDisplay s)=>!s.active?"Пассивная способность":s.blocked!=""?s.blocked:s.casting?"Применяет заклинание":s.left>0?"Перезарядка: "+s.left.ToString("F1")+" с":s.manaWait>0?float.IsInfinity(s.manaWait)?"Не хватает маны · нет регенерации":"Копит ману: "+Mathf.CeilToInt(s.manaWait)+" с":"Готово · автоматическое применение при подходящей цели / условии";
    string SpellRuntimeTip(string tip,SpellDisplay s)=>tip+"\n\n"+SpellStatus(s)+(s.active?"\nМана: "+s.cost.ToString("F0")+" · Перезарядка: "+s.total.ToString("0.##")+" с"+(s.Wait>0&&!float.IsInfinity(s.Wait)?"\nСледующее применение не раньше чем через "+Mathf.CeilToInt(s.Wait)+" с":""):"");
    void ObserveSelectedSpells(){
        if(spellTraceMatch!=match){spellTraces.Clear();spellDetailsId=null;spellTraceMatch=match;}
        var u=SelectedUnit;var h=SelectedHut;if(u==null&&h==null)return;
        var ids=(u!=null?UnitCardAbilities(u):BuildingCardAbilities(h)).Where(id=>SpellState(id,u,h).active).ToArray();
        foreach(string id in ids){var s=SpellState(id,u,h);string key=(u!=null?"u":"h")+(u?.id??h.id)+":"+id;
            if(!spellTraces.TryGetValue(key,out var trace)){spellTraces[key]=new SpellTrace{left=s.left,mana=s.mana};continue;}
            if(s.left>trace.left+.3f||ids.Length==1&&s.cost>0&&trace.mana-s.mana>=s.cost*.8f)trace.flash=Time.unscaledTime+1;
            trace.left=s.left;trace.mana=s.mana;
        }
        if(spellTraces.Count>512)spellTraces.Clear();
    }
    Texture2D SpellSweep(float fraction){
        int slice=Mathf.Clamp(Mathf.CeilToInt(fraction*32),1,32);if(spellSweeps[slice])return spellSweeps[slice];
        var pixels=new Color32[64*64];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float angle=Mathf.Atan2(x-31.5f,y-31.5f);if(angle<0)angle+=Mathf.PI*2;pixels[y*64+x]=angle<=slice/32f*Mathf.PI*2?new Color32(0,0,0,175):new Color32(0,0,0,0);}
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Spell cooldown "+slice,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.DontSave};texture.SetPixels32(pixels);texture.Apply(false,true);return spellSweeps[slice]=texture;
    }
    void DrawSpellIcon(string buttonId,Rect rect,string id,string tip,HumanMatch.Unit unit=null,HumanMatch.Hut hut=null){
        var s=SpellState(id,unit,hut);tip=SpellRuntimeTip(tip,s);
        if(ArtButton(buttonId,rect,AbilityArt(id),"",tip)){spellDetailsId=spellDetailsId==id&&spellDetailsActor==(unit?.id??hut.id)?null:id;spellDetailsActor=unit?.id??hut.id;spellDetailsBuilding=hut!=null;spellDetailsScroll=Vector2.zero;aspectInfoOpen=false;}
        if(!s.active)return;
        GUI.BeginGroup(rect);var local=new Rect(0,0,rect.width,rect.height);
        if(s.blocked!=""||s.manaWait>0)Fill(local,new Color(.015f,.035f,.08f,.5f));
        if(s.left>0)GUI.DrawTexture(local,SpellSweep(s.left/Mathf.Max(.01f,s.total)));
        if(s.cost>0){var badge=new Rect(2,2,Mathf.Min(36,rect.width-4),16);Fill(badge,new Color(.01f,.035f,.13f,.94f));GUI.Label(badge,s.cost.ToString("F0"),new GUIStyle(hudSmall){fontSize=11,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.55f,.78f,1)}});}
        string center=s.casting?"ЧАРЫ":s.left>0?Mathf.CeilToInt(s.left)+" с":s.manaWait>0?float.IsInfinity(s.manaWait)?"МАНА":"М "+Mathf.CeilToInt(s.manaWait)+" с":s.blocked!=""?"СТОП":"";
        if(center!=""){var textRect=new Rect(1,rect.height*.38f,rect.width-2,20);Fill(textRect,new Color(0,0,0,.6f));GUI.Label(textRect,center,new GUIStyle(hudText){fontSize=rect.width<60?11:14,alignment=TextAnchor.MiddleCenter,normal={textColor=s.manaWait>0&&s.left<=0?new Color(.6f,.8f,1):new Color(1,.86f,.45f)}});}
        string key=(unit!=null?"u":"h")+(unit?.id??hut.id)+":"+id;bool cast=spellTraces.TryGetValue(key,out var trace)&&trace.flash>Time.unscaledTime;
        GUI.Label(new Rect(1,rect.height-16,rect.width-2,15),cast?"ПРИМЕНЕНО":"АВТО",new GUIStyle(hudSmall){fontSize=cast?9:10,alignment=TextAnchor.MiddleCenter,normal={textColor=cast?Color.yellow:new Color(.6f,.83f,.65f)}});
        if(cast)Border(local,new Color(1,.86f,.3f),2);else Border(new Rect(1,1,rect.width-2,rect.height-2),new Color(.6f,.45f,.15f),1);
        GUI.EndGroup();
    }
    void DrawManaBar(Rect rect,float mana,float maximum,bool number){
        if(maximum<=0)return;Fill(rect,new Color(.015f,.025f,.08f));Fill(new Rect(rect.x+1,rect.y+1,(rect.width-2)*Mathf.Clamp01(mana/maximum),rect.height-2),new Color(.12f,.3f,.87f));
        if(number)GUI.Label(rect,Mathf.FloorToInt(mana)+" / "+Mathf.FloorToInt(maximum),new GUIStyle(hudSmall){fontSize=10,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.82f,.9f,1)}});
    }
    void DrawSpellDetails(){
        if(string.IsNullOrEmpty(spellDetailsId))return;var u=spellDetailsBuilding?null:SelectedUnit;var h=spellDetailsBuilding?SelectedHut:null;
        if((u?.id??h?.id??-1)!=spellDetailsActor){spellDetailsId=null;return;}
        var cfg=match.Abilities[spellDetailsId];string text=SpellRuntimeTip(u!=null?UnitSkillTip(u,spellDetailsId,cfg):RaceSkillTip(spellDetailsId,cfg),SpellState(spellDetailsId,u,h));
        var rect=new Rect(700,148,410,326);StoneFrame(rect);GUI.Label(new Rect(716,160,320,36),Clean((string)cfg?["name"]??spellDetailsId),new GUIStyle(hudTitle){fontSize=17});
        if(Button("spell.details.close",new Rect(1066,158,30,30),"X",false,button)){spellDetailsId=null;return;}
        var style=new GUIStyle(hudText){fontSize=15,wordWrap=true};float height=Mathf.Max(240,style.CalcHeight(new GUIContent(text),352));spellDetailsScroll=GUI.BeginScrollView(new Rect(716,208,378,246),spellDetailsScroll,new Rect(0,0,352,height));GUI.Label(new Rect(0,0,352,height),text,style);GUI.EndScrollView();
    }
}
}
