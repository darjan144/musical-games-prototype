using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct BeatmapHit
{
    [Tooltip("When to press, in beats from the first beat of the song.")]
    public float beat;
}

/// <summary>Timing data for one song. Generated from the audio clip in the editor; the game reads only this.</summary>
[CreateAssetMenu(menuName = "Musical Games/Beatmap", fileName = "Beatmap")]
public class Beatmap : ScriptableObject
{
    public AudioClip clip;
    public float bpm = 120f;
    [Tooltip("Seconds from the start of the clip to the first beat.")]
    public float firstBeatOffset;
    [Tooltip("How long before the hit the circle appears and its ring starts closing.")]
    public float approachSeconds = 2f;
    public List<BeatmapHit> hits = new List<BeatmapHit>();

    public float SecondsPerBeat => 60f / bpm;

    public float BeatToTime(float beat) => firstBeatOffset + beat * SecondsPerBeat;

    public float TimeToBeat(float time) => (time - firstBeatOffset) / SecondsPerBeat;
}
