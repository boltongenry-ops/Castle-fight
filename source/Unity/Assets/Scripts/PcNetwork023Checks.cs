using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseNetwork023(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"network023"));Directory.CreateDirectory(folder);MainMenu();
        directoryEndpoint=BattleLab.Arg("--pc-directory-url","ws://127.0.0.1:49395");networkName="Isolated Unity QA";networkSessionToken="";
        Check(networkRaceNames.Length==15&&networkRaceLabels.Length==15,"All fifteen race names have native protocol mappings");
        raceChoice=3;botRaceChoice=0;matchMode=0;aspectsOn=false;NewMatch();testHoldAuthority=true;match.Aspects[0]=match.Aspects[1]=0;scoreExpanded=true;
        Check(AspectPassiveChoice(0)==0&&!HudContains(new Vector2(100,50)),"Aspect is a passive card icon; old top-left badge no longer blocks controls");yield return Action("ability.aspect.0");Check(aspectInfoOpen&&aspectInfoTeam==0,"Builder passive opens selected aspect description");yield return Shot(folder,"00a-builder-passive");yield return Action("ability.aspect.close");
        MainMenu();raceChoice=9;botRaceChoice=9;NewMatch();testHoldAuthority=true;match.Aspects[0]=1;match.Aspects[1]=0;
        var fire=match.Spawn(0,match.Lines.Select((line,index)=>new{line,index}).First(row=>row.line.unit=="h02Z").index,new Vector2(-148,24));RenderMatch();builderSelected=false;selectedUnit=fire.id;yield return null;
        Check(PassiveAspectTip(0).Contains("Штормовой разряд")&&!PassiveAspectTip(0).Contains("Резонанс стихий")&&PassiveAspectTip(1).Contains("Резонанс стихий"),"Own/enemy elemental passives use exact selected aspect without clamping to another choice");
        Check(!ElementalLinkReady(fire)&&UnitSkillTip(fire,"A0F5",match.Abilities["A0F5"]).Contains("Не действует: постройте"),"Innate fire Link is clearly inactive without its separate special building");
        testHudPointer=Slot(0,0).center;yield return Shot(folder,"00b-fire-aspect-and-inactive-link");testHudPointer=null;yield return Action("ability.aspect.0");Check(aspectInfoOpen&&aspectInfoTeam==0,"Unit passive opens correct selected aspect");yield return Shot(folder,"00c-fire-aspect-description");yield return Action("ability.aspect.close");
        match.Huts.Add(new HumanMatch.Hut{id=700000,team=0,kind=match.Kind("h063"),paid=true,hp=100,maxHp=100,position=new Vector2(-150,30)});Check(ElementalLinkReady(fire)&&UnitSkillTip(fire,"A0F5",match.Abilities["A0F5"]).Contains("Действует: союзная"),"Innate Link becomes active when its source building exists independently of aspect");
        var enemyFire=match.Spawn(1,fire.kind,new Vector2(-144,24));selectedUnit=enemyFire.id;RenderMatch();yield return null;yield return Action("ability.aspect.1");Check(aspectInfoOpen&&aspectInfoTeam==1,"Enemy unit opens opponent aspect instead of player aspect");yield return Shot(folder,"00d-enemy-aspect");aspectInfoOpen=false;
        match.Aspects[0]=-1;Check(AspectPassiveChoice(0)==-1,"Unchosen aspect has no passive icon");testHoldAuthority=false;MainMenu();
        BeginDirectory();yield return RecoveryWait(()=>directoryReady,"Initial authoritative directory",20);page=Page.Network;
        yield return Action("network.race");Check(showNetworkRacePicker,"Race selector opens a grid instead of cycling four choices");yield return Shot(folder,"01-race-grid");yield return Action("network.race.select.14");Check(networkRace==14&&!showNetworkRacePicker,"Last race can be selected directly");
        raceChoice=3;botRaceChoice=7;matchMode=2;startGold=1000;roundsChoice=7;aspectsOn=true;
        var rows=new JArray();
        for(int scenario=0;scenario<17;scenario++){
            int race=scenario%15;
            if(testFailures>0)break;
            bool guest=scenario%2==1;PcNetworkClient peer=null;networkRace=race;page=Page.Network;
            if(guest){
                peer=new PcNetworkClient(PcNetworkClient.Handshake("create","","Isolated host",networkRaceNames[(race+1)%15]));_=peer.ConnectAsync(new Uri(directoryEndpoint));
                float joinDeadline=Time.realtimeSinceStartup+15;string code="";
                while(code==""&&Time.realtimeSinceStartup<joinDeadline){while(peer.TryRead(out var p))if((string)p["type"]=="lobby")code=(string)p["code"]??"";yield return null;}
                Check(code!="","Isolated opponent creates room for Unity guest "+scenario);EnterRoom("join",code);
            }else EnterRoom("create","");
            yield return RecoveryWait(()=>networkSlot==(guest?1:0)&&networkRoom!=null,"Unity joins authoritative slot "+(guest?1:0)+" / "+networkRaceNames[race],15);
            if(networkRoom==null)break;
            string mode=scenario==15?"mirror":scenario==16?"random":"fixed";
            if(mode!="fixed"){if(guest){var config=(JObject)networkRoom["config"].DeepClone();config["mode"]=mode;_=peer.SendAsync(new JObject{["type"]="configure",["config"]=config});}else Configure("mode",mode);yield return RecoveryWait(()=>(string)networkRoom["config"]?["mode"]==mode,"Server confirms "+mode+" mode",10);}
            Check((bool?)networkRoom["config"]?["fog"]==true,"Native default fog remains enabled for "+race);
            if(!guest){peer=new PcNetworkClient(PcNetworkClient.Handshake("join",(string)networkRoom["code"],"Isolated peer",networkRaceNames[(race+1)%15]));_=peer.ConnectAsync(new Uri(directoryEndpoint));}
            yield return RecoveryWait(()=>networkRoom?["players"]?[1]?.HasValues==true,"Second client joined "+race,15);
            _=peer.SendAsync(new JObject{["type"]="ready",["ready"]=true});SetNetworkReady(true);
            yield return RecoveryWait(()=>networkRoom["players"].All(p=>(bool?)p["ready"]==true),"Both ready "+race,15);
            if(race==0)yield return Shot(folder,"02-room-default-fog");
            if(guest)_=peer.SendAsync(new JObject{["type"]="start"});else yield return Action("network.start");float deadline=Time.realtimeSinceStartup+50;JObject load=null;bool loaded=false;
            while((!onlineStarted||match==null||match.Elapsed<.1f)&&Time.realtimeSinceStartup<deadline){
                while(peer.TryRead(out var packet))if((string)packet["type"]=="load")load=packet;
                if(load!=null&&versusPrepared&&!loaded){if(race==0)yield return Shot(folder,"03-versus");loaded=true;_=peer.SendAsync(new JObject{["type"]="loaded"});}
                if(!string.IsNullOrEmpty(networkLoadError))break;
                yield return null;
            }
            Check(loaded&&onlineMatch&&onlineStarted&&match!=null&&match.Elapsed>.05f,"Both clients reach authoritative battle with default fog "+race);
            if(!onlineStarted){peer.Dispose();break;}
            Check(networkRaceNames[matchRace]==(string)load["races"]?[networkSlot]&&networkRaceNames[matchBotRace]==(string)load["races"]?[1-networkSlot]&&FogActive,"Race indices and network fog follow server "+scenario);
            Check(onlineReplica.UnknownUnits==0&&onlineReplica.UnknownBuildings==0,"Initial snapshot mapped without missing entities "+race);
            if(matchRace==12||matchBotRace==12){int side=matchRace==12?0:1;int source=side==0?networkSlot:1-networkSlot;Check(ultimateRoundLoadouts[side].SequenceEqual(HumanMatch.Strings(load["ultimate_loadouts"]?[source])),"Ultimate uses exact authoritative draw in slot "+side);}
            AspectOnlineApply(new JObject{["enabled"]=true,["choices"]=guest?new JArray(-1,0):new JArray(0,-1),["remaining"]=12});Check(aspectWaiting,"Remain blocked until opponent chooses aspect "+scenario);AspectOnlineApply(new JObject{["enabled"]=true,["choices"]=new JArray(0,1),["remaining"]=11});Check(!aspectWaiting,"Both chosen aspects unblock UI "+scenario);AspectOnlineApply(new JObject{["enabled"]=false});
            Check(AspectPassiveChoice(0)==-1&&AspectPassiveChoice(1)==-1,"Disabled network aspects clear old passive icons "+scenario);
            int kind=match.BaseKinds.FirstOrDefault(k=>match.Lines[k].cost<=match.Gold[0]&&match.Lines[k].woodCost==0);
            NetworkCommand(PcServerReplica.Build(match.Lines[kind].building,HumanMatch.Plot(0,3,3)));
            deadline=Time.realtimeSinceStartup+16;while(!match.Huts.Any(h=>h.team==0)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(match.Huts.Any(h=>h.team==0&&match.Lines[h.kind].building==match.Lines[kind].building),"Native server accepts selected race building "+race);
            if(race==0||race==12||race==14)yield return Shot(folder,"04-battle-"+race);
            var snap=new JObject{["units"]=new JArray(),["huts"]=new JArray(),["banks"]=new JArray(new JObject(),new JObject()),["castles"]=new JArray(new JObject(),new JObject())};var actors=(JArray)snap["units"];
            foreach(var line in ((JArray)JObject.Parse(RaceCatalog(matchRace))["lines"]).OfType<JObject>())if(!string.IsNullOrEmpty((string)line["unit"])){
                int id=800000+actors.Count;actors.Add(new JObject{["id"]=id,["t"]=networkSlot,["r"]=line["unit"],["p"]=new JArray(-155,0,24),["hp"]=new JArray(100,100),["a"]=new JArray("Stand",0,1,0)});
            }
            onlineReplica.Apply(snap);Check(onlineReplica.UnknownUnits==0,"Every catalog fighter/helper maps through snapshot adapter "+race);
            foreach(var unit in match.Units){var prefab=RaceModel(unit.team,unit.kind,false);Check(prefab&&prefab.GetComponent<PcModel>()||match.Hidden(unit),"Network fighter model or intentionally hidden controller "+race+"/"+match.Lines[unit.kind].unit);}
            if(scenario==0){
                var card=new JObject{["id"]=999,["t"]=0,["armor"]=3,["damage"]=20,["state"]=new JObject{["buff_armor"]=2,["item_armor"]=1,["buff_mult"]=1.5,["aspect_info"]=new JObject{["mods"]=new JObject{["damage"]=.25}}},["aura_stats"]=new JObject{["armor"]=4,["armor_negative"]=-1,["damage"]=.2,["damage_negative"]=-.1},["stats"]=new JObject{["armor"]=2,["damage"]=.3},["passive"]=new JObject{["armor"]=3}};
                networkUnitCards[999]=card;Check(Mathf.Abs(NetworkCardStat(999,"armor",0)-14)<.001f&&Mathf.Abs(NetworkCardStat(999,"damage",0)-52.5f)<.01f,"Server card stats include buff, item, aura and aspect once");
            }
            rows.Add(new JObject{["race"]=networkRaceNames[race],["slot"]=networkSlot,["mode"]=mode,["mappedFighters"]=actors.Count,["serverLoad"]=load});
            // Remove the isolated room explicitly; do not reserve a recovery slot.
            onlineMatch=false;onlineStarted=false;page=Page.Network;ClearMatch();series=null;StopDirectory();Check(raceChoice==3&&botRaceChoice==7&&matchMode==2&&startGold==1000&&roundsChoice==7&&aspectsOn,"Network match preserves offline setup "+scenario);_=peer.LeaveAsync();yield return new WaitForSecondsRealtime(.5f);
            BeginDirectory();yield return RecoveryWait(()=>directoryReady&&directoryCanCreate,"Server room released "+race,15);page=Page.Network;
        }
        Check(rows.Count==17,"All fifteen authoritative race starts plus mirror/random completed");
        yield return GuardNetworkLoad(FaultyNetwork023());Check(network!=null&&network.Connected&&!string.IsNullOrEmpty(networkLoadError)&&page==Page.Network,"Resource iterator failure stays visible without leaving server");yield return Shot(folder,"05-load-error");
        StopDirectory();MainMenu();
        var result=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["races"]=rows,["androidDeviceTest"]=false};File.WriteAllText(Path.Combine(folder,"network023-verification.json"),result.ToString());Debug.Log("CF_NETWORK023_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
    IEnumerator FaultyNetwork023(){yield return null;throw new InvalidOperationException("Проверка ошибки ресурсов");}
}
}
