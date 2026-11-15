using System;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    bool selectedCastle;int selectedItem=-1;public Texture2D castleIcon;
    Texture2D ItemArt(string id){int index=Array.IndexOf(itemIds,id);return index>=0?HdIcon(itemIcons[index]):null;}
    void SelectCastle(bool focus=true){ClearGroupSelection();selectedCastle=true;builderSelected=false;selectedHut=selectedUnit=-1;selectedItem=chosen=-1;targetedSpell=null;moveOrder=buildMenu=showRange=false;if(focus)controls.FocusWorld(World(HumanMatch.CastlePosition(0)),48);Notice("Замок · магазин предметов. F1 — строитель");}
    void DrawCastleCard() {
        if(castleIcon)GUI.DrawTexture(new Rect(348,590,109,93),HdIcon(castleIcon));
        Fill(new Rect(348,685,109,16),new Color(.07f,.14f,.03f));Fill(new Rect(350,687,105*match.CastleHp[0]/HumanMatch.CastleMax,12),new Color(.2f,.75f,.08f));GUI.Label(new Rect(348,684,109,18),Mathf.CeilToInt(match.CastleHp[0])+" / 5000",new GUIStyle(hudSmall){alignment=TextAnchor.MiddleCenter});
        GUI.Label(new Rect(491,578,306,31),"Замок · магазин",new GUIStyle(hudText){alignment=TextAnchor.MiddleCenter});
        GUI.DrawTexture(new Rect(504,623,36,36),CombatIcon(true,match.CastleArmorType),ScaleMode.ScaleToFit);GUI.Label(new Rect(550,622,245,44),"Броня: "+match.CastleArmorFor(0)+" · "+ArmorLabel(match.CastleArmorType)+"\nВосстановление: 0,5 здоровья / сек.",hudSmall);
        if(new Rect(504,623,36,36).Contains(HudPointer)){hudTip=ArmorTable(match.CastleArmorType);hudTipTable=true;}
        GUI.Label(new Rect(504,669,286,20),"Предметы получает ваш рабочий",hudSmall);GUI.Label(new Rect(504,691,286,20),"Запасы магазина пополняются со временем",new GUIStyle(hudSmall){fontSize=11});
        for(int i=0;i<match.ShopItems.Count;i++) {
            var item=match.ShopItems[i];string id=(string)item["id"];var stock=match.Stock(0,i);int col=i%3,row=i/3;var rect=Slot(col,row);string error=match.ItemPurchaseError(0,i);
            string tip=(string)item["name"]+"\n"+item["gold"]+" зол. · запас "+stock.count+" / "+item["stock_max"]+"\n"+(string)item["desc"]+(error==""?"\nНажмите, чтобы купить":"\n"+error);
            if(ArtButton("shop.buy."+id,rect,ItemArt(id),stock.count.ToString(),tip)){if(onlineMatch)NetworkCommand(PcServerReplica.BuyItem(i));else{bool bought=match.BuyItem(0,i,out var why);Notice(bought?"Куплено: "+item["name"]:why);}}
            if(error!="")Fill(new Rect(rect.x,rect.y,rect.width,rect.height-16),new Color(0,0,0,.45f));
        }
        if(ArtButton("builder.buy-rescue",Slot(3,0),Extra("rescue"),"+","Дополнительный спасительный удар\n1000 золота")){if(onlineMatch)NetworkCommand(PcServerReplica.Command("buy_rescue"));else Notice(match.BuyRescue(0)?"Получен дополнительный заряд":"Нужно 1000 свободного золота");}
        if(ArtButton("shop.back",Slot(3,2),CommandIcon(8),"F1","Вернуться к рабочему"))FocusBuilder();
    }
    void DrawItemInventory() {
        var bag=match.Inventories[0];GUI.Label(new Rect(824,560,141,21),"Инвентарь · "+bag.Used+"/6",hudSmall);
        int cell=0;foreach(var slot in bag.slots) {
            var item=match.ItemCatalog[slot.id];var rect=InventorySlot(cell);float cooldown=Mathf.Max(slot.cooldown,bag.cooldowns.TryGetValue(slot.id,out var shared)?shared:0);
            string tip=item["name"]+"\n"+item["desc"]+(cooldown>0?"\nПерезарядка: "+Mathf.CeilToInt(cooldown)+" с":"")+"\nNum "+(cell+1)+" · зарядов / предметов: "+slot.count;
            if(ArtButton("inventory."+cell,rect,ItemArt(slot.id),slot.count>1?slot.count.ToString():"",tip,selectedItem==cell))BeginItem(cell);
            if(cooldown>0){Fill(rect,new Color(0,0,0,.55f));GUI.Label(rect,Mathf.CeilToInt(cooldown).ToString(),new GUIStyle(hudSmall){alignment=TextAnchor.MiddleCenter});}cell++;
        }
    }
    void BeginItem(int index) {
        if(index<0||index>=match.Inventories[0].slots.Count)return;var slot=match.Inventories[0].slots[index];var item=match.ItemCatalog[slot.id];
        if((string)item["mode"]=="passive"){Notice("Пассивный предмет работает у рабочего: "+item["name"]);return;}
        if(Mathf.Max(slot.cooldown,match.Inventories[0].cooldowns.TryGetValue(slot.id,out var left)?left:0)>0){Notice("Предмет перезаряжается");return;}
        ClearGroupSelection();selectedItem=index;selectedCastle=false;builderSelected=true;chosen=-1;buildMenu=moveOrder=false;targetedSpell=null;Notice((string)item["mode"]=="target"?"Укажите вражеского бойца ЛКМ":"Укажите область действия ЛКМ");
    }
    void ExecuteItem(Vector2 point) {
        int target=-1;if(selectedItem>=0&&selectedItem<match.Inventories[0].slots.Count) {
            var item=match.ItemCatalog[match.Inventories[0].slots[selectedItem].id];
            if((string)item["mode"]=="target") {float best=float.MaxValue;foreach(var u in match.Units.Where(v=>v.hp>0)) {var screen=Camera.main.WorldToScreenPoint(World(u.position)+Vector3.up*(match.Height(u)+1));float distance=Vector2.Distance(ActivePointer,screen);if(screen.z>0&&distance<Mathf.Max(30,45*guiScale)&&distance<best){target=u.id;best=distance;}}}
        }
        if(onlineMatch){try{NetworkCommand(PcServerReplica.UseItem(selectedItem,Mathf.Max(0,target),point));selectedItem=-1;}catch(ArgumentOutOfRangeException){Notice("Точка вне карты");}return;}bool ok=match.UseItem(0,selectedItem,target,point,out var why);Notice(ok?"Предмет применён":why);if(ok)selectedItem=-1;
    }
}
}
