//using UnityEngine;

//[RequireComponent(typeof(Collider))]
//public class PoliceBarbedTrap : MonoBehaviour
//{
//    [Tooltip("อายุของกับดัก ก่อนลบตัวเอง")]
//    public float lifetime = 10f;

//    [Header("ผลกระทบต่อรถ")]
//    [Tooltip("ลดความเร็วสัดส่วน (0.4 = ลด 40%)")]
//    [Range(0f, 0.95f)] public float slowPercent = 0.4f;
//    [Tooltip("ค้างที่ความเร็วต่ำสุด")]
//    public float slowHoldSeconds = 0.6f;
//    [Tooltip("เวลาคืนความเร็วแบบค่อยๆ กลับ")]
//    public float slowRecoverSeconds = 1.8f;

//    [Tooltip("แท็กของผู้เล่น/รถ")]
//    public string playerTag = "Player";
//    public bool enableDebug = false;

//    float timer;

//    void Reset()
//    {
//        var col = GetComponent<Collider>();
//        col.isTrigger = true;
//    }

//    void Update()
//    {
//        timer += Time.deltaTime;
//        if (timer >= lifetime)
//        {
//            if (enableDebug) Debug.Log("[BarbedTrap] Auto-destroy");
//            Destroy(gameObject);
//        }
//    }

//    void OnTriggerEnter(Collider other)
//    {
//        if (!other.CompareTag(playerTag)) return;

//        var car = other.GetComponentInParent<ArcadeCarController>();
//        if (car)
//        {
//            if (enableDebug) Debug.Log("[BarbedTrap] Hit player → apply speed debuff");
//            car.ApplySpeedDebuff(slowPercent, slowHoldSeconds, slowRecoverSeconds);
//        }

//        // ถ้าอยากให้กับดักหายทันทีเมื่อโดน:
//        // Destroy(gameObject);
//    }
//}
