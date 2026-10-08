# Musical Games Prototype

Unity prototype of three short musical games for young children, played on a **Tomplay keyboard** (a toy piano that shows up to the computer as a regular keyboard and sends letters/digits). Each game teaches one concept: rhythm, melody, harmony. Source brief: `Games suggestions.pdf`. Key layout: `tomplay keyboard - mapping.pdf`.

## Project facts

- Unity **6000.3.11f1**, URP 17.3, uGUI + TextMesh Pro, Linear colour space.
- **Ships as WebGL.** WebGL consequences: no threads; audio can only start after a user click/keypress; the audio clock is coarse, so read song position from the playing `AudioSource` and smooth it; do audio analysis in the editor, never at runtime. Source music is finished mp3/wav/ogg only (no MIDI, no stems).
- **Input System package only** (`activeInputHandler: 1`). `UnityEngine.Input.GetKey*` throws — use `Keyboard.current` / `InputAction`.
- **DOTween** (free) in `Assets/Plugins/Demigiant`, `DOTWEEN` define set. Use it for all UI/sprite animation (pulses, pops, growth, movement) instead of Animator/coroutine lerps. Kill tweens on disable/scene unload (`transform.DOKill()` or `SetLink(gameObject)`).
- Unity MCP (`mcp__unity-mcp__*`, from `com.unity.ai.assistant`) is available for editor automation: `Unity_RunCommand` (run editor C#), `Unity_GetConsoleLogs`, scene captures. After writing scripts, check the console for compile errors before claiming something works.
- Existing assets: `Assets/Art` (backgrounds, button PSDs, particles, `Audio/` with click/pop/woosh/music), `Assets/Fonts` (TMP SDF fonts). `Assets/TutorialInfo` and `Readme.asset` are Unity template leftovers.

## Target structure

Four scenes, all in Build Settings, `MainMenu` first. Scene changes go through `SceneLoader.Instance.Load(name)` (self-creating singleton); UI buttons use the `SceneButton` component:

| Scene | Purpose |
|---|---|
| `MainMenu` | Pick one of the three games |
| `Game1_Rhythm` | Press any key in time with a pulse |
| `Game2_Melody` | Low/middle/high note moves a character to bottom/middle/top |
| `Game3_Harmony` | 2–3 children hold C, E, G together to grow a flower |

Code layout (create the rest as needed, don't scaffold ahead):

```
Assets/Scripts/
  Core/      shared: AudioManager, SceneLoader + SceneButton, TomplayInput (key list only so far; note mapping planned)
  Rhythm/    Beatmap, SongClock, RhythmCircle, RhythmGame; Editor/ has BeatmapAnalyzer + Beatmap inspector
  Melody/    MelodyGame, ScrollingBackground, BirdAnimation
  Harmony/   HarmonyGame
  Menu/      (planned)
Assets/Beatmaps/           one Beatmap asset per song
```

Audio rules:
- All sound goes through `AudioManager.Instance` (in each game scene, survives scene loads). `PlaySfx` uses a fixed pool of AudioSources: when all are busy it reuses the oldest, and it drops repeats of the same clip that arrive too fast. Never `AddComponent<AudioSource>` or `PlayClipAtPoint` per sound — children spam keys.
- Music plays on the manager's single music source; `SongClock` turns its position into a smooth song time.
- Clips that get analysed need Load Type = Decompress On Load (the analyser reads samples with `AudioClip.GetData`).

## Tomplay input

The keyboard has 21 white and 15 black keys (3 octaves starting on C, inferred from the black-key grouping in the mapping PDF — confirm actual pitches against the hardware).

| Octave | C | C# | D | D# | E | F | F# | G | G# | A | A# | B |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Low  | z | 1 | x | 2 | c | v | 3 | b | 4 | n | 5 | m |
| Mid  | a | 6 | s | 7 | d | f | 8 | g | 9 | h | 0 | j |
| High | q | i | w | o | e | r | p | t | k | y | l | u |

Rules:
- All key→note knowledge lives in one shared mapping (Core). Games ask for "a note was pressed/released", never for raw `Key.Z`.
- A normal PC keyboard must be able to play every game using the same letters, so development doesn't need the hardware.
- Untested hardware behaviour — don't assume: whether a held key gives a clean down/up or auto-repeats, and how many simultaneous keys it reports (rollover). Game 3 depends on both.

## The games

### Game 1 — Rhythm
Goal: pulse awareness, timing-based cause and effect.
- A pulsing object (drum / heart / dancing animal) beats at a steady tempo. The child presses **any** Tomplay key (pitch ignored) in time.
- Stage 1: simple pulse; each well-timed press → percussion sound + a star.
- Stage 2: after a run of well-timed presses, background music starts and the child is the "drummer".
- Stage 3: the required rhythm follows a familiar song (e.g. "If You're Happy and You Know It"); on completion the full song plays with a celebration animation.
- Inspiration is **osu!**, but only one mechanic: an approach circle shrinks onto a target, and the moment it meets the target is when to press. No cursor aiming, sliders, spinners, score or fail.
- Six target circles in a row at fixed positions (the song's longest run is six claps one beat apart). **No mouse, no aiming.**
- **Input is any of the 36 Tomplay keys** (pitch ignored), via `TomplayInput.AnyKeyPressedThisFrame()` in Core.
- **No score, no late/miss feedback.** A press either counts for the current circle → small positive feedback, or the circle just fades. A press within `perfectWindowSeconds` of the ring closing (on `RhythmGame`) gets a bigger celebration (ring burst + pastel dots, on `RhythmCircle`). Nothing negative is ever shown.
- Song: "If You're Happy and You Know It". Audio in `Assets/Sounds/rhythm/`: the instrumental mp3 (ripped from YouTube — placeholder, not cleared for release) and `clap.mp3` (royalty-free hit sound).
- Each song has a **beatmap asset** (tempo, first-beat offset, hit times in beats, approach time). Tempo/offset come from the "Analyze tempo from clip" button on the asset. The game reads only the clip and its asset; no external chart formats.
- Hits can be re-authored by ear: tick `Record Hits` on the `RhythmGame` component, press Play, tap any piano key through the song; the taps replace the beatmap's hits when the song ends or Play mode stops.
- A press pops the earliest circle currently on screen. Layout is automatic: hits that can be on screen together (gap ≤ approach + linger) form a group, and each group is shown as a centred row with one circle per hit (2 claps → 2 circles in the middle, 6 claps → the full row). A circle is never restarted while it is still showing; a group longer than the row continues in a new row once the current rings have closed.
- The song starts only from the on-screen start button (user's choice; the click also unlocks browser audio). No "press anything to start".
- Build order agreed with the user: the song with clap circles first (done as a first pass), then the pulse tutorial stage in front of it, then the rest of the stage flow.
- Timing must be driven by the song position, not `Time.time`/frame counts or free-running tweens, so the approach circle, music and hit judgement stay in sync.

### Game 2 — Melody
Goal: connect pitch (low/middle/high) with position (bottom/middle/top).
- Side-scroller (user's design, 2026-10-08): the character stays on the left while the `parallaxmountain` layers scroll right to left (`ScrollingBackground`, tiled sprites; far layers slower).
- Keys: **A = C = low level, D = E = middle, G = G = high** (`TomplayInput.MiddleC/E/G`), **held**. With nothing held the character falls (gravity, hand-rolled in `MelodyGame`, no Rigidbody) to the ground, which is below the low level and collects nothing. While it rests on the ground with nothing held, the background and items ease to a stop (`speedChangeSeconds`), so nothing is missed while the child waits.
- Items (pooled with `UnityEngine.Pool.ObjectPool`, prefab `Assets/Prefabs/Melody Item.prefab`) arrive from the right on the three levels and are collected by proximity (`collectRadius`). Missed items just leave. Goal: `goal` items (50), shown on the fill bar; then a short celebration and a new round.
- Spawning is in phrases: a row of 3–5 on one level (levels drawn from a shuffled bag so all three come round), or a staircase through all three. `levelChangeGap` is the time the child gets to change note.
- The character is the user's animated bird (`Assets/Birds`, `Assets/Animation/BirdAnimController`, triggers `ToIdle` / `ToFly`). `BirdAnimation.SetFlying` is called by `MelodyGame`: Idle only while resting on the ground, Fly whenever in the air (falling included). The item is a code-drawn placeholder star in `Assets/Art/Melody`.
- The only on-screen text is "Controls: A, D, G to fly!" (user's wording). Don't explain the levels; the child works them out.
- No note sounds from the game so far (the piano makes its own); only a pop on collect.

### Game 3 — Harmony
Goal: collaboration and building a chord together.
- Starts with an empty (black) scene. Needs 2–3 children.
- The brief's flower (stem → leaves → bloom) is replaced by the **fluid simulation** (user's decision, 2026-10-08): each held note pours its own colour into the fluid from its own place at the bottom of the screen (C left, E middle, G right), swaying slowly while held. All three held → the streams lean over to the centre, where an extra `chordColor` (picked by hand on the component) is stirred in.
- **Low stimulus, warm and fuzzy** (user's call, seizure safety): warm pastel colours (rose, amber, lilac; gold centre), everything eases in/out (`fadeSeconds`, `chordBlendSeconds`), sway ≤ ~1 Hz, slow stir, low dye so nothing burns out to white. Never add fast repeating motion, flashes or hard pop-ins here.
- Keys: **A = C, D = E, G = G** (middle octave only; `TomplayInput.MiddleC/E/G`). `HarmonyGame` (Scripts/Harmony) calls `FluidSimulation.Splat` every frame for each held note; everything is tunable on the component.
- **The game plays no sound** — the Tomplay keyboard makes its own.
- Releasing a key stops that colour; what is on screen fades by the fluid's density dissipation.
- In this scene the `Fluid Background` instance is overridden: start splats and pointer input off (only notes add colour), and softened (shading off, curl 5, splat radius 0.5, velocity dissipation 0.6, dye resolution 512, warm dark back colour).
- No theory text on screen ("C Major Chord" etc.) — reward is purely sensory.

## Design constraints (all games)

- **The games must be easy.** Players are children with special needs (autism, poor motor skills). When in doubt, choose the easier option: wide timing windows, slow approach, few things on screen, no mouse aiming or precise movement.
- No fail states, no punishing feedback, no harsh sounds or flashing. Wrong/late input is simply not rewarded.
- Minimal or no text in gameplay; communicate with shape, motion and sound.
- Large, readable visuals; generous timing windows; everything tunable from the Inspector (tempo, windows, streak lengths) so it can be adjusted during playtests.
- It's a prototype: favour simple, direct code per game over shared frameworks. Only input mapping, audio helpers and scene navigation are shared.
