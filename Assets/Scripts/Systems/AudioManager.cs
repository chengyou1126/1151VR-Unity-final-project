using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum Sfx { Click, Pickup, Success, Error, Caught, Alarm, Door, Footstep, TimeTravel, GameOver, GameClear, Panel }

/// <summary>播放關卡配樂與音效，警示響起時降低配樂音量。</summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    const float MusicVolume = 0.50f;
    const float WarningMusicVolume = 0.10f;
    // 各關配樂倍率：1f 維持原音量，0.8f 播放八成，1.2f 播放一點二倍。
    const float Stage1Gain = 1f;  // Dark Fog
    const float Stage2Gain = 1f;  // Crypto
    const float Stage3Gain = 1f;  // 密室
    const float Stage4_1Gain = 1f;  // Volatile Reaction（逃脫）
    const float Stage4_2Gain = 1f;  // Reunited（回到現代）

    float trackGain = 1f;
    float mixVolume = MusicVolume;
    float duckUntil;

    void ApplyMusicVolume()
    {
        musicSource.volume = Mathf.Clamp01(mixVolume * trackGain);
    }

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
        ApplyMusicVolume();

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
        if (!clips.TryGetValue(id, out var c) || c == null) return;
        if (id == Sfx.Alarm || id == Sfx.Caught || id == Sfx.Error || id == Sfx.GameOver)
        {
            mixVolume = Mathf.Min(MusicVolume, WarningMusicVolume);
            ApplyMusicVolume();
            duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + c.length);
        }
        if (id == Sfx.Footstep) volume *= 0.25f;
        sfxSource.PlayOneShot(c, volume);
    }

    void Update()
    {
        if (Time.unscaledTime >= duckUntil && mixVolume < MusicVolume)
        {
            mixVolume = Mathf.MoveTowards(mixVolume, MusicVolume, (MusicVolume - WarningMusicVolume) / 0.75f * Time.unscaledDeltaTime);
            ApplyMusicVolume();
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool returning = scene.name == "ModernLab" && GameManager.Instance.Data.returningToGuide;
        ApplyTrack(returning ? "ReturnToGuide" : scene.name);
    }

    /// <summary>在同一個場景內切換關卡配樂。</summary>
    public static void SetMusic(string track)
    {
        if (Instance != null) Instance.ApplyTrack(track);
    }

    void ApplyTrack(string track)
    {
        switch (track)
        {
            case "ModernLab": trackGain = Stage1Gain; break;
            case "PastLab": trackGain = Stage2Gain; break;
            case "SecretRoom": trackGain = Stage3Gain; break;
            case "Escape": trackGain = Stage4_1Gain; break;
            case "ReturnToGuide": trackGain = Stage4_2Gain; break;
            default: trackGain = 1f; break;
        }
        ApplyMusicVolume();
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
            case Sfx.TimeTravel: return Chirp("travel", 120f, 1800f, 3f, 0.45f);
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
