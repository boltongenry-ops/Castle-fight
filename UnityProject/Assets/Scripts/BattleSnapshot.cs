using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
    internal static class BattleSnapshot {
        static JArray Point(Vector2 p,float height=0)=>new JArray(p.x,height,p.y);
        static float Yaw(Vector2 direction)=>Mathf.Atan2(direction.x,direction.y);
        static JObject Timers(Dictionary<string,float> timers,float regen)=>new JObject{
            ["regen"]=regen,["cooldowns"]=JObject.FromObject(timers)
        };

        public static JObject Capture(HumanMatch match,int viewer,Func<Vector2,bool> visible,
            Func<HumanMatch.Hut,bool> rememberHut,bool aspects=false,float aspectTime=0){
            if(viewer<0||viewer>1)throw new ArgumentOutOfRangeException(nameof(viewer));
            var units=new JArray();
            foreach(var b in match.Builders)
                if(b.team==viewer||visible(b.position))
                    units.Add(new JObject{["t"]=b.team,["builder"]=true,["p"]=Point(b.position),
                        ["yaw"]=Yaw(b.direction),["a"]=new JArray(b.pose),["repair"]=b.repairing,
                        ["repair_building"]=b.repairing?b.repairBuilding:-1});
            foreach(var u in match.Units){
                if(u.team!=viewer&&(!visible(u.position)||!match.NightCanDetect(viewer,u)))continue;
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
            var huts=new JArray(match.Huts.Where(h=>h.team==viewer||visible(h.position)||rememberHut(h))
                .Select(h=>new JObject{["id"]=h.id,["team"]=h.team,["line_raw"]=match.Lines[h.kind].building,["p"]=Point(h.position),
                    ["paid"]=h.paid,["started"]=h.started,["build_left"]=h.buildLeft,["paused"]=h.paused,
                    ["hp"]=new JArray(h.hp,h.maxHp),["m"]=new JArray(h.mana),
                    ["cost"]=h.baseCost,["spent"]=h.spent,["income"]=h.income,["timer"]=h.spawnLeft,
                    ["armor"]=match.BuildingArmor(h),["a"]=new JArray(h.attackPoseLeft>0?"Attack":"Stand",0,0,h.attackSerial),
                    ["spell_ui"]=Timers(h.spellTimers,HumanMatch.F(match.Lines[h.kind].buildingStats,"mana_regen"))}));
            return new JObject{
                ["type"]="snapshot",["units"]=units,["huts"]=huts,
                ["elapsed"]=match.Elapsed,["over"]=match.Finished,["winner"]=match.Winner,
                ["castles"]=new JArray(match.CastleHp.Select(hp=>new JObject{["hp"]=hp})),
                ["team_overview"]=new JArray(Enumerable.Range(0,2).Select(team=>new JObject{
                    ["income"]=match.Income(team),["buildings"]=match.Huts.Count(h=>h.team==team&&h.paid&&h.hp>0),
                    ["army"]=match.ArmyCount(team)
                })),
                ["income_in"]=match.IncomeIn,
                ["banks"]=new JArray(Enumerable.Range(0,2).Select(team=>Bank(match,team))),
                ["rescue"]=new JArray(match.RescueCharges),["rescue_cd"]=new JArray(match.RescueCooldown),
                ["kills"]=new JArray(match.TeamKills),
                ["aspects"]=new JObject{["enabled"]=aspects,["choices"]=new JArray(match.Aspects),["remaining"]=aspectTime},
                ["north_sight"]=new JArray(match.NorthernOrbs.Where(o=>o.team==viewer).Select(o=>new JObject{["team"]=viewer,["p"]=Point(o.position)})),
                ["night_sight"]=new JArray(match.NightZones.Where(z=>z.team==viewer&&z.age<8).Select(z=>new JObject{["team"]=viewer,["p"]=Point(z.point)})),
                ["projectiles"]=new JArray(match.Shots.Where(s=>!s.hit||s.age-s.duration<=.26f).Select(s=>Projectile(match,s,visible)).Where(p=>p!=null&&visible(new Vector2((float)p["p"][0],(float)p["p"][2]))))
            };
        }

        static JObject Bank(HumanMatch match,int team){
            var bag=match.Inventories[team];
            return new JObject{["gold"]=match.Gold[team],["wood"]=match.Wood[team],["limit"]=match.LegendaryLimit[team],
                ["reserved_gold"]=match.ReservedGold(team),["reserved_wood"]=match.ReservedWood(team),["reserved_slots"]=match.ReservedLegendary(team),
                ["items"]=new JArray(bag.slots.Select(item=>new JObject{["id"]=item.id,["count"]=item.count,["cooldown"]=item.cooldown})),
                ["stock"]=JObject.FromObject(bag.stocks),["item_cooldowns"]=JObject.FromObject(bag.cooldowns)};
        }

        static JObject Projectile(HumanMatch match,HumanMatch.Shot shot,Func<Vector2,bool> visible){
            if(shot.duration<=0||shot.weapon==null||shot.kind==1)return null;
            float t=Mathf.Clamp01(shot.age/shot.duration);
            Vector2 p=Vector2.Lerp(shot.start,shot.end,t);
            float height=Mathf.Lerp(shot.startHeight,shot.endHeight,t)+4*shot.arc*t*(1-t);
            return new JObject{["id"]=shot.id,["path"]=shot.weapon.missile,
                ["p"]=Point(p,height),["origin"]=visible(shot.start)?Point(shot.start,shot.startHeight):null,["relative_height"]=true,["t"]=shot.team,["impact_age"]=shot.hit?(float?)shot.duration:null,["yaw"]=Yaw(shot.end-shot.start),["born"]=match.Elapsed-shot.age};
        }
    }
}
