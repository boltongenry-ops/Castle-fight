using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class HumanMatch {
    public sealed class ItemSlot {public string id;public int count=1;public float cooldown,accumulator;}
    public sealed class ItemStock {public int count;public float next;}
    public sealed class ItemBoost {public float power,left;public string id;}
    public sealed class Inventory {
        public readonly List<ItemSlot> slots=new List<ItemSlot>();
        public readonly Dictionary<string,ItemStock> stocks=new Dictionary<string,ItemStock>();
        public readonly Dictionary<string,float> cooldowns=new Dictionary<string,float>();
        public readonly List<ItemBoost> boosts=new List<ItemBoost>();
        public int Used=>slots.Sum(s=>s.count);
        public float Boost=>boosts.Where(b=>b.left>0).Select(b=>b.power).DefaultIfEmpty(0).Max();
    }
    public JArray ShopItems;public JObject ItemCatalog;public readonly Inventory[] Inventories={new Inventory(),new Inventory()};public int ItemShots,ItemsBought,ItemsUsed;
    void InitInventory(JObject data){ShopItems=(JArray)data["shopItems"]??new JArray();ItemCatalog=(JObject)data["itemCatalog"]??new JObject();foreach(var bag in Inventories)foreach(var item in ShopItems)bag.stocks[(string)item["id"]]=new ItemStock {next=F(item,"stock_start")};}
    public int ShopIndex(string id)=>ShopItems.ToList().FindIndex(v=>(string)v["id"]==id);
    public ItemStock Stock(int team,int index) {
        var item=ShopItems[index];var record=Inventories[team].stocks[(string)item["id"]];int max=(int)item["stock_max"];
        if(record.count<max&&Elapsed>=record.next){float regen=Mathf.Max(.001f,F(item,"stock_regen"));int count=Mathf.FloorToInt((Elapsed-record.next)/regen)+1;record.count=Mathf.Min(max,record.count+count);record.next+=count*regen;}return record;
    }
    public string ItemPurchaseError(int team,int index) {
        if(team<0||team>1||index<0||index>=ShopItems.Count)return "Предмет не продаётся в замке";if(Finished)return "Матч завершён";
        if(!ChallengeShopAllowed(team))return "Испытание: покупки в замке недоступны";
        var item=ShopItems[index];if(Elapsed<F(item,"unlock_time"))return "Доступно после 15 минут раунда";
        var stock=Stock(team,index);if(stock.count<=0)return "Предмет появится через "+Mathf.CeilToInt(Mathf.Max(0,stock.next-Elapsed))+" с";
        if(AvailableGold(team)<F(item,"gold"))return "Недостаточно золота";if(AvailableWood(team)<F(item,"wood"))return "Недостаточно дерева";
        return (string)item["mode"]!="purchase"&&Inventories[team].Used>=6?"Инвентарь заполнен: 6 слотов":"";
    }
    public bool BuyItem(int team,int index,out string error) {
        error=ItemPurchaseError(team,index);if(error!="")return false;var item=ShopItems[index];var bag=Inventories[team];string id=(string)item["id"];var stock=Stock(team,index);
        if(stock.count==(int)item["stock_max"])stock.next=Elapsed+F(item,"stock_regen");stock.count--;Gold[team]-=F(item,"gold");Wood[team]-=(int?)item["wood"]??0;ItemsBought++;
        if((string)item["mode"]=="purchase") {
            if(id=="I004")LegendaryLimit[team]++;else bag.boosts.Add(new ItemBoost {id=id,power=F(item,"power"),left=F(item,"duration")});
            PulseAt(Builders[team].position,3,Color.yellow,(string)item["name"]);return true;
        }
        var slot=bag.slots.FirstOrDefault(s=>s.id==id);if(slot==null){slot=new ItemSlot {id=id,cooldown=bag.cooldowns.TryGetValue(id,out float left)?left:0};bag.slots.Add(slot);}else slot.count++;
        if(id=="I001"&&slot.count>=4){slot.count-=4;if(slot.count==0)bag.slots.Remove(slot);var multi=bag.slots.FirstOrDefault(s=>s.id=="I002");if(multi!=null)multi.count++;else bag.slots.Add(new ItemSlot {id="I002"});PulseAt(Builders[team].position,2,Color.cyan,"Мультипосох");}return true;
    }
    public float ItemPassive(int team,string stat,Unit receiver=null) {
        float result=0;foreach(var slot in Inventories[team].slots){var item=ItemCatalog[slot.id];if(receiver!=null&&Vector2.Distance(Builders[team].position,receiver.position)>F(item,"aura_range",float.PositiveInfinity))continue;result=Mathf.Max(result,F(item,stat));}return result;
    }
    public float InventoryDamage(Unit u)=>1+ItemPassive(u.team,"damage_bonus",u)+Inventories[u.team].Boost;
    void TickInventory(float dt) {
        for(int team=0;team<2;team++) {
            var bag=Inventories[team];foreach(var b in bag.boosts)b.left-=dt;bag.boosts.RemoveAll(b=>b.left<=0);
            using(var snapshot035_1=SimulationBuffers035.Take(bag.cooldowns.Keys))foreach(string id in snapshot035_1)bag.cooldowns[id]=Mathf.Max(0,bag.cooldowns[id]-dt);
            foreach(var slot in bag.slots) {
                slot.cooldown=Mathf.Max(0,slot.cooldown-dt);var item=ItemCatalog[slot.id];float rate=F(item,"rate")*slot.count;
                if(rate>0){float period=1/rate;slot.accumulator=Mathf.Min(slot.accumulator+dt,period*8);while(slot.accumulator+.000001f>=period){slot.accumulator-=period;FireItemStaff(team,item);}}
                float repair=F(item,"repair")*slot.count*dt;if(repair<=0)continue;float radius=F(item,"range",23.333333f);var position=Builders[team].position;
                foreach(var h in Huts)if(h.team==team&&h.hp>0&&Vector2.Distance(h.position,position)<=radius+.00001f)h.hp=Mathf.Min(h.maxHp,h.hp+repair);
                if(CastleHp[team]>0&&Vector2.Distance(CastlePosition(team),position)<=radius+.00001f)CastleHp[team]=Mathf.Min(CastleMax,CastleHp[team]+repair);
            }
        }
    }
    void FireItemStaff(int team,JToken item) {
        var position=Builders[team].position;var targets=Units.Where(u=>u.hp>0&&u.team!=team&&u.spellResist<1&&Vector2.Distance(u.position,position)<=F(item,"range")).ToArray();if(targets.Length==0)return;
        var victim=targets[Mathf.Min(targets.Length-1,(int)(Roll()*targets.Length))];var weapon=new Weapon {type="missile",attack="magic",homing=true,speed=1000f/30,air=true,ground=true};
        var shot=AddShot(team,0,-100-team,victim.id,position,victim.position,F(item,"damage"),weapon,0,3);shot.spell=true;shot.startHeight=1;ItemShots++;
    }
    public bool UseItem(int team,int slotIndex,int target,Vector2 point,out string error) {
        error="";if(team<0||team>1||Finished){error="Матч завершён";return false;}var bag=Inventories[team];if(slotIndex<0||slotIndex>=bag.slots.Count){error="Нет предмета в слоте";return false;}
        var slot=bag.slots[slotIndex];var item=ItemCatalog[slot.id];float cd=Mathf.Max(slot.cooldown,bag.cooldowns.TryGetValue(slot.id,out float shared)?shared:0);
        if(cd>0){error="Перезарядка: "+Mathf.CeilToInt(cd)+" с";return false;}
        if((string)item["mode"]=="passive"){error="Пассивный предмет: работает из инвентаря";return false;}
        var position=Builders[team].position;
        if(slot.id=="I006") {
            var victim=FindUnit(target);if(victim==null||victim.team==team||victim.spellResist>=1||VisionTest!=null&&!VisionTest(team,victim.position)){error="Выберите вражеского бойца";return false;}
            if(Vector2.Distance(position,victim.position)>F(item,"range")+.00001f){error="Цель вне радиуса строителя";return false;}
            var used=new HashSet<int>();Vector2 from=position;float damage=F(item,"damage");
            for(int hop=0;hop<(int)item["hops"]&&victim!=null;hop++) {
                used.Add(victim.id);Shots.Add(new Shot {id=nextId++,team=team,kind=4,start=from,end=victim.position,duration=.35f,startHeight=1,endHeight=Height(victim)+1});DamageUnit(victim,damage,team);from=victim.position;damage*=F(item,"falloff");
                victim=Units.Where(u=>u.hp>0&&u.team!=team&&u.spellResist<1&&!used.Contains(u.id)&&Vector2.Distance(from,u.position)<=F(item,"hop_range")).OrderBy(u=>(u.position-from).sqrMagnitude).FirstOrDefault();
            }
        } else if(slot.id=="I005") {
            if(float.IsNaN(point.x)||float.IsNaN(point.y)||float.IsInfinity(point.x)||float.IsInfinity(point.y)||Vector2.Distance(position,point)>F(item,"range")){error="Укажите область действия свитка";return false;}
            foreach(var u in Units)if(u.hp>0&&u.team==team&&Vector2.Distance(point,u.position)<=F(item,"area")){u.hp=Mathf.Min(u.maxHp,u.hp+F(item,"heal"));u.mana=Mathf.Min(u.maxMana,u.mana+F(item,"mana"));u.effects.RemoveAll(e=>e.id=="A02G");u.effects.Add(new Effect {id="A02G",group="A02G",left=F(item,"duration"),armor=F(item,"armor")});}
            PulseAt(point,F(item,"area"),new Color(.4f,1,.65f),"Каменный свиток");
        } else {error="У предмета нет активного действия";return false;}
        slot.cooldown=F(item,"cooldown");bag.cooldowns[slot.id]=slot.cooldown;if((bool?)item["consumable"]==true&&--slot.count<=0)bag.slots.Remove(slot);ItemsUsed++;return true;
    }
}
}
