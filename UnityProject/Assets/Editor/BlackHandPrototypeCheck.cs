using System;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class BlackHandPrototypeCheck {
    static void CheckWeapons(JObject clan,JObject human) {
        var world=new HumanMatch(clan,1000,1,-1,-1,human);
        object Call(string name, params object[] args)=>typeof(HumanMatch).GetMethod(name,
            System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(world,args);
        void Check(bool ok,string detail){if(!ok)throw new Exception(detail);}
        HumanMatch.Unit Spawn(string id,int team)=>world.Spawn(team,Array.FindIndex(world.Lines,l=>l.unit==id),Vector2.zero);
        var executioner=Spawn("bU07",0);
        var foe=Spawn("hfoo",1);
        void Hit(HumanMatch.Unit attacker,float damage)=>Call("ResolveAttack",attacker.id,0,foe.id,damage,world.Lines[attacker.kind].weapons[0],0f,true,false);
        foe.maxHp=2000;foe.hp=135;
        Hit(executioner,1);
        Check(foe.hp>120,"Execution ignored absolute cap");
        foe.hp=110;
        world.Abilities["PShd"]=new JObject();
        foe.effects.Add(new HumanMatch.Effect{id="PShd",sourceTeam=1,left=5,power=100});
        Hit(executioner,1);
        Check(foe.hp==110 && !executioner.spellTimers.ContainsKey("BH07"),"Shielded execution");
        foe.effects.Clear();
        Hit(executioner,1);
        Check(foe.hp==0 && executioner.spellTimers["BH07"]==10,"Execution failed");
        foe=Spawn("hfoo",1);foe.hp=foe.maxHp=10000;
        var pike=Spawn("bU06",0);
        for(int i=0;i<100 && !foe.effects.Any(e=>e.id=="BHW2");i++)Hit(pike,1);
        Check(foe.effects.Any(e=>e.id=="BHW2"),"No bleed");
        float before=foe.hp;
        Call("TickEffects",foe,.5f);
        Check(foe.hp==before,"Early bleed");
        Call("TickEffects",foe,.5f);
        Check(foe.hp<before,"No bleed tick");
        foe.effects.Clear();
        var engine=Spawn("bU13",0);
        var shot=new HumanMatch.Shot{line=engine.kind,sourceId=engine.id,team=0,target=foe.id,kind=2,end=foe.position,weapon=world.Lines[engine.kind].weapons[0],damage=55};
        Call("BlackHandImpact",shot);before=foe.hp;
        Call("BlackHandZones",1f);
        Check(foe.hp<before,"No fire fragments");
        before=foe.hp;foe.position+=Vector2.right*12;
        Call("BlackHandZones",2f);
        Check(foe.hp==before,"Shrapnel followed target");
        var wounded=Spawn("hfoo",1);
        wounded.hp=wounded.maxHp=2000;
        foreach(var ammo in new[]{"bU12","bU14"}){
            var attacker=Spawn(ammo,0);
            shot.line=attacker.kind;shot.sourceId=attacker.id;shot.target=wounded.id;shot.end=wounded.position;
            Call("BlackHandImpact",shot);Call("BlackHandZones",1f);
            string status=ammo=="bU12"?"BHPs":"BHCr";
            Check(wounded.effects.Any(e=>e.id==status),"No "+status);
        }
        var cursed=Spawn("bU14",0);
        var runner=Spawn("hfoo",1);
        runner.position=new Vector2(55,12);runner.hp=runner.maxHp=3000;
        shot.line=cursed.kind;shot.sourceId=cursed.id;shot.target=runner.id;shot.end=runner.position;
        Call("BlackHandImpact",shot);
        float expected=world.DamageUnit(runner,12,0,"chaos",true);
        for(int tick=0;tick<4;tick++){
            if(tick==1)runner.position+=Vector2.right*15;
            float hp=runner.hp;Call("BlackHandZones",1f);
            Check(Mathf.Abs(hp-runner.hp-(tick<3?expected:0))<.02f && expected>0,"Cursed moving target tick "+tick);
        }
        Debug.Log("CURSED_MOVING_TARGET_PASS ticks=3 damage="+expected);
        var castleGuard=Spawn("bU01",0);
        var melee=world.Lines[wounded.kind].weapons[0];
        float reduced=(float)Call("BlackHandMelee",wounded,castleGuard,100f,true);
        Check(Mathf.Abs(reduced-45)<.01f && (float)Call("BlackHandMelee",wounded,castleGuard,100f,true)==100,"Veteran block");
        var soldier=Spawn("bU00",0);
        int blocks=0;bool previous=false, consecutive=false;
        for(int i=0;i<1000;i++){
            soldier.hp=soldier.maxHp;
            float hp=soldier.hp;
            Call("ResolveAttack",wounded.id,1,soldier.id,30f,melee,0f,true,false);
            bool blocked=soldier.hp==hp;
            if(blocked)blocks++;
            consecutive|=blocked && previous;
            previous=blocked;
        }
        Check(blocks>90 && blocks<215 && consecutive && !soldier.spellTimers.ContainsKey("BHGuard"),"Independent soldier shield");
        soldier.hp=soldier.maxHp;
        Debug.Log("BLACK_HAND_SOLDIER_SHIELD_PASS blocks="+blocks+"/1000");
        var infirmary=new HumanMatch.Hut{team=0,kind=world.Kind("bH16"),position=castleGuard.position};
        var medicA=Spawn("bU01",0);var medicB=Spawn("bU00",0);
        castleGuard.hp=medicA.hp=medicB.hp=1;
        Call("BlackHandBuilding",infirmary);
        Check(castleGuard.hp==76 && medicA.hp==76 && medicB.hp==76,"Three infirmary heals");
        foe.position=wounded.position;
        var court=new HumanMatch.Hut{team=0,kind=world.Kind("bH17"),position=wounded.position};
        Call("BlackHandBuilding",court);
        Check(wounded.effects.Any(e=>e.id=="BHJu"),"Court curse");
        before=wounded.hp;world.Heal(wounded,40);
        Check(Mathf.Abs(wounded.hp-before-20)<.01f,"Court healing reduction");
        var bell=new HumanMatch.Hut{team=0,kind=world.Kind("bH15"),position=wounded.position};
        before=wounded.hp;Call("BlackHandBuilding",bell);
        Check(wounded.hp<before && wounded.effects.Any(e=>e.id=="BHKn" && e.power==-.30f) && bell.spellTimers["BH15"]==14,"Bell hits ordinary enemies");
        var warlock=Spawn("bU09",0);
        warlock.mana=80;
        Check((bool)Call("BlackHandCast",warlock)&&warlock.pendingSpell=="BH09"&&warlock.mana==80,
            "Tithe has an interruptible windup");
        world.TickUnit(warlock,.5f);
        Check(warlock.mana==45&&world.Units.Any(u=>u.team==1&&u.effects.Any(e=>e.id=="BHTx")),
            "Tithe applies its effect and spends mana on release");
        var isolated=new Vector2(80,25);
        HumanMatch.Unit Fighter(string raw,int side,float offset){
            var actor=world.Spawn(side,Array.FindIndex(world.Lines,l=>l.unit==raw),isolated+Vector2.right*offset);
            actor.position=isolated+Vector2.right*offset;
            actor.hp=actor.maxHp=5000;
            return actor;
        }
        var front=Fighter("hfoo",1,2);
        var rear=Fighter("hfoo",1,5);
        foreach(string raw in new[]{"bU03","bU04"}){
            var bow=Fighter(raw,0,0);
            bow.pendingWeapon=world.Lines[bow.kind].weapons.First(w=>w.ground);bow.attackTarget=front.id;bow.shotDamage=100;
            int interval=raw=="bU03"?4:3;
            for(int i=1;i<=interval;i++){
                Call("Release",bow);
                var bolt=world.Shots.Last(s=>s.sourceId==bow.id);
                Check((bolt.chaosSpell=="hand-bolt")==(i==interval),"Piercing cadence "+raw);
                if(i==interval){
                    Call("HandBolt",bolt);
                    Check(Mathf.Abs(bolt.damage-(raw=="bU04"?135:100))<.01f,"Heavy bonus against high HP");
                    var fragment=world.Shots.Last();
                    Check(fragment.target==rear.id && Mathf.Abs(fragment.damage-(raw=="bU03"?40:65))<.01f,"Piercing damage "+raw+" target="+fragment.target+"/"+rear.id+" dmg="+fragment.damage);
                }
            }
        }
        var marshal=Fighter("bU02",0,1);
        var behind=Fighter("hfoo",1,-1);
        float frontHp=front.hp,backHp=behind.hp;
        Call("BlackHandHit",marshal,front);
        Check(front.hp<frontHp && behind.hp==backHp && marshal.spellTimers["BH02"]==8,"Marshal frontal bash");
        var elite=Fighter("bU08",0,1);
        rear.hp=190;Call("BlackHandHit",elite,rear);
        Check(rear.hp==0 && elite.spellTimers["BH08"]==8,"Elite execution");
        var inquisitor=Fighter("bU10",0,0);
        Check((bool)Call("ResolveBlackHandTithe",inquisitor,front,true),"Elite tithe cast");
        for(int i=0;i<6;i++){
            float hp=front.hp;Call("BlackHandRelease",front);
            Check((front.hp<hp)==(i<5),"Elite tithe charges");
        }
        var raging=Fighter("bU02",0,0);raging.maxHp=1000;raging.hp=300;
        float speed=world.AttackSpeed(raging);
        world.DamageUnit(raging,5,1,"magic",true);
        Debug.Log("BERSERK_THRESHOLD hp="+raging.hp+" raw="+world.Lines[raging.kind].unit+" resist="+raging.spellResist);
        Check(raging.spellTimers.TryGetValue("BHBz",out float protection) && protection==5 && Mathf.Abs(world.AttackSpeed(raging)-speed-.4f)<.001f,"Berserk threshold");
        Check(world.DamageUnit(raging,60,1,"pierce",false)==0 && world.DamageUnit(raging,20,1,"magic",true)>0,"Berserk physical only");
        raging.hp=600;Check(world.AttackSpeed(raging)==speed,"Berserk healing");
        raging.spellTimers["BHBz"]=0;raging.hp=301;
        Check(world.DamageUnit(raging,50,1,"normal",false)>0 && raging.spellTimers["BHBz"]==0,"Berserk expires and cannot retrigger");
        Debug.Log("MARSHAL_BERSERK_PASS");Debug.Log("BLACK_HAND_WEAPONS_PASS");
    }
    static void CheckAspects(JObject clan,JObject humans) {
        void Check(bool pass,string message){if(!pass)throw new Exception(message);}
        for(int team=0;team<2;team++){
            int enemy=1-team;
            var world=new HumanMatch(team==0?clan:humans,1000,1,-1,-1,team==0?humans:clan);
            world.Aspects[team]=0;
            var unit=world.Spawn(team,Array.FindIndex(world.Lines,l=>l.unit=="bU03"),Vector2.zero);
            unit.hp=unit.maxHp=1000;
            float Hit(bool spell)=>world.DamageUnit(unit,100,enemy,spell?"magic":"normal",spell,ignoreArmor:true);
            unit.hp=482;
            Check(!unit.handIronUsed&&Hit(false)>0&&!unit.handIronUsed,"Iron does not proc from a starting hp already below 50%");
            unit.hp=525;
            float passingHit=Hit(false);
            Check(unit.hp<500&&unit.effects.Single(e=>e.id=="BHIr").left==5,"Iron triggers at the crossing and lasts 5 s");
            Check(Mathf.Abs(Hit(false)-passingHit*.8f)<.01f,"Iron reduces subsequent weapon damage by 20%");
            unit.effects.Clear();
            unit.hp=525;
            Check(Mathf.Abs(Hit(false)-passingHit)<.01f&&unit.effects.All(e=>e.id!="BHIr"),"Iron grants one use per fighter");
            unit.hp=900;
            float magic=Hit(true);
            unit.effects.Add(new HumanMatch.Effect{id="BHIr",left=5,sourceTeam=team});
            Check(Mathf.Abs(Hit(true)-magic)<.01f,"Iron leaves spell damage unchanged");
            var hutKind=world.Kind("bH00");
            var hut=new HumanMatch.Hut{kind=hutKind,team=enemy,id=400,hp=1000,maxHp=1000,paid=true};
            world.Huts.Add(hut);
            var method=typeof(HumanMatch).GetMethod("ResolveAttack",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var weapon=world.Lines[unit.kind].weapons[0];
            float AttackStructure(int target) {
                float before=target==-1?world.CastleHp[enemy]:hut.hp;
                method.Invoke(world,new object[]{unit.id,team,target,100f,weapon,0f,true,false});
                return before-(target==-1?world.CastleHp[enemy]:hut.hp);
            }
            world.Aspects[team]=-1;
            float baseHit=AttackStructure(-1),baseHut=AttackStructure(-hut.id-2);
            world.Aspects[team]=1;
            Check(Mathf.Abs(AttackStructure(-1)-baseHit*1.15f)<.01f,"Doctrine adds 15% against the castle");
            Check(baseHut>0&&Mathf.Abs(AttackStructure(-hut.id-2)-baseHut*1.15f)<.01f,"Doctrine adds 15% against huts");
            int enemyKind=Array.FindIndex(world.Lines,l=>l.unit=="hfoo");
            var victim=world.Spawn(enemy,enemyKind,new Vector2(6.5f,0));
            victim.hp=victim.maxHp=2000;
            var impact=typeof(HumanMatch).GetMethod("BlackHandImpact",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var tick=typeof(HumanMatch).GetMethod("BlackHandZones",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            for(int variant=11;variant<=14;variant++){
                int line=Array.FindIndex(world.Lines,l=>l.unit=="bU"+variant);
                var shot=new HumanMatch.Shot{kind=2,team=team,line=line,target=victim.id,end=Vector2.zero};
                world.Aspects[team]=-1;
                float hp=victim.hp;
                impact.Invoke(world,new object[]{shot});tick.Invoke(world,new object[]{3f});
                Check(victim.hp==hp,"Normal shrapnel falls short");
                world.Aspects[team]=1;
                impact.Invoke(world,new object[]{shot});tick.Invoke(world,new object[]{3f});
                Check(victim.hp<hp,"Doctrine increases shrapnel reach: "+variant);
            }
        }
        Debug.Log("BLACK_HAND_ASPECTS_PASS both teams, once-per-unit guard and four shrapnel variants");
    }

    public static void Run() {
        string human=Resources.Load<TextAsset>("pc-human").text;
        var clan=BlackHand.Catalog(human);
        CheckWeapons(clan,JObject.Parse(human));
        CheckAspects(clan,JObject.Parse(human));
        foreach(bool right in new[]{false,true}){
            var people=JObject.Parse(human);
            var world=new HumanMatch(right?people:clan,2000,1,-1,-1,right?clan:people);
            int team=right?1:0;
            int index(string id)=>Array.FindIndex(world.Lines,l=>l.unit==id);
            var yard=world.Lines[index("bU11")];
            if(yard.cost!=225||yard.upgrades.Length!=3)throw new Exception("Siege yard");
            foreach(var target in yard.upgrades)if(world.Lines.Single(l=>l.building==target).cost!=155)throw new Exception("Branch price");
            var troops=Enumerable.Range(0,15).Select(i=>world.Spawn(team,index("bU"+i.ToString("00")),new Vector2(i%5,2*(i/5)))).ToArray();
            for(int i=0;i<18;i++)world.Spawn(1-team,index("hfoo"),new Vector2(10+i%4,2*(i/4)));
            for(int tick=0;tick<240;tick++)world.Step(.1f);
            if(world.Kills==0||world.ShellImpacts==0||world.SpellsCast==0)throw new Exception("No battle activity");
            Debug.Log("BLACK_HAND_BATTLE team="+team+" kills="+world.Kills+" impacts="+world.ShellImpacts+" spells="+world.SpellsCast);
        }
    }
}
