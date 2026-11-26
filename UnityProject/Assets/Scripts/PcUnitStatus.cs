using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    bool Debuff(HumanMatch.Effect e){string k=(string)e.config?["kind"];return e.left>0&&(e.id=="BHIr"||e.id==PepeRules.PepeMarkId||k=="debuff"||k=="poison"||k=="incinerate"||(bool?)e.config?["disable"]==true||e.root||e.ground||e.speed<0||e.haste<0||e.armor<0||e.incoming>0);}
    Texture2D DebuffArt(HumanMatch.Effect e){return AbilityArt(e.id=="corrupted_impale_air"?"A0AE":e.id=="A0AF"?"A0AE":e.id.StartsWith("aspect_plague_")?"A05R":e.id)??(e.root?AbilityArt("A06I"):null)??Extra("debuff");}
    void DrawUnitDebuffs(HumanMatch.Unit u,float x,float y){
        var debtStatus=match.PandaStagger(u)>0?new[]{new HumanMatch.Effect{id="APSt",left=4,config=new Newtonsoft.Json.Linq.JObject{["name"]="Выдержка · долг "+match.PandaStagger(u).ToString("F0")}}}:System.Array.Empty<HumanMatch.Effect>();var debuffs=debtStatus.Concat(u.effects.Where(Debuff)).Take(4).ToArray();var hostile=u.auraIds.Where(id=>(bool?)match.Abilities[id]?["profile"]?["enemy"]==true).Take(Mathf.Max(0,4-debuffs.Length)).ToArray();int count=debuffs.Length+hostile.Length+(u.stun>0?1:0);if(count==0)return;
        float start=x-(count*15-2)/2f;int i=0;
        foreach(var effect in debuffs){var r=new Rect(start+i++*15,y-16,13,13);Fill(r,Color.black);var art=DebuffArt(effect);if(art)GUI.DrawTexture(r,art);Border(r,effect.id=="BHIr"?new Color(.43f,.74f,.97f):new Color(.9f,.28f,.12f));if(r.Contains(HudPointer)&&GUI.enabled)hudTip=Clean((string)effect.config?["name"]??effect.id)+((string)effect.config?["description"] is string detail&&BlackHand.IsStatus(effect.id)?"\n"+detail:"")+(float.IsPositiveInfinity(effect.left)?"\nПостоянно":"\nОсталось: "+Mathf.CeilToInt(effect.left)+" сек.");}
        foreach(var id in hostile){var r=new Rect(start+i++*15,y-16,13,13);Fill(r,Color.black);GUI.DrawTexture(r,AbilityArt(id)??Extra("debuff"));Border(r,new Color(.9f,.28f,.12f));if(r.Contains(HudPointer)&&GUI.enabled)hudTip=Clean((string)match.Abilities[id]?["name"]??id)+"\nВражеская аура";}
        if(u.stun>0){var r=new Rect(start+i*15,y-16,13,13);GUI.DrawTexture(r,Extra("debuff"));Border(r,new Color(.95f,.6f,.1f));if(r.Contains(HudPointer)&&GUI.enabled)hudTip=(u.stasisLeft>0?"Стазис":"Оглушение")+"\nОсталось: "+Mathf.CeilToInt(u.stasisLeft>0?u.stasisLeft:u.stun)+" сек.";}
    }

    readonly struct StatusBadge {
        public readonly string name,tip,id;
        public readonly Texture2D art;
        public StatusBadge(string id,string name,string tip,Texture2D art){
            this.id=id;this.name=name;this.tip=tip;this.art=art;
        }
    }

    readonly List<StatusBadge> unitBuffs=new List<StatusBadge>(8),unitDebuffs=new List<StatusBadge>(8);
    int inspectedStatusUnit=-1;
    string inspectedStatus;

    void DrawUnitStatusCard(HumanMatch.Unit unit){
        unitBuffs.Clear();unitDebuffs.Clear();
        if(inspectedStatusUnit!=unit.id){inspectedStatusUnit=unit.id;inspectedStatus=null;}
        if(match.WinterAffects(unit))unitDebuffs.Add(new StatusBadge("winter.weather","Вечная зима",
            "Вечная зима\nСкорость движения −50%\nСкорость атаки −30%\nДо конца боя",
            AbilityArt("A04I")??Extra("debuff")));
        if(match.AbyssWaterAffects(unit))unitDebuffs.Add(new StatusBadge("abyss.water","Глубокая вода",
            "Глубокая вода\nСкорость движения −25%\n10% вероятности промаха оружием",AbilityArt("A00P")??Extra("debuff")));
        if(match.AbyssDeepForm(unit))unitBuffs.Add(new StatusBadge("abyss.form","Глубинная форма",
            "Глубинная форма\nСкорость атаки +25% · урон +20%\n"+AbyssSkillDescription(unit),AbilityArt("A06V")??Extra("heal")));
        foreach(var effect in unit.effects){
            if(effect.left<=0||string.IsNullOrEmpty(effect.id))continue;
            bool harmful=effect.id!="BHIr"&&(Debuff(effect)||effect.sourceTeam==1-unit.team);
            string name=Clean((string)effect.config?["name"]??effect.id);
            string detail=(string)effect.config?["description"];
            string duration=float.IsPositiveInfinity(effect.left)?"Постоянно":"Осталось: "+Mathf.CeilToInt(effect.left)+" с";
            var art=HordeStatusArt(effect.id)??(effect.id==HumanMatch.WinterChain?AbilityArt("A04H")??DebuffArt(effect):
                harmful?DebuffArt(effect):AbilityArt(effect.id)??Extra("heal")??Extra("debuff"));
            (harmful?unitDebuffs:unitBuffs).Add(new StatusBadge("effect."+effect.id,name,
                name+(string.IsNullOrEmpty(detail)?"":"\n"+Clean(detail))+"\n"+duration,art));
        }
        foreach(var id in unit.auraIds){
            var ability=match.Abilities[id];
            bool harmful=(bool?)ability?["profile"]?["enemy"]==true;
            string name=Clean((string)ability?["name"]??id);
            (harmful?unitDebuffs:unitBuffs).Add(new StatusBadge("aura."+id,name,
                name+(harmful?"\nВражеская аура":"\nСоюзная аура"),AbilityArt(id)??Extra("debuff")));
        }
        if(unit.stun>0||unit.stasisLeft>0){
            string name=unit.stasisLeft>0?"Стазис":"Оглушение";
            unitDebuffs.Insert(0,new StatusBadge("stun",name,
                name+"\nОсталось: "+Mathf.CeilToInt(Mathf.Max(unit.stasisLeft,unit.stun))+" с",Extra("debuff")));
        }
        float stagger=match.PandaStagger(unit);
        if(stagger>0)unitDebuffs.Add(new StatusBadge("stagger","Выдержка",
            "Выдержка\nОтложенный урон: "+stagger.ToString("F0"),AbilityArt("APSt")??Extra("debuff")));
        var green=new Color(.25f,.87f,.38f);
        var red=new Color(.96f,.27f,.20f);
        DrawStatusCells(0,"Баффы",unitBuffs,green);
        DrawStatusCells(2,"Дебаффы",unitDebuffs,red);
        DrawStatusSummary(green,red);
    }

    void DrawStatusSummary(Color green,Color red){
        int shown=0;
        foreach(var badges in new[]{unitDebuffs,unitBuffs}){
            foreach(var badge in badges){
                if(shown==3)return;
                var cell=new Rect(715+shown++*26,664,24,24);
                DrawStatusArt(cell,badge.art,ReferenceEquals(badges,unitDebuffs)?red:green);
                StatusTip(cell,badge.id,badge.tip,"summary.");
            }
        }
    }

    void DrawStatusArt(Rect cell,Texture2D art,Color accent){
        if(art)GUI.DrawTexture(new Rect(cell.x+2,cell.y+2,cell.width-4,cell.height-4),art,ScaleMode.StretchToFill);
        Border(cell,accent,2);
    }

    void DrawStatusCells(int column,string title,List<StatusBadge> badges,Color accent){
        for(int i=0;i<2;i++){
            var cell=Slot(column+i,2);
            Fill(cell,new Color(.02f,.025f,.027f,.98f));
            if(i>=badges.Count){
                var edge=accent;edge.a=.48f;Border(cell,edge);
                if(i==0)GUI.Label(cell,title,new GUIStyle(hudSmall){
                    alignment=TextAnchor.MiddleCenter,normal={textColor=edge}
                });
                continue;
            }
            var badge=badges[i];
            DrawStatusArt(cell,badge.art,accent);
            string tip=badge.tip;
            int remaining=i==1?badges.Count-2:0;
            if(remaining>0){
                var count=new Rect(cell.xMax-25,cell.yMax-19,23,17);
                Fill(count,new Color(0,0,0,.85f));
                GUI.Label(count,"+"+remaining,new GUIStyle(hudSmall){alignment=TextAnchor.MiddleCenter});
                tip+="\n\n"+title+" · ещё "+remaining+"\n"+string.Join("\n",badges.Skip(2).Select(b=>b.tip));
            }
            StatusTip(cell,badge.id,tip);
        }
    }

    void StatusTip(Rect cell,string id,string detail,string location=""){
        if(Button("status."+location+id,cell,"",false,iconStyle))inspectedStatus=inspectedStatus==id?null:id;
        if(GUI.enabled&&(cell.Contains(HudPointer)||inspectedStatus==id))hudTip=detail;
    }
}
}
