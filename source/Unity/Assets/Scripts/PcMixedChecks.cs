using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseMixedRaces(string folder){
        MainMenu();botRaceChoice=-1;yield return Action("main.play");yield return Action("setup.human");yield return Action("setup.bot-race");yield return Action("setup.bot-race");
        Check(botRaceChoice==1,"setup allows an Orc opponent for Humans");yield return Shot(folder,"78-mixed-setup");
        startGold=1000;options.hdTextures=true;yield return Action("setup.start");testHoldAuthority=true;RenderMatch();
        Check(!match.TeamOrc[0]&&match.TeamOrc[1]&&builders[0].raw=="h00C"&&builders[1].raw=="h019","mixed match assigns race and Reforged builder per side");
        Check(match.BaseKinds.Length==11&&match.Kind("h029")>=0&&match.Kind("h000")>=0,"mixed catalog retains both full rosters");
        Check(match.PurchaseError(0,match.Kind("h029"))=="Постройка другой расы"&&match.PurchaseError(1,match.Kind("h000"))=="Постройка другой расы","construction rejects the opposing race");
        match.Gold[1]=0;var human=match.Spawn(0,match.Kind("h000"),new Vector2(-160,35));var grunt=match.Spawn(1,match.Kind("h029"),new Vector2(-155,35));human.onRoad=grunt.onRoad=true;human.cooldown=grunt.cooldown=1000;human.pose=grunt.pose="Stand";
        match.ApplyEffect(human,"A038",1);match.ApplyEffect(human,"A030",1);human.stun=3;RenderMatch();builderSelected=false;selectedUnit=human.id;controls.FocusWorld(World(new Vector2(-158,35)),25);yield return new WaitForSecondsRealtime(.3f);
        Check(units[human.id].raw=="hfoo"&&units[grunt.id].raw=="o002"&&units[human.id].reforged&&units[grunt.id].reforged,"mixed Human and Orc units render their own Reforged models");
        Check(human.effects.Count(Debuff)==2&&DebuffArt(human.effects[0])!=null,"poison and acid are visible debuffs with icons");yield return Shot(folder,"79-mixed-debuffs");
        human.effects.Clear();human.stun=0;Check(!human.effects.Any(Debuff),"expired debuffs leave no markers");
        MainMenu();yield return Action("main.play");yield return Action("setup.orc");botRaceChoice=0;yield return Action("setup.start");testHoldAuthority=true;RenderMatch();
        Check(match.TeamOrc[0]&&!match.TeamOrc[1]&&builders[0].raw=="h019"&&builders[1].raw=="h00C","reverse mixed matchup renders both builders");
        match.Gold[1]=1000;for(int i=0;i<60;i++)Tick(.05f);Check(match.Huts.Any(h=>h.team==1&&match.Lines[h.kind].building=="h000"),"Human bot builds a Human barracks in reverse mixed matchup");
        var bot=match.Spawn(1,match.Kind("h000"),new Vector2(150,35));var ours=match.Spawn(0,match.Kind("h029"),new Vector2(145,35));bot.onRoad=ours.onRoad=true;bot.pose=ours.pose="Stand";RenderMatch();
        Check(units[bot.id].raw=="hfoo"&&units[ours.id].raw=="o002","reverse mixed matchup uses correct models for both teams");yield return Shot(folder,"80-mixed-reverse");
        options.hdTextures=true;RenderMatch();Check(units[bot.id].reforged&&units[ours.id].reforged,"mixed matchup retains Reforged models");options.hdTextures=true;
        botRaceChoice=-1;Debug.Log("CF_MIXED_PLAYER_PASS");
    }
}
}
