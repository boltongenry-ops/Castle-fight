using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
// Offline purchasing policy. Uses ordinary orders/prices and its own observations;
// combat, economy and the network authority are independent of this policy.
public sealed partial class HumanMatch {
    sealed class BotContact {public float seen,weight,resist;public string armor;public bool air;}
    sealed class BotOption {public int kind;public Hut hut;public float score,price;}
    readonly Dictionary<int,BotContact> botContacts=new Dictionary<int,BotContact>();
    readonly List<Unit> botVisible=new List<Unit>();readonly List<BotOption> botOptions=new List<BotOption>();
    int[] botRoots;float[] botTaste;uint botRandom;float botAir,botDanger,botAirCoverage;int botProducers,botArmy;
    string botPlan="Разведка и развитие";public string BotPlan=>botPlan;
    float BotRandom(){if(botRandom==0)botRandom=(uint)Guid.NewGuid().GetHashCode()|1u;botRandom^=botRandom<<13;botRandom^=botRandom>>17;botRandom^=botRandom<<5;return (botRandom&0xFFFFFF)/16777216f;}
    float BotGold()=>Mathf.Max(0,Gold[1]-Huts.Where(h=>h.team==1&&!h.paid).Sum(h=>GoldPrice(1,h.kind)));
    void InitBotPolicy(){
        if(botRoots!=null)return;botRoots=Enumerable.Repeat(-1,Lines.Length).ToArray();botTaste=new float[Lines.Length];
        foreach(int root in Enumerable.Range(0,Lines.Length).Where(k=>teamBaseBuildings[1].Contains(Lines[k].building))){
            var todo=new Stack<int>();todo.Push(root);while(todo.Count>0){int k=todo.Pop();if(botRoots[k]>=0)continue;botRoots[k]=root;foreach(string child in Lines[k].upgrades){int n=Kind(child);if(n>=0)todo.Push(n);}}
        }
        for(int k=0;k<Lines.Length;k++)botTaste[k]=.78f+BotRandom()*.44f;
    }
    bool BotSeesPoint(Vector2 point){
        if(VisionTest!=null&&!VisionTest(1,point))return false;
        if(InsideBase(1,point)||Vector2.Distance(point,Builders[1].position)<=800f/30)return true;
        foreach(var u in Units)if(u.team==1&&u.hp>0&&Vector2.Distance(point,u.position)<=F(Lines[u.kind].stats,"sight",800f/30))return true;
        foreach(var h in Huts)if(h.team==1&&h.hp>0&&h.paid&&Vector2.Distance(point,h.position)<=F(Lines[h.kind].buildingStats,"sight",1000f/30))return true;
        foreach(var orb in NorthernOrbs)if(orb.team==1&&Vector2.Distance(point,orb.position)<=700f/30)return true;
        foreach(var zone in NightZones)if(zone.team==1&&zone.age<8&&Vector2.Distance(point,zone.point)<=1100f/30)return true;
        return false;
    }
    bool BotSeesUnit(Unit unit)=>unit.team==0&&unit.hp>0&&!Hidden(unit)&&!Invulnerable(unit)&&NightCanDetect(1,unit)&&(ElvenReveals(unit,1)||BotSeesPoint(unit.position));
    void ObserveBot(){
        botVisible.Clear();botDanger=0;
        foreach(var u in Units){
            if(!BotSeesUnit(u))continue;botVisible.Add(u);var line=Lines[u.kind];float weight=Mathf.Clamp((float)Math.Sqrt(Mathf.Max(1,u.maxHp))/20,.4f,4);
            botContacts[u.id]=new BotContact{seen=Elapsed,weight=weight,armor=line.armorType,air=Flying(u),resist=u.spellResist};
            if(u.position.x>2016f/30&&line.unit!="YTct"&&line.unit!="n02E")botDanger+=weight*(Vector2.Distance(u.position,CastlePosition(1))<40?1.6f:.7f);
        }
        foreach(int id in botContacts.Where(pair=>Elapsed-pair.Value.seen>120).Select(pair=>pair.Key).ToArray())botContacts.Remove(id);
        if(botContacts.Count>512)foreach(int id in botContacts.OrderBy(pair=>pair.Value.seen).Take(botContacts.Count-512).Select(pair=>pair.Key).ToArray())botContacts.Remove(id);
        float air=0,total=0;foreach(var c in botContacts.Values){float w=c.weight*Mathf.Max(.1f,1-(Elapsed-c.seen)/120);total+=w;if(c.air)air+=w;}botAir=total>0?air/total:0;
        botProducers=0;botAirCoverage=0;float output=0;botArmy=ArmyCount(1);
        foreach(var h in Huts.Where(h=>h.team==1&&h.hp>0))if(BotCombatProducer(h.kind)){
            botProducers++;float power=BotProductionPower(h.kind)/Mathf.Max(15,Interval(h));output+=power;if(BotWeapons(h.kind).Any(w=>w.air))botAirCoverage+=power;
        }
        botAirCoverage=output>0?botAirCoverage/output:0;
    }
    int BotActorKind(int k){var l=Lines[k];string raw=l.unit=="u00F"?"h030":l.unit=="e00E"?"n02K":l.unit;if(raw==l.unit)return k;int actor=Array.FindIndex(Lines,x=>x.unit==raw);return actor>=0?actor:k;}
    Weapon[] BotWeapons(int k)=>Lines[BotActorKind(k)].weapons;
    bool BotCombatProducer(int k)=>Lines[k].Producer&&(bool?)Lines[BotActorKind(k)].stats["invulnerable"]!=true&&Lines[k].unit!="n01U"&&Lines[k].unit!="n02E";
    int BotCopies(int k)=>Huts.Count(h=>h.team==1&&h.hp>0&&(botRoots[h.kind]>=0?botRoots[h.kind]:h.kind)==(botRoots[k]>=0?botRoots[k]:k));
    float BotCounter(Weapon w){
        if(botContacts.Count==0)return w.ground?1:.35f;float sum=0,total=0;
        foreach(var c in botContacts.Values){float weight=c.weight*Mathf.Max(.1f,1-(Elapsed-c.seen)/120);total+=weight;if(c.air?!w.air:!w.ground)continue;float hit=Multiplier(w.attack,c.armor);if(w.attack=="magic"||w.attack=="spells")hit*=1-c.resist;sum+=weight*hit;}
        return total>0?Mathf.Max(.04f,sum/total):1;
    }
    bool BotHasRole(int k,params string[] kinds)=>Strings(Lines[BotActorKind(k)].stats["abilities"]).Any(id=>kinds.Contains((string)Abilities[id]?["kind"]));
    float BotProductionPower(int k){
        var actor=Lines[BotActorKind(k)];float dps=BotWeapons(k).Select(w=>w.damage/Mathf.Max(.3f,w.cooldown)*BotCounter(w)).DefaultIfEmpty(0).Max();
        float magic=botContacts.Count==0?1:botContacts.Values.Average(c=>1-c.resist);float critical=1,poison=0;
        foreach(string id in Strings(actor.stats["abilities"])){
            var cfg=Abilities[id];string kind=(string)cfg?["kind"];
            if(kind=="critical")critical=Mathf.Max(critical,1+F(cfg?["profile"],"chance")*Mathf.Max(0,F(cfg?["profile"],"multiplier",1)-1));
            if(kind=="poison")poison=Mathf.Max(poison,F(cfg,"dps")*.6f*magic);
            if(kind=="summon"||kind=="undead_raise_unit"||kind=="split"||kind=="panda_split")dps+=12;
            if(kind=="wave"||kind=="chain"||kind=="damage"||kind=="nova"||kind=="north_nova"||kind=="warlock_blast"||kind=="auto_fire")dps+=Mathf.Min(30,F(cfg,"power")/Mathf.Max(5,F(cfg,"cd",10)))*magic;
            if(kind=="barrage"||kind=="cleave"||kind=="pulverize")dps*=1.2f;
        }
        dps=dps*critical+poison;
        if(BotHasRole(k,"heal","regen","elven_heal","panda_heal"))dps+=8;
        if(BotHasRole(k,"aura","buff","dispel","orc_spell_steal","panda_guard"))dps+=4;
        float hp=Mathf.Max(20,actor.hp)*(1+Mathf.Clamp(actor.armor,0,30)*.04f);
        float power=(float)Math.Sqrt(hp*Mathf.Max(1,dps));if(actor.production.Length>1)power*=1.6f;if(Lines[k].unit=="e00E")power*=1.6f;
        float antiAir=Mathf.Clamp01((botAir-botAirCoverage)*2);power*=BotWeapons(k).Any(w=>w.air)?1+antiAir*2:1-antiAir*.7f;
        return power;
    }
    bool BotAllowed(int k,Hut replacing){
        if(k<0||k>=Lines.Length||!teamBuildings[1].Contains(Lines[k].building)||replacing==null&&!teamBaseBuildings[1].Contains(Lines[k].building))return false;
        if(ChallengePurchaseError(1,k,replacing?.kind??-1)!=""||AvailableWood(1)<Lines[k].woodCost)return false;
        return LegendaryUsed(1)+Mathf.Max(0,Lines[k].slots-(replacing==null?0:Lines[replacing.kind].slots))<=LegendaryLimit[1];
    }
    float BotSupportScore(int k){
        var line=Lines[k];int copies=BotCopies(k);if(copies>0)return .7f/(copies+1);
        if(line.unit=="n01U")return botProducers>=5?7:0;
        if(line.building=="h008")return botArmy>=130?9:botProducers>=6&&botDanger<2?3.5f:0;
        if(line.building=="h062")return Huts.Count(h=>h.team==1&&h.paid&&F(Lines[h.kind].buildingStats,"max_mana")>0)>=2?10:0;
        if(line.building=="h09T")return Huts.Count(h=>h.team==1&&h.hp>0)>=5?10:0;
        if(line.building=="h063")return botProducers>=5?11:0;
        if(line.building=="h05Z")return botProducers>=4?8:0;
        float score=botProducers<3?0:4.5f+Mathf.Min(5,botProducers*.5f);
        foreach(string id in line.abilities){string kind=(string)Abilities[id]?["kind"];
            if(kind=="erase"||kind=="map_erase"||kind=="execute"||kind=="control")score+=botDanger>2?5:2;
            if(kind=="heal"||kind=="regen"||kind=="panda_pool"||kind=="mech_shield")score+=botArmy>=8?3:0;
            if(kind=="nerub_brood"||kind=="nerub_acid"||kind=="panda_keg")score+=2;
        }
        if(line.slots>0)score+=3;return score;
    }
    float BotScore(int k,Hut replacing){
        var line=Lines[k];int copies=BotCopies(k);float price=Mathf.Max(30,GoldPrice(1,k)),score;
        if(BotCombatProducer(k)){
            float output=BotProductionPower(k)/(15+(replacing==null?line.cost:replacing.spent+line.cost)/20f);
            if(replacing==null){
                score=output*4/(1+copies*.85f);
                if(copies>=2)score/=(1+(copies-1)*.8f);
                if(botProducers<3&&copies==0)score*=1.35f;
                if(botArmy>=135)score*=.15f;
            }else{
                float prior=BotProductionPower(replacing.kind)/Interval(replacing);score=Mathf.Max(0,output-prior)*4;
                score+=botProducers>=3?4+Mathf.Min(4,copies):0;
                if(botArmy>=115)score+=8;
            }
            if(BotHasRole(k,"heal","regen","elven_heal","panda_heal")&&!Huts.Any(h=>h.team==1&&h.hp>0&&BotHasRole(h.kind,"heal","regen","elven_heal","panda_heal")))score*=botProducers>=2?1.3f:.6f;
        }else if(line.buildingWeapons.Length>0){
            float dps=line.buildingWeapons.Max(w=>w.damage/Mathf.Max(.3f,w.cooldown)*BotCounter(w));
            score=(3+dps*.2f)*(botDanger>2?Mathf.Min(4,1+botDanger/3):botProducers>=4?.7f:.15f)/(1+copies*1.5f);
            if(replacing!=null)score*=.85f;
        }else score=BotSupportScore(k);
        score*=200/(price+100);score+=line.cost*line.incomeRate/price*(botDanger>2?15:90)/(1+copies*.35f);
        if(line.slots>0&&botProducers<3)score*=.25f;
        return Mathf.Max(0,score)*botTaste[botRoots[k]>=0?botRoots[k]:k];
    }
    void ThinkBot(){
        if(Finished||NetworkReplica)return;InitBotPolicy();ObserveBot();
        int pending=Builders[1].jobs.Count;if(pending>=(difficulty==2?2:1)){botPlan="Завершает строительство";return;}
        if(pending==0&&CastleHp[1]<CastleHealthLimit(1)*.45f&&botDanger>0){RepairCastle(1);botPlan="Ремонтирует замок под давлением";return;}
        if(Builders[1].repairing&&CastleHp[1]<CastleHealthLimit(1)*.8f&&botDanger>0)return;
        botOptions.Clear();
        for(int k=0;k<Lines.Length;k++)if(BotAllowed(k,null))botOptions.Add(new BotOption{kind=k,score=BotScore(k,null),price=GoldPrice(1,k)});
        foreach(var hut in Huts.Where(h=>h.team==1&&h.paid&&h.hp>0&&!h.paused))foreach(string raw in Lines[hut.kind].upgrades){int k=Kind(raw);if(BotAllowed(k,hut))botOptions.Add(new BotOption{kind=k,hut=hut,score=BotScore(k,hut),price=GoldPrice(1,k)});}
        var ranked=botOptions.Where(o=>o.score>0).OrderByDescending(o=>o.score).ThenBy(o=>o.kind).ToArray();if(ranked.Length==0){botPlan="Нет доступных построек";return;}
        float gold=BotGold();var best=ranked[0];var affordable=ranked.FirstOrDefault(o=>o.price<=gold+.001f);
        float wait=(best.price-gold)/Mathf.Max(.5f,Income(1)/10);
        if(best.price>gold&&botDanger<3&&botProducers>=2&&wait<(difficulty==0?90:200)&&(affordable==null||best.score>affordable.score*1.08f)){botPlan="Копит на "+Lines[best.kind].name;return;}
        if(TryBotShop(gold))return;
        if(affordable==null){botPlan="Копит на "+Lines[best.kind].name;return;}
        if(difficulty==0){var top=ranked.Where(o=>o.price<=gold+.001f&&o.score>=affordable.score*.8f).Take(3).ToArray();affordable=top[(int)(BotRandom()*top.Length)];}
        if(affordable.hut!=null){if(Upgrade(1,affordable.hut.id,affordable.kind,out _)){botChoice++;botPlan="Улучшает: "+Lines[affordable.kind].name;}return;}
        if(BotPlace(affordable.kind)){botChoice++;botPlan=(botAir>.35f&&BotWeapons(affordable.kind).Any(w=>w.air)?"Против воздуха: ":"Строит: ")+Lines[affordable.kind].name;}
    }
    bool BotPlace(int k){
        var line=Lines[k];bool tower=line.buildingWeapons.Length>0;float desiredX=tower?3:line.Producer?18:21;float best=float.MaxValue;int bx=-1,by=-1;
        var occupied=new bool[32,24];foreach(var h in Huts)if(h.team==1&&h.x>=0&&h.x<32&&h.y>=0&&h.y<24)occupied[h.x,h.y]=true;
        for(int y=0;y<24;y++)for(int x=0;x<32;x++){
            if(!LegalCell(1,x,y)||occupied[x,y]||Vector2.Distance(Plot(1,x,y),CastlePosition(1))<CastleCollision+Cell*.5f)continue;float dy=Mathf.Min(Mathf.Abs(y-11),Mathf.Abs(y-12));float score=Mathf.Abs(x-desiredX)*1.3f+dy*1.4f+Vector2.Distance(Builders[1].position,Plot(1,x,y))*.015f;
            if(score<best){best=score;bx=x;by=y;}
        }
        return bx>=0&&TryBuild(1,k,bx,by,out _);
    }
    bool TryBotShop(float gold){
        if(botProducers<5||Builders[1].jobs.Count>0||botDanger>3||gold<650)return false;
        string wanted=botProducers>=10&&!Inventories[1].slots.Any(s=>s.id=="I00A")?"I00A":null;
        if(wanted==null&&botArmy>=20&&!Inventories[1].slots.Any(s=>s.id=="I003"))wanted="I003";
        if(wanted==null&&LegendaryUsed(1)>=LegendaryLimit[1]&&LegendaryLimit[1]<3&&gold>=1900)wanted="I004";
        int item=ShopIndex(wanted);if(item<0||F(ShopItems[item],"gold")>gold*.65f||!BuyItem(1,item,out _))return false;botPlan="Покупает: "+(string)ShopItems[item]["name"];return true;
    }
    void BotDefend(){
        if(Finished||NetworkReplica)return;var danger=Units.Where(BotSeesUnit).Where(u=>u.position.x>2016f/30&&Lines[u.kind].unit!="YTct"&&Lines[u.kind].unit!="n02E").ToArray();if(danger.Length==0)return;
        Unit best=null;float score=0;int count=0;foreach(var center in danger){float value=0;int n=0;foreach(var u in danger)if(Vector2.Distance(center.position,u.position)<=1000f/30){value+=Mathf.Min(2500,u.hp);n++;}if(value>score){score=value;best=center;count=n;}}
        bool urgent=CastleHp[1]<CastleHealthLimit(1)*.3f||danger.Any(u=>Vector2.Distance(u.position,CastlePosition(1))<24);
        if(best==null||!(urgent&&score>=500||count>=5&&score>=2200))return;
        if(RescueCharges[1]<=0&&BotGold()>=1000&&score>=2500)BuyRescue(1);
        if(RescueCharges[1]>0&&RescueCooldown[1]<=0)Rescue(1,best.position);
    }
}
}
