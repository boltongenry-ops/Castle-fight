using System;
using System.Linq;
using UnityEngine;
namespace CastleFight {
// Godot aspects.gd parity for the Nature Force aspects:
//   nature_resilience ("Буйство природы") - +20 п.п. сопротивления магии у бойцов
//   (предел 80%) и +10% золотой цены производящих зданий и их улучшений без
//   изменения дохода, дерева и интервала выпуска.
//   nature_forest ("Живой лес") - особые и легендарные здания природы готовят
//   заклинания на 25% чаще и восстанавливают ману на 25% быстрее; каждое рабочее
//   здание лечит союзных бойцов в радиусе 450 на 2 HP/с, суммарно не более 6 HP/с.
public sealed partial class HumanMatch {
    public int NatureAspect=>TeamNature[0]||TeamNature[1]?0:-1;
    public bool NatureResilience(int team)=>TeamNature[team]&&Aspects[team]==0;
    public bool NatureForest(int team)=>team>=0&&team<2&&TeamNature[team]&&Aspects[team]==1;
    public bool ForestBuilding(Hut hut){var l=Lines[hut.kind];return l.category=="special"||l.category=="lethal";}
    public int GoldPrice(int team,int kind) {
        if(team<0||team>1||kind<0||kind>=Lines.Length)return 0;
        var l=Lines[kind];int price=l.cost;
        if(NatureResilience(team)&&!string.IsNullOrEmpty(l.unit))price=Mathf.CeilToInt(l.cost*1.1f);
        return price;
    }
    public float BuildingCooldown(Hut hut,float cooldown)=>UndeadArmy(hut.team)&&UndeadRaiser(hut)?cooldown/1.4f:NatureForest(hut.team)&&ForestBuilding(hut)?cooldown/1.25f:cooldown;
    public float BuildingManaRegen(Hut hut,float regen)=>TeamPandaren[hut.team]&&Aspects[hut.team]==1&&Lines[hut.kind].building=="pK01"?regen*1.2f:UndeadArmy(hut.team)&&UndeadRaiser(hut)?regen*1.4f:NatureForest(hut.team)&&ForestBuilding(hut)?regen*1.25f:regen;
    public void ApplyNatureResilience(Unit unit) {
        if(!NatureResilience(unit.team)||unit.spellResist>=.8f)return;
        unit.spellResist=Mathf.Min(.8f,unit.spellResist+.2f);
    }
    // Godot aspects.gd ticks the forest aura at 2 Hz with a step capped at one second.
    public void TickNatureForest(float step) {
        for(int team=0;team<2;team++) {
            if(!NatureForest(team))continue;
            var huts=Huts.Where(h=>h.team==team&&h.paid&&h.hp>0&&!h.paused).ToArray();
            if(huts.Length==0)continue;
            const float radius=450f/30;
            foreach(var unit in Units) {
                if(unit.hp<=0||unit.team!=team||unit.hp>=unit.maxHp)continue;
                int sources=0;foreach(var hut in huts)if((hut.position-unit.position).sqrMagnitude<=radius*radius)sources++;
                if(sources==0)continue;
                float rate=Mathf.Min(6f,2f*sources);
                unit.hp=Mathf.Min(unit.maxHp,unit.hp+rate*step);
            }
        }
    }
}
}
