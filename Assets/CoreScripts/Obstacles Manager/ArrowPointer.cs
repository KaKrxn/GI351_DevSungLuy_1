// ArrowPointer.cs — Off-screen arrow pointer (fixed for camera.pixelRect & container)
// Unity 6000.0.56f1 compatible

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class ArrowPointer : MonoBehaviour
{
    [Header("References")]
    public Camera targetCamera;                  
    public RectTransform arrow;                  
    public RectTransform container;             
    public TMP_Text distanceText;               

    [Header("Target")]
    public Transform target;                     
    public bool pickNearest = false;             
    public List<Transform> candidates = new();   

    [Header("Behaviour")]
    public float edgePadding = 34f;              
    public bool showWhenOnScreen = true;         
    public bool hideArrowWhenOnScreen = false;   
    public float positionLerp = 14f;             
    public float rotationLerp = 18f;             
    public bool clampInside = false;             

    // ---- runtime ----
    Canvas canvas;
    RectTransform canvasRect;
    Vector2 curPos;
    float curAng;

    void Awake()
    {
        if (!targetCamera) targetCamera = Camera.main;

        canvas = GetComponentInParent<Canvas>();
        if (!canvas) canvas = FindObjectOfType<Canvas>();
        if (!canvas) { Debug.LogError("[ArrowPointer] Canvas not found in scene."); enabled = false; return; }

        canvasRect = canvas.GetComponent<RectTransform>();
        if (!arrow) arrow = GetComponent<RectTransform>();
        if (!container) container = (arrow && arrow.parent is RectTransform pr) ? pr : canvasRect;
    }

    void LateUpdate()
    {
        if (!targetCamera || !arrow) return;

        
        Transform t = SelectTarget();
        if (!t)
        {
            if (arrow.gameObject.activeSelf) arrow.gameObject.SetActive(false);
            return;
        }
        if (!arrow.gameObject.activeSelf) arrow.gameObject.SetActive(true);

        // --- world -> screen/viewport (ของ "กล้องนี้") ---
        Vector3 sp = targetCamera.WorldToScreenPoint(t.position);
        Vector3 vp = targetCamera.WorldToViewportPoint(t.position);
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
            if (vp.z < 0f) dir = -dir;                    
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.up;

            Vector2 ext = new Vector2((pr.width * 0.5f) - edgePadding,
                                      (pr.height * 0.5f) - edgePadding);

            float kx = Mathf.Abs(ext.x / (Mathf.Abs(dir.x) < 1e-5f ? 1e-5f : dir.x));
            float ky = Mathf.Abs(ext.y / (Mathf.Abs(dir.y) < 1e-5f ? 1e-5f : dir.y));
            float k = Mathf.Min(kx, ky);
            screenPos = prCenter + dir * k;

            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            curAng = Mathf.LerpAngle(curAng, ang, 1f - Mathf.Exp(-rotationLerp * Time.deltaTime));
            arrow.rotation = Quaternion.Euler(0f, 0f, curAng);
            arrow.gameObject.SetActive(true);
        }

        // --- ScreenPoint → LocalPoint (อิง "container") ---
        Camera camForUI =
            (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null
            : (canvas.worldCamera ? canvas.worldCamera : targetCamera);

       
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            container,
            screenPos,
            camForUI,
            out Vector2 localInContainer
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

        
        if (distanceText)
        {
            float dist = Vector3.Distance(targetCamera.transform.position, t.position);
            distanceText.text = $"{dist:0} m";
        }
    }

    // ---------- Public API ----------
    public void SetTarget(Transform t)
    {
        target = t;
        pickNearest = false;     
        if (t) arrow.gameObject.SetActive(true);
    }
    public void ClearTarget()
    {
        target = null;
        if (!pickNearest) arrow.gameObject.SetActive(false);
    }
    public void RegisterCandidate(Transform t)
    {
        if (t && !candidates.Contains(t)) candidates.Add(t);
        pickNearest = true;
    }
    public void UnregisterCandidate(Transform t)
    {
        if (t) candidates.Remove(t);
    }

    // --------- Helpers ---------
    Transform SelectTarget()
    {
        if (!pickNearest) return target;

        Transform bestT = null;
        float best = float.MaxValue;

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            var c = candidates[i];
            if (!c) { candidates.RemoveAt(i); continue; }
            float d = (c.position - targetCamera.transform.position).sqrMagnitude;
            if (d < best) { best = d; bestT = c; }
        }
        return bestT ? bestT : target;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        positionLerp = Mathf.Max(0f, positionLerp);
        rotationLerp = Mathf.Max(0f, rotationLerp);
        edgePadding = Mathf.Max(0f, edgePadding);
    }
#endif
}
