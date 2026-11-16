extends RefCounted

# Match-scoped, server-authoritative selection and immutable per-unit bonuses.
# Original race/ability databases remain immutable; area effects tick at 2 Hz.
const PICK_SECONDS := 30.0
static var _catalog: Dictionary = {}
static var _by_id: Dictionary = {}
static var _lines: Dictionary = {}
static var _legendary_producers: Dictionary = {}
var game: Node
var remaining := PICK_SECONDS
var _pulse := 0.0
var reinforcement := [30.0,30.0]
const Runtime = preload("res://scripts/game/spell_runtime.gd")

static func catalog() -> Dictionary:
	if _catalog.is_empty():
		_catalog = JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/aspects.json")).races
		for choices in _catalog.values():
			for choice in choices: _by_id[str(choice.id)] = choice
	return _catalog

static func options(race: String) -> Array:
	return catalog().get(race,[])

static func info(values: Dictionary) -> Dictionary:
	if values.is_empty(): return {}
	catalog()
	var choice: Dictionary = _by_id.get(str(values.get("id","")),{})
	if choice.is_empty(): return {}
	var result := values.duplicate(true)
	for key in ["title","description","icon","icon_path"]: result[key] = choice[key]
	return result

static func line_units(root: String) -> Array:
	if _lines.has(root): return _lines[root]
	var result: Array = []
	var queue: Array = [root]
	var seen: Dictionary = {}
	while not queue.is_empty():
		var raw: String = str(queue.pop_front())
		if seen.has(raw): continue
		seen[raw] = true
		var entry: Dictionary = GameDB.unit(raw)
		for unit in str(entry.get("utra","")).split(",",false):
			if not unit in result: result.append(unit)
		queue.append_array(str(entry.get("uupt","")).split(",",false))
	_lines[root] = result
	return result

func setup(owner_game: Node) -> void:
	game = owner_game
	if Match.aspects_enabled: producer_for("")
	if Match.aspects_enabled and not Net.active() and Match.aspect_choices[1]<0:
		choose(1,randi_range(0,1))

func waiting() -> bool:
	return Match.aspects_enabled and (-1 in Match.aspect_choices)

func selected(side: int) -> Dictionary:
	if not Match.aspects_enabled or side not in [0,1]: return {}
	var index: int = Match.aspect_choices[side]
	var values := options(Match.player_race if side==0 else Match.enemy_race)
	return values[index] if index>=0 and index<values.size() else {}

func has(side: int, id: String) -> bool:
	return str(selected(side).get("id",""))==id

static func valid_index(value: Variant, allow_unpicked := false) -> bool:
	return typeof(value) in [TYPE_INT,TYPE_FLOAT] and is_finite(float(value)) and float(value)==floorf(float(value)) and int(value) in ([-1,0,1] if allow_unpicked else [0,1])

func choose(side: int, index: int) -> bool:
	if not Match.aspects_enabled or side not in [0,1] or index not in [0,1] or Match.aspect_choices[side]!=-1: return false
	var race: String = Match.player_race if side==0 else Match.enemy_race
	if options(race).size()!=2: return false
	Match.aspect_choices[side] = index
	if not waiting() and not Net.is_server: MenuMusic.stop_music()
	return true

func request(index: int) -> bool:
	if Net.is_client: return Net.command("aspect",{"index":index})
	return choose(0,index)

func tick(delta: float) -> void:
	if waiting():
		remaining = maxf(0,remaining-delta)
		if remaining<=0:
			for side in 2:
				if Match.aspect_choices[side]<0: choose(side,0)
		return
	if not Match.aspects_enabled or Net.is_client: return
	_pulse += delta
	if _pulse<.5: return
	var step := minf(1,_pulse)
	_pulse = 0
	for side in 2:
		if has(side,"human_reinforce"):
			reinforcement[side] -= step
			if reinforcement[side]<=0:
				reinforcement[side] = 30
				var producers: Array = game.huts.filter(func(h):return int(h.team)==side and working(h) and not str(h.get("rawcode","")).is_empty() and not bool(h.get("tower",false)) and preload("res://scripts/game/economy.gd").slots(str(h.line_raw))==0)
				if not producers.is_empty():
					var hut: Dictionary = producers.pick_random()
					game.spawn_unit(side,str(hut.unit_kind),float(hut.x),float(hut.y),5120 if side==0 else -5120,false,str(hut.unit_model),str(hut.rawcode),false)
		var rot: Dictionary = {}
		var healing: Dictionary = {}
		for unit in game.units.duplicate():
			if not fighter(unit) or unit.team!=side: continue
			if has(side,"chaos_blood") and float(unit.get_meta("aspect_attacking_until",0))>game.round_rules.elapsed:
				unit.hp = minf(unit.hp,maxf(1,unit.hp-unit.max_hp*.01*step))
			if has(side,"corrupted_rot"):
				for enemy in nearby(unit.position,200.0/30,1-side): rot[enemy.get_instance_id()] = enemy
		for hut in game.huts:
			if int(hut.team)!=side or not working(hut): continue
			if has(side,"nature_forest"):
				for ally in nearby(hut.node.position,450.0/30,side):
					var id: int = ally.get_instance_id()
					healing[id] = {"unit":ally,"rate":minf(6,float(healing.get(id,{}).get("rate",0))+2)}
			var queen := false
			var sea := has(side,"naga_creatures") and str(hut.line_raw)=="h00G"
			if queen or sea:
				var period := 8.0 if queen else 20.0
				hut["aspect_spawn_timer"] = float(hut.get("aspect_spawn_timer",period))-step
				if float(hut.aspect_spawn_timer)<=0:
					hut.aspect_spawn_timer = period
					var count := 0
					for unit in game.units:
						if fighter(unit) and unit.team==side and (unit.rawcode=="nN04" if queen else unit.get_meta("aspect_sea",false)): count += 1
					if count<(12 if queen else 6):
						var raw := "nN04" if queen else ("n00I" if randf()<.5 else "n00W")
						var point: Vector3 = hut.node.position+Vector3(2 if side==0 else -2,0,1)
						if queen:
							var front := preload("res://scripts/game/nerubian_spells.gd").brood_front(game,side)
							# A mobile front anchor prevents most of the lifetime being
							# wasted crossing the lane; spiderlings cannot leapfrog it.
							if front==null: continue
							point = front.position+Vector3(-1 if side==0 else 1,0,.8)
						var summon := spawn(side,raw,point,24 if queen else 30)
						if not queen:
							summon.set_meta("aspect_sea",true)
							simple_summon(summon,250,20,1.4)
		for enemy in rot.values():
			if enemy.alive:
				game.damage_unit(enemy,8*step,side,"magic",false,true,null,{"aspect_proc":true})
				visual(enemy,side)
		for row in healing.values():
			if row.unit.alive and row.unit.hp<row.unit.max_hp:
				row.unit.heal(float(row.rate)*step,true)
				visual(row.unit,side)

func working(hut: Dictionary) -> bool:
	return is_instance_valid(hut.get("node")) and hut.node.alive and hut.node.disabled_for<=0 and not bool(hut.get("paused",false))

static func fighter(unit: Node) -> bool:
	return is_instance_valid(unit) and unit is GameUnit and unit.alive and not unit.controlled and not unit.invulnerable and not unit.illusion and unit.rawcode!="YTct" and not unit.get_meta("native_hidden",false)

func nearby(point: Vector3, radius: float, side: int) -> Array:
	var candidates: Array = game.unit_index.query(point,radius).filter(func(u):return fighter(u) and u.team==side and Vector2(u.position.x-point.x,u.position.z-point.z).length()<=radius)
	candidates.sort_custom(func(a,b):return Vector2(a.position.x-point.x,a.position.z-point.z).length_squared()<Vector2(b.position.x-point.x,b.position.z-point.z).length_squared())
	return candidates

func spawn(side: int, raw: String, point: Vector3, lifetime: float) -> GameUnit:
	var unit: GameUnit = load("res://scripts/game/advanced_spells.gd").spawn(game,side,raw,point,lifetime)
	unit.set_meta("no_bounty",true)
	unit.set_meta("aspect_spawn",true)
	return unit

func simple_summon(unit: GameUnit, health: float, damage: float, cooldown: float) -> void:
	unit.max_hp = health; unit.hp = health; unit.armor = 0
	unit.abilities.clear(); unit.active_spells.clear(); unit.aura_sources.clear()
	unit.ability_id = ""; unit.ability_cfg = {}; unit.passive_stats.clear()
	unit.crit_chance = 0; unit.evasion = 0; unit.spell_resist = 0; unit.skin_profile = {}; unit.bash_profile = {}
	unit.damage = damage; unit.attack_cd = cooldown
	for weapon in unit.weapons: weapon.damage = damage; weapon.cooldown = cooldown

func modifiers(unit: Node) -> Dictionary:
	var choice := selected(unit.team)
	if choice.is_empty(): return {}
	var result: Dictionary = {}
	for rule in choice.rules:
		var matches: bool = unit.rawcode in rule.get("units",[])
		for root in rule.get("lines",[]):
			if unit.rawcode in line_units(str(root)): matches = true
		if bool(rule.get("all",false)):
			matches = true
			# Warcraft melee reach can exceed 90 units (spiders, ents). The
			# native weapon delivery mode, not its range, defines the role.
			if rule.get("weapon","")=="ranged": matches = not melee(unit)
			elif rule.get("weapon","")=="melee": matches = melee(unit)
		if not matches: continue
		for key in rule.mods: result[key] = float(result.get(key,0))+float(rule.mods[key])
	return result

static func melee(unit: GameUnit) -> bool:
	if not unit.weapons.is_empty(): return str(unit.weapons[0].get("weapon_type","normal"))=="normal"
	return unit.attack_range<=3.0

func created(unit: Node) -> void:
	if not Match.aspects_enabled: return
	if Net.is_client or not fighter(unit) or unit.stationary or not unit.aspect_info.is_empty(): return
	var before: Dictionary = {}
	for key in ["max_hp","armor","mana_regen","hp_regen","speed","evasion","spell_resist","stealth_bonus"]: before[key] = float(unit.get(key))
	var mods := modifiers(unit)
	var choice := selected(unit.team)
	if choice.is_empty(): return
	unit.aspect_info = {"id":choice.id,"title":choice.title,"description":choice.description,"icon":choice.icon,"icon_path":choice.icon_path,"mods":mods}
	var ratio: float = unit.hp/maxf(1,unit.max_hp)
	unit.max_hp *= 1+float(mods.get("hp",0))
	unit.hp = unit.max_hp*ratio
	unit.armor += float(mods.get("armor",0))
	unit.mana_regen *= 1+float(mods.get("mana",0))
	unit.hp_regen += unit.max_hp*float(mods.get("regen_pct",0))
	unit.hp_regen += float(mods.get("regen",0))
	unit.speed = maxf(0,unit.speed+float(mods.get("move_flat",0)))
	if unit.spell_resist<.8: unit.spell_resist = minf(.8,unit.spell_resist+float(mods.get("resist",0)))
	if unit.evasion<.4: unit.evasion = minf(.4,unit.evasion+float(mods.get("evade",0)))
	unit.stealth_bonus *= 1+float(mods.get("stealth",0))
	var deltas: Dictionary = {}
	for key in before: deltas[key] = float(unit.get(key))-float(before[key])
	unit.set_meta("aspect_deltas",deltas)
	visual(unit)

func changed_owner(unit: GameUnit, old_team: int) -> void:
	if Net.is_client: return
	if not Match.aspects_enabled and unit.aspect_info.is_empty(): return
	var ratio := unit.hp/maxf(1,unit.max_hp)
	for key in unit.get_meta("aspect_deltas",{}): unit.set(key,float(unit.get(key))-float(unit.get_meta("aspect_deltas")[key]))
	unit.hp = minf(unit.max_hp,unit.max_hp*ratio)
	unit.aspect_info.clear()
	unit.set_meta("aspect_deltas",{})
	unit.effects = unit.effects.filter(func(s):return not (str(s.get("id","")).begins_with("aspect_") and int(s.get("source_team",-1))==old_team))
	Runtime.tick(unit,0)
	created(unit)

func visual(unit: Node, side := -1) -> void:
	if not is_instance_valid(unit) or game.vfx==null: return
	if side<0: side = unit.team
	var art: Dictionary = selected(side).get("visual",{}).get("trigger",{})
	if art.is_empty() or game.vfx.temporary.size()>=24: return
	if not Net.is_server and (not unit.visible or unit.get_meta("army_offscreen",false)): return
	if game.round_rules.elapsed-float(unit.get_meta("aspect_fx_at",-10))<1.5: return
	unit.set_meta("aspect_fx_at",game.round_rules.elapsed)
	game.vfx.temporary_attach(unit,art,1.2)

static func stat(unit: Node, key: String) -> float:
	if not is_instance_valid(unit) or not unit is GameUnit: return 0.0
	return float(unit.aspect_info.get("mods",{}).get(key,0))

static func spell_cooldown(unit: Node, cfg: Dictionary) -> float:
	var factor := 1+stat(unit,"cd")
	if is_instance_valid(unit) and is_instance_valid(unit.gameplay) and unit.gameplay.aspects!=null:
		var rules: RefCounted = unit.gameplay.aspects
		if rules.has(unit.team,"pandaren_revelry") and str(cfg.get("kind",""))=="panda_brew": factor *= .8
		if rules.has(unit.team,"orc_support") and unit is GameUnit and unit.rawcode in line_units("h02B") and str(cfg.get("id",""))=="A02M": factor /= 1.35
		if unit is Building:
			if rules.has(unit.team,"undead_army") and str(cfg.get("kind",""))=="undead_raise_building": factor /= 1.4
			if rules.has(unit.team,"nature_forest") and preload("res://scripts/game/economy.gd").category(unit.gameplay,unit.rawcode) in ["special","lethal"]: factor /= 1.25
	return maxf(0,float(cfg.get("cd",0))*factor)

func after_damage(source: Node, target: Node, dealt: float, spell: bool, attack_type := "normal") -> void:
	if not Match.aspects_enabled: return
	if dealt<=0 or not target is GameUnit: return
	if fighter(target) and target.hp<target.max_hp*.3 and has(target.team,"north_rage") and not target.get_meta("aspect_rage_used",false):
		target.set_meta("aspect_rage_used",true)
		Runtime.effect(target,{"id":"aspect_north_rage","name":"Берсерк Севера","kind":"buff","duration":8,"haste":.5,"power":.25,"speed":.25,"undispellable":true},target.team)
		visual(target)
	if spell or not fighter(source) or source.team==target.team or target.invulnerable: return
	if has(source.team,"elven_blood") and attack_type=="magic" and target.mana>0:
		target.mana = maxf(0,target.mana-15)
		visual(target,source.team)
	if has(source.team,"undead_plague") and target.alive and target.spell_resist<1:
		Runtime.effect(target,{"id":"aspect_plague_"+str(source.team),"name":"Чума","kind":"debuff","duration":8,"dps":6,"damage_type":"magic","aspect_plague":true,"aspect_hops":0},source.team)
		visual(target,source.team)

func attack_released(source: GameUnit, target: Node, deferred := false) -> Dictionary:
	if not Match.aspects_enabled or not fighter(source): return {}
	source.set_meta("aspect_attacking_until",game.round_rules.elapsed+1)
	# A released melee hit may already have killed its target. It still counts
	# as an attack; the burst can hit the remaining nearby enemies.
	if not is_instance_valid(target) or not target is GameUnit or target.controlled or target.invulnerable or target.illusion or target.rawcode=="YTct" or target.get_meta("native_hidden",false) or target.team==source.team: return {}
	var heat := has(source.team,"mech_heat")
	var storm := has(source.team,"elemental_storm")
	if not heat and not storm: return {}
	var count := int(source.aspect_info.get("attacks",0))+1
	source.aspect_info["attacks"] = count
	if count%(5 if heat else 4)!=0: return {}
	var payload := {"side":source.team,"heat":heat,"source":weakref(source)}
	if not deferred: burst(payload,target.position,target)
	return payload

func burst(payload: Dictionary, point: Vector3, primary: Node = null) -> void:
	if payload.is_empty(): return
	var side := int(payload.side)
	var heat := bool(payload.heat)
	var source: Node = payload.source.get_ref()
	if is_instance_valid(source) and source.team!=side: source = null
	var candidates := nearby(point,(200.0 if heat else 300.0)/30,1-side)
	# The main victim is guaranteed its slot even above a crowd of flyers.
	if is_instance_valid(primary) and primary in candidates:
		candidates.erase(primary); candidates.push_front(primary)
	for victim in candidates.slice(0,6 if heat else 3):
		game.damage_unit(victim,80 if heat else 45,side,"magic",false,true,source,{"aspect_proc":true})
	if is_instance_valid(source): game.combat_text(source,"ПЕРЕГРЕВ" if heat else "РАЗРЯД",Color("ddbd7a"))
	if is_instance_valid(primary): visual(primary,side)

func after_cast(source: Node, cfg: Dictionary) -> void:
	if not source is GameUnit or not has(source.team,"orc_support") or not source.rawcode in line_units("h02B") or str(cfg.get("id",""))!="A02M": return
	var echo := cfg.duplicate(true)
	echo["mana"] = 0
	echo["passive_cast"] = true
	Runtime.cast(source,echo)

func configured(building: Building) -> void:
	if not Match.aspects_enabled: return
	if has(building.team,"human_defense") and building.rawcode=="h006":
		var ratio := building.hp/maxf(1,building.max_hp)
		building.max_hp += 200
		building.hp = building.max_hp*ratio
		building.armor += 2
		if game.vfx!=null and game.vfx.temporary.size()<24:
			game.vfx.burst_at(selected(building.team).visual.trigger,building.position,building.team,1.2)
	if has(building.team,"undead_army") and building.spells.any(func(s):return str(s.cfg.get("kind",""))=="undead_raise_building"): building.mana_regen *= 1.4
	if has(building.team,"nature_forest") and preload("res://scripts/game/economy.gd").category(game,building.rawcode) in ["special","lethal"]: building.mana_regen *= 1.25

func mark_up(raw: String, base: int, side: int) -> int:
	return ceili(base*1.1) if has(side,"nature_resilience") and not str(GameDB.unit(raw).get("utra","")).is_empty() else base

func incoming(target: Node, amount: float) -> float:
	if fighter(target) and has(target.team,"mech_armor"): return minf(amount,maxf(5,amount-12))
	return amount

func outgoing(source: Node, amount: float, critical: bool, spell: bool) -> float:
	if spell or not is_instance_valid(source) or not has(source.team,"elven_doom"): return amount
	if source is Building: amount *= .92
	return amount*(1.25 if critical else 1.0)

static func attack_flat(unit: GameUnit) -> float:
	return float(unit.aspect_info.get("stacks",0))*(2 if str(unit.aspect_info.get("id",""))=="chaos_seal" else 4)

func stack(unit: GameUnit, cap: int) -> void:
	unit.aspect_info["stacks"] = mini(cap,int(unit.aspect_info.get("stacks",0))+1)
	visual(unit)

func ordinary_death(unit: GameUnit) -> bool:
	return not unit.controlled and not unit.invulnerable and not unit.illusion and unit.rawcode!="YTct" and not unit.get_meta("native_hidden",false) and not unit.summoned_unit and not unit.get_meta("spell_summoned",false) and not unit.get_meta("no_bounty",false) and not unit.get_meta("aspect_spawn",false)

func died(victim: GameUnit, killer: Node) -> void:
	if not Match.aspects_enabled: return
	if ordinary_death(victim):
		var side := 1-victim.team
		var candidates := nearby(victim.position,600.0/30,side)
		if has(side,"chaos_seal") and not candidates.is_empty(): stack(candidates[0],10)
		if has(side,"orc_honor") and fighter(killer) and killer.team==side:
			Runtime.effect(killer,{"id":"aspect_orc_honor","name":"Кровь и честь","kind":"buff","duration":5,"haste":.5,"incoming":-.1},side)
			visual(killer)
		var heirs := nearby(victim.position,600.0/30,victim.team)
		if has(victim.team,"night_vengeance") and not heirs.is_empty(): stack(heirs[0],10)
		if has(victim.team,"elemental_resonance"):
			var healed := 0
			for ally in heirs:
				if ally.rawcode!=victim.rawcode or game.round_rules.elapsed-float(ally.get_meta("aspect_resonance_at",-10))<3: continue
				ally.heal(ally.max_hp*.1,true)
				ally.set_meta("aspect_resonance_at",game.round_rules.elapsed)
				visual(ally)
				healed += 1
				if healed>=4: break
		if has(side,"corrupted_parasite") and preload("res://scripts/game/economy.gd").slots(producer_for(victim.rawcode))==0 and not nearby(victim.position,500.0/30,side).is_empty():
			var active: int = game.units.filter(func(u):return fighter(u) and u.team==side and u.get_meta("aspect_thrall",false)).size()
			if active<6 and randf()<.2:
				var thrall := spawn(side,"n01I",victim.position,25)
				thrall.set_meta("aspect_thrall",true)
				simple_summon(thrall,180,12,1)
	for status in victim.effects:
		var plague := bool(status.get("aspect_plague",false))
		var poison := bool(status.get("aspect_poison",false))
		if not plague and not poison: continue
		if float(status.get("remaining",0))<=0 or int(status.get("aspect_hops",0))>=2: continue
		var side := int(status.get("source_team",-1))
		if side not in [0,1]: continue
		var transferred := 0
		for enemy in nearby(victim.position,(150.0 if plague else 200.0)/30,1-side):
			if enemy.spell_resist>=1 or Runtime.has_effect(enemy,str(status.id)): continue
			var next: Dictionary = status.duplicate(true)
			next["aspect_hops"] = int(status.get("aspect_hops",0))+1
			next.erase("tick")
			Runtime.effect(enemy,next,side,float(status.remaining))
			visual(enemy,side)
			transferred += 1
			if transferred>=(3 if plague else 1): break

static func producer_for(raw: String) -> String:
	if _legendary_producers.is_empty():
		for building in GameDB.units:
			if preload("res://scripts/game/economy.gd").slots(str(building))<=0: continue
			for trained in str(GameDB.unit(str(building)).get("utra","")).split(",",false): _legendary_producers[trained] = building
	return str(_legendary_producers.get(raw,""))

func frost_duration(side: int, duration: float, hero := false) -> float:
	return duration+minf(1.0 if hero else 2.0,duration*.35) if has(side,"north_winter") else duration

func status_applied(target: GameUnit, status: Dictionary, side: int) -> void:
	if not Match.aspects_enabled: return
	if side==target.team: return
	var id := str(status.get("id",""))
	var group := str(status.get("stack_group",""))
	var frost := id in ["A03Y","A045","A043","Bfrz","BNbf","NWY2","NWY3","NWY4"] or group.begins_with("frost") or group=="northern_ice_chains"
	if frost and not status.get("aspect_frost",false):
		status["remaining"] = frost_duration(side,float(status.remaining),target.armor_type=="hero" or target.resistant_skin)
		status["lifetime"] = status.remaining
		status["aspect_frost"] = true
	if has(side,"nerub_toxin") and float(status.get("dps",0))>0 and float(status.get("slow",0))>0 and not status.get("aspect_poison",false):
		status["dps"] = float(status.dps)*1.25
		status["aspect_poison"] = true
		status["aspect_hops"] = 0

func state() -> Dictionary:
	return {"enabled":Match.aspects_enabled,"choices":Match.aspect_choices.duplicate(),"remaining":remaining}

func receive(value: Dictionary, slot: int) -> void:
	var choices: Variant = value.get("choices",[-1,-1])
	if not choices is Array or choices.size()!=2 or not choices.all(func(v):return valid_index(v,true)): return
	Match.aspects_enabled = bool(value.get("enabled",false))
	Match.aspect_choices = [int(choices[slot]),int(choices[1-slot])]
	remaining = clampf(float(value.get("remaining",0)),0,PICK_SECONDS)
	if Match.aspects_enabled and not waiting(): MenuMusic.stop_music()
