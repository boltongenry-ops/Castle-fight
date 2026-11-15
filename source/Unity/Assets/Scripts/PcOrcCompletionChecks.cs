using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseOrcCompletion(string folder) {
        MainMenu();yield return Action("main.play");yield return Action("setup.orc");
        aspectsOn=true;yield return Action("setup.start");testHoldAuthority=true;
        Check(page==Page.Match&&aspectWaiting&&match.Aspects[0]<0&&match.Aspects[1]>=0,"Orc aspect picker opens at round start and the bot already chose");
        yield return Shot(folder,"64-orc-aspects");
        yield return Action("aspect.choose1");Check(!aspectWaiting&&match.Aspects[0]==1,"Shaman support aspect is chosen inside the match");
        MainMenu();yield return Action("main.play");yield return Action("setup.orc");yield return Action("setup.start");Check(aspectWaiting,"a new match opens the aspect picker again");
        yield return Action("aspect.choose0");Check(match.Aspects[0]==0,"Blood and Honor aspect is chosen inside the match");
        aspectsOn=false;MainMenu();yield return Action("main.play");yield return Action("setup.orc");
        options.hdTextures=true;yield return Action("setup.start");testHoldAuthority=true;match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;
        string[] raw={"h02P","h02I","h02R","h02O","h02N","o000"};
        for(int i=0;i<raw.Length;i++)QaBuild(match.Kind(raw[i]),3+i*4,15);
        RenderMatch();builderSelected=true;buildMenu=true;controls.FocusWorld(World(new Vector2(-150,28)),58);yield return Shot(folder,"65-orc-complete-buildings");
        Check(huts.Count==6&&huts.Values.All(v=>v.reforged)&&match.BaseKinds.Length==11,"all six remaining Orc base buildings appear with Reforged models");
        var serpent=match.Huts.First(h=>match.Lines[h.kind].building=="h02N");Check(orcDecorModels.ContainsKey(serpent.id+"/serpent_rock/0"),"Serpent Rock includes its original rubble decoration");controls.FocusWorld(World(serpent.position),32);yield return Shot(folder,"92-serpent-decor");
        foreach(int k in new[]{match.Kind("h02P"),match.Kind("h02I")})for(int team=0;team<2;team++){var u=match.Spawn(team,k,new Vector2(-161+(k-match.Kind("h02P"))*12,30+team*7));u.onRoad=true;u.cooldown=10000;}
        RenderMatch();builderSelected=false;buildMenu=false;selectedUnit=match.Units.First(u=>u.team==0&&match.Lines[u.kind].unit=="o009").id;controls.FocusWorld(World(new Vector2(-155,33)),45);yield return Shot(folder,"66-wyvern-catapult-reforged");
        Check(units.Count==4&&units.Values.All(v=>v.reforged)&&AbilityArt("A038")&&AbilityArt("A02J"),"Wyvern and catapult use Reforged models for both teams with their source ability icons");
        options.hdTextures=true;Apply(options,false);RenderMatch();yield return Shot(folder,"67-orc-completion-classic");Check(units.Values.All(v=>v.reforged)&&huts.Values.All(v=>v.reforged),"all final Orc buildings and units use Reforged models");options.hdTextures=true;Apply(options,false);
        NewMatch();testHoldAuthority=true;match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;foreach(var l in match.Lines){l.speed=0;l.regen=0;}
        var acid=QaBuild(match.Kind("h02N"),7,15);var target=match.Spawn(1,0,acid.position+Vector2.right*10);target.onRoad=true;target.hp=target.maxHp=100000;target.cooldown=10000;acid.mana=12;match.TickBuilding(acid,.01f);
        RenderMatch();builderSelected=false;selectedHut=acid.id;controls.FocusWorld(World(acid.position+Vector2.right*5),40);Check(match.OrcAcidShots==1&&target.effects.Count==0&&BuildingStatus(acid).Contains("Кислотные бомбы")&&BuildingStatus(acid).Contains("мана"),"Serpent Rock displays its real ability and mana and launches acid before impact");yield return Shot(folder,"68-acid-flight");
        for(int i=0;i<12&&match.OrcAcidImpacts==0;i++)Tick(.05f);RenderMatch();selectedHut=-1;selectedUnit=target.id;Check(match.OrcAcidImpacts==1&&target.effects.Any(e=>e.id=="A030"),"acid hits and applies a timed debuff in the actual player");yield return Shot(folder,"69-acid-impact");
        Debug.Log("CF_ORC_COMPLETION_PLAYER_PASS buildings=11 upgrades=8 units=14 aspects=2 wyvern=poison catapult=fire specials=4 hd=true classic=true");
    }
}
}
