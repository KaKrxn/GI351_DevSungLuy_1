using UnityEngine;
using System.Collections.Generic;

/// เล่น/หยุด VFX ตอน "กดเบรก" และหยุดเมื่อความเร็ว < เกณฑ์ หรือปล่อยปุ่มเบรก
/// ไม่แตะ PlayerController: อ่านสถานะเอง + ใช้ CurrentSpeedKmh ถ้ามี
[DefaultExecutionOrder(10)]
public class DriftFXController : MonoBehaviour
{
    [Header("References")]
    public PlayerController player;      // เว้นได้ เดี๋ยวหาเองจากพาเรนต์
    public Rigidbody rb;                 // เผื่อไม่มี PlayerController (จะคำนวณความเร็วเอง)

    [Tooltip("ชุดพาร์ติเคิลสำหรับเอฟเฟกต์เบรก (ลากมาวางได้หลายหัวฉีด, ซ้าย-ขวา)")]
    public ParticleSystem[] brakeFX;

    [Header("Emit Points (ทางเลือก)")]
    [Tooltip("ตำแหน่งที่อยากให้ออก VFX (เช่น จุดล้อ)")]
    public Transform[] emitPoints;
    [Tooltip("Prefab ของ ParticleSystem; ถ้ามี Emit Points และ brakeFX ยังว่าง จะถูกสร้างอัตโนมัติ")]
    public ParticleSystem particlePrefab;
    public bool createOnePerPointIfMissing = true;

    [Header("Trigger")]
    [Tooltip("ปุ่มเบรก (ตาม PlayerController ปัจจุบันใช้ Space)")]
    public KeyCode brakeKey = KeyCode.Space;
    [Tooltip("ความเร็วขั้นต่ำ (km/h) ที่ยังให้เอฟเฟกต์ทำงาน")]
    public float minSpeedKmh = 20f;

    [Header("Auto-Find (fallback)")]
    public bool autoFindFXByName = true;
    [Tooltip("ค้นหา ParticleSystem ลูกที่ชื่อมีคำนี้ (เช่น \"Brake\", \"Drift\")")]
    public string nameContains = "Brake";

    [Header("(เลือกได้) ปรับความหนาเอฟเฟกต์ตามความเร็ว")]
    public bool scaleEmissionBySpeed = false;
    [Tooltip("อัตราพ่นที่ความเร็ว = minSpeedKmh")]
    public float emissionAtMin = 10f;
    [Tooltip("อัตราพ่นที่ความเร็วสูง (ดูที่ speedForMax)")]
    public float emissionAtMax = 45f;
    [Tooltip("ถือว่า ‘เร็วมาก’ ที่ความเร็วนี้ (km/h) เพื่อแมปไป emissionAtMax")]
    public float speedForMax = 90f;

    void Reset()
    {
        player = GetComponentInParent<PlayerController>();
        rb = player ? player.GetComponent<Rigidbody>() : GetComponentInParent<Rigidbody>();
    }

    void Awake()
    {
        if (!player) player = GetComponentInParent<PlayerController>();
        if (!rb) rb = player ? player.GetComponent<Rigidbody>() : GetComponentInParent<Rigidbody>();

        // 1) ถ้าผู้ใช้กำหนด brakeFX แล้ว → ใช้ชุดนั้นเลย
        // 2) ถ้าไม่มี แต่มี emitPoints + prefab → สร้างตามจุด
        // 3) ถ้ายังไม่มี → ค้นหาจากชื่อในลูกหลาน (fallback)
        if (brakeFX == null || brakeFX.Length == 0)
        {
            if (createOnePerPointIfMissing && particlePrefab && emitPoints != null && emitPoints.Length > 0)
            {
                var list = new List<ParticleSystem>();
                foreach (var t in emitPoints)
                {
                    if (!t) continue;
                    var ps = Instantiate(particlePrefab, t.position, t.rotation, t);
                    list.Add(ps);
                }
                brakeFX = list.ToArray();
            }
            else if (autoFindFXByName)
            {
                var list = new List<ParticleSystem>();
                foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (!ps) continue;
                    if (ps.name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        list.Add(ps);
                }
                brakeFX = list.ToArray();
            }
        }

        Toggle(false); // ปิดไว้ก่อน
    }

    void OnDisable() => Toggle(false);

    void FixedUpdate()
    {
        // 1) ตรวจปุ่มเบรก
        bool braking = Input.GetKey(brakeKey);

        // 2) อ่านความเร็ว (km/h)
        float speedKmh = 0f;
        if (player != null)
        {
            speedKmh = player.CurrentSpeedKmh; // ใช้เมธอดใน PlayerController (แม่น/พร้อม)
        }
        else if (rb != null)
        {
            // เผื่อไม่มี PlayerController: คำนวณเองจาก rigidbody
            Vector3 v = rb.linearVelocity; // Unity 6 ใช้ linearVelocity ได้เช่นกัน
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            speedKmh = flat.magnitude * 3.6f;
        }

        // 3) เงื่อนไขเปิด: กดเบรก และ ความเร็ว >= 20 km/h
        bool shouldPlay = braking && speedKmh >= minSpeedKmh;

        if (!scaleEmissionBySpeed)
        {
            Toggle(shouldPlay);
        }
        else
        {
            float t = Mathf.InverseLerp(minSpeedKmh, speedForMax, speedKmh);
            float rate = Mathf.Lerp(emissionAtMin, emissionAtMax, t);
            Set(shouldPlay, rate);
        }
    }

    // ---------- Helpers ----------
    void Toggle(bool on)
    {
        if (brakeFX == null) return;
        for (int i = 0; i < brakeFX.Length; i++)
        {
            var ps = brakeFX[i];
            if (!ps) continue;
            var em = ps.emission;
            em.enabled = on;

            if (on && !ps.isPlaying) ps.Play(true);
            else if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void Set(bool on, float rate)
    {
        if (brakeFX == null) return;
        for (int i = 0; i < brakeFX.Length; i++)
        {
            var ps = brakeFX[i];
            if (!ps) continue;
            var em = ps.emission;
            em.enabled = on;

            var curve = em.rateOverTime;
            curve.constant = rate;
            em.rateOverTime = curve;

            if (on && !ps.isPlaying) ps.Play(true);
            else if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
