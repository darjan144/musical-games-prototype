using DG.Tweening;
using UnityEngine;

/// <summary>A fixed target circle with a ring that closes onto it. The ring meeting the circle is the moment to press.</summary>
[RequireComponent(typeof(CanvasGroup))]
public class RhythmCircle : MonoBehaviour
{
    [SerializeField] RectTransform body;
    [SerializeField] RectTransform ring;
    [SerializeField] float ringStartScale = 3f;
    [SerializeField] float fadeInSeconds = 0.25f;
    [SerializeField] float fadeOutSeconds = 0.4f;
    [SerializeField] float hitPunch = 0.25f;

    CanvasGroup group;
    float hitTime, approachSeconds, lingerSeconds;

    public bool IsActive { get; private set; }
    public float HitTime => hitTime;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
    }

    void OnDisable()
    {
        KillTweens();
    }

    public void Show(float hitTime, float approachSeconds, float lingerSeconds)
    {
        this.hitTime = hitTime;
        this.approachSeconds = approachSeconds;
        this.lingerSeconds = lingerSeconds;
        IsActive = true;

        KillTweens();
        body.localScale = Vector3.one;
        ring.gameObject.SetActive(true);
        ring.localScale = Vector3.one * ringStartScale;
        group.DOFade(1f, fadeInSeconds);
    }

    /// <summary>Sizes the ring from the song position. Fades the circle away once its moment has passed.</summary>
    public void Tick(float songTime)
    {
        if (!IsActive) return;

        float progress = Mathf.Clamp01(1f - (hitTime - songTime) / approachSeconds);
        ring.localScale = Vector3.one * Mathf.Lerp(ringStartScale, 1f, progress);

        if (songTime > hitTime + lingerSeconds)
        {
            IsActive = false;
            KillTweens();
            group.DOFade(0f, fadeOutSeconds);
        }
    }

    public void Hit()
    {
        if (!IsActive) return;
        IsActive = false;

        KillTweens();
        ring.gameObject.SetActive(false);
        group.alpha = 1f;
        body.localScale = Vector3.one;
        body.DOPunchScale(Vector3.one * hitPunch, 0.3f, 6, 0.5f);
        group.DOFade(0f, fadeOutSeconds).SetDelay(0.2f);
    }

    void KillTweens()
    {
        if (group != null) group.DOKill();
        body.DOKill();
    }
}
