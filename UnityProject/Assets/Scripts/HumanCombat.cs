using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    public Unit Spawn(int team,int kind,Vector2 position,bool production=false,Hut producer=null) {
        if(Lines[kind].unit=="u00F"){
            int elder=Array.FindIndex(Lines,line=>line.unit=="h030");
            if(elder>=0)kind=elder;
        }
        var u=new Unit {id=nextId++,team=team,kind=kind,position=position,previous=position,hp=Lines[kind].hp,direction=team==0?Vector2.right:Vector2.left};Ensure(u);Units.Add(u);InvalidateArmyCounts035();OnSpawnIndexed033(u);MechCreated(u,production,producer);UndeadCreated(u);ElementalCreated(u,production);NightCreated(u);ApplyUltimate(u);StrengthenAbyssSpawn(u);StrengthenQuackUnit(u);Spawned++;if(production)ChaosProduced(u);
        // Godot gameplay.route_through_arch: leave the keep's column before
        // heading to the road. Flying units can cross the footprint directly.
        if(!Flying(u)&&Mathf.Abs(position.x-(team==0?-5120f/30:5120f/30))<320f/30&&Mathf.Abs(position.y)>256f/30){u.leaveCastleLane=true;u.exitWaypoint=new Vector2(team==0?-4736f/30:4736f/30,position.y);}
        if(production&&!u.abilities.Contains("A070"))foreach(var shrine in Huts.Where(h=>h.team==team&&h.paid&&h.hp>0&&Lines[h.kind].building=="h05G").Take(2).ToArray())if(Roll()<.17f&&ArmyCount(team)+reservedWaveSlots<ArmyLimit){Spawn(team,kind,position);ShrineExtras++;PulseAt(position,2,Color.yellow,"Призыв к оружию");}
        return u;
    }
    public void Ensure(Unit u) {
        if(u.maxHp>0)return;var l=Lines[u.kind];u.maxHp=l.hp;u.hp=Mathf.Min(u.hp,u.maxHp);u.maxMana=F(l.stats,"max_mana");u.mana=F(l.stats,"start_mana");u.manaRegen=F(l.stats,"mana_regen");u.armor=l.armor;
        foreach(var id in Strings(l.stats["abilities"]))Grant(u,id);
        if(l.stats["ability_ranks"] is JObject ranks)foreach(var pair in ranks.Properties())u.abilityLevels[pair.Name]=(int?)pair.Value??1;
        if(l.unit=="cU16")u.abilityLevels["CT05"]=2;ApplyNatureResilience(u);
    }
    public void Grant(Unit u,string id) {
        if(!u.abilities.Add(id))return;if(!u.abilityLevels.ContainsKey(id))u.abilityLevels[id]=1;if(id=="A03G")PulseAt(u.position,1,Color.white,"",.35f,"defend");var c=Abilities[id];if(c==null)return;var p=c["profile"];string k=(string)c["kind"];
        foreach(string child in ((string)c["fields"]?["spb1"]??"").Split(new[]{','},StringSplitOptions.RemoveEmptyEntries))Grant(u,child);
        if(k=="resist")u.spellResist=Mathf.Max(u.spellResist,F(c,"power"));
        if(k=="critical"&&F(p,"chance")>=u.critChance){u.critChance=F(p,"chance");u.critMultiplier=F(p,"multiplier",1);}
        if(k=="evasion")u.evasion=Mathf.Max(u.evasion,F(p,"chance"));
        if(k=="self_passive"){u.maxHp+=F(c,"health_bonus");u.hp+=F(c,"health_bonus");u.armor+=F(c,"armor");}
    }
    public bool Invulnerable(Unit u)=>u.pandaSplit||UnitConfig035(u.kind).invulnerable||u.abilities.Contains("Avul");
    public bool Flying(Unit u){if(!UnitConfig035(u.kind).flying)return false;foreach(var e in u.effects)if(e.left>0&&e.ground)return false;return true;}
    public bool Rooted(Unit u){foreach(var e in u.effects)if(e.left>0&&e.root)return true;return false;}
    public bool Organic(Unit u)=>UnitConfig035(u.kind).organic;
    public float Height(Unit u)=>u.natureSpirit?1:Flying(u)?Lines[u.kind].flyHeight:0;
    public float Stat(Unit u,string name) {
        float value=(name=="haste"?ChaosHaste(u):0)+UltimateMod(u,name);double effects035=0;foreach(var e in u.effects)effects035+=name=="armor"?e.armor:name=="damage"?e.WeaponDamageModifier:name=="haste"?e.haste:name=="speed"?e.speed:name=="incoming"?e.incoming:0;value+=(float)effects035;
        if(name=="armor")value+=u.auraArmor+u.auraArmorNegative+NerubArmorOverlap(u);if(name=="haste")value+=u.auraHaste+u.auraHasteNegative;if(name=="speed")value+=u.auraSpeed+u.auraSpeedNegative;
        foreach(var id in AbilityView035.Get(u,Abilities).passives){var c=Ranked(u,id);if((string)c?["kind"]=="self_passive"&&name!="armor")value+=F(c,name);}
        return value;
    }
    public float MoveSpeed(Unit u)=>BaseMoveSpeed(u)*WinterMoveFactor(u)*(HordeRaging(u)?1.25f:1)*(FuneralChoir?1-.65f*Burial(u):1)*AbyssMoveFactor(u);
    float BaseMoveSpeed(Unit u){var l=Lines[u.kind];if(u.elementalArmed)return Rooted(u)?0:15;float fixedMove=-1;foreach(var e in u.effects)if(e.left>0){float candidate=F(e.config,"move_override");if(candidate>0&&(fixedMove<0||candidate<fixedMove))fixedMove=candidate;}if(fixedMove>=0)return Rooted(u)?0:fixedMove;return l.speed<=0||Rooted(u)?0:Mathf.Clamp((l.speed+(TeamNaga[u.team]&&Aspects[u.team]==0?-20f/30:0))*(1+ItemPassive(u.team,"speed_bonus",u))*Mathf.Max(0,1+Stat(u,"speed")+(u.nightInvisible?u.nightSpeed:0)),UnitConfig035(u.kind).minSpeed,UnitConfig035(u.kind).maxSpeed);}
    public float AttackSpeed(Unit u)=>Mathf.Clamp(1+(Lines[u.kind].unit=="bU02" && u.hp*10<u.maxHp*3?.4f:0)+AbyssAttackHaste(u)+Stat(u,"haste")+ItemPassive(u.team,"haste_bonus",u),.2f,5)*WinterAttackFactor(u);
    public Weapon WeaponFor(Unit u,Unit target=null,bool structure=false){
        if(Ethereal(u)||Disarmed(u))return null;
        foreach(var w in u.elementalWeapons??Lines[u.kind].weapons)if((target==null||!HordeImmuneToWeapon(target,w)&&!Invulnerable(target)&&!CorruptedAirborne(target)&&!Hidden(target)&&NightCanDetect(u.team,target)&&Lines[target.kind].unit!="n02E")&&(target==null||Lines[target.kind].unit!="YTct"||w.targets.Contains("tree"))&&(target==null||!Ethereal(target)||w.attack=="magic"||w.attack=="spells")&&(structure?w.structure:target!=null&&Flying(target)?w.air:w.ground))return w;
        return null;
    }
    int HutTarget(Hut h)=>-h.id-2;
    Hut FindHut(int target)=>FindHutFast033(target);
    Unit FindUnit(int target)=>FindUnitFast033(target);
    Vector2 TargetPosition(int target,int sourceTeam)=>FindUnit(target)?.position??FindHut(target)?.position??BattleCastlePosition(1-sourceTeam);
    static bool OnForcedRetreat(Unit unit)=>unit.warlockPhase==1||unit.bearPhase==0;
    bool TargetAlive(int target)=>target==-1||FindUnit(target)!=null||FindHut(target)!=null;
    bool AirTarget(int target)=>FindUnit(target) is Unit u&&Flying(u);
    static Vector2 ArmyWaypoint(Unit unit,Vector2 goal,bool marching){
        var from=unit.position;
        if(marching&&unit.leaveCastleLane){
            if((from-unit.exitWaypoint).sqrMagnitude>.16f)return unit.exitWaypoint;
            unit.leaveCastleLane=false;
        }
        float direction=Mathf.Sign(goal.x-from.x);
        foreach(float wall in direction>0?EastboundWalls:WestboundWalls){
            if((wall-from.x)*direction<=0||(goal.x-wall)*direction<=0)continue;
            return new Vector2(wall+direction*.10f,0);
        }
        return goal;
    }
    static readonly float[] EastboundWalls={-3520f/30,-2016f/30,2016f/30,3520f/30};
    static readonly float[] WestboundWalls={3520f/30,2016f/30,-2016f/30,-3520f/30};
    public void TickUnit(Unit u,float dt) {var before=u.position;TickUnitCore(u,dt);ObserveCentaurStride(u,before,dt);}
    void TickUnitCore(Unit u,float dt) {
        var l=Lines[u.kind];
        if(u.pandaSplit||Hidden(u)||Lines[u.kind].category=="visual")return;
        if(TickNatureBear(u,dt)||TickWarlock(u,dt))return;
        if(u.stun>0||Disabled(u)){u.strikeLeft=-1;u.pendingSpell=null;u.castLeft=-1;u.pose="Stand";return;}
        if(TickHordeActor(u,dt)||TickAbyssHunter(u,dt))return;
        TickAbyssPanzer(u);
        if(TickElementalWind(u,dt))return;TickMechPassiveFire(u);TickElvenPassive(u);if(TickMechUnit(u,dt))return;
        if(u.castLeft>=0){u.castLeft-=dt;u.pose=u.finalCastPose??"Spell";if(u.castLeft<=0){var id=u.pendingSpell;u.pendingSpell=null;u.castLeft=-1;bool ok=Cast(u,id,u.spellTarget);u.spellTimers[id]=ok?SpellCooldown(u,id):.25f;}return;}
        if(u.castPause>0){u.pose=u.finalCastPose??"Spell";return;}u.finalCastPose=null;
        if(Ethereal(u)||Disarmed(u))u.strikeLeft=-1;
        if(u.strikeLeft>=0&&FindUnit(u.attackTarget) is Unit fleeing&&OnForcedRetreat(fleeing)&&
            (fleeing.position-u.position).sqrMagnitude>Mathf.Pow((u.pendingWeapon?.range??0)+1,2)){
            u.strikeLeft=-1;u.cooldown=0;u.attackPoseLeft=0;
        }
        if(u.strikeLeft>=0){u.strikeLeft-=dt;if(u.strikeLeft<=0){Release(u);u.strikeLeft=-1;}u.pose=u.attackPoseLeft>0?"Attack":"Stand";return;}
        if(BlackHandCast(u)||PepeTryHeal(u)||TryAutocast(u))return;if(TickNightHunt(u,dt))return;
        if(Ethereal(u)||Disarmed(u)){u.strikeLeft=-1;var etherealGoal=BuilderWaypoint(u.position,BattleCastlePosition(1-u.team));u.direction=(etherealGoal-u.position).normalized;u.position=Vector2.MoveTowards(u.position,etherealGoal,MoveSpeed(u)*dt);u.pose="Walk";return;}
        u.onRoad=true; // Eligible for attack-move acquisition from the first combat step.
        var target=FindUnit(u.combatTarget);
        if(target!=null) {
            float keepMax=l.acquisitionRange+2.333333f;
            if(target.team==u.team||OnForcedRetreat(target)||(target.position-u.position).sqrMagnitude>keepMax*keepMax||WeaponFor(u,target)==null)target=null;
        }
        if(target==null) {
            float searchMax=l.acquisitionRange;float searchMaxSqr=searchMax*searchMax;
            Unit best=null;bool indexed=false;
            if(TryFindBestAny033(u.position,searchMaxSqr,u.team,false,v=>!OnForcedRetreat(v)&&WeaponFor(u,v)!=null,out best))indexed=true;
            if(!indexed) {
                float bestSqr=float.MaxValue;
                for(int i=0;i<Units.Count;i++) {var v=Units[i];if(v.team==u.team||v.hp<=0||OnForcedRetreat(v))continue;var d=v.position-u.position;float s=d.sqrMagnitude;if(s>searchMaxSqr||s>=bestSqr)continue;if(WeaponFor(u,v)==null)continue;best=v;bestSqr=s;}
            }
            target=best;
        }
        target=NerubHuntTarget(u,QuackFocusMarked(u,target));if(target!=null)u.skirmish=true;
        int targetId=target?.id??-1;
        Weapon weapon=WeaponFor(u,target,target==null);
        float acquire=l.acquisitionRange;
        if((u.team==0?u.position.x:-u.position.x)+acquire>=2048f/30){
            var siegeWeapon=target==null?weapon:WeaponFor(u,null,true);
            if(siegeWeapon!=null){
                float best=target==null?acquire*acquire:Mathf.Min(acquire*acquire,(target.position-u.position).sqrMagnitude);
                foreach(var hut in Huts){
                    if(hut.team==u.team||hut.hp<=0||!hut.paid)continue;
                    if(target!=null&&(Lines[hut.kind].buildingWeapons?.Length??0)==0&&!IsProtocolStation(hut))continue;
                    float distanceSqr=(hut.position-u.position).sqrMagnitude;
                    if(distanceSqr>=best)continue;
                    if(VisionTest!=null&&!VisionTest(u.team,hut.position))continue;
                    targetId=HutTarget(hut);weapon=siegeWeapon;best=distanceSqr;
                }
            }
        }
        u.combatTarget=targetId;
        bool canStrike=weapon!=null;
        bool stationTarget=LastProtocol&&targetId<-1&&IsProtocolStation(FindHut(targetId));
        Vector2 destination=TargetPosition(targetId,u.team),delta=destination-u.position;
        float reach=(weapon?.range??0)+(targetId==-1?CastleCollision:targetId< -1?2:1);
        float distance=delta.magnitude;
        bool ground=!Flying(u);
        if(delta.sqrMagnitude>.001f)u.direction=delta.normalized;
        if(distance<=reach+(targetId!=-1&&u.pose=="Attack"?1:0)) {if(canStrike&&u.cooldown<=0)BeginAttack(u,weapon,targetId);u.pose=u.attackPoseLeft>0?"Attack":"Stand";}
        else {
            if(Rooted(u)){u.pose="Stand";return;}
            u.pose="Walk";
            Vector2 waypoint=ground?ArmyWaypoint(u,destination,targetId==-1):destination;
            bool approachingCastle=ground&&(targetId==-1||stationTarget)&&waypoint==destination&&distance<=Mathf.Min(24,reach+12)
                &&delta.x*(u.team==0?1:-1)>0;
            if(approachingCastle){
                var slot=CastleApproach038(u,destination,reach);
                var travel=slot-u.position;
                u.position=Vector2.MoveTowards(u.position,slot,MoveSpeed(u)*dt);
                if(travel.sqrMagnitude>.01f)u.direction=travel.normalized;
                return;
            }
            Vector2 advance=waypoint-u.position;
            Vector2 heading=advance.normalized;
            // Reach the gate itself before applying a new detour. Otherwise the last
            // two units of travel can become an oscillation beside an occupied gate.
            bool enteringPassage=ground&&waypoint!=destination&&advance.sqrMagnitude<=4;
            Vector2 direction=ground&&!enteringPassage?GroundSeparation(u,heading):heading;
            Vector2 start=u.position;
            float step=Mathf.Min(MoveSpeed(u)*dt,Mathf.Max(0,distance-reach+.1f));
            var next=enteringPassage?Vector2.MoveTowards(start,waypoint,step):
                start+direction*Mathf.Min(advance.magnitude,step);
            u.position=next;
            if((next-start).sqrMagnitude>.000001f)u.direction=(next-start).normalized;
        }
    }
    void BeginAttack(Unit u,Weapon weapon,int target) {
        NightOnAttacked(FindUnit(target));u.pendingWeapon=weapon;float haste=AttackSpeed(u);u.cooldown=(u.cooldownOverride>0?u.cooldownOverride:weapon.cooldown)/haste;u.strikeLeft=weapon.point/haste;u.attackTarget=target;u.attackSerial++;u.attackPoseLeft=(weapon.point+weapon.backswing)/haste;
        float flat=u.abilities.Sum(id=>(string)Abilities[id]?["kind"]=="self_passive"?F(Abilities[id],"damage_bonus"):0);u.shotDamage=((u.attackOverride>=0?u.attackOverride:weapon.damage)+flat+ChaosSealBonus(u)+NightFlat(u))*Mathf.Max(0,1+Stat(u,"damage"))*Mathf.Max(0,1+u.auraDamage+u.auraDamageNegative)*InventoryDamage(u);u.shotDamage*=ElvenWeaponFactor(u.team,false)*AbyssWeaponDamage(u);u.shotDamage+=NightOpening(u);u.shotStun=0;bool elvenCritical=u.critChance>0&&Roll()<u.critChance;if(elvenCritical)u.shotDamage*=u.critMultiplier;
        var victim=FindUnit(target);if(victim!=null)foreach(var id in u.abilities){var c=Abilities[id];var p=c?["profile"];if((string)c?["kind"]!="bash"||!(Flying(victim)?((string)p?["targets"]??"").Contains("air"):((string)p?["targets"]??"").Contains("ground")))continue;if(Roll()<F(p,"chance")){u.shotDamage+=F(p,"bonus");u.shotStun=F(p,Lines[victim.kind].armorType=="hero"?"hero_duration":"duration");}}if(elvenCritical&&TeamElven[u.team]&&Aspects[u.team]==1)u.shotDamage*=1.25f;CentaurOpening(u);
    }
    void Release(Unit u) {
        if(!TargetAlive(u.attackTarget))return;var w=u.pendingWeapon;if(w==null)return;CorruptedEnhance(u,ref w);
        if(BlackHandRelease(u))return;
        if(ChaosFighter(u))u.chaosAttackingUntil=Elapsed+1;Vector2 end=TargetPosition(u.attackTarget,u.team);bool storm=ElementalStormReleased(u,u.attackTarget);bool heat=MechHeatReleased(u,u.attackTarget);float miss=WeaponMissChance(u,w);if(miss>0&&Roll()<miss){if(storm){if(w.type=="normal"||w.type=="instant")ElementalStormBurst(u.team,end,u.attackTarget,u);else{var failed=AddShot(u.team,u.kind,u.id,u.attackTarget,u.position,end,0,w,0,3);failed.chaosSpell="elemental_miss";failed.elementalStorm=true;failed.elementalStormSource=u;}}if(heat){if(w.type=="normal"||w.type=="instant")MechHeatBurst(u.team,end,u.attackTarget);else {var failed=AddShot(u.team,u.kind,u.id,u.attackTarget,u.position,end,0,w,0,3);failed.chaosSpell="mech_miss";failed.mechHeat=true;}}ChaosMisses++;PulseAt(u.position,1,Color.white,"Промах");return;}
        AbyssWeaponReleased(u,w);
        if(w.type=="normal")BattleAudio.For(this)?.Swing(Lines[u.kind].unit,u.position);
        if(w.type=="normal"||w.type=="instant") {
            if(Vector2.Distance(u.position,end)<=w.range+(u.attackTarget==-1?CastleCollision:u.attackTarget< -1?2:1))ResolveAttack(u.id,u.team,u.attackTarget,u.shotDamage,w,u.shotStun);
            if(heat)MechHeatBurst(u.team,end,u.attackTarget);if(storm)ElementalStormBurst(u.team,end,u.attackTarget,u);
            if(w.type=="instant"){AddShot(u.team,u.kind,u.id,u.attackTarget,u.position,end,u.shotDamage,w,u.shotStun,1);RifleShots++;}
        } else {var released=AddShot(u.team,u.kind,u.id,u.attackTarget,u.position,end,u.shotDamage,w,u.shotStun,w.type.StartsWith("artillery")?2:3,u.abilities.Contains("A02J"));if((Lines[u.kind].unit=="bU03"||Lines[u.kind].unit=="bU04")&&++u.handBolts%(Lines[u.kind].unit=="bU03"?4:3)==0)released.chaosSpell="hand-bolt";if(IsHandStone(Lines[u.kind].unit)){released.chaosSpell="hand-stone";released.donorId=BlackHand.Ammo(Lines[u.kind].unit);}released.mechHeat=heat;released.elementalStorm=storm;released.elementalStormSource=storm?u:null;NorthernBarrage(u,w);NightBarrage(u,w,u.attackTarget);}
    }
    Shot AddShot(int team,int line,int owner,int target,Vector2 start,Vector2 end,float damage,Weapon w,float stun,int kind,bool incendiary=false) {
        var source=FindUnit(owner);var victim=FindUnit(target);var s=new Shot {id=nextId++,team=team,line=line,sourceId=owner,target=target,start=start,end=end,damage=damage,weapon=w,stun=stun,kind=kind,arc=w.arc,incendiary=incendiary,homing=w.homing&&!w.type.StartsWith("artillery"),duration=kind==1?.14f:w.speed<=0?.05f:Mathf.Max(.1f,Vector2.Distance(start,end)/w.speed),hit=kind==1,startHeight=(source==null?3:Height(source)+((Lines[source.kind].unit??"").StartsWith("cU",StringComparison.Ordinal)?2.7f:1.4f)),endHeight=victim==null?4:Height(victim)+.8f};Shots.Add(s);BattleAudio.For(this)?.LocalMissile(s,false);if(kind==2)ShellsFired++;return s;
    }
    void TickShots(float dt) {
        using(var snapshot035_1=SimulationBuffers035.Take(Shots))foreach(var shot in snapshot035_1) {
            BlackHandSplit(shot,dt);shot.age+=dt;if(shot.chaosSpell=="hand-shard"){shot.hit=shot.age>=shot.duration;continue;}if(shot.kind==1||shot.kind==4)continue;
            if(shot.homing&&TargetAlive(shot.target)){shot.end=TargetPosition(shot.target,shot.team);var v=FindUnit(shot.target);if(v!=null)shot.endHeight=Height(v)+.8f;shot.duration=shot.weapon!=null&&shot.weapon.speed<=0?.05f:Mathf.Max(.1f,Vector2.Distance(shot.start,shot.end)/Mathf.Max(1,shot.weapon?.speed??30));}
            if(shot.chaosSpell=="north_spell:A082"||shot.chaosSpell=="north_spell:A083")shot.duration=.05f;
            if(shot.hit||shot.age<shot.duration)continue;shot.hit=true;BattleAudio.For(this)?.LocalMissile(shot,true);if(shot.kind==2)ShellImpacts++;
            Weapon w=shot.weapon??Lines[shot.line==0?2:shot.line].weapons.FirstOrDefault();if(w==null)continue;
            if(BlackHandImpact(shot))continue;
            bool artillery=w.type.StartsWith("artillery");var victim=FindUnit(shot.target);if(ImpactFinalShot(shot)||ImpactElvenShot(shot)||ImpactElementalShot(shot)||ImpactMechShot(shot)||ImpactNorthernShot(shot))continue;if(shot.elementalStorm)ElementalStormBurst(shot.team,shot.end,shot.target,shot.elementalStormSource);if(shot.chaosSpell=="elemental_miss")continue;if(shot.mechHeat)MechHeatBurst(shot.team,shot.end,shot.target);if(shot.chaosSpell=="twins_blast"){ImpactChaosBlast(shot);continue;}if(shot.chaosSpell=="hell_fist"){ImpactChaosFist(shot);continue;}
            if(shot.stolenEffect!=null){ImpactOrcSteal(shot,victim);continue;}
            if(shot.kind==7){ImpactOrcAcid(shot);continue;}
            if(!string.IsNullOrEmpty(shot.effectId)){if(victim!=null&&((shot.effectId=="A0AR"||shot.effectId=="A0AS")?NatureDebuffTarget(shot.team,victim,shot.effectId,Abilities[shot.effectId]):OrcNetTarget(shot.team,victim))){ApplyEffect(victim,shot.effectId,shot.team);OrcNetHits++;PulseAt(victim.position,1.5f,Color.cyan,"Сеть");}continue;}
            if(shot.spell){if(victim!=null&&victim.team!=shot.team)DamageUnit(victim,shot.damage,shot.team);continue;}
            if(!artillery&&w.attack=="pierce"&&victim!=null&&victim.team!=shot.team&&victim.abilities.Contains("A03G")&&shot.reflections<32&&Roll()<F(Abilities["A03G"],"deflect")) {
                var owner=FindUnit(shot.sourceId);var hut=Huts.FirstOrDefault(h=>h.id==shot.sourceId&&h.hp>0);int returnTarget=owner?.id??(hut!=null?HutTarget(hut):-1000000);
                if(returnTarget!=-1000000){var reflected=AddShot(victim.team,victim.kind,victim.id,returnTarget,shot.end,owner?.position??hut.position,shot.damage,w,shot.stun,3);reflected.reflections=shot.reflections+1;PulseAt(victim.position,1,Color.yellow,"Отражение");continue;}
            }
            if(Lines[shot.line].building=="pB10"&&victim!=null&&victim.team!=shot.team&&victim.hp>0)
                PulseAt(victim.position,1.35f,PepeRules.ReedGreen,"",.42f,"pepe:reed-tower",victim.id);
            bool raven=shot.chaosSpell=="hand-raven"&&victim!=null;
            float healthBefore=raven?victim.hp:0;
            HandBolt(shot);
            if(!artillery&&TargetAlive(shot.target))ResolveAttack(shot.sourceId,shot.team,shot.target,shot.damage,w,shot.stun,!shot.nightSecondary,HandFighter(Lines[shot.line]));
            if(raven&&victim.hp<healthBefore&&HandTarget(victim,shot.team)){
                HandEffect(victim,shot.team,"BHSl",3);
                victim.effects.Find(e=>e.id=="BHSl").speed=-.35f;
                PulseAt(victim.position,1.45f,iron,"",.54f,"blackhand:raven",victim.id);
            }
            if(!ElvenWeaponPattern(shot))NightBounce(shot);if(w.full>0||w.half>0||w.quarter>0) {
                using(var snapshot035_2=SimulationBuffers035.Take(Units))foreach(var u in snapshot035_2)if(u.team!=shot.team&&u.hp>0&&(artillery||u.id!=shot.target)&&(Flying(u)?w.air:w.ground)){float f=Splash(w,Vector2.Distance(u.position,shot.end));if(f>0)ResolveAttack(shot.sourceId,shot.team,u.id,shot.damage*f,w,shot.stun,false);}
                using(var snapshot035_3=SimulationBuffers035.Take(Huts))foreach(var h in snapshot035_3)if(!(w.missile??"").StartsWith("corrupted:")&&Lines[shot.line].building!="n01J"&&h.team!=shot.team&&h.hp>0&&h.paid&&w.structure&&(artillery||HutTarget(h)!=shot.target)){float f=Splash(w,Vector2.Distance(h.position,shot.end));if(f>0){DamageHut(h,shot.damage*f,w.attack,shot.team);var owner=FindUnit(shot.sourceId);if(h.hp>0&&owner!=null&&Lines[owner.kind].unit=="n02A")h.dragonArmorRank=Mathf.Min(3,h.dragonArmorRank+1);}}
                int enemy=1-shot.team;float factor=Splash(w,Vector2.Distance(BattleCastlePosition(enemy),shot.end));if(!(w.missile??"").StartsWith("corrupted:")&&Lines[shot.line].building!="n01J"&&w.structure&&factor>0&&(artillery||shot.target!=-1)&&!ChoirCastleEvades(enemy))DamageCastle(enemy,shot.damage*factor,w.attack);
            }
            if(artillery&&Lines[shot.line].unit?.StartsWith("pU",StringComparison.Ordinal)==true)PepeArtilleryImpact(shot);
            if(artillery&&shot.incendiary)AddFire(shot.team,shot.end,Lines[shot.line].unit=="o001"?"A02J":"A02K");
        }
    }
    float Splash(Weapon w,float distance)=>distance<=w.full?1:distance<=w.half?w.halfFactor:distance<=w.quarter?w.quarterFactor:0;
    public float DamageUnit(Unit victim,float amount,int team,string attack="magic",bool spell=true,bool execute=false,Unit killer=null,bool suppressUndead=false,bool ignoreArmor=false,float minimumHp=0,bool suppressElemental=false,string attackTable=null,bool bypassResistance=false,bool pandaDebt=false) {
        if(victim==null||victim.hp<=0||amount<=0||CorruptedAirborne(victim))return 0;InvalidateArmyCounts035();Ensure(victim);
        if(!execute&&IsHordeWarchief(victim)&&(spell||attack=="magic"||attack=="spells"))return 0;
        if(!execute&&!spell&&!pandaDebt&&BerserkImmune(victim,attack))return 0;
        if(!execute&&(Invulnerable(victim)||Lines[victim.kind].unit=="n02E"))return 0;
        if(!execute&&!pandaDebt&&Ethereal(victim)){if(!spell&&attack!="magic"&&attack!="spells")return 0;if(spell&&attack=="magic")amount*=1.66f;}
        if(!execute&&!pandaDebt){if(!spell)foreach(var id in victim.abilities){var cfg=Abilities[id];if((string)cfg?["kind"]=="spiked")amount*=1-Mathf.Clamp01(F(cfg,"reduction"));}if(spell&&attack=="magic"&&(!bypassResistance||victim.spellResist>=1))amount*=1-Mathf.Clamp01(victim.spellResist);if(!spell&&attack=="pierce"&&victim.abilities.Contains("A03G"))amount*=F(Abilities["A03G"],"defend",.4f);amount*=Multiplier(attackTable??(spell&&attack=="magic"?"spells":attack),Lines[victim.kind].armorType);if(!ignoreArmor&&!(spell&&attack=="magic"))amount*=ArmorFactor(victim.armor+Stat(victim,"armor"));amount*=Mathf.Max(0,1+Stat(victim,"incoming"));}
        float unblockedAmount=amount;
        if(!spell&&!execute&&!pandaDebt)amount=CentaurPhysicalBlock(victim,amount);
        if(!spell&&!execute&&!pandaDebt)foreach(var id in victim.abilities){var c=Abilities[id];var p=c?["profile"];if((string)c?["kind"]=="hardened"&&Roll()<F(p,"chance"))amount=Mathf.Min(amount,Mathf.Max(F(p,"minimum",1),amount-F(p,"block")));}
        if(!execute&&!pandaDebt&&TeamMech[victim.team]&&Aspects[victim.team]==0&&MechFighter(victim))amount=Mathf.Min(amount,Mathf.Min(unblockedAmount,Mathf.Max(5,unblockedAmount-12)));
        if(!execute&&!pandaDebt)amount=PepeMitigate(victim,amount,spell,team);
        if(!spell&&!execute&&!pandaDebt&&team!=victim.team)amount=HandIronDamage(victim,amount);
        float before=victim.hp;
        if(!pandaDebt&&!suppressUndead&&killer!=null)UndeadAfterDamage(killer,victim,Mathf.Min(before,amount),before);
        if(victim.hp<=0)return before;
        if(!execute&&!pandaDebt)amount=PandaDefer(victim,amount,team,killer);
        victim.hp=Mathf.Max(Mathf.Min(before,minimumHp),victim.hp-amount);if(!pandaDebt&&victim.hp>0){NatureOnDamage(victim);if(before>victim.hp){TriggerBerserk(victim,before);if(team!=victim.team)TriggerHandIron(victim,before);NorthernRage(victim);if(victim.nightStealth>0){NightReveal(victim);if(killer!=null&&killer.team!=victim.team)victim.combatTarget=killer.id;}}}
        if(!pandaDebt){ElementalAfterDamage(killer,victim,Mathf.Max(0,before-victim.hp),suppressElemental);NerubAfterDamage(victim,amount,spell);PandaAfterDamage(victim,amount);}
        if(victim.hp<=0&&!victim.deathProcessed){victim.deathProcessed=true;ForgetAbyssHunter(victim);bool winterDefection=QueueWinterDefection(victim);BattleAudio.For(this)?.Death(Lines[victim.kind].unit,victim.position);victim.pose="Death";victim.pendingSpell=null;victim.strikeLeft=-1;Kills++;if(team>=0&&team<2&&team!=victim.team){TeamKills[team]++;if(!victim.noBounty)Gold[team]+=Mathf.Max(1,(int)(victim.maxHp/50));}
            NerubDied(victim);ElvenDied(victim,team);NightKilled(killer,victim);NightDeathAspect(victim);ElementalDied(victim,killer,team);CorruptedDied(victim);UndeadDied(victim,killer);MechDied(victim);OrcKilled(killer,victim);NagaOnDeath(victim);ChaosDeath(victim);
            if(winterDefection)victim.deadAge=4;
            else if(!victim.noCorpse&&!ElementalIllusion(victim)&&((int?)Lines[victim.kind].stats["death_type"]??0)%2==1&&Organic(victim))Corpses.Add(new Corpse {unit=victim,left=F(Lines[victim.kind].stats,"corpse_duration",90)});
        }
        return before-victim.hp;
    }
    public float Multiplier(string attack,string armorType)=>(float?)damageTable[attack]?[armorType]??1;
    public static float ArmorFactor(float armor)=>armor>=0?1/(1+.06f*armor):2-Mathf.Pow(.94f,-armor);
    void DamageHut(Hut h,float amount,string attack,int team,bool spell=false,bool ignoreArmor=false){
        if(h==null||h.hp<=0)return;
        var line=Lines[h.kind];
        amount*=Multiplier(spell&&attack=="magic"?"spells":attack,(string)line.buildingStats["armor_type"]??"fortified");
        if(!ignoreArmor&&!(spell&&attack=="magic"))amount*=ArmorFactor(BuildingArmor(h));
        h.hp-=amount;
        if(h.hp>0)return;
        int bounty=h.paid&&team>=0&&team<Gold.Length&&team!=h.team?BuildingBounty(h):0;
        if(bounty>0)Gold[team]+=bounty;
        BattleAudio.For(this)?.Death(line.building,h.position,true);
        RemoveHut(h);
        PulseAt(h.position,4,bounty>0?Color.yellow:new Color(1,.4f,.1f),bounty>0?"+ "+bounty+" золота":"Здание разрушено");
    }
    void DamageCastle(int team,float amount,string attack,bool spell=false){amount*=Multiplier(spell&&attack=="magic"?"spells":attack,CastleArmorType);if(!(spell&&attack=="magic"))amount*=ArmorFactor(CastleArmorFor(team));ApplyCastleDamage(team,amount);}
    void ResolveAttack(int ownerId,int team,int target,float amount,Weapon w,float stun,bool passives=true,bool handProjectile=false) {
        var victim=FindUnit(target);var owner=FindUnit(ownerId);
        if(victim!=null) {
            if(victim.team==team||CorruptedAirborne(victim)||BerserkImmune(victim,w.attack))return;Ensure(victim);float evasion=Mathf.Max(NightEvasion(victim),PepeEvasion(victim));if(evasion>0&&Roll()<evasion){if(PepeEvasion(victim)>0)PulseAt(victim.position,1.6f,Color.white,"Уклонение",.55f,"pepe:PEva",victim.id);else PulseAt(victim.position,1,Color.white,"Уклонение");return;}
            if(w.type=="normal")BattleAudio.For(this)?.Contact(owner!=null?Lines[owner.kind].unit:null,Lines[victim.kind].unit,false,victim.position);
            bool pepeHit=passives&&owner!=null&&owner.team==team&&Lines[owner.kind].unit?.StartsWith("pU",StringComparison.Ordinal)==true&&!Invulnerable(victim)&&(!Ethereal(victim)||w.attack=="magic"||w.attack=="spells");
            if(w.type=="normal")amount=BlackHandMelee(owner,victim,amount,passives);
            if(pepeHit)amount+=PepeWeaponBonus(owner,victim,w);
            float bonus=passives&&owner!=null&&owner.hp>0?CorruptedIncinerate(owner,victim):0;float before=victim.hp;DamageUnit(victim,amount*NerubHuntMultiplier(owner,victim),team,w.attack,false,false,owner);if(bonus>0&&victim.hp>0)DamageUnit(victim,bonus,team,"magic",true,false,owner);float dealt=before-victim.hp;ElvenBlood(owner,victim,dealt,w.attack);if(dealt>0&&victim.hp>0&&owner!=null&&Lines[owner.kind].unit=="n02A")NatureDragonArmor(victim);if(owner==null&&victim.hp>0&&Huts.FirstOrDefault(h=>h.id==ownerId&&h.hp>0) is Hut feedbackTower)foreach(string id in Lines[feedbackTower.kind].abilities){var cfg=Abilities[id];if((string)cfg?["kind"]=="feedback"){float drained=Mathf.Min(victim.mana,F(cfg,"power"));victim.mana-=drained;DamageUnit(victim,drained*F(cfg,"ratio",1),team);}}if(victim.hp>0&&stun>0){victim.stun=Mathf.Max(victim.stun,stun);PulseAt(victim.position,1,Color.yellow,"Оглушение");}
            if(passives&&owner!=null&&dealt>0&&victim.hp>0)BlackHandHit(owner,victim);
            if(pepeHit&&owner.hp>0)PepeWeaponHit(owner,victim,w);
            if(passives){CentaurAfterWeapon(owner,victim,dealt);var nativeTower=owner==null?Huts.FirstOrDefault(h=>h.id==ownerId&&h.hp>0):null;NerubAfterWeapon(owner,nativeTower,victim,dealt);PandaAfterWeapon(owner,nativeTower,victim);ElementalAfterWeapon(owner,dealt);}ElementalTowerHit(ownerId,team,victim,dealt);UndeadWeaponHit(owner,victim,dealt,w);MechTowerHit(ownerId,team,victim,dealt);NorthernAfterWeapon(ownerId,victim,w);if(owner!=null&&owner.hp>0&&passives){OnHit(owner,victim,dealt,w);if(w.type=="normal"&&victim.auraThorns>0)DamageUnit(owner,dealt*victim.auraThorns,victim.team,"magic",true,false,victim);if(w.type=="normal")foreach(var id in victim.abilities){var cfg=Abilities[id];if((string)cfg?["kind"]=="spiked"&&F(cfg,"reflect")>0){DamageUnit(owner,dealt*F(cfg,"reflect")+F(cfg,"bonus"),victim.team,"magic",true,false,victim);SpikedHits++;}}}else if(victim.hp>0&&Huts.FirstOrDefault(h=>h.id==ownerId&&h.hp>0&&h.paid) is Hut tower&&Lines[tower.kind].abilities.Contains("A020")&&Valid(victim,team,Abilities["A020"],false))ApplyEffect(victim,"A020",team);
        } else {
            if(target==-1&&ChoirCastleEvades(1-team))return;
            if(Aspects[team]==1&&(handProjectile||owner!=null&&owner.team==team&&HandFighter(Lines[owner.kind])))amount*=1.15f;
            Vector2 center=TargetPosition(target,team);if(w.type=="normal")BattleAudio.For(this)?.Contact(owner!=null?Lines[owner.kind].unit:null,null,true,center);float elementalBefore=target==-1?CastleHp[1-team]:FindHut(target)?.hp??0;
            if(owner!=null)foreach(var id in owner.abilities)if((string)Abilities[id]?["kind"]=="demolish")amount*=F(Abilities[id],"multiplier",1);if(target==-1){if(owner!=null&&passives)UndeadStructureHit(owner,target);DamageCastle(1-team,amount,w.attack);}else {var hut=FindHut(target);if(hut!=null&&owner!=null&&passives)UndeadStructureHit(owner,target);DamageHut(hut,amount,w.attack,team);if(hut!=null&&hut.hp>0&&owner!=null&&Lines[owner.kind].unit=="n02A")hut.dragonArmorRank=Mathf.Min(3,hut.dragonArmorRank+1);}
            if(owner!=null&&owner.hp>0&&passives){ElementalStructureHit(owner,target,elementalBefore-(target==-1?CastleHp[1-team]:FindHut(target)?.hp??0));CorruptedStructureHit(owner,target);NorthernStructureHit(owner,target);Pulverize(owner,center,w);if(Lines[owner.kind].unit=="n02B")owner.abilities.Remove("A0C8");}
        }
    }
    void OnHit(Unit owner,Unit target,float dealt,Weapon w) {
        if(FuneralChoir&&dealt>0)ChoirOnHit(owner,target);
        Pulverize(owner,target.position,w);NatureOnHit(owner,target);
        using(var snapshot035_4=SimulationBuffers035.Take(owner.abilities))foreach(var id in snapshot035_4) {
            var cfg=Abilities[id];string kind=(string)cfg?["kind"];
            if(kind=="cleave"&&w.type=="normal"){PulseAt(target.position,1,Color.white,"",.45f,"cleave");using(var snapshot035_5=SimulationBuffers035.Take(Units))foreach(var v in snapshot035_5)if(v!=target&&v.hp>0&&v.team!=owner.team&&!Flying(v)&&Vector2.Distance(v.position,target.position)<=F(cfg,"area"))DamageUnit(v,dealt*F(cfg,"power"),owner.team,w.attack,false);}
            if(kind=="orb"&&target.hp>0&&Roll()<F(cfg,"chance")){var effect=Abilities[(string)cfg["effect"]];if((string)effect?["kind"]=="mana_burn"){if(Valid(target,owner.team,effect,false)){float drain=Mathf.Min(target.mana,F(effect,"power"));target.mana-=drain;if(drain>0){DamageUnit(target,drain*F(effect,"ratio",1),owner.team,"magic",true,false,owner);FeedbackHits++;PulseAt(target.position,1,Color.cyan,"Выжигание маны");}}}else if(!ElementalOrb(owner,target,effect)&&!CorruptedOrb(owner,target,effect)&&!UndeadOrb(owner,target,effect)&&!NorthernOrbEffect(owner,target,effect))Chain(owner,target,effect);}
            if((kind=="poison"||kind=="corruption")&&target.hp>0&&Valid(target,owner.team,cfg,false))ApplyEffect(target,id,owner.team,owner.id);
            if(kind=="feedback"&&target.hp>0){float drained=Mathf.Min(target.mana,F(cfg,"power"));target.mana-=drained;float bonus=(target.summoned||(bool?)Lines[target.kind].stats["summoned"]==true)?F(cfg,"summoned_bonus"):0;if(drained+bonus>0){DamageUnit(target,drained*F(cfg,"ratio",1)+bonus,owner.team,"magic",true,false,owner);FeedbackHits++;PulseAt(target.position,1,Color.cyan,"Выжигание маны");}}
        }
    }
    public int SpikedHits {get;private set;}
    public int FeedbackHits {get;private set;}
    public int PulverizeProcs {get;private set;}
    void Pulverize(Unit owner,Vector2 center,Weapon weapon) {
        foreach(var id in owner.abilities) {
            var cfg=Abilities[id];if((string)cfg?["kind"]!="pulverize"||Roll()>=F(cfg,"chance"))continue;
            float full=F(cfg,"area"),half=F(cfg,"half_area");string flags=(string)cfg["targets"]??"ground";
            using(var snapshot035_6=SimulationBuffers035.Take(Units))foreach(var victim in snapshot035_6) {
                if(victim.hp<=0||victim.team==owner.team||(Flying(victim)&&!flags.Contains("air"))||(!Flying(victim)&&!flags.Contains("ground"))||(flags.Contains("organic")&&!Organic(victim)))continue;
                float distance=Vector2.Distance(victim.position,center);if(distance>Mathf.Max(full,half))continue;
                // This is secondary physical weapon damage: armor applies, spell immunity does not block it.
                DamageUnit(victim,F(cfg,"power")*(distance<=full?1:.5f),owner.team,weapon.attack,false);
            }
            PulverizeProcs++;PulseAt(center,Mathf.Max(full,half),new Color(1,.55f,.12f),"Сокрушение",.7f);
        }
    }
}
}
