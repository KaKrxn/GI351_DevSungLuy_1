// CinematicCarCamera.cs — smooth chase camera for arcade racers
// - Soft follow + rotation damping (exp smoothing)
// - Speed-based FOV zoom
// - Velocity look-ahead (มองนำหน้าตามความเร็ว)
// - Banking/roll ตามแรงสไลด์ด้านข้าง (ล้อดริฟต์เอียงกล้อง)
// - Collision avoidance (sphere cast)
// Unity 6000.x compatible

using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CinematicCarCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody targetRb;                   
    public Vector3 localOffset = new Vector3(0f, 3.2f, -6.5f); 

    [Header("Damping (higher = เกาะเร็วขึ้น)")]
    public float positionDamping = 10f;         
    public float rotationDamping = 12f;

    [Header("Aim / Look-Ahead")]
    public bool faceVelocity = true;           
    public float minSpeedForVelocityAim = 2.0f; 
    public float lookAheadBySpeed = 0.20f;      
    public float lookAheadMax = 6f;             

    [Header("FOV by Speed")]
    public float baseFOV = 60f;
    public float maxFOV = 85f;
    public float fovAtSpeed = 25f;              

    [Header("Banking (Camera Roll)")]
    public float rollMaxDeg = 10f;              
    public float rollResponsiveness = 4f;       
    public bool keepHorizonLevel = false;     

    [Header("Collision Avoidance")]
    public bool avoidCollision = true;
    public float castRadius = 0.35f;
    public float collisionBuffer = 0.25f;       
    public LayerMask obstacleMask = ~0;         

    [Header("Misc")]
    public bool lockCursor = true;

    Camera cam;
    Vector3 velSmoothed;       
    float rollCur;           

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (!target && !targetRb)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) { target = p.transform; targetRb = p.GetComponent<Rigidbody>(); }
        }

        if (!cam) cam = Camera.main;
        if (lockCursor) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        cam.fieldOfView = baseFOV;
    }

    void LateUpdate()
    {
        if (!target && !targetRb) return;
        var t = target ? target : targetRb.transform;

       
        Vector3 worldVel = GetTargetVelocity();
        float speed = worldVel.magnitude;

        
        Vector3 aimDir;
        if (faceVelocity && speed > minSpeedForVelocityAim)
            aimDir = worldVel.normalized;
        else
            aimDir = t.forward;

       
        float lookAhead = Mathf.Min(lookAheadMax, speed * lookAheadBySpeed);
        Vector3 lookPoint = t.position + aimDir * lookAhead + Vector3.up * 0.5f;

        Vector3 desiredPos =
            t.TransformPoint(localOffset.x, localOffset.y, localOffset.z); 
        
        if (avoidCollision)
        {
            Vector3 from = t.position + Vector3.up * 0.5f;
            Vector3 dir = (desiredPos - from);
            float dist = dir.magnitude;
            if (dist > 0.001f)
            {
                dir /= dist;
                if (Physics.SphereCast(from, castRadius, dir, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    desiredPos = hit.point + hit.normal * collisionBuffer;
                }
            }
        }

        
        float kp = 1f - Mathf.Exp(-positionDamping * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, kp);

        
        Quaternion lookRot = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);

        
        float rollTarget = 0f;
        if (!keepHorizonLevel)
        {
            
            float lateral = Vector3.Dot(worldVel, t.right);
            float lateral01 = Mathf.Clamp(lateral / 15f, -1f, 1f); // ปรับสเกล 15 m/s ตามเกมคุณ
            rollTarget = -lateral01 * rollMaxDeg;
        }
        
        float kr = 1f - Mathf.Exp(-rollResponsiveness * Time.deltaTime);
        rollCur = Mathf.Lerp(rollCur, rollTarget, kr);

        
        Quaternion rollRot = Quaternion.AngleAxis(rollCur, Vector3.forward);
        Quaternion desiredRot = lookRot * rollRot;

        float kRot = 1f - Mathf.Exp(-rotationDamping * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, kRot);

        
        float f = Mathf.InverseLerp(0f, Mathf.Max(0.01f, fovAtSpeed), speed);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Mathf.Lerp(baseFOV, maxFOV, f), 1f - Mathf.Exp(-6f * Time.deltaTime));
    }

    Vector3 GetTargetVelocity()
    {
        if (targetRb) return targetRb.linearVelocity;
        
        Vector3 pos = (target ? target.position : transform.position);
        Vector3 v = (pos - velSmoothed) / Mathf.Max(Time.deltaTime, 1e-5f);
        velSmoothed = pos;
        return v;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!target && !targetRb) return;
        var t = target ? target : targetRb.transform;
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.6f);
        Gizmos.DrawWireSphere(t.position + Vector3.up * 0.5f, 0.25f);
    }
   
#endif
}




//// BirdEyeCameraFollow.cs — toggle V, Top-down doesn't follow player yaw
//using UnityEngine;

//public class BirdEyeCameraFollow : MonoBehaviour
//{
//    public Transform target;

//    [Header("Normal View")]
//    public float normalDistance = 12f;
//    public float normalPitch = 65f;
//    public float normalFOV = 60f;

//    [Header("Top-Down View")]
//    public bool topDownOrthographic = false;
//    public float topDownDistance = 20f;      // ใช้เมื่อ Perspective
//    public float topDownPitch = 88f;         // 85..90 = มองลง
//    public float topDownFOV = 50f;           // ใช้เมื่อ Perspective
//    public float topDownOrthoSize = 18f;     // ใช้เมื่อ Orthographic

//    [Tooltip("ให้ Top-down หมุนตามหัวรถ (ปิดไว้เป็นค่าเริ่ม)")]
//    public bool followTargetYawInTopDown = false;

//    [Tooltip("ล็อกมุมคงที่ใน Top-down (เช่น North-up)")]
//    public bool lockTopDownYaw = true;
//    public float topDownFixedYaw = 0f;       // 0 = หันทิศเหนือโลก

//    [Header("Rig Limits / Zoom")]
//    public float minDistance = 5f;
//    public float maxDistance = 25f;
//    public float zoomSpeed = 5f;

//    [Header("Mouse Control (Normal)")]
//    public float mouseSensitivity = 3f;
//    public bool invertY = false;

//    [Header("Smoothing")]
//    public float followDamping = 10f;
//    public float lookDamping = 15f;
//    public float switchLerp = 10f;

//    [Header("Auto Align (Normal)")]
//    public bool autoAlign = true;
//    public float autoAlignSpeed = 2f;
//    public float idleTimeBeforeAlign = 1.5f;

//    [Header("Toggle")]
//    public KeyCode toggleKey = KeyCode.V;
//    public bool startTopDown = false;

//    // --- runtime ---
//    Camera cam;
//    bool isTopDown;
//    float yaw;
//    float currentPitch;
//    float lastMouseInputTime;
//    float currentDistance;

//    void Start()
//    {
//        if (!target)
//        {
//            var p = GameObject.FindGameObjectWithTag("Player");
//            if (p) target = p.transform;
//        }

//        cam = GetComponent<Camera>();
//        if (!cam) cam = Camera.main;

//        isTopDown = startTopDown;

//        if (isTopDown)
//        {
//            currentPitch = topDownPitch;
//            currentDistance = topDownDistance;
//            ApplyProjTopDownImmediate();

//            // เริ่มโหมด Top-down: ถ้าล็อกมุม ให้ใช้ fixed yaw; ไม่งั้นใช้ yaw ปัจจุบัน
//            if (lockTopDownYaw)
//                yaw = topDownFixedYaw;
//            else if (followTargetYawInTopDown && target)
//                yaw = target.eulerAngles.y;
//            else
//                yaw = transform.eulerAngles.y;
//        }
//        else
//        {
//            currentPitch = normalPitch;
//            currentDistance = normalDistance;
//            ApplyProjNormalImmediate();
//            yaw = transform.eulerAngles.y;
//        }

//        Cursor.lockState = CursorLockMode.Locked;
//        Cursor.visible = false;
//    }

//    void Update()
//    {
//        if (Input.GetKeyDown(toggleKey))
//            isTopDown = !isTopDown;

//        if (!isTopDown)
//        {
//            // NORMAL VIEW — ควบคุมเมาส์
//            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
//            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * (invertY ? 1 : -1);

//            if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
//            {
//                yaw += mouseX;
//                normalPitch = Mathf.Clamp(normalPitch + mouseY, 20f, 80f);
//                lastMouseInputTime = Time.time;
//            }
//            else if (autoAlign && target && Time.time - lastMouseInputTime > idleTimeBeforeAlign)
//            {
//                float targetYaw = target.eulerAngles.y;
//                yaw = Mathf.LerpAngle(yaw, targetYaw, Time.deltaTime * autoAlignSpeed);
//            }

//            float scroll = Input.GetAxis("Mouse ScrollWheel");
//            if (Mathf.Abs(scroll) > 0.01f)
//                normalDistance = Mathf.Clamp(normalDistance - scroll * zoomSpeed, minDistance, maxDistance);
//        }
//        else
//        {
//            // TOP-DOWN — ไม่หมุนตามผู้เล่น โดยค่าเริ่ม (followTargetYawInTopDown=false)
//            if (followTargetYawInTopDown && target)
//            {
//                // ถ้าอยากตามหัวรถ ให้ติ๊ก true ใน Inspector
//                yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, Time.deltaTime * (autoAlign ? autoAlignSpeed * 2f : 1f));
//            }
//            else if (lockTopDownYaw)
//            {
//                // ล็อกมุมคงที่ (North-up)
//                yaw = Mathf.LerpAngle(yaw, topDownFixedYaw, Time.deltaTime * (autoAlign ? autoAlignSpeed * 2f : 1f));
//            }
//            // else: คง yaw เดิม (ไม่เปลี่ยน)
//        }

//        // Blend ค่าระหว่างโหมด
//        float t = 1f - Mathf.Exp(-switchLerp * Time.deltaTime);
//        if (isTopDown)
//        {
//            currentPitch = Mathf.Lerp(currentPitch, topDownPitch, t);
//            currentDistance = Mathf.Lerp(currentDistance, topDownOrthographic ? currentDistance : topDownDistance, t);
//            ApplyProjTopDown(t);
//        }
//        else
//        {
//            currentPitch = Mathf.Lerp(currentPitch, normalPitch, t);
//            currentDistance = Mathf.Lerp(currentDistance, normalDistance, t);
//            ApplyProjNormal(t);
//        }
//    }

//    void LateUpdate()
//    {
//        if (!target) return;

//        Quaternion rot = Quaternion.Euler(currentPitch, yaw, 0f);
//        Vector3 offset = rot * new Vector3(0f, 0f, -currentDistance);
//        Vector3 desiredPos = target.position + offset;

//        float tPos = 1f - Mathf.Exp(-followDamping * Time.deltaTime);
//        transform.position = Vector3.Lerp(transform.position, desiredPos, tPos);

//        Quaternion look = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
//        float tRot = 1f - Mathf.Exp(-lookDamping * Time.deltaTime);
//        transform.rotation = Quaternion.Slerp(transform.rotation, look, tRot);
//    }

//    // ----- Projection helpers -----
//    void ApplyProjTopDownImmediate()
//    {
//        if (!cam) return;
//        if (topDownOrthographic)
//        {
//            cam.orthographic = true;
//            cam.orthographicSize = topDownOrthoSize;
//        }
//        else
//        {
//            cam.orthographic = false;
//            cam.fieldOfView = topDownFOV;
//        }
//    }

//    void ApplyProjNormalImmediate()
//    {
//        if (!cam) return;
//        cam.orthographic = false;
//        cam.fieldOfView = normalFOV;
//    }

//    void ApplyProjTopDown(float t)
//    {
//        if (!cam) return;
//        if (topDownOrthographic)
//        {
//            if (!cam.orthographic) cam.orthographic = true;
//            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, topDownOrthoSize, t);
//        }
//        else
//        {
//            if (cam.orthographic) cam.orthographic = false;
//            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, topDownFOV, t);
//        }
//    }

//    void ApplyProjNormal(float t)
//    {
//        if (!cam) return;
//        if (cam.orthographic) cam.orthographic = false;
//        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, normalFOV, t);
//    }
//}
