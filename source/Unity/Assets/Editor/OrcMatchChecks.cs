using System;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEngine;
public static class OrcMatchChecks {
    public static void Run() {
        var checks=new JArray();Action<bool,string> check=(passed,name)=>{checks.Add(new JObject{["check"]=name,["passed"]=passed});if(!passed)throw new Exception("Orc stage one: "+name);};
        var c=JObject.Parse(File.ReadAllText("Assets/Resources/pc-orcs.json"));var m=new HumanMatch(c,1000,1);
        check(m.Lines.Length==19&&m.BaseKinds.Length==11&&m.Lines.Count(l=>l.Producer)==14,"staged catalog has eleven base buildings eight upgrades and fourteen unit types");
        check(m.Lines[0].unit=="o002"&&m.Lines[0].cost==110&&m.Lines[1].unit=="o007"&&m.Lines[1].cost==260,"source Grunt and Kodo lines and prices");
        check(m.Lines[m.Kind("h029")].upgrades.SequenceEqual(new[]{"h02U"})&&m.Lines[m.Kind("h02U")].upgrades.SequenceEqual(new[]{"h031"})&&m.Aspects.All(a=>a==-1),"only the completed Grunt upgrade chain is available; aspects remain disabled");
        check(m.TryBuild(0,0,7,15,out _),"Orc builder accepts a building order");var h=m.Huts.Single();
        check(m.Gold[0]==1000&&Math.Abs(m.AvailableGold(0)-890)<.01f,"reservation does not charge before arrival");
        var start=m.Builders[0].position;m.Step(.05f);check(Vector2.Distance(start,m.Builders[0].position)>0&&!h.started,"builder physically travels to the plot");
        for(int i=0;i<500&&!h.started;i++)m.Step(.05f);
        check(h.started&&!h.paid&&h.buildLeft>0&&m.Gold[0]==1000,"arrival starts construction while the cost remains reserved");
        check(m.MoveBuilder(0,m.Builders[0].position+Vector2.up*4),"builder can interrupt construction");float left=h.buildLeft;
        for(int i=0;i<20;i++)m.Step(.05f);check(Math.Abs(left-h.buildLeft)<.001f,"interrupted construction waits for a builder");
        check(m.ResumeBuilding(0,h.id),"builder resumes the unfinished building");for(int i=0;i<500&&h.buildLeft>0;i++)m.Step(.05f);
        check(h.buildLeft<=0&&m.Huts.Contains(h),"resumed Grunt hall completes");
        check(m.Wood[0]==110&&Math.Abs(m.Income(0)-7.2f)<.01f,"source wood reward and producer income");
        check(Math.Abs(m.Interval(h)-20.5f)<.001f,"Grunt production interval derives from invested gold");
        check(!m.TryBuild(0,1,7,15,out _),"occupied plot cannot charge twice");
        check(!m.TryBuild(0,2,9,15,out _)&&m.Wood[0]==110,"treasure chest rejects insufficient wood");
        m.Gold[0]=1000;check(m.TryBuild(0,1,9,15,out _),"Kodo farm can be ordered");
        for(int i=0;i<800;i++)m.Step(.05f);
        check(m.Units.Any(u=>u.team==0&&m.Lines[u.kind].unit=="o002")&&m.Units.Any(u=>u.team==0&&m.Lines[u.kind].unit=="o007"),"both Orc producers release the correct unit");
        check(m.Huts.Any(v=>v.team==1)&&m.Units.Any(u=>u.team==1),"the bot builds and produces from its Orc catalog");
        var farm=m.Huts.First(v=>v.team==0&&v.kind==1);float before=farm.spawnLeft;check(m.PauseHut(0,farm.id,true),"Kodo production can pause");for(int i=0;i<10;i++)m.Step(.05f);check(Math.Abs(before-farm.spawnLeft)<.001f,"paused Kodo farm preserves its production timer");m.PauseHut(0,farm.id,false);
        for(int i=0;i<4000&&!m.Finished;i++)m.Step(.05f);check(m.Kills>0,"Orc units engage and kill through shared combat rules");
        CheckUpgrades(c,check);CheckCombat(c,check);CheckTrolls(c,check);OrcShamanChecks.Run(c,check);OrcKodoChecks.Run(c,check);OrcCompletionChecks.Run(c,check);
        var output=Path.GetFullPath("../Validation/PC/orc-simulation-verification.json");File.WriteAllText(output,new JObject{["status"]="PASS",["checks"]=checks,["stage"]=6}.ToString());Debug.Log("CF_ORC_SIMULATION_PASS checks="+checks.Count);
    }
    static HumanMatch Fresh(JObject c){var m=new HumanMatch((JObject)c.DeepClone(),100000);m.Gold[1]=0;return m;}
    static void CheckUpgrades(JObject c,Action<bool,string> check) {
        var m=Fresh(c);m.TryBuild(0,m.Kind("h029"),7,15,out _);var h=m.Huts.Single();m.Builders[0].position=h.position-new Vector2(0,2.4f);for(int i=0;i<40&&!h.paid;i++)m.Step(.05f);
        int warrior=m.Kind("h02U"),chief=m.Kind("h031");float gold=m.Gold[0];
        check(!m.Upgrade(0,h.id,chief,out _)&&m.Gold[0]==gold,"cannot skip Warrior upgrade or spend on an invalid upgrade");
        check(!m.TryBuild(0,warrior,8,15,out _),"upgrade buildings cannot be built directly");
        h.hp=h.maxHp*.43f;h.paused=true;check(m.Upgrade(0,h.id,warrior,out _),"Grunt hall upgrades to Warrior hall");
        check(m.Gold[0]==gold-100&&m.Wood[0]==210&&h.spent==210&&h.baseCost==110,"Warrior upgrade charges only the incremental cost and preserves the base price");
        check(Math.Abs(h.hp/h.maxHp-.43f)<.0001f&&!h.paused&&Math.Abs(h.spawnLeft-25.5f)<.001f&&Math.Abs(m.Income(0)-9.2f)<.001f,"Warrior upgrade preserves health ratio and updates income and production");
        check(m.Upgrade(0,h.id,chief,out _),"Warrior hall upgrades to Chieftain hall");
        check(m.Gold[0]==gold-340&&m.Wood[0]==450&&h.spent==450&&Math.Abs(h.spawnLeft-37.5f)<.001f&&Math.Abs(m.Income(0)-14)<.001f,"Chieftain production uses all 450 invested gold and the exact incremental income");
        check(Math.Abs(h.hp/h.maxHp-.43f)<.0001f,"Chieftain upgrade preserves the damaged building health ratio");
        for(int i=0;i<760;i++)m.Step(.05f);check(m.Units.Any(u=>u.team==0&&u.kind==chief),"upgraded hall produces a Chieftain rather than a stale Grunt");
        gold=m.Gold[0];check(!m.Upgrade(0,h.id,warrior,out _)&&m.Gold[0]==gold,"invalid downgrade does not charge resources");
        check(m.Sell(0,h.id)&&Math.Abs(m.Gold[0]-gold-22)<.001f,"sale still refunds only 20 percent of the original building price");
        var bot=Fresh(c);bot.Gold[1]=100000;bot.Wood[1]=100000;for(int i=0;i<2500&&!bot.Finished;i++)bot.Step(.05f);check(bot.Huts.Any(b=>b.team==1&&!bot.Lines[b.kind].basic),"Orc bot uses the newly available upgrade chain");
    }
    static HumanMatch.Unit Unit(HumanMatch m,int team,string building,Vector2 p,float hp=10000){var u=m.Spawn(team,m.Kind(building),p);u.onRoad=true;u.hp=u.maxHp=hp;u.cooldown=10000;return u;}
    static void Strike(HumanMatch m,HumanMatch.Unit source,HumanMatch.Unit target){source.cooldown=0;source.combatTarget=target.id;source.strikeLeft=-1;m.TickUnit(source,.01f);m.TickUnit(source,1);}
    static void CheckCombat(JObject c,Action<bool,string> check) {
        var m=Fresh(c);var warrior=Unit(m,0,"h02U",Vector2.zero);var chief=Unit(m,0,"h031",Vector2.zero);var victim=Unit(m,1,"h029",new Vector2(2,0),1000000);
        check(warrior.critChance==.25f&&warrior.critMultiplier==2&&chief.critChance==.3f&&chief.critMultiplier==3&&chief.abilities.Contains("A02P"),"source critical profiles and Pulverize are granted to the right unit tier");
        chief.abilities.Remove("A02P");
        foreach(var source in new[]{warrior,chief}) {int criticals=0;bool valid=true;var weapon=m.Lines[source.kind].weapons[0];for(int i=0;i<1000;i++){source.cooldown=0;source.strikeLeft=-1;source.combatTarget=victim.id;m.TickUnit(source,.01f);if(Math.Abs(source.shotDamage-weapon.damage*source.critMultiplier)<.01f)criticals++;else valid&=Math.Abs(source.shotDamage-weapon.damage)<.01f;}
            check(valid,"critical and ordinary attacks use their respective damage multipliers");
            check(Math.Abs(criticals/1000f-source.critChance)<.05f,"seeded attack distribution matches source critical chance for "+m.Lines[source.kind].unit);
            source.critChance=1;float hp=victim.hp;Strike(m,source,victim);float expected=weapon.damage*source.critMultiplier*m.Multiplier(weapon.attack,m.Lines[victim.kind].armorType)*HumanMatch.ArmorFactor(victim.armor);check(Math.Abs(hp-victim.hp-expected)<.1f,"critical weapon damage reaches the target after physical armor");
        }
        var data=(JObject)c.DeepClone();data["abilities"]["A02P"]["chance"]=1;((JObject)data["lines"][1]["stats"])["flying"]=true;
        m=Fresh(data);chief=Unit(m,0,"h031",Vector2.zero);chief.critChance=0;victim=Unit(m,1,"h029",new Vector2(2,0));var near=Unit(m,1,"h029",new Vector2(2,2));near.spellResist=1;
        var ally=Unit(m,0,"h029",new Vector2(2,-2));var air=Unit(m,1,"h02K",new Vector2(2,1));var outside=Unit(m,1,"h029",new Vector2(2,7));
        float primary=victim.hp,adjacent=near.hp;Strike(m,chief,victim);float armor=HumanMatch.ArmorFactor(near.armor)*m.Multiplier("normal",m.Lines[near.kind].armorType);
        check(Math.Abs(adjacent-near.hp-130*armor)<.01f,"Pulverize deals armor-adjusted physical damage even against magic immunity");
        check(Math.Abs(primary-victim.hp-(75+130)*armor)<.01f,"Pulverize includes the primary target in addition to the ordinary hit");
        check(ally.hp==10000&&air.hp==10000&&outside.hp==10000,"Pulverize excludes allies, flying units, and targets outside the source radius");
        check(m.PulverizeProcs==1,"secondary area damage does not recursively trigger Pulverize");
        near.hp=10000;victim.hp=1;Strike(m,chief,victim);check(victim.hp==0&&near.hp<10000,"a lethal primary hit still triggers area damage around its impact");
        victim.hp=10000;victim.evasion=1;int procs=m.PulverizeProcs;near.hp=10000;Strike(m,chief,victim);check(m.PulverizeProcs==procs&&near.hp==10000,"an evaded attack does not trigger Pulverize");
        victim.evasion=0;chief.attackTarget=victim.id;chief.pendingWeapon=m.Lines[chief.kind].weapons[0];chief.shotDamage=75;chief.strikeLeft=0;victim.position=new Vector2(100,0);m.TickUnit(chief,.01f);check(m.PulverizeProcs==procs,"an interrupted out-of-range hit does not trigger Pulverize");
        m=Fresh(data);chief=Unit(m,0,"h031",HumanMatch.CastlePosition(1)-Vector2.right*5);near=Unit(m,1,"h029",HumanMatch.CastlePosition(1)+Vector2.up*2);
        chief.attackTarget=-1;chief.pendingWeapon=m.Lines[chief.kind].weapons[0];chief.shotDamage=75;chief.strikeLeft=0;m.TickUnit(chief,.01f);check(m.PulverizeProcs==1&&near.hp<10000,"a hit on the castle can trigger Pulverize against nearby units");
        data["abilities"]["A02P"]["half_area"]=10;m=Fresh(data);chief=Unit(m,0,"h031",Vector2.zero);chief.critChance=0;victim=Unit(m,1,"h029",new Vector2(2,0));near=Unit(m,1,"h029",new Vector2(2,8));adjacent=near.hp;Strike(m,chief,victim);check(Math.Abs(adjacent-near.hp-65*armor)<.01f,"configured outer radius applies half damage without changing the native equal-radius profile");
        data["abilities"]["A02P"]["chance"]=0;m=Fresh(data);chief=Unit(m,0,"h031",Vector2.zero);chief.critChance=0;victim=Unit(m,1,"h029",new Vector2(2,0));Strike(m,chief,victim);check(m.PulverizeProcs==0,"zero proc chance never triggers");
        m=Fresh(c);chief=Unit(m,0,"h031",Vector2.zero,1000000);chief.critChance=0;victim=Unit(m,1,"h029",new Vector2(2,0),1000000);for(int i=0;i<1000;i++)Strike(m,chief,victim);check(Math.Abs(m.PulverizeProcs/1000f-.35f)<.05f,"seeded hit distribution matches the native 35 percent Pulverize chance");
    }
    static void CheckTrolls(JObject c,Action<bool,string> check) {
        var m=Fresh(c);m.Wood[0]=2000;int basic=m.Kind("h02H"),trapper=m.Kind("h05E"),leader=m.Kind("h06D");
        check(m.Lines[basic].cost==200&&m.Lines[trapper].cost==80&&m.Lines[leader].cost==490&&m.Lines[leader].woodCost==390,"Troll chain uses exact Godot incremental gold and wood costs");
        check(m.TryBuild(0,basic,7,15,out _),"Troll hut is a new initial construction option");var h=m.Huts.Single();m.Builders[0].position=h.position-new Vector2(0,2.4f);for(int i=0;i<40&&!h.paid;i++)m.Step(.05f);
        check(h.paid&&h.spent==200&&m.Wood[0]==2200&&Math.Abs(m.Interval(h)-25)<.001f,"Troll hut completes and grants wood with a 25 second production cycle");
        check(!m.Upgrade(0,h.id,leader,out _),"Troll leader cannot skip the trapper upgrade");
        check(m.Upgrade(0,h.id,trapper,out _)&&h.spent==280&&m.Wood[0]==2280&&Math.Abs(h.spawnLeft-29)<.001f,"Trapper upgrade preserves cumulative 280 investment and grants 80 wood");
        h.hp=h.maxHp*.4f;m.Wood[0]=389;check(!m.Upgrade(0,h.id,leader,out _),"legendary Troll leader rejects insufficient wood without using its wood reward in advance");m.Wood[0]=2000;
        check(m.Upgrade(0,h.id,leader,out _)&&h.spent==770&&m.Wood[0]==1733&&m.LegendaryUsed(0)==1&&Math.Abs(h.spawnLeft-53.5f)<.001f,"legendary Troll upgrade costs 390 wood grants rounded 25 percent reward and occupies one slot");
        check(Math.Abs(h.hp/h.maxHp-.4f)<.001f&&Math.Abs(m.Income(0)-20.4f)<.001f,"Troll upgrades preserve health ratio and cumulative income");
        var legendary=Unit(m,0,"h06D",Vector2.zero);check(legendary.maxMana==0&&Math.Abs(legendary.spellResist-.7f)<.0001f&&legendary.critChance==.15f&&legendary.critMultiplier==2,"Troll leader receives source legendary resistance and critical strike");
        float gold=m.Gold[0];check(m.Sell(0,h.id)&&m.Gold[0]==gold+40&&m.LegendaryUsed(0)==0,"legendary Troll sale releases its slot and refunds 20 percent of base cost");
        m=Fresh(c);foreach(var line in m.Lines){line.speed=0;line.regen=0;}var troll=Unit(m,0,"h02H",Vector2.zero);var victim=Unit(m,1,"h029",new Vector2(5,0),1);Strike(m,troll,victim);
        check(m.OrcFrenzies==0&&victim.hp==1,"Troll frenzy waits for missile impact and a real kill");for(int i=0;i<40&&victim.hp>0;i++)m.Step(.05f);
        check(m.OrcFrenzies==1&&troll.kills==1&&Math.Abs(m.AttackSpeed(troll)-3)<.001f,"Troll projectile kill grants 200 percent attack speed frenzy");
        m.Lines[troll.kind].speed=9;check(Math.Abs(m.MoveSpeed(troll)-11.25f)<.001f,"Troll frenzy grants 25 percent movement speed");
        float hp=troll.hp;m.DamageUnit(troll,100,1,"chaos",false);check(Math.Abs(hp-troll.hp-110*m.Multiplier("chaos",m.Lines[troll.kind].armorType)*HumanMatch.ArmorFactor(troll.armor))<.01f,"frenzied Troll takes 10 percent extra incoming damage");
        var second=Unit(m,1,"h029",new Vector2(5,1),1);m.DamageUnit(second,1000,0,"chaos",true,true,troll);check(m.OrcFrenzies==2&&troll.effects.Count(e=>e.id=="A02I")==1&&Math.Abs(troll.effects.Single(e=>e.id=="A02I").left-6)<.001f,"frenzy refreshes six seconds without stacking");
        m.Lines[0].stats["unit_types"]="mechanical";var nonSapper=Unit(m,1,"h029",new Vector2(5,2),1);m.DamageUnit(nonSapper,1000,0,"chaos",true,true,troll);check(m.OrcFrenzies==2,"killing a non-sapper does not refresh frenzy");
        for(int i=0;i<122;i++)m.Step(.05f);check(!troll.effects.Any(e=>e.id=="A02I")&&Math.Abs(m.AttackSpeed(troll)-1)<.001f,"frenzy expires and restores normal speed");
        foreach(string raw in new[]{"h05E","h06D"}) {
            var data=(JObject)c.DeepClone();data["lines"][1]["stats"]["flying"]=true;data["lines"][1]["stats"]["unit_types"]="sapper,ward";data["lines"][1]["stats"]["fly_height"]=8;
            m=Fresh(data);foreach(var line in m.Lines){line.speed=0;line.regen=0;}var caster=Unit(m,0,raw,Vector2.zero);var targets=Enumerable.Range(0,7).Select(i=>Unit(m,1,"h02K",new Vector2(15,i))).ToArray();targets[0].spellResist=1;
            string id=raw=="h05E"?"A06I":"A0FB";int count=raw=="h05E"?2:5;
            check(m.Cast(caster,id,targets[0].id)&&m.OrcNetShots==count&&targets.All(u=>m.Flying(u)),"nets launch the correct count without grounding before impact "+raw);
            check(!m.Cast(caster,id,targets[1].id)&&Math.Abs(caster.spellTimers[id]-8)<.001f,"net cast has an eight second cooldown and cannot be repeated immediately");caster.hp=0;
            for(int i=0;i<12;i++)m.Step(.05f);
            check(m.OrcNetHits==count&&targets.Count(m.Rooted)==count&&!m.Flying(targets[0])&&m.Height(targets[0])==0,"released nets survive caster death and ground magic-immune organic targets on impact");
            check(targets.Where(m.Rooted).All(u=>u.hp==10000)&&targets.All(u=>u.stun==0),"nets are physical control without damage or stun");
            var caught=targets[0];m.Lines[1].speed=9;var point=caught.position;for(int i=0;i<10;i++)m.Step(.05f);check(caught.position==point&&m.MoveSpeed(caught)==0,"netted targets cannot move while still being allowed to act");
            var ground=Unit(m,0,"h029",caught.position-Vector2.right);check(m.WeaponFor(ground,caught)!=null,"ground-only weapons can attack a netted flying target");
            foreach(var line in m.Lines)line.speed=0;ground.cooldown=10000;for(int i=0;i<330;i++)m.Step(.05f);check(!m.Rooted(caught)&&m.Flying(caught)&&m.Height(caught)==8,"net expiration restores flight and clears immobilization");
            m=Fresh(data);caster=Unit(m,0,raw,Vector2.zero);var invalid=Unit(m,1,"h029",Vector2.right*5);check(!m.Cast(caster,id,invalid.id),"net excludes ground targets");var far=Unit(m,1,"h02K",Vector2.right*21);check(!m.Cast(caster,id,far.id),"initial net target respects 600 source unit cast range");
            var mechanical=Unit(m,1,"h02K",Vector2.right*5);m.Lines[1].stats["unit_types"]="mechanical,sapper";check(!m.Cast(caster,id,mechanical.id),"net excludes mechanical flyers");m.Lines[1].stats["unit_types"]="sapper";
            m.Lines[1].armorType="hero";check(m.Cast(caster,id,mechanical.id),"net accepts an organic airborne hero");caster.hp=0;for(int i=0;i<7;i++)m.Step(.05f);check(mechanical.effects.Any(e=>e.root&&e.left<3.14f),"net applies the shortened native hero duration");for(int i=0;i<70;i++)m.Step(.05f);check(!m.Rooted(mechanical),"hero net expires after about 3.13 seconds");
        }
    }
}
