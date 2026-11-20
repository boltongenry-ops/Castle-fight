using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    bool Debuff(HumanMatch.Effect e){string k=(string)e.config?["kind"];return e.left>0&&(e.id==PepeRules.PepeMarkId||k=="debuff"||k=="poison"||k=="incinerate"||(bool?)e.config?["disable"]==true||e.root||e.ground||e.speed<0||e.haste<0||e.armor<0||e.incoming>0);}
    Texture2D DebuffArt(HumanMatch.Effect e){return AbilityArt(e.id=="corrupted_impale_air"?"A0AE":e.id=="A0AF"?"A0AE":e.id.StartsWith("aspect_plague_")?"A05R":e.id)??(e.root?AbilityArt("A06I"):null)??Extra("debuff");}
    void DrawUnitDebuffs(HumanMatch.Unit u,float x,float y){
        var debtStatus=match.PandaStagger(u)>0?new[]{new HumanMatch.Effect{id="APSt",left=4,config=new Newtonsoft.Json.Linq.JObject{["name"]="Выдержка · долг "+match.PandaStagger(u).ToString("F0")}}}:System.Array.Empty<HumanMatch.Effect>();var debuffs=debtStatus.Concat(u.effects.Where(Debuff)).Take(4).ToArray();var hostile=u.auraIds.Where(id=>(bool?)match.Abilities[id]?["profile"]?["enemy"]==true).Take(Mathf.Max(0,4-debuffs.Length)).ToArray();int count=debuffs.Length+hostile.Length+(u.stun>0?1:0);if(count==0)return;
        float start=x-(count*15-2)/2f;int i=0;
        foreach(var effect in debuffs){var r=new Rect(start+i++*15,y-16,13,13);Fill(r,Color.black);var art=DebuffArt(effect);if(art)GUI.DrawTexture(r,art);Border(r,new Color(.9f,.28f,.12f));if(r.Contains(HudPointer)&&GUI.enabled)hudTip=Clean((string)effect.config?["name"]??effect.id)+(float.IsPositiveInfinity(effect.left)?"\nПостоянно":"\nОсталось: "+Mathf.CeilToInt(effect.left)+" сек.");}
        foreach(var id in hostile){var r=new Rect(start+i++*15,y-16,13,13);Fill(r,Color.black);GUI.DrawTexture(r,AbilityArt(id)??Extra("debuff"));Border(r,new Color(.9f,.28f,.12f));if(r.Contains(HudPointer)&&GUI.enabled)hudTip=Clean((string)match.Abilities[id]?["name"]??id)+"\nВражеская аура";}
        if(u.stun>0){var r=new Rect(start+i*15,y-16,13,13);GUI.DrawTexture(r,Extra("debuff"));Border(r,new Color(.95f,.6f,.1f));if(r.Contains(HudPointer)&&GUI.enabled)hudTip=(u.stasisLeft>0?"Стазис":"Оглушение")+"\nОсталось: "+Mathf.CeilToInt(u.stasisLeft>0?u.stasisLeft:u.stun)+" сек.";}
    }
}
}
