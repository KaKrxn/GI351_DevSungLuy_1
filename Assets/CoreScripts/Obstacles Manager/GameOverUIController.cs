using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUIController : MonoBehaviour
{
    [Header("Root")]
    public CanvasGroup panel;          // = GameOverPanel
    public Image dimmer;               // พื้นหลังมืด
    public RectTransform card;         // การ์ดกลางจอ
    public RectTransform buttonsGroup; // กล่องปุ่ม

    [Header("Texts")]
    public TMP_Text titleTMP;          // GAME OVER
    public TMP_Text scoreLabelTMP;     // "SCORE"
    public TMP_Text scoreValueTMP;     // ค่า Score (จะนับขึ้น)
    public TMP_Text bestLabelTMP;      // "BEST"
    public TMP_Text bestValueTMP;      // ค่า Best

    [Header("Timing (unscaled)")]
    public float fadeDuration = 0.25f;
    public float cardPopDuration = 0.35f;
    public float buttonsFadeDelay = 0.15f;
    public float buttonsFadeDuration = 0.20f;
    public float countUpDuration = 0.6f;       // เวลาไล่เลขแต้ม
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Button Pulse")]
    public Button primaryButton;       // ใส่ RestartBtn ถ้ามี
    public float pulseScale = 1.05f;
    public float pulsePeriod = 1.2f;

    // runtime
    Vector3 cardStartScale;
    bool isVisible;

    void Awake()
    {
        if (panel) { panel.alpha = 0f; panel.interactable = false; panel.blocksRaycasts = false; }
        if (dimmer) dimmer.color = new Color(0, 0, 0, 0);
        if (card) cardStartScale = card.localScale;
        if (buttonsGroup) buttonsGroup.localScale = Vector3.one;
        HideInstant();
    }

    void HideInstant()
    {
        if (!panel) return;
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
        if (dimmer) dimmer.color = new Color(0, 0, 0, 0);
        if (card) card.localScale = Vector3.one * 0.85f;
        if (buttonsGroup) buttonsGroup.gameObject.SetActive(false);
        isVisible = false;
    }

    public void Show(int score, int best)
    {
        if (!panel) return;
        StopAllCoroutines();
        StartCoroutine(ShowRoutine(score, best));
    }

    IEnumerator ShowRoutine(int score, int best)
    {
        isVisible = true;

        // เปิด Panel ทันที (ยังโปร่ง)
        panel.gameObject.SetActive(true);
        panel.interactable = true;
        panel.blocksRaycasts = true;

        // เตรียมค่าเริ่ม
        if (card) card.localScale = Vector3.one * 0.85f;
        if (dimmer) dimmer.color = new Color(0, 0, 0, 0);
        if (titleTMP) titleTMP.alpha = 0f;
        if (scoreValueTMP) scoreValueTMP.text = "0";
        if (bestValueTMP) bestValueTMP.text = best.ToString();
        if (buttonsGroup) { buttonsGroup.gameObject.SetActive(false); SetGroupAlpha(buttonsGroup, 0f); }

        // 1) Fade ทั้งจอ + Dimmer
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(t / fadeDuration));
            panel.alpha = k;
            if (dimmer) dimmer.color = new Color(0, 0, 0, Mathf.Lerp(0f, 0.65f, k));
            if (titleTMP) titleTMP.alpha = k;
            yield return null;
        }
        panel.alpha = 1f;

        // 2) Card pop (scale 0.85 -> 1.0)
        t = 0f;
        while (t < cardPopDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOutBack(Mathf.Clamp01(t / cardPopDuration));
            if (card) card.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, k);
            yield return null;
        }
        if (card) card.localScale = Vector3.one;

        // 3) Count-up score
        if (scoreValueTMP)
            yield return CountUpNumber(scoreValueTMP, 0, score, countUpDuration);

        // 4) ปุ่มโผล่ตามหลังนิดหน่อย
        if (buttonsGroup)
        {
            yield return new WaitForSecondsRealtime(buttonsFadeDelay);
            buttonsGroup.gameObject.SetActive(true);
            yield return FadeGroup(buttonsGroup, 0f, 1f, buttonsFadeDuration);
        }

        // 5) Pulse ปุ่มหลักเบา ๆ
        if (primaryButton) StartCoroutine(PulseButton(primaryButton.transform));
    }

    // ---------- Helpers ----------

    IEnumerator CountUpNumber(TMP_Text label, int from, int to, float dur)
    {
        if (!label) yield break;
        dur = Mathf.Max(0.05f, dur);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(t / dur));
            int v = Mathf.RoundToInt(Mathf.Lerp(from, to, k));
            label.text = v.ToString();
            yield return null;
        }
        label.text = to.ToString();
    }

    IEnumerator FadeGroup(RectTransform group, float from, float to, float dur)
    {
        CanvasGroup cg = group.GetComponent<CanvasGroup>();
        if (!cg) cg = group.gameObject.AddComponent<CanvasGroup>();
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(t / dur));
            cg.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }
        cg.alpha = to;
    }

    void SetGroupAlpha(RectTransform group, float a)
    {
        var cg = group.GetComponent<CanvasGroup>();
        if (!cg) cg = group.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = a;
    }

    IEnumerator PulseButton(Transform target)
    {
        if (!target) yield break;
        Vector3 baseScale = target.localScale;
        float t = 0f;
        while (isVisible)
        {
            // sine 0..1..0
            t += Time.unscaledDeltaTime;
            float s = 0.5f + 0.5f * Mathf.Sin((t / pulsePeriod) * Mathf.PI * 2f);
            float k = Mathf.Lerp(1f, pulseScale, s);
            target.localScale = baseScale * k;
            yield return null;
        }
        target.localScale = baseScale;
    }

    // เบา ๆ แบบ out-back ไม่ต้องใช้ DOTween
    float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
    }

    // เรียกซ่อน (ถ้าต้องการ)
    public void Hide()
    {
        StopAllCoroutines();
        isVisible = false;
        HideInstant();
        panel.gameObject.SetActive(false);
    }
}
