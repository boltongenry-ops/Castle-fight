extends RefCounted

const Runtime = preload("res://scripts/game/spell_runtime.gd")
const MARK := "ANMK"
const NETS := ["AUW1","AUW2","AUWB","AUMW"]

static func fighter(unit: Node) -> bool:
	return is_instance_valid(unit) and unit is GameUnit and unit.alive and not unit.controlled and not unit.illusion and not unit.invulnerable and unit.rawcode!="YTct"

static func nerub(unit: Node) -> bool:
	return fighter(unit) and str(unit.rawcode).begins_with("nN0")

static func mark(unit: GameUnit, side: int, power: float) -> void:
	if not fighter(unit) or unit.team==side: return
	Runtime.effect(unit,{"id":MARK,"name":"Добыча роя","kind":"debuff","duration":4,"power":0,"hunt_bonus":power,"stack_strength":power,"stack_group":"nerub_hunt"},side)

static func after_damage(unit: Node, amount: float, spell: bool) -> void:
	if spell or amount<=0 or not nerub(unit): return
	for id in unit.abilities:
		var cfg: Dictionary = unit.gameplay.ability_db.get(id,{})
		if str(cfg.get("kind",""))=="nerub_chitin":
			Runtime.effect(unit,{"id":"ANCH","name":"Живой хитин","kind":"buff","duration":3,"armor":float(cfg.armor),"stack_group":"nerub_chitin"},unit.team)
			break

static func after_weapon(source: Node, target: Node, dealt: float) -> void:
	if dealt<=0 or not is_instance_valid(source) or not source.alive or not is_instance_valid(target) or not target is GameUnit or source.team==target.team: return
	var game: Node = source.gameplay
	if source is GameUnit and source.illusion: return
	if not (nerub(source) or source is Building and source.rawcode in ["hNT1","hNT2"]): return
	for id in source.abilities if source is GameUnit else str(GameDB.unit(source.rawcode).get("uabi","")).split(",",false):
		var cfg: Dictionary = game.ability_db.get(id,{})
		match str(cfg.get("kind","")):
			"nerub_hunt": mark(target,source.team,float(cfg.power))
			"nerub_ambush":
				var hunted: bool = target.effects.any(func(e):return str(e.id)==MARK and int(e.source_team)==source.team and float(e.remaining)>0)
				if (hunted or target.rooted) and game.round_rules.elapsed>=float(source.get_meta("nerub_ambush_after",0)):
					source.set_meta("nerub_ambush_after",game.round_rules.elapsed+8)
					game.damage_unit(target,float(cfg.power),source.team,"normal",false,false,source)
					Runtime.effect(source,{"id":"ANHB","name":"Охотничий азарт","kind":"buff","duration":3,"haste":.25,"speed":.15,"stack_group":"nerub_hunter"},source.team)
			"nerub_scatter":
				var poison: Dictionary = game.ability_db.get(str(cfg.poison),{})
				var hits := int(source.get_meta("nerub_scatter_hits",0))+1
				source.set_meta("nerub_scatter_hits",hits%3)
				if hits%3!=0: continue
				var nearby: Array = game.unit_index.query(target.position,5).filter(func(u):return u!=target and Runtime.valid_target(source,u,poison,false) and Runtime.distance(u,target)<=5)
				nearby.sort_custom(func(a,b):return Runtime.distance(a,target)<Runtime.distance(b,target))
				for victim in nearby.slice(0,int(cfg.count)):
					var venom_cfg: Dictionary = poison.duplicate(true)
					venom_cfg["nerub_source"] = source.get_instance_id()
					Runtime.effect(victim,venom_cfg,source.team)

static func allies(source: Node, cfg: Dictionary) -> Array:
	var list: Array = source.gameplay.units.filter(func(u):return fighter(u) and Runtime.valid_target(source,u,cfg,true) and Runtime.distance(source,u)<=float(cfg.get("range",0)))
	list.sort_custom(func(a,b):
		if (a.rawcode=="nN04")!=(b.rawcode=="nN04"): return a.rawcode!="nN04"
		return a.hp/a.max_hp<b.hp/b.max_hp)
	return list

static func cast_unit(source: Node, cfg: Dictionary, suggested: Node, preview: bool) -> bool:
	var game: Node = source.gameplay
	var kind := str(cfg.kind)
	if kind=="nerub_cocoon":
		var targets := allies(source,cfg).filter(func(u):return u.hp<u.max_hp and not u.effects.any(func(e):return str(e.get("stack_group",""))=="nerub_cocoon"))
		if targets.is_empty(): return false
		if preview: return true
		var target: GameUnit = targets[0]
		if bool(cfg.get("cleanse",false)):
			for status in target.effects.duplicate():
				if int(status.get("source_team",target.team))==target.team or bool(status.get("undispellable",false)): continue
				if float(status.get("dps",0))<=0 and float(status.get("slow",0))<=0 and float(status.get("speed",0))>=0: continue
				target.effects.erase(status)
				break
		var cocoon := {"id":cfg.id,"name":cfg.name,"kind":"regen","duration":3,"power":float(cfg.power),"armor":float(cfg.armor),"stack_group":"nerub_cocoon","stack_strength":float(cfg.power)}
		Runtime.effect(target,cocoon,source.team)
		Runtime.tick(target,0)
		if "ANSH" in source.abilities:
			var brood: Array = game.units.filter(func(u):return fighter(u) and u.team==source.team and u!=target and u.rawcode!="nN04" and not str(game.unit_traits.get(u.rawcode,{}).get("unit_types","")).to_lower().contains("mechanical") and u.hp<u.max_hp and Runtime.distance(target,u)<=200.0/30)
			brood.sort_custom(func(a,b):return a.hp/a.max_hp<b.hp/b.max_hp)
			for child in brood.slice(0,1): child.heal(40)
		if game.vfx!=null: game.vfx.named(str(cfg.id),target.position,source.team)
	elif kind=="nerub_command":
		var threat: bool = game.unit_index.query(source.position,float(cfg.range)).any(func(u):return Runtime.valid_target(source,u,{"targets":"air,ground,enemy","physical_control":true},false) and Runtime.distance(source,u)<=float(cfg.range))
		if not threat: return false
		var targets := allies(source,cfg).filter(func(u):return not Runtime.has_effect(u,"ANCF"))
		if targets.is_empty(): return false
		if preview: return true
		targets.sort_custom(func(a,b):
			if (a.rawcode=="nN04")!=(b.rawcode=="nN04"): return a.rawcode!="nN04"
			return a.position.x*(1 if source.team==0 else -1)>b.position.x*(1 if source.team==0 else -1))
		for target in targets.slice(0,6):
			Runtime.effect(target,{"id":"ANCF","name":"Приказ матриарха","kind":"buff","duration":6,"haste":.2,"speed":.15,"stack_group":"nerub_command"},source.team)
		if game.vfx!=null: game.vfx.named(str(cfg.id),source.position,source.team)
	else:
		var targets: Array = game.unit_index.query(source.position,float(cfg.range)).filter(func(u):return Runtime.valid_target(source,u,cfg,false) and Runtime.distance(source,u)<=float(cfg.range))
		if targets.is_empty(): return false
		targets.sort_custom(func(a,b):return Runtime.distance(source,a)<Runtime.distance(source,b))
		var center: GameUnit = suggested if suggested in targets else targets[0]
		if preview: return true
		var count := 0
		for target in targets:
			if Runtime.distance(target,center)>float(cfg.area): continue
			game.damage_unit(target,float(cfg.power),source.team,"magic",false,true,source)
			if target.alive: Runtime.effect(target,{"id":"ANFR","name":"Разлом хитина","kind":"debuff","duration":1,"hero_duration":.5,"root":true},source.team)
			count += 1
			if count>=3: break
		if game.vfx!=null: game.vfx.named(str(cfg.id),center.position,source.team)
	source.mana -= float(cfg.get("mana",0))
	source.cast_pause = maxf(source.cast_pause,float(cfg.get("cast_pause",source.cast_point if source is GameUnit else 0)))
	if not bool(cfg.get("passive_cast",false)): source.play_spell(cfg)
	return true

static func on_death(victim: GameUnit) -> void:
	load("res://scripts/game/nerubian_rework033.gd").plague_death(victim)

static func brood_count(game: Node, team: int) -> int:
	var count := 0
	for unit in game.units:
		if is_instance_valid(unit) and unit.alive and unit.team==team and unit.rawcode=="nN04": count += 1
	return count

static func brood_front(game: Node, team: int) -> GameUnit:
	var front: GameUnit
	for unit in game.units:
		if not is_instance_valid(unit) or not unit.alive or unit.controlled or unit.invulnerable or unit.flying or unit.team!=team or unit.rawcode=="nN04": continue
		if front==null or unit.position.x*(1 if team==0 else -1)>front.position.x*(1 if team==0 else -1): front = unit
	return front

static func cast(source: Node, cfg: Dictionary, suggested: Node = null, preview := false) -> bool:
	var game: Node = source.gameplay
	var cost := float(cfg.get("mana",0))
	if not source.alive or source.mana<cost: return false
	var kind := str(cfg["kind"])
	if kind in ["nerub_cocoon","nerub_command","nerub_fissure"]: return cast_unit(source,cfg,suggested,preview)
	if kind=="nerub_brood":
		var available := mini(int(cfg["count"]),20-brood_count(game,source.team))
		if available<=0: return false
		var front := brood_front(game,source.team)
		if front==null: return false
		if preview: return true
		for i in available:
			var spawned: GameUnit = load("res://scripts/game/advanced_spells.gd").spawn(game,source.team,str(cfg["summon"]),front.position+Vector3(-1 if source.team==0 else 1,0,(i-1)*.8),float(cfg["duration"]))
			spawned.set_meta("no_bounty",true)
		if game.vfx!=null: game.vfx.named(str(cfg.id),front.position,source.team)
	else:
		var candidates: Array = []
		for unit in game.units:
			if not Runtime.valid_target(source,unit,cfg,false) or Runtime.has_effect(unit,str(cfg["id"])): continue
			if kind=="nerub_plague" and (unit.summoned_unit or unit.illusion): continue
			candidates.append(unit)
		if candidates.is_empty(): return false
		if preview: return true
		# Pick an actual enemy cluster, then cap both radius and number of victims.
		var center: Node = candidates[0]
		var best := -1
		for unit in candidates:
			var score := 0
			for other in game.unit_index.query(unit.position,float(cfg.area)):
				if Runtime.distance(unit,other)>float(cfg.area) or not Runtime.valid_target(source,other,cfg,false) or Runtime.has_effect(other,str(cfg.id)): continue
				if kind=="nerub_plague" and (other.summoned_unit or other.illusion): continue
				score += 1
				if score>=int(cfg.count): break
			if score>best:
				best = score
				center = unit
			if best>=int(cfg.count): break
		var cast_id: int = load("res://scripts/game/nerubian_rework033.gd").plague_cast(game,float(cfg.duration)) if kind=="nerub_plague" else 0
		var count := 0
		for unit in candidates:
			if Runtime.distance(center,unit)>float(cfg["area"]): continue
			var status := cfg.duplicate(true)
			status["kind"] = "debuff"
			# Spell power is the initial hit, not the generic debuff's weapon
			# multiplier. Carrying 35 here gave the victim +3500% attack damage.
			status["power"] = 0.0
			status["nerub_plague_cast"] = cast_id
			if kind=="nerub_acid":
				game.damage_unit(unit,float(cfg["power"]),source.team,"magic")
				status["death_spawn"] = ""
			if unit.alive:
				Runtime.effect(unit,status,source.team)
				mark(unit,source.team,8)
			count += 1
			if count>=int(cfg["count"]): break
		if game.vfx!=null: game.vfx.named(str(cfg.id),center.position,source.team)
	source.mana -= cost
	return true
