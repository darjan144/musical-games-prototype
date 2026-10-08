using UnityEngine;

/// <summary>
/// Game 3. Holding C, E or G pours that note's colour into the fluid from its own place on screen, swaying gently while it is held.
/// Letting go stops the colour. Holding all three turns the streams towards the centre, where they are stirred together.
/// Everything eases in and out: nothing on screen may flash, jump or move fast.
/// No sound is played here: the piano makes its own.
/// </summary>
public class HarmonyGame : MonoBehaviour
{
    [System.Serializable]
    class Voice
    {
        public Color color = Color.white;
        [Tooltip("Where the colour comes out: (0,0) is the bottom left of the screen, (1,1) the top right.")]
        public Vector2 source = new Vector2(0.5f, 0.1f);
        [System.NonSerialized] public float lastHeldTime = float.NegativeInfinity;
        // 0 = silent, 1 = fully pouring. Eases between the two so a stream never pops in or out.
        [System.NonSerialized] public float level;
    }

    [SerializeField] FluidSimulation fluid;
    [Tooltip("The three notes in order: C, E, G.")]
    [SerializeField] Voice[] voices =
    {
        new Voice { color = new Color(1f, 0.42f, 0.4f), source = new Vector2(0.17f, 0.1f) },
        new Voice { color = new Color(1f, 0.74f, 0.3f), source = new Vector2(0.5f, 0.1f) },
        new Voice { color = new Color(0.8f, 0.5f, 0.95f), source = new Vector2(0.83f, 0.1f) },
    };
    [Tooltip("A key still counts as held for this long after it reads as released, in case the piano's signal flickers.")]
    [SerializeField, Min(0f)] float releaseGraceSeconds = 0.08f;
    [Tooltip("How long a stream takes to swell up when its key goes down, and to die away when it is let go.")]
    [SerializeField, Min(0.01f)] float fadeSeconds = 0.6f;

    [Header("Stream (one note)")]
    [Tooltip("How much colour is poured in. Too much and the colours burn out to white.")]
    [SerializeField] float dyeStrength = 0.07f;
    [Tooltip("How hard the stream is pushed.")]
    [SerializeField] float pushStrength = 14f;
    [Tooltip("Sways per second. Keep it slow: fast repeating movement is exactly what this game must avoid.")]
    [SerializeField, Range(0f, 3f)] float vibrationHz = 1.2f;
    [Tooltip("How far the stream sways from side to side, as a fraction of the screen height.")]
    [SerializeField] float vibrationSize = 0.02f;
    [Tooltip("How much the stream's direction wags with each sway.")]
    [SerializeField] float vibrationWag = 0.3f;

    [Header("Chord (all three notes)")]
    [Tooltip("Where the colours meet.")]
    [SerializeField] Vector2 centre = new Vector2(0.5f, 0.55f);
    [Tooltip("The colour poured in at the centre while all three notes are held.")]
    [SerializeField] Color chordColor = new Color(1f, 0.8f, 0.55f);
    [Tooltip("How much of the centre colour is poured in.")]
    [SerializeField] float chordDyeStrength = 0.06f;
    [Tooltip("How long the streams take to turn towards the centre, and to turn back.")]
    [SerializeField, Min(0.01f)] float chordBlendSeconds = 1.5f;
    [Tooltip("How hard the centre is stirred.")]
    [SerializeField] float chordSwirlStrength = 10f;
    [Tooltip("Size of the circle the centre is stirred around, as a fraction of the screen height.")]
    [SerializeField] float chordRadius = 0.06f;
    [Tooltip("Turns per second of the stirring.")]
    [SerializeField, Range(0f, 1f)] float chordSpinHz = 0.15f;

    static readonly UnityEngine.InputSystem.Key[] NoteKeys = { TomplayInput.MiddleC, TomplayInput.MiddleE, TomplayInput.MiddleG };

    const int StirPoints = 3;

    float chordBlend;

    void Update()
    {
        float now = Time.time;
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

        bool chord = true;
        for (int i = 0; i < voices.Length; i++)
        {
            var voice = voices[i];
            if (TomplayInput.IsHeld(NoteKeys[i])) voice.lastHeldTime = now;

            bool held = now - voice.lastHeldTime <= releaseGraceSeconds;
            voice.level = Mathf.MoveTowards(voice.level, held ? 1f : 0f, dt / fadeSeconds);
            if (!held) chord = false;
        }
        chordBlend = Mathf.MoveTowards(chordBlend, chord ? 1f : 0f, dt / chordBlendSeconds);
        float turn = Mathf.SmoothStep(0f, 1f, chordBlend);

        // Splat amounts are per 60 fps frame; scale them so the stream is the same at any frame rate.
        float amount = dt * 60f;
        float swayPhase = now * vibrationHz * Mathf.PI * 2f;

        for (int i = 0; i < voices.Length; i++)
        {
            var voice = voices[i];
            if (voice.level <= 0f) continue;

            // Alone, a stream rises straight up in its own part of the screen; in a chord it leans over towards the centre.
            Vector2 toCentre = ToSquare(centre - voice.source).normalized;
            Vector2 direction = Vector2.Lerp(Vector2.up, toCentre, turn).normalized;
            var side = new Vector2(-direction.y, direction.x);
            float sway = Mathf.Sin(swayPhase + i * 2.1f);

            Vector2 position = voice.source + ToUv(side * (sway * vibrationSize));
            Vector2 force = (direction + side * (sway * vibrationWag)) * (pushStrength * amount * voice.level);
            fluid.Splat(position, force, voice.color * (dyeStrength * amount * voice.level));
        }

        if (turn > 0f) StirCentre(now, amount * turn, swayPhase);
    }

    // The centre colour is poured in at points that drift round a small, slowly breathing circle, which folds the streams together.
    void StirCentre(float now, float amount, float swayPhase)
    {
        float radius = chordRadius * (1f + 0.25f * Mathf.Sin(swayPhase));
        for (int i = 0; i < StirPoints; i++)
        {
            float angle = (now * chordSpinHz + (float)i / StirPoints) * Mathf.PI * 2f;
            var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var along = new Vector2(-outward.y, outward.x);

            fluid.Splat(centre + ToUv(outward * radius), along * (chordSwirlStrength * amount), chordColor * (chordDyeStrength * amount / StirPoints));
        }
    }

    // Screen positions run 0..1 both ways, so on a wide screen a step sideways covers more ground than the same step up.
    // "Square" units are fractions of the screen height in both directions.
    static Vector2 ToSquare(Vector2 uv)
    {
        return new Vector2(uv.x * Screen.width / Screen.height, uv.y);
    }

    static Vector2 ToUv(Vector2 square)
    {
        return new Vector2(square.x * Screen.height / Screen.width, square.y);
    }
}
