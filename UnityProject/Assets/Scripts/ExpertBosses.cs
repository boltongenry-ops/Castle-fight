using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class HumanMatch {
    internal const string YurneroRaw="expert_yurnero",HydraRaw="expert_hydra";
    internal const int YurneroHealth=5000,HydraHealth=4000;
    const string YurneroMirror="expert_mirror",YurneroWhirl="expert_whirl",YurneroBleed="expert_bleed",HydraVenom="expert_venom";
    const float WhirlRadius=12,WhirlDuration=5;
    static readonly JObject WhirlBleed=new JObject{
        ["id"]=YurneroBleed,["name"]="Кровотечение",["kind"]="debuff",["duration"]=5,["dps"]=14,["interval"]=1
    };
    static readonly JObject HydraPoison=new JObject{
        ["id"]=HydraVenom,["name"]="Яд гидры",["kind"]="poison",["duration"]=4,["dps"]=12,["interval"]=1,["speed"]= -.35f
    };
    Unit expertFinalBoss;
    bool expertCastleFallen;
    internal bool ExpertEmergencePending{get;private set;}
    internal bool ExpertCastleFallen=>expertCastleFallen;
    internal Unit ExpertFinalBoss=>expertFinalBoss;
    float whirlEnds,whirlTick,whirlReady,mirrorReady;

    internal static JObject ExpertFinaleCatalog(JObject catalog,string trial){
        bool orc=trial==HordeTrial;
        var result=(JObject)catalog.DeepClone();
        var rows=(JArray)result["lines"];
        var source=rows.OfType<JObject>().First(row=>(string)row["unit"]==(orc?"o005":"n01P"));
        var boss=(JObject)source.DeepClone();
        boss["building"]=boss["unit"]=orc?YurneroRaw:HydraRaw;
        boss["name"]=boss["unitName"]=orc?"Юрнеро · Багровый клинок":"Гидра Бездны";
        boss["category"]="boss";boss["base"]=false;boss["allowed"]=false;boss["slots"]=0;
        boss["abilities"]=new JArray();boss["buildingStats"]=new JObject();
        rows.Add(boss);
        var stats=(JObject)boss["stats"];
        stats["hp"]=orc?YurneroHealth:HydraHealth;
        if(!orc)stats["speed"]=0;
        stats["armor_type"]=orc?"light":"heavy";stats["armor"]=orc?6:8;
        stats["unit_types"]="hero";stats["level"]=10;stats["hp_regen"]=0;
        stats["max_mana"]=stats["start_mana"]=stats["mana_regen"]=0;
        stats.Remove("ability");
        stats["abilities"]=orc?new JArray(YurneroMirror,YurneroWhirl):new JArray(HydraVenom);
        var weapon=(JObject)stats["weapons"][0];
        weapon["damage"]=stats["damage"]=orc?140:115;
        weapon["attack_type"]=stats["attack_type"]=orc?"hero":"chaos";
        weapon["range"]=orc?4.5f:26;weapon["cooldown"]=orc?1.15f:1.8f;
        weapon["can_structure"]=true;weapon["weapon_type"]=orc?"normal":"missile";
        weapon.Remove("splash");
        var abilities=(JObject)result["abilities"];
        if(orc){
            abilities[YurneroMirror]=new JObject{["id"]=YurneroMirror,["name"]="Зеркальные образы",["kind"]="expert_boss",["cd"]=20,
                ["description"]="Два образа живут 12 секунд, имеют по 1000 HP и наносят 40% урона Юрнеро."};
            abilities[YurneroWhirl]=new JObject{["id"]=YurneroWhirl,["name"]="Вихрь клинков",["kind"]="expert_boss",["cd"]=16,
                ["description"]="5 секунд наносит всем врагам вокруг по 40 урона в секунду. Накладывает кровотечение: по 14 урона в секунду на 5 секунд."};
        }else{
            abilities[HydraVenom]=(JObject)HydraPoison.DeepClone();
            abilities[HydraVenom]["description"]="До трёх целей за атаку. Яд на 4 секунды замедляет на 35% и наносит по 12 урона в секунду.";
        }
        return result;
    }

    bool ExpertFinaleCastleHit(int team,float amount){
        if(team!=1||!ExpertTrial||!(CursedHorde||AbyssTrialActive))return false;
        if(expertCastleFallen)return true;
        if(amount<CastleHp[1])return false;
        expertCastleFallen=true;
        CastleHp[1]=0;
        if(CursedHorde)ExpertEmergencePending=true;
        return true;
    }

    void PrepareExpertFinaleAfterCombat(){
        if(expertCastleFallen&&AbyssTrialActive&&expertFinalBoss==null)SpawnExpertFinalBoss(false);
    }

    bool ExpertFinaleBlocksVictory=>expertCastleFallen&&(ExpertEmergencePending||expertFinalBoss==null||expertFinalBoss.hp>0);
    internal static Vector2 YurneroEmergence=>CastlePosition(0)+new Vector2(16,-7);
    internal void ReleaseYurnero(){
        if(!ExpertEmergencePending)return;
        RevealYurnero();
        expertFinalBoss.pose="Stand";
        ExpertEmergencePending=false;
    }

    internal void RevealYurnero(){
        if(ExpertEmergencePending&&expertFinalBoss==null)SpawnExpertFinalBoss(true);
    }

    void SpawnExpertFinalBoss(bool orc){
        int kind=Kind(orc?YurneroRaw:HydraRaw);
        if(kind<0)throw new InvalidOperationException("Expert boss catalog is missing.");
        Vector2 point=orc?YurneroEmergence:CastlePosition(1);
        expertFinalBoss=Spawn(1,kind,point);
        expertFinalBoss.onRoad=expertFinalBoss.noBounty=expertFinalBoss.noCorpse=true;
        expertFinalBoss.direction=Vector2.left;
        expertFinalBoss.pose="Stand";
        mirrorReady=Elapsed+.9f;whirlReady=Elapsed+3.5f;
        if(!orc)PulseAt(point,15,new Color(.25f,.93f,.56f),"Гидра Бездны",1.4f);
    }

    bool RescueImmune(Unit unit)=>unit==WinterBoss||unit==expertFinalBoss;
    bool ExpertFinaleFocus(Unit unit)=>unit.team==0&&ExpertFinaleBlocksVictory&&expertFinalBoss?.hp>0;

    bool TickExpertBoss(Unit unit,float dt){
        if(unit!=expertFinalBoss||Lines[unit.kind].unit!=YurneroRaw)return false;
        if(Elapsed<whirlEnds){
            unit.pose="Spin";
            if(!Rooted(unit))unit.position=Vector2.MoveTowards(unit.position,CastlePosition(0),MoveSpeed(unit)*.35f*dt);
            if(Elapsed>=whirlTick){
                whirlTick++;
                using var wave=SimulationBuffers035.Take(Units);
                foreach(var enemy in wave){
                    if(enemy.team==unit.team||enemy.hp<=0||!ChaosFighter(enemy)||(enemy.position-unit.position).sqrMagnitude>WhirlRadius*WhirlRadius)continue;
                    if(DamageUnit(enemy,40,unit.team,"hero",false,killer:unit,ignoreArmor:true)>0&&enemy.hp>0){
                        ApplyEffectData(enemy,WhirlBleed,unit.team);
                        PulseAt(enemy.position,1.2f,Color.red,"",.42f,"blackhand:bleed",enemy.id);
                    }
                }
            }
            return true;
        }
        if(Disarmed(unit)||Ethereal(unit))return false;
        if(Elapsed<whirlReady&&Elapsed<mirrorReady)return false;
        float nearest=float.PositiveInfinity;
        foreach(var enemy in Units)
            if(enemy.team!=unit.team&&enemy.hp>0&&ChaosFighter(enemy)&&!Hidden(enemy))
                nearest=Mathf.Min(nearest,(enemy.position-unit.position).sqrMagnitude);
        if(Elapsed>=whirlReady&&nearest<=WhirlRadius*WhirlRadius){
            whirlReady=Elapsed+16;whirlEnds=Elapsed+WhirlDuration;whirlTick=Elapsed;
            unit.strikeLeft=unit.castLeft=-1;unit.attackSerial++;unit.pose="Spin";
            PulseAt(unit.position,WhirlRadius,new Color(.97f,.23f,.12f),"Вихрь клинков",.8f);
            return true;
        }
        if(Elapsed<mirrorReady||nearest>24*24)return false;
        mirrorReady=Elapsed+20;
        unit.strikeLeft=unit.castLeft=-1;unit.attackSerial++;
        unit.castPause=.55f;unit.finalCastPose=unit.pose="Spell";
        for(int side=-1;side<=1;side+=2){
            if(ArmyCount(1)>=ArmyLimit)break;
            var image=Spawn(1,unit.kind,unit.position+new Vector2(1,side*4.5f));
            image.hp=image.maxHp=YurneroHealth/5;
            image.attackOverride=Lines[image.kind].damage*.40f;
            image.summonLeft=12;image.summoned=image.noBounty=image.noCorpse=true;
            image.abilities.Clear();
            PulseAt(image.position,2.3f,new Color(.78f,.29f,.92f),"",.75f);
        }
        return true;
    }

    bool ExpertHydraAttack(Unit unit,Weapon weapon){
        if(unit!=expertFinalBoss||Lines[unit.kind].unit!=HydraRaw)return false;
        int primary=unit.attackTarget;
        var targets=new int[3]{int.MinValue,int.MinValue,int.MinValue};
        var ranges=new float[3]{float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity};
        int start=0;
        var victim=FindUnit(primary);
        if(TargetAlive(primary)&&(victim==null||victim.team!=unit.team&&WeaponFor(unit,victim)!=null)&&
            Vector2.Distance(unit.position,TargetPosition(primary,unit.team))<=weapon.range+(primary==-1?CastleCollision:primary< -1?2:1)){
            targets[0]=primary;start=1;
        }
        foreach(var target in Units){
            if(target.id==primary||target.team==unit.team||target.hp<=0||OnForcedRetreat(target)||WeaponFor(unit,target)==null)continue;
            float square=(target.position-unit.position).sqrMagnitude;
            if(square>(weapon.range+1)*(weapon.range+1))continue;
            for(int i=start;i<targets.Length;i++){
                if(square>ranges[i]||square==ranges[i]&&target.id>=targets[i])continue;
                for(int j=targets.Length-1;j>i;j--){targets[j]=targets[j-1];ranges[j]=ranges[j-1];}
                targets[i]=target.id;ranges[i]=square;break;
            }
        }
        for(int i=0;i<targets.Length&&targets[i]!=int.MinValue;i++){
            int target=targets[i];
            var shot=AddShot(unit.team,unit.kind,unit.id,target,unit.position,TargetPosition(target,unit.team),unit.shotDamage,weapon,0,3);
            shot.startHeight=3.8f;
        }
        return true;
    }

    void ExpertHydraImpact(Shot shot,Unit target,float healthBefore){
        if(target==null||target.team==shot.team||target.hp<=0||target.hp>=healthBefore)return;
        ApplyEffectData(target,HydraPoison,shot.team);
        PulseAt(target.position,1.4f,new Color(.54f,.98f,.22f),"",.52f,"poison",target.id);
    }
}
}
