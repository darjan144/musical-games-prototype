using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One music source plus a fixed pool of sources for sound effects.
/// The pool caps how many effects can overlap, so key spamming can't pile up voices.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField, Min(1)] int sfxPoolSize = 8;
    [Tooltip("The same clip won't retrigger faster than this (seconds).")]
    [SerializeField, Min(0f)] float minRepeatInterval = 0.06f;
    [SerializeField, Range(0f, 1f)] float musicVolume = 1f;
    [SerializeField, Range(0f, 1f)] float sfxVolume = 1f;

    AudioSource music;
    AudioSource[] pool;
    float[] startedAt;
    readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();

    public AudioSource Music => music;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        music = CreateSource("Music");
        pool = new AudioSource[sfxPoolSize];
        startedAt = new float[sfxPoolSize];
        for (int i = 0; i < sfxPoolSize; i++)
            pool[i] = CreateSource("Sfx " + i);
    }

    AudioSource CreateSource(string sourceName)
    {
        var go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    public void PlayMusic(AudioClip clip, bool loop = false)
    {
        music.clip = clip;
        music.loop = loop;
        music.volume = musicVolume;
        music.Play();
    }

    public void StopMusic()
    {
        music.Stop();
    }

    /// <summary>Plays an effect on a pooled source. Returns false if it was dropped as a too-fast repeat.</summary>
    public bool PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return false;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(clip, out float last) && now - last < minRepeatInterval)
            return false;
        lastPlayed[clip] = now;

        int index = NextSourceIndex();
        var source = pool[index];
        startedAt[index] = now;
        source.Stop();
        source.clip = clip;
        source.volume = volume * sfxVolume;
        source.pitch = pitch;
        source.Play();
        return true;
    }

    // A free source if there is one, otherwise the one that has been playing longest.
    int NextSourceIndex()
    {
        int oldest = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            if (!pool[i].isPlaying) return i;
            if (startedAt[i] < startedAt[oldest]) oldest = i;
        }
        return oldest;
    }
}
