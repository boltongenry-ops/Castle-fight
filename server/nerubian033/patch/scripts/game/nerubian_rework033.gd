extends RefCounted

static func venom(item: Dictionary) -> bool:
	return str(item.get("stack_group",""))=="nerub_venom" and str(item.get("kind",""))=="poison"

static func layers(unit: Node, live := true) -> Array:
	var list: Array = unit.effects.filter(func(e):return venom(e) and (not live or float(e.remaining)>0))
	list.sort_custom(func(a,b):
		if float(a.dps)!=float(b.dps): return float(a.dps)>float(b.dps)
		return int(a.get("nerub_source",0))<int(b.get("nerub_source",0)))
	return list

static func apply_venom(unit: Node, item: Dictionary) -> bool:
	if not venom(item): return false
	if unit.spell_resist>=1: return true
	var list := layers(unit)
	var previous: Dictionary = {}
	for old in list:
		if int(old.source_team)==int(item.source_team) and int(old.get("nerub_source",0))==int(item.get("nerub_source",0)):
			previous = old
			break
	if not previous.is_empty() and float(previous.dps)>float(item.dps): return true
	if previous.is_empty() and list.size()>=2:
		if float(list.back().dps)>=float(item.dps): return true
		unit.effects.erase(list.back())
	if not previous.is_empty():
		item["tick"] = float(previous.get("tick",0))
		unit.effects.erase(previous)
	unit.effects.append(item)
	return true

static func periodic(unit: Node, item: Dictionary) -> float:
	if not venom(item): return float(item.get("dps",0))
	var rank := layers(unit,false).find(item)
	return float(item.dps)*(1.0 if rank==0 else .5 if rank==1 else 0.0)

static func stats(unit: Node, item: Dictionary, stat: String) -> float:
	var value := float(item.get(stat,0))
	if venom(item) and stat in ["armor","slow"]:
		var list := layers(unit)
		if list.is_empty() or list[0]!=item: return 0
		value = 0
		for layer in list:
			value = minf(value,float(layer.get(stat,0))) if stat=="armor" else maxf(value,float(layer.get(stat,0)))
	return value

static func armor_overlap(unit: Node) -> float:
	var total := 0.0
	var strongest := 0.0
	for item in unit.effects:
		if float(item.remaining)<=0 or not (venom(item) or str(item.id)=="AUAc"): continue
		var armor := stats(unit,item,"armor")
		if armor<0:
			total += armor
			strongest = minf(strongest,armor)
	return strongest-total

static func hunt(source: Node, target: Node) -> float:
	if not is_instance_valid(source) or not source is GameUnit or not source.alive or source.illusion or not str(source.rawcode).begins_with("nN0") or not target is GameUnit or source.team==target.team: return 1
	var bonus := 0.0
	for item in target.effects:
		if str(item.id)=="ANMK" and int(item.source_team)==source.team and float(item.remaining)>0: bonus = maxf(bonus,float(item.hunt_bonus))
	return 1+bonus/100*(.25 if source.rawcode=="nN04" else 1.0)

static func spider(unit: Node) -> void:
	if unit.rawcode!="nN04": return
	unit.set_meta("no_bounty",true)
	unit.set_meta("no_corpse",true)
	if unit.gameplay.aspects.has(unit.team,"nerub_queen"):
		unit.max_hp *= 1.2
		unit.hp = unit.max_hp
		unit.summoned_life *= 1.25

static func hunt_target(source: Node, current: Node) -> Node:
	if not str(source.rawcode).begins_with("nN0") or source.illusion or not source.abilities.any(func(id):return str(source.gameplay.ability_db.get(id,{}).get("kind",""))=="nerub_ambush"): return current
	var distance := 6.0
	if is_instance_valid(current): distance = minf(distance,Vector2(current.position.x-source.position.x,current.position.z-source.position.z).length()+1)
	var result: Node = current
	for target in source.gameplay.unit_index.query(source.position,distance):
		if not target.alive or target.team==source.team or source.weapon_for(target).is_empty(): continue
		var gap := Vector2(target.position.x-source.position.x,target.position.z-source.position.z).length()
		if gap>distance or not target.effects.any(func(e):return str(e.id)=="ANMK" and int(e.source_team)==source.team and float(e.remaining)>0): continue
		result = target;distance = gap
	return result

static func plague_cast(game: Node, duration: float) -> int:
	var id := int(game.get_meta("nerub_cast_serial",0))+1
	game.set_meta("nerub_cast_serial",id)
	var budgets: Dictionary = game.get_meta("nerub_cast_budgets",{})
	for key in budgets.keys():
		if float(budgets[key].until)<game.round_rules.elapsed: budgets.erase(key)
	budgets[id] = {"used":0,"until":game.round_rules.elapsed+duration+1}
	game.set_meta("nerub_cast_budgets",budgets)
	return id

static func plague_death(victim: GameUnit) -> void:
	if victim.illusion or victim.summoned_unit: return
	var game: Node = victim.gameplay
	for item in victim.effects:
		if str(item.id)!="AUPs" or (float(item.remaining)<=0 and victim.get_meta("nerub_final_tick",{})!=item): continue
		var side := int(item.source_team)
		if side not in [0,1] or side==victim.team: continue
		var budgets: Dictionary = game.get_meta("nerub_cast_budgets",{})
		var id := int(item.get("nerub_plague_cast",0))
		if budgets.has(id):
			if int(budgets[id].used)>=3: continue
			budgets[id].used += 1
		var list: Array = game.units.filter(func(u):return is_instance_valid(u) and u.alive and not u.illusion and u.team==side and str(u.rawcode).begins_with("nN0") and u.hp<u.max_hp and Vector2(u.position.x-victim.position.x,u.position.z-victim.position.z).length()<=350.0/30)
		list.sort_custom(func(a,b):
			if (a.rawcode=="nN04")!=(b.rawcode=="nN04"): return a.rawcode!="nN04"
			return a.hp/a.max_hp<b.hp/b.max_hp)
		for ally in list.slice(0,2): ally.heal(float(item.get("death_heal",60)))
		var mechanical := str(game.unit_traits.get(victim.rawcode,{}).get("unit_types","")).to_lower().contains("mechanical")
		if not mechanical and load("res://scripts/game/nerubian_spells.gd").brood_count(game,side)<20:
			load("res://scripts/game/advanced_spells.gd").spawn(game,side,"nN04",victim.position,float(item.get("death_spawn_life",12)))
