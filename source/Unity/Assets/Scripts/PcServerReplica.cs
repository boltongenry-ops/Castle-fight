using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
// Projects Godot's authoritative snapshots into the existing PC renderer. This class
// does not advance the simulation or authorize starting a match by itself.
public sealed class PcServerReplica {
    readonly HumanMatch world;
    readonly int slot;
    readonly Func<int,string,int> unitKind;
    public int UnknownUnits { get; private set; }
    public int UnknownBuildings { get; private set; }
    public PcServerReplica(HumanMatch world,int slot,Func<int,string,int> unitKind) {
        this.world=world??throw new ArgumentNullException(nameof(world));
        if(slot<0||slot>1)throw new ArgumentOutOfRangeException(nameof(slot));
        this.slot=slot;this.unitKind=unitKind??throw new ArgumentNullException(nameof(unitKind));
    }
    int Team(int serverTeam)=>serverTeam==slot?0:1;
    Vector2 Point(JToken token) {
        if(!(token is JArray a)||a.Count<3)throw new FormatException("Invalid position in server snapshot");
        float x=(float)a[0],z=(float)a[2];
        if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(z)||float.IsInfinity(z))throw new FormatException("Non-finite server position");
        return new Vector2(slot==1?-x:x,z);
    }
    static float Number(JToken value,float fallback=0)=>(float?)value??fallback;
    static float Part(JToken row,string key,int index,float fallback=0)=>row[key] is JArray a&&a.Count>index?Number(a[index],fallback):fallback;
    static string Pose(JToken row)=>row["a"] is JArray a&&a.Count>0?((string)a[0]??"Stand"):"Stand";
    static int Serial(JToken row)=>row["a"] is JArray a&&a.Count>3?((int?)a[3]??0):0;
    public void Apply(JObject snapshot) {
        if(!(snapshot?["units"] is JArray actors)||!(snapshot["huts"] is JArray structures)||!(snapshot["banks"] is JArray banks)||banks.Count!=2||!(snapshot["castles"] is JArray castles)||castles.Count!=2)
            throw new FormatException("Incomplete server snapshot");
        if(actors.Count>4096||structures.Count>2048)throw new FormatException("Server snapshot exceeds entity limits");
        var nextUnits=new List<HumanMatch.Unit>();var nextHuts=new List<HumanMatch.Hut>();
        var previous=world.Units.ToDictionary(x=>x.id);int unknownUnits=0,unknownBuildings=0;
        foreach(JObject row in actors.OfType<JObject>()) {
            int team=(int?)row["t"]??-1;if(team<0||team>1)continue;int local=Team(team);var point=Point(row["p"]);
            string pose=Pose(row);float yaw=Number(row["yaw"]);var direction=new Vector2(Mathf.Sin(yaw)*(slot==1?-1:1),Mathf.Cos(yaw));
            if((bool?)row["builder"]==true) {
                var builder=world.Builders[local];builder.previous=builder.position;builder.position=point;builder.direction=direction;builder.pose=pose;builder.repairing=(bool?)row["repair"]??false;continue;
            }
            int kind=unitKind(local,(string)row["r"]??"");if(kind<0||kind>=world.Lines.Length){unknownUnits++;continue;}
            int id=(int?)row["id"]??0;if(id<=0)continue;
            var unit=new HumanMatch.Unit { id=id,team=local,kind=kind,position=point,previous=previous.TryGetValue(id,out var old)?old.position:point,direction=direction,pose=pose,attackSerial=Serial(row),hp=Part(row,"hp",0),maxHp=Part(row,"hp",1,1),mana=Part(row,"m",0),maxMana=Part(row,"m",1),armor=Number(row["armor"]),stun=Number(row["stun"]) };
            if(row["abilities"] is JArray abilities)foreach(var ability in abilities)if(ability.Type==JTokenType.String)unit.abilities.Add((string)ability);
            if(row["auras"] is JArray auras)foreach(var aura in auras)if(aura.Type==JTokenType.String)unit.auraIds.Add((string)aura);
            if(row["state"] is JObject state){
                unit.pandaSplit=(bool?)state["pandaren_split"]??false;unit.nightInvisible=(bool?)state["invisible"]??false;
                unit.spellResist=Number(state["spell_resist"]);unit.critChance=Number(state["crit_chance"]);unit.critMultiplier=Number(state["crit_multiplier"],1);unit.evasion=Number(state["evasion"]);
                if(state["ability_levels"] is JObject levels)foreach(var level in levels.Properties())unit.abilityLevels[level.Name]=(int?)level.Value??1;
            }
            if(row["statuses"] is JArray statuses)foreach(var status in statuses.OfType<JObject>()){
                var cfg=(JObject)status.DeepClone();string code=(string)cfg["code"]??(string)cfg["id"]??"";
                if(world.Abilities[code] is JObject definition)foreach(var property in definition.Properties())if(cfg[property.Name]==null)cfg[property.Name]=property.Value.DeepClone();
                unit.effects.Add(new HumanMatch.Effect{id=(string)status["id"]??code,group=(string)status["stack_group"]??code,left=Number(status["remaining"]),lifetime=Number(status["lifetime"]),sourceTeam=Team((int?)status["source_team"]??team),power=Number(status["power"]),armor=Number(status["armor"]),haste=Number(status["haste"]),speed=-Number(status["slow"]),root=(bool?)status["root"]??false,ground=(bool?)status["ground"]??false,config=cfg});
            }
            nextUnits.Add(unit);
        }
        foreach(JObject row in structures.OfType<JObject>()) {
            int team=(int?)row["team"]??-1;if(team<0||team>1)continue;int local=Team(team);int kind=world.Kind((string)row["line_raw"]??"");
            if(kind<0){unknownBuildings++;continue;}int id=(int?)row["id"]??0;if(id<=0)continue;
            Vector2 p=Point(row["p"]);int x=Mathf.RoundToInt((p.x*30-(local==0?-6080:2112))/128);float wy=p.y*30;int y=wy<=-448?Mathf.RoundToInt((wy+1920)/128):12+Mathf.RoundToInt((wy-512)/128);
            nextHuts.Add(new HumanMatch.Hut { id=id,team=local,kind=kind,x=x,y=y,position=p,paid=true,started=true,spent=(int?)row["spent"]??0,baseCost=(int?)row["cost"]??0,hp=Part(row,"hp",0),maxHp=Part(row,"hp",1,1),mana=Part(row,"m",0),income=Number(row["income"]),spawnLeft=Number(row["timer"]),paused=(bool?)row["paused"]??false,attackSerial=Serial(row) });
        }
        world.Units.Clear();world.Units.AddRange(nextUnits);world.Huts.Clear();world.Huts.AddRange(nextHuts);
        for(int serverTeam=0;serverTeam<2;serverTeam++) {
            int local=Team(serverTeam);var bank=banks[serverTeam];world.Gold[local]=Number(bank["gold"]);world.Wood[local]=(int?)bank["wood"]??0;world.LegendaryLimit[local]=(int?)bank["limit"]??1;world.CastleHp[local]=Number(castles[serverTeam]?["hp"],HumanMatch.CastleMax);
            world.NetworkReservedGold[local]=(int?)bank["reserved_gold"]??0;world.NetworkReservedWood[local]=(int?)bank["reserved_wood"]??0;world.NetworkReservedSlots[local]=(int?)bank["reserved_slots"]??0;
            var bag=world.Inventories[local];bag.slots.Clear();if(bank["items"] is JArray items)foreach(JObject item in items.OfType<JObject>()){string id=(string)item["id"]??"";if(world.ItemCatalog[id]!=null)bag.slots.Add(new HumanMatch.ItemSlot {id=id,count=(int?)item["count"]??1,cooldown=Number(item["cooldown"])});}
            if(bank["stock"] is JObject stocks)foreach(var entry in stocks.Properties())if(bag.stocks.TryGetValue(entry.Name,out var stock)){stock.count=(int?)entry.Value["count"]??0;stock.next=Number(entry.Value["next"]);}
            bag.cooldowns.Clear();if(bank["item_cooldowns"] is JObject cooldowns)foreach(var entry in cooldowns.Properties())bag.cooldowns[entry.Name]=Number(entry.Value);
            if(snapshot["rescue"] is JArray rescue&&rescue.Count==2)world.RescueCharges[local]=(int?)rescue[serverTeam]??0;
            if(snapshot["rescue_cd"] is JArray cooldown&&cooldown.Count==2)world.RescueCooldown[local]=Number(cooldown[serverTeam]);
            if(snapshot["kills"] is JArray kills&&kills.Count==2)world.TeamKills[local]=(int?)kills[serverTeam]??0;
        }
        world.Elapsed=Number(snapshot["elapsed"]);UnknownUnits=unknownUnits;UnknownBuildings=unknownBuildings;
        if((bool?)snapshot["over"]==true){int winner=(int?)snapshot["winner"]??-1;world.Winner=winner<0?-1:Team(winner);}
    }
    public static JObject Command(string kind,JObject data=null) {var command=data==null?new JObject():(JObject)data.DeepClone();command["kind"]=kind;return new JObject { ["type"]="command",["command"]=command };}
    // Coordinates use the local west-side view; server_commands.gd mirrors X for slot 1.
    static JArray CommandPoint(Vector2 point) {
        if(float.IsNaN(point.x)||float.IsInfinity(point.x)||float.IsNaN(point.y)||float.IsInfinity(point.y)||Math.Abs(point.x)>6144f/30||Math.Abs(point.y)>2048f/30)
            throw new ArgumentOutOfRangeException(nameof(point));
        return new JArray(point.x,0,point.y);
    }
    public static JObject Move(Vector2 point,bool queued=false)=>Command("move",new JObject { ["point"]=CommandPoint(point),["queued"]=queued });
    public static JObject Build(string raw,Vector2 point,bool queued=false) {
        if(string.IsNullOrEmpty(raw))throw new ArgumentException("A building rawcode is required",nameof(raw));
        return Command("build",new JObject { ["raw"]=raw,["point"]=CommandPoint(point),["queued"]=queued });
    }
    public static JObject BuildingAction(string kind,int id,string raw=null) {
        if(kind!="pause"&&kind!="upgrade"&&kind!="sell"&&kind!="nuke")throw new ArgumentException("Unknown building action",nameof(kind));
        if(id<=0)throw new ArgumentOutOfRangeException(nameof(id));
        var data=new JObject { ["id"]=id };if(kind=="upgrade"){if(string.IsNullOrEmpty(raw))throw new ArgumentException("Upgrade rawcode is required",nameof(raw));data["raw"]=raw;}return Command(kind,data);
    }
    public static JObject PauseAll(bool paused)=>Command("pause_all",new JObject { ["paused"]=paused });
    public static JObject Aspect(int index) {
        if(index<0||index>1)throw new ArgumentOutOfRangeException(nameof(index));
        return Command("aspect",new JObject { ["index"]=index });
    }
    public static JObject BuilderSpell(string spell,Vector2 point) {
        if(string.IsNullOrEmpty(spell))throw new ArgumentException("A spell is required",nameof(spell));
        return Command("builder_spell",new JObject { ["spell"]=spell,["point"]=CommandPoint(point) });
    }
    public static JObject BuyItem(int index) {if(index<0)throw new ArgumentOutOfRangeException(nameof(index));return Command("buy_item",new JObject { ["index"]=index });}
    public static JObject UseItem(int index,int target,Vector2 point) {if(index<0||target<0)throw new ArgumentOutOfRangeException();return Command("use_item",new JObject { ["index"]=index,["target"]=target,["point"]=CommandPoint(point) });}
}
}
