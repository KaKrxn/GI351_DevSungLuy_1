using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeCarController : MonoBehaviour
{
    // ===== Forward Accel =====
    [Header("🚀 Acceleration (Forward)")]
    public float maxAccelerationForce = 12000f;
    [Range(0.01f, 1f)] public float throttleResponse = 0.7f;

    // ===== Reverse =====
    [Header("🔁 Reverse")]
    public float reverseAccelerationForce = 6500f;
    public float reverseTopSpeedKmh = 60f;
    [Tooltip("เข้าเกียร์ถอยเมื่อความเร็วไปข้างหน้า < ค่านี้ (m/s) + กด S ค้าง")]
    public float autoReverseSpeedMps = 1.5f;
    [Tooltip("เวลาต้องกด S ค้าง (วินาที) เพื่อเข้าถอยเมื่ออยู่ใต้เกณฑ์ความเร็ว")]
    public float reverseEngageHold = 0.2f;
    public bool invertSteerWhenReversing = true;

    // ===== Braking =====
    [Header("🛑 Braking")]
    public float brakeForce = 12000f;

    // ===== Steering =====
    [Header("🕹️ Steering")]
    public float maxSteerAngle = 35f, minSteerAngle = 9f;
    public float steerTorque = 4600f;
    public float noSteerUnderSpeed = 0.2f;
    public AnimationCurve speedSteerDampen = AnimationCurve.EaseInOut(0, 1, 1, 0.5f);

    // ===== Grip & Stability (แก้ไถลข้าง) =====
    [Header("🧲 Grip & Stability")]
    [Tooltip("สปริงต้านความเร็วด้านข้าง (สูง=เกาะขึ้น)")]
    public float lateralGrip = 0.14f;
    [Tooltip("ระดับความเร็วข้างที่ถือว่าเริ่มลื่นหนัก (m/s)")]
    public float sideSlipSaturation = 8f;
    [Tooltip("ดึงหัวรถให้ตรงทิศวิ่ง (0..1)")]
    [Range(0f, 1f)] public float stabilityAlign = 0.2f;
    [Tooltip("หน่วง yaw เพิ่ม (กันท้ายกวาด)")]
    [Range(0f, 5f)] public float yawDamping = 2.0f;

    // ===== Aero / TopSpeed =====
    [Header("🌬️ Resistance & Top Speed")]
    [Tooltip("drag เกมเพลย์: ใช้กับ v^2 (ไม่คูณมวล) — เริ่มจูน 3–5")]
    public float dragCoefficient = 3.5f;
    public bool limitTopSpeed = true;
    public float topSpeedKmh = 300f;

    // ===== Physics =====
    [Header("⚙️ Physics")]
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.45f, 0f);

    // ===== Rush Tuning =====
    [Header("💥 Rush Tuning")]
    [Range(1f, 2f)] public float rushIntensity = 1.3f;
    [Range(0.5f, 1.2f)] public float limiterCurveExp = 0.7f;
    [Range(0.5f, 1f)] public float rushLowSpeedDragMul = 0.8f;
    public float rushDragBlendStartKmh = 70f;
    public float rushDragBlendEndKmh = 160f;

    // ===== Launch Assist =====
    [Header("🏁 Launch Assist")]
    public float launchBoostMultiplier = 2.0f;
    public float launchCutoffKmh = 50f;
    public float launchEndKmh = 80f;

    // ===== Rush Burst (Nitro) =====
    [Header("⚡ Rush Burst (Nitro)")]
    public KeyCode burstKey = KeyCode.LeftShift;
    public float burstAccelMul = 2.0f;
    public float burstTopSpeedKmh = 360f;
    public float burstCooldown = 2.0f;
    public float burstDuration = 1.2f;

    // ===== Perceived Speed =====
    [Header("🎥 Dynamic FOV")]
    public bool dynamicFOV = true;
    public Camera targetCamera;
    public float baseFOV = 60f;
    public float maxFOV = 85f;
    public float fovAtKmh = 220f;

    // ===== Debug =====
    [Header("🔧 Debug")]
    public bool debugLog = false;
    [Tooltip("Log speed ทุก ๆ ระยะเวลานี้ (วินาที). 0 = ปิด")]
    public float speedLogInterval = 0f;

    // ===== Internals =====
    Rigidbody rb;
    float currentThrottle, targetThrottle;
    float burstTimer, burstCdTimer;
    bool reversing;
    float _reverseHoldTimer, _spdTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass += centerOfMassOffset;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (!targetCamera && Camera.main) targetCamera = Camera.main;
        if (targetCamera) baseFOV = targetCamera.fieldOfView;
    }

    void Update()
    {
        // ===== Input =====
        bool accelHeld = Input.GetKey(KeyCode.W);
        bool brakeHeld = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.Space);

        targetThrottle = brakeHeld ? 0f : (accelHeld ? 1f : 0f);
        currentThrottle = Mathf.MoveTowards(currentThrottle, targetThrottle, throttleResponse * Time.deltaTime);

        // ===== Reverse Decision =====
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward); // + ไปหน้า, - ถอย

        if (brakeHeld)
        {
            if (!reversing)
            {
                if (forwardSpeed > autoReverseSpeedMps)
                {
                    _reverseHoldTimer = 0f; // ยังเร็ว → เบรกก่อน
                }
                else
                {
                    _reverseHoldTimer += Time.deltaTime;
                    if (_reverseHoldTimer >= reverseEngageHold)
                    {
                        reversing = true;
                        ZeroForwardComponent(); // ตัดความเร็วแกนหน้าให้ 0 เพื่อพร้อมถอย
                        if (debugLog) Debug.Log("🔁 Enter REVERSE");
                    }
                }
            }
        }
        else _reverseHoldTimer = 0f;

        if (accelHeld && reversing)
        {
            reversing = false;
            if (debugLog) Debug.Log("▶ Exit REVERSE");
        }

        // ===== Nitro =====
        if (burstCdTimer > 0f) burstCdTimer -= Time.deltaTime;
        if (burstTimer > 0f) burstTimer -= Time.deltaTime;
        if (Input.GetKeyDown(burstKey) && burstCdTimer <= 0f && !reversing)
        {
            burstTimer = burstDuration;
            burstCdTimer = burstCooldown + burstDuration;
            if (debugLog) Debug.Log("⚡ Rush Burst!");
        }

        // ===== Dynamic FOV =====
        if (dynamicFOV && targetCamera)
        {
            float kmh = GetSpeedKmh();
            float s = Mathf.Clamp01(kmh / Mathf.Max(1f, fovAtKmh));
            float burstBonus = (burstTimer > 0f) ? 0.15f : 0f;
            float t = Mathf.Clamp01(s + burstBonus);
            float targetFov = Mathf.Lerp(baseFOV, maxFOV, t);
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, Time.deltaTime * 6f);
        }

        // ===== Debug speed console =====
        if (speedLogInterval > 0f)
        {
            _spdTimer += Time.deltaTime;
            if (_spdTimer >= speedLogInterval)
            {
                _spdTimer = 0f;
                Debug.Log($"[SPD] {GetSpeedKmh():0} km/h (forward {Vector3.Dot(rb.linearVelocity, transform.forward) * 3.6f:0} km/h)");
            }
        }
    }

    void FixedUpdate()
    {
        if (limitTopSpeed) ApplyTopSpeedLimit();
        ApplyAirDrag();

        // เบรกเฉพาะ “แกนหน้า” เมื่อยังไม่เข้า reverse และยังวิ่งไปข้างหน้า
        bool brakeHeld = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.Space);
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        if (brakeHeld && !reversing && forwardSpeed > 0.05f)
            ApplyBrakeForwardOnly();

        // เร่ง (ขึ้นกับสถานะ reverse)
        ApplyAcceleration();

        // เลี้ยว
        ApplySteering();

        // 👉 แก้ไถลข้าง: ยางเกาะ + ดึงหัวรถ + หน่วง yaw
        ApplyLateralGripAndStability(Time.fixedDeltaTime);

        if (debugLog && rb.linearVelocity.sqrMagnitude < 0.01f)
            Debug.Log("⏸ Idle (almost stopped)");
    }

    // ===== Driving Forces =====
    void ApplyAcceleration()
    {
        if (!reversing)
        {
            // เดินหน้า
            float activeTopKmh = GetActiveTopSpeedKmh();
            float topMps = KmhToMps(activeTopKmh);

            float speedLimiter = 1f;
            if (limitTopSpeed && topMps > 1f)
            {
                float speed01 = Mathf.InverseLerp(0f, topMps, GetSpeedMpsAbsForward());
                speedLimiter = Mathf.Pow(1f - speed01, limiterCurveExp);
            }

            float kmh = Mathf.Abs(GetSpeedKmh());
            float launchMul = 1f;
            if (kmh <= launchCutoffKmh) launchMul = launchBoostMultiplier;
            else if (kmh < launchEndKmh)
            {
                float t = Mathf.InverseLerp(launchEndKmh, launchCutoffKmh, kmh);
                launchMul = Mathf.Lerp(1f, launchBoostMultiplier, t);
            }

            float nitroMul = (burstTimer > 0f) ? burstAccelMul : 1f;
            float force = maxAccelerationForce * currentThrottle * speedLimiter * launchMul * rushIntensity * nitroMul;

            rb.AddForce(transform.forward * force, ForceMode.Force);
            if (debugLog && currentThrottle > 0f) Debug.Log($"→ FW Accel: {force:0}");
        }
        else
        {
            // ถอยหลัง (ไม่มี Launch/Nitro)
            float reverseLimiter = 1f;
            if (limitTopSpeed && reverseTopSpeedKmh > 1f)
            {
                float sp01 = Mathf.InverseLerp(0f, KmhToMps(reverseTopSpeedKmh), Mathf.Abs(GetSpeedMps()));
                reverseLimiter = 1f - sp01;
            }

            float force = reverseAccelerationForce * currentThrottle * reverseLimiter;
            rb.AddForce(-transform.forward * force, ForceMode.Force);
            if (debugLog && currentThrottle > 0f) Debug.Log($"→ REV Accel: {force:0}");
        }
    }

    void ApplyBrakeForwardOnly()
    {
        Vector3 vFwd = Vector3.Project(rb.linearVelocity, transform.forward);
        if (vFwd.sqrMagnitude < 0.0001f) return;
        rb.AddForce(-vFwd.normalized * brakeForce, ForceMode.Force);
        if (debugLog) Debug.Log("← Brake (forward component)");
    }

    void ApplySteering()
    {
        float steerInput = Mathf.Clamp(Input.GetAxisRaw("Horizontal"), -1f, 1f);
        if (rb.linearVelocity.magnitude < noSteerUnderSpeed || Mathf.Abs(steerInput) < 0.001f) return;
        if (reversing && invertSteerWhenReversing) steerInput = -steerInput;

        float speedRatio = Mathf.InverseLerp(0f, KmhToMps(topSpeedKmh), GetSpeedMpsAbsForward());
        float steerCurve = Mathf.Lerp(1f, speedSteerDampen.Evaluate(1f), speedRatio);
        float steerAngleNow = Mathf.Lerp(maxSteerAngle, minSteerAngle, speedRatio) * steerCurve;

        float torque = steerTorque * Mathf.Deg2Rad * steerAngleNow * steerInput;
        rb.AddTorque(Vector3.up * torque, ForceMode.Force);

        if (debugLog)
        {
            if (steerInput > 0f) Debug.Log(reversing ? "↩️ Steer RIGHT (rev)" : "↪️ Steer RIGHT");
            else if (steerInput < 0f) Debug.Log(reversing ? "↪️ Steer LEFT (rev)" : "↩️ Steer LEFT");
        }
    }

    // ===== Grip & Stability =====
    void ApplyLateralGripAndStability(float dt)
    {
        Vector3 v = rb.linearVelocity;
        if (v.sqrMagnitude < 0.0001f) return;

        // แยกความเร็วเป็นหน้า/ข้าง
        Vector3 right = transform.right;
        Vector3 lateral = Vector3.Project(v, right);
        Vector3 forward = v - lateral;

        // 1) ต้านความเร็วด้านข้าง (ยางเกาะ)
        float slip = lateral.magnitude;                                  // m/s
        float slip01 = Mathf.Clamp01(slip / Mathf.Max(0.001f, sideSlipSaturation));
        float gripEff = Mathf.Lerp(1f, 0.55f, slip01);                   // ลื่นหนัก → ลดแรงแก้คืนลงเล็กน้อย
        Vector3 lateralCorrection = -lateral * (lateralGrip * gripEff) * rb.mass;
        rb.AddForce(lateralCorrection, ForceMode.Force);

        // 2) ดึงหัวรถให้ตรงทิศวิ่ง (ลด oversteer)
        Vector3 desired = Vector3.Lerp(v, forward.normalized * v.magnitude, stabilityAlign);
        Vector3 alignForce = (desired - v) * rb.mass;
        rb.AddForce(alignForce, ForceMode.Force);

        // 3) หน่วง yaw เพิ่ม
        var ang = rb.angularVelocity;
        float damp = 1f / (1f + yawDamping * dt);
        rb.angularVelocity = new Vector3(ang.x, ang.y * damp, ang.z);
    }

    // ===== Drag & Limits =====
    void ApplyAirDrag()
    {
        float v = rb.linearVelocity.magnitude;
        if (v < 0.001f) return;

        float kmh = v * 3.6f;
        float dragMul = 1f;
        if (kmh < rushDragBlendEndKmh)
        {
            if (kmh <= rushDragBlendStartKmh) dragMul = rushLowSpeedDragMul;
            else
            {
                float t = Mathf.InverseLerp(rushDragBlendStartKmh, rushDragBlendEndKmh, kmh);
                dragMul = Mathf.Lerp(rushLowSpeedDragMul, 1f, t);
            }
        }
        dragMul *= 1f / Mathf.Max(0.0001f, rushIntensity);

        Vector3 drag = -rb.linearVelocity.normalized * v * v * dragCoefficient * dragMul; // ไม่คูณมวล
        rb.AddForce(drag, ForceMode.Force);
    }

    void ApplyTopSpeedLimit()
    {
        float maxMps = KmhToMps(GetActiveTopSpeedKmh());
        float sp = GetSpeedMpsAbsForward();
        if (sp > maxMps)
        {
            // จำกัดเฉพาะองค์ประกอบตามแกนหน้า (ไม่ตัดความเร็วข้าง)
            Vector3 v = rb.linearVelocity;
            Vector3 vFwd = Vector3.Project(v, transform.forward);
            Vector3 vSide = v - vFwd;
            Vector3 vFwdClamped = vFwd.normalized * maxMps;
            rb.linearVelocity = vFwdClamped + vSide;

            if (debugLog) Debug.Log("⏱ Top speed limited");
        }
    }

    float GetActiveTopSpeedKmh()
    {
        if (reversing) return reverseTopSpeedKmh;
        float baseTop = topSpeedKmh * rushIntensity;
        if (burstTimer > 0f) baseTop = Mathf.Max(baseTop, burstTopSpeedKmh);
        return baseTop;
    }

    // ===== Helpers =====
    public float GetSpeedMps() => rb.linearVelocity.magnitude;
    public float GetSpeedKmh() => rb.linearVelocity.magnitude * 3.6f;
    float GetSpeedMpsAbsForward() => Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.forward));
    static float KmhToMps(float kmh) => kmh / 3.6f;

    void ZeroForwardComponent()
    {
        Vector3 v = rb.linearVelocity;
        Vector3 vFwd = Vector3.Project(v, transform.forward);
        rb.linearVelocity = v - vFwd; // คงความเร็วด้านข้างไว้ได้ (ไม่กระชาก)
    }
}
