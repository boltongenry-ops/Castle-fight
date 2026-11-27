#if UNITY_SERVER || CF_AUTHORITY_QA
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
    // Network input reaches here only after the gateway has assigned a player slot.
    internal sealed class DedicatedBattle {
        readonly HumanMatch match;
        readonly bool fogEnabled, aspects;
        readonly FogGrid[] fog = { new FogGrid(), new FogGrid() };
        readonly HashSet<int>[] knownHuts = { new HashSet<int>(), new HashSet<int>() };
        float aspectTime=30f, fogTime;
        public bool Paused { get; set; }
        public bool Finished => match.Finished;
        bool ChoosingAspect => aspects && (match.Aspects[0]<0 || match.Aspects[1]<0);

        public DedicatedBattle(JObject west,JObject east,int gold,bool fogEnabled,bool aspects) {
            this.fogEnabled=fogEnabled;
            this.aspects=aspects && (west["aspects"] as JArray)?.Count==2 && (east["aspects"] as JArray)?.Count==2;
            match=new HumanMatch(west,gold,botCatalog:east) {PlayerVersusPlayer=true};
            match.VisionTest=Visible;
            UpdateSight();
        }

        public void Step(float seconds) {
            if(Paused||Finished)return;
            if(ChoosingAspect) {
                aspectTime=Mathf.Max(0,aspectTime-seconds);
                if(aspectTime<=0)for(int team=0;team<2;team++)if(match.Aspects[team]<0)match.Aspects[team]=0;
                return;
            }
            if((fogTime-=seconds)<=0){fogTime=.15f;UpdateSight();}
            match.Step(seconds);
        }

        static Vector2 Mirror(int team,Vector2 p)=>team==0?p:new Vector2(-p.x,p.y);
        bool Visible(int team,Vector2 p)=>!fogEnabled||fog[team].Visible(Mirror(team,p));

        void UpdateSight(){
            if(!fogEnabled)return;
            for(int team=0;team<2;team++){
                var grid=fog[team];
                grid.Clear();grid.RevealBase();
                grid.Reveal(Mirror(team,match.Builders[team].position),800f/30);
                foreach(var u in match.Units)
                    if(u.hp>0&&(u.team==team||match.ElvenReveals(u,team)))
                        grid.Reveal(Mirror(team,u.position),HumanMatch.F(match.Lines[u.kind].stats,"sight",800f/30));
                foreach(var h in match.Huts)
                    if(h.hp>0&&h.team==team)grid.Reveal(Mirror(team,h.position),HumanMatch.F(match.Lines[h.kind].buildingStats,"sight",1000f/30));
                foreach(var orb in match.NorthernOrbs)if(orb.team==team)grid.Reveal(Mirror(team,orb.position),700f/30);
                foreach(var zone in match.NightZones)if(zone.team==team&&zone.age<8)grid.Reveal(Mirror(team,zone.point),1100f/30);
                foreach(var h in match.Huts)if(h.team==team||Visible(team,h.position))knownHuts[team].Add(h.id);
            }
        }

        static bool TryPoint(int team,JToken row,out Vector2 p){
            p=default;
            if(!(row is JArray a)||a.Count!=3)return false;
            if(!Numeric(a[0])||!Numeric(a[2]))return false;
            var local=new Vector2((float)a[0],(float)a[2]);
            if(float.IsNaN(local.x)||float.IsNaN(local.y)||Mathf.Abs(local.x)>6144f/30||Mathf.Abs(local.y)>2048f/30)return false;
            p=Mirror(team,local);
            return true;
        }
        static bool Numeric(JToken n)=>n.Type==JTokenType.Float||n.Type==JTokenType.Integer;
        static int Int(JToken n,int fallback=-1)=>n?.Type==JTokenType.Integer?(int)n:fallback;

        public JObject Command(int team,JObject command){
            if(team<0||team>1||command==null||Paused||Finished)return Result(false,"Бой недоступен");
            string kind=(string)command["kind"];
            if(kind=="surrender"){match.Surrender(team);return Result(true);}
            int index=Int(command["index"]);
            if(ChoosingAspect){
                if(kind!="aspect"||index<0||index>1||match.Aspects[team]>=0)return Result(false,"Сначала выберите аспект");
                match.Aspects[team]=index;
                return Result(true);
            }
            int id=Int(command["id"]);
            switch(kind){
                case "build":
                    if(!TryPoint(team,command["point"],out var plot))break;
                    int x=Mathf.RoundToInt((plot.x*30-(team==0?-6080:2112))/128);
                    int y=HumanMatch.BuildRow(plot.y*30,true);
                    int building=match.Kind((string)command["raw"]);
                    if(building<0||(HumanMatch.Plot(team,x,y)-plot).sqrMagnitude>.25f)break;
                    return Result(match.TryBuild(team,building,x,y,out var constructionError),constructionError);
                case "upgrade":
                    int next=match.Kind((string)command["raw"]);
                    if(next<0)break;
                    return Result(match.Upgrade(team,id,next,out var upgradeError),upgradeError);
                case "sell":return Result(match.Sell(team,id));
                case "pause":
                    var hut=match.Huts.FirstOrDefault(h=>h.id==id&&h.team==team&&h.paid&&match.Lines[h.kind].Producer);
                    return Result(hut!=null&&match.PauseHut(team,id,!hut.paused));
                case "pause_all":
                    if(command["paused"]?.Type!=JTokenType.Boolean)break;
                    match.PauseAll(team,(bool)command["paused"]);return Result(true);
                case "sync_production":
                    int count=match.SynchronizeProduction(team);
                    return new JObject{["ok"]=count>=2,["count"]=count,["message"]=count<0?"Отряд превышает лимит армии":"Нужно хотя бы два действующих здания выпуска"};
                case "move":
                    return Result(TryPoint(team,command["point"],out var move)&&match.MoveBuilder(team,move));
                case "stop":return Result(match.MoveBuilder(team,match.Builders[team].position));
                case "repair":return Result(match.RepairCastle(team));
                case "repair_building":return Result(match.RepairBuilding(team,id));
                case "builder_spell":
                    if(!TryPoint(team,command["point"],out var point))break;
                    string spell=(string)command["spell"];
                    return Result(spell=="blink"?match.Blink(team,point):spell=="rescue"&&match.Rescue(team,point));
                case "buy_rescue":return Result(match.BuyRescue(team));
                case "buy_item":return Result(match.BuyItem(team,index,out var buyError),buyError);
                case "use_item":
                    if(!TryPoint(team,command["point"],out var itemPoint))break;
                    return Result(match.UseItem(team,index,Int(command["target"],0),itemPoint,out var itemError),itemError);
            }
            return Result(false,"Команда недоступна");
        }

        static JObject Result(bool ok,string error=null)=>ok?new JObject{["ok"]=true}:new JObject{["ok"]=false,["message"]=string.IsNullOrEmpty(error)?"Команда недоступна":error};
        public JObject Snapshot(int viewer)=>BattleSnapshot.Capture(match,viewer,
            point=>Visible(viewer,point),hut=>knownHuts[viewer].Contains(hut.id),aspects,aspectTime);

    }
}
#endif
