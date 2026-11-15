using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
// Optional separate voice relay. The microphone is off until the player explicitly enables it.
public sealed class PcVoiceSession : MonoBehaviour {
    const int Rate=16000,Frame=320,MaxQueued=Rate/2;
    readonly object ringLock=new object();readonly float[] ring=new float[MaxQueued];int head,count;
    readonly ConcurrentQueue<string> statuses=new ConcurrentQueue<string>();
    readonly SemaphoreSlim sender=new SemaphoreSlim(1,1);
    CancellationTokenSource cancel;ClientWebSocket socket;AudioSource speaker;AudioClip playback,microphone;float[] capture;
    int readPosition,channels,sourceFrame,sourceRate;string device;bool microphoneEnabled,peerConnected;
#if UNITY_ANDROID && !UNITY_EDITOR
    bool permissionPending;
#endif
    public static string[] AvailableMicrophones {
        get {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))return Array.Empty<string>();
#endif
            return Microphone.devices;
        }
    }
    public bool MicrophoneEnabled=>microphoneEnabled;
    public bool TouchTalking {get;set;}
    public bool Connected=>socket!=null&&socket.State==WebSocketState.Open;
    public bool PeerConnected=>peerConnected;
    public bool OutputMuted{get;private set;}
    public float InputLevel{get;private set;}
    int receivedFrames;public int ReceivedFrames=>Interlocked.CompareExchange(ref receivedFrames,0,0);
    public void SetOutputMuted(bool muted){OutputMuted=muted;if(speaker)speaker.mute=muted;}
    internal void SendTestTone(){var frame=new byte[Frame*2];for(int i=0;i<Frame;i++){short sample=(short)(Math.Sin(i*.1)*8000);frame[2*i]=(byte)sample;frame[2*i+1]=(byte)(sample>>8);}_=SendFrame(frame);}
    public string Status {get;private set;}="Голос выключен";
#if CF_ONLINE_QA
    int testFrames;public int TestFrames=>Interlocked.CompareExchange(ref testFrames,0,0);
    public void TestSend(){var frame=new byte[Frame*2];for(int i=0;i<Frame;i++){short s=(short)(Math.Sin(i*.1)*8000);frame[2*i]=(byte)s;frame[2*i+1]=(byte)(s>>8);}_=SendFrame(frame);}
#endif
    public void Connect(string endpoint,string room,string ticket="") {
        Disconnect();
        if(!Uri.TryCreate(endpoint.Trim(),UriKind.Absolute,out var uri)||(uri.Scheme!="wss"&&!(uri.Scheme=="ws"&&uri.IsLoopback))){Status="Нужен адрес wss:// или локальный ws://";return;}
        if(!Regex.IsMatch(room??"",@"^[A-Za-z0-9_-]{16,96}$")){Status="Ключ комнаты: 16–96 латинских букв, цифр, _ или -";return;}
        cancel=new CancellationTokenSource();socket=new ClientWebSocket();Status="Подключаем голос…";_ = ConnectAsync(socket,uri,room,ticket,cancel.Token);
        if(!speaker){speaker=gameObject.AddComponent<AudioSource>();speaker.playOnAwake=false;speaker.loop=true;speaker.spatialBlend=0;playback=AudioClip.Create("Castle Fight voice",Rate,1,Rate,true,ReadPlayback);speaker.clip=playback;}
        speaker.mute=OutputMuted;speaker.Play();
    }
    async Task ConnectAsync(ClientWebSocket client,Uri uri,string room,string ticket,CancellationToken token) {
        try {
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){timeout.CancelAfter(TimeSpan.FromSeconds(10));await client.ConnectAsync(uri,timeout.Token);}
            var hello=Encoding.UTF8.GetBytes(new JObject { ["room"]=room,["ticket"]=ticket }.ToString(Newtonsoft.Json.Formatting.None));
            await client.SendAsync(new ArraySegment<byte>(hello),WebSocketMessageType.Text,true,token);
            var buffer=new byte[2048];
            while(!token.IsCancellationRequested&&client.State==WebSocketState.Open) {
                using(var memory=new MemoryStream()) {
                    WebSocketReceiveResult result;
                    do {result=await client.ReceiveAsync(new ArraySegment<byte>(buffer),token);if(result.MessageType==WebSocketMessageType.Close)return;memory.Write(buffer,0,result.Count);if(memory.Length>2048)throw new IOException("Слишком большой голосовой пакет");}while(!result.EndOfMessage);
                    var data=memory.ToArray();if(result.MessageType==WebSocketMessageType.Binary&&data.Length==Frame*2)PushAudio(data);
                    else if(result.MessageType==WebSocketMessageType.Text){var msg=JObject.Parse(Encoding.UTF8.GetString(data));string type=(string)msg["type"];if(type=="ready")statuses.Enqueue((int?)msg["peers"]==2?"peer-on":"ready");else if(type=="peer")statuses.Enqueue((bool?)msg["connected"]==true?"peer-on":"peer-off");}
                }
            }
        } catch(OperationCanceledException){} catch(Exception e){if(!token.IsCancellationRequested)statuses.Enqueue("error:"+e.GetBaseException().Message);}
        finally {if(!token.IsCancellationRequested)statuses.Enqueue("closed");}
    }
    public void ToggleMicrophone(string selectedDevice=null){if(microphoneEnabled){StopMicrophone();Status="Микрофон выключен";return;}if(!Connected){Status="Сначала подключите голос";return;}
#if UNITY_ANDROID && !UNITY_EDITOR
        if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)){if(permissionPending)return;permissionPending=true;var callbacks=new UnityEngine.Android.PermissionCallbacks();callbacks.PermissionGranted+=_=>{permissionPending=false;if(this&&Connected)ToggleMicrophone(selectedDevice);};callbacks.PermissionDenied+=_=>{permissionPending=false;Status="Нет разрешения на использование микрофона";};callbacks.PermissionDeniedAndDontAskAgain+=_=>{permissionPending=false;Status="Разрешите микрофон в настройках Android";};Status="Ожидаем разрешение на микрофон";UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone,callbacks);return;}
#endif
        if(Microphone.devices.Length==0){Status="Микрофон не найден";return;}device=string.IsNullOrEmpty(selectedDevice)?Microphone.devices[0]:selectedDevice;
        try {microphone=Microphone.Start(device,true,1,Rate);if(!microphone||microphone.frequency<8000||microphone.frequency>192000){StopMicrophone();Status="Не удалось запустить микрофон";return;}sourceRate=microphone.frequency;sourceFrame=Mathf.Max(1,Mathf.RoundToInt(sourceRate/50f));channels=Math.Max(1,microphone.channels);capture=new float[sourceFrame*channels];readPosition=0;microphoneEnabled=true;Status=Application.isMobilePlatform?"Микрофон включён · удерживайте «ГОВОРИТЬ»":"Микрофон включён · удерживайте левый Alt";}catch(Exception e){StopMicrophone();Status="Микрофон: "+e.Message;}
    }
    void StopMicrophone(){if(!string.IsNullOrEmpty(device))Microphone.End(device);if(microphone)Destroy(microphone);microphone=null;capture=null;device=null;microphoneEnabled=false;InputLevel=0;}
    void Update() {
        while(statuses.TryDequeue(out string msg)){if(msg=="peer-on"){peerConnected=true;Status="Собеседник подключён";}else if(msg=="peer-off"){peerConnected=false;Status="Ожидаем собеседника";}else if(msg=="ready")Status="Ожидаем собеседника";else if(msg.StartsWith("error:"))Status="Голос: "+msg.Substring(6);else if(msg=="closed"){peerConnected=false;StopMicrophone();Status="Голосовое соединение закрыто";}}
        if(!microphoneEnabled||!microphone||!Connected)return;
        InputLevel=Mathf.MoveTowards(InputLevel,0,Time.unscaledDeltaTime*3);int position=Microphone.GetPosition(device);if(position<0)return;int available=(position-readPosition+microphone.samples)%microphone.samples;
        if(available>sourceRate/4){readPosition=(position-sourceFrame+microphone.samples)%microphone.samples;available=sourceFrame;}
        while(available>=sourceFrame){if(!microphone.GetData(capture,readPosition)){StopMicrophone();Status="Не удалось прочитать микрофон";return;}readPosition=(readPosition+sourceFrame)%microphone.samples;available-=sourceFrame;
            float peak=0;foreach(float sample in capture)peak=Mathf.Max(peak,Mathf.Abs(sample));InputLevel=Mathf.Max(InputLevel,peak);if(!(Input.GetKey(KeyCode.LeftAlt)||TouchTalking)||!peerConnected)continue;byte[] frame=new byte[Frame*2];for(int i=0;i<Frame;i++){float positionInFrame=i*(float)sourceFrame/Frame;int lo=Mathf.Min(sourceFrame-1,Mathf.FloorToInt(positionInFrame)),hi=Mathf.Min(sourceFrame-1,lo+1);float fraction=positionInFrame-lo,sum=0;for(int c=0;c<channels;c++)sum+=Mathf.Lerp(capture[lo*channels+c],capture[hi*channels+c],fraction);short s=(short)(Mathf.Clamp(sum/channels,-1,1)*32767);frame[2*i]=(byte)s;frame[2*i+1]=(byte)(s>>8);} _=SendFrame(frame);}
    }
    async Task SendFrame(byte[] frame) {if(!await sender.WaitAsync(0))return;try {var client=socket;var token=cancel?.Token??CancellationToken.None;if(client!=null&&client.State==WebSocketState.Open)await client.SendAsync(new ArraySegment<byte>(frame),WebSocketMessageType.Binary,true,token);}catch(OperationCanceledException){}catch(Exception e){statuses.Enqueue("error:"+e.GetBaseException().Message);}finally{sender.Release();}}
    void PushAudio(byte[] data){Interlocked.Increment(ref receivedFrames);
#if CF_ONLINE_QA
        Interlocked.Increment(ref testFrames);
#endif
        lock(ringLock){for(int i=0;i<data.Length;i+=2){short value=(short)(data[i]|data[i+1]<<8);if(count==ring.Length){head=(head+1)%ring.Length;count--;}ring[(head+count)%ring.Length]=value/32768f;count++;}}}
    void ReadPlayback(float[] data){lock(ringLock){for(int i=0;i<data.Length;i++){if(count==0)data[i]=0;else {data[i]=ring[head];head=(head+1)%ring.Length;count--;}}}}
    public void Disconnect(){StopMicrophone();cancel?.Cancel();socket?.Dispose();socket=null;cancel=null;peerConnected=false;lock(ringLock){head=count=0;}if(speaker)speaker.Stop();while(statuses.TryDequeue(out _)){}Status="Голос выключен";}
    void OnDestroy(){Disconnect();if(playback)Destroy(playback);}
}
}
