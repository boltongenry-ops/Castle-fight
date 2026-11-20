using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class HumanMatch {
    public static void CheckPepe(JObject pepe,JObject humans,Action<bool,string> check) {
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
        var tower=game.Lines[game.Kind("pB10")].buildingWeapons.Single();
        check(tower.air&&!tower.ground&&!tower.structure,"Reed tower targets air only");
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
        check(mirrored.BaseKinds.Length==11&&mirrored.Lines.Count(x=>x.Producer)==10,
            "The race catalog exposes eleven buildings and ten fighters");
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
        Check(roster.Select(row=>PepeContent.FighterIcon(row.line.unit)).Distinct().Count()==10,"Ten distinct production portraits");
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
        MainMenu();OpenGuide();GuideChooseRace(PepeContent.Race);GuideSelectEntry(3);
        Check(guideModel&&guideModel.raw=="pU04"&&guideDescription.Contains("Вираж"),"Guide uses live model and personal ability description");
        yield return Shot(folder,"04-pepe-guide");
        GuideSelectEntry(1);
        guideSpin=false;
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
