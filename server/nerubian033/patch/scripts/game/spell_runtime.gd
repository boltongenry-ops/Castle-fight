extends RefCounted

const ACTIVE := ["nerub_cocoon","nerub_command","nerub_fissure","panda_brew","panda_heal","panda_stomp","panda_gust","panda_split","panda_fountain","panda_gong","panda_keg","undead_raise_building","undead_raise_unit","undead_execute","mech_mines","mech_grenade","mech_shield","elemental_barrage","elemental_enchant","elemental_rain","corrupted_pain","corrupted_tentacles","corrupted_impale","north_winter","north_frost_bolt","north_mushroom","north_stomp","north_howl","north_nova","night_roots","night_grant","night_moonshine","night_starfall","elven_hex","elven_solar","elven_heal","naga_tsunami","naga_control","naga_quake","naga_purge","orc_blessing","orc_acid","orc_nets","orc_spell_steal","human_prayer","chaos_roar","twins_blast","nature_banish","nature_cyclone","nature_spirits","nature_trees","wave","mana_burn","heal","regen","buff","debuff","dispel","damage","chain","nova","ground_dot","summon","resurrect","mirror","taunt","repair","control","erase","execute","grant","enchantment","auto_fire","warlock_blast","holy_power","chaos_gift","map_erase","eruption","hell_fist","raise_dead","frost_bolt","tentacle_prison","moonshine","nerub_brood","nerub_acid","nerub_plague"]

static func valid_target(source: Node, target: Node, cfg: Dictionary, ally: bool) -> bool:
	if not is_instance_valid(target) or not target is GameUnit: return false
	return valid_area_target(source.gameplay,source.team,target,cfg,ally)

static func valid_area_target(game: Node, source_team: int, target: Node, cfg: Dictionary, ally := false) -> bool:
	if not is_instance_valid(target) or not target.alive: return false
	if (target.team == source_team) != ally: return false
	var flags := str(cfg.get("targets", ""))
	if target is GameUnit and target.rawcode=="YTct": return flags.contains("tree")
	if target is Building: return flags.contains("structure")
	if not target is GameUnit or target.invulnerable: return false
	for status in target.effects:
		if bool(status.get("untargetable",false)): return false
		if bool(cfg.get("exclude_ethereal",false)) and bool(status.get("ethereal",false)): return false
	if not ally and target.invisible and not game.can_detect(source_team,target) and not bool(cfg.get("aoe",false)) and not str(cfg.get("kind","")) in ["nova","ground_dot","wave"]: return false
	if preload("res://scripts/game/corrupted_rules.gd").airborne(target): return false
	if not ally and target.spell_resist >= 1 and not bool(cfg.get("physical_control",false)): return false
	var types := str(game.unit_traits.get(target.rawcode,{}).get("unit_types", "")).to_lower()
	if str(cfg.get("code",""))=="AHbn" and types.contains("mechanical"): return false
	if flags.contains("organic") and types.contains("mechanical"): return false
	if flags.contains("nonancient") and types.contains("ancient"): return false
	if flags.contains("structure") and not flags.contains("ground") and not flags.contains("air"): return false
	var in_air: bool = target.flying and not target.grounded
	if bool(cfg.get("air_only",false)) and not in_air: return false
	if in_air and flags.contains("ground") and not flags.contains("air"): return false
	if not in_air and flags.contains("air") and not flags.contains("ground"): return false
	return true

static func distance(a: Node3D, b: Node3D) -> float:
	return Vector2(a.position.x-b.position.x,a.position.z-b.position.z).length()

static func effect(unit: Node, cfg: Dictionary, source_team: int, duration := -1.0) -> void:
	if not unit is GameUnit or not unit.alive: return
	var id := str(cfg.get("id", cfg.get("name", "effect")))
	var lifetime := float(cfg.get("hero_duration",cfg.get("duration",0))) if unit.armor_type == "hero" or unit.resistant_skin else float(cfg.get("duration",0))
	if duration >= 0: lifetime = duration
	if lifetime <= 0: return
	if id in ["AUW1","AUW2","AUWB","AUMW"]:
		load("res://scripts/game/nerubian_spells.gd").mark(unit,source_team,12 if id in ["AUW2","AUMW"] else 8)
	var item := cfg.duplicate(true)
	if str(item.get("code",""))=="AOhx":
		item["move_override"] = 150.0/30 if unit.flying and not unit.grounded else 50.0/30
	item["remaining"] = lifetime
	item["lifetime"] = lifetime
	item["source_team"] = source_team
	unit.gameplay.aspects.status_applied(unit,item,source_team)
	if load("res://scripts/game/nerubian_rework033.gd").apply_venom(unit,item): return
	var stack_group := str(cfg.get("stack_group",""))
	if not stack_group.is_empty():
		for old in unit.effects.duplicate():
			if str(old.get("stack_group",""))==stack_group:
				# Compare the effective status after aspect/rank modifiers. A fresh
				# base poison must be able to refresh its own amplified version.
				if float(old.get("stack_strength",old.get("dps",0)))>float(item.get("stack_strength",item.get("dps",0))): return
				item["tick"] = float(old.get("tick",0))
				unit.effects.erase(old)
	for i in unit.effects.size():
		if str(unit.effects[i].get("id","")) == id:
			unit.effects[i] = item
			return
	unit.effects.append(item)
	if bool(item.get("ground",false)):
		unit.grounded = true
		unit.position.y = unit.terrain.height_at_wc3(unit.position.x*30,unit.position.z*30)
	if bool(item.get("root",false)): unit.rooted = true
	if bool(item.get("disarm",false)): unit.disarmed = true
	unit.gameplay.combat_text(unit,preload("res://scripts/ui/unit_details.gd").clean(str(cfg.get("name","ЭФФЕКТ"))),Color(.65,.85,1))

static func has_effect(unit: Node, id: String) -> bool:
	for item in unit.effects:
		if str(item.get("id","")) == id: return true
	return false

static func tick(unit: Node, delta: float) -> void:
	unit.status_stats.clear()
	unit.rooted = false
	unit.disabled = false
	unit.silenced = false
	unit.disarmed = false
	unit.grounded = false
	for item in unit.effects.duplicate():
		var step := minf(delta,float(item["remaining"]))
		item["remaining"] = float(item["remaining"])-delta
		if float(item.get("dps",0)) > 0:
			item["tick"] = float(item.get("tick",0))+step
			var tick := float(item["tick"])
			var interval := maxf(.05,float(item.get("interval",1)))
			var due := floorf(tick/interval)*interval if float(item["remaining"])>0 else tick
			if due > 0:
				unit.set_meta("nerub_final_tick",item)
				unit.gameplay.damage_unit(unit,load("res://scripts/game/nerubian_rework033.gd").periodic(unit,item)*due,int(item["source_team"]),str(item.get("damage_type","magic")),false,true,null,{"ignore_armor":bool(item.get("ignore_armor",false)),"minimum_hp":float(item.get("minimum_hp",0))})
				unit.remove_meta("nerub_final_tick")
				item["tick"] = tick-due
		if not unit.alive: return
		if str(item.get("kind","")) == "regen":
			unit.heal(float(item.get("power",0))*step/float(item["lifetime"]))
			unit.mana = minf(unit.max_mana,unit.mana+float(item.get("restore_mana",0))*step/float(item["lifetime"]))
		elif float(item.get("regen",0))>0:
			unit.heal(float(item["regen"])*step)
		if float(item["remaining"]) <= 0:
			continue
		if bool(item.get("purge",false)): preload("res://scripts/game/naga_rules.gd").update_purge(item,unit)
		for stat in ["armor","haste","speed","miss","incoming"]:
			unit.status_stats[stat] = float(unit.status_stats.get(stat,0))+load("res://scripts/game/nerubian_rework033.gd").stats(unit,item,stat)
		# `power` is contextual: healing HP, spell damage or an incineration
		# charge are not fractions of weapon damage. Only modifier statuses
		# (Inner Fire, Roar, Cripple, etc.) use it as a damage percentage.
		if str(item.get("kind","")) in ["buff","debuff"]:
			unit.status_stats["damage"] = float(unit.status_stats.get("damage",0))+float(item.get("power",0))
		unit.status_stats["speed"] = float(unit.status_stats.get("speed",0))-load("res://scripts/game/nerubian_rework033.gd").stats(unit,item,"slow")
		if bool(item.get("root",false)): unit.rooted = true
		if bool(item.get("disable",false)): unit.disabled = true
		if bool(item.get("silence",false)): unit.silenced = true
		if bool(item.get("disarm",false)): unit.disarmed = true
		if bool(item.get("ground",false)): unit.grounded = true
	unit.effects = unit.effects.filter(func(e):return float(e.remaining)>0)
	unit.status_stats["armor"] = float(unit.status_stats.get("armor",0))+load("res://scripts/game/nerubian_rework033.gd").armor_overlap(unit)
	if unit.summoned_life > 0:
		unit.summoned_life -= delta
		if unit.summoned_life <= 0:
			# Native timed life expires even on an invulnerable Gobbo Machinist.
			unit.invulnerable = false
			unit.gameplay.damage_unit(unit,unit.max_hp*100,-1,"chaos")
	if unit.flying:
		unit.position.y = unit.terrain.height_at_wc3(unit.position.x*30,unit.position.z*30)+(0.0 if unit.grounded else unit.fly_height)

static func cast(source: Node, cfg: Dictionary, suggested: Node = null, preview := false) -> bool:
	var game: Node = source.gameplay
	var kind := str(cfg.get("kind",""))
	if kind in ["panda_brew","panda_heal","panda_stomp","panda_gust","panda_split","panda_fountain","panda_gong","panda_keg"]: return game.pandaren.cast(source,cfg,suggested,preview)
	if kind in ["undead_raise_building","undead_raise_unit","undead_execute"]: return game.undead.cast(source,cfg,suggested,preview)
	if kind.begins_with("mech_"): return game.mech.cast(source,cfg,suggested,preview)
	if kind.begins_with("elemental_"): return game.elemental.cast(source,cfg,suggested,preview)
	if kind.begins_with("corrupted_"): return game.corrupted.cast(source,cfg,suggested,preview)
	if kind.begins_with("north_"): return game.northern.cast(source,cfg,suggested,preview)
	if kind.begins_with("night_"): return game.night.cast(source,cfg,suggested,preview)
	if kind.begins_with("elven_"): return game.elven.cast(source,cfg,suggested,preview)
	if kind=="auto_fire" and str(cfg.get("id","")) in ["A000","A010","A09F"]: return game.elven.auto_fire(source,cfg,suggested,preview)
	if kind.begins_with("naga_"): return preload("res://scripts/game/naga_rules.gd").cast(source,cfg,suggested,preview)
	if kind in ["orc_blessing","orc_acid","orc_nets","orc_spell_steal"]: return preload("res://scripts/game/orc_rules.gd").cast(source,cfg,suggested,preview)
	if kind in ["holy_power","human_prayer"]: return game.human.cast(source,cfg,preview)
	if kind=="resurrect": return game.human.resurrect(source,cfg,preview)
	if kind in ["chaos_roar","twins_blast"]: return preload("res://scripts/game/chaos_rules.gd").cast(source,cfg,suggested,preview)
	if kind.begins_with("nature_"): return game.nature.cast(source,cfg,suggested,preview)
	if kind.begins_with("nerub_"):
		return load("res://scripts/game/nerubian_spells.gd").cast(source,cfg,suggested,preview)
	if kind in ["raise_dead","frost_bolt","tentacle_prison","moonshine"]:
		if preview: return false
		return load("res://scripts/game/advanced_spells.gd").cast(source,cfg)
	if kind in ["warlock_blast","holy_power","chaos_gift","map_erase","eruption","hell_fist"]:
		return load("res://scripts/game/map_spells.gd").cast(source,cfg,suggested,preview)
	var cost := float(cfg.get("mana",0))
	if source.mana < cost: return false
	var ally := bool(cfg.get("ally",false))
	if bool(cfg.get("impact",false)) and not valid_target(source,suggested,cfg,ally): return false
	var radius := float(cfg.get("range",0))
	var candidates: Array = []
	for unit in game.units:
		if valid_target(source,unit,cfg,ally) and distance(source,unit) <= radius:
			if kind=="mana_burn" and unit.mana<=0: continue
			if ally and kind=="chain" and unit.hp>=unit.max_hp: continue
			if ally and kind in ["heal","regen"] and unit.hp >= unit.max_hp and (kind!="regen" or float(cfg.get("restore_mana",0))<=0 or unit.mana>=unit.max_mana): continue
			if kind in ["buff","regen","debuff"] and has_effect(unit,str(cfg.get("id",""))): continue
			if not str(cfg.get("exclude_granted","")).is_empty() and str(cfg["exclude_granted"]) in unit.abilities: continue
			candidates.append(unit)
	if bool(cfg.get("impact",false)):
		candidates.clear()
		candidates.append(suggested)
		if bool(cfg.get("aoe",false)):
			for unit in game.units:
				if unit!=suggested and valid_target(source,unit,cfg,ally) and distance(suggested,unit)<=float(cfg.get("area",0)): candidates.append(unit)
	if not ally and bool(cfg.get("prefer_air",false)):
		candidates.sort_custom(func(a,b): return a.flying and not a.grounded and not (b.flying and not b.grounded))
	elif ally:
		candidates.sort_custom(func(a,b):return a.hp/a.max_hp < b.hp/b.max_hp)
	elif is_instance_valid(suggested) and candidates.has(suggested):
		candidates.erase(suggested)
		candidates.push_front(suggested)
	else: candidates.shuffle()
	var target: Node = candidates[0] if not candidates.is_empty() else null
	if bool(cfg.get("force_target",false)):
		if not candidates.has(suggested): return false
		target = suggested
	if bool(cfg.get("self",false)): target = source
	if kind in ["buff","regen"] and target is GameUnit and has_effect(target,str(cfg.get("id",""))): return false
	if kind not in ["summon","resurrect","mirror"] and target == null: return false
	if preview:
		if kind=="summon" and bool(cfg.get("corpse",false)): return not game.nearest_corpse(source.position,float(cfg.get("range",0))).is_empty()
		if kind=="resurrect":
			for corpse in game.corpses:
				if int(corpse["team"])==source.team and source.position.distance_to(corpse["pos"])<=float(cfg.get("area",0)): return true
			return false
		return true
	if not ally and target is GameUnit and target.spell_shield_timer <= 0 and not bool(cfg.get("impact",false)) and not bool(cfg.get("aoe",false)) and kind not in ["ground_dot","auto_fire"]:
		for id in target.abilities:
			var shield: Dictionary = preload("res://scripts/game/combat_rules.gd").configuration(target,id)
			if str(shield.get("kind","")) == "shield":
				target.spell_shield_timer = float(shield.get("cd",0))
				game.combat_text(target,"МАГИЧЕСКИЙ ЩИТ",Color(.5,.7,1))
				source.mana -= cost
				return true
	var missile_targets: Array = [target]
	if bool(cfg.get("multi",false)) and target!=null:
		missile_targets.clear()
		for candidate in candidates:
			if float(cfg.get("area",0))>0 and distance(candidate,target)>float(cfg["area"]): continue
			missile_targets.append(candidate)
			if missile_targets.size()>=int(cfg.get("count",1)): break
	var launched := false
	if kind in ["damage","debuff","ground_dot"] and target!=null and game.vfx!=null and not bool(cfg.get("impact",false)):
		for victim in missile_targets:
			var payload := cfg.duplicate(true)
			payload["multi"] = false
			launched = preload("res://scripts/game/spell_missile.gd").launch(source,victim,payload) or launched
	if launched:
		source.mana -= cost
		source.cast_pause = maxf(source.cast_pause,source.cast_point if source is GameUnit else 0)
		if not bool(cfg.get("passive_cast",false)): source.play_spell(cfg)
		var launch_visual := cfg.duplicate()
		launch_visual["caster_only"] = true
		game.vfx.cast(source,launch_visual,null)
		return true
	match kind:
		"mana_burn":
			var drained := minf(target.mana,float(cfg.get("power",0)))
			target.mana -= drained
			game.damage_unit(target,drained*float(cfg.get("ratio",1)),source.team,"magic")
		"wave":
			var wave: Node3D = load("res://scripts/game/spell_wave.gd").new()
			game.projectiles_root.add_child(wave)
			wave.setup(source,target,cfg)
		"auto_fire":
			game.damage_unit(target,float(cfg.get("power",0)),source.team,"magic")
			if target.alive and float(cfg.get("dps",0)) > 0: effect(target,cfg,source.team)
		"control":
			if "A070" in target.abilities or "A06V" in target.abilities: return false
			target.change_team(source.team)
		"execute","erase":
			if str(cfg.get("id",""))=="A033" and preload("res://scripts/game/spell_shields.gd").block(target):
				source.mana -= cost
				if source.has_method("play_spell"): source.play_spell(cfg)
				return true
			if "A070" in target.abilities or "A06V" in target.abilities: return false
			for unit in game.units.duplicate():
				if unit == target or (kind == "erase" and unit.rawcode == target.rawcode and unit.team != source.team):
					game.damage_unit(unit,float(cfg.get("power",100500)),source.team,"chaos")
		"grant","enchantment":
			var grants: Array = cfg.get("grant",[]).duplicate()
			if kind == "enchantment":
				if target.max_hp-target.hp > 350: target.heal(450)
				else:
					for id in ["A0D7","A0F3","A0F1"]:
						if not id in target.abilities: grants.append(id)
					if grants.is_empty(): target.heal(450)
					else: grants = [grants.pick_random()]
			if cfg.has("choices_grant"): grants.append_array(cfg["choices_grant"].pick_random())
			for id in grants:
				var level := int(cfg.get("grant_levels",{}).get(id,cfg.get("grant_level",1)))
				preload("res://scripts/game/combat_rules.gd").grant(target,game,id,level)
		"heal","repair":
			var hp := minf(float(cfg.get("power",0)),target.max_hp-target.hp)
			target.hp += hp
			var permanent := float(cfg.get("permanent_armor",0))
			if permanent > 0 and not bool(target.get_meta(str(cfg["id"]),false)):
				target.armor += permanent
				target.set_meta(str(cfg["id"]),true)
			game.combat_text(target,"+%.0f" % hp,Color(.3,1,.4))
			if kind=="heal" and source is GameUnit and str(cfg.id)=="A03K": game.human.after_heal(source,target)
			if kind=="heal" and source is GameUnit and str(cfg.id)=="A0CG": source.cast_pause = maxf(source.cast_pause,1.2)
		"buff","regen","debuff":
			var selected: Array = [target]
			if bool(cfg.get("aoe",false)):
				selected = candidates.filter(func(candidate):return distance(candidate,source if bool(cfg.get("center_self",false)) else target)<=float(cfg.get("area",0)))
			if bool(cfg.get("multi",false)):
				selected = []
				for candidate in candidates:
					if float(cfg.get("area",0))>0 and distance(candidate,target)>float(cfg["area"]): continue
					selected.append(candidate)
					if selected.size()>=int(cfg.get("count",1)): break
			for unit in selected:
				var status := cfg.duplicate()
				if unit!=target and cfg.has("secondary_dps"): status["dps"] = float(cfg["secondary_dps"])
				effect(unit,status,source.team)
		"dispel":
			target.effects = target.effects.filter(func(e):return bool(e.get("undispellable",false)) or bool(e.get("physical_control",false)))
			effect(target,cfg,source.team)
		"damage","nova","ground_dot":
			var center: Vector3 = source.position if bool(cfg.get("center_self",false)) else target.position
			var selected: Array = [target]
			if bool(cfg.get("multi",false)): selected = missile_targets
			if bool(cfg.get("aoe",false)) or kind in ["nova","ground_dot"]:
				selected = []
				for unit in game.units:
					if valid_target(source,unit,cfg,false) and Vector2(unit.position.x-center.x,unit.position.z-center.z).length() <= float(cfg.get("area",0)): selected.append(unit)
			for unit in selected:
				if kind == "ground_dot":
					var dot: Dictionary = cfg.duplicate()
					dot["dps"] = float(cfg.get("power",0))/maxf(.01,float(cfg.get("interval",1)))
					effect(unit,dot,source.team)
				else:
					var amount := float(cfg.get("power",0)) if unit == target else float(cfg.get("secondary",cfg.get("power",0)))
					if amount>0: game.damage_unit(unit,amount,source.team,str(cfg.get("damage_type","magic")))
					if float(cfg.get("stun",0)) > 0: unit.stun_timer = maxf(unit.stun_timer,float(cfg["hero_duration"] if unit.armor_type == "hero" or unit.resistant_skin else cfg["stun"]))
					if unit.alive and (float(cfg.get("slow",0))!=0 or float(cfg.get("haste",0))!=0): effect(unit,cfg,source.team)
		"chain":
			var hit: Array = []
			var current: Node = target
			var amount := float(cfg.get("power",0))
			var previous_point: Vector3 = source.position+Vector3.UP
			for i in int(cfg.get("count",1)):
				if current == null: break
				if str(cfg.get("code","")) in ["AOcl","AOhw"] and game.vfx!=null:
					game.vfx.lightning(("HWPB" if i==0 else "HWSB") if ally else ("CLPB" if i==0 else "CLSB"),previous_point,current.position+Vector3.UP,source.team)
					game.vfx.cast(source,cfg,current)
				previous_point = current.position+Vector3.UP
				hit.append(current)
				if ally: current.heal(amount)
				else: game.damage_unit(current,amount,source.team,"magic")
				var previous: Node = current
				current = null
				# Only the first target is limited by the caster's range. Each
				# subsequent jump searches around the previous victim.
				var nearest := INF
				for unit in game.units:
					if hit.has(unit) or not valid_target(source,unit,cfg,ally): continue
					if ally and unit.hp>=unit.max_hp: continue
					var gap := distance(previous,unit)
					if gap<=float(cfg.get("area",0)) and gap<nearest:
						current = unit
						nearest = gap
				amount *= 1-float(cfg.get("falloff",0))
		"summon","mirror":
			var position: Vector3 = source.position
			if bool(cfg.get("corpse",false)):
				var corpse: Dictionary = game.nearest_corpse(position,float(cfg.get("range",0)))
				if corpse.is_empty(): return false
				position = corpse["pos"]
				game.consume_corpse(corpse)
			var raw: String = source.rawcode if kind == "mirror" else str(cfg.get("summon",""))
			if raw.is_empty(): return false
			for i in int(cfg.get("count",1)):
				var pool: Array = cfg.get("summon_pool",[])
				if not pool.is_empty(): raw = str(cfg["summon_rare"]) if cfg.has("summon_rare") and randf()<float(cfg.get("summon_rare_chance",0)) else str(pool.pick_random())
				var spawn_at := position+Vector3(cos(i*TAU/5)*1.5,0,sin(i*TAU/5)*1.5)
				var created: GameUnit = load("res://scripts/game/advanced_spells.gd").spawn(game,source.team,raw,spawn_at,float(cfg.get("duration",0)))
				for id in cfg.get("summon_grants",[]):
					created.abilities.append(id)
					preload("res://scripts/game/combat_rules.gd").add_ability(created,game,id,game.ability_db.get(id,{}))
				if kind == "mirror":
					created.illusion = true
					created.active_spells.clear()
					created.passive_stats["incoming"] = float(cfg.get("incoming",1))-1
					for weapon in created.weapons: weapon["damage"] = float(weapon["damage"])*float(cfg.get("power",0))
			if bool(cfg.get("corpse",false)) and game.vfx!=null:
				for art in game.vfx.visuals.get(str(cfg["id"]),{}).get("effect",[]): game.vfx.burst_at(art,position,source.team)
		"taunt":
			var taunted := 0
			for unit in game.units:
				if not valid_target(source,unit,cfg,false) or distance(unit,source)>float(cfg.get("area",0)): continue
				unit.combat_target = source
				taunted += 1
				if taunted>=int(cfg.get("count",1)): break
		_:
			return false
	source.mana -= cost
	if kind=="heal" and source is GameUnit and source.rawcode=="e009":
		game.nature.jobs.append({"kind":"keeper_summon","source":weakref(source),"remaining":.6})
	source.cast_pause = maxf(source.cast_pause,float(cfg.get("cast_pause",source.cast_point if source is GameUnit else 0)))
	if not bool(cfg.get("passive_cast",false)): source.play_spell(cfg)
	if game.vfx != null and not bool(cfg.get("corpse",false)):
		if bool(cfg.get("impact",false)): game.vfx.impact(source,cfg,target)
		else:
			var visual_cfg := cfg.duplicate()
			if kind=="wave": visual_cfg["caster_only"] = true
			game.vfx.cast(source,visual_cfg,target)
	if target != null: game.combat_text(target,preload("res://scripts/ui/unit_details.gd").clean(str(cfg.get("name",""))),Color(.65,.8,1))
	return true

static func on_hit(attacker: Node, target: Node, dealt: float) -> void:
	var game: Node = attacker.gameplay
	for id in attacker.abilities:
		var cfg: Dictionary = preload("res://scripts/game/combat_rules.gd").configuration(attacker,id)
		match str(cfg.get("kind","")):
			"corruption":
				if target is GameUnit and valid_target(attacker,target,cfg,false): effect(target,cfg,attacker.team)
			"poison":
				if target is GameUnit and valid_target(attacker,target,cfg,false):
					var venom_cfg: Dictionary = cfg.duplicate(true)
					venom_cfg["nerub_source"] = attacker.get_instance_id()
					effect(target,venom_cfg,attacker.team)
			"feedback": feedback_hit(attacker,target,cfg)
			"orb":
				if target.alive and randf() < float(cfg.get("chance",0)):
					var child: Dictionary = game.ability_db.get(str(cfg.get("effect","")),{}).duplicate()
					if game.elemental.orb_hit(attacker,target,child): continue
					if game.corrupted.orb_hit(attacker,target,child): continue
					if target is Building and game.northern.structure_orb(attacker,target,child): continue
					child["mana"] = 0
					child["cast_pause"] = 0
					child["passive_cast"] = true
					# Orb procs belong to this hit, including refreshes of a debuff.
					child["impact"] = true
					child["range"] = maxf(attacker.attack_range+2,float(child.get("range",0)))
					cast(attacker,child,target)
			"cleave","pulverize","barrage":
				if bool(cfg.get("projectile_barrage",false)): continue
				var kind := str(cfg["kind"])
				if kind == "cleave" and str(attacker.active_weapon.get("weapon_type","normal"))!="normal": continue
				if kind == "pulverize" and randf() >= float(cfg.get("chance",0)): continue
				var count := 0
				var area_cfg := cfg.duplicate()
				area_cfg["physical_control"] = true
				area_cfg["aoe"] = true
				for unit in game.units.duplicate():
					if not valid_target(attacker,unit,area_cfg,false) or (unit == target and kind != "pulverize"): continue
					if distance(unit,target) > maxf(float(cfg.get("half_area",0)),float(cfg.get("area",attacker.attack_range))): continue
					var amount: float = dealt*float(cfg.get("power",0)) if kind == "cleave" else (attacker.damage if kind == "barrage" else float(cfg.get("power",0)))
					if kind == "pulverize" and distance(unit,target) > float(cfg.get("area",0)): amount *= .5
					game.damage_unit(unit,amount,attacker.team,attacker.attack_type,false,false)
					if kind=="cleave" and game.vfx!=null:
						for art in game.vfx.visuals.get(id,{}).get("special",[]): game.vfx.temporary_attach(unit,art)
					count += 1
					if kind == "barrage" and count >= int(cfg.get("count",1))-1: break
			"frost_attack":
				preload("res://scripts/game/northern_rules.gd").chill(attacker,target,cfg)

static func feedback_hit(attacker: Node, target: Node, cfg: Dictionary) -> void:
	if not target is GameUnit or not target.alive: return
	var drained := minf(target.mana,float(cfg.get("power",0)))
	target.mana -= drained
	var bonus := float(cfg.get("summoned_bonus",0)) if target.summoned_unit or target.summoned_life>0 else 0.0
	if drained+bonus>0:
		attacker.gameplay.damage_unit(target,drained*float(cfg.get("ratio",1))+bonus,attacker.team,"magic",false,true,attacker)
		if attacker.gameplay.vfx!=null: attacker.gameplay.vfx.cast(attacker,cfg,target)

static func attack_spell_bonus(attacker: Node, target: Node) -> float:
	# Mark before either component of the hit can kill the victim.
	# Effects expire normally, so a later hit starts a new charge sequence.
	if attacker.illusion: return 0
	for id in attacker.abilities:
		var cfg: Dictionary = preload("res://scripts/game/combat_rules.gd").configuration(attacker,id)
		if str(cfg.get("kind",""))!="incinerate" or not valid_target(attacker,target,cfg,false): continue
		if str(attacker.gameplay.unit_traits.get(target.rawcode,{}).get("unit_types","")).contains("mechanical"): continue
		var stacks := 1
		for old in target.effects:
			if str(old.get("kind",""))=="incinerate" and float(old.get("remaining",0))>0: stacks += int(old.get("stacks",0))
		var mark := cfg.duplicate(true)
		mark["stacks"] = stacks
		effect(target,mark,attacker.team)
		return float(cfg.get("power",0))*stacks
	return 0

static func on_kill(killer: Node, victim: GameUnit) -> void:
	if not is_instance_valid(killer) or not killer is GameUnit or not killer.alive or killer.illusion or killer.team==victim.team: return
	if not str(victim.gameplay.unit_traits.get(victim.rawcode,{}).get("unit_types","")).contains("sapper"): return
	for id in killer.abilities:
		var cfg: Dictionary = preload("res://scripts/game/combat_rules.gd").configuration(killer,id)
		if str(cfg.get("kind",""))!="kill_frenzy": continue
		effect(killer,cfg,killer.team)
		if killer.gameplay.vfx!=null:
			killer.gameplay.vfx.cast(killer,cfg,killer)
			killer.gameplay.vfx.sync(killer)

static func incinerate_blast(game: Node, point: Vector3, team: int, cfg: Dictionary) -> void:
	if not is_instance_valid(game): return
	if game.vfx!=null: game.vfx.named("incinerate_blast",point,team)
	for target in game.units.duplicate():
		if not target is GameUnit or not target.alive or target.invulnerable or target.team==team or target.spell_resist>=1: continue
		if target.rawcode=="YTct": continue
		var untargetable := false
		for status in target.effects:
			if bool(status.get("untargetable",false)): untargetable = true
		if untargetable: continue
		var radius := Vector2(target.position.x-point.x,target.position.z-point.z).length()
		if radius>float(cfg.get("half_area",cfg.get("area",0))): continue
		var amount := float(cfg.get("blast",0)) if radius<=float(cfg.get("area",0)) else float(cfg.get("half_blast",0))
		game.damage_unit(target,amount,team,"magic")

static func on_death(unit: Node) -> void:
	if unit is GameUnit: load("res://scripts/game/nerubian_spells.gd").on_death(unit)
	if unit.illusion: return
	var game: Node = unit.gameplay
	for status in unit.effects:
		if str(status.get("kind",""))!="incinerate" or float(status.get("remaining",0))<=0: continue
		unit.set_meta("incinerated",true)
		var delay := maxf(.001,float(status.get("blast_delay",0)))
		game.get_tree().create_timer(delay,false).timeout.connect(incinerate_blast.bind(game,unit.position,int(status["source_team"]),status.duplicate(true)))
		game.get_tree().create_timer(delay,false).timeout.connect(unit.queue_free)
		break
	for effect in unit.effects:
		if str(effect.get("id",""))=="AUPs": continue
		var raw := str(effect.get("death_spawn",""))
		if not raw.is_empty():
			var spawn_team := int(effect["source_team"])
			for i in int(effect.get("death_spawn_count",effect.get("count",1))):
				if raw=="nN04" and load("res://scripts/game/nerubian_spells.gd").brood_count(game,spawn_team)>=20: break
				var spawned: GameUnit = load("res://scripts/game/advanced_spells.gd").spawn(game,spawn_team,raw,unit.position+Vector3(i*.6,0,0),float(effect.get("death_spawn_life",0)))
				if raw=="nN04": spawned.set_meta("no_bounty",true)
	for id in unit.abilities:
		var cfg: Dictionary = preload("res://scripts/game/combat_rules.gd").configuration(unit,id)
		match str(cfg.get("kind","")):
			"split":
				var raw := str(cfg.get("summon",""))
				if not raw.is_empty():
					for i in int(cfg.get("count",0)):
						if raw=="nN04" and load("res://scripts/game/nerubian_spells.gd").brood_count(game,unit.team)>=20: break
						var child: GameUnit = load("res://scripts/game/advanced_spells.gd").spawn(game,unit.team,raw,unit.position+Vector3(cos(i*TAU/maxi(1,int(cfg["count"]))),0,sin(i*TAU/maxi(1,int(cfg["count"]))))*.8,float(cfg.get("summon_life",0)))
						child.summoned_unit = true
						if raw=="nN04": child.set_meta("no_bounty",true)
			"death_blast":
				var blast := cfg.duplicate()
				blast["aoe"] = true
				var targets: Array = game.units.duplicate()
				if str(cfg.get("targets","")).contains("structure"):
					for hut in game.huts:
						if is_instance_valid(hut.get("node")): targets.append(hut["node"])
				var radius := maxf(float(cfg.get("area",0)),float(cfg.get("half_area",0)))
				for target in targets:
					if not is_instance_valid(target) or not target.alive or target.team==unit.team: continue
					if target is GameUnit and not valid_target(unit,target,blast,false): continue
					var gap := distance(unit,target)
					if gap>radius: continue
					game.damage_unit(target,float(cfg.get("power",0)) if gap<=float(cfg.get("area",0)) else float(cfg.get("half_power",0)),unit.team,"magic")
				if str(cfg.get("targets","")).contains("structure"):
					var enemy: int = 1-unit.team
					var castle_point: Vector3 = game.terrain.wc3_to_world(float(game.castles[enemy]["x"]),64)
					var gap := Vector2(unit.position.x-castle_point.x,unit.position.z-castle_point.z).length()
					if gap<=radius:
						var amount := float(cfg.get("power",0)) if gap<=float(cfg.get("area",0)) else float(cfg.get("half_power",0))
						game.damage_castle(enemy,amount,false,"magic",true)
