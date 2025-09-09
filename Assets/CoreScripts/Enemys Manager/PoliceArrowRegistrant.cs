// PoliceArrowRegistrant.cs — ติดกับ Prefab รถตำรวจ
// ลงทะเบียนตัวเองเข้ากับ ArrowPointer_Offscreen ตอน spawn และถอดออกเมื่อถูกปิด/ทำลาย
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PoliceArrowRegistrant : MonoBehaviour
{
    [Header("Arrow")]
    public ArrowPointer_Offscreen arrow;   // เว้นว่างได้ เดี๋ยวหาเอง
    [Tooltip("จุดที่ให้ลูกศรตาม (ว่าง=transform ของเกมออบเจ็กต์นี้)")]
    public Transform followTarget;

    void Reset()
    {
        followTarget = transform;
    }

    IEnumerator Start()
    {
        // รอ 1 เฟรมให้ Canvas/Arrow พร้อมก่อน
        yield return null;
        if (!arrow) arrow = FindObjectOfType<ArrowPointer_Offscreen>();
        if (!followTarget) followTarget = transform;
        if (arrow && followTarget) arrow.RegisterCandidate(followTarget);
    }

    void OnDisable()
    {
        if (arrow && followTarget) arrow.UnregisterCandidate(followTarget);
    }

    void OnDestroy()
    {
        if (arrow && followTarget) arrow.UnregisterCandidate(followTarget);
    }
}
