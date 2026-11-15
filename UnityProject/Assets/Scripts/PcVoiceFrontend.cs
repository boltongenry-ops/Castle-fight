using System;using Newtonsoft.Json.Linq;using UnityEngine;
namespace CastleFight {public sealed partial class PcGame {
    PcVoiceSession voice;bool showVoicePanel,voiceRequested;float voiceRequestAt;int voiceDevice;JObject voiceSessionInfo;
    static readonly Rect VoicePanelTalkRect=new Rect(320,431,640,64);
    void ReceiveVoiceSession(JObject packet){voiceSessionInfo=packet;if(!voiceRequested)return;voiceRequested=false;if(!voice)voice=gameObject.AddComponent<PcVoiceSession>();voice.Connect((string)packet["url"]??"",(string)packet["room"]??"",(string)packet["ticket"]??"");}
    void RequestVoiceConnection(){if(network==null||!network.Connected||networkSlot<0){Notice("Сначала войдите в сетевую комнату");return;}voiceRequested=true;voiceRequestAt=Time.realtimeSinceStartup;
        string prefix="cf_"+((string)networkRoom?["code"]??"")+"_";
        if(voiceSessionInfo!=null&&((string)voiceSessionInfo["room"]??"").StartsWith(prefix,StringComparison.Ordinal)&&(long?)voiceSessionInfo["expires"]>System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()+15){ReceiveVoiceSession(voiceSessionInfo);return;}
        SendNetwork(new JObject{["type"]="voice_info"});}
    void DrawVoicePanel(){
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.8f));LobbyPanel(new Rect(280,80,720,560));Text(320,111,640,44,"ГОЛОС КОМНАТЫ",heading);Accent(320,168,640);
        Text(320,190,640,55,voiceRequested?"Получаем доступ к голосовой комнате…":voice!=null?voice.Status:"Подключитесь, чтобы слышать собеседника. Микрофон включается отдельно.",new GUIStyle(label){wordWrap=true});
        GUI.enabled=networkSlot>=0&&!voiceRequested;
        if(LobbyButton("voice.connect",new Rect(320,262,305,54),voice!=null&&voice.Connected?"Отключить голос":"Подключить голос",true)){if(voice!=null&&voice.Connected)voice.Disconnect();else RequestVoiceConnection();}
        GUI.enabled=voice!=null&&voice.Connected;
        var devices=PcVoiceSession.AvailableMicrophones;if(devices.Length>0)voiceDevice=Mathf.Clamp(voiceDevice,0,devices.Length-1);
        if(LobbyButton("voice.mic",new Rect(640,262,320,54),voice!=null&&voice.MicrophoneEnabled?"Выключить микрофон":"Включить микрофон"))voice.ToggleMicrophone(devices.Length>0?devices[voiceDevice]:null);
        if(LobbyButton("voice.output",new Rect(320,331,305,54),voice!=null&&voice.OutputMuted?"Включить звук":"Заглушить собеседника"))voice.SetOutputMuted(!voice.OutputMuted);GUI.enabled=true;
        GUI.enabled=devices.Length>1&&(voice==null||!voice.MicrophoneEnabled);
        if(LobbyButton("voice.device",new Rect(640,331,320,54),devices.Length>0?"Микрофон: "+devices[voiceDevice]:"Микрофон не найден"))voiceDevice=(voiceDevice+1)%devices.Length;GUI.enabled=true;
        Text(320,393,640,28,Application.isMobilePlatform?"При первом включении Android запросит доступ к микрофону.":"Говорите с зажатым левым Alt или удерживайте кнопку ниже.",small);
        GUI.enabled=voice!=null&&voice.MicrophoneEnabled&&voice.Connected;LobbyButton("voice.talk",VoicePanelTalkRect,voice!=null&&voice.TouchTalking?"● ГОВОРИТЕ":"УДЕРЖИВАЙТЕ, ЧТОБЫ ГОВОРИТЬ",voice!=null&&voice.TouchTalking);GUI.enabled=true;
        float level=voice?voice.InputLevel:0;Fill(new Rect(320,507,640,6),new Color(.1f,.16f,.16f));Fill(new Rect(320,507,640*Mathf.Clamp01(level*3),6),new Color(.35f,.8f,.45f));
        Text(320,528,640,30,"Голос передаётся через VPS metal. Записи разговоров не сохраняются.",small);
        if(LobbyButton("voice.close",new Rect(320,573,640,48),"← Вернуться"))showVoicePanel=false;
    }
    void UpdateVoiceControls(){if(voiceRequested&&Time.realtimeSinceStartup-voiceRequestAt>15){voiceRequested=false;Notice("Сервис голоса не ответил. Повторите подключение");}if(!voice)return;bool held=false;if(voice.MicrophoneEnabled&&networkSlot>=0&&(showVoicePanel||(Application.isMobilePlatform&&page==Page.Match))){Rect target=showVoicePanel?VoicePanelTalkRect:MobileTalkRect;if(Input.GetMouseButton(0)&&target.Contains(GuiPoint(Input.mousePosition)))held=true;for(int i=0;i<Input.touchCount;i++){var t=Input.GetTouch(i);if(t.phase!=TouchPhase.Ended&&t.phase!=TouchPhase.Canceled&&target.Contains(GuiPoint(t.position)))held=true;}}voice.TouchTalking=held;}
    void DisposeVoice(){voiceRequested=false;voiceSessionInfo=null;if(voice){voice.Disconnect();Destroy(voice);voice=null;}showVoicePanel=false;}
}}
