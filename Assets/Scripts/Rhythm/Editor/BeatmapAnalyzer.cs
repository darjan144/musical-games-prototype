using UnityEngine;

/// <summary>Editor-time audio analysis: finds tempo and first-beat offset of a clip.</summary>
public static class BeatmapAnalyzer
{
    public const int Hop = 512;

    public static float HopSeconds(AudioClip clip) => (float)Hop / clip.frequency;

    /// <summary>
    /// Per-frame "something started here" strength: the rise in loudness from one frame to the next.
    /// highBand emphasises bright transients (claps, hi-hats) over bass.
    /// </summary>
    public static float[] OnsetEnvelope(AudioClip clip, bool highBand = false)
    {
        int channels = clip.channels;
        var data = new float[clip.samples * channels];
        if (!clip.GetData(data, 0))
        {
            Debug.LogError("Can't read samples from " + clip.name + ". Set its Load Type to Decompress On Load.");
            return new float[0];
        }

        int frames = clip.samples / Hop;
        var envelope = new float[frames];
        float previousSample = 0f, previousEnergy = 0f;
        for (int f = 0; f < frames; f++)
        {
            float sum = 0f;
            for (int i = 0; i < Hop; i++)
            {
                int index = (f * Hop + i) * channels;
                float mono = 0f;
                for (int c = 0; c < channels; c++) mono += data[index + c];
                mono /= channels;
                float value = highBand ? mono - previousSample : mono;
                previousSample = mono;
                sum += value * value;
            }
            float energy = Mathf.Log(1f + 10000f * sum / Hop);
            envelope[f] = Mathf.Max(0f, energy - previousEnergy);
            previousEnergy = energy;
        }
        return envelope;
    }

    /// <summary>Estimates tempo and the time of the first beat. Returns false if the clip can't be read.</summary>
    public static bool EstimateTempo(AudioClip clip, out float bpm, out float firstBeatOffset, float minBpm = 70f, float maxBpm = 160f)
    {
        bpm = 0f;
        firstBeatOffset = 0f;
        var envelope = OnsetEnvelope(clip);
        if (envelope.Length == 0) return false;
        float hopSeconds = HopSeconds(clip);

        // Coarse: the beat length whose multiples line up best with the envelope.
        float bestScore = float.MinValue, coarseBpm = minBpm;
        for (float candidate = minBpm; candidate <= maxBpm; candidate += 0.1f)
        {
            float score = CombScore(envelope, 60f / candidate / hopSeconds, out _);
            if (score > bestScore) { bestScore = score; coarseBpm = candidate; }
        }

        // Fine: small steps around the coarse answer, so the grid doesn't drift over a whole song.
        bestScore = float.MinValue;
        float bestPhase = 0f;
        for (float candidate = coarseBpm - 0.1f; candidate <= coarseBpm + 0.1f; candidate += 0.005f)
        {
            float score = CombScore(envelope, 60f / candidate / hopSeconds, out float phase);
            if (score > bestScore) { bestScore = score; bpm = candidate; bestPhase = phase; }
        }

        bpm = Mathf.Round(bpm * 100f) / 100f;
        firstBeatOffset = bestPhase * hopSeconds;
        return true;
    }

    // Best sum of the envelope sampled on a grid with the given period (in frames), over all grid phases.
    static float CombScore(float[] envelope, float period, out float bestPhase)
    {
        float best = float.MinValue;
        bestPhase = 0f;
        for (float phase = 0f; phase < period; phase += 1f)
        {
            float sum = 0f;
            int count = 0;
            for (float position = phase; position < envelope.Length - 1; position += period)
            {
                int i = (int)position;
                float t = position - i;
                sum += Mathf.Lerp(envelope[i], envelope[i + 1], t);
                count++;
            }
            float score = sum / Mathf.Max(1, count);
            if (score > best) { best = score; bestPhase = phase; }
        }
        return best;
    }
}
