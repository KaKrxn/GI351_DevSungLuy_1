using UnityEngine;

/// ลดความเร็วผู้เล่นชั่วคราวเมื่อชน Trap โดยใช้กลไกเดียวกับ SpeedBoostReceiver
/// - ลดเพดาน Top speed ตามสัดส่วน slowFactor เป็นเวลา slowDuration วินาที
/// - ตัดความเร็วปัจจุบันทันทีหนึ่งครั้งให้สอดคล้องกับเพดานใหม่
[DisallowMultipleComponent]
public class TrapCollision : MonoBehaviour
{
    [Header("Slow Settings")]
    [Tooltip("สัดส่วนลด Top speed (เช่น 0.5 = เหลือ 50%)")]
    [Range(0.05f, 1f)] public float slowFactor = 0.5f;

    [Tooltip("ระยะเวลาที่ช้าลง (วินาที)")]
    [Min(0.05f)] public float slowDuration = 3f;

    [Header("Instant Feel")]
    [Tooltip("ลดความเร็วปัจจุบันทันทีตอนชน (ครั้งเดียว)")]
    public bool cutCurrentVelocityOnHit = true;

    [Tooltip("สัดส่วนความเร็วปัจจุบันหลังโดนหัก (ถ้าเว้นว่าง จะใช้ slowFactor เดียวกัน)")]
    [Range(0.05f, 1f)] public float velocityCutFactor = 0.5f;

    const float MS_TO_KMH = 3.6f;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // ต้องมี PlayerController + SpeedBoostReceiver บนตัวผู้เล่น
        var pc = other.GetComponent<PlayerController>();
        var sbr = other.GetComponent<SpeedBoostReceiver>();

        if (!pc || !sbr)
        {
            Debug.LogWarning("[TrapCollision] ต้องมี PlayerController และ SpeedBoostReceiver บนผู้เล่นเพื่อใช้เอฟเฟกต์สโลว์");
            return;
        }

        // 1) ลดความเร็วปัจจุบันทันที (ครั้งเดียว) เพื่อให้รู้สึกชัดเจน
        if (cutCurrentVelocityOnHit)
        {
            var rb = sbr.rb ? sbr.rb : other.attachedRigidbody;
            if (rb != null)
            {
                float cut = Mathf.Clamp01(velocityCutFactor > 0f ? velocityCutFactor : slowFactor);
#if UNITY_6000_0_OR_NEWER
                Vector3 v = rb.linearVelocity;
#else
                Vector3 v   = rb.velocity;
#endif
                Vector3 flat = new Vector3(v.x, 0f, v.z);
                Vector3 newFlat = flat * cut;
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = new Vector3(newFlat.x, v.y, newFlat.z);
#else
                rb.velocity = new Vector3(newFlat.x, v.y, newFlat.z);
#endif
            }
        }

        // 2) ใช้กลไกของ SpeedBoostReceiver: ตั้ง "Top speed ชั่วคราว" ให้ต่ำลง
        //    - อ่านเพดานปัจจุบัน (m/s) จาก PlayerController
        //    - คำนวณเป้าหมาย (km/h) แล้วสั่ง ApplyTimedTopKmh(..., onlyIfHigher:false)
        float curTopMs = Mathf.Max(0.01f, pc.maxForwardSpeed);   // เพดาน ณ ตอนโดนกับดัก
        float targetTopKmh = curTopMs * slowFactor * MS_TO_KMH;  // เป้าหมายลดสัดส่วนเดิม

        // onlyIfHigher=false = ยอมให้ "ลด" เพดานได้, accelMultiplier=1f (ไม่ไปยุ่งอัตราเร่ง)
        sbr.ApplyTimedTopKmh(
            targetTopKmh,
            slowDuration,
            onlyIfHigher: false,
            accelMultiplier: 1f,
            instantExtraKmh: 0f
        );
    }
}
