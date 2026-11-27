using System;
using System.Collections.Concurrent;
using System.IO;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CastleFight {
// One socket owns one connection. Incoming packets are handed to Unity on its main thread.
public sealed class PcNetworkClient : IDisposable {
    const int MaxFrame=2*1024*1024,MaxInflated=8*1024*1024;
    const int SpectatorChunk=30*1024,SpectatorCompressedLimit=512*1024;
    readonly ConcurrentQueue<JObject> incoming=new ConcurrentQueue<JObject>();
    JObject latestSpectatorFrame;
    readonly SemaphoreSlim sendLock=new SemaphoreSlim(1,1);
    readonly CancellationTokenSource stop=new CancellationTokenSource();
    ClientWebSocket socket;readonly JObject opening;readonly Stopwatch receiveClock=Stopwatch.StartNew();
    public PcNetworkClient(JObject handshake=null){opening=handshake;}
    public double SecondsSinceReceive=>receiveClock.Elapsed.TotalSeconds;
    public bool Connected => socket!=null&&socket.State==WebSocketState.Open&&!stop.IsCancellationRequested;
    public bool TryRead(out JObject packet){
        if(incoming.TryDequeue(out packet))return true;
        packet=Interlocked.Exchange(ref latestSpectatorFrame,null);
        return packet!=null;
    }
    void Status(string message)=>incoming.Enqueue(new JObject { ["type"]="_status",["message"]=message });
    public async Task ConnectAsync(Uri endpoint) {
        try {
            if(endpoint==null||(endpoint.Scheme!="wss"&&!(endpoint.Scheme=="ws"&&endpoint.IsLoopback)))throw new ArgumentException("Используйте wss:// или локальный ws://");
            socket=new ClientWebSocket();socket.Options.KeepAliveInterval=TimeSpan.FromSeconds(20);
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop.Token)) {
                deadline.CancelAfter(TimeSpan.FromSeconds(12));
                await socket.ConnectAsync(endpoint,deadline.Token);
            }
            receiveClock.Restart();await SendAsync(opening??Handshake("browse","", "Игрок", "Human Alliance"));
            Status("Подключено к серверу");
            _=HeartbeatAsync();
            await ReceiveAsync();
            if(!stop.IsCancellationRequested)Status("Соединение закрыто сервером");
        } catch(OperationCanceledException) { if(!stop.IsCancellationRequested)Status("Время подключения истекло"); }
        catch(Exception e) { if(!stop.IsCancellationRequested)Status("Ошибка соединения: "+e.GetBaseException().Message); }
        finally { if(!stop.IsCancellationRequested)incoming.Enqueue(new JObject { ["type"]="_disconnected" }); }
    }
    // Do not claim optional capabilities until their corresponding Unity flows are implemented.
    public static JObject Handshake(string action,string code,string name,string race)=>new JObject {
        ["type"]=action,["protocol"]=1,["build"]="0.6.19",["code"]=code??"",
        ["name"]=name??"Игрок",["race"]=race??"Human Alliance",["heartbeat"]=true,
        ["authority"]=1,["post_match"]=1,["snapshot_delta"]=0,["aspects"]=1
    };
    public async Task SendAsync(JObject packet) {
        if(!Connected)return;
        byte[] payload=Encoding.UTF8.GetBytes(packet.ToString(Formatting.None));
        if(payload.Length>MaxFrame)throw new InvalidOperationException("Слишком большое сообщение");
        await sendLock.WaitAsync(stop.Token);
        try { if(Connected)await socket.SendAsync(new ArraySegment<byte>(payload),WebSocketMessageType.Text,true,stop.Token); }
        finally {sendLock.Release();}
    }
    internal static byte[] CompressSpectatorFrame(JObject frame){
        using(var bytes=new MemoryStream()){
            using(var gzip=new GZipStream(bytes,CompressionLevel.Fastest,true))
            using(var writer=new StreamWriter(gzip,new UTF8Encoding(false)))
            using(var json=new JsonTextWriter(writer))frame.WriteTo(json);
            return bytes.Length<=SpectatorCompressedLimit?bytes.ToArray():null;
        }
    }

    internal async Task SendSpectatorFrameAsync(byte[] frame){
        if(frame==null||!Connected)return;
        await sendLock.WaitAsync(stop.Token);
        try{
            if(!Connected)return;
            var chunk=new byte[SpectatorChunk];
            for(int offset=0;offset<frame.Length;){
                int size=Math.Min(SpectatorChunk-1,frame.Length-offset);
                chunk[0]=(byte)((offset==0?1:0)|(offset+size==frame.Length?2:0));
                Buffer.BlockCopy(frame,offset,chunk,1,size);
                await socket.SendAsync(new ArraySegment<byte>(chunk,0,size+1),WebSocketMessageType.Binary,true,stop.Token);
                offset+=size;
            }
        }finally{sendLock.Release();}
    }

    async Task HeartbeatAsync() {
        try {while(!stop.IsCancellationRequested){await Task.Delay(TimeSpan.FromSeconds(10),stop.Token);await SendAsync(new JObject { ["type"]="ping" });}}
        catch(OperationCanceledException) {} catch(Exception e) {if(!stop.IsCancellationRequested)Status("Потеря связи: "+e.GetBaseException().Message);}
    }
    async Task ReceiveAsync() {
        var buffer=new byte[16384];
        while(!stop.IsCancellationRequested&&Connected) {
            using(var bytes=new MemoryStream()) {
                WebSocketReceiveResult result;
                do {
                    result=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),stop.Token);
                    receiveClock.Restart();if(result.MessageType==WebSocketMessageType.Close)return;
                    bytes.Write(buffer,0,result.Count);
                    if(bytes.Length>MaxFrame)throw new IOException("Пакет сервера слишком большой");
                } while(!result.EndOfMessage);
                byte[] decoded=bytes.ToArray();
                if(result.MessageType==WebSocketMessageType.Binary) {
                    using(var compressed=new MemoryStream(decoded))using(var gzip=new GZipStream(compressed,CompressionMode.Decompress))using(var output=new MemoryStream()) {
                        int n;while((n=gzip.Read(buffer,0,buffer.Length))>0){output.Write(buffer,0,n);if(output.Length>MaxInflated)throw new IOException("Распакованный пакет слишком большой");}
                        decoded=output.ToArray();
                    }
                } else if(result.MessageType!=WebSocketMessageType.Text)throw new IOException("Неизвестный тип сообщения");
                var packet=JObject.Parse(Encoding.UTF8.GetString(decoded));
                string type=(string)packet["type"];
                if(type=="spectate_frame")Interlocked.Exchange(ref latestSpectatorFrame,packet);
                else if(type!="pong")incoming.Enqueue(packet);
            }
        }
    }
    public async Task LeaveAsync() {
        try {
            if(Connected) {
                await SendAsync(new JObject { ["type"]="leave" });
                // Godot polls packets once per frame; allow the leave packet to be
                // consumed before the close frame or it can reserve a recovery slot.
                await Task.Delay(200);
                using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure,"",timeout.Token);
            }
        } catch(Exception) {} finally {Dispose();}
    }
    internal void InterruptTransport(){socket?.Abort();}
    public void Dispose(){if(!stop.IsCancellationRequested)stop.Cancel();socket?.Dispose();}
}
}
