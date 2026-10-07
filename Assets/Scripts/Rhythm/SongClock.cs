using UnityEngine;

/// <summary>
/// Song position in seconds. Advances smoothly every frame and is steered by the music source's
/// real position, which on WebGL only updates in coarse steps.
/// </summary>
public class SongClock : MonoBehaviour
{
    const float HardResync = 0.1f;
    const float Steering = 0.05f;

    AudioSource source;
    float time;

    public float Time => time;
    public bool IsPlaying { get; private set; }

    public void Play(AudioClip clip)
    {
        AudioManager.Instance.PlayMusic(clip);
        source = AudioManager.Instance.Music;
        time = 0f;
        IsPlaying = true;
    }

    public void Stop()
    {
        if (IsPlaying) AudioManager.Instance.StopMusic();
        IsPlaying = false;
    }

    void Update()
    {
        if (!IsPlaying) return;

        if (!source.isPlaying)
        {
            // Reached the end of the clip (a paused tab keeps isPlaying true).
            if (time >= source.clip.length - 0.5f) IsPlaying = false;
            return;
        }

        time += UnityEngine.Time.unscaledDeltaTime;
        float drift = source.time - time;
        if (Mathf.Abs(drift) > HardResync) time = source.time;
        else time += drift * Steering;
    }
}
