#if UNITY_SERVER || CF_AUTHORITY_QA
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
    internal sealed class DedicatedHost : MonoBehaviour {
        const int MaxRequestBytes=32768;
        const float StepSeconds=.05f;
        readonly Dictionary<string,JObject> catalogs=new Dictionary<string,JObject>(StringComparer.Ordinal);
        readonly Dictionary<string,DedicatedBattle> battles=new Dictionary<string,DedicatedBattle>(StringComparer.Ordinal);
        readonly ConcurrentQueue<Request> requests=new ConcurrentQueue<Request>();
        HttpListener listener;
        float elapsed;
        string key;

        sealed class Request {
            public JObject Data;
            public readonly TaskCompletionSource<JObject> Reply=new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

#if UNITY_SERVER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap() => StartServer();
#endif

        public static void StartServer(){
            Application.targetFrameRate=40;
            new GameObject("Castle Fight Dedicated Host").AddComponent<DedicatedHost>();
        }

        void Awake(){
            try {
                int port=int.Parse(Environment.GetEnvironmentVariable("CF_AUTHORITY_PORT")??"49290");
                key=Environment.GetEnvironmentVariable("CF_AUTHORITY_KEY");
                if(string.IsNullOrEmpty(key)||key.Length<32)throw new InvalidOperationException("Missing private authority key");
                string folder=Environment.GetEnvironmentVariable("CF_CATALOG_DIR")??Path.Combine(AppContext.BaseDirectory,"catalogs");
                foreach(string path in Directory.EnumerateFiles(folder,"race-*.json")){
                    var catalog=JObject.Parse(File.ReadAllText(path));
                    catalogs.Add((string)catalog["race"],catalog);
                }
                if(catalogs.Count==0)throw new InvalidOperationException("Нет каталогов рас");
                listener=new HttpListener();
                listener.Prefixes.Add("http://127.0.0.1:"+port+"/");
                listener.Start();
                _=Listen(listener);
                Debug.Log("CF_AUTHORITY_READY port="+port+" races="+catalogs.Count);
            }catch(Exception error){Debug.LogError(error);Application.Quit(1);}
        }

        async Task Listen(HttpListener server){
            while(server.IsListening){
                HttpListenerContext context;
                try{context=await server.GetContextAsync().ConfigureAwait(false);}
                catch(HttpListenerException){break;}
                catch(ObjectDisposedException){break;}
                _=Accept(context);
            }
        }
        async Task Accept(HttpListenerContext context){
            try {
                var input=context.Request;
                if(input.HttpMethod!="POST"||input.Url.AbsolutePath!="/command"||
                   !string.Equals(input.Headers["X-Castle-Authority"],key,StringComparison.Ordinal)){
                    context.Response.StatusCode=403;
                    return;
                }
                if(input.ContentLength64<1||input.ContentLength64>MaxRequestBytes){context.Response.StatusCode=413;return;}
                if(requests.Count>=128){context.Response.StatusCode=503;return;}
                var data=new byte[(int)input.ContentLength64];
                int used=0;
                while(used<data.Length){
                    int count=await input.InputStream.ReadAsync(data,used,data.Length-used).ConfigureAwait(false);
                    if(count==0)return;
                    used+=count;
                }
                var request=new Request{Data=JObject.Parse(System.Text.Encoding.UTF8.GetString(data))};
                requests.Enqueue(request);
                var reply=await request.Reply.Task.ConfigureAwait(false);
                var response=System.Text.Encoding.UTF8.GetBytes(reply.ToString(Formatting.None));
                context.Response.ContentType="application/json";
                context.Response.ContentLength64=response.Length;
                await context.Response.OutputStream.WriteAsync(response,0,response.Length).ConfigureAwait(false);
            }catch(IOException){}catch(HttpListenerException){}catch(JsonException){context.Response.StatusCode=400;}
            finally{context.Response.Close();}
        }

        void Update(){
            for(int i=0;i<32&&requests.TryDequeue(out var request);i++){
                try{request.Reply.SetResult(Dispatch(request.Data));}
                catch(Exception error) when(error is ArgumentException||error is FormatException||error is OverflowException||error is InvalidCastException){
                    request.Reply.SetResult(Error("Некорректный запрос"));
                }
                catch(Exception error){
                    Debug.LogError(error);request.Reply.SetResult(Error("Ошибка расчёта боя"));
                }
            }
            elapsed+=Mathf.Min(.2f,Time.unscaledDeltaTime);
            while(elapsed>=StepSeconds){
                foreach(var battle in battles.Values)battle.Step(StepSeconds);
                elapsed-=StepSeconds;
            }
        }

        JObject Dispatch(JObject request){
            string op=(string)request["op"];
            if(op=="health")return new JObject{["ok"]=true,["races"]=new JArray(catalogs.Keys)};
            string room=(string)request["room"];
            if(string.IsNullOrEmpty(room)||room.Length>32||room.Any(ch=>!char.IsLetterOrDigit(ch)))return Error("Некорректный код игры");
            if(op=="create"){
                if(battles.ContainsKey(room)||battles.Count>=4)return Error("Нет свободного места");
                var races=request["races"] as JArray;
                if(races?.Count!=2||!catalogs.TryGetValue((string)races[0]??"",out var west)||!catalogs.TryGetValue((string)races[1]??"",out var east))
                    return Error("Неизвестная раса");
                int gold=(int?)request["gold"]??250;
                if(gold!=250&&gold!=500&&gold!=1000)return Error("Недопустимое стартовое золото");
                var draw=new JArray();
                west=PrepareRace(west,draw);
                east=PrepareRace(east,draw);
                battles.Add(room,new DedicatedBattle(west,east,gold,(bool?)request["fog"]??true,(bool?)request["aspects"]??false){Paused=true});
                return new JObject{["ok"]=true,["ultimate_loadouts"]=draw};
            }
            if(!battles.TryGetValue(room,out var match))return Error("Игра закрыта");
            switch(op){
                case "drop":battles.Remove(room);break;
                case "hold":match.Paused=true;break;
                case "resume":match.Paused=false;break;
                case "state":
                    return new JObject{["ok"]=true,["snapshots"]=new JArray(match.Snapshot(0),match.Snapshot(1))};
                case "input":return match.Command((int?)request["slot"]??-1,request["command"] as JObject);
                default:return Error("Команда комнаты недоступна");
            }
            return new JObject{["ok"]=true};
        }
        static JObject PrepareRace(JObject catalog,JArray loadouts){
            if((string)catalog["race"]!="Ultimate Builder"){loadouts.Add(new JArray());return catalog;}
            var plan=new UltimateLoadout(catalog["ultimateSlots"] as JArray,n=>UnityEngine.Random.Range(0,n));
            string[] draw=plan.Draw();
            loadouts.Add(new JArray(draw));
            return plan.Playable(catalog,draw);
        }
        static JObject Error(string message)=>new JObject{["ok"]=false,["message"]=message};
        void OnDestroy(){
            listener?.Close();
            while(requests.TryDequeue(out var request))request.Reply.TrySetResult(Error("Служба боя остановлена"));
        }
    }
}
#endif
