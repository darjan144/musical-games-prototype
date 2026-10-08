using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;
using UnityEngine.UI;

/// <summary>
/// Game 2. The scene scrolls past a character that stays on the left. Holding C, E or G lifts it to the low, middle or high level;
/// with nothing held it falls back to the ground, where there is nothing to collect.
/// Items arrive from the right on the three levels and are collected by being at their height. Missed items just leave.
/// Collecting the goal number of items ends the round with a small celebration, then a new round starts.
/// </summary>
public class MelodyGame : MonoBehaviour
{
    [SerializeField] Camera view;
    [SerializeField] ScrollingBackground background;
    [Tooltip("Stays at the x it has in the scene; only its height changes.")]
    [SerializeField] Transform character;
    [Tooltip("Optional. Told whether the character is in the air or on the ground.")]
    [SerializeField] BirdAnimation bird;
    [SerializeField] SpriteRenderer itemPrefab;
    [Tooltip("Optional. A Filled image that fills up as items are collected.")]
    [SerializeField] Image progressFill;
    [SerializeField] AudioClip collectSound;

    [Header("World")]
    [Tooltip("How fast the ground and the items move to the left, in world units per second.")]
    [SerializeField, Min(0f)] float scrollSpeed = 3.5f;
    [Tooltip("How long the world takes to slow to a stop when the character rests on the ground, and to get going again.")]
    [SerializeField, Min(0.01f)] float speedChangeSeconds = 0.3f;

    [Header("Character")]
    [Tooltip("Height the character rests at when no note is held.")]
    [SerializeField] float groundY = -3.8f;
    [Tooltip("Heights of the three levels, in order: C (low), E (middle), G (high).")]
    [SerializeField] float[] levelHeights = { -1.6f, 0.6f, 2.8f };
    [Tooltip("Roughly how long the character takes to reach a level once its note is held.")]
    [SerializeField, Min(0.01f)] float riseSeconds = 0.15f;
    [Tooltip("How hard the character is pulled down when no note is held.")]
    [SerializeField, Min(0f)] float gravity = 25f;
    [Tooltip("A key still counts as held for this long after it reads as released, in case the piano's signal flickers.")]
    [SerializeField, Min(0f)] float releaseGraceSeconds = 0.08f;

    [Header("Items")]
    [Tooltip("How many items to collect to finish a round.")]
    [SerializeField, Min(1)] int goal = 50;
    [Tooltip("An item is collected when it comes this close to the character. Bigger is easier.")]
    [SerializeField, Min(0f)] float collectRadius = 1.1f;
    [Tooltip("Distance between items in a row on the same level.")]
    [SerializeField, Min(0.1f)] float itemSpacing = 2f;
    [Tooltip("Empty stretch before items on a different level, which is the time the child gets to change note.")]
    [SerializeField, Min(0.1f)] float levelChangeGap = 5f;
    [Tooltip("Fewest and most items in a row on one level.")]
    [SerializeField] Vector2Int runLength = new Vector2Int(3, 5);
    [Tooltip("How often a staircase (all three levels in order, up or down) comes instead of a single row.")]
    [SerializeField, Range(0f, 1f)] float stairsChance = 0.35f;
    [Tooltip("Items on each step of a staircase.")]
    [SerializeField, Min(1)] int stairStepLength = 2;

    [Header("Round end")]
    [Tooltip("How long the celebration lasts before a new round starts.")]
    [SerializeField, Min(0f)] float celebrationSeconds = 4f;

    static readonly Key[] NoteKeys = { TomplayInput.MiddleC, TomplayInput.MiddleE, TomplayInput.MiddleG };

    ObjectPool<SpriteRenderer> pool;
    readonly List<SpriteRenderer> items = new List<SpriteRenderer>();
    readonly Queue<int> upcoming = new Queue<int>();   // levels of the next items, in order
    readonly List<int> levelBag = new List<int>();

    readonly float[] lastHeldTime = new float[NoteKeys.Length];
    readonly int[] pressOrder = new int[NoteKeys.Length];
    int presses;

    Vector3 characterScale;
    Vector3 itemScale;
    Color itemColor;
    float velocityY;
    bool resting;
    float currentSpeed;
    float distanceToNextItem;
    int lastLevel = -1;
    bool lastPhraseWasStairs;
    int collected;
    float celebrationLeft;

    void Start()
    {
        characterScale = character.localScale;
        itemScale = itemPrefab.transform.localScale;
        itemColor = itemPrefab.color;
        for (int i = 0; i < lastHeldTime.Length; i++) lastHeldTime[i] = float.NegativeInfinity;

        pool = new ObjectPool<SpriteRenderer>(
            () => Instantiate(itemPrefab, transform),
            item => item.gameObject.SetActive(true),
            item => item.gameObject.SetActive(false),
            item => Destroy(item.gameObject));

        var position = character.position;
        position.y = groundY;
        character.position = position;
        ShowProgress();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        MoveCharacter(dt);
        // In the air is flying, whether rising to a note or falling with nothing held.
        if (bird != null) bird.SetFlying(!resting);

        // Resting on the ground with no note held pauses the world, so nothing is missed while the child waits.
        float targetSpeed = resting ? 0f : scrollSpeed;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, scrollSpeed * dt / speedChangeSeconds);
        float distance = currentSpeed * dt;

        background.Scroll(distance);
        MoveItems(distance);

        if (celebrationLeft > 0f)
        {
            celebrationLeft -= dt;
            if (celebrationLeft <= 0f) NewRound();
        }
        else
        {
            SpawnItems(distance);
        }
    }

    void OnDisable()
    {
        character.DOKill();
        if (progressFill != null) progressFill.transform.DOKill();
    }

    // The level of the note being held, or -1 for none. With several held, the one pressed last wins.
    int HeldLevel()
    {
        float now = Time.time;
        int level = -1;
        for (int i = 0; i < NoteKeys.Length; i++)
        {
            if (TomplayInput.WasPressedThisFrame(NoteKeys[i])) pressOrder[i] = ++presses;
            if (TomplayInput.IsHeld(NoteKeys[i])) lastHeldTime[i] = now;

            bool held = now - lastHeldTime[i] <= releaseGraceSeconds;
            if (held && (level < 0 || pressOrder[i] > pressOrder[level])) level = i;
        }
        return level;
    }

    void MoveCharacter(float dt)
    {
        var position = character.position;
        int level = HeldLevel();
        resting = false;
        if (level >= 0)
        {
            position.y = Mathf.SmoothDamp(position.y, levelHeights[level], ref velocityY, riseSeconds, Mathf.Infinity, dt);
        }
        else
        {
            velocityY -= gravity * dt;
            position.y += velocityY * dt;
            if (position.y <= groundY)
            {
                position.y = groundY;
                velocityY = 0f;
                resting = true;
            }
        }
        character.position = position;
    }

    void MoveItems(float distance)
    {
        float leftEdge = view.transform.position.x - view.orthographicSize * view.aspect - 1f;
        Vector2 characterPosition = character.position;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            item.transform.position += Vector3.left * distance;

            if (Vector2.Distance(item.transform.position, characterPosition) <= collectRadius)
            {
                items.RemoveAt(i);
                Collect(item);
            }
            else if (item.transform.position.x < leftEdge)
            {
                items.RemoveAt(i);
                pool.Release(item);
            }
        }
    }

    void SpawnItems(float distance)
    {
        float rightEdge = view.transform.position.x + view.orthographicSize * view.aspect + 1f;

        distanceToNextItem -= distance;
        while (distanceToNextItem <= 0f)
        {
            if (upcoming.Count == 0) QueuePhrase();
            int level = upcoming.Dequeue();

            var item = pool.Get();
            item.transform.localScale = itemScale;
            item.color = itemColor;
            // distanceToNextItem is how far past the edge this item already should be, which keeps the spacing exact.
            item.transform.position = new Vector3(rightEdge + distanceToNextItem, levelHeights[level], 0f);
            items.Add(item);
            lastLevel = level;

            if (upcoming.Count == 0) QueuePhrase();
            distanceToNextItem += upcoming.Peek() == level ? itemSpacing : levelChangeGap;
        }
    }

    // Queues the next few items: a row on one level, or a staircase through all three.
    // Rows take their level from a shuffled bag of all the levels, so every level comes round before any repeats.
    void QueuePhrase()
    {
        if (!lastPhraseWasStairs && Random.value < stairsChance)
        {
            bool up = Random.value < 0.5f;
            for (int step = 0; step < levelHeights.Length; step++)
            {
                int level = up ? step : levelHeights.Length - 1 - step;
                for (int n = 0; n < stairStepLength; n++) upcoming.Enqueue(level);
            }
            lastPhraseWasStairs = true;
            return;
        }

        if (levelBag.Count == 0)
            for (int level = 0; level < levelHeights.Length; level++) levelBag.Add(level);

        // Not the level the last item was on, if the bag has another one.
        int pick = Random.Range(0, levelBag.Count);
        if (levelBag[pick] == lastLevel) pick = (pick + 1) % levelBag.Count;
        int rowLevel = levelBag[pick];
        levelBag.RemoveAt(pick);

        int count = Random.Range(runLength.x, runLength.y + 1);
        for (int n = 0; n < count; n++) upcoming.Enqueue(rowLevel);
        lastPhraseWasStairs = false;
    }

    void Collect(SpriteRenderer item)
    {
        PopAway(item);
        if (celebrationLeft > 0f) return;

        collected++;
        ShowProgress();
        AudioManager.Instance.PlaySfx(collectSound);

        character.DOKill();
        character.localScale = characterScale;
        character.localRotation = Quaternion.identity;
        character.DOPunchScale(characterScale * 0.2f, 0.25f, 4, 0.5f);

        if (collected >= goal) Celebrate();
    }

    void PopAway(SpriteRenderer item)
    {
        item.transform.DOScale(itemScale * 1.8f, 0.25f).SetEase(Ease.OutQuad).SetLink(item.gameObject);
        item.DOFade(0f, 0.25f).SetLink(item.gameObject).OnComplete(() => pool.Release(item));
    }

    void Celebrate()
    {
        celebrationLeft = celebrationSeconds;
        upcoming.Clear();

        foreach (var item in items) PopAway(item);
        items.Clear();

        character.DOLocalRotate(new Vector3(0f, 0f, -360f), 1.2f, RotateMode.FastBeyond360).SetEase(Ease.InOutSine).SetLoops(2);
        if (progressFill != null) progressFill.transform.DOPunchScale(Vector3.one * 0.15f, 1f, 3, 0.5f);
    }

    void NewRound()
    {
        collected = 0;
        distanceToNextItem = 0f;
        ShowProgress();
    }

    void ShowProgress()
    {
        if (progressFill != null) progressFill.fillAmount = (float)collected / goal;
    }
}
