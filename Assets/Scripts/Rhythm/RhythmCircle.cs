using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Perfect hit")]
    [Tooltip("A ring that grows out of the circle and fades.")]
    [SerializeField] Image burst;
    [Tooltip("Small dots that fly outwards, spread evenly around the circle.")]
    [SerializeField] Image[] sparkles;
    [SerializeField] float perfectPunch = 0.5f;
    [SerializeField] float burstEndScale = 2.4f;
    [SerializeField] float sparkleDistance = 230f;
    [SerializeField] float perfectSeconds = 0.7f;

    CanvasGroup group;
    Sequence perfectTween;
    float hitTime, approachSeconds, lingerSeconds;

    public bool IsActive { get; private set; }
    public float HitTime => hitTime;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        HidePerfect();
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

    /// <summary>Pops the circle. A perfect hit adds a growing ring and a burst of dots.</summary>
    public void Hit(bool perfect)
    {
        if (!IsActive) return;
        IsActive = false;

        KillTweens();
        ring.gameObject.SetActive(false);
        group.alpha = 1f;
        body.localScale = Vector3.one;

        if (!perfect)
        {
            body.DOPunchScale(Vector3.one * hitPunch, 0.3f, 6, 0.5f);
            group.DOFade(0f, fadeOutSeconds).SetDelay(0.2f);
            return;
        }

        body.DOPunchScale(Vector3.one * perfectPunch, 0.45f, 6, 0.5f);
        group.DOFade(0f, fadeOutSeconds).SetDelay(perfectSeconds * 0.5f);

        perfectTween = DOTween.Sequence().SetLink(gameObject).OnComplete(HidePerfect);

        burst.gameObject.SetActive(true);
        burst.rectTransform.localScale = Vector3.one;
        SetAlpha(burst, 1f);
        perfectTween.Join(burst.rectTransform.DOScale(burstEndScale, perfectSeconds).SetEase(Ease.OutCubic));
        perfectTween.Join(burst.DOFade(0f, perfectSeconds).SetEase(Ease.InQuad));

        for (int i = 0; i < sparkles.Length; i++)
        {
            var sparkle = sparkles[i].rectTransform;
            float angle = (i + 0.5f) / sparkles.Length * Mathf.PI * 2f;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            sparkle.gameObject.SetActive(true);
            sparkle.anchoredPosition = Vector2.zero;
            sparkle.localScale = Vector3.one;
            perfectTween.Join(sparkle.DOAnchorPos(direction * sparkleDistance, perfectSeconds).SetEase(Ease.OutCubic));
            perfectTween.Join(sparkle.DOScale(0f, perfectSeconds).SetEase(Ease.InQuad));
        }
    }

    void HidePerfect()
    {
        if (burst != null) burst.gameObject.SetActive(false);
        foreach (var sparkle in sparkles) sparkle.gameObject.SetActive(false);
    }

    static void SetAlpha(Graphic graphic, float alpha)
    {
        var color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    void KillTweens()
    {
        if (group != null) group.DOKill();
        body.DOKill();
        perfectTween?.Kill();
        perfectTween = null;
        HidePerfect();
    }
}
