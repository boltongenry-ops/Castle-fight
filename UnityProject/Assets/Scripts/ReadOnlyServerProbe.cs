using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CastleFight {
// Explicitly invoked probe only. No profile, room creation, gameplay commands or saved credentials.
// Intended for native Unity players. WebGL requires a browser WebSocket transport.
public static class ReadOnlyServerProbe {
    public static async Task<JObject> BrowseAsync(Uri endpoint,CancellationToken cancellation) {
        if(endpoint.Scheme!="wss" && !(endpoint.Scheme=="ws"&&endpoint.IsLoopback))throw new ArgumentException("Use WSS or local WS");
        using(var socket=new ClientWebSocket()) using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation)) {
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            await socket.ConnectAsync(endpoint,deadline.Token);
            var hello=Encoding.UTF8.GetBytes(new JObject { ["type"]="browse",["protocol"]=1,["build"]="0.6.19",["name"]="Unity lab probe",["race"]="Human Alliance",["snapshot_delta"]=1,["heartbeat"]=true }.ToString(Newtonsoft.Json.Formatting.None));
            await socket.SendAsync(new ArraySegment<byte>(hello),WebSocketMessageType.Text,true,deadline.Token);
            var buffer=new byte[16384];
            while(true) {
                using(var stream=new MemoryStream()) {
                    WebSocketReceiveResult part;
                    do {
                        part=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),deadline.Token);
                        if(part.MessageType==WebSocketMessageType.Close)throw new IOException("Peer closed before directory");
                        if(part.MessageType!=WebSocketMessageType.Text)throw new IOException("Expected JSON text");
                        stream.Write(buffer,0,part.Count);if(stream.Length>2097152)throw new IOException("Frame limit exceeded");
                    }while(!part.EndOfMessage);
                    var packet=JObject.Parse(Encoding.UTF8.GetString(stream.ToArray()));
                    if((string)packet["type"]=="error")throw new IOException((string)packet["message"]);
                    if((string)packet["type"]=="directory")return packet;
                }
            }
        }
    }
}
}
