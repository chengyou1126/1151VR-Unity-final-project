using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum Sfx { Click, Pickup, Success, Error, Caught, Alarm, Door, Footstep, TimeTravel, GameOver, GameClear, Panel }

/// <summary>
/// 音效管理。目前沒有正式音檔，所以音效與背景音樂都是程式即時合成的「替代音」。
/// 要換成正式音檔：把檔案放進 Assets/Resources/Audio/，檔名與 Sfx 名稱相同（例如 Pickup.wav），
/// 背景音樂檔名用 BGM_場景名（例如 BGM_PastLab.mp3），就會自動取代。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    AudioSource sfxSource;
    AudioSource musicSource;
    readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
    readonly System.Random rng = new System.Random(7);

    public static void Play(Sfx id, float volume = 1f)
    {
        if (Instance != null) Instance.PlayOne(id, volume);
    }

    void Awake()
    {
        Instance = this;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.spatialBlend = 0f;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.spatialBlend = 0f;
        musicSource.loop = true;
        musicSource.volume = 0.3f;

        foreach (Sfx id in Enum.GetValues(typeof(Sfx)))
        {
            var custom = Resources.Load<AudioClip>("Audio/" + id);
            clips[id] = custom != null ? custom : Generate(id);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void PlayOne(Sfx id, float volume)
    {
        if (clips.TryGetValue(id, out var c) && c != null) sfxSource.PlayOneShot(c, volume);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyTrack(scene.name);
    }

    /// <summary>換背景音樂（同一個場景裡進入隱藏密室時用："SecretRoom"；回到實驗室用 "PastLab"）。</summary>
    public static void SetMusic(string track)
    {
        if (Instance != null) Instance.ApplyTrack(track);
    }

    void ApplyTrack(string track)
    {
        var custom = Resources.Load<AudioClip>("Audio/BGM_" + track);
        AudioClip clip = custom;
        if (clip == null)
        {
            switch (track)
            {
                case "ModernLab": clip = Drone("bgm_modern", 55f, 0.25f); break;
                case "PastLab": clip = Drone("bgm_past", 65.125f, 0.125f); break;
                case "SecretRoom": clip = Drone("bgm_secret", 49f, 0.5f); break;
                default: clip = Drone("bgm_menu", 73f, 0.125f); break;
            }
        }
        if (musicSource.clip != clip)
        {
            musicSource.clip = clip;
            musicSource.Play();
        }
    }

    // ───────────── 程式合成 ─────────────

    AudioClip Generate(Sfx id)
    {
        switch (id)
        {
            case Sfx.Click: return Chirp("click", 900f, 1200f, 0.06f, 0.4f);
            case Sfx.Panel: return Chirp("panel", 400f, 700f, 0.12f, 0.35f);
            case Sfx.Pickup: return Chirp("pickup", 600f, 1300f, 0.22f, 0.5f);
            case Sfx.Success: return Two("success", 660f, 990f, 0.35f, 0.5f);
            case Sfx.Error: return Chirp("error", 180f, 90f, 0.4f, 0.6f, square: true);
            case Sfx.Caught: return Siren("caught", 0.9f, 0.7f);
            case Sfx.Alarm: return Siren("alarm", 1.6f, 0.45f);
            case Sfx.Door: return Noise("door", 0.35f, 0.5f, 0.0045f);
            case Sfx.Footstep: return Noise("step", 0.09f, 0.5f, 0.02f);
            case Sfx.TimeTravel: return Chirp("travel", 120f, 1800f, 3f, 0.55f);
            case Sfx.GameOver: return Chirp("gameover", 440f, 70f, 1.6f, 0.6f, square: true);
            case Sfx.GameClear: return Two("clear", 523f, 784f, 1.2f, 0.5f);
            default: return Chirp("default", 440f, 440f, 0.1f, 0.3f);
        }
    }

    static AudioClip Make(string name, float seconds, Func<float, float> f)
    {
        const int rate = 44100;
        int n = Mathf.Max(1, (int)(rate * seconds));
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)rate), -1f, 1f);
        var c = AudioClip.Create(name, n, 1, rate, false);
        c.SetData(data, 0);
        return c;
    }

    static float Env(float t, float T)
    {
        return Mathf.Min(1f, t * 80f) * Mathf.Pow(Mathf.Clamp01(1f - t / T), 1.4f);
    }

    static AudioClip Chirp(string name, float f0, float f1, float T, float vol, bool square = false)
    {
        return Make(name, T, t =>
        {
            float phase = 2f * Mathf.PI * (f0 * t + (f1 - f0) * t * t / (2f * T));
            float s = Mathf.Sin(phase);
            if (square) s = Mathf.Sign(s) * 0.6f;
            return s * Env(t, T) * vol;
        });
    }

    static AudioClip Two(string name, float fa, float fb, float T, float vol)
    {
        return Make(name, T, t =>
        {
            float f = t < T * 0.4f ? fa : fb;
            return Mathf.Sin(2f * Mathf.PI * f * t) * Env(t, T) * vol;
        });
    }

    static AudioClip Siren(string name, float T, float vol)
    {
        return Make(name, T, t =>
        {
            float f = 700f + 350f * Mathf.Sin(2f * Mathf.PI * 2.5f * t);
            return Mathf.Sin(2f * Mathf.PI * f * t) * Env(t, T) * vol;
        });
    }

    AudioClip Noise(string name, float T, float vol, float lowpass)
    {
        float prev = 0f;
        return Make(name, T, t =>
        {
            float n = (float)(rng.NextDouble() * 2.0 - 1.0);
            prev += (n - prev) * Mathf.Clamp01(lowpass * 60f);
            return prev * Env(t, T) * vol * 2f;
        });
    }

    static AudioClip Drone(string name, float baseFreq, float lfoHz)
    {
        // 8 秒無縫循環：頻率都取 1/8 Hz 的整數倍
        return Make(name, 8f, t =>
        {
            float lfo = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * lfoHz * t);
            float s = Mathf.Sin(2f * Mathf.PI * baseFreq * t) * 0.5f
                    + Mathf.Sin(2f * Mathf.PI * baseFreq * 1.5f * t) * 0.25f
                    + Mathf.Sin(2f * Mathf.PI * baseFreq * 2f * t) * 0.12f;
            return s * lfo * 0.6f;
        });
    }
}
