extends SceneTree

func _initialize():
	call_deferred("verify")

func verify() -> void:
	var settings = root.get_node("Settings")
	settings.selected_race = "Human Alliance"
	settings.bot_race = "Orcish Horde"
	settings.start_gold = 10000
	settings.aspects_enabled = false
	root.get_node("Match").begin()
	var main: Node = load("res://scenes/main.tscn").instantiate()
	root.add_child(main)
	current_scene = main
	await process_frame
	var game: Node = main.gameplay
	game.set_process(false)
	var entries: Array = game._collect_lines(game.race)
	var cells: Array = game.grid.cells.filter(func(c): return c.team==0 and not c.occupied and not c.reserved)
	assert(game.build_at_cell(0,cells[0],entries[0]))
	assert(game.build_at_cell(0,cells[1],entries[1]))
	var a: Dictionary = game.huts[-2]
	var b: Dictionary = game.huts[-1]
	a["timer"]=1.0
	b["timer"]=3.0
	assert(game.synchronize_production(1)==0)
	assert(game.synchronize_production(0)==2)
	assert(a.timer==3.0 and b.timer==3.0)
	var before: int = game.units.size()
	b["paused"]=true
	game._tick_production_waves(1.0)
	assert(a.timer==3.0 and b.timer==3.0)
	b["paused"]=false
	game._tick_production_waves(1.0)
	assert(a.timer==2.0 and b.timer==2.0 and game.units.size()==before)
	game._tick_production_waves(2.0)
	assert(a.timer==a.interval and b.timer==b.interval and not a.has("sync_wave") and not b.has("sync_wave"))
	assert(game.units.size()>=before+2)
	print("SQUAD045_GAMEPLAY_PASS")
	quit()
