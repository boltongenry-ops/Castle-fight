extends Node
class_name Gameplay

## Vertical slice with a real bot opponent: both sides have a race, economy,
## build plots and producers that spawn units toward the enemy keep.

const BuildGridScript = preload("res://scripts/game/build_grid.gd")
const GameUnitScript = preload("res://scripts/game/game_unit.gd")
const ProjectileScript = preload("res://scripts/game/projectile.gd")
const BuildingScript = preload("res://scripts/game/building.gd")

const WC3_TO_M := 1.0 / 30.0
const HUT_COST := 100
const CASTLE_HP := 5000.0
const INCOME_PER_SEC := 8.0
const INCOME_INTERVAL := 10.0
const BASE_INCOME := 5.0
const Economy = preload("res://scripts/game/economy.gd")
const BOT_BUILD_INTERVAL := 12.0
const BOT_HUT_COST := 120
const BUILDING_HEIGHT := 5.0
const HOTKEY_ORDER := {"Q": 0, "W": 1, "E": 2, "R": 3, "A": 4, "S": 5,
	"D": 6, "F": 7, "Z": 8, "X": 9, "C": 10, "V": 11, "T": 12, "G": 13}
const ATTACK_TABLE := {
	"normal": {"light": 1.0, "medium": 1.0, "heavy": 1.0, "fortified": 1.0, "normal": 1.0, "hero": 1.0, "unarmored": 1.0},
	"pierce": {"light": 2.0, "medium": 1.0, "heavy": 1.0, "fortified": 0.35, "normal": 1.0, "hero": 0.5, "unarmored": 1.0},
	"siege": {"light": 1.0, "medium": 0.5, "heavy": 1.0, "fortified": 1.5, "normal": 1.0, "hero": 0.5, "unarmored": 1.0},
	"magic": {"light": 1.25, "medium": 0.75, "heavy": 2.0, "fortified": 0.35, "normal": 1.0, "hero": 0.5, "unarmored": 1.0},
	"chaos": {"light": 1.0, "medium": 1.0, "heavy": 1.0, "fortified": 1.0, "normal": 1.0, "hero": 1.0, "unarmored": 1.0},
	"hero": {"light": 1.0, "medium": 1.0, "heavy": 1.0, "fortified": 1.0, "normal": 1.0, "hero": 1.0, "unarmored": 1.0},
}
const ABILITIES := {
	"A03K": {"kind": "heal", "power": 150.0, "cd": 7.0, "range": 12.0},
	"A03M": {"kind": "buff", "power": 0.25, "cd": 7.0, "duration": 10.0},
	"A02U": {"kind": "slow", "power": 0.5, "cd": 10.0, "duration": 3.0, "range": 12.0},
	"A038": {"kind": "slow", "power": 0.3, "cd": 8.0, "duration": 3.0, "range": 10.0},
}

var fog: Node
var vfx: Node
var terrain: Node = null
var grid: Node = null
var army_visuals: Node
var unit_index := preload("res://scripts/game/unit_index.gd").new()
var metrics: RefCounted
var build_preview: Node3D
var gold: int = Settings.START_GOLD:
	set(value):
		if metrics!=null and not Net.is_client and not Chronicle.playing and value<gold: metrics.spend(0,gold-value)
		gold = value
var built := 0
var units: Array = []
var huts: Array = []
var player_builder: Node = null
var castles := {
	0: {"hp": CASTLE_HP, "x": -5120.0},
	1: {"hp": CASTLE_HP, "x": 5120.0},
}
var game_over := false
var winner := -1
var kills := {0: 0, 1: 0}
var aura_timer := 1.0
var corpses: Array = []
var fog_timer := 0.0
const FOG_RADIUS := 18.0

# --- player race ---
var race: Dictionary = {}
var race_name := "Human Alliance"
var building_name := "Barracks"
var building_rawcode := "h000"
var building_model := ""
var unit_kind := "footman"
var unit_rawcode := "hfoo"
var builder_model := ""
var unit_model := ""
var race_building_model := ""
var missile_particles: Dictionary = {}
var weapon_visuals: Dictionary = {}
var race_unit_model := ""
var unit_traits: Dictionary = {}
var ability_db: Dictionary = {}
var selected_unit: Node = null
var combat_sounds: Dictionary = {}
var unit_ready_audio: Node
var combat_profiles: Dictionary = {}
var reserved_gold := 0
var tower_traits: Dictionary = {}
var unit_targets: Dictionary = {}
var upgrade_tiers: Dictionary = {}
var line_buildings: Array = []
var tower_buildings: Array = []
var selected_tower := -1
var selected_line := 0
var build_mode := false

# --- bot ---
var bot_race: Dictionary = {}
var bot_race_name := "Orcish Horde"
var bot_building_name := ""
var bot_building_model := ""
var bot_unit_model := ""
var bot_unit_rawcode := "hfoo"
var bot_building_raw := ""
var bot_gold: int = Settings.START_GOLD:
	set(value):
		if metrics!=null and not Net.is_client and not Chronicle.playing and value<bot_gold: metrics.spend(1,bot_gold-value)
		bot_gold = value
var bot_build_cd := BOT_BUILD_INTERVAL
var bot_built := 0
var bot_line_buildings: Array = []
var bot_race_building_model := ""
var bot_race_unit_model := ""
var wood: int = Settings.START_WOOD
var bot_wood: int = Settings.START_WOOD
var bot_reserved_gold := 0
var bot_reserved_wood := 0
var bot_reserved_slots := 0
var bot_legendary_limit := 1
var bot_inventory: Node
var network_builders: Array = []
var reserved_wood := 0
var reserved_slots := 0
var round_rules: Node
var aspects: RefCounted
var nature: Node
var human: Node
var elven: Node
var night: Node
var northern: Node
var corrupted: Node
var elemental: Node
var mech: Node
var pandaren: Node
var undead: Node
var _detectors: Array = []
var _detector_frame := -1
var _detector_count := -1
var income_fraction := {0: 0.0, 1: 0.0}
var income_bonus := 0.0
var income_base := {0: 0.0, 1: 0.0}
var _income_timer := 0.0
var damage_bonus := 0.0
var speed_bonus := 0.0
var castle_armor := 0.0
var damage_boost := 0.0
var damage_boost_timer := 0.0
var bot_damage_boost := 0.0
var bot_damage_boost_timer := 0.0
var shop_items: Array = []
var item_catalog: Dictionary = {}
var damage_table: Dictionary = {}
var display_names: Dictionary = {}
var inventory: Node
var legendary_limit := 1
var tech_level := 0

var buildings: Node3D
var units_root: Node3D
var projectiles_root: Node3D
var _stone_mat: StandardMaterial3D
var _income_accum := 0.0


func setup(world_terrain: Node, show_grid: bool) -> void:
	Match.ensure_started()
	aspects = preload("res://scripts/game/aspects.gd").new()
	aspects.setup(self)
	gold = Settings.start_gold
	bot_gold = Settings.start_gold
	wood = Settings.START_WOOD
	bot_wood = Settings.START_WOOD
	metrics = preload("res://scripts/game/battle_stats.gd").new()
	damage_table = JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/damage_table.json"))
	display_names = JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/display_names.json"))
	combat_sounds = JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/combat_sounds.json"))
	unit_ready_audio = preload("res://scripts/game/unit_ready_audio.gd").new()
	unit_ready_audio.name = "UnitReadyAudio"
	add_child(unit_ready_audio)
	combat_profiles = JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/combat_profiles.json"))
	combat_profiles.merge(JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/combat_extra_profiles.json")),true)
	var particle_path := "res://assets/data/missile_particles.json"
	if FileAccess.file_exists(particle_path):
		var particle_doc = JSON.parse_string(FileAccess.get_file_as_string(particle_path))
		if particle_doc is Dictionary:
			missile_particles = particle_doc
	weapon_visuals = JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/weapon_visuals.json"))
	terrain = world_terrain
	units_root = Node3D.new()
	units_root.name = "Units"
	add_child(units_root)
	projectiles_root = Node3D.new()
	projectiles_root.name = "Projectiles"
	add_child(projectiles_root)
	buildings = Node3D.new()
	buildings.name = "Buildings"
	add_child(buildings)
	grid = BuildGridScript.new()
	unit_index.setup(self)
	army_visuals = preload("res://scripts/game/army_optimization.gd").new()
	add_child(army_visuals)
	army_visuals.setup(self)
	var automatic := preload("res://scripts/game/auto_performance.gd").new()
	add_child(automatic)
	automatic.setup(self)
	grid.name = "BuildGrid"
	add_child(grid)
	grid.build(terrain)
	grid.show_overlay(show_grid)
	grid.show_recommendations()
	_stone_mat = StandardMaterial3D.new()
	_stone_mat.albedo_color = Color(0.45, 0.42, 0.36)
	_stone_mat.roughness = 1.0

	var traits_raw := FileAccess.get_file_as_string("res://assets/data/unit_traits.json")
	if not traits_raw.is_empty():
		var parsed = JSON.parse_string(traits_raw)
		if parsed is Dictionary:
			unit_traits = parsed
	item_catalog = JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/item_catalog.json"))
	var items_raw := FileAccess.get_file_as_string("res://assets/data/shop_items.json")
	if not items_raw.is_empty():
		var items_parsed = JSON.parse_string(items_raw)
		if items_parsed is Array and not (items_parsed as Array).is_empty():
			shop_items = items_parsed
	var abilities_raw := FileAccess.get_file_as_string("res://assets/data/abilities.json")
	if not abilities_raw.is_empty():
		var ab_parsed = JSON.parse_string(abilities_raw)
		if ab_parsed is Dictionary:
			ability_db = ab_parsed
	var tw_raw := FileAccess.get_file_as_string("res://assets/data/towers_traits.json")
	if not tw_raw.is_empty():
		var tw_parsed = JSON.parse_string(tw_raw)
		if tw_parsed is Dictionary:
			tower_traits = tw_parsed
	var ut_raw := FileAccess.get_file_as_string("res://assets/data/unit_targets.json")
	if not ut_raw.is_empty():
		var ut_parsed = JSON.parse_string(ut_raw)
		if ut_parsed is Dictionary:
			unit_targets = ut_parsed
	var utier_raw := FileAccess.get_file_as_string("res://assets/data/upgrade_tiers.json")
	if not utier_raw.is_empty():
		var utier_parsed = JSON.parse_string(utier_raw)
		if utier_parsed is Dictionary:
			upgrade_tiers = utier_parsed

	_select_race()
	_select_bot_race()
	if not Net.active() and not Chronicle.playing and Match.activity!="tutorial": Progress.begin_solo()
	var castle_stats: Dictionary = unit_traits.get("hcas",{})
	for team in [0,1]:
		castles[team].merge({"hp":float(castle_stats.get("hp",CASTLE_HP)),"max_hp":float(castle_stats.get("hp",CASTLE_HP)),"armor":float(castle_stats.get("armor",5)),"armor_type":str(castle_stats.get("armor_type","fortified")),"regen":float(castle_stats.get("hp_regen",.5)),"collision":float(castle_stats.get("collision",172.0/30))},true)
		sync_castle_hp(team)
	spawn_unit(0, "peasant", -4544.0, 448.0, -5120.0, true, builder_model, str(race.get("builder",{}).get("rawcode","hpea")))
	player_builder = units.back() if not units.is_empty() else null
	if player_builder != null:
		player_builder.controlled = true
		if not Net.active(): preload("res://scripts/world/builder_cosmetic.gd").apply(player_builder,str(Progress.account.get("equipped",{}).get("aura","")))
	inventory = preload("res://scripts/game/inventory.gd").new()
	add_child(inventory)
	inventory.setup(self)
	round_rules = preload("res://scripts/game/round_rules.gd").new()
	round_rules.game = self
	add_child(round_rules)
	vfx = preload("res://scripts/game/effect_runtime.gd").new()
	add_child(vfx)
	vfx.setup(self)
	nature = preload("res://scripts/game/nature_rules.gd").new()
	add_child(nature)
	nature.setup(self)
	human = preload("res://scripts/game/human_rules.gd").new()
	human.game = self
	add_child(human)
	elven = preload("res://scripts/game/elven_rules.gd").new()
	elven.game = self
	add_child(elven)
	night = preload("res://scripts/game/night_rules.gd").new()
	night.game = self
	add_child(night)
	northern = preload("res://scripts/game/northern_rules.gd").new()
	northern.game = self
	add_child(northern)
	corrupted = preload("res://scripts/game/corrupted_rules.gd").new()
	corrupted.game = self
	add_child(corrupted)
	elemental = preload("res://scripts/game/elemental_rules.gd").new()
	elemental.game = self
	add_child(elemental)
	mech = preload("res://scripts/game/mech_rules.gd").new()
	mech.game = self
	add_child(mech)
	pandaren = preload("res://scripts/game/pandaren_rules.gd").new()
	pandaren.game = self
	add_child(pandaren)
	undead = preload("res://scripts/game/undead_rules.gd").new()
	undead.game = self
	add_child(undead)
	fog = preload("res://scripts/world/fog_of_war.gd").new()
	add_child(fog)
	fog.setup(self,get_viewport().get_camera_3d())
	spawn_unit(1, "peasant", 4544.0, 384.0, 5120.0, true, "res://assets/models_races/%s.glb" % _race_key(bot_race_name), str(bot_race.get("builder",{}).get("rawcode","hpea")))
	network_builders = [player_builder,units.back()]
	bot_inventory = preload("res://scripts/game/inventory.gd").new()
	add_child(bot_inventory)
	bot_inventory.setup(self,1)
	if Net.active(): Net.attach_game(self)

	print("[Gameplay] ready: player=%s (%s), bot=%s (%s), gold=%d, abilities=%d" % [
		race_name, building_name, bot_race_name, bot_building_name, gold, ability_db.size()
	])
	if "--dmg-test" in OS.get_cmdline_user_args():
		print("[Gameplay] pierce vs fortified=%.2f, magic vs heavy=%.2f, normal vs medium=%.2f" % [
			attack_multiplier("pierce", "fortified"),
			attack_multiplier("magic", "heavy"),
			attack_multiplier("normal", "medium")])
	if "--gallery" in OS.get_cmdline_user_args():
		spawn_gallery()
	if "--chat=" in " ".join(OS.get_cmdline_user_args()):
		for arg in OS.get_cmdline_user_args():
			if arg.begins_with("--chat="):
				print("[Chat] ", chat_command(arg.substr(7)))
	if "--tower-build" in OS.get_cmdline_user_args():
		select_tower(0)
		var tcell = grid.cell_at_wc3(-5056.0, 1184.0, 0)
		if tcell != null and player_builder != null and not tower_buildings.is_empty():
			player_builder.issue_build_entry(tcell, tower_buildings[0])
	if "--builder-queue" in OS.get_cmdline_user_args():
		select_line(0)
		var c1 = grid.cell_at_wc3(-5056.0, 928.0, 0)
		var c2 = grid.cell_at_wc3(-5056.0, 1184.0, 0)
		if c1 != null and player_builder != null:
			player_builder.issue_build(c1, 0)
		if c2 != null and player_builder != null:
			player_builder.issue_build(c2, 0)
	if "--builder-build" in OS.get_cmdline_user_args():
		select_line(0)
		var bcell = grid.cell_at_wc3(-5056.0, 928.0, 0)
		if bcell != null and player_builder != null:
			player_builder.issue_build(bcell, 0)
	if "--rich" in OS.get_cmdline_user_args():
		gold = 1000
	if "--build" in OS.get_cmdline_user_args():
		for _i in 3:
			_build_hut()
	if "--line=" in " ".join(OS.get_cmdline_user_args()):
		for arg in OS.get_cmdline_user_args():
			if arg.begins_with("--line="):
				selected_line = int(arg.substr(7)) - 1
		for _i in 3:
			_build_hut()
	if "--bot-build" in OS.get_cmdline_user_args():
		for _i in 2:
			_build_bot_hut()
	if "--upgrade-hut" in OS.get_cmdline_user_args():
		var target_hut = null
		for h in huts:
			if int(h.get("team", -1)) == 0 and str(h.get("line_raw", "")) == "h003":
				target_hut = h
				break
		if target_hut == null:
			for h in huts:
				if int(h.get("team", -1)) == 0:
					target_hut = h
					break
		if target_hut != null:
			upgrade_hut(target_hut)
			upgrade_hut(target_hut)
	if "--kill-hut" in OS.get_cmdline_user_args():
		for h in huts:
			if int(h.get("team", -1)) == 1:
				damage_unit(h.get("node"), 900.0, 0)
				break
	if "--upgrade" in OS.get_cmdline_user_args():
		upgrade_line(selected_line)
	if "--gameover" in OS.get_cmdline_user_args():
		game_over = true
		winner = 0
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--buy="):
			buy_item(int(arg.substr(6)))
	if "--siege" in OS.get_cmdline_user_args():
		for _i in 3:
			spawn_unit(0, "footman", 4600.0, 64.0, 5120.0, false, unit_model, unit_rawcode)


func _race_key(name: String) -> String:
	return name.to_lower().replace("'", "").replace(" ", "_").replace("-", "_")


func _find_race(name: String, side := 0) -> Dictionary:
	var r: Dictionary = Match.race_for(name,side,not Net.is_client)
	if not r.is_empty():
		return r
	for candidate in GameDB.races:
		if str(candidate.get("name", "")).to_lower().begins_with(name.to_lower()):
			return Match.race_for(str(candidate.name),side,not Net.is_client)
	return {}


func _choose_line(race_data: Dictionary) -> Dictionary:
	var chosen: Dictionary = {}
	for b in race_data.get("buildings", []):
		if not str(b.get("role", "")).contains("line"):
			continue
		for t in b.get("trains", []):
			if str(t.get("rawcode", "")) == "hfoo":
				chosen = b
				break
		if not chosen.is_empty():
			break
	if chosen.is_empty():
		for b in race_data.get("buildings", []):
			if str(b.get("role", "")).contains("line") and (b.get("trains", []) as Array).size() > 0:
				chosen = b
				break
	return chosen


func _collect_lines(race_data: Dictionary) -> Array:
	var out: Array = []
	for b in race_data.get("buildings", []):
		if not str(b.get("role", "")).contains("line"):
			continue
		var trains_list: Array = b.get("trains", [])
		if trains_list.is_empty():
			continue
		var wf: Dictionary = (GameDB.units.get(str(b.get("rawcode", "")), {}) as Dictionary)
		var bcost := 100
		if wf.has("ugol"):
			bcost = maxi(50, int(wf.get("ugol", 100)))
		out.append({
			"rawcode": str(b.get("rawcode", "")),
			"name": str(b.get("name", "")),
			"unit_rawcode": str(trains_list[0].get("rawcode", "hfoo")),
			"unit_name": str(trains_list[0].get("name", "")),
			"hotkey": str(b.get("hotkey", "")),
			"upgrades_to": b.get("upgrades_to", []),
			"cost": bcost,
			"building_model": "res://assets/models_lines/%s_b.glb" % str(b.get("rawcode", "")),
			"unit_model": "res://assets/models_lines/%s_u.glb" % str(trains_list[0].get("rawcode", "")),
		})
	out.sort_custom(func(a, b):
		return HOTKEY_ORDER.get(str(a.get("hotkey", "")).to_upper(), 99) < HOTKEY_ORDER.get(str(b.get("hotkey", "")).to_upper(), 99)
	)
	return out


func _select_race() -> void:
	race_name = Match.player_race
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--race="):
			race_name = arg.substr(7)
	race = _find_race(race_name)
	if race.is_empty():
		race_name = "Human Alliance"
		race = GameDB.race(race_name)
	if race.is_empty():
		return
	race_name = str(race.get("name", race_name))
	var key := _race_key(race_name)
	builder_model = "res://assets/models_races/%s.glb" % key
	if not ResourceLoader.exists(builder_model):
		builder_model = ""
	building_model = "res://assets/models_races_content/%s_building.glb" % key
	if not ResourceLoader.exists(building_model):
		building_model = ""
	unit_model = "res://assets/models_races_content/%s_unit.glb" % key
	if not ResourceLoader.exists(unit_model):
		unit_model = ""
	race_building_model = building_model
	race_unit_model = unit_model
	var chosen := _choose_line(race)
	if not chosen.is_empty():
		building_name = str(chosen.get("name", "Barracks"))
		building_rawcode = str(chosen.get("rawcode", "h000"))
		unit_rawcode = str((chosen.get("trains", []) as Array)[0].get("rawcode", "hfoo"))
		unit_kind = "footman"
	line_buildings = _collect_lines(race)
	tower_buildings = _collect_towers(race)
	selected_line = 0


func _select_bot_race() -> void:
	bot_race_name = Match.enemy_race
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--bot-race="):
			bot_race_name = arg.substr(11)
	bot_race = _find_race(bot_race_name,1)
	if bot_race.is_empty():
		bot_race_name = "Orcish Horde"
		bot_race = GameDB.race(bot_race_name)
	if bot_race.is_empty():
		return
	bot_race_name = str(bot_race.get("name", bot_race_name))
	var key := _race_key(bot_race_name)
	bot_building_model = "res://assets/models_races_content/%s_building.glb" % key
	if not ResourceLoader.exists(bot_building_model):
		bot_building_model = ""
	bot_unit_model = "res://assets/models_races_content/%s_unit.glb" % key
	if not ResourceLoader.exists(bot_unit_model):
		bot_unit_model = ""
	var chosen := _choose_line(bot_race)
	if not chosen.is_empty():
		bot_building_name = str(chosen.get("name", "Barracks"))
		bot_unit_rawcode = str((chosen.get("trains", []) as Array)[0].get("rawcode", "hfoo"))
	bot_race_building_model = bot_building_model
	bot_race_unit_model = bot_unit_model
	bot_line_buildings = _collect_lines(bot_race)


func _spawn_interval(rawcode: String) -> float:
	# Used by audit/legacy callers; production itself always uses paid hut.spent.
	var production: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/production.json"))
	var intervals: Array = []
	for row in production.values():
		if str(row["unit"]) == rawcode: intervals.append(float(row["seconds"]))
	return float(intervals.min()) if not intervals.is_empty() else 20.0

func _hut_interval(hut: Dictionary) -> float:
	return Economy.spawn_interval(float(hut.get("spent", hut.get("cost", 100))))

func _income_amount(team: int) -> float:
	var amount := BASE_INCOME
	for hut in huts:
		var node = hut.get("node")
		if int(hut["team"]) == team and is_instance_valid(node) and node.alive:
			amount += float(hut.get("income", 0))
	return amount

func income_per_sec(team: int) -> float:
	return _income_amount(team) / INCOME_INTERVAL


func _update_fog(delta: float) -> void:
	if fog != null: fog.tick(delta)


func _apply_auras(delta: float) -> void:
	aura_timer -= delta
	if aura_timer > 0.0:
		return
	aura_timer = 0.5
	preload("res://scripts/game/combat_rules.gd").update_auras(self, 0.5)


func _process(delta: float) -> void:
	if game_over:
		return
	var selecting: bool = aspects.waiting()
	aspects.tick(delta)
	if selecting: return
	if inventory != null: inventory.tick(delta)
	if bot_inventory!=null: bot_inventory.tick(delta)
	for team in [0,1]:
		if float(castles[team]["hp"])<castle_max_hp(team):
			castles[team]["hp"] = minf(castle_max_hp(team),float(castles[team]["hp"])+float(castles[team].get("regen",0))*delta)
			sync_castle_hp(team)
	_income_timer += delta
	while _income_timer >= INCOME_INTERVAL:
		_income_timer -= INCOME_INTERVAL
		for team in [0, 1]:
			income_fraction[team] = float(income_fraction[team]) + _income_amount(team)
			var payment := int(income_fraction[team])
			income_fraction[team] = float(income_fraction[team])-payment
			if team == 0: gold += payment
			else: bot_gold += payment
	round_rules.tick(delta)
	nature.tick(delta)
	human.tick(delta)
	elven.tick(delta)
	night.tick(delta)
	northern.tick(delta)
	corrupted.tick(delta)
	elemental.tick(delta)
	mech.tick(delta)
	undead.tick(delta)
	pandaren.tick(delta)
	for c in corpses.duplicate():
		c["life"] = float(c.get("life", 0.0)) - delta
		if float(c["life"])<=0: consume_corpse(c)
	_update_fog(delta)
	_apply_auras(delta)

	for hut in huts.duplicate():
		if not huts.has(hut): continue
		if hut.has("nuke_cd"): hut["nuke_cd"] = maxf(0,float(hut["nuke_cd"])-delta)
		if bool(hut.get("paused", false)):
			continue
		var caster = hut.get("node")
		if is_instance_valid(caster):
			if caster.rawcode != str(hut.get("line_raw","")): caster.configure_spells(self,str(hut.get("line_raw","")))
			caster.tick_spells(delta)
			if caster.disabled_for > 0:
				hut.erase("pending_shot")
				continue
		if bool(hut.get("tower", false)):
			_tick_tower(hut,delta)
			continue
		hut["timer"] -= delta
		var interval := float(hut.get("interval", _hut_interval(hut)))
		hut["progress"] = clampf(1.0 - float(hut["timer"]) / maxf(interval, 0.1), 0.0, 1.0)
		if hut["timer"] <= 0.0:
			hut["timer"] += interval
			hut["progress"] = 0.0
			var goal: float = 5120.0 if hut["team"] == 0 else -5120.0
			spawn_unit(hut["team"], hut.get("unit_kind", "footman"), float(hut["x"]), float(hut["y"]), goal,
				false, hut.get("unit_model", ""), hut.get("rawcode", "hfoo"),true,caster)

	bot_build_cd -= delta
	if bot_build_cd <= 0.0:
		bot_build_cd = BOT_BUILD_INTERVAL
		if bot_gold >= BOT_HUT_COST:
			_build_bot_hut()


func _unhandled_input(event: InputEvent) -> void:
	if aspects!=null and aspects.waiting(): return
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_B:
			select_line(selected_line)
		elif event.keycode >= KEY_1 and event.keycode <= KEY_6:
			select_line(event.keycode - KEY_1)


func spawn_unit(team: int, kind: String, x: float, y: float, goal: float,
		stationary := false, model_path := "", rawcode := "", production := true, producer: Node = null) -> void:
	var unit := GameUnitScript.new()
	unit.name = "%s_%d_%d" % [kind, team, units.size()]
	add_child(unit)
	var traits: Dictionary = (unit_traits.get(rawcode, {}) as Dictionary).duplicate()
	if not traits.is_empty():
		traits["rawcode"] = rawcode
		traits["name"] = str((unit_traits.get(rawcode, {}) as Dictionary).get("name", kind))
		traits["snd_attack"] = weapon_sound(rawcode, kind, traits)
		traits["snd_death"] = ""
		# Attack-target profile from WC3 (targs1): air-only units like the gyrocopter.
		var ut: Dictionary = unit_targets.get(rawcode, {})
		if not ut.is_empty():
			traits["can_air"] = bool(ut.get("can_air", true))
			traits["can_ground"] = bool(ut.get("can_ground", true))
			traits["can_structure"] = bool(ut.get("can_structure", true))
	unit.setup(team, kind, x, y, goal, self, terrain, stationary, model_path, traits)
	unit.rawcode = rawcode
	# Original ld: A09H duplicates the trained unit, independent of locale.
	var ab: Array = traits.get("abilities", [])
	var pair_unit := "A09H" in ab
	if rawcode in ["n00U", "n01N", "n01W"]:
		pair_unit = true
	if pair_unit and production:
		var lane := maxf(1.1,float(traits.get("collision",.5))*1.1)
		unit.formation_group = unit.get_instance_id()
		unit.formation_lane = -lane
		unit.position.z -= lane
		var twin := GameUnitScript.new()
		twin.name = "%s_twin_%d" % [kind, units.size()]
		add_child(twin)
		twin.setup(team, kind, x, y+lane*30, goal, self, terrain, stationary, model_path, traits)
		twin.rawcode = rawcode
		twin.formation_group = unit.formation_group
		twin.formation_lane = lane
		units.append(twin)
		if mech!=null: mech.created(twin,production,producer)
		aspects.created(twin)
	units.append(unit)
	unit_index.invalidate()
	_detector_frame = -1
	if production and rawcode=="n00L": preload("res://scripts/game/unit_birth.gd").blood_fiend(unit)
	if production and "A0FO" in ab:
		# ld adds these companions once; jd does not recursively duplicate them.
		for companion in ["n02K","n02L"]:
			spawn_unit(team,name_for(companion),x,y,goal,stationary,"res://assets/models_lines/%s_u.glb" % companion,companion,false)
	if production and human!=null: human.trained(unit)
	if mech!=null: mech.created(unit,production,producer)
	if production and elemental!=null: elemental.trained(unit)
	aspects.created(unit)
	if production and not stationary and is_instance_valid(producer) and is_instance_valid(unit_ready_audio):
		unit_ready_audio.produced(team,rawcode,round_rules.elapsed)
	print("[Gameplay] spawn %s team %d at (%.0f, %.0f) flying=%s air=%s" % [
		kind, team, x, y, str(traits.get("flying", false)), str(traits.get("can_air", true))
	])


func missile_for(unit_raw: String, unit_name: String, attack_type := "normal", slot := 1) -> String:
	var native: Dictionary = weapon_visuals.get(unit_raw+"_"+str(slot),{})
	if not native.is_empty() and vfx.models.has(str(native["key"])): return "native:"+str(native["key"])
	var own := "res://assets/models_missiles/%s.glb" % unit_raw
	if ResourceLoader.exists(own):
		return own
	if missile_particles.has(unit_raw):
		return "particles:" + unit_raw
	var n := unit_name.to_lower()
	var generic := "generic_arrow"
	if n.contains("тролл") or n.contains("troll") or n.contains("headhunter") or n.contains("берсерк") or n.contains("топор"):
		generic = "generic_arrow"
	elif n.contains("копь") or n.contains("spear") or n.contains("виверн") or n.contains("wyvern"):
		generic = "generic_spear"
	elif n.contains("мином") or n.contains("пушк") or n.contains("катапульт") or n.contains("баллист") or n.contains("снаряд"):
		generic = "generic_bolt"
	elif n.contains("лед") or n.contains("мороз") or n.contains("frost") or n.contains("ice") or n.contains("снеж"):
		generic = "generic_ice"
	elif n.contains("огон") or n.contains("fire") or n.contains("плам") or n.contains("лава"):
		generic = "generic_fire"
	elif attack_type == "siege":
		generic = "generic_bolt"
	var path := "res://assets/models_missiles/%s.glb" % generic
	return path if ResourceLoader.exists(path) else ""


func spawn_projectile(from_unit: Node, target_unit: Node, amount: float) -> Projectile:
	if not is_instance_valid(target_unit):
		return null
	var p := ProjectileScript.new()
	p.model_path = str(from_unit.active_weapon.get("missile_override",missile_for(str(from_unit.rawcode), str(from_unit.kind), str(from_unit.attack_type),int(from_unit.active_weapon.get("slot",1)))))
	p.speed = maxf(0,float(from_unit.active_weapon.get("missile_speed",26)))
	p.arc_ratio = float(from_unit.active_weapon.get("missile_arc",0))
	p.homing = bool(from_unit.active_weapon.get("missile_homing",true))
	p._life = maxf(4,float(from_unit.active_weapon.get("range",30))/maxf(.1,p.speed)+3)
	projectiles_root.add_child(p)
	p.setup(from_unit.global_position + Vector3(0, 1.2, 0), target_unit, amount,
		from_unit.team, self)
	p.hit = not high_ground_miss(from_unit, target_unit)
	p.attack_type = str(from_unit.attack_type)
	p.attacker = from_unit
	p.critical = bool(from_unit.last_attack_critical)
	p.stun_duration = float(from_unit.last_stun_duration)
	p.incendiary = "A02J" in from_unit.abilities
	if p.incendiary: p.fire_ability = "A02J"
	p.splash = from_unit.active_weapon.get("splash",{})
	p.splash_air = bool(from_unit.active_weapon.get("can_air",false))
	p.weapon_type = str(from_unit.active_weapon.get("weapon_type","missile"))
	p.attack_pattern = from_unit.active_weapon.get("pattern",{}).duplicate(true)
	return p


func spawn_castle_projectile(from_unit: Node, castle_team: int, amount: float) -> void:
	var p := ProjectileScript.new()
	p.incendiary = "A02J" in from_unit.abilities
	if p.incendiary: p.fire_ability = "A02J"
	p.model_path = str(from_unit.active_weapon.get("missile_override",missile_for(str(from_unit.rawcode), str(from_unit.kind), str(from_unit.attack_type),int(from_unit.active_weapon.get("slot",1)))))
	p.speed = maxf(0,float(from_unit.active_weapon.get("missile_speed",26)))
	p.arc_ratio = float(from_unit.active_weapon.get("missile_arc",0))
	p.homing = bool(from_unit.active_weapon.get("missile_homing",true))
	p._life = maxf(4,float(from_unit.active_weapon.get("range",30))/maxf(.1,p.speed)+3)
	p.critical = bool(from_unit.last_attack_critical)
	p.attacker = from_unit
	p.splash = from_unit.active_weapon.get("splash",{})
	p.weapon_type = str(from_unit.active_weapon.get("weapon_type","missile"))
	p.attack_pattern = from_unit.active_weapon.get("pattern",{}).duplicate(true)
	projectiles_root.add_child(p)
	p.attack_type = str(from_unit.attack_type)
	var pos := Vector3(castles[castle_team]["x"] * WC3_TO_M, 3.0, 0.0)
	p.setup(from_unit.global_position + Vector3(0, 1.2, 0), null, amount,
		from_unit.team, self, castle_team, pos)


func optimized_queries() -> bool:
	return Settings.army_optimization==2 or (Settings.army_optimization==1 and units.size()>=120)

func can_detect(side: int, target: GameUnit) -> bool:
	if not target.invisible or target.team==side: return true
	if preload("res://scripts/game/elven_rules.gd").reveals(target,side): return true
	if _detector_frame!=Engine.get_process_frames() or _detector_count!=units.size()+huts.size():
		_detector_frame = Engine.get_process_frames()
		_detector_count = units.size()+huts.size()
		_detectors.clear()
		var sources: Array = units.duplicate()
		for hut in huts:
			if is_instance_valid(hut.get("node")): sources.append(hut.node)
		for source in sources:
			if not is_instance_valid(source) or not source.alive: continue
			var ids: Array = source.abilities if source is GameUnit else unit_traits.get(source.rawcode,{}).get("abilities",[])
			for id in ids:
				var cfg: Dictionary = ability_db.get(id,{})
				if str(cfg.get("code",""))=="Atru": _detectors.append({"node":source,"radius":maxf(float(cfg.get("range",0)),float(cfg.get("area",0)))})
	for entry in _detectors:
		var detector = entry.node
		if not is_instance_valid(detector) or not detector.alive or detector.team!=side: continue
		if Vector2(detector.position.x-target.position.x,detector.position.z-target.position.z).length()<=float(entry.radius): return true
	return false

func find_target(unit: Node, units_only := false):
	if unit is GameUnit and unit.invisible:
		return preload("res://scripts/game/assassin_rules.gd").hunt(unit)
	var old = unit.combat_target
	if is_instance_valid(old) and old is GameUnit and old.alive and not old.invulnerable and old.team != unit.team:
		var weapon: Dictionary = unit.weapon_for(old)
		if not weapon.is_empty() and Vector2(old.position.x-unit.position.x,old.position.z-unit.position.z).length() <= maxf(unit.aggro_range,float(weapon["range"])+4):
			unit.skirmish_active = true
			return load("res://scripts/game/nerubian_rework033.gd").hunt_target(unit,old)
	var best = null
	var best_score := -INF
	var candidates: Array = units
	var siblings: Array = units
	if optimized_queries():
		var radius: float = unit.aggro_range
		for weapon in unit.weapons: radius = maxf(radius,float(weapon.range)+3)
		candidates = unit_index.query(unit.position,radius)
		if unit.formation_group!=0: siblings = unit_index.siblings(unit.formation_group)
	for other in candidates:
		if not is_instance_valid(other) or not other.alive or other.invulnerable or other.team == unit.team: continue
		var weapon: Dictionary = unit.weapon_for(other)
		if weapon.is_empty(): continue
		var distance: float = Vector2(other.position.x-unit.position.x,other.position.z-unit.position.z).length()
		if distance > maxf(unit.aggro_range,float(weapon["range"])+3): continue
		var score := attack_multiplier(str(weapon["attack_type"]),other.armor_type)*1.5-distance
		if unit.formation_group!=0:
			for sibling in siblings:
				if sibling!=unit and is_instance_valid(sibling) and sibling.alive and sibling.formation_group==unit.formation_group and sibling.combat_target==other: score -= 3
		if score > best_score:
			best_score = score
			best = other
	if best != null:
		unit.skirmish_active = true
		return load("res://scripts/game/nerubian_rework033.gd").hunt_target(unit,best)
	if units_only: return null
	# JASS cB issues attack-move to the opposite castle. Passive buildings do
	# not acquire units from the entire minion engagement radius. Only a unit
	# pulled into actual weapon reach can turn onto a nearby structure.
	if castle_in_weapon_range(unit):
		unit.skirmish_active = false
		return null
	if not unit.skirmish_active: return null
	var nearest := INF
	for hut in huts:
		var node = hut.get("node")
		if int(hut["team"]) == unit.team or not is_instance_valid(node) or not node.alive: continue
		var weapon: Dictionary = unit.weapon_for(node,true)
		if weapon.is_empty(): continue
		var distance: float = Vector2(node.position.x-unit.position.x,node.position.z-unit.position.z).length()-node.collision_radius()
		if distance<=float(weapon["range"]) and distance<nearest:
			nearest = distance
			best = node
	if best==null: unit.skirmish_active = false
	return best

func castle_in_weapon_range(unit: Node) -> bool:
	var weapon: Dictionary = unit.weapon_for(null,true)
	if weapon.is_empty(): return false
	var enemy: int = 1-unit.team
	var center := Vector2(float(castles[enemy]["x"])/30,64.0/30)
	return Vector2(unit.position.x,unit.position.z).distance_to(center)<=float(weapon["range"])+float(castles[enemy].get("collision",172.0/30))



func heal_lowest_ally(caster: Node, amount: float, radius: float) -> void:
	var best = null
	var best_ratio := 1.0
	for u in units:
		if not is_instance_valid(u) or not u.alive or u.team != caster.team:
			continue
		var d: float = Vector2(u.global_position.x - caster.global_position.x,
			u.global_position.z - caster.global_position.z).length()
		if d > radius:
			continue
		var ratio: float = u.hp / u.max_hp
		if ratio < best_ratio:
			best_ratio = ratio
			best = u
	if best != null:
		var healed := minf(amount, best.max_hp-best.hp)
		best.heal(amount)
		combat_text(best, "+%d" % roundi(healed), Color(0.3, 1.0, 0.4))
		caster._play_sound(effect_sound("heal"))
		print("[Gameplay] %s healed %s (+%.0f)" % [caster.kind, best.kind, amount])


func apply_slow(target: Node, factor: float, duration: float) -> void:
	if not is_instance_valid(target) or not target.alive or not target is GameUnit:
		return
	target.slow_factor = 1.0 - factor
	target.slow_timer = duration
	combat_text(target, "ЗАМЕДЛЕНИЕ", Color(0.45, 0.8, 1))
	print("[Gameplay] %s slowed %.0f%%" % [target.kind, factor * 100.0])


func apply_buff(unit: Node, mult: float, duration: float, config: Dictionary = {}) -> void:
	if not is_instance_valid(unit) or not unit.alive or not unit is GameUnit:
		return
	unit.buff_mult = 1.0 + mult
	unit.buff_timer = duration
	unit.buff_armor = float(config.get("armor", 0))
	unit.buff_haste = float(config.get("haste", 0))
	unit.buff_speed = float(config.get("speed", 0))
	unit.buff_name = "ЗАЩИТА" if unit.buff_armor > 0 else ("ЖАЖДА КРОВИ" if unit.buff_haste > 0 else "УСИЛЕНИЕ")
	combat_text(unit, unit.buff_name, Color(1, 0.75, 0.2))
	print("[Gameplay] %s buffed +%.0f%% damage" % [unit.kind, mult * 100.0])


func try_damage(attacker: Node, target: Node, amount: float, atype := "normal", critical := false, stun := 0.0) -> void:
	if not is_instance_valid(attacker) or not is_instance_valid(target):
		return
	if high_ground_miss(attacker, target):
		combat_text(target, "ПРОМАХ", Color(0.8, 0.85, 1))
		return
	resolve_attack(attacker, target, amount, attacker.team, atype, critical, stun)


func resolve_attack(attacker: Node, target: Node, amount: float, team: int, atype: String, critical := false, stun := 0.0, secondary := false) -> void:
	if not is_instance_valid(target) or not target.alive:
		return
	if target is GameUnit and randf() < target.evasion:
		combat_text(target, "УКЛОНЕНИЕ", Color(0.5, 0.85, 1))
		return
	var before: float = target.hp
	var spell_bonus := 0.0
	if attacker is GameUnit and attacker.alive and not secondary:
		spell_bonus = preload("res://scripts/game/spell_runtime.gd").attack_spell_bonus(attacker,target)
	amount *= load("res://scripts/game/nerubian_rework033.gd").hunt(attacker,target)
	damage_unit(target, amount, team, atype, critical, false,attacker)
	if spell_bonus>0 and is_instance_valid(target) and target.alive:
		damage_unit(target,spell_bonus,team,"magic",false,true,attacker)
	var dealt := maxf(0, before - target.hp)
	if not secondary: elemental.after_weapon(attacker,dealt)
	if not secondary: pandaren.after_weapon(attacker,target)
	if not secondary or attacker is Building and attacker.rawcode in ["hNT1","hNT2"]: preload("res://scripts/game/nerubian_spells.gd").after_weapon(attacker,target,dealt)
	preload("res://scripts/game/northern_rules.gd").after_weapon(attacker,target)
	if target is GameUnit and target.alive and stun > 0:
		target.stun_timer = maxf(target.stun_timer,stun)
		combat_text(target,"ОГЛУШЕНИЕ",Color(0.75,0.8,1))
	if attacker is Building and target is GameUnit and target.alive:
		for id in str(GameDB.unit(attacker.rawcode).get("uabi","")).split(","):
			var passive: Dictionary = ability_db.get(id,{})
			if str(passive.get("kind",""))=="feedback": preload("res://scripts/game/spell_runtime.gd").feedback_hit(attacker,target,passive)
			if str(passive.get("kind",""))=="poison" and preload("res://scripts/game/spell_runtime.gd").valid_target(attacker,target,passive,false):
				var venom_cfg: Dictionary = passive.duplicate(true)
				venom_cfg["nerub_source"] = attacker.get_instance_id()
				preload("res://scripts/game/spell_runtime.gd").effect(target,venom_cfg,team)
	if is_instance_valid(attacker) and attacker.alive and attacker is GameUnit:
		if not secondary: preload("res://scripts/game/spell_runtime.gd").on_hit(attacker,target,dealt)
		if attacker.chilling and target is GameUnit and target.alive:
			apply_slow(target, 0.2, 2)
		if target is GameUnit and target.spell_resist<1 and str(attacker.active_weapon.get("weapon_type","normal"))=="normal": attacker.heal(dealt * attacker.aura_value("vampiric"))
		if target is GameUnit and str(attacker.active_weapon.get("weapon_type","normal"))=="normal" and target.aura_value("thorns") > 0:
			damage_unit(attacker, dealt * target.aura_value("thorns"), target.team, "magic")
		if target is GameUnit and str(attacker.active_weapon.get("weapon_type","normal"))=="normal":
			for id in target.abilities:
				var cfg: Dictionary = preload("res://scripts/game/combat_rules.gd").configuration(target,id)
				if str(cfg.get("kind","")) == "spiked" and float(cfg.get("reflect",0)) > 0:
					damage_unit(attacker,dealt*float(cfg["reflect"])+float(cfg.get("bonus",0)),target.team,"magic")


func tower_weapon(hut: Dictionary, target: Node) -> Dictionary:
	if target is GameUnit and target.invisible and not can_detect(int(hut.team),target): return {}
	if target is GameUnit and (target.rawcode=="YTct" or target.effects.any(func(e):return bool(e.get("untargetable",false)))): return {}
	for weapon in unit_traits.get(str(hut["line_raw"]),{}).get("weapons",[]):
		if target is GameUnit and target.effects.any(func(e):return bool(e.get("ethereal",false))) and not str(weapon.get("attack_type","normal")) in ["magic","spells"]: continue
		var airborne: bool = target.flying and not target.grounded
		if airborne and not bool(weapon.get("can_air",false)): continue
		if not airborne and not bool(weapon.get("can_ground",false)): continue
		return weapon
	return {}

func _tick_tower(hut: Dictionary, delta: float) -> void:
	var node = hut.get("node")
	if not is_instance_valid(node) or not node.alive: return
	if str(hut.line_raw)=="h001":
		human.tick_artillery(hut,delta)
		return
	hut["weapon_cd"] = maxf(0,float(hut.get("weapon_cd",0))-delta)
	if hut.has("pending_shot"):
		var shot: Dictionary = hut["pending_shot"]
		shot["remaining"] = float(shot["remaining"])-delta
		if float(shot["remaining"])<=0:
			hut.erase("pending_shot")
			for index in shot["targets"].size():
				var target = shot["targets"][index]
				hut["barrage_secondary"] = index>0
				if is_instance_valid(target) and target.alive and not tower_weapon(hut,target).is_empty():
					var factor := .6 if index>0 and str(hut.line_raw) in ["hNT1","hNT2"] else 1.0
					if Vector2(target.position.x-node.position.x,target.position.z-node.position.z).length()<=float(shot.weapon.range): tower_attack(hut,target,float(shot["weapon"]["damage"])*factor,shot["weapon"])
			hut.erase("barrage_secondary")
		return
	if float(hut["weapon_cd"])>0: return
	var candidates: Array = []
	for target in units:
		if not is_instance_valid(target) or not target.alive or target.invulnerable or target.team==int(hut["team"]): continue
		var weapon := tower_weapon(hut,target)
		if weapon.is_empty(): continue
		var distance := Vector2(target.position.x-node.position.x,target.position.z-node.position.z).length()
		if distance<=float(weapon["range"]): candidates.append({"unit":target,"distance":distance,"weapon":weapon})
	if candidates.is_empty(): return
	candidates.sort_custom(func(a,b): return float(a["distance"])<float(b["distance"]))
	var chosen: Dictionary = candidates[0]["weapon"]
	var limit := 2 if str(hut.line_raw) in ["hNT1","hNT2"] else 1
	var barrage: Dictionary = {}
	for id in str(GameDB.unit(str(hut["line_raw"])).get("uabi","")).split(","):
		var cfg: Dictionary = ability_db.get(id,{})
		if str(cfg.get("kind",""))=="barrage":
			limit = maxi(limit,int(cfg.get("count",1)))
			barrage = cfg
	var targets: Array = []
	for candidate in candidates:
		if int(candidate["weapon"]["slot"])!=int(chosen["slot"]) or targets.size()>=limit: continue
		if not targets.is_empty() and barrage.has("secondary_range") and float(candidate.distance)>float(barrage.secondary_range): continue
		targets.append(candidate["unit"])
	hut["weapon_cd"] = float(chosen["cooldown"])
	if not targets.is_empty(): preload("res://scripts/game/advanced_spells.gd").on_attacked(targets[0])
	hut["pending_shot"] = {"remaining":float(chosen.get("damage_point",0)),"targets":targets,"weapon":chosen}
	node._play("Attack",true)
	if float(chosen.get("damage_point",0))<=0: _tick_tower(hut,0)

func tower_attack(hut: Dictionary, target: Node, amount: float, weapon := {}) -> void:
	var building = hut.get("node")
	if is_instance_valid(building): amount *= 1+float(building.get_meta("mech_damage_bonus",0))
	var critical := false
	var stun := 0.0
	var fields: Dictionary = GameDB.unit(str(hut.get("line_raw", "")))
	for id in str(fields.get("uabi", "")).split(","):
		var profile: Dictionary = combat_profiles.get(id, {})
		if str(profile.get("kind", "")) == "critical" and randf() < float(profile.get("chance", 0)):
			amount *= float(profile.get("multiplier", 2))
			critical = true
			break
	for id in str(fields.get("uabi", "")).split(","):
		var profile: Dictionary = combat_profiles.get(id,{})
		if target is GameUnit and str(profile.get("kind",""))=="bash" and ((target.flying and not target.grounded and str(profile.get("targets","")).contains("air")) or ((not target.flying or target.grounded) and str(profile.get("targets","")).contains("ground"))) and randf()<float(profile.get("chance",0)):
			amount += float(profile.get("bonus",0))
			stun = float(profile.get("hero_duration",0) if target.resistant_skin or target.armor_type=="hero" else profile.get("duration",0))
	var node = hut.get("node")
	if weapon.is_empty(): weapon = tower_weapon(hut,target)
	var attack_type := str(weapon.get("attack_type",hut.get("attack_type","pierce")))
	if str(weapon.get("weapon_type","normal")) not in ["normal","instant"]:
		var projectile := ProjectileScript.new()
		projectile.model_path = missile_for(str(hut["line_raw"]),str(hut["name"]),attack_type,int(weapon.get("slot",1)))
		projectile.speed = maxf(0,float(weapon.get("missile_speed",26)))
		projectile.arc_ratio = float(weapon.get("missile_arc",0))
		projectile.homing = bool(weapon.get("missile_homing",true))
		# A015 overrides missile art on the additional barrage attacks.
		if str(hut.line_raw)=="h014" and bool(hut.get("barrage_secondary",false)):
			var barrage: Dictionary = ability_db.A015
			projectile.model_path = "native:"+str(vfx.visuals.A015.missile[0].key)
			projectile.speed = float(barrage.fields.amsp)/30
			projectile.homing = bool(int(barrage.fields.amho))
		projectile._life = maxf(4,float(weapon.get("range",30))/maxf(.1,projectile.speed)+3)
		projectile.attack_type = attack_type
		projectile.critical = critical
		projectile.stun_duration = stun
		projectile.attacker = node
		projectile.incendiary = str(hut.line_raw)=="h001"
		projectile.splash = weapon.get("splash",{})
		projectile.splash_air = bool(weapon.get("can_air",false))
		projectile.weapon_type = str(weapon.get("weapon_type","missile"))
		projectiles_root.add_child(projectile)
		projectile.setup(node.global_position+Vector3.UP*3,target,amount,int(hut["team"]),self)
	else: resolve_attack(node,target,amount,int(hut["team"]),attack_type,critical,stun)
	if is_instance_valid(node):
		var player = node.get_meta("weapon_audio") if node.has_meta("weapon_audio") else null
		if player == null:
			player = AudioStreamPlayer3D.new()
			player.bus = "SFX"
			player.unit_size = 15
			player.max_db = -6
			node.add_child(player)
			node.set_meta("weapon_audio", player)
		var sound := weapon_sound(str(hut.get("line_raw", "")), str(hut.get("name", "")), hut)
		if not sound.is_empty():
			player.stream = load(sound)
			player.play()


func high_ground_miss(attacker: Node, target: Node) -> bool:
	if attacker.flying:
		return false
	if attacker.attack_range <= 4.0:
		return false
	var ha: float = terrain.height_at_wc3(attacker.global_position.x / WC3_TO_M, attacker.global_position.z / WC3_TO_M)
	var hb: float = terrain.height_at_wc3(target.global_position.x / WC3_TO_M, target.global_position.z / WC3_TO_M)
	return ha + 2.0 < hb and randf() < 0.25


func show_float(pos: Vector3, text: String, color: Color) -> void:
	var label := Label3D.new()
	label.text = text
	var notable := text.contains("КРИТ") or text.contains("УКЛОНЕНИЕ") or text.contains("ПРОМАХ")
	label.font_size = 32 if notable else 28
	label.pixel_size = 0.0008 if notable else 0.0006
	label.fixed_size = true
	label.outline_size = 8
	label.modulate = color
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.no_depth_test = true
	label.position = pos + Vector3(randf_range(-0.4, 0.4), 2.6, randf_range(-0.4, 0.4))
	add_child(label)
	var tw := label.create_tween()
	tw.set_parallel(true)
	tw.tween_property(label, "position:y", label.position.y + 1.4, 0.8)
	tw.tween_property(label, "modulate:a", 0.0, 0.8).set_delay(0.25)
	tw.chain().tween_callback(label.queue_free)


func combat_text(unit: Node3D, message: String, color: Color) -> void:
	if not unit.visible:
		return
	var bar := unit.get_node_or_null("HP")
	var offset := float(bar.position.y)+3.0 if bar != null else 4.0
	show_float(unit.global_position+Vector3.UP*offset, message, color)


func attack_multiplier(atype: String, armor_type: String) -> float:
	var tbl: Dictionary = damage_table.get(atype, {})
	return float(tbl.get(armor_type, 1.0))


func damage_unit(target, amount: float, killer_team := -1, atype := "normal", critical := false, spell := true, killer: Node = null, native: Dictionary = {}) -> void:
	if target is GameUnit and preload("res://scripts/game/corrupted_rules.gd").airborne(target): return
	if not is_instance_valid(target) or not target.alive:
		return
	if target is GameUnit and target.invulnerable: return
	var resolved := bool(native.get("pandaren_debt",false))
	var aspect_extra := 0.0
	var mult := 1.0
	var armor_red := 1.0
	var unblocked_amount := amount
	if not resolved:
		var original_amount := amount
		amount = aspects.outgoing(killer,amount,critical,spell)
		aspect_extra = maxf(0,amount-original_amount)
		if target is GameUnit and not spell and not atype in ["magic","spells"] and target.effects.any(func(e):return bool(e.get("ethereal",false))): return
		if target is Building and not spell and is_instance_valid(killer): amount *= preload("res://scripts/game/combat_rules.gd").structure_multiplier(killer)
		var armor_type := str(target.get("armor_type") if target.get("armor_type") != null else "normal")
		var armor := float(target.get("armor") if target.get("armor") != null else 0.0)
		if target is GameUnit:
			armor += target.aura_value("armor") + target.buff_armor + target.item_armor
			if spell and atype == "magic":
				amount *= 1.0 - (0.0 if bool(native.get("bypass_resistance",false)) and target.spell_resist<1 else clampf(target.spell_resist, 0, 1))
				for status in target.effects:
					if bool(status.get("ethereal",false)): amount *= 1.66
			amount *= maxf(0,1+target.aura_value("incoming"))
			if not spell:
				if atype=="pierce" and "A03G" in target.abilities: amount *= float(ability_db.A03G.defend)
				for id in target.abilities:
					var cfg: Dictionary = preload("res://scripts/game/combat_rules.gd").configuration(target,id)
					if str(cfg.get("kind","")) == "spiked": amount *= 1-float(cfg.get("reduction",0))
		mult = 1.0
		var tbl: Dictionary = damage_table.get(str(native.get("attack_table","spells" if spell and atype == "magic" else atype)), {})
		mult = float(tbl.get(armor_type, 1.0))
		armor_red = 1.0
		if armor > 0.0:
			armor_red = 1.0 - armor * 0.06 / (1.0 + 0.06 * armor)
		elif armor < 0:
			armor_red = 2.0 - pow(0.94, -armor)
		if spell and atype == "magic" or bool(native.get("ignore_armor",false)): armor_red = 1.0
		amount = amount * mult * armor_red
		unblocked_amount = amount
		if amount>0 and target is GameUnit and target.stealth_time>0:
			preload("res://scripts/game/assassin_rules.gd").reveal(target)
			if is_instance_valid(killer) and killer.team!=target.team: target.combat_target = killer
		if not spell and target is GameUnit and not target.skin_profile.is_empty():
			var profile: Dictionary = target.skin_profile
			if randf() < float(profile.get("chance",1)):
				amount = minf(amount,maxf(float(profile.get("minimum",1)),amount-float(profile.get("block",0))))
	var previous_hp: float = target.hp
	var aspect_cap: float = amount if resolved else aspects.incoming(target,unblocked_amount)
	if target.team in [0,1]: metrics.teams[target.team].aspect_prevented += maxf(0,amount-aspect_cap)
	amount = minf(amount,aspect_cap)
	if float(native.get("minimum_hp",0))>0:
		amount = minf(amount,maxf(0,previous_hp-float(native.minimum_hp)))
		if amount<=0: return
	# nh/PG reads the target's life before Warcraft commits the weapon hit.
	if not resolved and undead!=null and not bool(native.get("suppress_undead",false)):
		undead.after_damage(killer,target,minf(previous_hp,amount),previous_hp)
		if not target.alive: return
	if not resolved: amount = pandaren.defer_damage(target,amount,killer_team,killer)
	target.take_damage(amount)
	var dealt := maxf(0,previous_hp-maxf(0,target.hp))
	metrics.dealt(killer,killer_team,dealt,target.team)
	if killer_team in [0,1] and killer_team!=target.team: metrics.teams[killer_team].aspect_damage += minf(dealt,aspect_extra*mult*armor_red)
	if not resolved:
		aspects.after_damage(killer,target,minf(previous_hp,maxf(0,amount)),spell,atype)
		if amount>0 and elemental!=null and not bool(native.get("suppress_elemental",false)):
			elemental.after_damage(killer,target,minf(previous_hp,amount))
		if amount>0 and mech!=null and not bool(native.get("suppress_elemental",false)):
			mech.after_damage(killer,target,minf(previous_hp,amount))
		if amount>0:
			if target is GameUnit: preload("res://scripts/game/nature_rules.gd").on_damage(target)
			if is_instance_valid(killer): preload("res://scripts/game/nature_rules.gd").on_dealt(killer,target)
		pandaren.after_damage(target,amount)
		preload("res://scripts/game/nerubian_spells.gd").after_damage(target,amount,spell)
	if target.visible:
		combat_text(target, ("КРИТ! %d" if critical else "-%d") % maxi(1, roundi(amount)), Color(1, 0.82, 0.15) if critical else Color(1.0, 0.35, 0.25))
	if not target.alive:
		# Nested native damage (earth bonus / forged weapon) may have already
		# processed this death while the outer damage event was still running.
		if target.get_meta("death_processed",false): return
		target.set_meta("death_processed",true)
		if killer_team in [0,1] and killer_team!=target.team and target is GameUnit: metrics.teams[killer_team].kills += 1
		if target.kind == "building":
			for h in huts:
				if h.get("node") == target:
					var cell = h.get("cell")
					if cell != null:
						cell["occupied"] = false
					huts.erase(h)
					break
			print("[Gameplay] building %s destroyed" % str(target.display_name))
			await get_tree().create_timer(1.0).timeout
			target.queue_free()
			return
		var bounty := 0 if bool(target.get_meta("no_bounty", false)) else maxi(1, int(target.max_hp / 50.0))
		if killer_team == 0:
			gold += bounty
			kills[0] += 1
		elif killer_team == 1:
			bot_gold += bounty
			kills[1] += 1
		units.erase(target)
		aspects.died(target,killer)
		elemental.died(killer,target,killer_team)
		mech.died(target)
		undead.died(target)
		preload("res://scripts/game/spell_runtime.gd").on_death(target)
		night.killed(killer,target)
		preload("res://scripts/game/spell_runtime.gd").on_kill(killer,target)
		var converted: bool = load("res://scripts/game/undead_rules.gd").vampire_kill(killer,target)
		if not converted and corpse_eligible(target):
			corpses.append({"pos":target.global_position,"team":target.team,"life":float(unit_traits[target.rawcode].get("corpse_duration",90)),"rawcode":target.rawcode,"node":target})
		if not converted: elven.on_death(target,killer_team)
		print("[Gameplay] %s team %d died (units left %d, bounty %d)" % [
			target.kind, target.team, units.size(), bounty])


func damage_mult(team: int, receiver: Node = null) -> float:
	var owned := inventory_for(team)
	return (1.0+(damage_bonus if team==0 else 0.0)+(float(owned.passive_bonus("damage_bonus",receiver)) if owned!=null else 0.0)+(damage_boost if team==0 else bot_damage_boost))*(1+preload("res://scripts/game/aspects.gd").stat(receiver,"damage"))


func speed_mult(team: int, receiver: Node = null) -> float:
	var owned := inventory_for(team)
	return (1.0+(speed_bonus if team==0 else 0.0)+(float(owned.passive_bonus("speed_bonus",receiver)) if owned!=null else 0.0))*(1+preload("res://scripts/game/aspects.gd").stat(receiver,"speed"))


func haste_mult(team: int, receiver: Node = null) -> float:
	var owned := inventory_for(team)
	return (float(owned.passive_bonus("haste_bonus",receiver)) if owned!=null else 0.0)+preload("res://scripts/game/aspects.gd").stat(receiver,"haste")


func buy_item(index: int) -> bool:
	return inventory.buy(index)


func sync_castle_hp(team: int) -> void:
	for castle in get_tree().get_nodes_in_group("castle_%d" % team):
		castle.set_hp(float(castles[team]["hp"])/castle_max_hp(team),float(castles[team]["hp"]),castle_max_hp(team))


func heal_castle(team: int, amount: float) -> float:
	var before := float(castles[team].hp)
	castles[team].hp = minf(castle_max_hp(team),before+amount)
	var restored := maxf(0,float(castles[team].hp)-before)
	if metrics!=null and not Net.is_client: metrics.healed(team,restored)
	sync_castle_hp(team)
	return restored

func castle_max_hp(team: int) -> float:
	return float(castles[team].get("max_hp",CASTLE_HP))

func draw_beam(start: Vector3, end: Vector3, color: Color) -> void:
	if Net.is_server and Net.effects.size()<128: Net.effects.append({"kind":"beam","p":[start.x,start.y,start.z],"end":[end.x,end.y,end.z],"color":color.to_html()})
	var mesh := ImmediateMesh.new()
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.albedo_color = color
	mat.no_depth_test = true
	mesh.surface_begin(Mesh.PRIMITIVE_LINES,mat)
	mesh.surface_add_vertex(start)
	mesh.surface_add_vertex(end)
	mesh.surface_end()
	var instance := MeshInstance3D.new()
	instance.mesh = mesh
	projectiles_root.add_child(instance)
	get_tree().create_timer(0.3).timeout.connect(instance.queue_free)


func name_for(raw: String) -> String:
	var labels := {"h05I":"Руины магической башни","h01M":"Ластик · уровень 1","h01W":"Ластик · уровень 2","h01X":"Ластик · уровень 3","h01F":"Вулкан","h01E":"Святыня Разрушения","h010":"Рог Гьяллархорн"}
	if labels.has(raw): return str(labels[raw])
	return str(display_names.get(raw,GameDB.unit_name(raw)))


func launch_nuke(hut: Dictionary) -> bool:
	if Net.is_client: return Net.command("nuke",{"id":int(hut.get("node").get_meta("network_id",0))})
	if str(hut.get("line_raw",""))!="h06K": return false
	var node = hut.get("node")
	if not is_instance_valid(node) or not node.alive or node.disabled_for > 0 or float(hut.get("nuke_cd",0)) > 0: return false
	hut["nuke_cd"] = 30.0
	var projectile := ProjectileScript.new()
	projectile.model_path = missile_for("","magic fire","magic")
	projectile.attack_type = "spells"
	projectile.blast_radius = 20
	projectile.blast_damage = 600
	projectile._life = 20
	projectile.speed = 45
	projectile.scale = Vector3.ONE*2
	projectiles_root.add_child(projectile)
	var team := int(hut.get("team",0))
	projectile.setup(node.position+Vector3.UP*6,null,400,team,self,1-team,terrain.wc3_to_world(5120 if team==0 else -5120,64)+Vector3.UP*2)
	return true


func damage_castle(team: int, amount: float, critical := false, atype := "normal", spell := false, killer: Node = null, native: Dictionary = {}) -> void:
	if Match.activity=="tutorial": return
	if game_over:
		return
	amount = aspects.outgoing(killer,amount,critical,spell)
	if not spell and is_instance_valid(killer): amount *= preload("res://scripts/game/combat_rules.gd").structure_multiplier(killer)
	var magic := spell or atype=="spells"
	amount *= attack_multiplier(str(native.get("attack_table","spells" if magic else atype)),str(castles[team].get("armor_type","fortified")))
	if not magic and not bool(native.get("ignore_armor",false)):
		var armor := float(castles[team].get("armor",5))
		amount *= 1.0/(1.0+.06*armor) if armor>=0 else 2.0-pow(.94,-armor)
	if team == 0:
		amount *= (1.0 - castle_armor)
	var before: float = castles[team]["hp"]
	if undead!=null and not bool(native.get("suppress_undead",false)): undead.castle_hit(killer,team)
	castles[team]["hp"] = maxf(0.0, before - amount)
	metrics.dealt(killer,1-team,before-float(castles[team]["hp"]),team,true)
	if amount>0 and killer is GameUnit and killer.rawcode=="n02A":
		var rank := mini(3,int(castles[team].get("dragon_armor",0))+1)
		castles[team]["armor"] = float(castles[team].get("armor",5))-2.0*(rank-int(castles[team].get("dragon_armor",0)))
		castles[team]["dragon_armor"] = rank
	if int(before / 100.0) != int(castles[team]["hp"] / 100.0):
		print("[Gameplay] castle team %d hp %.0f" % [team, castles[team]["hp"]])
	show_float(Vector3(castles[team]["x"] * WC3_TO_M, 8.0, 0.0), ("КРИТ! %d" if critical else "-%d") % int(amount), Color(1,0.82,0.15) if critical else Color(1.0, 0.35, 0.25))
	for c in get_tree().get_nodes_in_group("castle_%d" % team):
		if c.has_method("set_hp"):
			c.set_hp(castles[team]["hp"] / castle_max_hp(team), castles[team]["hp"], castle_max_hp(team))
	if not spell:
		northern.castle_hit(killer,team)
		corrupted.castle_hit(killer,team)
		elemental.castle_hit(killer,team,minf(before,amount))
	if castles[team]["hp"] <= 0.0:
		game_over = true
		winner = 1 - team
		Match.finish_round(winner)
		if Net.is_server: Net.queue_finished_result()
		elif not Net.is_client and not Chronicle.playing:
			Progress.finish_solo(winner==0)
			if winner==0 and Match.activity=="challenge": Progress.finish_challenge(str(Match.challenge.get("id","")))
		print("[Gameplay] WINNER: team %d" % winner)


func select_line(index: int) -> void:
	if line_buildings.is_empty():
		return
	selected_line = clampi(index, 0, line_buildings.size() - 1)
	selected_tower = -1
	build_mode = true
	if grid:
		grid.set_overlay_visible(true)
	print("[Gameplay] build mode: %s" % str(line_buildings[selected_line].get("name", "")))


func build_line(index: int) -> void:
	if line_buildings.is_empty():
		return
	selected_line = clampi(index, 0, line_buildings.size() - 1)
	_build_hut()


func next_upgrade_cost(index: int) -> int:
	if line_buildings.is_empty() or index < 0 or index >= line_buildings.size():
		return 0
	var entry: Dictionary = line_buildings[index]
	var ups: Array = entry.get("upgrades_to", [])
	if ups.is_empty():
		return 0
	var nf: Dictionary = (GameDB.units.get(str(ups[0]), {}) as Dictionary)
	return maxi(50, int(nf.get("ugol", 150)))


func upgrade_line(index: int) -> void:
	if index < 0 or index >= line_buildings.size(): return
	for hut in huts:
		if int(hut["team"]) == 0 and str(hut["line_raw"]) == str(line_buildings[index]["rawcode"]):
			upgrade_hut(hut)
			return

func build_at_cell(team: int, cell: Dictionary, entry: Dictionary) -> bool:
	if game_over or cell == null or bool(cell.get("occupied", true)) or int(cell.get("team", -1)) != team:
		return false
	var cost := int(entry.get("cost", 100))
	if not build_error(entry, team,-1,false).is_empty(): return false
	var u_raw := str(entry.get("unit_rawcode", "hfoo"))
	if bool(entry.get("tower",false)) and not entry.has("unit_rawcode"): u_raw = ""
	var u_model := str(entry.get("unit_model", ""))
	if not u_model.is_empty() and not ResourceLoader.exists(u_model):
		u_model = ""
	var b_model := str(entry.get("building_model", ""))
	if not b_model.is_empty() and not ResourceLoader.exists(b_model):
		b_model = ""
	if b_model.is_empty() and team == 0 and not race_building_model.is_empty():
		b_model = race_building_model
	elif b_model.is_empty() and team == 1 and not bot_race_building_model.is_empty():
		b_model = bot_race_building_model
	# Doodad-style line buildings still break under the skin bake; use the
	# verified barracks GLB as a safe placeholder until the bake is fixed.
	var safe := ""
	if ResourceLoader.exists(safe):
		b_model = safe
	cell["occupied"] = true
	var p: Vector3 = terrain.wc3_to_world(cell["wc3"].x, cell["wc3"].y)
	var hut := BuildingScript.new()
	hut.position = p
	hut.setup(team, float(unit_traits.get(str(entry.get("rawcode", "")), {}).get("hp", 800)), str(entry.get("name", "Здание")))
	hut.configure_spells(self,str(entry.get("rawcode","")))
	var placed_model := false
	if not b_model.is_empty():
		var scene: PackedScene = BattleAssets.resource(b_model)
		if scene:
			var inst := scene.instantiate()
			hut.add_child(inst)
			_ground_model(inst)

			_play_model_anim(inst,str(entry.get("rawcode","")))
			placed_model = true
	if not placed_model:
		var mi := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = Vector3(6.0, 4.5, 6.0)
		mi.mesh = box
		mi.material_override = _stone_mat
		hut.add_child(mi)
	buildings.add_child(hut)
	var bsfx := AudioStreamPlayer3D.new()
	bsfx.unit_size = 20.0
	bsfx.max_db = -2.0
	bsfx.bus = "SFX"
	var constr := effect_sound("construction")
	if ResourceLoader.exists(constr):
		bsfx.stream = load(constr)
	hut.add_child(bsfx)
	if bsfx.stream != null:
		bsfx.play()
	huts.append({
		"team": team, "x": cell["wc3"].x, "y": cell["wc3"].y, "timer": Economy.spawn_interval(cost),
		"unit_kind": str((GameDB.units.get(u_raw, {}) as Dictionary).get("unam", u_raw)).to_lower(), "rawcode": u_raw, "unit_model": u_model,
		"interval": Economy.spawn_interval(cost), "line_raw": str(entry.get("rawcode", "")),
		"node": hut, "name": str(entry.get("name", "")),
		"hp": hut.hp, "max_hp": hut.max_hp, "cell": cell,
		"entry": entry.duplicate(true), "paused": false, "progress": 0.0, "cost": int(entry.get("cost", 100)), "spent": int(entry.get("cost", 100)),
		"tower": bool(entry.get("tower", false)),
		"damage": float(entry.get("damage", 0.0)), "range": float(entry.get("range", 0.0)),
		"cd": float(entry.get("cd", 1.5)),
		"ability": str(entry.get("ability", "")),
		"attack_type": str(entry.get("attack_type", "pierce")),
		"can_air": bool(entry.get("can_air", true)),
		"can_ground": bool(entry.get("can_ground", true)),
	})
	print("[Gameplay] builder finished %s at WC3 (%.0f, %.0f) ability=%s" % [
		str(entry.get("name", "")), cell["wc3"].x, cell["wc3"].y, str(entry.get("ability", ""))])
	var data: Dictionary = huts.back()
	hut.hut_ref = data
	data["income"] = cost * Economy.rate(self, str(entry.get("rawcode", "")))
	_pay_build(team, entry)
	if team == 0: built += 1
	return true


func available_gold(team := 0) -> int:
	return maxi(0, gold-reserved_gold) if team==0 else maxi(0,bot_gold-bot_reserved_gold)

func builder_for(team: int) -> Node:
	if team==0: return player_builder if is_instance_valid(player_builder) else null
	return network_builders[1] if network_builders.size()>1 and is_instance_valid(network_builders[1]) else null

func inventory_for(team: int) -> Node:
	return inventory if team==0 else bot_inventory

func add_gold(team: int, amount: int) -> void:
	if team==0: gold += amount
	else: bot_gold += amount

func prepare_network_server() -> void:
	for builder in network_builders:
		if is_instance_valid(builder): builder.controlled = true
	bot_build_cd = INF

func builder_inside_base(team: int, position: Vector3) -> bool:
	var wc3 := Vector2(position.x,position.z)*30
	return absf(wc3.y) <= 2016 and (wc3.x >= -6144 and wc3.x <= -2016 if team == 0 else wc3.x >= 2016 and wc3.x <= 6144)

func enforce_builder_bounds(builder: Node) -> bool:
	if builder_inside_base(builder.team,builder.position): return false
	builder.cancel_orders()
	builder.position = terrain.wc3_to_world(-4544 if builder.team==0 else 4544,448)
	combat_text(builder,"ВОЗВРАТ НА БАЗУ",Color(1,.8,.2))
	return true


func available_wood(team := 0) -> int:
	return maxi(0, wood-reserved_wood) if team==0 else maxi(0,bot_wood-bot_reserved_wood)

func legendary_used(team := 0) -> int:
	var count := 0
	for hut in huts:
		if int(hut["team"]) == team: count += Economy.slots(str(hut["line_raw"]))
	return count

func build_cost(entry: Dictionary, team := 0) -> int:
	return aspects.mark_up(str(entry.get("rawcode","")),int(entry.get("cost",0)),team)

func cost_text(entry: Dictionary) -> String:
	var text := "%d зол." % build_cost(entry)
	var lumber := Economy.wood_cost(entry)
	if lumber > 0: text += " · %d дер." % lumber
	if Economy.slots(str(entry.get("rawcode", ""))) > 0: text += " · 1 легенд."
	return text

func build_error(entry: Dictionary, team := 0, extra_slots := -1, include_reserved := true) -> String:
	if Match.activity=="challenge":
		if bool(Match.challenge.get("no_legends",false)) and Economy.slots(str(entry.get("rawcode","")))>0: return "Испытание: легендарные постройки недоступны"
		if team==0 and extra_slots<0 and Match.challenge.has("max_buildings"):
			var count := huts.filter(func(h): return int(h.team)==0).size()
			var builder := builder_for(0)
			if include_reserved and is_instance_valid(builder): count += builder.build_queue.filter(func(order):return order.has("cell")).size()+(1 if builder.build_cell!=null else 0)
			if count>=int(Match.challenge.max_buildings): return "Испытание: максимум %d построек" % int(Match.challenge.max_buildings)
	var funds: int = available_gold(team)
	var lumber: int = available_wood(team)
	if funds < build_cost(entry,team): return "Не хватает золота: нужно %d" % build_cost(entry,team)
	if lumber < Economy.wood_cost(entry): return "Не хватает дерева: нужно %d" % Economy.wood_cost(entry)
	var need := Economy.slots(str(entry.get("rawcode", ""))) if extra_slots < 0 else extra_slots
	var used := legendary_used(team)+(reserved_slots if team == 0 else bot_reserved_slots)
	if used+need > (legendary_limit if team == 0 else bot_legendary_limit): return "Заняты все легендарные места: %d" % used
	return ""

func reserve_build(entry: Dictionary, reserve := true, team := 0) -> void:
	var direction := 1 if reserve else -1
	if team==1:
		bot_reserved_gold = maxi(0,bot_reserved_gold+direction*build_cost(entry,team))
		bot_reserved_wood = maxi(0,bot_reserved_wood+direction*Economy.wood_cost(entry))
		bot_reserved_slots = maxi(0,bot_reserved_slots+direction*Economy.slots(str(entry.get("rawcode",""))))
		return
	reserved_gold = maxi(0, reserved_gold+direction*build_cost(entry,team))
	reserved_wood = maxi(0, reserved_wood+direction*Economy.wood_cost(entry))
	reserved_slots = maxi(0, reserved_slots+direction*Economy.slots(str(entry.get("rawcode", ""))))

func _pay_build(team: int, entry: Dictionary) -> void:
	var cost := int(entry.get("cost", 0))
	var reward := Economy.wood_reward(self, str(entry.get("rawcode", "")), cost)
	cost = build_cost(entry,team)
	if team == 0:
		gold -= cost
		wood += reward-Economy.wood_cost(entry)
	else:
		bot_gold -= cost
		bot_wood += reward-Economy.wood_cost(entry)

func upgrade_error(hut: Dictionary, next_raw: String, team := 0) -> String:
	if not huts.has(hut) or int(hut.get("team", -1)) != team or not upgrade_options(hut).has(next_raw): return "Улучшение недоступно"
	var node = hut.get("node")
	if not is_instance_valid(node) or not node.alive: return "Здание уничтожено"
	if node.disabled_for > 0: return "Здание временно отключено"
	var fields: Dictionary = GameDB.unit(next_raw)
	return build_error({"rawcode":next_raw, "cost":int(fields.get("ugol", 0))}, team, maxi(0,Economy.slots(next_raw)-Economy.slots(str(hut["line_raw"]))))

func selected_entry() -> Dictionary:
	if selected_tower >= 0 and selected_tower < tower_buildings.size():
		return tower_buildings[selected_tower]
	return line_buildings[selected_line] if selected_line < line_buildings.size() else {}


func cancel_build_mode() -> void:
	if is_instance_valid(build_preview): build_preview.hide()
	build_mode = false
	selected_tower = -1
	grid.set_overlay_visible(false)
	grid.hide_hover()


func effect_sound(effect: String) -> String:
	var choices: Array = combat_sounds.get(effect, [])
	return str(choices.pick_random()) if not choices.is_empty() else ""


func weapon_sound(raw: String, unit_name: String, traits: Dictionary) -> String:
	var type := str(traits.get("attack_type", "normal"))
	var name_lower := unit_name.to_lower()
	if raw in ["hrif", "h009"] or name_lower.contains("rifle") or name_lower.contains("sniper") or name_lower.contains("стрелок"):
		return effect_sound("rifle")
	if type == "siege":
		return effect_sound("siege")
	if type == "magic":
		return effect_sound("magic")
	return effect_sound("arrow" if float(traits.get("range", 3)) > 4 else "melee")


func buff_target(caster: Node, radius: float):
	var best = null
	var best_damage := -1.0
	for u in units:
		if not is_instance_valid(u) or not u.alive or u.team != caster.team or u.controlled or u.buff_timer > 1:
			continue
		if Vector2(u.position.x-caster.position.x, u.position.z-caster.position.z).length() <= radius and u.damage > best_damage:
			best = u
			best_damage = u.damage
	return best


## The map has one arch per base, centred at WC3 (+/-2016, 0).
func route_through_arch(team: int, spawn: Vector2) -> Array[Vector3]:
	var route: Array[Vector3] = []
	var direction := 1.0 if team == 0 else -1.0
	var gate_x := -2016.0 if team == 0 else 2016.0
	if (spawn.x - gate_x) * direction >= 0:
		return route
	var lane_x := spawn.x
	# Walk beside the keep rather than through its footprint.
	if absf(absf(lane_x) - 5120) < 320:
		lane_x = -4736 if team == 0 else 4736
		if absf(spawn.y) > 256:
			route.append(terrain.wc3_to_world(lane_x, spawn.y))
	route.append(terrain.wc3_to_world(lane_x, 0))
	route.append(terrain.wc3_to_world(gate_x - direction*256, 0))
	route.append(terrain.wc3_to_world(gate_x, 0))
	route.append(terrain.wc3_to_world(gate_x + direction*256, 0))
	return route


func base_travel_route(from: Vector3, to: Vector3) -> Array[Vector3]:
	var route: Array[Vector3] = []
	var start := Vector2(from.x, from.z)*30
	var finish := Vector2(to.x, to.z)*30
	for wall in [-3520.0, -2016.0, 2016.0, 3520.0]:
		if (start.x-wall)*(finish.x-wall) >= 0 or absf(start.x-finish.x) < 1:
			continue
		var crossing_y := lerpf(start.y, finish.y, (wall-start.x)/(finish.x-start.x))
		if absf(crossing_y) > 256:
			route.append(terrain.wc3_to_world(start.x, 0))
			route.append(terrain.wc3_to_world(finish.x, 0))
			break
	return route


func toggle_pause(hut: Dictionary) -> void:
	if Net.is_client:
		Net.command("pause",{"id":int(hut.get("node").get_meta("network_id",0))})
		return
	hut["paused"] = not bool(hut.get("paused", false))
	print("[Gameplay] %s %s" % [str(hut.get("name", "")), "paused" if hut["paused"] else "resumed"])


func production_state(team := 0) -> Dictionary:
	var total := 0; var paused := 0
	for hut in huts:
		var node: Node = hut.get("node")
		if int(hut.get("team",-1))!=team or bool(hut.get("tower",false)) or not is_instance_valid(node) or not node.alive: continue
		total += 1
		if bool(hut.get("paused",false)): paused += 1
	return {"total":total,"paused":paused,"all_paused":total>0 and total==paused}


func set_all_production_paused(paused: bool, team := 0) -> bool:
	if Net.is_client: return Net.command("pause_all",{"paused":paused})
	if team not in [0,1] or game_over: return false
	var changed := false
	for hut in huts:
		var node: Node = hut.get("node")
		if int(hut.get("team",-1))!=team or bool(hut.get("tower",false)) or not is_instance_valid(node) or not node.alive: continue
		hut["paused"] = paused
		changed = true
	return changed


func upgrade_options(hut: Dictionary) -> Array:
	var fields: Dictionary = GameDB.unit(str(hut.get("line_raw","")))
	var result: Array = []
	for raw in str(fields.get("uupt", "")).split(","):
		if not raw.is_empty(): result.append(raw)
	if result.is_empty() and not fields.has("uupt"):
		result = (hut.get("entry",{}) as Dictionary).get("upgrades_to",[]).duplicate()
	return result


func upgrade_hut(hut: Dictionary) -> void:
	var options := upgrade_options(hut)
	if not options.is_empty(): choose_upgrade(hut,str(options[0]))


func choose_upgrade(hut: Dictionary, next_raw: String, team := 0) -> bool:
	if Net.is_client: return Net.command("upgrade",{"id":int(hut.get("node").get_meta("network_id",0)),"raw":next_raw})
	if not upgrade_error(hut, next_raw,team).is_empty(): return false
	var fields: Dictionary = GameDB.unit(next_raw)
	var cost := maxi(0,int(fields.get("ugol",150)))
	var unit_raw := str(fields.get("utra", "")).split(",")[0]
	if unit_raw.is_empty() and not bool(hut.get("tower",false)): return false
	var entry: Dictionary = (hut.get("entry",{}) as Dictionary).duplicate(true)
	if unit_raw.is_empty() and int(fields.get("upoi",0)) in range(1000,1010): tech_level = maxi(tech_level,int(fields["upoi"])-1000)
	var next_options: Array = []
	for raw in str(fields.get("uupt", "")).split(","):
		if not raw.is_empty(): next_options.append(raw)
	entry.merge({"rawcode":next_raw,"name":name_for(next_raw),"unit_rawcode":unit_raw,"unit_name":name_for(unit_raw),"unit_model":"res://assets/models_lines/%s_u.glb" % unit_raw,"building_model":"res://assets/models_lines/%s_b.glb" % next_raw,"upgrades_to":next_options},true)
	hut.merge({"entry":entry,"line_raw":next_raw,"rawcode":unit_raw,"name":name_for(next_raw),"unit_kind":name_for(unit_raw).to_lower(),"unit_model":entry["unit_model"],"spent":int(hut.get("spent",hut.get("cost",100)))+cost},true)
	_pay_build(team, {"rawcode":next_raw,"cost":cost})
	hut["income"] = float(hut.get("income",0)) + cost*Economy.rate(self,next_raw)
	hut["interval"] = _hut_interval(hut)
	hut["timer"] = float(hut["interval"])
	hut["progress"] = 0.0
	hut["paused"] = false
	hut["weapon_cd"] = 0.0
	hut.erase("pending_shot")
	var node = hut.get("node")
	if is_instance_valid(node):
		for child in node.get_children(): child.queue_free()
		if node.has_meta("weapon_audio"): node.remove_meta("weapon_audio")
		if node.has_meta("aura_raw"): node.remove_meta("aura_raw")
		if ResourceLoader.exists(entry["building_model"]):
			var scene: PackedScene = BattleAssets.resource(entry["building_model"])
			var model = scene.instantiate()
			node.add_child(model)
			_ground_model(model)
			_play_model_anim(model,next_raw)
		var ratio: float = node.hp/node.max_hp
		node.max_hp = float(fields.get("uhpm",node.max_hp))
		node.hp = ratio*node.max_hp
		node.display_name = name_for(next_raw)
		node.configure_spells(self,next_raw)
	print("[Gameplay] chosen branch ",next_raw," produces ",unit_raw," next=",next_options)
	return true


func destroy_hut(hut: Dictionary) -> void:
	if Net.is_client:
		Net.command("sell",{"id":int(hut.get("node").get_meta("network_id",0))})
		return
	var refund := int(float(hut.get("cost", 100)) * 0.2)
	add_gold(int(hut.get("team",0)),refund)
	var cell = hut.get("cell")
	if cell != null:
		cell["occupied"] = false
	var node = hut.get("node")
	if node != null and is_instance_valid(node):
		node.queue_free()
	huts.erase(hut)
	print("[Gameplay] destroyed own %s" % str(hut.get("name", "")))


func select_tower(index: int) -> void:
	if tower_buildings.is_empty():
		return
	selected_tower = clampi(index, 0, tower_buildings.size() - 1)
	build_mode = true
	if grid:
		grid.set_overlay_visible(true)


func _collect_towers(race_data: Dictionary) -> Array:
	var out: Array = []
	for b in race_data.get("buildings", []):
		var role := str(b.get("role", ""))
		if role.contains("line"):
			continue
		var wf: Dictionary = (GameDB.units.get(str(b.get("rawcode", "")), {}) as Dictionary)
		var ab: Array = []
		for a in str(wf.get("uabi", "")).split(","):
			if not a.is_empty():
				ab.append(a)
		var active := ""
		var tt: Dictionary = tower_traits.get(str(b.get("rawcode", "")), {})
		out.append({
			"rawcode": str(b.get("rawcode", "")),
			"name": str(b.get("name", "")),
			"hotkey": str(b.get("hotkey", "")),
			"cost": maxi(50, int(wf.get("ugol", 150))),
			"building_model": "res://assets/models_lines/%s_b.glb" % str(b.get("rawcode", "")),
			"tower": true,
			"damage": float(tt.get("damage", 0.0)) if not (unit_traits.get(str(b.get("rawcode","")),{}).get("weapons",[]) as Array).is_empty() else 0.0,
			"range": float(tt.get("range", 12.0)),
			"cd": float(tt.get("cd", 1.5)),
			"attack_type": str(tt.get("attack_type", "pierce")),
			"can_air": bool(tt.get("can_air", true)),
			"can_ground": bool(tt.get("can_ground", true)),
			"ability": active,
			"unit_rawcode": "", "unit_model": "",
			"upgrades_to": b.get("upgrades_to", []),
		})
	return out


func chat_command(text: String) -> String:
	var cmd := text.strip_edges().to_lower()
	if cmd.begins_with("-"):
		cmd = cmd.substr(1)
	match cmd:
		"income":
			return "Доход: %.2f зол. каждые 10 с (база 5 + здания %.2f)" % [_income_amount(0),_income_amount(0)-5]
		"gold":
			return "Золото: %d · Дерево: %d · Легендарные места: %d/%d" % [gold,wood,legendary_used(),legendary_limit]
		"kills":
			return "Убийства: %d : %d" % [int(kills[0]), int(kills[1])]
		"ap":
			return "Юнитов: %d, построек: %d" % [units.size(), huts.size()]
		"armor":
			var armor_text := "Атака → броня (% урона):"
			for attack in damage_table:
				armor_text += "\n"+preload("res://scripts/ui/unit_details.gd").damage_help(self, attack).replace("\n", " · ")
			return armor_text
		"help":
			return "Команды: -income, -gold, -kills, -ap, -armor, -help"
		_:
			return "Неизвестная команда: %s" % text


func _build_hut() -> void:
	if game_over or line_buildings.is_empty(): return
	var entry: Dictionary = line_buildings[clampi(selected_line, 0, line_buildings.size()-1)]
	_build_entry_near_keep(0, entry)

func _build_bot_hut() -> void:
	var affordable := bot_line_buildings.filter(func(e): return build_error(e, 1).is_empty())
	if affordable.is_empty(): return
	if _build_entry_near_keep(1, affordable.pick_random()): bot_built += 1

func _build_entry_near_keep(team: int, entry: Dictionary) -> bool:
	var candidates: Array = grid.cells.filter(func(c): return int(c["team"]) == team and not c["occupied"] and not c["reserved"])
	var keep := -5120 if team == 0 else 5120
	candidates.sort_custom(func(a,b): return absf(a["wc3"].x-keep)+absf(a["wc3"].y)*.25 < absf(b["wc3"].x-keep)+absf(b["wc3"].y)*.25)
	return build_at_cell(team,candidates[0],entry) if not candidates.is_empty() else false

func _fit_building_model(inst: Node3D) -> void:
	var aabb := AABB()
	var has := false
	var stack: Array = [inst]
	while not stack.is_empty():
		var n = stack.pop_back()
		if n is MeshInstance3D and n.mesh != null:
			var box: AABB = n.mesh.get_aabb()
			aabb = box if not has else aabb.merge(box)
			has = true
		for c in n.get_children():
			stack.append(c)
	if not has:
		return
	var h: float = maxf(aabb.size.y, 0.001)
	var s: float = BUILDING_HEIGHT / h
	inst.scale = Vector3(s, s, s)
	inst.position.y = -aabb.position.y * s


func _build_producer(team: int, _keep_x: float) -> bool:
	var entries: Array = line_buildings if team == 0 else bot_line_buildings
	var raw: String = building_rawcode if team == 0 else bot_building_raw
	for entry in entries:
		if str(entry["rawcode"]) == raw: return _build_entry_near_keep(team,entry)
	return false

func status_line() -> String:
	var msg := ""
	if game_over:
		msg = "   WINNER: %s" % ("WEST" if winner == 0 else "EAST")
	return "%s — %s   vs   BOT %s — %s\ngold: %d   huts: %d   plots: %d   units: %d\nline: %s   [1-6] build line, [B] first line\ncastle W: %.0f   E: %.0f%s" % [
		race_name, building_name, bot_race_name, bot_building_name,
		gold, built, grid.cell_count(0) if grid else 0, units.size(),
		("%d/%d %s" % [selected_line + 1, line_buildings.size(),
			str((line_buildings[selected_line] as Dictionary).get("name", ""))]) if not line_buildings.is_empty() else "-",
		castles[0]["hp"], castles[1]["hp"], msg
	]

func _play_model_anim(node: Node, raw := "") -> void:
	preload("res://scripts/world/wc3_appearance.gd").prepare(node,-1,raw)
	preload("res://scripts/game/orc_rules.gd").decorate(self,node,raw)
	preload("res://scripts/game/naga_rules.gd").decorate(self,node,raw)
	if raw in ["h0A1","h08X","h08Y"]: node.visible = false
	var stack: Array = [node]
	while not stack.is_empty():
		var n = stack.pop_back()
		if n is AnimationPlayer:
			var ap := n as AnimationPlayer
			preload("res://scripts/game/native_animation.gd").initialize(ap,node.scene_file_path)
			var names := ap.get_animation_list()
			if names.size() > 0:
				var pick := preload("res://scripts/game/native_animation.gd").choose(ap,"Stand",raw)
				if pick.is_empty(): pick = str(names[0])
				ap.play(pick)
				ap.advance(0.0)
			preload("res://scripts/game/corrupted_rules.gd").building_spheres(self,node,raw)
			preload("res://scripts/game/elemental_rules.gd").decorate(self,node,raw)
			return
		for c in n.get_children():
			stack.append(c)

func _ground_model(inst: Node3D) -> void:
	var aabb := AABB()
	var has := false
	var stack: Array = [inst]
	while not stack.is_empty():
		var n = stack.pop_back()
		if n is MeshInstance3D and n.mesh != null:
			var box: AABB = n.mesh.get_aabb()
			aabb = box if not has else aabb.merge(box)
			has = true
		for c in n.get_children():
			stack.append(c)
	if has:
		inst.position.y = -aabb.position.y


func upgrade_hut_variant(hut: Dictionary, index: int) -> void:
	var options := upgrade_options(hut)
	if index >= 0 and index < options.size(): choose_upgrade(hut,str(options[index]))


func nearest_corpse(pos: Vector3, radius: float) -> Dictionary:
	var best := {}
	var best_d := radius
	for c in corpses:
		var d: float = Vector2(c["pos"].x - pos.x, c["pos"].z - pos.z).length()
		if d < best_d:
			best_d = d
			best = c
	return best


func consume_corpse(c: Dictionary) -> void:
	corpses.erase(c)
	var node = c.get("node")
	if is_instance_valid(node):
		node.hide()
		node.queue_free()

func corpse_eligible(unit: GameUnit) -> bool:
	if bool(unit.get_meta("no_corpse",false)): return false
	var data: Dictionary = unit_traits.get(unit.rawcode,{})
	return not unit.illusion and not bool(unit.get_meta("incinerated",false)) and not bool(unit.get_meta("no_bounty",false)) and (int(data.get("death_type",0))&1)!=0 and not str(data.get("unit_types","")).to_lower().contains("mechanical")


func spawn_gallery() -> void:
	# Dev: line up every line unit (all tiers) for a visual audit.
	var x := -5400.0
	var z := -600.0
	var col := 0
	var rows := 0
	var gallery_entries: Array = []
	for entry in line_buildings:
		gallery_entries.append(entry)
	# Also include every upgrade tier reachable from this race's lines.
	for entry in line_buildings:
		var chain: Array = []
		var u0 = entry.get("upgrades_to", [])
		if u0 is Array:
			chain = (u0 as Array).duplicate()
		var guard := 0
		while not chain.is_empty() and guard < 8:
			guard += 1
			var raw := str(chain.pop_front())
			var nf: Dictionary = GameDB.units.get(raw, {})
			var tu := str(nf.get("utra", "")).split(",")[0]
			if tu.is_empty():
				continue
			var tn := str(nf.get("unam", tu))
			if tn.is_empty():
				tn = tu
			gallery_entries.append({
				"rawcode": raw, "name": tn, "unit_rawcode": tu, "unit_name": tn,
				"unit_model": "res://assets/models_lines/%s_u.glb" % tu,
			})
			var nxt: Array = []
			for rr in line_buildings:
				if str(rr.get("rawcode", "")) == raw:
					nxt = (rr.get("upgrades_to", []) as Array).duplicate()
			for xv in nxt:
				chain.append(xv)
	for entry in gallery_entries:
		var u_raw := str(entry.get("unit_rawcode", "hfoo"))
		var u_model := str(entry.get("unit_model", ""))
		var traits: Dictionary = (unit_traits.get(u_raw, {}) as Dictionary).duplicate()
		var unit := GameUnitScript.new()
		unit.name = "gallery_%s" % u_raw
		add_child(unit)
		unit.setup(0, str(entry.get("unit_name", u_raw)).to_lower(), x, z, 0.0, self, terrain, true, u_model, traits)
		unit.rawcode = u_raw
		units.append(unit)
		col += 1
		if col >= 8:
			col = 0
			rows += 1
			x = -5400.0
			z += 110.0
		else:
			x += 110.0
		if rows >= 14:
			break
	print("[Gallery] spawned %d units" % units.size())
