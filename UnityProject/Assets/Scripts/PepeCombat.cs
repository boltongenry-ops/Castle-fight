using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static CastleFight.PepeRules;

namespace CastleFight {
static class PepeRules {
    public static readonly Color ReedGreen = new Color(.37f, .85f, .52f);
    public const string PepeMarkId = "PTmp", PepeShieldId = "PShd", MarkImmunity = "PepeMarkImmunity",
        LilyImmunity = "PepeLilyImmunity";
}

public sealed partial class HumanMatch {
    static bool PepeReady(Dictionary<string,float> timers, string id) =>
        !timers.TryGetValue(id, out float left) || left <= 0;

    bool PepeAspect(int team, int side) => Aspects[team] == side &&
        teamBaseBuildings[team].Contains("pB31");

    bool PepeVisible(Unit unit, int team) => !Hidden(unit) && !CorruptedAirborne(unit) &&
        NightCanDetect(team, unit) && (VisionTest == null || VisionTest(team, unit.position));

    bool PepeMainFighter(Unit unit) => unit.hp > 0 && !unit.summoned && !ElementalIllusion(unit) &&
        !Invulnerable(unit) && Lines[unit.kind].Producer && Lines[unit.kind].category != "visual";

    bool PepeGround(Unit unit, int team) => unit.team != team && !Flying(unit) &&
        PepeMainFighter(unit) && PepeVisible(unit, team);

    bool PepeHasMark(Unit unit, int team) => unit.effects.Any(effect =>
        effect.id == PepeMarkId && effect.sourceTeam == team && effect.left > 0);

    bool PepeCanMark(Unit unit, int team) => PepeGround(unit, team) &&
        PepeReady(unit.spellTimers, MarkImmunity) && !PepeHasMark(unit, team);

    bool PepeMark(Unit unit, int team, string sourceSkill = "PTmp") {
        if (!PepeCanMark(unit, team)) return false;
        float duration = PepeAspect(team, 1) ? 7 : 5;
        unit.effects.Add(new Effect {id=PepeMarkId, group=PepeMarkId, left=duration, lifetime=duration,
            sourceTeam=team, config=(JObject)Abilities[PepeMarkId]});
        PulseAt(unit.position,1.6f,ReedGreen,"",.7f,"pepe:"+sourceSkill,unit.id);
        return true;
    }

    internal Effect PepeShield(Unit unit) => unit.effects.Find(effect => effect.id == PepeShieldId &&
        effect.left > 0 && effect.power > 0 && effect.sourceTeam == unit.team);

    void PepeGiveShield(Unit unit, float points, float seconds, string sourceSkill = "PShd") {
        var config=(JObject)Abilities[PepeShieldId];
        if(sourceSkill=="PBll"&&F(Abilities["PBll"],"break_slow")>0){
            config=(JObject)config.DeepClone();
            config["break_slow"]=Abilities["PBll"]["break_slow"].DeepClone();
        }
        unit.effects.Add(new Effect {id=PepeShieldId, group=PepeShieldId, left=seconds, lifetime=seconds,
            power=points, sourceTeam=unit.team, config=config,
            rainRelay=QuackActive&&unit.team==1&&sourceSkill=="PGSh"});
        PulseAt(unit.position, 1.2f, ReedGreen, "", .65f, "pepe:"+sourceSkill,unit.id);
    }

    float PepeMitigate(Unit victim, float amount, bool spell, int team) {
        if (team == victim.team || amount <= 0 || Abilities[PepeShieldId] == null) return amount;
        string id = victim.abilities.Contains("PBr1") ? "PBr1" : victim.abilities.Contains("PBr2") ? "PBr2" : null;
        if (!spell && id != null && PepeReady(victim.spellTimers, id)) {
            var cfg = Abilities[id];
            amount = Mathf.Max(0, amount - F(cfg, "power"));
            victim.spellTimers[id] = F(cfg, "cd");
            PulseAt(victim.position, 1.1f, ReedGreen, "", .45f, "pepe:"+id,victim.id);
        }
        if (amount > 0 && PepeShield(victim) is Effect shield) {
            float absorbed = Mathf.Min(shield.power, amount);
            shield.power -= absorbed;
            amount -= absorbed;
            if (shield.power <= .001f){
                victim.effects.Remove(shield);
                if(F(shield.config,"break_slow")>0)PepeBellShock(victim,F(shield.config,"break_slow"));
            }
        }
        return amount;
    }

    void PepeBellShock(Unit bearer,float slow){
        var status=new JObject{["id"]="PBel",["name"]="Отзвук болотного колокола",["kind"]="debuff",
            ["duration"]=2.5f,["speed"]=-slow,["stack_group"]="pepe_bell_shock"};
        foreach(var enemy in PepeInRange(bearer.position,5).Where(u=>PepeGround(u,bearer.team)&&u.spellResist<1)
            .OrderBy(u=>(u.position-bearer.position).sqrMagnitude).Take(3).ToArray())
            ApplyEffectData(enemy,status,bearer.team);
        PulseAt(bearer.position,5,ReedGreen,"Отзвук колокола",.70f,"pepe:PBll");
    }

    void PepeChangeTeam(Unit unit) {
        unit.effects.RemoveAll(effect => effect.id == PepeMarkId || effect.id == PepeShieldId);
        unit.spellTimers.Remove(MarkImmunity);
        unit.spellTimers.Remove(LilyImmunity);
    }

    float PepeEvasion(Unit unit) => unit.abilities.Contains("PEva") && Flying(unit)
        ? F(Ranked(unit, "PEva"), "chance") : 0;

    float PepeWeaponBonus(Unit owner, Unit target, Weapon weapon) {
        string id = null;
        if (Flying(target) && weapon.air)
            id = owner.abilities.Contains("PAr1") ? "PAr1" : owner.abilities.Contains("PAr2") ? "PAr2" : null;
        else if (owner.abilities.Contains("PLea") && weapon.type == "normal" && PepeGround(target, owner.team))
            id = "PLea";
        else if (owner.abilities.Contains("PDiv") && PepeGround(target, owner.team) && PepeHasMark(target, owner.team))
            id = "PDiv";
        if (id == null || !PepeReady(owner.spellTimers, id)) return 0;
        var cfg = Ranked(owner, id);
        owner.spellTimers[id] = F(cfg, "cd");
        PulseAt(target.position, 1.45f, ReedGreen, "", .55f, "pepe:"+id,target.id);
        return F(cfg, "power");
    }

    void PepeWeaponHit(Unit owner, Unit target, Weapon weapon) {
        string markSkill = owner.abilities.Contains("PMrk") ? "PMrk" :
            owner.abilities.Contains("PMr2") ? "PMr2" : null;
        if (markSkill != null && PepeReady(owner.spellTimers, markSkill) && PepeMark(target, owner.team, markSkill))
            owner.spellTimers[markSkill] = F(Abilities[markSkill], "cd");
        if (weapon.type != "normal") return;
        if (owner.abilities.Contains("PGSh") && PepeShield(owner) == null && PepeHasMark(target, owner.team)) {
            target.effects.RemoveAll(effect => effect.id == PepeMarkId && effect.sourceTeam == owner.team);
            target.spellTimers[MarkImmunity] = 3;
            float amount = owner.abilities.Contains("PQua") ? 105 : owner.abilities.Contains("PBr2") ? 75 : 45;
            PepeGiveShield(owner, amount * (PepeAspect(owner.team, 0) ? 1.25f : 1), 3,"PGSh");
        }
        if (!owner.abilities.Contains("PQua") || !PepeReady(owner.spellTimers, "PQua")) return;
        var cfg = Abilities["PQua"];
        var victims = PepeInRange(target.position, F(cfg, "area"))
            .Where(unit => unit != target && PepeGround(unit, owner.team)).Take(3).ToArray();
        if (victims.Length == 0) return;
        owner.spellTimers["PQua"] = F(cfg, "cd");
        foreach (var unit in victims) {
            DamageUnit(unit, F(cfg, "power"), owner.team, "normal", false, false, owner);
            ApplyEffect(unit, "PQDb", owner.team);
        }
        PulseAt(target.position, F(cfg, "area"), ReedGreen, "Громовой квак", .8f,"pepe:PQua");
    }

    IEnumerable<Unit> PepeInRange(Vector2 center, float radius) => RadiusUnits035(center, radius)
        .Where(unit => (unit.position - center).sqrMagnitude <= radius * radius);

    void PepeArtilleryImpact(Shot shot) {
        if (FindUnit(shot.sourceId) is not Unit source || source.team != shot.team) return;
        string id = source.abilities.Contains("PSg1") ? "PSg1" :
            source.abilities.Contains("PSg2") ? "PSg2" : null;
        if (id == null || !PepeReady(source.spellTimers, id)) return;
        var cfg = Abilities[id];
        var enemies = PepeInRange(shot.end, F(cfg, "area")).Where(unit => PepeGround(unit, shot.team))
            .OrderBy(unit => (unit.position - shot.end).sqrMagnitude).ThenBy(unit => unit.id).Take(3).ToArray();
        if (enemies.Length == 0) return;
        source.spellTimers[id] = F(cfg, "cd");
        foreach (var enemy in enemies) ApplyEffect(enemy, (string)cfg["effect"], shot.team);
        PulseAt(shot.end, F(cfg, "area"), ReedGreen, "", .65f,"pepe:"+id);
    }

    bool PepeTryHeal(Unit sage) {
        string heal=sage.abilities.Contains("PHe2")?"PHe2":"PHea";
        if (!sage.abilities.Contains(heal) || !PepeReady(sage.spellTimers, heal) || Silenced(sage)) return false;
        var cfg = Abilities[heal];
        float mana = F(cfg, "mana");
        if (sage.mana < mana) return false;
        var ally = PepeInRange(sage.position, F(cfg, "area"))
            .Where(unit => unit.team == sage.team && PepeMainFighter(unit) && PepeVisible(unit, sage.team)
                && unit.maxHp - unit.hp >= F(cfg, "minimum"))
            .OrderBy(unit => unit.hp / unit.maxHp).ThenBy(unit => unit.id).FirstOrDefault();
        if (ally == null) return false;
        sage.mana -= mana;
        sage.spellTimers[heal] = F(cfg, "cd");
        sage.castPause = .35f;
        sage.pose = sage.finalCastPose = "Attack";
        sage.attackSerial++;
        ally.hp = Mathf.Min(ally.maxHp, ally.hp + F(cfg, "power"));
        Heals++;
        SpellsCast++;
        PulseAt(ally.position, 1, ReedGreen, "", .85f, "pepe:"+heal,ally.id);
        return true;
    }

    void TickPepeBuilding(Hut building) {
        var line = Lines[building.kind];
        if (building.mana < 10 || !line.building.StartsWith("pB", StringComparison.Ordinal) || line.abilities.Length != 1) return;
        string id = line.abilities[0];
        if (id != "PHrn" && id != "PHr2" && id != "PLil" && id != "PBll") return;
        if (!PepeReady(building.spellTimers, id)) return;
        var cfg = Abilities[id];
        float mana = F(cfg, "mana");
        if (building.mana < mana) return;
        IEnumerable<Unit> nearby = (bool?)cfg["global"] == true ? Units : PepeInRange(building.position, F(cfg, "area"));
        int count = (int)F(cfg, "count");
        string name = (string)cfg["name"];
        Vector2 center = building.position;
        switch (id) {
            case "PHrn":
            case "PHr2":
                var marked = nearby.Where(unit => PepeCanMark(unit, building.team))
                    .OrderBy(unit => (unit.position - building.position).sqrMagnitude)
                    .ThenBy(unit => unit.id).Take(count).ToArray();
                if (marked.Length == 0) return;
                foreach (var unit in marked) PepeMark(unit, building.team);
                break;
            case "PLil":
                float radius = F(cfg, "radius");
                bool Eligible(Unit unit) => PepeGround(unit, building.team) && PepeReady(unit.spellTimers, LilyImmunity);
                var anchors = nearby.Where(Eligible).OrderBy(unit => (unit.position - building.position).sqrMagnitude)
                    .ThenBy(unit => unit.id).ToArray();
                Unit[] slowed = null;
                foreach (var anchor in anchors) {
                    var cluster = PepeInRange(anchor.position, radius).Where(Eligible).Take(count).ToArray();
                    if (cluster.Length < 2) continue;
                    slowed = cluster;
                    center = anchor.position;
                    break;
                }
                if (slowed == null) return;
                foreach (var unit in slowed) {
                    unit.spellTimers[LilyImmunity] = 6;
                    ApplyEffect(unit, PepeAspect(building.team, 1) ? "PLD2" : "PLDb", building.team);
                }
                break;
            case "PBll":
                var shielded = nearby.Where(unit => unit.team == building.team && PepeMainFighter(unit)
                    && PepeVisible(unit, building.team) && (unit.attackPoseLeft > 0 || unit.combatTarget > 0)
                    && PepeShield(unit) == null).OrderBy(unit => unit.hp / unit.maxHp)
                    .ThenBy(unit => unit.id).Take(count).ToArray();
                if (shielded.Length < 2) return;
                foreach (var unit in shielded) PepeGiveShield(unit,F(cfg,"power"),6,F(cfg,"break_slow")>0?"PBll":"PShd");
                break;
        }
        building.mana -= mana;
        building.spellTimers[id] = F(cfg, "cd");
        SpellsCast++;
        PulseAt(center, id == "PLil" ? F(cfg,"radius") : 2, ReedGreen, name, .9f,"pepe:"+id);
    }
}
}
