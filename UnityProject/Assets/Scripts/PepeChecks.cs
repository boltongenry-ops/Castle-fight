using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class HumanMatch {
    static void CheckPepeShieldDamage(JObject pepe, JObject humans, Action<bool,string> check) {
        var game = new HumanMatch(pepe, 250, 1, -1, -1, humans);
        foreach (var line in game.Lines.Where(l => l.Producer && l.unit.StartsWith("pU", StringComparison.Ordinal))) {
            var shooter = game.Spawn(0, game.Kind(line.building), Vector2.zero);
            var target = game.Spawn(1, game.Kind("h000"), Vector2.right);
            var weapon = line.weapons[0];
            float Attack() {
                target.hp = target.maxHp = 100000;
                shooter.spellTimers.Clear();
                game.BeginAttack(shooter, weapon, target.id);
                game.Release(shooter);
                game.TickShots(3);
                return target.maxHp - target.hp;
            }
            float initial = Attack();
            game.PepeGiveShield(shooter, 170, 6);
            float full = Attack();
            game.DamageUnit(shooter, 50, 1, "normal", false);
            float remaining = game.PepeShield(shooter)?.power ?? 0;
            float partial = Attack();
            check(initial > 0 && Mathf.Abs(full - initial) < .05f && Mathf.Abs(partial - initial) < .05f
                && remaining > 0 && remaining < 170,
                "Shielded weapon damage " + line.unit + " unshielded/full/partial: "
                + initial.ToString("F2") + "/" + full.ToString("F2") + "/" + partial.ToString("F2"));
            shooter.effects.Add(new Effect { id="testDamageBuff", left=5, power=.25f });
            game.BeginAttack(shooter, weapon, target.id);
            check(Mathf.Abs(shooter.shotDamage - weapon.damage * 1.25f) < .01f,
                "Shield preserves an ordinary 25% weapon buff on " + line.unit);
            shooter.hp = target.hp = 0;
        }
    }

    public static void CheckPepe(JObject pepe,JObject humans,Action<bool,string> check) {
        CheckPepeShieldDamage(pepe, humans, check);
        var game = new HumanMatch(pepe, 250, 1, 0, -1, humans);
        Unit Add(string row, int team, float x, float y=0) => game.Spawn(team,game.Kind(row),new Vector2(x,y));
        void Hit(Unit source, Unit target) {
            var weapon = game.Lines[source.kind].weapons[0];
            game.ResolveAttack(source.id, source.team, target.id, weapon.damage, weapon, 0);
        }
        var archer = Add("pB32",0,-5);
        var guard = Add("pB31",0,-2);
        var victim = Add("h000",1,0);
        var arrow=game.AddShot(0,archer.kind,archer.id,victim.id,archer.position,victim.position,34,
            game.Lines[archer.kind].weapons[0],0,3);
        check(!game.PepeHasMark(victim,0)&&!arrow.hit,"A shot in flight does not mark a target");
        game.TickShots(1);
        check(arrow.hit&&game.PepeHasMark(victim,0),"Archer marks only after impact");
        Hit(guard,victim);
        check(!game.PepeHasMark(victim,0)&&Mathf.Approximately(game.PepeShield(guard)?.power??0,56.25f),
            "Shell aspect scales absorbed damage and melee consumes one mark");
        check(!game.PepeMark(victim,0),"Mark cannot be reapplied during target immunity");
        float before = guard.hp;
        game.DamageUnit(guard,30,1,"normal",false);
        check(guard.hp==before && guard.spellTimers.ContainsKey("PBr1") && game.PepeShield(guard).power<56.25f,
            "Guard personal block and shared shield work on one hit");
        game.PepeChangeTeam(guard);
        check(game.PepeShield(guard)==null,"A converted guard loses its old shield");

        var griffin = Add("h015",1,2);
        Hit(archer,griffin);
        check(archer.spellTimers.ContainsKey("PAr1")&&!game.PepeHasMark(griffin,0),
            "Personal archer bonus affects air without marking it");
        var rider = Add("pB04",0,-4);
        victim.spellTimers.Clear();
        check(game.PepeMark(victim,0)&&game.PepeEvasion(rider)>.07f,"Flying rider has its own evasion");
        Hit(rider,victim);
        check(rider.spellTimers.ContainsKey("PDiv")&&game.PepeHasMark(victim,0),"Rider attack preserves the mark");
        var eliteRider=Add("pB17",0,-7);
        check(eliteRider.abilityLevels["PDiv"]==2&&eliteRider.abilityLevels["PEva"]==2,
            "Elite dragonfly guard uses rank two PDiv and PEva");
        check(game.PepeEvasion(eliteRider)>.11f,"Elite dragonfly guard evasion is twelve percent");
        float eliteBonus=game.PepeWeaponBonus(eliteRider,victim,game.Lines[eliteRider.kind].weapons[0]);
        check(Mathf.Abs(eliteBonus-27)<.01f,"Elite dragonfly guard low pass adds twenty-seven");

        var tadpole = Add("pB06",0,-1);
        Hit(tadpole,victim);
        check(tadpole.spellTimers.ContainsKey("PLea"),"Tadpole triggers a personal damage bonus");
        guard.hp=120;
        var sage = Add("pB05",0,-5);
        check(game.PepeTryHeal(sage)&&guard.hp==210&&sage.mana==0&&!game.PepeTryHeal(sage),
            "The healer spends mana for exactly one targeted cast");

        var heavy = Add("pB15",0,-4);
        game.PepeArtilleryImpact(new Shot{sourceId=heavy.id,team=0,end=victim.position});
        check(heavy.spellTimers.ContainsKey("PSg2")&&victim.effects.Any(e=>e.id=="PSl2")&&!griffin.effects.Any(e=>e.id=="PSl2"),
            "Heavy ballista hooks slow the ground around the impact");
        var quak = Add("pB13",0,-1);
        var second = Add("h000",1,1);
        Hit(quak,victim);
        check(quak.spellTimers.ContainsKey("PQua")&&second.effects.Any(e=>e.id=="PQDb"),
            "Great Quak hits adjacent ground targets and slows their attacks");

        foreach(string id in new[]{"pB07","pB08","pB09","pB16"}){
            var hut = new Hut{team=0,kind=game.Kind(id),position=Vector2.zero,mana=48,paid=true,hp=1000,maxHp=1000};
            victim.spellTimers.Clear();second.spellTimers.Clear();
            victim.effects.Clear();second.effects.Clear();
            guard.combatTarget=second.id;sage.combatTarget=victim.id;
            game.TickBuilding(hut,.05f);
            check(hut.mana<48 && hut.spellTimers.Count==1,"Building casts automatically "+id);
        }
        var farBell=new HumanMatch(pepe,250,1,0,-1,humans);
        var farAllyA=farBell.Spawn(0,farBell.Kind("pB31"),new Vector2(100,0));
        var farAllyB=farBell.Spawn(0,farBell.Kind("pB31"),new Vector2(100,2));
        var farEnemy=farBell.Spawn(1,farBell.Kind("h000"),new Vector2(120,0));
        farAllyA.combatTarget=farAllyB.combatTarget=farEnemy.id;
        var farBellHut=new Hut{team=0,kind=farBell.Kind("pB09"),position=new Vector2(-160,0),mana=48,paid=true,hp=1000,maxHp=1000};
        farBell.Huts.Add(farBellHut);farBell.TickBuilding(farBellHut,.05f);
        check(farAllyA.effects.Any(e=>e.id=="PShd"&&e.power>0)&&
            farAllyB.effects.Any(e=>e.id=="PShd"&&e.power>0)&&farBellHut.mana==24,
            "Bell shields fighting allies anywhere on the map");
        var tower=game.Lines[game.Kind("pB10")].buildingWeapons;
        check(tower.Length==2&&tower[0].air&&!tower[0].ground&&tower[1].ground&&!tower[1].air&&
            tower.All(w=>!w.structure),"Reed tower has separate air and ground weapons");
        check(tower[0].damage>tower[1].damage,"Air projectile keeps its damage advantage");
        var mirrored=new HumanMatch(pepe);
        mirrored.Wood[0]=2000;mirrored.Gold[0]=2000;
        check(mirrored.TryBuild(0,mirrored.Kind("pB09"),15,15,out _) &&
            mirrored.PurchaseError(0,mirrored.Kind("pB13")).Contains("легендар"),"Both legendary branches share a slot");
        check(mirrored.TryBuild(0,mirrored.Kind("pB31"),17,15,out _),"The ordinary guardhouse can be built");
        var house=mirrored.Huts.Last();
        house.paid=true;
        check(mirrored.Upgrade(0,house.id,mirrored.Kind("pB12"),out _)&&
            !mirrored.Upgrade(0,house.id,mirrored.Kind("pB13"),out _),"Actual upgrade enforces the reserved slot");
        mirrored.Sell(0,mirrored.Huts.First().id);
        check(mirrored.Upgrade(0,house.id,mirrored.Kind("pB13"),out _)&&mirrored.Interval(house)==41,
            "Finishing the legendary chain produces Quak at 41 seconds");
        check(mirrored.BaseKinds.Length==11&&mirrored.Lines.Count(x=>x.Producer)==12,
            "The race catalog exposes eleven base buildings and twelve fighters");
        foreach(var pair in new[]{("pB04","pB17",35f),("pB05","pB18",31.75f)}){
            var trial=new HumanMatch(pepe,250,1,0,-1,humans);trial.Gold[0]=2000;
            bool built=trial.TryBuild(0,trial.Kind(pair.Item1),17,15,out _);
            var hut=trial.Huts.Last();hut.paid=true;
            check(built&&trial.Upgrade(0,hut.id,trial.Kind(pair.Item2),out _)&&trial.Interval(hut)==pair.Item3,"Producer upgrade "+pair.Item2);
        }
        var healing=new HumanMatch(pepe,250,1,0,-1,humans);
        var elder=healing.Spawn(0,healing.Kind("pB18"),Vector2.zero);
        var ally=healing.Spawn(0,healing.Kind("pB31"),Vector2.right);ally.hp=120;elder.mana=35;
        check(healing.PepeTryHeal(elder)&&ally.hp==265&&elder.mana==0&&!healing.PepeTryHeal(elder),"Elder heal and cooldown");
        var ambush=new HumanMatch(pepe,250,1,1,-1,humans);
        var marked=ambush.Spawn(1,ambush.Kind("h000"),Vector2.zero);
        check(ambush.PepeMark(marked,0)&&marked.effects.Single(e=>e.id==PepeRules.PepeMarkId).left==7,
            "Ambush aspect extends marks to seven seconds");
        var bot=new HumanMatch(humans,250,1,-1,1,pepe);
        for(int tick=0;tick<1200;tick++)bot.Step(.05f);
        check(bot.Huts.Any(h=>h.team==1&&h.paid&&bot.Lines[h.kind].building.StartsWith("pB")) &&
            bot.Units.Any(u=>u.team==1&&bot.Lines[u.kind].unit?.StartsWith("pU")==true),
            "The bot builds and produces Pepe fighters starting with 250 gold");
    }
}

public sealed partial class PcGame {
    IEnumerator ExercisePepe() {
        string folder = BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"pepe"));
        Directory.CreateDirectory(folder);
        MainMenu();
        Check(RaceStaged(PepeContent.Race)&&RaceEmblem(PepeContent.Race),"Pepe is available in offline race selection");
        Check(AspectField(PepeContent.Race,0,"title")=="Крепкий панцирь"&&AspectIcon(PepeContent.Race,1),
            "Both aspects have titles and icons");
        HumanMatch.CheckPepe(JObject.Parse(RaceCatalog(PepeContent.Race)),JObject.Parse(RaceCatalog(0)),Check);
        raceChoice=PepeContent.Race;botRaceChoice=0;page=Page.Setup;
        yield return Shot(folder,"01-pepe-selection");
        for(int side=0;side<2;side++){
            BeginVersus(side==0?PepeContent.Race:0,side==0?0:PepeContent.Race,false);
            yield return LoadVersusArt(versusSerial);
            var painting=side==0?versusLeft:versusRight;
            Check(painting && painting.width*3==painting.height*2 && painting.height>=900,"Full-height Pepe VS painting on side "+side);
            versusPrepared=true;
            yield return Shot(folder,"vs-pepe-"+side);
            CloseVersus();
        }
        startGold=100000;aspectsOn=false;NewMatch();testHoldAuthority=true;options.fogOfWar=false;
        match.Wood[0]=10000;
        foreach(int kind in match.BaseKinds){
            int index=Array.IndexOf(match.BaseKinds,kind);
            var hut=QaBuild(kind,15+index%4,12+index/4);
            hut.paused=true;
        }
        var roster=match.Lines.Select((line,index)=>(line,index)).Where(row=>row.line.unit?.StartsWith("pU")==true).ToArray();
        for(int i=0;i<roster.Length;i++){
            var unit=match.Spawn(0,roster[i].index,new Vector2(-144+i%5*5,10+i/5*8));
            unit.castPause=120;
        }
        RenderMatch();
        Check(match.Huts.All(h=>huts.ContainsKey(h.id)),"All eleven base buildings render");
        foreach(var view in pepePack.buildings){
            var bounds=view.GetComponent<PcModel>().BodyBounds();
            Check(Mathf.Max(bounds.size.x,bounds.size.z)<HumanMatch.Cell-.3f && bounds.size.y<=5.3f,
                "Building fits one cell "+view.name);
        }
        Check(pepePack.abilityIds.All(id=>AbilityArt(id)), "Every Pepe skill resolves in the live HUD");
        Check(roster.All(row=>PepeContent.FighterIcon(row.line.unit)),"Every producer has a fighter portrait");
        Check(roster.Select(row=>PepeContent.FighterIcon(row.line.unit)).Distinct().Count()==12,"Twelve distinct production portraits");
        var firstHut=match.Huts.First(h=>match.Lines[h.kind].building=="pB31");
        controls.FocusWorld(World(firstHut.position),27);
        QaSelect(firstHut);
        yield return Shot(folder,"adjacent-producers");
        foreach(var unit in match.Units){
            var model=units[unit.id];
            Check(model&&model.raw==match.Lines[unit.kind].unit&&model.clips.Any(clip=>clip.name=="Walk")
                &&model.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).All(m=>m&&m.mainTexture&&m.shader.isSupported),
                "Playable model and materials "+match.Lines[unit.kind].unit);
        }
        foreach(string raw in new[]{"pU02","pU14"}){
            var model=units[match.Units.First(u=>match.Lines[u.kind].unit==raw).id];
            var bones=model.GetComponentsInChildren<Transform>(true);
            var nock=bones.First(t=>t.name=="ReedNock");
            var shaft=bones.First(t=>t.name=="ReedShaft");
            var drawHand=bones.First(t=>t.name=="Arrow");
            foreach(var clip in model.clips.Where(c=>c.name=="Attack"||c.name=="Walk"||c.name=="Stand")){
                foreach(float t in new[]{.02f,.16f,.27f,.41f,.85f}){
                    clip.SampleAnimation(model.animatedRoot,t);
                    bool loose=clip.name=="Attack"&&t>.31f&&t<.79f;
                    Check(loose ? shaft.localScale.x<.005f :
                        Vector3.Distance(nock.position,shaft.position)<.025f && Vector3.Distance(shaft.position,drawHand.position)<.025f,
                        raw+" arrow "+clip.name+" at "+t.ToString("F2"));
                }
            }
            model.StandClip().SampleAnimation(model.animatedRoot,.1f);
        }
        builderSelected=false;selectedUnit=match.Units.First(u=>match.Lines[u.kind].unit=="pU04").id;
        selectedHut=-1;controls.FocusWorld(World(new Vector2(-134,15)),37);
        yield return Shot(folder,"02-pepe-army");
        builderSelected=true;selectedUnit=-1;
        yield return Action("builder.build");
        Check(buildMenu&&ConstructionPageKinds().Length==11,"Builder exposes all race structures in one menu");
        yield return Shot(folder,"03-pepe-construction");
        foreach(string raw in new[]{"pU02","pU14"}){
            var fighter=match.Units.First(u=>match.Lines[u.kind].unit==raw);
            var model=units[fighter.id];
            match.DamageUnit(fighter,100000,1,"chaos",true,true);
            RenderMatch();
            var death=model.clips.Single(c=>c.name=="Death");
            var track=model.animatedRoot.GetComponent<Animation>()["Death"];
            Check(fighter.hp<=0 && track.enabled && track.wrapMode==WrapMode.ClampForever,
                raw+" plays and holds Death after a lethal hit");
            var head=model.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Bone_Head");
            death.SampleAnimation(model.animatedRoot,0);
            float standing=head.position.y, height=model.BodyBounds().size.y;
            death.SampleAnimation(model.animatedRoot,1.05f);
            var fallen=head.position;
            death.SampleAnimation(model.animatedRoot,1.34f);
            Check(standing-fallen.y>height*.22f && Vector3.Distance(head.position,fallen)<height*.035f,
                raw+" falls to the ground and stays down");
        }
        buildMenu=false;builderSelected=false;selectedHut=-1;selectedUnit=-1;
        var guard=match.Units.First(x=>match.Lines[x.kind].unit=="pU01");
        controls.FocusWorld(World(guard.position),21);
        match.Pulses.Clear();
        guard.effects.Add(new HumanMatch.Effect{id="PShd",sourceTeam=guard.team,power=45,left=2.2f,lifetime=3});
        RenderMatch();
        var barrier=visuals.transform.Find("Swamp spells").GetComponent<MeshRenderer>();
        Check(barrier.enabled,"The shield stays visible after its acquisition flash expires");
        yield return Shot(folder,"08-pepe-shield");
        guard.effects.First(x=>x.id=="PShd").power=0;
        RenderMatch();Check(!barrier.enabled,"The barrier disappears on the hit that exhausts absorption");
        var battleSounds=BattleAudio.For(match);
        battleSounds.Missile(@"Abilities\Weapons\Arrow\ArrowMissile.mdl",3,false,guard.position);
        var shotSource=visuals.GetComponents<AudioSource>().FirstOrDefault(a=>a.clip && a.clip.name.StartsWith("WC3 combat"));
        Check(shotSource!=null,"Archive combat audio starts on the battle mixer");
        if(shotSource){
            var pcm=new float[shotSource.clip.samples];shotSource.clip.GetData(pcm,0);
            Check(pcm.Any(x=>Mathf.Abs(x)>.025f),"The decoded Warcraft recording contains audible PCM");
        }
        var center=new Vector2(-124,12);
        controls.FocusWorld(World(center),36);
        string[] spells={"PMrk","PMr2","PTmp","PGSh","PShd","PBr1","PBr2","PAr1","PAr2","PSg1",
            "PSg2","PEva","PDiv","PHea","PHe2","PLea","PQua","PHrn","PHr2","PLil","PBll","reed-tower"};
        for(int i=0;i<spells.Length;i++){
            match.PulseAt(center+new Vector2((i%5-2)*5,(i/5-1.5f)*4),1.8f,Color.white,"",1,"pepe:"+spells[i]);
            match.Pulses.Last().age=.28f;
        }
        RenderMatch();
        foreach(string spell in spells)Check(match.Pulses.Any(p=>p.fx=="pepe:"+spell&&Art036.rendered.Contains(p.id)),
            "Dedicated effect draws "+spell);
        string[] otherFx={"steppe:charge","steppe:break","steppe:harpoon","steppe:needles","steppe:stomp","steppe:sand","jade:panda_brew","jade:panda_keg","jade:panda_gust","jade:panda_stomp","jade:panda_tower","jade:combo"};
        foreach(var fx in otherFx)match.PulseAt(center,1.5f,Color.white,"",.8f,fx);
        RenderMatch();
        foreach(var fx in otherFx)Check(match.Pulses.Any(p=>p.fx==fx&&Art036.rendered.Contains(p.id)),"Dedicated effect draws "+fx);
        var swamp=visuals.transform.Find("Swamp spells");
        Check(swamp && swamp.GetComponent<MeshFilter>().sharedMesh.vertexCount>900
            && swamp.GetComponent<Renderer>().sharedMaterial.shader.isSupported,"Swamp skills share one supported mesh renderer");
        yield return Shot(folder,"07-pepe-skills");
        MainMenu();OpenGuide();GuideChooseRace(PepeContent.Race);GuideSelectEntry(3);
        Check(guideAuthority.Lines[GuideActorKind].unit=="pU04"&&GuideUnitIcon(GuideActorKind)&&guideDescription.Contains("Вираж"),"Guide uses the unit portrait and personal ability description");
        yield return Shot(folder,"04-pepe-guide");
        GuideSelectEntry(1);
        yield return Shot(folder,"05-archer-guide");
        GuideSelectEntry(0);
        yield return Shot(folder,"06-guard-guide");
        MainMenu();
        File.WriteAllText(Path.Combine(folder,"pepe-verification.json"),new JObject {
            ["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures
        }.ToString());
        Debug.Log("CF_PEPE_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);
        Application.Quit(testFailures==0?0:1);
    }
}
}
