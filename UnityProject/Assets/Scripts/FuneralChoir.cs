using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class HumanMatch {
    public const string ChoirTrial="mythic_funeral_choir";
    public const float ChoirPreparation=240f;
    public const float ChoirBar=4*60f/130, ChoirLead=4*ChoirBar, ChoirPass=24;
    public enum ChoirMovement {Verse, First, Interlude, Second, Wail, Rest}
    public bool FuneralChoir {get;private set;}
    public ChoirMovement Movement {get;private set;}
    public float MovementAge=>Elapsed-choirStart;
    public int ChoirTolls {get;private set;}
    public float TollIn=>Mathf.Max(0,nextToll-Elapsed);
    public bool ChoirRevealed {get;private set;}
    public bool ChoirAwakened {get;private set;}
    float choirStart,nextToll,nextVerseEnd;
    readonly Dictionary<int,Unit> crawlers=new Dictionary<int,Unit>();
    readonly List<Unit> invaders=new List<Unit>();
    readonly Dictionary<int,Unit> haunts=new Dictionary<int,Unit>();
    static readonly Color GraveGreen=new Color(.38f,.88f,.49f);
    static readonly JObject ChoirBleed=new JObject{["id"]="grave_bleed",["kind"]="debuff",["duration"]=4,["dps"]=9,["interval"]=1};
    public bool IsCrawler(Unit u)=>crawlers.ContainsKey(u.id);
    public float Burial(Unit u)=>haunts.TryGetValue(u.id,out var ghost)&&ghost.hp>0?Mathf.Clamp01(MovementAge/12):0;

    bool ChoirCastleEvades(int defender){
        if(!FuneralChoir||defender!=1||Roll()>=.20f)return false;
        PulseAt(CastlePosition(defender),1.6f,GraveGreen,"Промах",.6f);
        return true;
    }
    void BeginChoir(){
        FuneralChoir=(string)ChallengeRules?["id"]==ChoirTrial;
        if(!FuneralChoir)return;
        nextToll=ChoirPreparation+8*ChoirBar;
        nextVerseEnd=ChoirPreparation;
    }
    void NextChoir(ChoirMovement next){
        Movement=next;choirStart=Elapsed;ChoirRevealed=false;
        if(next==ChoirMovement.Interlude||next==ChoirMovement.Rest)nextVerseEnd=Elapsed+8*ChoirBar;
    }
    bool ChoirVictim(Unit u)=>u.team==0&&ChaosFighter(u)&&!Invulnerable(u);
    Unit GraveSpawn(string raw,Vector2 at){
        int kind=Array.FindIndex(Lines,line=>line.unit==raw);
        if(ChoirRevealed)GraveSites.Add(at);
        var unit=Spawn(1,kind,at);unit.onRoad=true;unit.noBounty=unit.noCorpse=unit.summoned=true;
        PulseAt(at,2,GraveGreen,"",1,"undead_raise");return unit;
    }
    public readonly List<Vector2> GraveSites=new List<Vector2>();
    void OpenGraves(){
        ChoirRevealed=true;
        if(Movement==ChoirMovement.Second){
            var keep=CastlePosition(0);
            for(int i=0;i<3;i++)invaders.Add(GraveSpawn(i==1?"u007":"n00E",keep+new Vector2(11,i*5-5)));
            return;
        }
        foreach(var target in Units.Where(ChoirVictim).OrderByDescending(u=>u.position.x).Take(6).ToArray()){
            var ghoul=GraveSpawn("n00B",target.position+Vector2.right*2);
            crawlers.Add(ghoul.id,target);ghoul.combatTarget=target.id;invaders.Add(ghoul);
        }
    }
    void EndGraves(){
        foreach(var u in invaders)DismissChoir(u);
        invaders.Clear();crawlers.Clear();GraveSites.Clear();
    }
    static void DismissChoir(Unit u){
        if(u.hp<=0)return;
        u.hp=0;u.deathProcessed=true;u.pose="Death";u.strikeLeft=-1;
    }
    void StartHaunts(){
        NextChoir(ChoirMovement.Wail);
        foreach(var target in Units.Where(ChoirVictim).ToArray()){
            var ghost=GraveSpawn("u001",target.position+Vector2.right);
            ghost.maxHp=ghost.hp=Mathf.Clamp(Lines[target.kind].damage*2,20,400);
            ghost.armor=0;ghost.abilities.Clear();target.combatTarget=ghost.id;
            haunts.Add(target.id,ghost);
        }
    }
    void TickHaunts(){
        foreach(var pair in haunts){
            var target=FindUnit(pair.Key);var ghost=pair.Value;
            if(ghost.hp<=0||target==null||!ChoirVictim(target)){DismissChoir(ghost);continue;}
            float angle=MovementAge*1.57f+target.id;
            ghost.position=target.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*1.1f;
            target.combatTarget=ghost.id;ghost.castPause=.2f;
            if(MovementAge<12)continue;
            DamageUnit(target,target.hp,1,execute:true,killer:ghost,suppressUndead:true);
            if(target.hp<=0)GraveSpawn("u002",target.position);
            DismissChoir(ghost);
        }
        if(MovementAge>=12.92f){haunts.Clear();NextChoir(ChoirMovement.Rest);}
    }
    void ChoirOnHit(Unit owner,Unit target){
        if(owner==null||target.hp<=0||!crawlers.ContainsKey(owner.id))return;
        ApplyEffectData(target,ChoirBleed,1);
        PulseAt(target.position,1,Color.red,"",.5f,"blackhand:bleed",target.id);
    }
    internal void TickChoir(){
        if(!FuneralChoir||Finished)return;
        if(Elapsed>=nextToll){
            ChoirTolls++;nextToll+=8*ChoirBar;
            foreach(var u in Units)if(ChoirVictim(u)&&u.hp>1)u.hp=Mathf.Max(1,u.hp*.94f);
        }
        if(CastleHp[1]<=CastleHealthLimit(1)*.5f)ChoirAwakened=true;
        switch(Movement){
            case ChoirMovement.Verse: if(Elapsed>=nextVerseEnd)NextChoir(ChoirMovement.First);break;
            case ChoirMovement.Interlude:
            case ChoirMovement.Rest:
                if(Elapsed>=nextVerseEnd){
                    if(ChoirAwakened)NextChoir(ChoirMovement.Second);
                    else NextChoir(ChoirMovement.First);
                }
                break;
            case ChoirMovement.First:
            case ChoirMovement.Second:
                if(!ChoirRevealed&&MovementAge>=ChoirLead)OpenGraves();
                if(MovementAge>=ChoirPass){
                    EndGraves();
                    if(Movement==ChoirMovement.Second)StartHaunts();else NextChoir(ChoirMovement.Interlude);
                }
                break;
            case ChoirMovement.Wail:TickHaunts();break;
        }
    }
}
}
