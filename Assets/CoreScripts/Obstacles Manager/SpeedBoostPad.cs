// SpeedBoostPad.cs — ติดที่แผ่นในฉาก (มี Collider เป็น Trigger)
// เมื่อ Player เหยียบ → เพิ่มความเร็วชั่วคราว แล้วคืนค่าเดิมอัตโนมัติ
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpeedBoostPad : MonoBehaviour
{
    [Header("Boost Settings")]
    [Tooltip("คูณ Top speed ชั่วคราว")]
    public float speedMultiplier = 1.5f;
    [Tooltip("คูณอัตราเร่งชั่วคราว")]
    public float accelMultiplier = 1.5f;
    [Tooltip("ระยะเวลาบูสต์ (วินาที)")]
    public float duration = 2.0f;

    [Tooltip("เติมความเร็วทันที (m/s) ตอนเหยียบ เพื่อรู้สึกพุ่ง)")]
    public float instantExtraVelocity = 4.0f;

    [Header("Pad Options")]
    public string playerTag = "Player";
    public bool oneShot = false;     // ใช้ครั้งเดียวแล้วปิด
    public float cooldown = 0.5f;    // กันกดรัว
    public ParticleSystem onBoostFX; // เอฟเฟกต์ตอนเหยียบ (ทางเลือก)
    public AudioSource onBoostSFX;   // เสียงตอนเหยียบ (ทางเลือก)

    bool coolingDown;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (coolingDown) return;
        if (!other.CompareTag(playerTag)) return;

        var receiver = other.GetComponentInParent<SpeedBoostReceiver>();
        if (!receiver) receiver = other.GetComponent<SpeedBoostReceiver>();
        if (!receiver) return; // ผู้เล่นไม่มีตัวรับบูสต์

        // สั่งบูสต์
        receiver.ApplyTimedBoost(speedMultiplier, accelMultiplier, duration, instantExtraVelocity);

        // เอฟเฟกต์
        if (onBoostFX) onBoostFX.Play();
        if (onBoostSFX) onBoostSFX.Play();

        if (oneShot)
        {
            gameObject.SetActive(false);
            return;
        }

        if (cooldown > 0f) StartCoroutine(Cooldown());
    }

    System.Collections.IEnumerator Cooldown()
    {
        coolingDown = true;
        yield return new WaitForSeconds(cooldown);
        coolingDown = false;
    }
}
