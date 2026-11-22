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
        var castleGuard=Spawn("bU01",0);
        var melee=world.Lines[wounded.kind].weapons[0];
        float reduced=(float)Call("BlackHandMelee",wounded,castleGuard,100f,true);
        Check(Mathf.Abs(reduced-45)<.01f && (float)Call("BlackHandMelee",wounded,castleGuard,100f,true)==100,"Veteran block");
        var infirmary=new HumanMatch.Hut{team=0,kind=world.Kind("bH16"),position=castleGuard.position};
        castleGuard.hp=1;Call("BlackHandBuilding",infirmary);
        Check(castleGuard.hp==76,"Infirmary heal");
        foe.position=wounded.position;
        var court=new HumanMatch.Hut{team=0,kind=world.Kind("bH17"),position=wounded.position};
        Call("BlackHandBuilding",court);
        Check(wounded.effects.Any(e=>e.id=="BHJu"),"Court curse");
        before=wounded.hp;world.Heal(wounded,40);
        Check(Mathf.Abs(wounded.hp-before-20)<.01f,"Court healing reduction");
        var bell=new HumanMatch.Hut{team=0,kind=world.Kind("bH15"),position=wounded.position};
        wounded.pendingSpell="A001";wounded.castLeft=1;
        Call("BlackHandBuilding",bell);
        Check(wounded.pendingSpell==null && bell.spellTimers.Count==1,"Cast interrupt");
        var warlock=Spawn("bU09",0);
        warlock.mana=80;
        Check((bool)Call("BlackHandCast",warlock)&&warlock.pendingSpell=="BH09"&&warlock.mana==80,
            "Tithe has an interruptible windup");
        var enemyBell=new HumanMatch.Hut{team=1,kind=world.Kind("bH15"),position=warlock.position};
        Call("BlackHandBuilding",enemyBell);
        Check(warlock.pendingSpell==null&&warlock.mana==80&&warlock.spellTimers["BH09"]==12&&enemyBell.spellTimers["BH15"]==12,
            "Bell interrupts Tithe during its cast");
        warlock.spellTimers.Clear();warlock.castPause=0;
        Check((bool)Call("BlackHandCast",warlock),"Tithe can be cast after recovery");
        world.TickUnit(warlock,.5f);
        Check(warlock.mana==45&&world.Units.Any(u=>u.team==1&&u.effects.Any(e=>e.id=="BHTx")),
            "Tithe applies its effect and spends mana on release");
        Debug.Log("BLACK_HAND_WEAPONS_PASS");
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
