using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace CastleFight {
public sealed partial class PcGame
{
    AudioSource winterWindAudio;
    AudioClip winterWindClip;
    const string EpochalBossTrack = "Frozen Final Stand";
    const string ProtocolBossTrack = "Krangs Battle";
    const float WinterBattleMusicVolume = .55f, ProtocolBattleMusicVolume = .48f;
    const float WinterMusicFadeSeconds = 1.35f, WinterPhaseTwoPitch = 1.2f;
    AudioSource bossSoundtrack;
    bool ownsBossSoundtrackClip;

    IEnumerator PrepareBossSoundtrack(int generation,bool? protocolTrack=null)
    {
        AudioClip clip;
        bool protocol=protocolTrack??match.LastProtocol;
        string track=protocol?ProtocolBossTrack:EpochalBossTrack;
        string path = Path.Combine(Application.streamingAssetsPath, "Frontend", track+(protocol?".ogg":".mp3"));
        bool localFile = !Application.isMobilePlatform && File.Exists(path);
        if (localFile)
        {
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,
                protocol?AudioType.OGGVORBIS:AudioType.MPEG))
            {
                ((DownloadHandlerAudioClip)request.downloadHandler).compressed = true;
                yield return request.SendWebRequest();
                if (generation != versusSerial) yield break;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("Epochal soundtrack: " + request.error);
                    yield break;
                }
                clip = DownloadHandlerAudioClip.GetContent(request);
            }
        }
        else
        {
            var load = Resources.LoadAsync<AudioClip>("WinterBattle/" + track);
            yield return load;
            if (generation != versusSerial) yield break;
            clip = load.asset as AudioClip;
        }

        if (!clip) yield break;
        if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        while (generation == versusSerial && clip.loadState == AudioDataLoadState.Loading)
            yield return null;
        if (generation != versusSerial || clip.loadState == AudioDataLoadState.Failed)
        {
            if (localFile) Destroy(clip);
            yield break;
        }

        clip.name = track;
        ownsBossSoundtrackClip = localFile;
        bossSoundtrack = visuals.AddComponent<AudioSource>();
        bossSoundtrack.playOnAwake = false;
        bossSoundtrack.spatialBlend = 0;
        bossSoundtrack.clip = clip;
        bossSoundtrack.loop = true;
        bossSoundtrack.volume = 1;
    }

    void StartBossSoundtrack()
    {
        if (bossSoundtrack) bossSoundtrack.Play();
    }

    void UpdateBossSoundtrack()
    {
        if (!bossSoundtrack || winterIntro || hordeIntro || protocolIntro || abyssIntro || match == null || !bossSoundtrack.isPlaying) return;
        float target=match.LastProtocol?ProtocolBattleMusicVolume:WinterBattleMusicVolume;
        float rate = (1 - target) / WinterMusicFadeSeconds;
        bossSoundtrack.volume = Mathf.MoveTowards(bossSoundtrack.volume, target,
            Time.unscaledDeltaTime * rate);
        if (match.WinterPhaseTwo || match.HordeSecondStage)
            bossSoundtrack.pitch = Mathf.MoveTowards(bossSoundtrack.pitch, WinterPhaseTwoPitch,
                Time.unscaledDeltaTime * .5f);
    }

    void StopBossSoundtrack()
    {
        if (bossSoundtrack) bossSoundtrack.Stop();
    }

    void ClearBossSoundtrack()
    {
        if (!bossSoundtrack) return;
        StopBossSoundtrack();
        var clip = bossSoundtrack.clip;
        bossSoundtrack.clip = null;
        Destroy(bossSoundtrack);
        bossSoundtrack = null;
        if (ownsBossSoundtrackClip && clip) Destroy(clip);
        ownsBossSoundtrackClip = false;
    }

    void PrepareWinterAudio()
    {
        winterWindClip = CreateWindClip();
        winterWindAudio = visuals.AddComponent<AudioSource>();
        winterWindAudio.clip = winterWindClip;
        winterWindAudio.loop = true;
        winterWindAudio.playOnAwake = false;
        winterWindAudio.spatialBlend = 0;
        winterWindAudio.volume = 0;
    }

    void UpdateWinterAudio(float surge)
    {
        if (!winterWindAudio) return;
        if (!winterWindAudio.isPlaying) winterWindAudio.Play();
        float target = winterIntro ? .43f + .45f * surge :
            match.WinterPhaseTwo ? .32f + .45f * surge : .18f + .35f * surge;
        winterWindAudio.volume = Mathf.MoveTowards(winterWindAudio.volume, target, Time.unscaledDeltaTime * .85f);
    }

    void ClearWinterAudio()
    {
        ClearBossSoundtrack();
        if (winterWindAudio)
        {
            winterWindAudio.Stop();
            Destroy(winterWindAudio);
        }
        if (winterWindClip) Destroy(winterWindClip);
        winterWindClip = null;
        winterWindAudio = null;
    }

    static AudioClip CreateWindClip()
    {
        const int rate = 16000, length = 12 * rate;
        var samples = new float[length];
        float body = 0, bass = 0, whistle = 0, high = 0;
        for (int i = -rate; i < length; i++)
        {
            uint hash;
            unchecked
            {
                hash = (uint)((i + length) % length);
                hash ^= hash >> 16; hash *= 0x7feb352d;
                hash ^= hash >> 15; hash *= 0x846ca68b;
                hash ^= hash >> 16;
            }
            float noise = (hash & 65535) / 32767.5f - 1;
            bass += (noise - bass) * .014f;
            body += (noise - body) * .074f;
            whistle += (noise - whistle) * .21f;
            high += (noise - high) * .45f;
            if (i < 0) continue;
            float time = i * Mathf.PI * 2 / length;
            float gust = .72f + .19f * Mathf.Sin(3 * time) + .09f * Mathf.Sin(7 * time);
            samples[i] = Mathf.Clamp(((body - bass) * 1.3f + bass * 1.6f +
                (whistle - body) * .62f + (high - whistle) * .11f) * gust, -.78f, .78f);
        }
        var clip = AudioClip.Create("Eternal winter wind", length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
}
