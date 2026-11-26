using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class HumanMatch {
    public const string AbyssTrial="mythic_abyssal_tide";
    public const float AbyssSlow=.25f, AbyssMiss=.10f;
    public const float AbyssFirstUndertow=315, AbyssUndertowInterval=42, AbyssRageInterval=28, AbyssUndertowWarning=5;
    public const float AbyssHaste=.25f, AbyssWeaponPower=1.20f, AbyssUnitHealth=1.28f;
    public const int AbyssCastleHealth=19000;
    public const string AbyssQueenCode="n00S";
    internal const float AbyssWorldEast=6144f/30, AbyssWorldNorth=3584f/30;
    static readonly Color AbyssCyan=new Color(.20f,.95f,.76f);
    static readonly string[] AbyssRaidRoster={"n00X","n00J","n004","n00S","n00H","n007","n01P"};
    static readonly HashSet<string> AbyssPanzerUnits=new HashSet<string>{
        "n00I","n00J","n00K","n002","n00X","n004","n01P","n00W","n01Q","n01R"
    };
    const string AbyssPanzerUsed="mythic_abyss_panzer_used", AbyssSiegeTimer="mythic_abyss_siege";
    static readonly JObject AbyssFrenzy=new JObject{
        ["id"]="mythic_abyss_frenzy",["name"]="Сброшенный панцирь",["kind"]="buff",
        ["description"]="Скорость атаки +40% до конца жизни.",["duration"]=float.PositiveInfinity,["haste"]=.4f
    };
    static readonly JObject AbyssSirenBond=new JObject{
        ["id"]="mythic_abyss_bond",["name"]="Зов глубин",["kind"]="buff",
        ["description"]="Боец вернётся на свою сторону после разрушения Оракула сирен.",["duration"]=12
    };
    static readonly JObject AbyssLullaby=new JObject{
        ["id"]="mythic_abyss_lullaby",["name"]="Песня впадины",["kind"]="debuff",
        ["description"]="Умения заглушены, скорость атаки −25%.",["duration"]=4,
        ["silence"]=true,["haste"]=-.25f
    };
    sealed class AbyssBond {public Unit victim;public Hut oracle;public float end;}
    sealed class AbyssHunter {public Hut target;public float expires;}
    List<AbyssBond> abyssBonds;
    Dictionary<int,AbyssHunter> abyssHunters;
    Hut abyssOracle,abyssTower,abyssHatchery,abyssSiegeYard;
    int[] abyssRaidKinds;
    int abyssHunterKind=-1;
    float abyssNextRaid=AbyssFirstUndertow,abyssNextCharm;
    public bool AbyssTrialActive {get;private set;}
    public int AbyssRaids {get;private set;}
    public bool AbyssSecondPhase {get;private set;}
    public int AbyssBondCount=>abyssBonds?.Count??0;
    public Vector2 AbyssRaidPoint=>CastlePosition(0)+new Vector2(24,AbyssRaids%2==0?18:-18);
    public float AbyssRaidIn=>Mathf.Max(0,abyssNextRaid-Elapsed);
    public bool AbyssRaidWarning=>AbyssTrialActive&&AbyssRaidIn<=AbyssUndertowWarning;

    void BeginAbyssTrial(){
        AbyssTrialActive=(string)ChallengeRules?["id"]==AbyssTrial&&TeamNaga[1];
        if(!AbyssTrialActive)return;
        AbyssRaids=0;
        abyssNextRaid=AbyssFirstUndertow;
        abyssBonds=new List<AbyssBond>(4);
        abyssHunters=new Dictionary<int,AbyssHunter>(4);
        abyssRaidKinds=Array.ConvertAll(AbyssRaidRoster,raw=>Array.FindIndex(Lines,line=>line.unit==raw));
        abyssHunterKind=Array.FindIndex(Lines,line=>line.unit=="n01R");
        var castle=CastlePosition(1);
        abyssOracle=PlaceChallengeStructure("h00M",castle+new Vector2(-19,12),2200);
        abyssTower=PlaceChallengeStructure("h00N",castle+new Vector2(-28,-10),2500);
        abyssHatchery=AbyssProducer("h00F",castle+new Vector2(-12,21),1250,17);
        abyssSiegeYard=AbyssProducer("h01B",castle+new Vector2(-10,-21),1550,32);
    }

    Hut AbyssProducer(string raw,Vector2 point,float hp,float firstSpawn){
        var hut=PlaceChallengeStructure(raw,point,hp);
        hut.spent=hut.baseCost=Lines[hut.kind].cost;
        hut.spawnLeft=firstSpawn;
        return hut;
    }

    bool AbyssBlocksPlot(Vector2 point)=>AbyssTrialActive&&(
        abyssOracle.hp>0&&(abyssOracle.position-point).sqrMagnitude<Cell*Cell||
        abyssTower.hp>0&&(abyssTower.position-point).sqrMagnitude<Cell*Cell||
        abyssHatchery.hp>0&&(abyssHatchery.position-point).sqrMagnitude<Cell*Cell||
        abyssSiegeYard.hp>0&&(abyssSiegeYard.position-point).sqrMagnitude<Cell*Cell);

    public bool AbyssWaterAffects(Unit unit)=>AbyssTrialActive&&unit.team==0&&
        !Flying(unit)&&!Invulnerable(unit)&&Lines[unit.kind].category!="visual"&&Lines[unit.kind].unit!="YTct"&&
        NativeMapTerrain.Contains(unit.position.x*30,unit.position.y*30);
    float AbyssMoveFactor(Unit unit)=>AbyssWaterAffects(unit)?1-AbyssSlow:1;
    float WeaponMissChance(Unit unit,Weapon weapon)=>Mathf.Clamp01(CentaurMissChance(unit,weapon)+(AbyssWaterAffects(unit)?AbyssMiss:0));
    public bool AbyssDeepForm(Unit unit)=>AbyssTrialActive&&unit.team==1&&unit.hp>0&&
        teamBuildings[1].Contains(Lines[unit.kind].building)&&Lines[unit.kind].category!="visual"&&
        !unit.effects.Exists(effect=>effect.id=="mythic_abyss_bond");
    public bool AbyssSeaHunter(Unit unit)=>AbyssTrialActive&&abyssHunters.ContainsKey(unit.id);
    float AbyssAttackHaste(Unit unit)=>AbyssDeepForm(unit)?AbyssHaste:0;
    float AbyssWeaponDamage(Unit unit)=>AbyssDeepForm(unit)?AbyssWeaponPower:1;
    void StrengthenAbyssSpawn(Unit unit){
        if(!AbyssDeepForm(unit))return;
        unit.hp*=AbyssUnitHealth;
        unit.maxHp*=AbyssUnitHealth;
    }

    bool AbyssOracleTarget(Unit unit,JToken cfg)=>NagaCandidate(unit,1)&&Valid(unit,1,Abilities["A00N"],false)&&
        !IsNagaHero(unit)&&F(Lines[unit.kind].stats,"level",1)<=F(cfg,"level_limit",5);

    void AbyssOracleCast(Hut oracle){
        if(Elapsed<abyssNextCharm)return;
        var cfg=Abilities["A00O"];
        Unit first=null,second=null;
        float near=70*70,next=near;
        foreach(var unit in Units){
            if(!AbyssOracleTarget(unit,cfg))continue;
            float distance=(unit.position-oracle.position).sqrMagnitude;
            if(distance<near){second=first;next=near;first=unit;near=distance;}
            else if(distance<next){second=unit;next=distance;}
        }
        if(first==null)return;
        abyssNextCharm=Elapsed+20;
        oracle.mana-=F(cfg,"mana");
        oracle.spellTimers["A00O"]=BuildingCooldown(oracle,F(cfg,"cd"));
        SpellsCast++;
        BindAbyssSiren(oracle,first);
        BindAbyssSiren(oracle,second);
        PulseAt(oracle.position,4.5f,AbyssCyan,"Зов глубин",1.3f);
    }

    void BindAbyssSiren(Hut oracle,Unit unit){
        if(unit==null||NagaShieldBlock(unit))return;
        ChangeAbyssTeam(unit,1);
        ApplyEffectData(unit,AbyssSirenBond,1);
        abyssBonds.Add(new AbyssBond{oracle=oracle,victim=unit,end=Elapsed+F(AbyssSirenBond,"duration")});
        NagaControls++;
        PulseAt(unit.position,2,AbyssCyan,"Подчинение",1.7f,"naga_charm");
    }

    void ChangeAbyssTeam(Unit unit,int team){
        PepeChangeTeam(unit);
        unit.effects.RemoveAll(effect=>effect.id=="mythic_abyss_bond");
        unit.team=team;
        InvalidateArmyCounts035();
        ApplyUltimate(unit);
        unit.combatTarget=unit.attackTarget=-1;
        unit.strikeLeft=unit.castLeft=-1;
        unit.pendingSpell=null;
        unit.leaveCastleLane=false;
        unit.onRoad=true;
        unit.direction=team==0?Vector2.right:Vector2.left;
    }

    void TickAbyssTide(){
        if(!AbyssTrialActive)return;
        for(int i=abyssBonds.Count-1;i>=0;i--){
            var bond=abyssBonds[i];
            if(bond.victim.hp>0&&bond.victim.team==1&&bond.oracle.hp>0&&Elapsed<bond.end)continue;
            abyssBonds.RemoveAt(i);
            if(bond.victim.hp<=0||bond.victim.team!=1)continue;
            ChangeAbyssTeam(bond.victim,0);
            PulseAt(bond.victim.position,2,AbyssCyan,"Оковы спали",.9f);
        }
        if(!AbyssSecondPhase&&CastleHp[1]>0&&CastleHp[1]<=AbyssCastleHealth*.5f){
            AbyssSecondPhase=true;
            abyssNextRaid=Mathf.Min(abyssNextRaid,Elapsed+AbyssRageInterval);
            PulseAt(CastlePosition(1),8,AbyssCyan,"Бездна наступает",2.2f);
        }
        if(Elapsed<abyssNextRaid)return;
        var point=AbyssRaidPoint;
        AbyssRaids++;
        abyssNextRaid=Elapsed+(AbyssSecondPhase?AbyssRageInterval:AbyssUndertowInterval);
        int spawned=0;
        int count=AbyssSecondPhase?abyssRaidKinds.Length:abyssRaidKinds.Length-1;
        for(int i=0;i<count;i++){
            if(ArmyCount(1)>=ArmyLimit)break;
            if(abyssRaidKinds[i]<0)continue;
            Spawn(1,abyssRaidKinds[i],point+new Vector2(i%2*3,i/2*3-4.5f));
            spawned++;
        }
        if(spawned>0)PulseAt(point,7,AbyssCyan,"Прорыв из глубин",2);
    }

    void TickAbyssPanzer(Unit unit){
        if(!AbyssDeepForm(unit)||unit.hp>unit.maxHp*.5f||!AbyssPanzerUnits.Contains(Lines[unit.kind].unit)||
            AbyssSeaHunter(unit)||!unit.abilities.Add(AbyssPanzerUsed))return;
        ApplyEffectData(unit,AbyssFrenzy,1);
        PulseAt(unit.position,4.5f,AbyssCyan,"Панцирь лопнул",1);
        using(var snapshot=SimulationBuffers035.Take(Units))foreach(var victim in snapshot)
            if(Valid(victim,1,null,false)&&!Flying(victim)&&(victim.position-unit.position).sqrMagnitude<=4.5f*4.5f)
                DamageUnit(victim,85,1,"normal",false,killer:unit);
    }

    void AbyssWeaponReleased(Unit source,Weapon weapon){
        if(!AbyssDeepForm(source)||Silenced(source))return;
        int targetId=source.attackTarget;
        string raw=Lines[source.kind].unit;
        if(raw=="n007"){TryLaunchAbyssHunter(source,targetId);return;}
        float cooldown=raw=="n00S"?14:raw=="n00H"?9:raw=="n01P"?11:0;
        if(cooldown==0||!Valid(FindUnit(targetId),1,null,false)||
            source.spellTimers.TryGetValue("mythic_abyss_weapon",out float wait)&&wait>0)return;
        var target=FindUnit(targetId);
        int hits=0;
        using(var snapshot=SimulationBuffers035.Take(Units))foreach(var victim in snapshot){
            if((victim.position-target.position).sqrMagnitude>7*7||!Valid(victim,1,null,false))continue;
            if(raw=="n00S"){
                ApplyEffectData(victim,AbyssLullaby,1);
                PulseAt(victim.position,1.5f,AbyssCyan,"",.85f,"naga_charm");
                if(++hits>=4)break;
            }else{
                if(victim==target||WeaponFor(source,victim)==null)continue;
                AddShot(1,source.kind,source.id,victim.id,source.position,victim.position,source.shotDamage*.65f,weapon,0,3);
                if(++hits>=(raw=="n01P"?2:1))break;
            }
        }
        if(hits==0)return;
        source.spellTimers["mythic_abyss_weapon"]=cooldown;
        string name=raw=="n00S"?"Песня впадины":raw=="n00H"?"Двойное жало":"Три пасти";
        PulseAt(target.position,4.5f,AbyssCyan,name,.85f);
    }

    void TryLaunchAbyssHunter(Unit unit,int target){
        if(!AbyssDeepForm(unit)||Lines[unit.kind].unit!="n007"||target>=0||abyssHunterKind<0||abyssHunters.Count>=4||
            ArmyCount(1)>=ArmyLimit||unit.spellTimers.TryGetValue(AbyssSiegeTimer,out float wait)&&wait>0)return;
        unit.spellTimers[AbyssSiegeTimer]=13;
        var hunter=Spawn(1,abyssHunterKind,unit.position);
        hunter.hp=hunter.maxHp=320;
        hunter.noBounty=hunter.noCorpse=true;
        abyssHunters.Add(hunter.id,new AbyssHunter{target=FindHut(target),expires=Elapsed+12});
        PulseAt(unit.position,2.8f,AbyssCyan,"Глубинная тварь",.8f);
    }

    bool TickAbyssHunter(Unit unit,float dt){
        if(!AbyssTrialActive||!abyssHunters.TryGetValue(unit.id,out var hunt))return false;
        if(unit.team!=1||Elapsed>=hunt.expires){SinkAbyssHunter(unit);return true;}
        if(hunt.target!=null&&(hunt.target.hp<=0||hunt.target.team!=0))hunt.target=null;
        var target=hunt.target?.position??BattleCastlePosition(0);
        float reach=hunt.target==null?CastleCollision+1:3.5f;
        if((target-unit.position).sqrMagnitude<=reach*reach){
            PulseAt(unit.position,4,AbyssCyan,"Удар из глубин",1.15f);
            if(hunt.target!=null)DamageHut(hunt.target,325,"siege",1);
            else DamageCastle(0,280,"siege");
            SinkAbyssHunter(unit);
            return true;
        }
        var step=ArmyWaypoint(unit,target,false);
        unit.direction=(step-unit.position).normalized;
        unit.position=Vector2.MoveTowards(unit.position,step,15*dt);
        unit.pose="Walk";
        return true;
    }

    void ForgetAbyssHunter(Unit unit){if(AbyssTrialActive)abyssHunters.Remove(unit.id);}

    void SinkAbyssHunter(Unit unit){
        abyssHunters.Remove(unit.id);
        unit.hp=0;
        unit.pose="Death";
        unit.deathProcessed=true;
        InvalidateArmyCounts035();
    }

    static float AbyssWaveEdge(Vector2 origin,Vector2 direction){
        float horizontal=direction.x==0?float.PositiveInfinity:
            (Mathf.Sign(direction.x)*AbyssWorldEast-origin.x)/direction.x;
        float vertical=direction.y==0?float.PositiveInfinity:
            (Mathf.Sign(direction.y)*AbyssWorldNorth-origin.y)/direction.y;
        return Mathf.Max(1,Mathf.Min(horizontal,vertical));
    }
}
}
