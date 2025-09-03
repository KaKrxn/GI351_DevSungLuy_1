// MinimapCameraFollow.cs
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MinimapCameraFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform target;                 // ลาก Player มาวาง
    public float height = 60f;               // ความสูงจากพื้น
    public float orthoSize = 25f;            // ซูมของแมพ (Orthographic Size)
    public bool rotateWithTarget = true;     // true = แมพหมุนตามผู้เล่น (ลูกศรชี้ขึ้นตลอด)

    [Header("Freeze Rotation")]
    public bool freezeRotation = false;      // เปิด/ปิด การล็อกหมุนทั้งหมด (ใช้ lockedEuler)
    public bool lockPitch = true;            // ล็อกแกน X (ก้ม/เงย) - ปกติแมพบนฟ้าคือ 90°
    public bool lockYaw = false;             // ล็อกแกน Y (หันซ้าย/ขวา) - เปิด true = คงทิศเหนือบน
    public bool lockRoll = true;             // ล็อกแกน Z (เอียงซ้าย/ขวา)
    public Vector3 lockedEuler = new Vector3(90f, 0f, 0f); // มุมที่ต้องการล็อกไว้เมื่อ freeze/lock แกน

    private Camera cam;
    private Vector3 initialEuler;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;

        initialEuler = transform.eulerAngles; // เก็บมุมตั้งต้นไว้ใช้เป็นค่าอ้างอิง
        // ถ้าอยากให้ค่า lockedEuler เริ่มตามค่าปัจจุบัน ให้ใช้บรรทัดด้านล่างแทน:
        // lockedEuler = initialEuler;
    }

    void LateUpdate()
    {
        if (!target) return;

        // ตำแหน่งกล้องเหนือเป้าหมาย
        transform.position = new Vector3(target.position.x, height, target.position.z);

        // ถ้า freeze ทั้งก้อน ให้ใช้ lockedEuler ตลอด
        if (freezeRotation)
        {
            transform.rotation = Quaternion.Euler(lockedEuler);
            return;
        }

        // มุมพื้นฐานที่อยากได้ตามโหมดหมุนตามผู้เล่นหรือไม่
        Vector3 desired = rotateWithTarget
            ? new Vector3(90f, target.eulerAngles.y, 0f)
            : new Vector3(90f, 0f, 0f);

        // นำการล็อกแกนมาครอบอีกชั้น (เลือกใช้ค่า lockedEuler เฉพาะแกนที่ล็อก)
        float x = lockPitch ? lockedEuler.x : desired.x;
        float y = lockYaw ? lockedEuler.y : desired.y;
        float z = lockRoll ? lockedEuler.z : desired.z;

        transform.rotation = Quaternion.Euler(x, y, z);
    }

    /// <summary>
    /// Set orthographic zoom safely.
    /// </summary>
    public void SetZoom(float size)
    {
        cam.orthographicSize = Mathf.Max(1f, size);
    }

    /// <summary>
    /// จับมุมปัจจุบันของกล้องเก็บไว้ใน lockedEuler ทันที
    /// เรียกใช้จากปุ่มใน Inspector หรือจากสคริปต์อื่นได้
    /// </summary>
    public void FreezeNow()
    {
        lockedEuler = transform.eulerAngles;
    }
}
