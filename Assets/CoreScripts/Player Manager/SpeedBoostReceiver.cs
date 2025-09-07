// SpeedBoostReceiver.cs — Snapshot-safe boost + TMP status + Soft Clamp to top speed after boost
// Unity 6000.x (Rigidbody.linearVelocity) compatible

using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro;

[DisallowMultipleComponent]
public class SpeedBoostReceiver : MonoBehaviour
{
    const float KMH_TO_MS = 1f / 3.6f;

    [Header("References (optional)")]
    public Rigidbody rb;
    [Tooltip("สคริปต์คุมรถที่มีฟิลด์ maxForwardSpeed และ acceleration (เช่น PlayerController)")]
    public MonoBehaviour playerController;

    [Header("Baseline cache (fallback)")]
    [Tooltip("Top speed เดิม (m/s) ที่แคชไว้เป็นค่า fallback")]
    [FormerlySerializedAs("baseMaxForwardSpeed")]
    public float TopSpeed = -1f;        // fallback only
    [Tooltip("Acceleration เดิม (m/s²) ที่แคชไว้เป็นค่า fallback")]
    public float baseAcceleration = -1f;

    [Header("Timing & End behaviour")]
    [Tooltip("ใช้เวลาจริง (ไม่โดน timeScale) สำหรับนับถอยหลังบูสต์")]
    public bool useRealtimeTimer = false;

    [Tooltip("ตัดความเร็วรวมให้ไม่เกินเพดานทันทีเมื่อบูสต์จบ")]
    public bool clampVelocityOnEnd = false;

    [Tooltip("ค่อย ๆ ลดความเร็วลงสู่เพดานหลังบูสต์จบ (แนะนำ)")]
    public bool softClampOnEnd = true;

    [Tooltip("ระยะเวลาที่ให้ค่อย ๆ ลดลงหาค่าเพดาน (วินาที)")]
    public float softClampDuration = 0.8f;

    // ---------- Status UI (TMP) ----------
    [Header("Status UI (TMP)")]
    public TMP_Text statusTMP;
    [Tooltip("ข้อความตอนปกติ")]
    public string normalText = "NORMAL";
    [Tooltip("ข้อความตอนบูสต์ (จะแนบเวลาที่เหลือ)")]
    public string boostText = "BOOST";
    public Color normalColor = Color.white;
    public Color boostColor = Color.yellow;

    // ---- runtime ----
    Coroutine boostCo;
    Coroutine softClampCo;
    float snapshotTop = -1f;   // ค่าขณะเริ่มบูสต์ (m/s)
    float snapshotAcc = -1f;   // ค่าขณะเริ่มบูสต์ (m/s²)

    public bool IsBoosting => boostCo != null;
    public float BoostEndsAt { get; private set; } = -1f;
    public float BoostRemaining =>
        IsBoosting ? Mathf.Max(0f, (useRealtimeTimer ? Time.unscaledTime : Time.time) - BoostEndsAt) * -1f : 0f;

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        if (!playerController) playerController = GetComponent<MonoBehaviour>();
        CacheBaseline();
        RefreshStatusUI(forceNormal: true);
    }

    void Update()
    {
        RefreshStatusUI();
    }

    void CacheBaseline()
    {
        if (!playerController) return;
        var t = playerController.GetType();
        var fMax = t.GetField("maxForwardSpeed");
        var fAcc = t.GetField("acceleration");
        if (fMax != null && TopSpeed < 0f) TopSpeed = (float)fMax.GetValue(playerController);
        if (fAcc != null && baseAcceleration < 0f) baseAcceleration = (float)fAcc.GetValue(playerController);
    }

    // =========================================================
    // Public APIs (Boost)
    // =========================================================

    /// <summary>คูณ Top speed และ Acceleration ชั่วคราว (หน่วยภายใน m/s, m/s²)</summary>
    public void ApplyTimedBoost(float speedMultiplier, float accelMultiplier, float duration, float extraVelocityMs = 0f)
    {
        speedMultiplier = Mathf.Max(1f, speedMultiplier);
        accelMultiplier = Mathf.Max(1f, accelMultiplier);
        duration = Mathf.Max(0.01f, duration);

        StartNewBoostAndApply(
            duration,
            (fMax, fAcc) =>
            {
                float curTop = (float)fMax.GetValue(playerController);
                float curAcc = (float)fAcc.GetValue(playerController);
                fMax.SetValue(playerController, curTop * speedMultiplier);
                fAcc.SetValue(playerController, curAcc * accelMultiplier);

                if (rb && extraVelocityMs > 0f)
                {
                    Vector3 add = transform.forward * extraVelocityMs;
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x + add.x, rb.linearVelocity.y, rb.linearVelocity.z + add.z);
                }
            });
    }

    /// <summary>ตั้ง Top speed เป็นค่า km/h ชั่วคราว (Override)</summary>
    public void ApplyTimedTopKmh(float targetTopKmh, float duration, bool onlyIfHigher = true,
                                 float accelMultiplier = 1f, float instantExtraKmh = 0f)
    {
        duration = Mathf.Max(0.01f, duration);
        accelMultiplier = Mathf.Max(1f, accelMultiplier);
        float targetMs = Mathf.Max(0.01f, targetTopKmh * KMH_TO_MS);
        float extraMs = Mathf.Max(0f, instantExtraKmh) * KMH_TO_MS;

        StartNewBoostAndApply(
            duration,
            (fMax, fAcc) =>
            {
                float curTop = (float)fMax.GetValue(playerController);
                float newTop = onlyIfHigher ? Mathf.Max(curTop, targetMs) : targetMs;
                fMax.SetValue(playerController, newTop);

                float curAcc = (float)fAcc.GetValue(playerController);
                fAcc.SetValue(playerController, curAcc * accelMultiplier);

                if (rb && extraMs > 0f)
                {
                    Vector3 add = transform.forward * extraMs;
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x + add.x, rb.linearVelocity.y, rb.linearVelocity.z + add.z);
                }
            });
    }

    /// <summary>เพิ่ม Top speed แบบ +Δ km/h ชั่วคราว (AddTop)</summary>
    public void ApplyAddTopKmh(float addKmh, float duration, float accelMultiplier = 1f, float instantExtraKmh = 0f)
    {
        duration = Mathf.Max(0.01f, duration);
        accelMultiplier = Mathf.Max(1f, accelMultiplier);
        float addMs = addKmh * KMH_TO_MS;
        float extraMs = Mathf.Max(0f, instantExtraKmh) * KMH_TO_MS;

        StartNewBoostAndApply(
            duration,
            (fMax, fAcc) =>
            {
                float curTop = (float)fMax.GetValue(playerController);
                fMax.SetValue(playerController, Mathf.Max(0.01f, curTop + addMs));

                float curAcc = (float)fAcc.GetValue(playerController);
                fAcc.SetValue(playerController, curAcc * accelMultiplier);

                if (rb && extraMs > 0f)
                {
                    Vector3 add = transform.forward * extraMs;
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x + add.x, rb.linearVelocity.y, rb.linearVelocity.z + add.z);
                }
            });
    }

    // =========================================================
    // Core boost flow (snapshot-safe, stack-safe)
    // =========================================================

    delegate void ApplyFn(FieldInfo fMax, FieldInfo fAcc);

    void StartNewBoostAndApply(float duration, ApplyFn apply)
    {
        CacheBaseline();

        var t = playerController ? playerController.GetType() : null;
        var fMx = t != null ? t.GetField("maxForwardSpeed") : null;
        var fAc = t != null ? t.GetField("acceleration") : null;

        if (playerController == null || fMx == null || fAc == null)
        {
            Debug.LogWarning("[SpeedBoostReceiver] playerController หรือฟิลด์ maxForwardSpeed/acceleration ไม่พบ");
            return;
        }

        // ยกเลิก soft-clamp เก่า (ถ้ามี)
        if (softClampCo != null) { StopCoroutine(softClampCo); softClampCo = null; }

        // ถ้ามีบูสต์ค้างอยู่ → คืนค่าก่อน เพื่อกัน 'เพดานลอย'
        if (boostCo != null) EndBoostImmediate(fMx, fAc);

        // ถ่ายรูปค่าปัจจุบันไว้เป็น snapshot
        snapshotTop = (float)fMx.GetValue(playerController);
        snapshotAcc = (float)fAc.GetValue(playerController);

        // นำค่าบูสต์ไปใช้
        apply.Invoke(fMx, fAc);

        // ตั้งเวลาและเริ่มนับ
        BoostEndsAt = (useRealtimeTimer ? Time.unscaledTime : Time.time) + duration;
        RefreshStatusUI(); // อัปเดตป้ายทันที
        boostCo = StartCoroutine(BoostCountdown(duration, fMx, fAc));
    }

    IEnumerator BoostCountdown(float duration, FieldInfo fMax, FieldInfo fAcc)
    {
        if (useRealtimeTimer) yield return new WaitForSecondsRealtime(duration);
        else yield return new WaitForSeconds(duration);

        // คืนค่าเดิม (snapshot ณ ตอนเริ่มบูสต์นี้)
        RestoreSnapshot(fMax, fAcc);

        // จัดการ overspeed หลังบูสต์จบ
        if (softClampOnEnd) softClampCo = StartCoroutine(SoftClampToCurrentTopSpeed(softClampDuration));
        else if (clampVelocityOnEnd) ClampVelocityToCurrentTopSpeed();

        boostCo = null;
        BoostEndsAt = -1f;
        RefreshStatusUI(); // กลับเป็น NORMAL (ถ้า softClamp ยังทำงาน ป้ายจะยัง NORMAL)
    }

    void EndBoostImmediate(FieldInfo fMax, FieldInfo fAcc)
    {
        StopCoroutine(boostCo);
        boostCo = null;

        RestoreSnapshot(fMax, fAcc);

        if (softClampOnEnd) softClampCo = StartCoroutine(SoftClampToCurrentTopSpeed(softClampDuration));
        else if (clampVelocityOnEnd) ClampVelocityToCurrentTopSpeed();

        snapshotTop = -1f;
        snapshotAcc = -1f;
        BoostEndsAt = -1f;
        RefreshStatusUI();
    }

    void RestoreSnapshot(FieldInfo fMax, FieldInfo fAcc)
    {
        if (playerController == null) return;

        if (fMax != null)
        {
            float back = (snapshotTop >= 0f) ? snapshotTop : (TopSpeed >= 0f ? TopSpeed : (float)fMax.GetValue(playerController));
            fMax.SetValue(playerController, back);
        }
        if (fAcc != null)
        {
            float back = (snapshotAcc >= 0f) ? snapshotAcc : (baseAcceleration >= 0f ? baseAcceleration : (float)fAcc.GetValue(playerController));
            fAcc.SetValue(playerController, back);
        }
    }

    // =========================================================
    // Overspeed handling
    // =========================================================

    float GetCurrentTopSpeedMs()
    {
        if (playerController == null) return TopSpeed > 0f ? TopSpeed : 20f;
        var t = playerController.GetType();
        var f = t.GetField("maxForwardSpeed");
        if (f != null)
        {
            float v = (float)f.GetValue(playerController);
            if (v > 0f) return v;
        }
        return TopSpeed > 0f ? TopSpeed : 20f;
    }

    void ClampVelocityToCurrentTopSpeed()
    {
        if (!rb) return;
        float cap = GetCurrentTopSpeedMs();
        var v = rb.linearVelocity;
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        float mag = flat.magnitude;
        if (mag > cap)
        {
            flat = flat.normalized * cap;
            rb.linearVelocity = new Vector3(flat.x, v.y, flat.z);
        }
    }

    IEnumerator SoftClampToCurrentTopSpeed(float duration)
    {
        if (!rb) yield break;

        duration = Mathf.Max(0.05f, duration);

        // ค่าคงที่ให้เข้าใกล้เพดานแบบ exponential ภายในเวลาที่กำหนด (~98% ที่ t=duration)
        // e^-lambda*duration = 0.02  => lambda ~= 3.912/duration
        float lambda = 3.912f / duration;
        const float EPS = 0.02f; // เผื่อเล็กน้อย

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float dt = useRealtimeTimer ? Time.unscaledDeltaTime : Time.fixedDeltaTime;

            // ใช้ FixedUpdate cadence เพื่อไม่ตีกับระบบฟิสิกส์
            yield return new WaitForFixedUpdate();
            elapsed += dt;

            float cap = GetCurrentTopSpeedMs();
            var v = rb.linearVelocity;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float speed = flat.magnitude;

            if (speed <= cap + EPS) break;

            // Exponential smoothing เข้าหา cap
            float k = 1f - Mathf.Exp(-lambda * dt);
            float newSpeed = Mathf.Lerp(speed, cap, k);

            if (newSpeed < 0f) newSpeed = 0f;

            Vector3 newFlat = (speed > 1e-4f) ? flat * (newSpeed / speed) : Vector3.zero;
            rb.linearVelocity = new Vector3(newFlat.x, v.y, newFlat.z);
        }

        softClampCo = null;
    }

    // =========================================================

    void RefreshStatusUI(bool forceNormal = false)
    {
        if (!statusTMP) return;

        if (!forceNormal && IsBoosting)
        {
            float remain = Mathf.Max(0f, BoostEndsAt - (useRealtimeTimer ? Time.unscaledTime : Time.time));
            statusTMP.text = $"{boostText} {remain:0.0}s";
            statusTMP.color = boostColor;
        }
        else
        {
            statusTMP.text = normalText;
            statusTMP.color = normalColor;
        }
    }

    void OnDisable()
    {
        // ยกเลิก soft clamp ที่ค้าง
        if (softClampCo != null) { StopCoroutine(softClampCo); softClampCo = null; }

        // ถ้าถูกปิดระหว่างบูสต์ → คืนค่าให้ชัวร์
        var t = playerController ? playerController.GetType() : null;
        var fMx = t != null ? t.GetField("maxForwardSpeed") : null;
        var fAc = t != null ? t.GetField("acceleration") : null;

        if (boostCo != null)
        {
            EndBoostImmediate(fMx, fAc);
            
        }
        else
        {
            // ไม่ได้บูสต์อยู่ แต่เพื่อความปลอดภัย คืนเป็น fallback ถ้ามีค่า
            if (playerController)
            {
                if (fMx != null && TopSpeed >= 0f) fMx.SetValue(playerController, TopSpeed);
                if (fAc != null && baseAcceleration >= 0f) fAc.SetValue(playerController, baseAcceleration);
            }
            RefreshStatusUI(forceNormal: true);
        }
    }
}
