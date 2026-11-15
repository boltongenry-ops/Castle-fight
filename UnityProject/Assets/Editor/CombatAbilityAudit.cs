using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

public static class CombatAbilityAudit {
    static JArray results;
    static JObject human,nerub;
    static object Call(HumanMatch m,string method,params object[] args)=>typeof(HumanMatch).GetMethod(method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(m,args);
    static void Check(bool value,string description){results.Add(new JObject{["pass"]=value,["check"]=description});if(!value)throw new Exception(description);}
    static void Case(string name,Action action){try{action();results.Add(new JObject{["case"]=name,["status"]="PASS"});}catch(Exception e){while(e.InnerException!=null)e=e.InnerException;results.Add(new JObject{["case"]=name,["status"]="FAIL",["error"]=e.Message});Debug.LogWarning("COMBAT_AUDIT_FAIL "+name+": "+e.Message);}}
    static HumanMatch Human(){var m=new HumanMatch((JObject)human.DeepClone(),0,0);foreach(var l in m.Lines){l.speed=0;l.regen=0;}return m;}
    static HumanMatch.Unit Spawn(HumanMatch m,string raw,int team,Vector2 position){int kind=m.Kind(raw);if(kind<0)kind=Array.FindIndex(m.Lines,l=>l.unit==raw);var u=m.Spawn(team,kind,position);u.onRoad=true;u.cooldown=1000;return u;}
    static HumanMatch.Unit Corpse(HumanMatch m,int team=0,float distance=2){var u=Spawn(m,"h000",team,new Vector2(distance,0));m.DamageUnit(u,100000,1-team,"chaos",true,true);return u;}
    static HumanMatch.Unit Paladin(HumanMatch m,float mana=200){var u=Spawn(m,"h038",0,Vector2.zero);u.mana=mana;return u;}
    static HumanMatch.Unit Wounded(HumanMatch m){var u=Spawn(m,"h000",0,Vector2.one);u.hp=50;return u;}
    static void PaladinCases(){
        Case("paladin_heal_then_resurrection",()=>{var m=Human();var p=Paladin(m);var a=Wounded(m);Corpse(m);Check(m.Cast(p,"A03K",a.id),"Paladin healing casts");Call(m,"TickHumanJobs",.01f);Check(m.Resurrections==0,"Resurrection is delayed");Check(Math.Abs(p.mana-134)<.01,"Healing and inner fire consume 66 mana");Call(m,"TickHumanJobs",1f);Check(m.Resurrections==1&&m.Corpses.Count==0,"Delayed resurrection consumes one friendly corpse");Check(Math.Abs(p.mana-64)<.01&&Math.Abs(p.spellTimers["A03H"]-40)<.01,"Resurrection costs 70 mana and starts 40-second cooldown");});
        Case("paladin_without_wounded_ally",()=>{var m=Human();var p=Paladin(m);Corpse(m);Call(m,"TryAutocast",p);Call(m,"TickHumanJobs",1.1f);Check(m.Resurrections==0&&m.Corpses.Count==1,"A corpse alone does not trigger followup-only resurrection");});
        Case("paladin_insufficient_mana",()=>{var m=Human();var p=Paladin(m,100);var a=Wounded(m);Corpse(m);Check(m.Cast(p,"A03K",a.id),"Low-mana paladin still heals");Call(m,"TickHumanJobs",.01f);Call(m,"TickHumanJobs",1f);Check(m.Resurrections==0&&m.Corpses.Count==1&&Math.Abs(p.mana-34)<.01,"After heal and inner fire, less than 70 mana blocks resurrection without spending it");});
        Case("paladin_corpse_range_and_team",()=>{var m=Human();var p=Paladin(m);var a=Wounded(m);Corpse(m,1);Corpse(m,0,31);m.Cast(p,"A03K",a.id);Call(m,"TickHumanJobs",.01f);Call(m,"TickHumanJobs",1f);Check(m.Resurrections==0,"Enemy or more-than-900-distance corpses are not resurrected");});
        Case("paladin_caster_dies",()=>{var m=Human();var p=Paladin(m);var a=Wounded(m);Corpse(m);m.Cast(p,"A03K",a.id);m.DamageUnit(p,100000,1,"chaos",true,true);Call(m,"TickHumanJobs",1.1f);Check(m.Resurrections==0,"Dead paladin cannot complete the delayed resurrection");});
        Case("paladin_resurrection_cooldown",()=>{var m=Human();var p=Paladin(m);var a=Wounded(m);Corpse(m);m.Cast(p,"A03K",a.id);Call(m,"TickHumanJobs",.01f);Call(m,"TickHumanJobs",1f);Corpse(m);p.mana=300;Check(!m.Cast(p,"A03H")&&m.Resurrections==1,"Resurrection cannot bypass its active cooldown");});
        Case("paladin_normal_autocast",()=>{var m=Human();var p=Paladin(m);Wounded(m);Corpse(m);for(int i=0;i<40;i++)m.Step(.05f);Check(m.Resurrections==1,"Ordinary simulation Step/autocast actually resurrects an ally");});
        Case("paladin_sustained_healing",()=>{var m=Human();var p=Paladin(m);var a=Wounded(m);for(int j=0;j<4;j++)Corpse(m,0,2+j);for(int i=0;i<2400;i++){a.hp=Math.Min(a.hp,50);m.Step(.05f);}results.Add(new JObject{["measurement"]="paladin_120s_continuous_wounded_ally",["resurrections"]=m.Resurrections,["remainingMana"]=p.mana,["spells"]=m.SpellsCast});});
    }

    static HumanMatch Nerub(){return new HumanMatch((JObject)nerub.DeepClone(),100000,0,-1,-1,(JObject)human.DeepClone());}
    static HumanMatch.Unit Enemy(HumanMatch m,Vector2? point=null){var u=Spawn(m,"h000",1,point??new Vector2(2,0));u.hp=u.maxHp=10000;u.armor=u.spellResist=0;m.Lines[u.kind].armorType="normal";return u;}
    static void NerubCases(){
        Case("matriarch_actual_upgrade_and_production",()=>{
            var m=Nerub();typeof(HumanMatch).GetField("botAge",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(m,100000f);
            Check(!m.TryBuild(0,m.Kind("hNO1"),17,14,out _),"Matriarch cannot be directly constructed");
            Check(m.TryBuild(0,m.Kind("hNS1"),17,14,out var error),"Cocoon Shrine construction starts: "+error);var hut=m.Huts.Last();m.Builders[0].position=hut.position+new Vector2(0,-Math.Sign(hut.position.y)*2.4f);
            for(int i=0;i<50&&!hut.paid;i++)m.Step(.05f);Check(hut.paid,"Cocoon Shrine finishes construction");
            Check(m.Upgrade(0,hut.id,m.Kind("hNS2"),out error),"Shrine upgrades to Temple of Depths: "+error);
            m.Wood[0]=0;Check(!m.Upgrade(0,hut.id,m.Kind("hNO1"),out _),"Matriarch upgrade requires lumber");m.Wood[0]=300;
            Check(m.Upgrade(0,hut.id,m.Kind("hNO1"),out error),"Temple upgrades to Matriarch Court: "+error);
            Check(hut.spent==520&&Math.Abs(m.Interval(hut)-41)<.001&&m.LegendaryUsed(0)==1,"Matriarch chain costs 520 gold, has 41-second production and uses one legendary slot");
            hut.spawnLeft=0;m.Step(.05f);Check(m.Units.Any(u=>u.team==0&&m.Lines[u.kind].unit=="nN0D"),"Upgraded building actually produces a Matriarch");
        });
        Case("matriarch_command",()=>{var m=Nerub();var queen=Spawn(m,"nN0D",0,Vector2.zero);for(int i=0;i<6;i++)Spawn(m,"nN01",0,new Vector2(i+1,0));Enemy(m);queen.mana=60;Check(m.Cast(queen,"AUC2"),"Matriarch command casts with nearby enemy");Check(m.Units.Count(u=>u.effects.Any(e=>e.id=="ANCF"))==4&&queen.mana==0,"Command affects at most four allies and costs 60 mana");Check(m.Units.Where(u=>u.effects.Any(e=>e.id=="ANCF")).All(u=>Math.Abs(m.Stat(u,"haste")-.2f)<.001),"Command grants 20% attack speed");});
        Case("nerubian_cocoon_heals_spiderlings",()=>{var m=Nerub();var nurse=Spawn(m,"nN0A",0,Vector2.zero);nurse.mana=45;var guard=Spawn(m,"nN01",0,new Vector2(2,0));guard.hp=10;var children=new List<HumanMatch.Unit>();for(int i=0;i<3;i++){var c=Spawn(m,"nN04",0,new Vector2(2,i+1));c.hp=40;children.Add(c);}Check(m.Cast(nurse,"ANH2",guard.id),"Cleansing cocoon casts");Check(children.Count(c=>Math.Abs(c.hp-65)<.01)==2,"Nurse heals exactly two nearby spiderlings for 25");});
        Case("nerubian_colossus_fissure",()=>{var m=Nerub();var giant=Spawn(m,"nN03",0,Vector2.zero);var targets=Enumerable.Range(0,5).Select(i=>Enemy(m,new Vector2(2,i*.5f))).ToArray();Check(m.Cast(giant,"ANFH",targets[0].id),"Colossus fissure casts");Check(targets.Count(u=>Math.Abs(u.hp-9960)<.01)==3&&targets.Count(u=>m.Rooted(u))==3,"Fissure hits only three targets for 40 and roots them");});
        Case("nerubian_hunt_shared_limit",()=>{var m=Nerub();var a=Spawn(m,"nN05",0,Vector2.zero);var b=Spawn(m,"nN05",0,Vector2.one);var victim=Enemy(m);Call(m,"NerubAfterWeapon",a,null,victim,1f);Call(m,"NerubAfterWeapon",b,null,victim,1f);Check(m.NerubHunts==1&&Math.Abs(victim.hp-9990)<.01,"Two attackers share one hunt proc per victim per second");m.Elapsed=1;Call(m,"NerubAfterWeapon",b,null,victim,1f);Check(m.NerubHunts==2,"Hunt can proc again after one second");});
        Case("nerubian_neurotoxin_scope",()=>{var m=Nerub();m.Aspects[0]=1;var u=Enemy(m);m.ApplyEffect(u,"AUAc",0);m.ApplyEffect(u,"AUPs",0);Check(Math.Abs(HumanMatch.F(u.effects.Single(e=>e.id=="AUAc").config,"dps")-4)<.01&&Math.Abs(HumanMatch.F(u.effects.Single(e=>e.id=="AUPs").config,"dps")-10)<.01,"Neurotoxin does not amplify acid or plague without slowing poison");});
        Case("nerubian_magic_immunity_and_poison",()=>{var m=Nerub();var u=Enemy(m);u.spellResist=1;Check(!(bool)Call(m,"Valid",u,0,m.Abilities["AUN5"],false),"Magic-immune enemies reject poison through targeting validation");});
        Case("nerubian_aura_nonstacking",()=>{var m=Nerub();Spawn(m,"nN0D",0,Vector2.zero);Spawn(m,"nN0D",0,Vector2.one);var guard=Spawn(m,"nN01",0,new Vector2(2,0));Call(m,"UpdateAuras");Check(Math.Abs(guard.auraArmor-2)<.01,"Two Matriarch armor auras do not stack");});

        Case("plague_death_heals_allies",()=>{var m=Nerub();var a=Spawn(m,"nN01",0,Vector2.zero);var b=Spawn(m,"nN01",0,Vector2.one);a.hp=b.hp=100;var target=Enemy(m);m.ApplyEffect(target,"AUPs",0);m.DamageUnit(target,100000,0,"chaos",true,true);Check(Math.Abs(a.hp-130)<.01&&Math.Abs(b.hp-130)<.01,"Death under an active plague heals two allies for 30 each");});
        Case("plague_final_tick_death_healing",()=>{var m=Nerub();var a=Spawn(m,"nN01",0,Vector2.zero);var b=Spawn(m,"nN01",0,Vector2.one);a.hp=b.hp=100;var target=Enemy(m);target.hp=60;m.ApplyEffect(target,"AUPs",0);for(int i=0;i<6&&target.hp>0;i++)Call(m,"TickEffects",target,1f);Check(target.hp==0,"Plague kills a 60-HP target on its sixth damage tick");results.Add(new JObject{["measurement"]="plague_final_tick_healing",["allyA"]=a.hp,["allyB"]=b.hp,["queuedSpiders"]=m.NerubBirths.Count});Check(Math.Abs(a.hp-130)<.01&&Math.Abs(b.hp-130)<.01,"Plague final-tick death must retain its described death healing");});
        Case("resurrection_preserves_temporary_spider_semantics",()=>{var m=Nerub();var spider=Spawn(m,"nN04",0,Vector2.zero);spider.summoned=spider.noBounty=true;spider.summonLeft=12;m.DamageUnit(spider,100000,1,"chaos",true,true);int count=m.Raise(0,Vector2.zero,30,1,false);var raised=m.Units.Last();results.Add(new JObject{["measurement"]="raised_spider",["resurrected"]=count,["summoned"]=raised.summoned,["lifeRemaining"]=raised.summonLeft,["noBounty"]=raised.noBounty});Check(count==0||raised.summoned&&raised.noBounty&&raised.summonLeft>0,"Resurrection must not turn a temporary spiderling into a permanent bounty-bearing unit");});
    }
    static int Cost(JObject data,string raw){var rows=(JArray)data["lines"];var row=rows.Single(r=>(string)r["building"]==raw);if((bool)row["base"])return (int)row["cost"];var parent=rows.First(r=>((JArray)r["upgrades"]).Any(u=>(string)u==raw));return (int)row["cost"]+Cost(data,(string)parent["building"]);}
    static JObject Load(string race)=>JObject.Parse(File.ReadAllText("Assets/Resources/pc-"+race+".json"));
    static HumanMatch.Hut Tower(HumanMatch m,string raw){var h=new HumanMatch.Hut{id=80000,team=0,kind=m.Kind(raw),position=Vector2.zero,paid=true,hp=1200,maxHp=1200};m.Huts.Add(h);return h;}
    static void TowerTime(HumanMatch m,HumanMatch.Hut h,float seconds){for(int i=0;i<Mathf.RoundToInt(seconds/.05f);i++){m.Elapsed+=.05f;foreach(var u in m.Units.ToArray())Call(m,"TickEffects",u,.05f);m.TickBuilding(h,.05f);Call(m,"TickShots",.05f);}}
    public static void RunTower(){
        results=new JArray();human=Load("human");nerub=JObject.Parse(File.ReadAllText("Assets/Nerubian032/pc-nerubian.json"));
        foreach(string raw in new[]{"hNT1","hNT2"})foreach(bool air in new[]{false,true})Case(raw+"_shoots_"+(air?"air":"ground"),()=>{
            var m=Nerub();var h=Tower(m,raw);var e=Enemy(m,new Vector2(10,0));e.hp=e.maxHp=100000;m.Lines[e.kind].stats["flying"]=air;TowerTime(m,h,30);
            results.Add(new JObject{["measurement"]=raw+(air?"_air":"_ground"),["damage30s"]=100000-e.hp,["attacksStarted"]=h.attackSerial,["huntProcs"]=m.NerubHunts,["spellCasts"]=m.SpellsCast});
            Check(e.hp<100000,"Tower projectiles actually damage the target");Check(m.NerubHunts==0,"Tower alone does not consume the hunt mark bonus");
            if(!air)Check(raw=="hNT2"?e.effects.Any(x=>x.id=="ANMK"):!e.effects.Any(x=>x.id=="ANMK"),"Only upgraded tower marks ground targets");
        });
        Case("tower_range_and_target_priority",()=>{var m=Nerub();var h=Tower(m,"hNT1");var outside=Enemy(m,new Vector2(27,0));TowerTime(m,h,2);Check(h.attackSerial==0&&outside.hp==10000,"Tower does not shoot beyond its 800 range");var ground=Enemy(m,new Vector2(5,0));var air=Spawn(m,"h015",1,new Vector2(20,0));air.hp=air.maxHp=10000;TowerTime(m,h,.5f);
            Check(h.attackTarget==ground.id,"Weapon picks nearest eligible target");Check(air.effects.Any(e=>e.id=="AUWB")&&m.Rooted(air)&&!m.Flying(air),"Independent web grounds a farther air target while weapon aims at ground");Check(h.spellTimers["AUWB"]>11&&h.spellTimers["AUWB"]<=12,"Web has a 12-second cooldown");
        });
        Case("tower_web_magic_immunity",()=>{var m=Nerub();var h=Tower(m,"hNT2");var e=Enemy(m);m.Lines[e.kind].stats["flying"]=true;e.spellResist=1;TowerTime(m,h,.05f);Check(e.effects.Any(x=>x.id=="AUWB")&&m.Rooted(e),"Physical web works against magic-immune flyers");TowerTime(m,h,5.1f);Check(m.Flying(e)&&!m.Rooted(e),"Flight and movement recover after the five-second web");});
        foreach(string raw in new[]{"hNT1","hNT2"})foreach(int aspect in new[]{-1,1})Case(raw+"_poison_aspect_"+aspect,()=>{var m=Nerub();m.Aspects[0]=aspect;var h=Tower(m,raw);var e=Enemy(m);TowerTime(m,h,.8f);string id=raw=="hNT1"?"AUN4":"AUN5";var poison=e.effects.Single(x=>x.id==id);float expected=(raw=="hNT1"?4:9)*(aspect==1?1.5f:1);Check(Math.Abs(HumanMatch.F(poison.config,"dps")-expected)<.001,"Real tower projectile applies correct poison and Neurotoxin factor");Check(Math.Abs(poison.lifetime-5)<.01,"Tower poison lasts five seconds");Check(Math.Abs(HumanMatch.F(poison.config,"slow")-(raw=="hNT1"?.08f:.15f))<.001,"Tower poison has the existing movement slow");Check(Math.Abs(poison.armor-(raw=="hNT1"?0:-3))<.001,"Upgraded poison reduces armor by three");float afterHit=e.hp;TowerTime(m,h,1.2f);Check(e.hp<afterHit,"Applied tower poison actually ticks between shots");Check(e.effects.Count(x=>x.group==poison.group)==1,"Repeated tower hits do not stack poison effects");});
        Case("tower_poison_immunity_and_strength",()=>{var m=Nerub();var h=Tower(m,"hNT1");var e=Enemy(m);e.spellResist=1;TowerTime(m,h,1);Check(!e.effects.Any(x=>x.id=="AUN4"),"Tower poison respects magic immunity");e.spellResist=0;m.ApplyEffect(e,"AUN5",0);TowerTime(m,h,2);Check(e.effects.Any(x=>x.id=="AUN5")&&!e.effects.Any(x=>x.id=="AUN4"),"Weak tower poison does not replace stronger Venomancer poison");});
        Case("existing_nerubian_scenarios",()=>FinalRaceScenarios.Run("nerubian",nerub,human,Check));
        Case("tower_art_references",()=>{var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>("Assets/Resources/NerubianSwarm.asset");var rows=(JArray)JObject.Parse(pack.catalog.text)["lines"];Check(rows.Count==18&&pack.buildings.Length==36,"Original 18-row two-team presentation layout is retained");foreach(string raw in new[]{"hNT1","hNT2"}){int row=rows.ToList().FindIndex(x=>(string)x["building"]==raw);for(int team=0;team<2;team++){var p=pack.buildings[team*rows.Count+row];var model=p.GetComponent<PcModel>();Check(model!=null&&model.raw==raw&&p.GetComponentsInChildren<Renderer>(true).Length>0,"Correct nonempty tower prefab for "+raw+" team "+team);results.Add(new JObject{["towerPrefab"]=raw,["team"]=team,["asset"]=AssetDatabase.GetAssetPath(p),["clips"]=new JArray(model.clips.Select(c=>c.name))});}Check(AssetDatabase.LoadAssetAtPath<Texture2D>((string)BuildNerubian032.Registry()["NerubianBuildingIcon/"+raw])!=null,"Separate building icon resolves for "+raw);}});
        string output=Environment.GetEnvironmentVariable("CF_AUDIT_OUTPUT")??"/workspace/tower-audit.json";var report=new JObject{["results"]=results,["failedCases"]=results.Count(r=>(string)r["status"]=="FAIL"),["source"]="Actual Unity simulation and prefab references; icons visually inspected separately. No GUI or device execution."};File.WriteAllText(output,report.ToString());Debug.Log("TOWER_AUDIT_COMPLETE "+output);if((int)report["failedCases"]>0)throw new Exception("Tower audit failed; read "+output);
    }
    static void Wave(HumanMatch m,JObject data,string[] roster,int side){int i=0;foreach(var raw in roster){int k=m.Kind(raw);bool ranged=m.Lines[k].range>4;float x=(side==0?-1:1)*(ranged?10:5);var u=m.Spawn(side,k,new Vector2(x,(i++%3-1)*1.4f));u.onRoad=true;}}
    static void Balance(){
        var rosters=new Dictionary<string,string[]>{
            {"mixed",new[]{"hNV1","hNW1","hNP1","hNP1","hNS1","hNA1"}},
            {"front_heavy",new[]{"hNV1","hNV1","hNV1","hNV1","hNP1","hNP1","hNS1"}},
            {"poison_heavy",new[]{"hNP1","hNP1","hNP1","hNP1","hNP1","hNW1"}},
            {"web_hunt",new[]{"hNW1","hNW1","hNW1","hNP1","hNP1","hNA1"}},
            {"cocoon_heavy",new[]{"hNS1","hNS1","hNS1","hNV1","hNP1","hNP1"}},
            {"upgraded_poison",new[]{"hNP2","hNP2","hNP1","hNW1"}},
            {"upgraded_front_hunt",new[]{"hNV2","hNW1","hNS1","hNA1","hNA1"}},
            {"upgraded_tanks",new[]{"hNV2","hNV2","hNV2","hNV2"}}
        };
        string[] mirror=rosters["mixed"];
        var enemies=new Dictionary<string,Tuple<JObject,string[]>>{
            {"human_infantry_rifles",Tuple.Create(human,new[]{"h000","h000","h000","h000","h000","h003","h003","h003","h003","h003"})},
            {"human_air_mix",Tuple.Create(human,new[]{"h000","h000","h000","h003","h003","h015","h015"})},
            {"human_paladin_guard",Tuple.Create(human,new[]{"h038","h039","h003","h003"})},
            {"orc_mixed",Tuple.Create(Load("orcs"),new[]{"h029","h029","h029","h029","h02B","h02B","h02P"})},
            {"orc_berserkers",Tuple.Create(Load("orcs"),new[]{"h02H","h02H","h02H","h02H","h02H"})},
            {"undead_mixed",Tuple.Create(Load("undead"),new[]{"h01N","h01N","h01K","h01K","h01R"})},
            {"nerubian_mirror",Tuple.Create(nerub,mirror)}
        };
        var matrix=new JArray();
        foreach(var roster in rosters)foreach(var enemy in enemies)foreach(int aspect in new[]{-1,1}){
            string[] own=roster.Value;
            if(aspect==1&&!own.Any(r=>r=="hNP1"||r=="hNP2"))continue;
            int wins=0,losses=0,draws=0,totalRaises=0;float totalSeconds=0;
            int ownCost=own.Sum(r=>Cost(nerub,r)),otherCost=enemy.Value.Item2.Sum(r=>Cost(enemy.Value.Item1,r));if(ownCost!=1000||otherCost!=1000)throw new Exception("Unequal wave budgets");
            for(int seed=1;seed<=20;seed++)for(int side=0;side<2;side++){
                JObject a=side==0?nerub:enemy.Value.Item1,b=side==0?enemy.Value.Item1:nerub;
                var m=new HumanMatch((JObject)a.DeepClone(),0,0,side==0?aspect:-1,side==1?aspect:-1,(JObject)b.DeepClone());
                typeof(HumanMatch).GetField("randomState",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(m,(uint)(seed*91771));
                typeof(HumanMatch).GetField("botAge",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(m,100000f);
                typeof(HumanMatch).GetField("botRescueAge",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(m,100000f);
                Wave(m,nerub,own,side);Wave(m,enemy.Value.Item1,enemy.Value.Item2,1-side);float empty=0;
                for(int t=0;t<1800;t++){
                    m.Step(.05f);if(m.Units.Any(u=>float.IsNaN(u.hp)||float.IsNaN(u.position.x)))throw new Exception("Non-finite simulation state");
                    if(m.ArmyCount(0)==0||m.ArmyCount(1)==0)empty+=.05f;else empty=0;if(empty>=2)break;
                }
                bool alive=m.ArmyCount(side)>0,other=m.ArmyCount(1-side)>0;if(alive&&!other)wins++;else if(other&&!alive)losses++;else draws++;
                totalSeconds+=m.Elapsed;totalRaises+=m.Resurrections;
            }
            matrix.Add(new JObject{["nerubianRoster"]=roster.Key,["enemy"]=enemy.Key,["nerubianAspect"]=aspect==1?"neurotoxin":"none",["goldBudgetEach"]=1000,["nerubianBuildings"]=new JArray(own),["enemyBuildings"]=new JArray(enemy.Value.Item2),["wins"]=wins,["losses"]=losses,["draws"]=draws,["meanSeconds"]=totalSeconds/40,["totalResurrections"]=totalRaises});
        }
        results.Add(new JObject{["balanceMatrix"]=matrix,["scope"]="8 Nerubian rosters, 1000 cumulative building gold per side, 20 seeded repetitions and both sides; no production, income spending, items, upgrades during combat or faction-special buildings. Not a match win rate."});
    }
    public static void Run(){
        results=new JArray();human=JObject.Parse(File.ReadAllText("Assets/Resources/pc-human.json"));nerub=JObject.Parse(File.ReadAllText("Assets/Nerubian032/pc-nerubian.json"));
        Case("existing_nerubian_scenarios",()=>FinalRaceScenarios.Run("nerubian",nerub,human,Check));
        Case("existing_human_checks",()=>{HumanMatchChecks.Run();var f=typeof(HumanMatchChecks).GetField("checks",BindingFlags.Static|BindingFlags.NonPublic);results.Add(new JObject{["existingHumanChecks"]=(int)f.GetValue(null)});});
        results.Add(new JObject{["humanChecksReached"]=(int)typeof(HumanMatchChecks).GetField("checks",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)});
        PaladinCases();NerubCases();Case("equal_gold_wave_matrix",Balance);
        string output=Environment.GetEnvironmentVariable("CF_AUDIT_OUTPUT")??"/workspace/combat-ability-audit.json";
        var report=new JObject{["results"]=results,["failedCases"]=results.Count(r=>(string)r["status"]=="FAIL"),["source"]="Actual Unity HumanMatch simulation; no device or GUI execution"};File.WriteAllText(output,report.ToString());Debug.Log("COMBAT_AUDIT_COMPLETE failedCases="+report["failedCases"]+" output="+output);if((int)report["failedCases"]>0)throw new Exception("Combat audit has failing cases; read "+output);
    }
}
