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
    const string WinterBossTrack = "Frozen Final Stand";
    const float WinterBattleMusicVolume = .55f;
    const float WinterMusicFadeSeconds = 1.35f, WinterPhaseTwoPitch = 1.2f;
    AudioSource winterBossMusic;
    bool ownsWinterBossClip;

    IEnumerator PrepareWinterBossMusic(int generation)
    {
        AudioClip clip;
        string path = Path.Combine(Application.streamingAssetsPath, "Frontend", WinterBossTrack + ".mp3");
        bool localFile = !Application.isMobilePlatform && File.Exists(path);
        if (localFile)
        {
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.MPEG))
            {
                ((DownloadHandlerAudioClip)request.downloadHandler).compressed = true;
                yield return request.SendWebRequest();
                if (generation != versusSerial) yield break;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("Winter soundtrack: " + request.error);
                    yield break;
                }
                clip = DownloadHandlerAudioClip.GetContent(request);
            }
        }
        else
        {
            var load = Resources.LoadAsync<AudioClip>("WinterBattle/" + WinterBossTrack);
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

        clip.name = WinterBossTrack;
        ownsWinterBossClip = localFile;
        winterBossMusic = visuals.AddComponent<AudioSource>();
        winterBossMusic.playOnAwake = false;
        winterBossMusic.spatialBlend = 0;
        winterBossMusic.clip = clip;
        winterBossMusic.loop = true;
        winterBossMusic.volume = 1;
    }

    void StartWinterBossMusic()
    {
        if (winterBossMusic) winterBossMusic.Play();
    }

    void UpdateWinterBossMusic()
    {
        if (!winterBossMusic || winterIntro || !winterBossMusic.isPlaying) return;
        float rate = (1 - WinterBattleMusicVolume) / WinterMusicFadeSeconds;
        winterBossMusic.volume = Mathf.MoveTowards(winterBossMusic.volume, WinterBattleMusicVolume,
            Time.unscaledDeltaTime * rate);
        if (match.WinterPhaseTwo)
            winterBossMusic.pitch = Mathf.MoveTowards(winterBossMusic.pitch, WinterPhaseTwoPitch,
                Time.unscaledDeltaTime * .5f);
    }

    void StopWinterBossMusic()
    {
        if (winterBossMusic) winterBossMusic.Stop();
    }

    void ClearWinterBossMusic()
    {
        if (!winterBossMusic) return;
        StopWinterBossMusic();
        var clip = winterBossMusic.clip;
        winterBossMusic.clip = null;
        Destroy(winterBossMusic);
        winterBossMusic = null;
        if (ownsWinterBossClip && clip) Destroy(clip);
        ownsWinterBossClip = false;
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
        ClearWinterBossMusic();
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
