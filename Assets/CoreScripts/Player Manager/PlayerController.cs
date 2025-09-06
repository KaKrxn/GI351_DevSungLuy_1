// PlayerController.cs — Top-down car controller (NFS-style drift + Hard Tilt Lock)
// W/A/S/D ขับ, Space = TAP เพื่อ Drift, HOLD เพื่อ Brake (ตัดคันเร่ง)
// เพิ่ม: forceZeroTiltEveryFrame → บังคับ Rotation แกน X,Z = 0 ทุกเฟรมฟิสิกส์
// Unity 6000.x compatible

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Top Speed (m/s)")]
    public float maxForwardSpeed = 20f;
    public float maxReverseSpeed = 10f;

    [Header("Rates (m/s²)")]
    public float acceleration = 12f; // throttle
    public float engineDeceleration = 5f;  // coast (release)
    public float brakeDeceleration = 25f; // Space (hold)

    [Header("Steering (deg/sec) — Low speed -> High speed")]
    public float steerLowSpeedDeg = 120f; // slow
    public float steerHighSpeedDeg = 50f;  // fast
    public float steerLockAtSpeed = 0.25f; // below this (m/s) → no steering

    [Header("Handling / Stability")]
    public float lateralGrip = 10f;   // higher = less sideslip
    public float stopThreshold = 0.15f; // snap to 0 near stop (anti-jitter)
    public float inputDeadzone = 0.12f; // ignore tiny input noise

    [Header("Steer Filtering")]
    public float steerSlewRate = 4f;  // max change of steer input / sec
    public float steerSmooth = 8f;  // extra smoothing (exp)
    float steerInputFiltered;

    [Header("NFS-style Drift (Space TAP)")]
    public bool enableDrift = true;
    public float driftMinSpeed = 6f;
    public float driftSteerThreshold = 0.30f;
    [Range(0f, 1f)] public float driftGripMultiplier = 0.35f;
    public float driftYawAddDegPerSec = 140f;
    public float driftRecovery = 8f;
    public float brakeHoldThreshold = 0.28f;   // Space held >= this → brake
    public float driftTapDuration = 0.60f;     // Space tap → drift window

    [Header("Physics (Top-down)")]
    public bool topDownMode = true;   // run on XZ plane
    public bool freezeTilt = true;   // lock rot X/Z via constraints
    public float rbDrag = 0.2f;
    public float rbAngularDrag = 6f;
    public Transform centerOfMass;

    [Header("Hard Lock")]
    public bool forceZeroTiltEveryFrame = true; // ✅ บังคับ X,Z rotation = 0 ทุกเฟรมฟิสิกส์

    // ---- runtime ----
    Rigidbody rb;
    float ih, iv;
    float planeY;

    // Space tap/hold
    float spaceDownAt = -999f;
    bool spaceHeld;
    bool brakingHold;
    float driftTapTimer;

    // Drift state
    [Range(0f, 1f)] public float driftBlend = 1f; // 1=normal grip, 0=full drift
    public bool isDrifting { get; private set; }

    static float Dead(float v, float dz) => Mathf.Abs(v) < dz ? 0f : v;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = !topDownMode ? true : false;
        rb.linearDamping = rbDrag;
        rb.angularDamping = rbAngularDrag;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        if (centerOfMass)
            rb.centerOfMass = transform.InverseTransformPoint(centerOfMass.position);

        var cons = RigidbodyConstraints.None;
        if (freezeTilt) cons |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        if (topDownMode) cons |= RigidbodyConstraints.FreezePositionY;
        rb.constraints = cons;

        planeY = transform.position.y;
    }

    void Update()
    {
        // Input + steer filtering
        float rawH = Dead(Input.GetAxis("Horizontal"), inputDeadzone);
        float rawV = Dead(Input.GetAxis("Vertical"), inputDeadzone);
        steerInputFiltered = Mathf.MoveTowards(steerInputFiltered, rawH, steerSlewRate * Time.deltaTime);
        float k = 1f - Mathf.Exp(-steerSmooth * Time.deltaTime);
        ih = Mathf.Lerp(ih, steerInputFiltered, k);
        iv = rawV;

        // Space tap/hold
        bool spaceDown = Input.GetKeyDown(KeyCode.Space);
        bool spaceUp = Input.GetKeyUp(KeyCode.Space);
        spaceHeld = Input.GetKey(KeyCode.Space);

        if (spaceDown) spaceDownAt = Time.unscaledTime;
        brakingHold = spaceHeld && (Time.unscaledTime - spaceDownAt) >= brakeHoldThreshold;

        if (spaceUp)
        {
            float held = Time.unscaledTime - spaceDownAt;
            if (held < brakeHoldThreshold)
                driftTapTimer = driftTapDuration; // TAP → drift window
        }
    }

    void FixedUpdate()
    {
        // ตัด angular velocity แกนเอียงไว้ก่อน (กันเอียงสะสม)
        if (forceZeroTiltEveryFrame)
            rb.angularVelocity = new Vector3(0f, rb.angularVelocity.y, 0f);

        Vector3 fwd = transform.forward;
        Vector3 vel = rb.linearVelocity;
        Vector3 flatVel = new Vector3(vel.x, 0f, vel.z);

        float forwardSpeed = Vector3.Dot(flatVel, fwd);
        float speedAbs = Mathf.Abs(forwardSpeed);
        Vector3 lateral = flatVel - fwd * forwardSpeed;

        // ---- Drift logic (Space TAP) ----
        if (driftTapTimer > 0f) driftTapTimer -= Time.fixedDeltaTime;

        bool driftRequested = enableDrift && driftTapTimer > 0f;
        bool driftCondition = driftRequested
                              && speedAbs >= driftMinSpeed
                              && Mathf.Abs(ih) >= driftSteerThreshold;

        float targetBlend = driftCondition ? 0f : 1f;  // 0 = drift (low grip)
        driftBlend = Mathf.MoveTowards(driftBlend, targetBlend, driftRecovery * Time.fixedDeltaTime);
        isDrifting = driftBlend < 0.5f;

        // Effective grip now
        float gripNow = Mathf.Lerp(lateralGrip * Mathf.Clamp01(driftGripMultiplier), lateralGrip, driftBlend);

        // ลดการไถลข้าง (exp smoothing)
        float kGrip = 1f - Mathf.Exp(-gripNow * Time.fixedDeltaTime);
        lateral = Vector3.Lerp(lateral, Vector3.zero, kGrip);

        // ---- Throttle / Brake model ----
        float ivEffective = brakingHold ? 0f : iv; // brake disables throttle

        float desiredForward =
            ivEffective > 0f ? maxForwardSpeed * Mathf.Clamp01(ivEffective) :
            ivEffective < 0f ? -maxReverseSpeed * Mathf.Clamp01(-ivEffective) : 0f;

        float rate = brakingHold ? brakeDeceleration
                                 : (Mathf.Approximately(ivEffective, 0f) ? engineDeceleration : acceleration);

        float newForward = Mathf.MoveTowards(forwardSpeed, desiredForward, rate * Time.fixedDeltaTime);
        if (Mathf.Abs(newForward) < stopThreshold && Mathf.Approximately(desiredForward, 0f))
            newForward = 0f;

        // Compose velocity (keep Y if not top-down)
        Vector3 newFlat = fwd * newForward + lateral;
        rb.linearVelocity = new Vector3(newFlat.x, topDownMode ? 0f : rb.linearVelocity.y, newFlat.z);

        // (Top-down) ไม่ต้องเซ็ตตำแหน่ง Y ด้วยมือ — ใช้ FreezePositionY ลดจิตเตอร์

        // ---- Steering ----
        bool canSteer = speedAbs >= steerLockAtSpeed;
        float speed01 = Mathf.InverseLerp(0f, maxForwardSpeed, speedAbs);
        float steerRate = Mathf.Lerp(steerLowSpeedDeg, steerHighSpeedDeg, speed01); // fast → smaller
        float steer = canSteer ? (steerRate * ih * Time.fixedDeltaTime) : 0f;
        if (newForward < -1f) steer *= -1f; // reverse flips steer

        // Extra yaw while drifting
        if (enableDrift && isDrifting)
        {
            float phase = 1f - driftBlend; // 0..1
            float yawPerSec = driftYawAddDegPerSec * ih * phase * speed01;
            if (newForward < -1f) yawPerSec *= -1f;
            steer += yawPerSec * Time.fixedDeltaTime;
        }

        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, steer, 0f));

        // --- HARD LOCK TILT (X/Z) — บังคับ Rotation.x/z = 0 ทุกเฟรม ---
        if (forceZeroTiltEveryFrame)
        {
            // บังคับให้เหลือ yaw อย่างเดียว
            var e = rb.rotation.eulerAngles;
            if (Mathf.Abs(e.x) > 0.0001f || Mathf.Abs(e.z) > 0.0001f)
                rb.MoveRotation(Quaternion.Euler(0f, e.y, 0f));
        }
    }

    void OnValidate()
    {
        maxForwardSpeed = Mathf.Max(0.1f, maxForwardSpeed);
        maxReverseSpeed = Mathf.Clamp(maxReverseSpeed, 0.1f, maxForwardSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        engineDeceleration = Mathf.Max(0f, engineDeceleration);
        brakeDeceleration = Mathf.Max(engineDeceleration, brakeDeceleration);

        steerLowSpeedDeg = Mathf.Max(0f, steerLowSpeedDeg);
        steerHighSpeedDeg = Mathf.Clamp(steerHighSpeedDeg, 1f, steerLowSpeedDeg);
        steerLockAtSpeed = Mathf.Max(0f, steerLockAtSpeed);

        lateralGrip = Mathf.Max(0f, lateralGrip);
        stopThreshold = Mathf.Max(0f, stopThreshold);
        inputDeadzone = Mathf.Clamp01(inputDeadzone);

        steerSlewRate = Mathf.Max(0f, steerSlewRate);
        steerSmooth = Mathf.Max(0f, steerSmooth);

        driftMinSpeed = Mathf.Max(0f, driftMinSpeed);
        driftSteerThreshold = Mathf.Clamp01(driftSteerThreshold);
        driftGripMultiplier = Mathf.Clamp(driftGripMultiplier, 0f, 1f);
        driftYawAddDegPerSec = Mathf.Max(0f, driftYawAddDegPerSec);
        driftRecovery = Mathf.Max(0f, driftRecovery);
        brakeHoldThreshold = Mathf.Clamp(brakeHoldThreshold, 0.05f, 1.0f);
        driftTapDuration = Mathf.Clamp(driftTapDuration, 0.1f, 2f);

        rbDrag = Mathf.Max(0f, rbDrag);
        rbAngularDrag = Mathf.Max(0f, rbAngularDrag);
    }
}
