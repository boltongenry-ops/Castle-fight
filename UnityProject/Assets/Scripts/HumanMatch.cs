using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
// Offline authority. All presentation commands enter here; imported Godot data stays read-only.
public sealed partial class HumanMatch {
    public Func<int,Vector2,bool> VisionTest;
    public sealed class Weapon {
        public JObject pattern;public string type,attack,targets,missile;public bool air,ground,structure,homing;
        public float damage,range,cooldown,point,backswing,speed,arc,full,half,quarter,halfFactor,quarterFactor;
        public Weapon Copy()=>(Weapon)MemberwiseClone();
        public static Weapon Read(JToken s) {var a=s["splash"];return new Weapon {
            pattern=s["pattern"] as JObject,type=(string)s["weapon_type"]??"normal",missile=(string)s["missile"]??"",attack=(string)s["attack_type"]??"normal",targets=(string)s["targets"]??"ground,air,structure",
            air=(bool?)s["can_air"]??false,ground=(bool?)s["can_ground"]??true,structure=(bool?)s["can_structure"]??((string)s["targets"]??"").Contains("structure"),homing=(bool?)s["missile_homing"]??true,
            damage=F(s,"damage"),range=F(s,"range"),cooldown=F(s,"cooldown",1),point=F(s,"damage_point"),backswing=F(s,"backswing"),speed=F(s,"missile_speed",30),arc=F(s,"missile_arc"),
            full=F(a,"full"),half=F(a,"half"),quarter=F(a,"quarter"),halfFactor=F(a,"half_factor",.5f),quarterFactor=F(a,"quarter_factor",.2f)};}
    }
    public sealed class Line {
        public string building,unit,name,unitName,hotkey,attack,armorType,weapon,category;public string[] upgrades,abilities,production;
        public int ultimateSlot=-1;public int cost,woodCost,woodReward,slots;public bool basic;public JObject stats,buildingStats;public Weapon[] weapons,buildingWeapons;
        public float interval,buildSeconds,incomeRate,hp,damage,range,cooldown,speed,armor,damagePoint,regen,backswing,walkRate,missileSpeed,arc,fullRadius,halfRadius,quarterRadius,halfFactor,quarterFactor,flyHeight;
        public float acquisitionRange,buildingAcquisitionRange;
        public bool Producer=>!string.IsNullOrEmpty(unit);
    }
    public sealed class Hut {
        public float nerubQueenLeft=8;public int pandaFocus=-1,pandaFocusHits;public int id,team,kind,x,y,spent,baseCost,dragonArmorRank;public float buildLeft,spawnLeft,income,hp,maxHp,mana,cooldown,attackPoseLeft,strikeLeft=-1,aimLeft,seaTimer=20;public bool started,paid,paused;
        public Vector2 position,aim;public int attackTarget=-1,attackSerial;public Weapon pendingWeapon;public readonly Dictionary<string,float> spellTimers=new Dictionary<string,float>();
    }
    public sealed class Builder {
        public int team;public Vector2 position,previous,goal,direction=Vector2.right;public string pose="Stand";public bool repairing;
        public int repairBuilding=-1;
        public readonly List<int> jobs=new List<int>();
    }
    public sealed class Effect {
        public string id,group;public float left,lifetime,power,armor,haste,speed,strength,incoming,dotTick;public bool root,ground;public int sourceTeam;public JObject config;
        public float WeaponDamageModifier => id == PepeRules.PepeShieldId || (string)config?["kind"] == "regen" ? 0 : power;
        public Effect Copy()=>(Effect)MemberwiseClone();
    }
    public sealed class Unit {
        public readonly List<PandaDebt> pandaDebts=new List<PandaDebt>();public bool pandaSplit;public int pandaHits,pandaFocus=-1,pandaFocusHits,nerubScatterHits,ultimateTeam=-2,ultimateChoice=-2;public float pandaGuardAfter,pandaRevelryAfter,nerubAmbushAfter,ultimateHealth,ultimateArmor;public readonly float[] nerubHuntAfter=new float[2];
        public float nightStealth,nightFade,nightSpeed,nightBonus,nightQuiet;public bool nightInvisible;public int nightVengeance,nightVengeanceTeam=-1;public bool elementalIllusion,elementalArmed,elementalDetonated,elementalMirrorPending,elementalLavaPending;public int elementalLavaGeneration,elementalLavaHits,elementalStormAttacks;public float elementalMirrorCd,elementalResonanceCd,elementalPoseLeft,auraSpeed,auraSpeedNegative,auraHasteNegative;public string elementalPose;public Weapon[] elementalWeapons;public bool corruptedThrall;public float corruptedCastPause,auraArmorNegative,auraHaste;public bool spellBorn;public float auraVampiric,auraDamageNegative;
        public bool mechEmpowered,mechFuse,mechActiveExplosion;public float mechMineAge;public int mechRepairTarget=-1000000,mechHeatAttacks;
        public bool northernRageUsed;public int bloodRoll=-1,chaosSeal,chaosSealTeam=-1;public float chaosAttackingUntil;
        public int handBolts;public bool handIronUsed;
        public int bearPhase=-1;public float bearLeft;public bool natureSpirit;public readonly HashSet<string> permanentHeals=new HashSet<string>();
        public int id,team,kind,kills,attackSerial,combatTarget=-1,attackTarget=-1;public Vector2 position,previous,direction,exitWaypoint;public bool leaveCastleLane;
        internal Vector2 groundPassAxis;
        internal float groundPassSide,groundPassUntil;
        public float hp,maxHp,mana,maxMana,manaRegen,armor,spellResist,critChance,critMultiplier=1,evasion,stun,stasisLeft,cooldown,deadAge,pending,attackPoseLeft,strikeLeft=-1,castLeft=-1,castPause,shotDamage,shotStun,auraArmor,auraMana,auraDamage,warlockLeft;
        public float auraRegen,auraThorns,auraDamageTick;public float summonLeft,attackOverride=-1,cooldownOverride=-1,sirenFollowup=-1;public bool summoned,noBounty,noCorpse,seaSummon;public int warlockPhase=-1,spellTarget=-1;public string finalCastPose,pendingSpell,pose="Walk";public Weapon pendingWeapon;public bool onRoad,deathProcessed,healthGranted,skirmish;
        public readonly Dictionary<string,int> abilityLevels=new Dictionary<string,int>();public readonly HashSet<string> abilities=new HashSet<string>(),auraIds=new HashSet<string>();public readonly Dictionary<string,float> spellTimers=new Dictionary<string,float>();public readonly List<Effect> effects=new List<Effect>();
    }
    public sealed class Shot {
        public int id,team,kind,target,line,sourceId,reflections,donorId;public string effectId,chaosSpell;public Effect stolenEffect;public Vector2 start,end;public float chaosDuration,chaosDps;public float age,duration,arc,damage,stun,startHeight=1.4f,endHeight=.8f;public bool nightSecondary;public int nightRemaining=-1;public int[] nightSeen;public JObject elementalConfig;public Unit elementalStormSource;public bool elementalStorm;public bool hit,incendiary,homing,spell,mechHeat;public Weapon weapon;
    }
    public sealed class Corpse {public Unit unit;public float left;}
    public sealed class Pulse {public int id,visualUnitId=-1;public Vector2 position;public float age,duration,radius;public Color color;public string text,fx;}
    public readonly Line[] Lines;public readonly int[] BaseKinds;public readonly int[] Aspects=new int[2];public readonly JArray AspectCatalog;public readonly bool IsOrc;public readonly bool[] TeamOrc=new bool[2],TeamNaga=new bool[2],TeamNature=new bool[2],TeamChaos=new bool[2],TeamNorthern=new bool[2],TeamMech=new bool[2],TeamHuman=new bool[2],TeamUndead=new bool[2],TeamCorrupted=new bool[2],TeamElemental=new bool[2],TeamNight=new bool[2],TeamElven=new bool[2];readonly JObject elementalBuildings;readonly HashSet<string>[] teamBuildings=new HashSet<string>[2],teamBaseBuildings=new HashSet<string>[2];readonly float[] reinforcement={30,30};double aspectAge;
    public readonly List<Hut> Huts=new List<Hut>();public readonly List<Unit> Units=new List<Unit>();public readonly List<Shot> Shots=new List<Shot>();public readonly List<Corpse> Corpses=new List<Corpse>();public readonly List<Pulse> Pulses=new List<Pulse>();
    public readonly float[] Gold=new float[2],CastleHp=new float[2];public readonly int[] Wood=new int[2],LegendaryLimit={1,1},TeamKills=new int[2],RescueCharges={1,1};public readonly float[] RescueCooldown=new float[2];
    public readonly Builder[] Builders=new Builder[2];public readonly JObject Abilities;
    public const int ArmyLimit=160;
    public const float CastleMax=5000,Cell=128f/30;public readonly float BuilderSpeed,BuilderWalkRate,CastleRegen,CastleArmor,CastleCollision;public readonly string CastleArmorType;
    public int Winner=-2,Kills,Spawned,Rejected,TickCount,ShellsFired,RifleShots,ShellImpacts,Upgrades,Heals,Resurrections,WarlockBlasts,HolyCasts,Prayers,ShrineExtras,Chains,ArtilleryFires,SpellsCast,AspectExtras;public float Elapsed;
    readonly JObject damageTable;readonly int difficulty;double incomeAge;float botAge=2,auraAge,botRescueAge=1;readonly double[] incomeFraction=new double[2];int nextId=1,botChoice;uint randomState=0xC45F17;
    public bool Finished=>Winner!=-2;
    public static float F(JToken s,string k,float fallback=0)=>(float?)s?[k]??fallback;
    public static string[] Strings(JToken s)=>s is JArray a?a.Select(v=>(string)v).ToArray():new string[0];
    public HumanMatch(JObject catalog,int startGold=250,int botDifficulty=1,int playerAspect=-1,int botAspect=-1,JObject botCatalog=null) {
        botCatalog=botCatalog??catalog;InitCentaur((string)catalog["race"],(string)botCatalog["race"]);for(int side=0;side<2;side++){var raceName=(string)(side==0?catalog:botCatalog)["race"];TeamUltimate[side]=raceName=="Ultimate Builder";TeamNerubian[side]=raceName=="Nerubian Swarm";TeamPandaren[side]=raceName=="Pandaren Order";var rows=(JArray)(side==0?catalog:botCatalog)["lines"];teamBaseBuildings[side]=new HashSet<string>(rows.Where(v=>(bool?)v["base"]!=false&&(bool?)v["allowed"]!=false).Select(v=>(string)v["building"]));}TeamOrc[0]=(string)catalog["race"]=="Orcish Horde";TeamOrc[1]=(string)botCatalog["race"]=="Orcish Horde";TeamNaga[0]=(string)catalog["race"]=="Naga Faction";TeamNaga[1]=(string)botCatalog["race"]=="Naga Faction";
        TeamNature[0]=(string)catalog["race"]=="Nature Force";TeamNature[1]=(string)botCatalog["race"]=="Nature Force";TeamChaos[0]=(string)catalog["race"]=="Chaos Legions";TeamChaos[1]=(string)botCatalog["race"]=="Chaos Legions";TeamNorthern[0]=(string)catalog["race"]=="Northern Realms";TeamNorthern[1]=(string)botCatalog["race"]=="Northern Realms";TeamMech[0]=(string)catalog["race"]=="Mech Coalition";TeamMech[1]=(string)botCatalog["race"]=="Mech Coalition";TeamHuman[0]=((string)catalog["race"]??"Human Alliance")=="Human Alliance";TeamHuman[1]=((string)botCatalog["race"]??"Human Alliance")=="Human Alliance";mechRepairValues=(JObject)(catalog["mechRepairValues"]??botCatalog["mechRepairValues"]??new JObject());mechPayloads=(JObject)(catalog["mechPayloads"]??botCatalog["mechPayloads"]??new JObject());TeamUndead[0]=(string)catalog["race"]=="Undead Forces";TeamUndead[1]=(string)botCatalog["race"]=="Undead Forces";TeamCorrupted[0]=(string)catalog["race"]=="Corrupted Conglomerate";TeamCorrupted[1]=(string)botCatalog["race"]=="Corrupted Conglomerate";TeamElemental[0]=(string)catalog["race"]=="Elementals' Universe";TeamElemental[1]=(string)botCatalog["race"]=="Elementals' Universe";elementalBuildings=(JObject)(catalog["elementalBuildings"]??botCatalog["elementalBuildings"]??new JObject());TeamNight[0]=(string)catalog["race"]=="Night Elfs";TeamNight[1]=(string)botCatalog["race"]=="Night Elfs";TeamElven[0]=(string)catalog["race"]=="Elven Union";TeamElven[1]=(string)botCatalog["race"]=="Elven Union";var playerCatalog=catalog;teamBuildings[0]=new HashSet<string>(((JArray)catalog["lines"]).Where(v=>(bool?)v["allowed"]!=false).Select(v=>(string)v["building"]));teamBuildings[1]=new HashSet<string>(((JArray)botCatalog["lines"]).Where(v=>(bool?)v["allowed"]!=false).Select(v=>(string)v["building"]));
        if(!ReferenceEquals(botCatalog,catalog)){catalog=(JObject)catalog.DeepClone();var combined=(JArray)catalog["lines"];foreach(var line in (JArray)botCatalog["lines"])if(!combined.Any(v=>(string)v["building"]==(string)line["building"]))combined.Add(line.DeepClone());foreach(var prop in ((JObject)botCatalog["abilities"]).Properties())if(catalog["abilities"][prop.Name]==null)((JObject)catalog["abilities"])[prop.Name]=prop.Value.DeepClone();}
        IsOrc=TeamOrc[0];Aspects[0]=playerAspect;Aspects[1]=botAspect;AspectCatalog=(JArray)catalog["aspects"]??new JArray();
        difficulty=Mathf.Clamp(botDifficulty,0,2);Gold[0]=Gold[1]=startGold;CastleHp[0]=CastleHp[1]=CastleMax;
        for(int t=0;t<2;t++){var p=new Vector2(t==0?-155:155,12);Builders[t]=new Builder {team=t,position=p,previous=p,goal=p};}
        InitInventory(catalog);damageTable=(JObject)catalog["damageTable"];Abilities=(JObject)catalog["abilities"]??new JObject();
        CastleRegen=F(catalog["castleStats"],"hp_regen",.5f);CastleArmor=F(catalog["castleStats"],"armor",5);CastleCollision=F(catalog["castleStats"],"collision",172f/30);CastleArmorType=(string)catalog["castleStats"]?["armor_type"]??"fortified";
        BuilderSpeed=Mathf.Clamp(F(catalog["builderStats"],"speed",18.333333f),F(catalog["builderStats"],"min_speed",5),F(catalog["builderStats"],"max_speed",13.333333f));BuilderWalkRate=BuilderSpeed*30/Mathf.Max(1,F(catalog["builderStats"],"walk_animation_speed",300));
        Lines=((JArray)catalog["lines"]).Select(v=>{var s=(JObject)v["stats"];var b=(JObject)v["buildingStats"]??s;var w=((JArray)s["weapons"]??new JArray()).Select(Weapon.Read).ToArray();var first=w.FirstOrDefault();
            if((string)v["unit"]=="z000")RestoreZerglingWeapon(first);
            return new Line {
            ultimateSlot=(int?)v["ultimateSlot"]??-1,building=(string)v["building"],unit=(string)v["unit"],name=(string)v["name"],unitName=(string)v["unitName"]=="z000"?"Зерглинг":(string)v["unitName"],hotkey=(string)v["hotkey"],category=(string)v["category"],basic=(bool?)v["base"]??true,
            cost=(int)v["cost"],woodCost=(int?)v["woodCost"]??0,woodReward=(int?)v["woodReward"]??0,slots=(int?)v["slots"]??0,upgrades=Strings(v["upgrades"]),abilities=Strings(v["abilities"]),production=Strings(v["production"]),stats=s,buildingStats=b,weapons=w,buildingWeapons=((JArray)b["weapons"]??new JArray()).Select(Weapon.Read).ToArray(),
            acquisitionRange=AcquisitionRange((string)v["unit"],s,Mathf.Max(800f/30,F(s,"range")+1)),buildingAcquisitionRange=AcquisitionRange((string)v["building"],b,float.PositiveInfinity),
            interval=F(v,"seconds"),buildSeconds=F(v,"buildSeconds",1.5f),incomeRate=F(v,"incomeRate"),hp=F(s,"hp"),damage=F(s,"damage"),range=F(s,"range"),cooldown=F(s,"cooldown",1),speed=F(s,"speed"),armor=F(s,"armor"),attack=(string)s["attack_type"],armorType=(string)s["armor_type"]??"normal",regen=F(s,"hp_regen"),flyHeight=F(s,"fly_height"),
            damagePoint=first?.point??.3f,backswing=first?.backswing??.4f,walkRate=F(s,"speed")*30/Mathf.Max(1,F(s,"walk_animation_speed",270))/Mathf.Max(.01f,F(s,"model_scale",1)),weapon=first?.type??"normal",missileSpeed=first?.speed??30,arc=first?.arc??0,
            fullRadius=first?.full??0,halfRadius=first?.half??0,quarterRadius=first?.quarter??0,halfFactor=first?.halfFactor??.5f,quarterFactor=first?.quarterFactor??.2f};}).ToArray();
        BaseKinds=Enumerable.Range(0,Lines.Length).Where(i=>teamBaseBuildings[0].Contains(Lines[i].building)).OrderBy(i=>Lines[i].ultimateSlot<0?i:Lines[i].ultimateSlot).ToArray();
    }
    public bool PlayerHasLine(int kind)=>teamBuildings[0].Contains(Lines[kind].building);
    public int Kind(string building)=>Array.FindIndex(Lines,l=>l.building==building);
    public bool PlayerVersusPlayer { get; set; }
    public bool NetworkReplica;public readonly int[] NetworkReservedGold=new int[2],NetworkReservedWood=new int[2],NetworkReservedSlots=new int[2];
    public int ReservedGold(int t)=>NetworkReplica?NetworkReservedGold[t]:Huts.Where(h=>h.team==t&&!h.paid).Sum(h=>Lines[h.kind].cost);
    public int ReservedWood(int t)=>NetworkReplica?NetworkReservedWood[t]:Huts.Where(h=>h.team==t&&!h.paid).Sum(h=>Lines[h.kind].woodCost);
    public int ReservedLegendary(int t)=>NetworkReplica?NetworkReservedSlots[t]:Huts.Where(h=>h.team==t&&!h.paid).Sum(h=>Lines[h.kind].slots);
    public float AvailableGold(int t)=>Mathf.Max(0,Gold[t]-ReservedGold(t));
    public int AvailableWood(int t)=>Mathf.Max(0,Wood[t]-ReservedWood(t));
    public int LegendaryUsed(int t)=>Huts.Where(h=>h.team==t).Sum(h=>Lines[h.kind].slots)+(NetworkReplica?NetworkReservedSlots[t]:0);
    public float Income(int t)=>5+Huts.Where(h=>h.team==t&&h.paid&&h.hp>0).Sum(h=>h.income);
    public float IncomeIn=>Mathf.Max(0,10-(float)incomeAge);
    public int ArmyCount(int t)=>ArmyCount035(t);
    public float Interval(Hut h)=>(15+h.spent/20f)*(AbyssTrialActive&&h.team==1?.82f:1);
    public static Vector2 CastlePosition(int t)=>new Vector2(t==0?-5120f/30:5120f/30,64f/30);
    public static Vector2 Plot(int team,int x,int y)=>new Vector2(((team==0?-6144:2048)+64+x*128)/30f,(y==24?-384:y==25?384:y<12?-1920+y*128:512+(y-12)*128)/30f);
    // CF240_BASE_NERUB.w3x, war3map.wpm: sixteen 32-unit pathing samples per 128-unit plot.
    // Columns run west to east. The two verge rows are appended to preserve existing plot IDs.
    static readonly uint[] BuildablePlots={
            0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFC7FFFFu,0xFFC7FFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0xFFEFFFFFu,0x7FCFF000u,0x7FCFF000u,
            0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFE3FFu,0xFFFFE3FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0xFFFFF7FFu,0x000FF3FEu,0x000FF3FEu
    };
    public static bool LegalCell(int team,int x,int y){
        return team>=0&&team<2&&x>=0&&x<32&&y>=0&&y<26&&(BuildablePlots[team*26+y]&(1u<<x))!=0;
    }
    public static int BuildRow(float wy,bool expanded){
        if(wy>=-1984&&wy< -448)return Mathf.FloorToInt((wy+1984)/128);
        if(wy>=448&&wy<1984)return 12+Mathf.FloorToInt((wy-448)/128);
        if(expanded&&wy>=-448&&wy< -320)return 24;
        if(expanded&&wy>=320&&wy<448)return 25;
        return -1;
    }
    public string PurchaseError(int team,int kind,int replacing=-1) {
        if(Finished)return "Матч завершён";if(team<0||team>1||kind<0||kind>=Lines.Length)return "Неизвестная постройка";
        string challengeError=ChallengePurchaseError(team,kind,replacing);if(challengeError!="")return challengeError;
        var l=Lines[kind];if(!teamBuildings[team].Contains(l.building))return "Постройка другой расы";if(AvailableGold(team)+.001f<GoldPrice(team,kind))return "Нужно золота: "+GoldPrice(team,kind);if(AvailableWood(team)<l.woodCost)return "Нужно дерева: "+l.woodCost;
        int old=replacing>=0?Lines[replacing].slots:0;return LegendaryUsed(team)+Mathf.Max(0,l.slots-old)>LegendaryLimit[team]?"Нет свободного легендарного места":"";
    }
    public bool TryBuild(int team,int kind,int x,int y,out string error) {
        error=PurchaseError(team,kind);
        if(error==""&&(!teamBaseBuildings[team].Contains(Lines[kind].building)||!LegalCell(team,x,y)))error="Стройте в своей зоне, вне дороги и стен";
        if(error==""&&(ProtocolBlocksPlot(team,x,y)||Huts.Any(h=>h.team==team&&h.x==x&&h.y==y)))error="Эта клетка занята";
        if(error==""&&Builders[team].jobs.Count>=32)error="В очереди уже 32 приказа";
        if(error!=""){Rejected++;return false;}
        var l=Lines[kind];var hut=new Hut {id=nextId++,team=team,kind=kind,x=x,y=y,position=Plot(team,x,y),buildLeft=l.buildSeconds,spawnLeft=l.interval,hp=F(l.buildingStats,"hp",1000),maxHp=F(l.buildingStats,"hp",1000),baseCost=l.cost,spent=l.cost};
        if(TeamHuman[team]&&Aspects[team]==0&&l.building=="h006"){hut.maxHp+=200;hut.hp=hut.maxHp;}
        Huts.Add(hut);Builders[team].jobs.Add(hut.id);Builders[team].repairing=false;return true;
    }
    public bool Upgrade(int team,int hutId,int kind,out string error) {
        var h=Huts.FirstOrDefault(v=>v.id==hutId&&v.team==team&&v.paid);error=h==null?"Выберите готовую свою постройку":kind<0||kind>=Lines.Length||!Lines[h.kind].upgrades.Contains(Lines[kind].building)?"Это улучшение недоступно":PurchaseError(team,kind,h.kind);
        if(error!="")return false;
        var l=Lines[kind];float ratio=h.hp/h.maxHp;Gold[team]-=GoldPrice(team,kind);Wood[team]+=l.woodReward-l.woodCost;h.spent+=l.cost;h.income+=l.cost*l.incomeRate;h.kind=kind;h.spawnLeft=Interval(h);h.paused=false;h.cooldown=0;h.strikeLeft=-1;h.pendingWeapon=null;h.spellTimers.Clear();h.maxHp=F(l.buildingStats,"hp",h.maxHp);h.hp=ratio*h.maxHp;h.mana=F(l.buildingStats,"start_mana");Upgrades++;return true;
    }
    void RemoveHut(Hut h){Huts.Remove(h);var b=Builders[h.team];if(b.jobs.Remove(h.id)&&b.jobs.Count==0){b.goal=b.position;b.pose="Stand";}}
    public bool Sell(int team,int id){var h=Huts.FirstOrDefault(v=>v.id==id&&v.team==team);if(h==null||Finished||IsProtocolDefense(h))return false;if(h.paid)Gold[team]+=(int)(h.baseCost*.2f);RemoveHut(h);return true;}
    public bool PauseHut(int team,int id,bool paused){var h=Huts.FirstOrDefault(v=>v.id==id&&v.team==team&&v.paid);if(h==null||Finished)return false;h.paused=paused;return true;}
    readonly Dictionary<Hut,int>[] synchronizedHuts={new Dictionary<Hut,int>(),new Dictionary<Hut,int>()};
    int reservedWaveSlots;

    public int SynchronizeProduction(int team) {
        if(Finished||NetworkReplica||team<0||team>=synchronizedHuts.Length)return 0;
        var producers=Huts.Where(h=>h.team==team&&h.paid&&h.hp>0&&!h.paused&&Lines[h.kind].Producer).ToArray();
        if(producers.Length<2)return 0;
        if(producers.Sum(WaveSlots)>ArmyLimit)return -1;
        float latest=Mathf.Max(0,producers.Max(h=>h.spawnLeft));
        var wave=synchronizedHuts[team];
        wave.Clear();
        foreach(var h in producers) {
            wave.Add(h,h.kind);
            h.spawnLeft=latest;
        }
        return producers.Length;
    }

    int WaveSlots(Hut h) {
        if(Lines[h.kind].unit=="e00E")return 2;
        return Mathf.Max(1,ProdRoster033(h.kind)?.Length??0)+(HasPair033(h.kind)?1:0);
    }

    void TickSynchronizedProduction(float dt) {
        for(int team=0;team<synchronizedHuts.Length;team++) {
            var wave=synchronizedHuts[team];
            if(wave.Count==0)continue;
            using(var snapshot=SimulationBuffers035.Take(wave.Keys))
                foreach(var h in snapshot)
                    if(h.hp<=0||h.kind!=wave[h]||!Huts.Contains(h))wave.Remove(h);
            if(wave.Count==0||wave.Keys.Any(h=>h.paused||BuildingDisabled(h)>0))continue;
            float left=Mathf.Max(0,wave.Keys.Max(h=>h.spawnLeft)-dt);
            foreach(var h in wave.Keys)h.spawnLeft=left;
            if(left>0)continue;
            int slots=wave.Keys.Sum(WaveSlots);
            if(ArmyCount(team)+slots>ArmyLimit)continue;
            try {
                reservedWaveSlots=slots;
                foreach(var h in Huts) {
                    if(!wave.ContainsKey(h))continue;
                    int ownSlots=WaveSlots(h);
                    Produce(h,ownSlots);
                    reservedWaveSlots-=ownSlots;
                }
            } finally {
                reservedWaveSlots=0;
                wave.Clear();
            }
        }
    }

    void Produce(Hut h,int waveSlots=0) {
        var roster=ProdRoster033(h.kind);
        int length=roster?.Length??0;
        int required=Lines[h.kind].unit=="e00E"?2:Mathf.Max(1,length);
        if(ArmyCount(h.team)+required>ArmyLimit){h.spawnLeft=0;return;}
        h.spawnLeft+=Interval(h);
        bool pair=HasPair033(h.kind);
        float lane=pair?Mathf.Max(1.1f,StatCollision033(h.kind)*1.1f):0;
        if(length>1) {
            var kinds=ProdKinds033(h.kind);
            for(int i=0;i<length;i++) {
                int kind=kinds[i];
                if(kind>=0)Spawn(h.team,kind,h.position+Vector2.up*((i-(length-1)*.5f)*2.2f),true,h);
            }
        }else Spawn(h.team,h.kind,h.position+Vector2.up*lane,true,h);
        if(pair&&(waveSlots>0||ArmyCount(h.team)<ArmyLimit))Spawn(h.team,h.kind,h.position-Vector2.up*lane,false,h);
    }
    public void PauseAll(int team,bool paused){if(Finished)return;foreach(var h in Huts.Where(h=>h.team==team&&h.paid&&h.hp>0&&Lines[h.kind].Producer))h.paused=paused;}
    public bool InsideBase(int t,Vector2 p)=>Mathf.Abs(p.y)<=2016f/30&&(t==0?p.x>=-6144f/30&&p.x<=-2016f/30:p.x>=2016f/30&&p.x<=6144f/30);
    public bool MoveBuilder(int team,Vector2 point) {
        if(Finished||team<0||team>1||float.IsNaN(point.x)||float.IsNaN(point.y)||float.IsInfinity(point.x)||float.IsInfinity(point.y)||!InsideBase(team,point))return false;
        var b=Builders[team];using(var snapshot035_1=SimulationBuffers035.Take(b.jobs))foreach(int id in snapshot035_1){var h=Huts.FirstOrDefault(v=>v.id==id);if(h!=null&&!h.started)Huts.Remove(h);}b.jobs.Clear();b.goal=point;b.repairing=false;return true;
    }
    public bool ResumeBuilding(int team,int hutId){if(Finished||team<0||team>1)return false;var h=Huts.FirstOrDefault(v=>v.id==hutId&&v.team==team&&!v.paid);if(h==null)return false;var b=Builders[team];if(!b.jobs.Contains(hutId))b.jobs.Insert(0,hutId);b.repairing=false;return true;}
    public bool RepairCastle(int team){
        if(Finished||team<0||team>1||!ChallengeBuilderRepairAllowed(team)||CastleHp[team]>=CastleHealthLimit(team))return false;
        StartRepair(Builders[team],-1);
        return true;
    }
    public const float WorkerRepairRate=15;
    public static bool CanRepairBuilding(int team,Hut hut)=>hut!=null&&hut.team==team&&hut.paid&&hut.hp>0&&hut.hp<hut.maxHp-.05f;
    public bool RepairBuilding(int team,int hutId){
        if(Finished||team<0||team>1||hutId<=0)return false;
        var hut=Huts.FirstOrDefault(value=>value.id==hutId);
        if(!CanRepairBuilding(team,hut))return false;
        StartRepair(Builders[team],hutId);
        return true;
    }
    void StartRepair(Builder builder,int hutId){
        MoveBuilder(builder.team,builder.position);
        builder.repairBuilding=hutId;
        builder.repairing=true;
    }
    public bool Blink(int team,Vector2 p){if(Finished||team<0||team>1||float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||Mathf.Abs(p.x)>6144f/30||Mathf.Abs(p.y)>2048f/30)return false;var b=Builders[team];MoveBuilder(team,b.position);PulseAt(b.position,2,Color.cyan,"Скачок",.633f,"blinkout");if(!InsideBase(team,p))p=new Vector2(team==0?-4544f/30:4544f/30,448f/30);b.position=b.previous=b.goal=p;PulseAt(p,2,Color.cyan,"",1.2f,"blinkin");return true;}
    static Vector2 BuilderWaypoint(Vector2 from,Vector2 goal) {
        float nearest=float.MaxValue,wall=0;foreach(float x in new[]{-3520f/30,-2016f/30,2016f/30,3520f/30})if((from.x-x)*(goal.x-x)<0&&Mathf.Abs(from.x-x)<nearest){nearest=Mathf.Abs(from.x-x);wall=x;}
        if(nearest<float.MaxValue){if(Mathf.Abs(from.y)>1.5f)return new Vector2(from.x,0);return new Vector2(wall+Mathf.Sign(goal.x-from.x)*2,0);}return goal;
    }
    void TickBuilder(Builder b,float dt) {
        b.previous=b.position;Hut hut=null;while(b.jobs.Count>0){hut=Huts.FirstOrDefault(h=>h.id==b.jobs[0]&&!h.paid);if(hut!=null)break;b.jobs.RemoveAt(0);}
        Vector2 destination=hut!=null?hut.position+new Vector2(0,-Mathf.Sign(hut.position.y)*2.4f):b.goal;
        Hut repair=null;
        if(b.repairing&&b.repairBuilding>0){
            repair=Huts.FirstOrDefault(h=>h.id==b.repairBuilding);
            if(CanRepairBuilding(b.team,repair))destination=repair.position+new Vector2(0,-Mathf.Sign(repair.position.y)*2.4f);
            else{b.repairing=false;b.repairBuilding=-1;repair=null;destination=b.goal=b.position;}
        }
        if(b.repairing&&repair==null){
            var point=CastlePosition(b.team);
            var d=b.position-point;
            destination=point+(d.sqrMagnitude>.01f?d.normalized:Vector2.right)*6.7f;
        }
        if((destination-b.position).sqrMagnitude>.16f){var waypoint=BuilderWaypoint(b.position,destination);b.direction=(waypoint-b.position).normalized;b.position=Vector2.MoveTowards(b.position,waypoint,BuilderSpeed*dt);b.pose="Walk";}
        else if(hut!=null){hut.started=true;hut.buildLeft=Mathf.Max(0,hut.buildLeft-dt);b.pose="Stand Work";b.direction=(hut.position-b.position).normalized;if(hut.buildLeft<=0){var l=Lines[hut.kind];Gold[b.team]-=GoldPrice(b.team,hut.kind);Wood[b.team]+=l.woodReward-l.woodCost;hut.paid=true;hut.income=l.cost*l.incomeRate;hut.mana=F(l.buildingStats,"start_mana");b.jobs.RemoveAt(0);b.goal=b.position;}}
        else if(b.repairing){
            b.pose="Stand Work";
            b.direction=((repair?.position??CastlePosition(b.team))-b.position).normalized;
            if(repair!=null){
                repair.hp=Mathf.Min(repair.maxHp,repair.hp+WorkerRepairRate*dt);
                if(repair.hp>=repair.maxHp){
                    b.repairing=false;b.repairBuilding=-1;b.goal=b.position;
                }
            }else{
                RestoreCastleHealth(b.team,WorkerRepairRate*dt);
                if(CastleHp[b.team]>=CastleHealthLimit(b.team)){b.repairing=false;b.goal=b.position;}
            }
        }
        else b.pose="Stand";
    }
    void Bot(){if(!PlayerVersusPlayer)ThinkBot();}
    public void Step(float dt) {
        if(Finished||dt<=0)return;AdvanceWinterPhase();SimulationPhase035.Begin();Elapsed+=dt;TickCount++;RebuildMechPowerCache();RebuildIdCache033();incomeAge+=dt;botAge-=dt;
        while(incomeAge>=10){incomeAge-=10;for(int t=0;t<2;t++){incomeFraction[t]+=Income(t);int payment=(int)(incomeFraction[t]+.00001f);incomeFraction[t]-=payment;Gold[t]+=payment;}}
        float recovery=NaturalCastleRecovery*dt;
        for(int t=0;t<2;t++)RestoreCastleHealth(t,recovery);
        foreach(var b in Builders)TickBuilder(b,dt);if(botAge<=0){botAge=difficulty==0?8:difficulty==1?4:2;Bot();}
        for(int t=0;t<2;t++)RescueCooldown[t]=Mathf.Max(0,RescueCooldown[t]-dt);
        foreach(var c in Corpses)c.left-=dt;Corpses.RemoveAll(c=>c.left<=0);
        foreach(var p in Pulses)p.age+=dt;Pulses.RemoveAll(p=>p.age>p.duration);
        SimulationPhase035.Mark("economy_builders");TickCentaur(dt);TickInventory(dt);TickHumanJobs(dt);TickNagaJobs(dt);TickNagaWaves(dt);TickNatureJobs(dt);TickChaosJobs(dt);TickNorthern(dt);TickMech(dt);TickUndead(dt);TickCorrupted(dt);TickElemental(dt);TickNight(dt);TickElven(dt);TickFinal(dt);FlushUndeadBirths();TickAspects(dt);if(!PlayerVersusPlayer)TickBotRescue(dt);EnsureUnitIndex033();RebuildElementalLinkCache033();SetBuildingActive033(true);SimulationPhase035.Mark("race_controllers");
        using(var snapshot035_2=SimulationBuffers035.Take(Huts))foreach(var h in snapshot035_2)if(h.paid&&h.hp>0&&!h.paused){var l=Lines[h.kind];h.hp=Mathf.Min(h.maxHp,h.hp+BuildingConfig035(h.kind).hpRegen*dt);h.mana=Mathf.Min(BuildingConfig035(h.kind).maxMana,h.mana+(BuildingManaRegen(h,BuildingConfig035(h.kind).manaRegen)+ElementalManaBonus(h)+(MechPowered(h)?F(Abilities["A09P"]?["fields"],"Hab1"):0))*dt);TickBuilding(h,dt);if(BuildingDisabled(h)>0)continue;if(l.Producer&&!synchronizedHuts[h.team].ContainsKey(h)){h.spawnLeft-=dt;if(h.spawnLeft<=0)Produce(h);}}
        TickSynchronizedProduction(dt);SetBuildingActive033(false);SimulationPhase035.Mark("buildings_production");
        foreach(var u in Units){Ensure(u);u.previous=u.position;if(u.hp<=0){u.deadAge+=dt;continue;}if(u.summonLeft>0&&(u.summonLeft-=dt)<=0){u.hp=0;u.deathProcessed=true;u.pose="Death";continue;}u.cooldown-=dt;u.stun=Mathf.Max(0,u.stun-dt);u.stasisLeft=Mathf.Max(0,u.stasisLeft-dt);u.castPause=Mathf.Max(0,u.castPause-dt);u.attackPoseLeft=Mathf.Max(0,u.attackPoseLeft-dt);TickEffects(u,dt);if(u.hp<=0)continue;u.hp=Mathf.Min(u.maxHp,u.hp+(Lines[u.kind].regen+u.auraRegen+Stat(u,"regen")+(TeamNaga[u.team]&&Aspects[u.team]==0?4:0))*dt);u.mana=Mathf.Min(u.maxMana,u.mana+(u.manaRegen+u.auraMana)*dt);using(var snapshot035_3=SimulationBuffers035.Take(u.spellTimers.Keys))foreach(var id in snapshot035_3)u.spellTimers[id]=Mathf.Max(0,u.spellTimers[id]-dt);}
        SimulationPhase035.Mark("unit_statuses");auraAge-=dt;if(auraAge<=0){auraAge=.5f;UpdateAuras();}
        SimulationPhase035.Mark("auras");BlackHandZones(dt);TickShots(dt);SimulationPhase035.Mark("projectiles");BeginUnitBatch033();using(var snapshot035_4=SimulationBuffers035.Take(Units))foreach(var u in snapshot035_4)if(u.hp>0){TickUnit(u,dt);UnitMoved033(u);}EndUnitBatch033();SimulationPhase035.Mark("unit_decisions_movement");FlushUndeadBirths();FlushFinalBirths();
        Units.RemoveAll(u=>u.hp<=0&&u.deadAge>3);Shots.RemoveAll(s=>s.age>s.duration+(s.kind==2||s.kind==3?.35f:0));
        if(CastleHp[0]<=0||CastleHp[1]<=0)Winner=CastleHp[0]<=0&&CastleHp[1]<=0?-1:CastleHp[0]<=0?1:0;
        ResolveChallengeClock(dt);
        FlushWinterDefections();
    }
    public void Surrender(int team){if(!Finished&&team>=0&&team<2)Winner=1-team;}
    public float Roll(){randomState^=randomState<<13;randomState^=randomState>>17;randomState^=randomState<<5;return (randomState&0xFFFFFF)/16777216f;}
    public void PulseAt(Vector2 p,float radius,Color color,string text,float duration=.8f,string fx=null,int visualUnitId=-1){if(fx!=null)BattleAudio.For(this)?.Spell(fx,p);Pulses.Add(new Pulse {id=nextId++,position=p,radius=radius,color=color,text=text,duration=duration,fx=fx,visualUnitId=visualUnitId});}
}
}
