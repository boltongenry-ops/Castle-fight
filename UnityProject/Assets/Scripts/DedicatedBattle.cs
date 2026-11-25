#if UNITY_SERVER || CF_AUTHORITY_QA
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
    // Network input reaches here only after the gateway has assigned a player slot.
    internal sealed class DedicatedBattle {
        readonly HumanMatch match;
        readonly bool fogEnabled, aspects;
        readonly FogGrid[] fog = { new FogGrid(), new FogGrid() };
        readonly HashSet<int>[] knownHuts = { new HashSet<int>(), new HashSet<int>() };
        float aspectTime=30f, fogTime;
        public bool Paused { get; set; }
        public bool Finished => match.Finished;
        bool ChoosingAspect => aspects && (match.Aspects[0]<0 || match.Aspects[1]<0);

        public DedicatedBattle(JObject west,JObject east,int gold,bool fogEnabled,bool aspects) {
            this.fogEnabled=fogEnabled;
            this.aspects=aspects && (west["aspects"] as JArray)?.Count==2 && (east["aspects"] as JArray)?.Count==2;
            match=new HumanMatch(west,gold,botCatalog:east) {PlayerVersusPlayer=true};
            match.VisionTest=Visible;
            UpdateSight();
        }

        public void Step(float seconds) {
            if(Paused||Finished)return;
            if(ChoosingAspect) {
                aspectTime=Mathf.Max(0,aspectTime-seconds);
                if(aspectTime<=0)for(int team=0;team<2;team++)if(match.Aspects[team]<0)match.Aspects[team]=0;
                return;
            }
            if((fogTime-=seconds)<=0){fogTime=.15f;UpdateSight();}
            match.Step(seconds);
        }

        static Vector2 Mirror(int team,Vector2 p)=>team==0?p:new Vector2(-p.x,p.y);
        bool Visible(int team,Vector2 p)=>!fogEnabled||fog[team].Visible(Mirror(team,p));

        void UpdateSight(){
            if(!fogEnabled)return;
            for(int team=0;team<2;team++){
                var grid=fog[team];
                grid.Clear();grid.RevealBase();
                grid.Reveal(Mirror(team,match.Builders[team].position),800f/30);
                foreach(var u in match.Units)
                    if(u.hp>0&&(u.team==team||match.ElvenReveals(u,team)))
                        grid.Reveal(Mirror(team,u.position),HumanMatch.F(match.Lines[u.kind].stats,"sight",800f/30));
                foreach(var h in match.Huts)
                    if(h.hp>0&&h.team==team)grid.Reveal(Mirror(team,h.position),HumanMatch.F(match.Lines[h.kind].buildingStats,"sight",1000f/30));
                foreach(var orb in match.NorthernOrbs)if(orb.team==team)grid.Reveal(Mirror(team,orb.position),700f/30);
                foreach(var zone in match.NightZones)if(zone.team==team&&zone.age<8)grid.Reveal(Mirror(team,zone.point),1100f/30);
                foreach(var h in match.Huts)if(h.team==team||Visible(team,h.position))knownHuts[team].Add(h.id);
            }
        }

        static bool TryPoint(int team,JToken row,out Vector2 p){
            p=default;
            if(!(row is JArray a)||a.Count!=3)return false;
            if(!Numeric(a[0])||!Numeric(a[2]))return false;
            var local=new Vector2((float)a[0],(float)a[2]);
            if(float.IsNaN(local.x)||float.IsNaN(local.y)||Mathf.Abs(local.x)>6144f/30||Mathf.Abs(local.y)>2048f/30)return false;
            p=Mirror(team,local);
            return true;
        }
        static bool Numeric(JToken n)=>n.Type==JTokenType.Float||n.Type==JTokenType.Integer;
        static int Int(JToken n,int fallback=-1)=>n?.Type==JTokenType.Integer?(int)n:fallback;

        public JObject Command(int team,JObject command){
            if(team<0||team>1||command==null||Paused||Finished)return Result(false,"Бой недоступен");
            string kind=(string)command["kind"];
            if(kind=="surrender"){match.Surrender(team);return Result(true);}
            int index=Int(command["index"]);
            if(ChoosingAspect){
                if(kind!="aspect"||index<0||index>1||match.Aspects[team]>=0)return Result(false,"Сначала выберите аспект");
                match.Aspects[team]=index;
                return Result(true);
            }
            int id=Int(command["id"]);
            switch(kind){
                case "build":
                    if(!TryPoint(team,command["point"],out var plot))break;
                    int x=Mathf.RoundToInt((plot.x*30-(team==0?-6080:2112))/128);
                    int y=HumanMatch.BuildRow(plot.y*30,true);
                    int building=match.Kind((string)command["raw"]);
                    if(building<0||(HumanMatch.Plot(team,x,y)-plot).sqrMagnitude>.25f)break;
                    return Result(match.TryBuild(team,building,x,y,out var constructionError),constructionError);
                case "upgrade":
                    int next=match.Kind((string)command["raw"]);
                    if(next<0)break;
                    return Result(match.Upgrade(team,id,next,out var upgradeError),upgradeError);
                case "sell":return Result(match.Sell(team,id));
                case "pause":
                    var hut=match.Huts.FirstOrDefault(h=>h.id==id&&h.team==team&&h.paid&&match.Lines[h.kind].Producer);
                    return Result(hut!=null&&match.PauseHut(team,id,!hut.paused));
                case "pause_all":
                    if(command["paused"]?.Type!=JTokenType.Boolean)break;
                    match.PauseAll(team,(bool)command["paused"]);return Result(true);
                case "sync_production":
                    int count=match.SynchronizeProduction(team);
                    return new JObject{["ok"]=count>=2,["count"]=count,["message"]=count<0?"Отряд превышает лимит армии":"Нужно хотя бы два действующих здания выпуска"};
                case "move":
                    return Result(TryPoint(team,command["point"],out var move)&&match.MoveBuilder(team,move));
                case "stop":return Result(match.MoveBuilder(team,match.Builders[team].position));
                case "repair":return Result(match.RepairCastle(team));
                case "builder_spell":
                    if(!TryPoint(team,command["point"],out var point))break;
                    string spell=(string)command["spell"];
                    return Result(spell=="blink"?match.Blink(team,point):spell=="rescue"&&match.Rescue(team,point));
                case "buy_rescue":return Result(match.BuyRescue(team));
                case "buy_item":return Result(match.BuyItem(team,index,out var buyError),buyError);
                case "use_item":
                    if(!TryPoint(team,command["point"],out var itemPoint))break;
                    return Result(match.UseItem(team,index,Int(command["target"],0),itemPoint,out var itemError),itemError);
            }
            return Result(false,"Команда недоступна");
        }

        static JObject Result(bool ok,string error=null)=>ok?new JObject{["ok"]=true}:new JObject{["ok"]=false,["message"]=string.IsNullOrEmpty(error)?"Команда недоступна":error};
        static JArray Point(Vector2 p,float height=0)=>new JArray(p.x,height,p.y);
        static float Yaw(Vector2 direction)=>Mathf.Atan2(direction.x,direction.y);
        static JObject Timers(Dictionary<string,float> timers,float regen)=>new JObject{
            ["regen"]=regen,["cooldowns"]=JObject.FromObject(timers)
        };

        public JObject Snapshot(int viewer){
            if(viewer<0||viewer>1)throw new ArgumentOutOfRangeException(nameof(viewer));
            var units=new JArray();
            foreach(var b in match.Builders)
                if(b.team==viewer||Visible(viewer,b.position))
                    units.Add(new JObject{["t"]=b.team,["builder"]=true,["p"]=Point(b.position),
                        ["yaw"]=Yaw(b.direction),["a"]=new JArray(b.pose),["repair"]=b.repairing});
            foreach(var u in match.Units){
                if(u.team!=viewer&&(!Visible(viewer,u.position)||!match.NightCanDetect(viewer,u)))continue;
                units.Add(new JObject{
                    ["id"]=u.id,["r"]=match.Lines[u.kind].unit,["t"]=u.team,["p"]=Point(u.position),["yaw"]=Yaw(u.direction),
                    ["hp"]=new JArray(u.hp,u.maxHp),["m"]=new JArray(u.mana,u.maxMana),
                    ["stun"]=u.stun,["armor"]=u.armor+match.Stat(u,"armor"),
                    ["a"]=new JArray(u.pose,0,0,u.attackSerial),["auras"]=new JArray(u.auraIds),["abilities"]=new JArray(u.abilities),
                    ["state"]=new JObject{["invisible"]=u.nightInvisible,["panda_split"]=u.pandaSplit,
                        ["stasis"]=u.stasisLeft,["stagger"]=match.PandaStagger(u),
                        ["spell_resist"]=u.spellResist,["crit_chance"]=u.critChance,["crit_multiplier"]=u.critMultiplier,
                        ["evasion"]=u.evasion,["ability_levels"]=JObject.FromObject(u.abilityLevels)},
                    ["spell_ui"]=Timers(u.spellTimers,u.manaRegen+u.auraMana),
                    ["statuses"]=new JArray(u.effects.Where(e=>e.left>0).Select(e=>new JObject{
                        ["id"]=e.id,["code"]=e.id,["remaining"]=e.left,["lifetime"]=e.lifetime,["source_team"]=e.sourceTeam,
                        ["stack_group"]=e.group,["power"]=e.power,["armor"]=e.armor,["haste"]=e.haste,["incoming"]=e.incoming,
                        ["name"]=(string)e.config?["name"],["kind"]=(string)e.config?["kind"],
                        ["slow"]=-e.speed,["root"]=e.root,["ground"]=e.ground
                    }))
                });
            }
            var huts=new JArray(match.Huts.Where(h=>h.team==viewer||Visible(viewer,h.position)||knownHuts[viewer].Contains(h.id))
                .Select(h=>new JObject{["id"]=h.id,["team"]=h.team,["line_raw"]=match.Lines[h.kind].building,["p"]=Point(h.position),
                    ["paid"]=h.paid,["started"]=h.started,["build_left"]=h.buildLeft,["paused"]=h.paused,
                    ["hp"]=new JArray(h.hp,h.maxHp),["m"]=new JArray(h.mana),
                    ["cost"]=h.baseCost,["spent"]=h.spent,["income"]=h.income,["timer"]=h.spawnLeft,
                    ["armor"]=match.BuildingArmor(h),["a"]=new JArray(h.attackPoseLeft>0?"Attack":"Stand",0,0,h.attackSerial),
                    ["spell_ui"]=Timers(h.spellTimers,HumanMatch.F(match.Lines[h.kind].buildingStats,"mana_regen"))}));
            return new JObject{
                ["type"]="snapshot",["units"]=units,["huts"]=huts,
                ["elapsed"]=match.Elapsed,["over"]=Finished,["winner"]=match.Winner,
                ["castles"]=new JArray(match.CastleHp.Select(hp=>new JObject{["hp"]=hp})),
                ["team_overview"]=new JArray(Enumerable.Range(0,2).Select(team=>new JObject{
                    ["income"]=match.Income(team),["buildings"]=match.Huts.Count(h=>h.team==team&&h.paid&&h.hp>0),
                    ["army"]=match.ArmyCount(team)
                })),
                ["income_in"]=match.IncomeIn,
                ["banks"]=new JArray(Enumerable.Range(0,2).Select(Bank)),
                ["rescue"]=new JArray(match.RescueCharges),["rescue_cd"]=new JArray(match.RescueCooldown),
                ["kills"]=new JArray(match.TeamKills),
                ["aspects"]=new JObject{["enabled"]=aspects,["choices"]=new JArray(match.Aspects),["remaining"]=aspectTime},
                ["north_sight"]=new JArray(match.NorthernOrbs.Where(o=>o.team==viewer).Select(o=>new JObject{["team"]=viewer,["p"]=Point(o.position)})),
                ["night_sight"]=new JArray(match.NightZones.Where(z=>z.team==viewer&&z.age<8).Select(z=>new JObject{["team"]=viewer,["p"]=Point(z.point)})),
                ["projectiles"]=new JArray(match.Shots.Where(s=>!s.hit||s.age-s.duration<=.26f).Select(s=>Projectile(s,viewer)).Where(p=>p!=null&&Visible(viewer,new Vector2((float)p["p"][0],(float)p["p"][2]))))
            };
        }

        JObject Bank(int team){
            var bag=match.Inventories[team];
            return new JObject{["gold"]=match.Gold[team],["wood"]=match.Wood[team],["limit"]=match.LegendaryLimit[team],
                ["reserved_gold"]=match.ReservedGold(team),["reserved_wood"]=match.ReservedWood(team),["reserved_slots"]=match.ReservedLegendary(team),
                ["items"]=new JArray(bag.slots.Select(item=>new JObject{["id"]=item.id,["count"]=item.count,["cooldown"]=item.cooldown})),
                ["stock"]=JObject.FromObject(bag.stocks),["item_cooldowns"]=JObject.FromObject(bag.cooldowns)};
        }

        JObject Projectile(HumanMatch.Shot shot,int viewer){
            if(shot.duration<=0||shot.weapon==null||shot.kind==1)return null;
            float t=Mathf.Clamp01(shot.age/shot.duration);
            Vector2 p=Vector2.Lerp(shot.start,shot.end,t);
            float height=Mathf.Lerp(shot.startHeight,shot.endHeight,t)+4*shot.arc*t*(1-t);
            return new JObject{["id"]=shot.id,["path"]=shot.weapon.missile,
                ["p"]=Point(p,height),["origin"]=Visible(viewer,shot.start)?Point(shot.start,shot.startHeight):null,["relative_height"]=true,["t"]=shot.team,["impact_age"]=shot.hit?(float?)shot.duration:null,["yaw"]=Yaw(shot.end-shot.start),["born"]=match.Elapsed-shot.age};
        }
    }
}
#endif
