// ArrowPointer_Offscreen.cs — Realtime Off-screen Target Indicator Arrow
// วางสคริปต์นี้บน UI GameObject (ไอคอนลูกศร) ใน Canvas
// รองรับ Screen Space - Overlay / Camera / World

using System.Collections.Generic;
using UnityEngine;
#if TMP_PRESENT
using TMPro;
#endif

[DisallowMultipleComponent]
public class ArrowPointer_Offscreen : MonoBehaviour
{
    [Header("References")]
    public Camera targetCamera;                  // กล้องหลัก (ไม่ตั้งจะใช้ Camera.main)
    public RectTransform arrow;                  // RectTransform ของไอคอนลูกศร (ตัวนี้เองก็ได้)
    public RectTransform container;              // พาเรนต์ที่ใช้คำนวณตำแหน่ง (ไม่ตั้งจะใช้ Canvas Root)
#if TMP_PRESENT
    public TMP_Text distanceText;                // (ไม่บังคับ) ใส่แล้วจะแสดงระยะทางเป็นเมตร
#endif

    [Header("Targets")]
    [Tooltip("เลือก 'เป้าหมายที่อยู่นอกจอและใกล้สุด' ก่อน ถ้าไม่มีนอกจอจึงเลือกที่ใกล้สุด")]
    public bool pickNearest = true;
    [Tooltip("รายการเป้าหมายศัตรู (เช่น รถตำรวจ) ที่จะให้ลูกศรชี้")]
    public List<Transform> candidates = new List<Transform>();

    [Header("Behaviour")]
    [Tooltip("ซ่อนลูกศรถ้ามี 'เป้าหมายตัวใดก็ตาม' อยู่ในจอ")]
    public bool hideWhenAnyCandidateVisible = true;
    [Tooltip("ซ่อนลูกศรถ้าเป้าหมายที่เลือกอยู่ในจอ")]
    public bool hideArrowWhenOnScreen = true;
    [Tooltip("กันขอบจอ (พิกเซล)")]
    public float edgePadding = 34f;
    [Tooltip("ความลื่นของตำแหน่ง (ค่ามาก = ลื่น)")]
    public float positionLerp = 14f;
    [Tooltip("ความลื่นของการหมุน (ค่ามาก = ลื่น)")]
    public float rotationLerp = 18f;
    [Tooltip("บังคับให้อยู่ในเฟรมแม้ OnScreen (ใช้กับ UI ดีไซน์บางแบบ)")]
    public bool clampInside = false;

    // ---- runtime ----
    Canvas canvas;
    RectTransform canvasRect;
    Transform selected;
    Vector2 curPos;
    float curAng;

    void Awake()
    {
        if (!targetCamera) targetCamera = Camera.main;

        canvas = GetComponentInParent<Canvas>();
        if (!canvas) canvas = FindObjectOfType<Canvas>();
        if (!canvas) { Debug.LogError("[ArrowPointer_Offscreen] Canvas not found in scene."); enabled = false; return; }

        canvasRect = canvas.GetComponent<RectTransform>();
        if (!arrow) arrow = GetComponent<RectTransform>();
        if (!container) container = (arrow && arrow.parent is RectTransform pr) ? pr : canvasRect;
    }

    void LateUpdate()
    {
        if (!targetCamera || !arrow) return;

        // ล้างรายการที่วัตถุถูกทำลาย
        for (int i = candidates.Count - 1; i >= 0; i--)
            if (!candidates[i]) candidates.RemoveAt(i);

        // ถ้ามีตัวไหนอยู่ในจอ -> ซ่อนทั้งลูกศร
        if (hideWhenAnyCandidateVisible && AnyCandidateVisible())
        {
            if (arrow.gameObject.activeSelf) arrow.gameObject.SetActive(false);
            return;
        }

        // เลือกเป้าหมาย
        if (pickNearest) selected = PickNearestOffscreenFirst();
        if (!selected)
        {
            if (arrow.gameObject.activeSelf) arrow.gameObject.SetActive(false);
            return;
        }
        if (!arrow.gameObject.activeSelf) arrow.gameObject.SetActive(true);

        // คำนวณตำแหน่งบนจอ
        Vector3 sp = targetCamera.WorldToScreenPoint(selected.position);
        Vector3 vp = targetCamera.WorldToViewportPoint(selected.position);
        bool onScreen = (vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f);

        Rect pr = targetCamera.pixelRect;
        Vector2 prCenter = new Vector2(pr.x + pr.width * 0.5f, pr.y + pr.height * 0.5f);
        Vector2 screenPos;

        if (onScreen)
        {
            screenPos = new Vector2(sp.x, sp.y);

            if (clampInside)
            {
                float pad = edgePadding;
                screenPos.x = Mathf.Clamp(screenPos.x, pr.x + pad, pr.xMax - pad);
                screenPos.y = Mathf.Clamp(screenPos.y, pr.y + pad, pr.yMax - pad);
            }

            arrow.gameObject.SetActive(!hideArrowWhenOnScreen);
        }
        else
        {
            Vector2 dir = new Vector2(sp.x, sp.y) - prCenter;
            if (vp.z < 0f) dir = -dir; // อยู่หลังกล้อง -> กลับทิศ

            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.up;

            Vector2 ext = new Vector2((pr.width * 0.5f) - edgePadding, (pr.height * 0.5f) - edgePadding);
            float kx = Mathf.Abs(ext.x / (Mathf.Abs(dir.x) < 1e-5f ? 1e-5f : dir.x));
            float ky = Mathf.Abs(ext.y / (Mathf.Abs(dir.y) < 1e-5f ? 1e-5f : dir.y));
            float k = Mathf.Min(kx, ky);
            screenPos = prCenter + dir * k;

            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            curAng = Mathf.LerpAngle(curAng, ang, 1f - Mathf.Exp(-rotationLerp * Time.deltaTime));
            arrow.rotation = Quaternion.Euler(0f, 0f, curAng);
            arrow.gameObject.SetActive(true);
        }

        // ScreenPoint -> Local (ใน container)
        Camera camForUI =
            (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null
            : (canvas.worldCamera ? canvas.worldCamera : targetCamera);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            container, screenPos, camForUI, out Vector2 localInContainer
        );

        RectTransform parentRT = arrow.parent as RectTransform;
        Vector2 localForParent = localInContainer;
        if (parentRT && parentRT != container)
        {
            Vector3 world = container.TransformPoint(localInContainer);
            localForParent = (Vector2)parentRT.InverseTransformPoint(world);
        }

        curPos = Vector2.Lerp(curPos, localForParent, 1f - Mathf.Exp(-positionLerp * Time.deltaTime));
        arrow.anchoredPosition = curPos;

        if (onScreen && !hideArrowWhenOnScreen)
        {
            float ang = 0f;
            curAng = Mathf.LerpAngle(curAng, ang, 1f - Mathf.Exp(-rotationLerp * Time.deltaTime));
            arrow.rotation = Quaternion.Euler(0f, 0f, curAng);
        }

#if TMP_PRESENT
        if (distanceText)
        {
            float dist = Vector3.Distance(targetCamera.transform.position, selected.position);
            distanceText.text = $"{dist:0} m";
        }
#endif
    }

    bool AnyCandidateVisible()
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            var t = candidates[i];
            if (!t) continue;
            Vector3 vp = targetCamera.WorldToViewportPoint(t.position);
            bool on = (vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f);
            if (on) return true;
        }
        return false;
    }

    Transform PickNearestOffscreenFirst()
    {
        float bestOff = float.MaxValue;
        Transform bestOffT = null;
        float bestAny = float.MaxValue;
        Transform bestAnyT = null;

        foreach (var t in candidates)
        {
            if (!t) continue;
            Vector3 vp = targetCamera.WorldToViewportPoint(t.position);
            float d2 = (t.position - targetCamera.transform.position).sqrMagnitude;

            bool on = (vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f);
            if (!on && d2 < bestOff) { bestOff = d2; bestOffT = t; }
            if (d2 < bestAny) { bestAny = d2; bestAnyT = t; }
        }
        return bestOffT ? bestOffT : bestAnyT;
    }

    // ---------- Public API ----------
    public void RegisterCandidate(Transform t)
    {
        if (t && !candidates.Contains(t)) candidates.Add(t);
    }
    public void UnregisterCandidate(Transform t)
    {
        if (t) candidates.Remove(t);
    }
    public void ClearAllCandidates()
    {
        candidates.Clear();
    }
}
