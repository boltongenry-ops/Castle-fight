using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseCastleItems(string folder) {
        NewMatch();testHoldAuthority=true;match.Gold[0]=10000;match.Gold[1]=0;match.Elapsed=30;SelectCastle();
        yield return Action("shop.buy.I009");yield return Action("shop.buy.I001");yield return Action("shop.buy.I005");yield return Action("shop.buy.I003");yield return Action("shop.buy.I00A");yield return Action("shop.buy.I004");
        Check(selectedCastle&&match.ItemsBought==6&&match.Inventories[0].Used==5&&match.LegendaryLimit[0]==2,"castle command card buys real items, cheese does not fill slot");
        yield return Action("shop.buy.I006");Check(match.ItemsBought==6,"orb unlock enforced by castle UI");
        yield return Shot(folder,"16-castle-shop");
        match.Elapsed=900;yield return Action("shop.buy.I006");Check(match.Inventories[0].Used==6,"inventory shows six occupied physical slots");yield return Action("shop.back");Check(builderSelected&&!selectedCastle,"castle returns to worker");
        var ally=match.Spawn(0,match.Kind("h038"),new Vector2(-143,12));ally.hp=100;ally.mana=0;ally.onRoad=true;ally.cooldown=1000;
        var enemy=match.Spawn(1,0,new Vector2(-140,9));enemy.hp=enemy.maxHp=2000;enemy.onRoad=true;enemy.cooldown=1000;foreach(var line in match.Lines)line.speed=0;
        yield return Action("inventory.2");Check(selectedItem==2,"inventory click enters scroll area targeting");ExecuteItem(ally.position);Check(selectedItem==-1&&ally.hp==500&&ally.mana==200&&ally.effects.Any(e=>e.id=="A02G"),"point item applies heal mana and armor and consumes charge");
        int orb=match.Inventories[0].slots.FindIndex(s=>s.id=="I006");yield return Action("inventory."+orb);Check(selectedItem==orb,"orb inventory icon enters enemy targeting");Check(match.UseItem(0,orb,enemy.id,enemy.position,out _),"orb works against real enemy in player");selectedItem=-1;
        for(int i=0;i<26;i++)Tick(.05f);RenderMatch();Check(match.ItemShots>=1&&match.ItemsUsed==2,"passive staff fires and active item use counted in player");FocusBuilder();yield return Shot(folder,"17-castle-items");
        NewMatch();testHoldAuthority=true;match.Gold[1]=0;int[] kinds={0,match.Kind("h039"),1,match.Kind("h0A1")};
        for(int i=0;i<kinds.Length;i++){var unit=match.Spawn(0,kinds[i],new Vector2(-150+i*4,0));unit.onRoad=true;unit.pose="Stand";unit.cooldown=1000;}
        RenderMatch();builderSelected=false;selectedUnit=match.Units[0].id;controls.FocusWorld(World(new Vector2(-144,0)),23);
        int refUnits=reforgedUnits.Count(p=>p);Check(refUnits==22&&reforgedBuilders.Length==2&&reforgedBuildings.Length==32,"complete Reforged Human roster references saved");
        options.hdTextures=true;RenderMatch();yield return new WaitForSecondsRealtime(.3f);Time.timeScale=0;RenderMatch();yield return null;yield return Shot(folder,"18-human-classic");Time.timeScale=1;
        options.hdTextures=true;RenderMatch();yield return new WaitForSecondsRealtime(.3f);Time.timeScale=0;RenderMatch();yield return null;yield return Shot(folder,"19-human-hd");Time.timeScale=1;
        Check(units.Values.All(v=>v.reforged)&&builders.All(v=>v.reforged)&&castleViews.All(v=>v.reforged),"Reforged switch replaces geometry for units builders and castles");
        yield return ExerciseReforgedRoster(folder);
        Check(options.hdTextures,"Reforged is the only model mode");testHoldAuthority=false;
    }
}
}
