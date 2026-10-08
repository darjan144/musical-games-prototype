using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Game 1. The start button plays the beatmap's song; each hit gets a circle, and any piano key pops the earliest circle on screen.
/// A press counts if a circle is showing, otherwise nothing happens. A well-timed press gets a bigger celebration; nothing is ever marked wrong.
/// Hits that can be on screen together form a group, and each group is laid out as a centred row.
/// </summary>
public class RhythmGame : MonoBehaviour
{
    [SerializeField] Beatmap beatmap;
    [SerializeField] SongClock clock;
    [Tooltip("Left to right, evenly spaced. This is the widest row; smaller groups of hits use fewer circles, centred.")]
    [SerializeField] RhythmCircle[] circles;
    [SerializeField] Button startButton;
    [SerializeField] AudioClip hitSound;
    [Tooltip("How long a circle stays after its ring has closed before it fades away.")]
    [SerializeField] float lingerSeconds = 0.5f;
    [Tooltip("A press this close to the ring closing (before or after, seconds) gets the bigger celebration. Other presses still count.")]
    [SerializeField, Min(0f)] float perfectWindowSeconds = 0.2f;

    [Header("Authoring (editor only)")]
    [Tooltip("Play the song and tap any piano key where the hits belong. They replace the beatmap's hits when the song ends or Play mode stops.")]
    [SerializeField] bool recordHits;
    [Tooltip("Recorded taps snap to this fraction of a beat.")]
    [SerializeField] float recordSnap = 0.5f;

    struct ScheduledHit
    {
        public float time;
        public int slot;      // position in its row, from the left
        public int rowCount;  // how many circles that row has
    }

    readonly List<ScheduledHit> pending = new List<ScheduledHit>();
    readonly List<RhythmCircle> showing = new List<RhythmCircle>();
    readonly List<float> recordedTimes = new List<float>();
    int nextHit;
    bool started;
    Vector2 rowCentre;
    float spacing;

    void Start()
    {
        var first = ((RectTransform)circles[0].transform).anchoredPosition;
        var last = ((RectTransform)circles[circles.Length - 1].transform).anchoredPosition;
        rowCentre = (first + last) / 2f;
        spacing = circles.Length > 1 ? (last.x - first.x) / (circles.Length - 1) : 0f;


        // The button click also satisfies the browser rule that audio may only start after a user action.
        startButton.onClick.AddListener(Begin);
    }

    void Update()
    {
        if (!started) return;

        bool pressed = TomplayInput.AnyKeyPressedThisFrame();

        if (!clock.IsPlaying)
        {
            Finish();
            return;
        }

        float now = clock.Time;
        if (recordHits)
        {
            if (pressed)
            {
                recordedTimes.Add(now);
                AudioManager.Instance.PlaySfx(hitSound);
            }
            return;
        }

        while (nextHit < pending.Count)
        {
            var hit = pending[nextHit];
            if (now < hit.time - beatmap.approachSeconds) break;
            if (now > hit.time)
            {
                // Waited for a circle until its moment passed; nothing left to show.
                nextHit++;
                continue;
            }

            // A circle is never restarted while it is showing, and a group too long for one row
            // only starts its next row once the rings already on screen have closed.
            var circle = circles[hit.slot];
            if (circle.IsActive || (hit.slot == 0 && AnyRingStillClosing(now))) break;

            float x = rowCentre.x + (hit.slot - (hit.rowCount - 1) / 2f) * spacing;
            ((RectTransform)circle.transform).anchoredPosition = new Vector2(x, rowCentre.y);
            circle.Show(hit.time, beatmap.approachSeconds, lingerSeconds);
            showing.Add(circle);
            nextHit++;
        }

        foreach (var circle in showing) circle.Tick(now);
        showing.RemoveAll(circle => !circle.IsActive);

        if (pressed && showing.Count > 0)
        {
            bool perfect = Mathf.Abs(now - showing[0].HitTime) <= perfectWindowSeconds;
            showing[0].Hit(perfect);
            showing.RemoveAt(0);
            AudioManager.Instance.PlaySfx(hitSound);
        }
    }

    bool AnyRingStillClosing(float now)
    {
        foreach (var circle in showing)
            if (circle.HitTime > now) return true;
        return false;
    }

    // Splits the hits into groups that can be on screen together and gives each hit its place in a centred row.
    void Schedule()
    {
        var times = new List<float>();
        foreach (var hit in beatmap.hits) times.Add(beatmap.BeatToTime(hit.beat));
        times.Sort();

        pending.Clear();
        float onScreenSeconds = beatmap.approachSeconds + lingerSeconds;
        int groupStart = 0;
        for (int i = 1; i <= times.Count; i++)
        {
            if (i < times.Count && times[i] - times[i - 1] <= onScreenSeconds) continue;

            int groupCount = i - groupStart;
            for (int index = 0; index < groupCount; index++)
            {
                int slot = index % circles.Length;
                pending.Add(new ScheduledHit
                {
                    time = times[groupStart + index],
                    slot = slot,
                    rowCount = Mathf.Min(circles.Length, groupCount - (index - slot)),
                });
            }
            groupStart = i;
        }
    }

    void Begin()
    {
        if (started) return;
        Schedule();
        showing.Clear();
        recordedTimes.Clear();
        nextHit = 0;
        started = true;
        startButton.gameObject.SetActive(false);
        clock.Play(beatmap.clip);
    }

    void Finish()
    {
        started = false;
        SaveRecording();
        startButton.gameObject.SetActive(true);
    }

    void OnDisable()
    {
        SaveRecording();
    }

    void SaveRecording()
    {
#if UNITY_EDITOR
        if (!recordHits || recordedTimes.Count == 0) return;

        beatmap.hits.Clear();
        float previousBeat = float.MinValue;
        foreach (float time in recordedTimes)
        {
            float beat = Mathf.Round(beatmap.TimeToBeat(time) / recordSnap) * recordSnap;
            if (beat <= previousBeat) continue;
            beatmap.hits.Add(new BeatmapHit { beat = beat });
            previousBeat = beat;
        }
        Debug.Log("Recorded " + beatmap.hits.Count + " hits into " + beatmap.name, beatmap);
        recordedTimes.Clear();
        UnityEditor.EditorUtility.SetDirty(beatmap);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(beatmap);
#endif
    }
}
