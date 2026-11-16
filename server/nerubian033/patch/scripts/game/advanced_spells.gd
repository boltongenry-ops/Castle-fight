extends RefCounted

const Runtime = preload("res://scripts/game/spell_runtime.gd")

static func cast(source: Node, cfg: Dictionary) -> bool:
	var game: Node = source.gameplay
	if str(cfg.get("kind","")) in ["undead_raise_building","undead_raise_unit","undead_execute"]: return game.undead.cast(source,cfg,null,false)
	var cost := float(cfg.get("mana",0))
	if source.mana<cost or not source.alive: return false
	var target: Node
	match str(cfg["kind"]):
		"raise_dead":
			var corpses: Array = []
			for corpse in game.corpses:
				var types := str(game.unit_traits.get(str(corpse["rawcode"]),{}).get("unit_types","")).to_lower()
				if types.contains("sapper") and not types.contains("undead"): corpses.append(corpse)
			if corpses.is_empty(): return false
			var corpse: Dictionary = corpses.pick_random()
			var raw := str(cfg["rare"] if randf()<float(cfg["rare_chance"]) else (cfg["pool"] as Array).pick_random())
			game.consume_corpse(corpse)
			spawn(game,source.team,raw,corpse["pos"],0)
			if game.vfx!=null: game.vfx.named("raise_dead",corpse["pos"],source.team)
		"tentacle_prison":
			var targets: Array = []
			for unit in game.units:
				if Runtime.valid_target(source,unit,{"targets":"ground,enemies"},false) and not unit.flying: targets.append(unit)
			if targets.is_empty(): return false
			target = targets.pick_random()
			for i in int(cfg["count"]):
				var angle := i*TAU/float(cfg["count"])
				var point: Vector3 = target.position+Vector3(cos(angle),0,sin(angle))*float(cfg["ring_radius"])
				var tentacle := spawn(game,source.team,str(cfg["summon"]),point,float(cfg["duration"]))
				tentacle.speed = 0
		"frost_bolt":
			var targets: Array = []
			for hut in game.huts:
				var node = hut.get("node")
				if int(hut["team"])==source.team or not is_instance_valid(node) or not node.alive: continue
				if str(hut["line_raw"])=="h06M" or bool(node.get_meta("B01K",false)): continue
				targets.append(node)
			if targets.is_empty(): return false
			target = targets.pick_random()
			var raw := str(cfg["missile_raw"])
			var weapon: Dictionary = game.unit_traits[raw]["weapons"][0]
			var projectile := Projectile.new()
			projectile.model_path = game.missile_for(raw,"frost bolt","magic")
			projectile.speed = float(weapon.get("missile_speed",50))
			projectile._life = source.position.distance_to(target.position)/maxf(.1,projectile.speed)+3
			projectile.impact_callback = load("res://scripts/game/advanced_spells.gd").frost_impact.bind(game,source.team,cfg)
			game.projectiles_root.add_child(projectile)
			projectile.setup(source.global_position+Vector3.UP,target,0,source.team,game)
		"moonshine":
			game.round_rules.moonshine_charges[source.team] += 1
			if game.vfx!=null: game.vfx.named("moonshine_ready",source.position,source.team)
		_: return false
	source.mana -= cost
	source.cast_pause = maxf(source.cast_pause,.1)
	source.play_spell(cfg)
	if game.vfx!=null and is_instance_valid(target): game.vfx.cast(source,cfg,target)
	return true

static func spawn(game: Node, team: int, raw: String, point: Vector3, life: float) -> GameUnit:
	game.spawn_unit(team,game.name_for(raw),point.x*30,point.z*30,5120 if team==0 else -5120,false,"res://assets/models_lines/%s_u.glb" % raw,raw,false)
	var unit: GameUnit = game.units.back()
	unit.summoned_life = life
	unit.summoned_unit = life>0
	unit.set_meta("spell_summoned",true)
	unit.exit_route.clear()
	unit.passed_own_arch = true
	load("res://scripts/game/nerubian_rework033.gd").spider(unit)
	return unit

static func frost_impact(target: Node, point: Vector3, game: Node, team: int, cfg: Dictionary) -> void:
	if not is_instance_valid(target) or not target.alive: return
	for hut in game.huts.duplicate():
		var node = hut.get("node")
		if not is_instance_valid(node) or not node.alive or int(hut["team"])==team: continue
		if node!=target and Vector2(node.position.x-point.x,node.position.z-point.z).length()>float(cfg["area"]): continue
		node.apply_disable("Bfrz",float(cfg["freeze"]))
		game.damage_unit(node,float(cfg["power"]),team,"chaos",false,false)
		game.combat_text(node,"ЗАМОРОЗКА · %.0f с" % float(cfg["freeze"]),Color(.4,.8,1))
		if game.vfx!=null: game.vfx.named("frost_impact",node.position,team)
		if game.vfx!=null: game.vfx.named("frost_freeze",node.position,team,float(cfg["freeze"]))

static func on_attacked(victim: Node) -> void:
	var game: Node = victim.gameplay
	if not victim.alive or victim.invulnerable or "A08H" in victim.abilities or game.round_rules.moonshine_charges[victim.team]<=0: return
	var types := str(game.unit_traits.get(victim.rawcode,{}).get("unit_types","")).to_lower()
	if not types.contains("sapper"): return
	game.round_rules.moonshine_charges[victim.team] -= 1
	var native: Dictionary = game.ability_db.get("A08A",{})
	var fields: Dictionary = native.get("fields",{})
	var cfg := {"id":"A08A","kind":"buff","name":"Лунное благословение","armor":float(fields.get("Idef",6)),"duration":float(native.get("duration",6))}
	for unit in game.units:
		if not is_instance_valid(unit) or not unit.alive or unit.team!=victim.team: continue
		if Runtime.distance(unit,victim)>float(native.get("area",250.0/30)) or not Runtime.valid_area_target(game,victim.team,unit,native,true): continue
		unit.heal(float(fields.get("Ihp2",175)))
		unit.mana = minf(unit.max_mana,unit.mana+float(fields.get("Imp2",50)))
		Runtime.effect(unit,cfg,victim.team)
		if game.vfx!=null: game.vfx.sync(unit)
