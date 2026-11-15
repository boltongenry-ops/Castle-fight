using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    void ChaosClear(){NatureClear();match.Huts.Clear();foreach(var b in match.Builders)b.jobs.Clear();match.ChaosFlames.Clear();match.HellCurses.Clear();match.Corpses.Clear();match.Pulses.Clear();}
    HumanMatch.Hut ChaosHut(int team,string raw,Vector2 at) {
        int k=match.Kind(raw);var l=match.Lines[k];var h=new HumanMatch.Hut {id=900000+match.Huts.Count,team=team,kind=k,position=at,paid=true,started=true,hp=HumanMatch.F(l.buildingStats,"hp"),maxHp=HumanMatch.F(l.buildingStats,"hp"),mana=HumanMatch.F(l.buildingStats,"max_mana"),spent=l.cost,baseCost=l.cost,spawnLeft=1000,income=l.cost*l.incomeRate};match.Huts.Add(h);return h;
    }
    IEnumerator ChaosBuildingChecks(string folder) {
        Check(new[]{"h01I","h008","h026","h01D","h03G","h009","h007"}.Select((raw,i)=>match.Kind(raw)==i).All(v=>v),"Chaos first seven saved catalog indices remain stable");
        Check(new[]{"A06R","A01W","A01J","A01E","A01G","A018","A0BL","A06S","A06T","A06U"}.All(id=>AbilityArt(id)),"Chaos special spell, curse and gift outcome icons resolve");
        NatureClear();match.LegendaryLimit[0]=4;match.Gold[0]=100000;match.Wood[0]=100000;var gift=QaBuild(match.Kind("h05I"),5,19);var erase=QaBuild(match.Kind("h01M"),9,19);QaSelect(erase);yield return Action("upgrade.h01W");
        Check(erase.spent==525&&Mathf.Abs(HumanMatch.F(match.Lines[erase.kind].buildingStats,"mana_regen")-2.66f)<.001f,"Eraser II keeps 525 invested gold, source regeneration and legendary slot");
        QaSelect(erase);yield return Action("upgrade.h01X");Check(erase.spent==700&&HumanMatch.F(match.Lines[erase.kind].buildingStats,"mana_regen")==4&&match.Lines[erase.kind].slots==1,"Eraser III accumulates 700 gold and upgrades to four mana per second");
        var volcano=QaBuild(match.Kind("h01F"),13,19);var fist=QaBuild(match.Kind("h01E"),17,19);var altar=QaBuild(match.Kind("h01C"),21,19);
        Check(gift.spent==200&&volcano.spent==340&&fist.spent==700&&altar.spent==230,"Chaos special constructions use the original source gold prices");
        Check(new[]{gift,erase,volcano,fist}.All(h=>Mathf.Abs(h.income-h.spent*.011f)<.001f)&&Mathf.Abs(altar.income-4.6f)<.001f,"Lethal buildings grant 1.1 percent income while the altar is a two percent producer");
        RenderMatch();Check(new[]{gift,erase,volcano,fist,altar}.All(h=>huts[h.id]&&!huts[h.id].reforged&&huts[h.id].BodyBounds().size.y>.05f),"Every new special construction renders textured classic geometry");
        ChaosHut(0,"h01M",new Vector2(-174,59));ChaosHut(0,"h01W",new Vector2(-157,59));RenderMatch();QaSelect(erase);controls.FocusWorld(World(new Vector2(-146,45)),58);yield return Shot(folder,"03-chaos-special-buildings");
        ChaosClear();var caster=ChaosHut(0,"h05I",Vector2.zero);var target=NatureUnit(1,"h000",Vector2.right*3);target.spellResist=1;match.Grant(target,"A10A");match.Grant(target,"A09L");target.abilityLevels["A10A"]=target.abilityLevels["A09L"]=2;
        Check(match.CastChaosBuilding(caster,"A06R",target.id)&&target.abilityLevels["A10A"]==1&&!target.effects.Any(),"Chaos Gift selects a magic-immune fighter but its large shield intercepts the outcome");
        for(int roll=0;roll<10;roll++) {
            ChaosClear();target=NatureUnit(1,"h000",Vector2.zero);target.mana=100;target.spellResist=0;float hp=target.hp,max=target.maxHp,gold=match.Gold[0];int kills=match.Kills;
            if(roll==1||roll==2)target.hp=target.maxHp=100000;
            match.ChaosGiftOutcome(target,0,roll);
            if(roll==0)Check(target.hp==0&&match.Gold[0]==gold&&!match.Corpses.Any(c=>c.unit==target),"Gift roll 0 kills without bounty or corpse");
            else if(roll==1||roll==2){float expected=(roll==1?50000:500)*match.Multiplier("normal",match.Lines[target.kind].armorType)*HumanMatch.ArmorFactor(target.armor);Check(Mathf.Abs(100000-target.hp-expected)<.02f,"Gift roll "+roll+" uses ordinary damage and armor");}
            else if(roll==3){Check(target.maxHp==max+400&&target.hp==target.maxHp&&target.abilities.Contains("A06U"),"Gift roll 3 permanently grants 400 HP, 25 damage and full healing");match.ChaosGiftOutcome(target,0,3);Check(target.maxHp==max+400,"Permanent Chaos Gift empowerment does not stack");}
            else if(roll==4){Check(match.Disarmed(target)&&match.Silenced(target)&&!match.Disabled(target)&&match.WeaponFor(target)==null,"Gift Hex blocks weapons and spells while allowing movement");}
            else if(roll==5){Check(match.Ethereal(target)&&Mathf.Abs(match.Stat(target,"speed")+.8f)<.001f,"Gift Banish retains ethereal state and eighty percent slow");}
            else if(roll==6)Check(target.stun==6&&target.hp<hp,"Gift Storm Bolt deals damage and stuns for six seconds");
            else if(roll==7)Check(target.hp==0&&match.Kills==kills&&!match.Corpses.Any(c=>c.unit==target)&&match.Units.Any(u=>u.hp>0&&u.team==0&&match.Lines[u.kind].unit=="n01S"),"Gift transformation creates allied Mutation without death rewards or death summons");
            else Check(target.hp==hp&&target.effects.Count==0,"Gift roll "+roll+" deliberately has no effect");
        }
        ChaosClear();target=NatureUnit(1,"h000",Vector2.zero);target.spellResist=1;match.ChaosGiftOutcome(target,0,4);Check(!match.Disarmed(target),"Magic immunity rejects the Hex outcome");
        ChaosClear();caster=ChaosHut(0,"h01M",Vector2.zero);target=NatureUnit(1,"h000",Vector2.right);var big=NatureUnit(1,"h000",Vector2.up);var small=NatureUnit(1,"h000",Vector2.left);var legendary=NatureUnit(1,"h000",Vector2.down);var far=NatureUnit(1,"h000",Vector2.right*4000);var other=NatureUnit(1,"h004",Vector2.up*2);
        match.Grant(big,"A10A");big.abilityLevels["A10A"]=2;match.Grant(small,"A10A");small.abilityLevels["A10A"]=1;match.Grant(legendary,"A070");match.Grant(far,"A08H");far.spellResist=1;
        Check(match.CastChaosBuilding(caster,"A01W",target.id)&&target.hp==0&&small.hp==0&&far.hp==0&&other.hp>0,"Erase affects every enemy copy of the selected rawcode including copies outside its initial pick filter");
        Check(big.hp>0&&!big.abilities.Contains("A10A")&&legendary.hp>0&&caster.mana==20,"Erase consumes the large shield, preserves legendary immunity and spends exactly 100 mana");
        ChaosClear();caster=ChaosHut(0,"h01F",Vector2.zero);target=NatureUnit(1,"h000",Vector2.right*3);target.hp=target.maxHp=10000;target.stun=100;float initial=target.hp;Check(match.CastChaosBuilding(caster,"A01J",target.id)&&caster.mana==3,"Volcano spends twelve mana and schedules the native delayed eruption");
        for(int i=0;i<15;i++)Tick(.05f);Check(target.hp==initial,"Eruption preparation causes no early damage");caster.hp=0;for(int i=0;i<182;i++)Tick(.05f);float expectedDamage=299*match.Multiplier("spells",match.Lines[target.kind].armorType)-match.Lines[target.kind].regen*8.85f;
        Check(Mathf.Abs(initial-target.hp-expectedDamage)<.15f&&match.ChaosFlames.Count==0,"Eruption survives the caster and completes eleven main pulses plus six residual pulses");
        ChaosClear();caster=ChaosHut(0,"h01F",Vector2.zero);var victims=Enumerable.Range(0,8).Select(i=>NatureUnit(1,"h000",Vector2.right*3)).ToArray();foreach(var u in victims){u.hp=u.maxHp=10000;u.stun=100;}
        match.CastChaosBuilding(caster,"A01J",victims[0].id);for(int i=0;i<21;i++)Tick(.05f);Check(victims.All(u=>Mathf.Abs(10000-u.hp-(150f/8*match.Multiplier("spells",match.Lines[u.kind].armorType)-match.Lines[u.kind].regen*.05f))<.1f),"The 150 eruption damage cap is divided across all eight eligible targets");
        ChaosClear();caster=ChaosHut(0,"h01F",Vector2.zero);var center=HumanMatch.CastlePosition(1);target=NatureUnit(1,"h000",center);target.stun=100;target.hp=target.maxHp=10000;var building=ChaosHut(1,"h000",center);float buildingHp=building.hp;match.CastleHp[1]=4000;var immune=NatureUnit(1,"h000",center);immune.spellResist=1;immune.stun=100;var flying=NatureUnit(1,"h015",center);flying.stun=100;
        match.CastChaosBuilding(caster,"A01J",target.id);for(int i=0;i<21;i++)Tick(.05f);
        Check(Mathf.Abs(buildingHp-building.hp-(10*match.Multiplier("spells","fortified")-HumanMatch.F(match.Lines[building.kind].buildingStats,"hp_regen")*.05f))<.1f&&match.CastleHp[1]<4000,"Eruption deals forty percent damage to buildings and the castle");Check(immune.hp==immune.maxHp&&flying.hp==flying.maxHp,"Eruption excludes magic-immune and flying units");
        ChaosClear();for(int stage=0;stage<4;stage++) {
            ChaosClear();match.Elapsed=new[]{0,1200,1500,2100}[stage];caster=ChaosHut(0,"h01E",Vector2.zero);building=ChaosHut(1,"h000",Vector2.right*6);float before=building.hp;
            Check(match.CastChaosBuilding(caster,"A01E",building.id)&&match.Shots.Last().damage==401&&match.Shots.Last().homing,"Hell Fist stage "+stage+" launches the original homing 401 damage projectile");
            match.Huts.Remove(caster);for(int i=0;i<4;i++)Tick(.05f);var curse=match.HellCurses.Single();Check(Mathf.Abs(curse.left-new[]{360,480,729,1200}[stage])<.21f&&curse.dps==stage+1&&building.hp<before,"Hell Fist stage "+stage+" survives source removal and uses native duration and damage per second");
        }
        float spawn=building.spawnLeft,mana=building.mana,income=match.Income(1);for(int i=0;i<20;i++)Tick(.05f);Check(building.spawnLeft==spawn&&match.Income(1)==income&&match.HellDisabled(building)>0,"Hell Fist disables production without removing building income");
        match.HellCurses.Single().left=.01f;Tick(.05f);Check(match.HellDisabled(building)==0&&building.spawnLeft<spawn,"Building production resumes when the curse expires");
        ChaosClear();match.Elapsed=1199.95f;caster=ChaosHut(0,"h01E",Vector2.zero);building=ChaosHut(1,"h000",Vector2.right*6);match.CastChaosBuilding(caster,"A01E",building.id);for(int i=0;i<4;i++)Tick(.05f);Check(match.Elapsed>1200&&match.HellCurses.Single().left<361&&match.HellCurses.Single().dps==1,"Hell Fist freezes its stage at launch across a minute threshold");
        ChaosClear();caster=ChaosHut(0,"h01E",Vector2.zero);ChaosHut(1,"h01M",Vector2.right*6);Check(!match.CastChaosBuilding(caster,"A01E")&&caster.mana==43,"Hell Fist cannot select legendary buildings or the castle and does not waste mana");
        ChaosClear();var curser=NatureUnit(0,"h01C",new Vector2(-146,32));target=NatureUnit(1,"h000",new Vector2(-140,32));target.hp=target.maxHp=100000;target.stun=100;
        Check(match.Cast(curser,"A01G",target.id)&&Mathf.Abs(match.MissChance(target)-.35f)<.001f&&target.effects.Any(e=>e.id=="A01G"&&e.left==45),"Altar fighter spends twenty mana to apply a visible forty-five-second curse with thirty-five percent misses");
        target.effects.First(e=>e.id=="A01G").left=1;curser.mana=100;Check(match.Cast(curser,"A01G",target.id)&&target.effects.First(e=>e.id=="A01G").left==45&&curser.mana==80,"Curse refreshes an existing debuff and pays its mana again");
        target.stun=0;target.cooldown=0;curser.stun=100;curser.hp=curser.maxHp=100000;target.position=curser.position+Vector2.right;target.attackOverride=1;int missed=match.ChaosMisses;for(int i=0;i<180;i++){target.cooldown=0;Tick(.1f);}Check(match.ChaosMisses>missed,"Cursed ordinary attacks actually miss during combat");
        RenderMatch();builderSelected=false;selectedHut=-1;selectedUnit=target.id;controls.FocusWorld(World(curser.position),22);yield return Shot(folder,"04-chaos-curse");Check(units[curser.id].raw=="n008"&&!units[curser.id].reforged,"Altar fighter renders its original classic model");
        ChaosClear();var mutation=NatureUnit(0,"n01S",new Vector2(-146,32));RenderMatch();selectedUnit=mutation.id;yield return Shot(folder,"05-chaos-mutation");Check(units[mutation.id].raw=="n01S"&&!units[mutation.id].reforged,"Mutation has its own classic model rather than a substituted fighter");
        ChaosClear();match.Elapsed=0;
    }
}
}

