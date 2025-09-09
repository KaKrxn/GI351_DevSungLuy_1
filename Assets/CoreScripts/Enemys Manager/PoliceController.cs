using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class PoliceController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public string playerTag = "Player";

    [Header("Patrol (Waypoints)")]
    [SerializeField] private Transform[] patrolPoints;
    public float patrolWaitTime = 1.0f;
    int patrolIndex = -1;
    float patrolWaitTimer = 0f;

    [Header("Lane Path (วิ่งตามเลน)")]
    [SerializeField] private Transform[] lanePathPoints;
    public bool laneLoop = true;
    public bool useLaneForPatrol = true;
    public bool useLaneForChase = true;
    public float laneReachRadius = 2.0f;
    int laneIndex = -1;

    // ---------- Speed model แบบผู้เล่น ----------
    [Header("Top Speed (Inspector – km/h)")]
    [Tooltip("ตั้ง Top speed เป็น km/h ใน Inspector (runtime จะใช้ m/s)")]
    public bool editSpeedInKmh = true;
    public float baseTopKmh = 72f; // ≈20 m/s

    const float KMH_TO_MS = 1f / 3.6f;
    const float MS_TO_KMH = 3.6f;

    [Header("Tuning (runtime m/s)")]
    [Tooltip("เพดานความเร็วพื้นฐาน (m/s) — ถ้า editSpeedInKmh=true จะ sync จาก baseTopKmh")]
    public float agentSpeed = 6f;
    public float detectRadius = 15f;
    public float loseSightRadius = 25f;
    public float captureRadius = 1.8f;

    [Header("Line of Sight")]
    public bool useLineOfSight = true;
    public LayerMask losObstacles = ~0;
    public float losHeightOffset = 1.2f;
    public float lostGraceTime = 1.0f;

    [Header("Search Settings")]
    public float searchDuration = 5f;

    [Header("Events")]
    public UnityEvent onPlayerHit;

    // ==== Heat ====
    [Header("Rush Scaling (Heat 1–5)")]
    public bool autoHeat = true;
    [Range(1, 5)] public int currentHeat = 1;
    public AnimationCurve speedByHeat = AnimationCurve.Linear(1, 6f, 5, 14f);
    public AnimationCurve detectRadiusByHeat = AnimationCurve.Linear(1, 15f, 5, 28f);
    public AnimationCurve loseSightRadiusByHeat = AnimationCurve.Linear(1, 25f, 5, 40f);
    public AnimationCurve lostGraceByHeat = AnimationCurve.Linear(1, 1.0f, 5, 0.6f);
    public AnimationCurve patrolWaitByHeat = AnimationCurve.Linear(1, 1.2f, 5, 0.2f);

    [Header("Contact Damage")]
    public int contactDamage = 1;
    public float hitCooldown = 0.6f;
    float nextHitAt = 0f;

    [Header("Catch-up (พื้นฐาน)")]
    public float catchUpDistance = 30f;
    public float catchUpMultiplier = 1.25f;

    [Header("Lane Chase Fallback (กันติด)")]
    public bool allowTemporaryOffLane = true;
    public float offLaneSwitchDist = 12f;
    public float stuckTimeout = 1.75f;
    public float resumeLaneDist = 18f;
    bool offLaneOverride = false;
    float lastChaseProgressDist = float.MaxValue;
    float lastChaseProgressTime = -999f;

    [Header("Lane Chase – Direction & Avoidance")]
    public bool laneChaseUseIndexDistance = true;
    [Range(1, 4)] public int laneChaseMaxStepPerTick = 2;
    public float avoidDisableDist = 5f;      // ระยะที่เลิกหลบสิ่งกีดขวางเพื่อไล่ตรง ๆ
    public float closeChaseDistance = 2.0f;  // ระยะเข้าใกล้มาก ๆ

    [Header("NavMesh / Agent")]
    public bool lockToRoadArea = false;
    public bool navAutoBraking = true;       // base; ระหว่าง Chase จะ override ได้
    public float agentAngularSpeedMax = 960f;
    public float agentAngularSpeedMin = 480f;

    [Header("Perf")]
    public float decisionInterval = 0.08f;
    float _nextDecisionAt;

    [Header("Debug")]
    public bool debugDrawGizmos = true;

    [Header("Editor Debug")]
    public bool showPathsInEditMode = true;
    public bool showNavMeshPreviewPath = true;
    public float gizmoLineThickness = 3f;
    public Color patrolPathColor = new Color(1f, 1f, 0f, 0.9f);
    public Color lanePathColor = new Color(0f, 1f, 1f, 0.9f);

    // ---------- Smoothing เพดานความเร็ว ----------
    [Header("Speed Model (m/s²) — smoothing")]
    public float speedAccelRate = 8f;
    public float speedDecelRate = 12f;

    // ---------- Smart Braking / Cornering ----------
    [Header("Smart Braking / Cornering")]
    public bool smartBraking = true;
    [Tooltip("คูณความปลอดภัยของระยะเบรก")]
    public float brakingSafety = 1.2f;
    [Tooltip("มองโค้งล่วงหน้ากี่เมตร")]
    public float minCornerLookAhead = 3.5f;
    [Tooltip("เริ่มชะลอเมื่อมุมโค้งมากกว่า (deg)")]
    public float cornerAngleForSlow = 35f;
    [Tooltip("ชะลอเหลือสัดส่วนเมื่อโค้ง 90°")]
    public float cornerSlowdownAt90 = 0.55f;
    [Tooltip("ชะลอเหลือสัดส่วนเมื่อโค้ง 135°")]
    public float cornerSlowdownAt135 = 0.40f;

    // ---------- Chase Safeguards ----------
    [Header("Chase Safeguards")]
    public bool disableSmartBrakingInChase = true;
    public bool disableAutoBrakingInChase = true;
    public bool softenCornerInChase = false;
    public float chaseMinSpeed = 5f;  // m/s
    public float smartBrakingMinRemDist = 6f;  // m

    // ---------- Aggressive Pursuit ----------
    [Header("Aggressive Pursuit")]
    public bool predictivePursuit = true;
    [Tooltip("เวลาเดายิงนำขั้นต่ำ/สูงสุด (วินาที)")]
    public float leadTimeMin = 0.15f;
    public float leadTimeMax = 0.9f;
    [Tooltip("เพิ่มเวลายิงนำตามระยะห่าง (วินาทีต่อเมตร)")]
    public float leadTimePerMeter = 0.02f;

    [Tooltip("บูสต์ความเร็วตามช่องว่าง: เริ่ม/เต็มที่ (เมตร) และคูณสูงสุด")]
    public bool catchUpBoostByGap = true;
    public float boostStartDist = 15f;
    public float boostFullDist = 60f;
    public float maxCatchupMul = 1.6f;

    // ---------- NavMesh Edge Repel ----------
    [Header("NavMesh Edge Repel")]
    public bool avoidNavMeshEdges = true;
    [Tooltip("ต้องการระยะห่างจากขอบขั้นต่ำ (เมตร)")]
    public float edgeMinDistance = 0.6f;
    [Tooltip("เผื่อขยับเข้าในเมชเพิ่มอีกนิด (เมตร)")]
    public float edgeExtraPush = 0.15f;

    // ---- Internals ----
    NavMeshAgent agent;
    enum State { Patrol, Chase, Search }
    State state = State.Patrol;

    float searchTimer = 0f;
    Vector3 lastKnownTargetPos;
    float timeSinceHadLOS = 999f;

    float _speedCap;        // เพดานความเร็วหลัง smoothing
    Vector3 _tPrevPos;      // cache สำหรับคำนวณ velocity เป้าหมายแบบคร่าว ๆ

    public float CurrentSpeedKmh => agent ? agent.velocity.magnitude * MS_TO_KMH : 0f;

    // ---------- Init ----------
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.autoBraking = navAutoBraking;
        agent.stoppingDistance = Mathf.Max(agent.stoppingDistance, 0.25f);
        agent.acceleration = Mathf.Max(agent.acceleration, speedAccelRate * 1.5f);
        ApplyAngularSpeed(0f);

        if (lockToRoadArea)
        {
            int road = NavMesh.GetAreaFromName("Road");
            if (road >= 0) agent.areaMask = 1 << road;
        }
    }

    void Start()
    {
        SyncSpeedUnits(pushKmhToRuntime: true);
        agent.speed = agentSpeed;
        _speedCap = agentSpeed;

        if (!target)
        {
            var p = GameObject.FindGameObjectWithTag(playerTag);
            if (p) target = p.transform;
        }

        if (patrolPoints != null && patrolPoints.Length > 0) ReturnToClosestPatrol();
        if (lanePathPoints != null && lanePathPoints.Length > 0) SnapToClosestLaneIndex();
    }

    void OnEnable()
    {
        nextHitAt = Time.time + 0.5f;
        offLaneOverride = false;
        lastChaseProgressDist = float.MaxValue;
        lastChaseProgressTime = Time.time;
        _nextDecisionAt = 0f;
        _speedCap = Mathf.Max(_speedCap, agent.speed, agentSpeed);
    }

    void OnValidate()
    {
        SyncSpeedUnits(pushKmhToRuntime: editSpeedInKmh);
        decisionInterval = Mathf.Clamp(decisionInterval, 0.02f, 0.5f);
        laneReachRadius = Mathf.Max(0.05f, laneReachRadius);
        speedAccelRate = Mathf.Max(0f, speedAccelRate);
        speedDecelRate = Mathf.Max(0.1f, speedDecelRate);
        brakingSafety = Mathf.Clamp(brakingSafety, 1.0f, 3.0f);
        minCornerLookAhead = Mathf.Max(0.5f, minCornerLookAhead);
        cornerAngleForSlow = Mathf.Clamp(cornerAngleForSlow, 10f, 175f);
        cornerSlowdownAt90 = Mathf.Clamp01(cornerSlowdownAt90);
        cornerSlowdownAt135 = Mathf.Clamp01(cornerSlowdownAt135);
        agentAngularSpeedMax = Mathf.Max(60f, agentAngularSpeedMax);
        agentAngularSpeedMin = Mathf.Clamp(agentAngularSpeedMin, 60f, agentAngularSpeedMax);
        chaseMinSpeed = Mathf.Max(0f, chaseMinSpeed);
        smartBrakingMinRemDist = Mathf.Max(0f, smartBrakingMinRemDist);
        edgeMinDistance = Mathf.Max(0f, edgeMinDistance);
        edgeExtraPush = Mathf.Max(0f, edgeExtraPush);
        boostFullDist = Mathf.Max(boostStartDist + 0.01f, boostFullDist);
        maxCatchupMul = Mathf.Max(1f, maxCatchupMul);
    }

    // ----------------------------- Update Loop -----------------------------
    void Update()
    {
        if (Time.time < _nextDecisionAt) return;
        _nextDecisionAt = Time.time + decisionInterval;

        // Heat
        if (autoHeat)
        {
            int h = GetHeatByTime(Time.timeSinceLevelLoad);
            if (h != currentHeat) ApplyHeat(h);
        }

        if (!target)
        {
            DoSpeedAndTurn(agentSpeed, State.Patrol);
            if (state != State.Patrol) { state = State.Patrol; GoNextPatrol(); }
            PatrolTick();
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);

        // LOS
        bool hasLOS = true;
        if (useLineOfSight)
        {
            Vector3 eyeFrom = transform.position + Vector3.up * losHeightOffset;
            Vector3 eyeTo = target.position + Vector3.up * losHeightOffset;
            hasLOS = !Physics.Linecast(eyeFrom, eyeTo, losObstacles, QueryTriggerInteraction.Ignore);
        }

        switch (state)
        {
            case State.Patrol:
                if (dist <= detectRadius && hasLOS)
                {
                    state = State.Chase;
                    lastKnownTargetPos = target.position;
                    timeSinceHadLOS = 0f;
                    offLaneOverride = false;
                    lastChaseProgressDist = dist;
                    lastChaseProgressTime = Time.time;
                }

                DoSpeedAndTurn(agentSpeed, State.Patrol);
                PatrolTick();
                break;

            case State.Chase:
                if (hasLOS) { lastKnownTargetPos = target.position; timeSinceHadLOS = 0f; }
                else { timeSinceHadLOS += decisionInterval; }

                if (dist > loseSightRadius || timeSinceHadLOS >= lostGraceTime)
                {
                    state = State.Search;
                    searchTimer = 0f;
                    agent.destination = lastKnownTargetPos;
                    offLaneOverride = false;
                    break;
                }

                // กันติด / ตัดเลนชั่วคราว
                if (allowTemporaryOffLane)
                {
                    if (dist <= offLaneSwitchDist) offLaneOverride = true;
                    if (IsAgentStuck() && (Time.time - lastChaseProgressTime) >= stuckTimeout) offLaneOverride = true;
                    if (offLaneOverride && dist >= resumeLaneDist) offLaneOverride = false;
                }
                else offLaneOverride = false;

                // จุดหมายยิงนำ + กันกอดขอบ
                Vector3 chaseGoal = GetPredictedTargetPos(dist);
                if (!FindSafePointOnNavMesh(chaseGoal, out var safe)) safe = chaseGoal;
                agent.destination = safe;

                // ความเร็ว: บูสต์ตามช่องว่าง
                float desired = agentSpeed;
                if (dist > catchUpDistance) desired *= catchUpMultiplier;
                if (catchUpBoostByGap)
                {
                    float t = Mathf.InverseLerp(boostStartDist, boostFullDist, dist);
                    float mul = Mathf.Lerp(1f, maxCatchupMul, t);
                    desired *= mul;
                }

                DoSpeedAndTurn(desired, State.Chase);
                ChaseTick(dist);
                break;

            case State.Search:
                if (dist <= detectRadius && hasLOS)
                {
                    state = State.Chase;
                    timeSinceHadLOS = 0f;
                    lastChaseProgressDist = dist;
                    lastChaseProgressTime = Time.time;
                    offLaneOverride = false;
                    break;
                }

                DoSpeedAndTurn(agentSpeed, State.Search);

                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
                {
                    searchTimer += decisionInterval;
                    if (searchTimer >= searchDuration) ReturnToClosestPatrol();
                    else GoNextPatrol();
                }
                break;
        }
    }

    // ------------------------- Speed & Turn core -------------------------
    void DoSpeedAndTurn(float desiredSpeed, State s)
    {
        // เลี้ยวไวขึ้นตอนช้า
        float v = agent.velocity.magnitude;
        float t = Mathf.InverseLerp(agentSpeed, 0f, v);
        float ang = Mathf.Lerp(agentAngularSpeedMin, agentAngularSpeedMax, t);
        ApplyAngularSpeed(ang);

        bool inChase = (s == State.Chase);

        // AutoBraking: ปิดช่วงไล่ (จุดหมายขยับตลอด)
        if (disableAutoBrakingInChase) agent.autoBraking = !inChase;
        else agent.autoBraking = navAutoBraking;

        // Smart Braking / Cornering
        if (smartBraking && !(inChase && disableSmartBrakingInChase))
            desiredSpeed = ApplySmartBrakingGuarded(desiredSpeed);

        if (!inChase || softenCornerInChase)
            desiredSpeed = ApplyCornerSlowdown(desiredSpeed);

        // กันช้าเกินไปช่วงไล่
        if (inChase) desiredSpeed = Mathf.Max(desiredSpeed, chaseMinSpeed);

        UpdateSpeedCap(desiredSpeed);
        agent.speed = _speedCap;
    }

    void ApplyAngularSpeed(float angDegPerSec)
    {
        if (agent) agent.angularSpeed = angDegPerSec;
    }

    float ApplySmartBrakingGuarded(float desiredSpeed)
    {
        if (agent.pathPending || !agent.hasPath || agent.remainingDistance < smartBrakingMinRemDist)
            return desiredSpeed;

        float v = Mathf.Max(0f, agent.velocity.magnitude);
        if (v <= 0.1f) return desiredSpeed;

        float aDecel = Mathf.Max(0.1f, speedDecelRate);
        float dStop = (v * v) / (2f * aDecel) * brakingSafety;
        float rem = agent.remainingDistance;

        if (rem > 0.0001f && rem < dStop + agent.stoppingDistance)
        {
            float vmax = Mathf.Sqrt(2f * aDecel * Mathf.Max(0f, rem - agent.stoppingDistance));
            desiredSpeed = Mathf.Min(desiredSpeed, vmax);
        }
        return desiredSpeed;
    }

    float ApplyCornerSlowdown(float desiredSpeed)
    {
        var corners = GetUpcomingCorners(minCornerLookAhead);
        if (corners.Count < 3) return desiredSpeed;

        Vector3 a = corners[1] - corners[0];
        Vector3 b = corners[2] - corners[1];
        a.y = 0f; b.y = 0f;
        if (a.sqrMagnitude < 0.001f || b.sqrMagnitude < 0.001f) return desiredSpeed;

        float angle = Vector3.Angle(a, b); // 0..180
        if (angle < cornerAngleForSlow) return desiredSpeed;

        float f90 = Mathf.Clamp01(cornerSlowdownAt90);
        float f135 = Mathf.Clamp01(cornerSlowdownAt135);

        float factor;
        if (angle <= 90f)
        {
            float t = Mathf.InverseLerp(cornerAngleForSlow, 90f, angle);
            factor = Mathf.Lerp(1f, f90, t);
        }
        else if (angle <= 135f)
        {
            float t = Mathf.InverseLerp(90f, 135f, angle);
            factor = Mathf.Lerp(f90, f135, t);
        }
        else
        {
            float t = Mathf.InverseLerp(135f, 180f, angle);
            factor = Mathf.Lerp(f135, Mathf.Min(0.3f, f135 * 0.75f), t);
        }

        return desiredSpeed * factor;
    }

    List<Vector3> GetUpcomingCorners(float minAhead)
    {
        var list = new List<Vector3>();
        if (!agent || agent.path == null || agent.path.corners == null || agent.path.corners.Length < 2)
            return list;

        var c = agent.path.corners;
        list.Add(c[0]);
        float acc = 0f;
        for (int i = 1; i < c.Length; i++)
        {
            list.Add(c[i]);
            acc += Vector3.Distance(c[i - 1], c[i]);
            if (acc >= minAhead && list.Count >= 3) break;
        }
        return list;
    }

    // ------------------------------ Patrol ------------------------------
    void PatrolTick()
    {
        if (useLaneForPatrol && lanePathPoints != null && lanePathPoints.Length > 0)
        {
            if (laneIndex < 0 || laneIndex >= lanePathPoints.Length || lanePathPoints[laneIndex] == null)
                SnapToClosestLaneIndex();

            if (!agent.pathPending && agent.remainingDistance <= laneReachRadius)
                laneIndex = NextLaneIndex(laneIndex, +1);

            if (laneIndex >= 0 && lanePathPoints[laneIndex] != null)
            {
                Vector3 lanePos = lanePathPoints[laneIndex].position;
                if (!FindSafePointOnNavMesh(lanePos, out lanePos)) { }
                agent.destination = lanePos;
            }
            return;
        }

        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
        {
            patrolWaitTimer += decisionInterval;
            if (patrolWaitTimer >= patrolWaitTime)
            {
                patrolWaitTimer = 0f;
                GoNextPatrol();
            }
        }
        else patrolWaitTimer = 0f;
    }

    void GoNextPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        if (patrolPoints[patrolIndex])
        {
            Vector3 p = patrolPoints[patrolIndex].position;
            if (!FindSafePointOnNavMesh(p, out p)) { }
            agent.destination = p;
        }
    }

    int FindClosestPatrolIndex()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return -1;
        int best = -1; float bestD = float.MaxValue;
        Vector3 pos = transform.position;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (!patrolPoints[i]) continue;
            float d = (patrolPoints[i].position - pos).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    void ReturnToClosestPatrol()
    {
        state = State.Patrol;
        int closest = FindClosestPatrolIndex();
        if (closest >= 0)
        {
            patrolIndex = closest;
            Vector3 p = patrolPoints[patrolIndex].position;
            if (!FindSafePointOnNavMesh(p, out p)) { }
            agent.destination = p;
        }
        else GoNextPatrol();
    }

    // ------------------------------ Chase ------------------------------
    void ChaseTick(float currentDistToPlayer)
    {
        if (currentDistToPlayer <= closeChaseDistance)
        {
            if (agent) agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            Vector3 p = target.position;
            if (!FindSafePointOnNavMesh(p, out p)) { }
            agent.destination = p;
            return;
        }

        if (agent)
        {
            agent.obstacleAvoidanceType =
                (currentDistToPlayer <= avoidDisableDist)
                ? ObstacleAvoidanceType.NoObstacleAvoidance
                : ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        }

        bool canUseLane = useLaneForChase && lanePathPoints != null && lanePathPoints.Length > 0 && !offLaneOverride;

        if (canUseLane)
        {
            if (laneIndex < 0) SnapToClosestLaneIndex();

            int playerIdx = GetClosestLaneIndex(target.position);
            if (playerIdx >= 0 && laneIndex != playerIdx)
            {
                if (laneChaseUseIndexDistance)
                {
                    int len = lanePathPoints.Length;
                    int dir = DirectionTowardsIndex(laneIndex, playerIdx, len, laneLoop);
                    int steps = Mathf.Clamp(ShortestStepDistance(laneIndex, playerIdx, len, laneLoop), 1, laneChaseMaxStepPerTick);
                    laneIndex = NextLaneIndex(laneIndex, dir * steps);
                }
                else
                {
                    int forward = NextLaneIndex(laneIndex, +1);
                    int backward = NextLaneIndex(laneIndex, -1);
                    float df = (lanePathPoints[forward].position - lanePathPoints[playerIdx].position).sqrMagnitude;
                    float db = (lanePathPoints[backward].position - lanePathPoints[playerIdx].position).sqrMagnitude;
                    laneIndex = (df < db) ? forward : backward;
                }
            }

            if (laneIndex >= 0 && lanePathPoints[laneIndex] != null)
            {
                Vector3 lanePos = lanePathPoints[laneIndex].position;
                if (!FindSafePointOnNavMesh(lanePos, out lanePos)) { }
                agent.destination = lanePos;

                float distNextToPlayer = Vector3.Distance(lanePos, target.position);
                if (allowTemporaryOffLane && distNextToPlayer > currentDistToPlayer * 1.05f)
                {
                    offLaneOverride = true;
                    Vector3 p = target.position;
                    if (!FindSafePointOnNavMesh(p, out p)) { }
                    agent.destination = p;
                }
            }
            else
            {
                Vector3 p = target.position;
                if (!FindSafePointOnNavMesh(p, out p)) { }
                agent.destination = p;
            }
        }
        else
        {
            Vector3 p = target.position;
            if (!FindSafePointOnNavMesh(p, out p)) { }
            agent.destination = p;
        }
    }

    // ----------------------- Lane Helpers -----------------------
    void SnapToClosestLaneIndex()
    {
        laneIndex = GetClosestLaneIndex(transform.position);
        if (laneIndex >= 0 && lanePathPoints[laneIndex] != null)
        {
            Vector3 p = lanePathPoints[laneIndex].position;
            if (!FindSafePointOnNavMesh(p, out p)) { }
            agent.destination = p;
        }
    }

    int GetClosestLaneIndex(Vector3 pos)
    {
        if (lanePathPoints == null || lanePathPoints.Length == 0) return -1;
        int best = -1; float bestDist = Mathf.Infinity;
        for (int i = 0; i < lanePathPoints.Length; i++)
        {
            var t = lanePathPoints[i];
            if (!t) continue;
            float d = (t.position - pos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    int NextLaneIndex(int i, int dir = 1)
    {
        if (lanePathPoints == null || lanePathPoints.Length == 0) return -1;
        int n = i + dir;
        if (laneLoop)
        {
            int len = lanePathPoints.Length;
            n = (n % len + len) % len;
        }
        else n = Mathf.Clamp(n, 0, lanePathPoints.Length - 1);
        return n;
    }

    int ShortestStepDistance(int from, int to, int len, bool looped)
    {
        if (len <= 0) return 0;
        if (!looped) return Mathf.Abs(to - from);
        int fwd = (to - from + len) % len;
        int bwd = (from - to + len) % len;
        return Mathf.Min(fwd, bwd);
    }

    int DirectionTowardsIndex(int from, int to, int len, bool looped)
    {
        if (len <= 0 || from == to) return +1;
        if (!looped) return (to > from) ? +1 : -1;

        int fwd = (to - from + len) % len;
        int bwd = (from - to + len) % len;

        if (fwd < bwd) return +1;
        if (bwd < fwd) return -1;

        int nextF = NextLaneIndex(from, +1);
        int nextB = NextLaneIndex(from, -1);
        float dF = (lanePathPoints[nextF].position - lanePathPoints[to].position).sqrMagnitude;
        float dB = (lanePathPoints[nextB].position - lanePathPoints[to].position).sqrMagnitude;
        return (dF <= dB) ? +1 : -1;
    }

    // ----------------------- External Hooks -----------------------
    public void SetPatrolPoints(Transform[] points)
    {
        patrolPoints = points;
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        int closest = FindClosestPatrolIndex();
        if (closest >= 0) { patrolIndex = closest - 1; GoNextPatrol(); }
    }

    public void SetLanePathPoints(Transform[] points)
    {
        lanePathPoints = points;
        if (lanePathPoints == null || lanePathPoints.Length == 0) return;
        SnapToClosestLaneIndex();
    }

    public void ApplyHeat(int heat)
    {
        currentHeat = Mathf.Clamp(heat, 1, 5);
        agentSpeed = speedByHeat.Evaluate(currentHeat);
        detectRadius = detectRadiusByHeat.Evaluate(currentHeat);
        loseSightRadius = loseSightRadiusByHeat.Evaluate(currentHeat);
        lostGraceTime = lostGraceByHeat.Evaluate(currentHeat);
        patrolWaitTime = patrolWaitByHeat.Evaluate(currentHeat);
        // หมายเหตุ: agentSpeed จะถูกส่งเข้าระบบ smoothing อีกชั้น
    }

    int GetHeatByTime(float t)
    {
        if (t < 60f) return 1;
        if (t < 120f) return 2;
        if (t < 180f) return 3;
        if (t < 240f) return 4;
        return 5;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (Time.time < nextHitAt) return;

        var hp = other.GetComponent<PlayerHealth>();
        if (hp != null)
        {
            hp.TakeDamage(contactDamage);
            onPlayerHit?.Invoke();
        }
        nextHitAt = Time.time + hitCooldown;
    }

    // -------- Helpers: pursuit & edge repel --------
    Vector3 GetTargetVelocity()
    {
        if (!target) return Vector3.zero;
        var rb = target.GetComponent<Rigidbody>();
        if (rb) return rb.linearVelocity;

        // ประเมินคร่าว ๆ (เผื่อไม่มี Rigidbody)
        _tPrevPos = Vector3.Lerp(_tPrevPos, target.position, 0.2f);
        Vector3 v = (target.position - _tPrevPos) / Mathf.Max(Time.deltaTime, 1e-4f);
        return v;
    }

    Vector3 GetPredictedTargetPos(float distToTarget)
    {
        if (!predictivePursuit || !target) return target ? target.position : transform.position;
        float lead = Mathf.Clamp(leadTimePerMeter * distToTarget, leadTimeMin, leadTimeMax);
        return target.position + GetTargetVelocity() * lead;
    }

    bool FindSafePointOnNavMesh(Vector3 desired, out Vector3 safe)
    {
        safe = desired;
        if (!avoidNavMeshEdges) return false;

        int mask = agent ? agent.areaMask : NavMesh.AllAreas;
        if (!NavMesh.SamplePosition(desired, out var hit, Mathf.Max(edgeMinDistance, 1.0f), mask))
            return false;

        Vector3 p = hit.position;

        if (NavMesh.FindClosestEdge(p, out var edge, mask))
        {
            if (edge.distance < edgeMinDistance)
            {
                Vector3 push = edge.normal * (edgeMinDistance - edge.distance + edgeExtraPush);
                p += push;
                if (NavMesh.SamplePosition(p, out var hit2, Mathf.Max(edgeMinDistance, 1.0f), mask))
                    p = hit2.position;
            }
        }

        safe = p;
        return true;
    }

    bool IsAgentStuck()
    {
        if (!agent) return false;
        return agent.velocity.sqrMagnitude < 0.04f;
    }

    void SyncSpeedUnits(bool pushKmhToRuntime)
    {
        if (pushKmhToRuntime) agentSpeed = Mathf.Max(0f, baseTopKmh * KMH_TO_MS);
        else baseTopKmh = Mathf.Max(0f, agentSpeed * MS_TO_KMH);
    }

    void UpdateSpeedCap(float desiredSpeed)
    {
        float rate = (desiredSpeed > _speedCap) ? speedAccelRate : speedDecelRate;
        _speedCap = Mathf.MoveTowards(_speedCap, desiredSpeed, rate * decisionInterval);
    }

#if UNITY_EDITOR
    // -------- Gizmos / Handles --------
    void OnDrawGizmos()
    {
        if (!showPathsInEditMode) return;

        if (patrolPoints != null && patrolPoints.Length > 1)
            DrawPolyline(patrolPoints, true, patrolPathColor, false);

        if (lanePathPoints != null && lanePathPoints.Length > 1)
            DrawPolyline(lanePathPoints, laneLoop, lanePathColor, true);

        if (showNavMeshPreviewPath && target != null)
            DrawNavMeshPreviewPath();
    }

    void DrawPolyline(Transform[] pts, bool loop, Color c, bool arrows)
    {
        var list = new List<Vector3>();
        for (int i = 0; i < pts.Length; i++)
        {
            if (pts[i]) list.Add(pts[i].position + Vector3.up * 0.05f);
        }
        if (list.Count < 2) return;

        UnityEditor.Handles.color = c;
        UnityEditor.Handles.DrawAAPolyLine(gizmoLineThickness, list.ToArray());
        if (loop)
            UnityEditor.Handles.DrawAAPolyLine(gizmoLineThickness, new Vector3[] { list[list.Count - 1], list[0] });

        if (arrows)
        {
            for (int i = 0; i < list.Count - 1; i++)
                DrawArrow(list[i], list[i + 1], c);
            if (loop) DrawArrow(list[list.Count - 1], list[0], c);
        }
    }

    void DrawArrow(Vector3 a, Vector3 b, Color c)
    {
        Vector3 dir = (b - a);
        float len = dir.magnitude;
        if (len < 0.01f) return;
        dir /= len;

        Vector3 p = Vector3.Lerp(a, b, 0.85f);
        float headLen = Mathf.Clamp(len * 0.15f, 0.6f, 2.0f);

        Vector3 left = Quaternion.AngleAxis(25f, Vector3.up) * (-dir);
        Vector3 right = Quaternion.AngleAxis(-25f, Vector3.up) * (-dir);

        UnityEditor.Handles.color = c;
        UnityEditor.Handles.DrawAAPolyLine(gizmoLineThickness, p, p + left * headLen);
        UnityEditor.Handles.DrawAAPolyLine(gizmoLineThickness, p, p + right * headLen);
    }

    void DrawNavMeshPreviewPath()
    {
        int mask = agent ? agent.areaMask : NavMesh.AllAreas;

        Vector3 from = transform.position;
        Vector3 to = target ? target.position : transform.position;
        if (NavMesh.SamplePosition(from, out var hf, 2f, mask)) from = hf.position;
        if (NavMesh.SamplePosition(to, out var ht, 2f, mask)) to = ht.position;

        var path = new NavMeshPath();
        if (NavMesh.CalculatePath(from, to, mask, path) && path.corners != null && path.corners.Length > 1)
        {
            UnityEditor.Handles.color = new Color(0.2f, 1f, 0.2f, 0.9f);
            UnityEditor.Handles.DrawAAPolyLine(gizmoLineThickness, path.corners);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!debugDrawGizmos) return;

        Vector3 origin = transform.position + Vector3.up * losHeightOffset;

        Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
        Gizmos.DrawWireSphere(origin, detectRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawWireSphere(origin, loseSightRadius);

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (!patrolPoints[i]) continue;
                Vector3 p = patrolPoints[i].position + Vector3.up * 0.25f;
                Gizmos.DrawSphere(p, 0.25f);
                Transform next = patrolPoints[(i + 1) % patrolPoints.Length];
                if (next) Gizmos.DrawLine(p, next.position + Vector3.up * 0.25f);
            }
        }

        if (lanePathPoints != null && lanePathPoints.Length > 0)
        {
            Gizmos.color = offLaneOverride ? new Color(1f, 0.2f, 0.2f, 0.9f) : Color.cyan;
            for (int i = 0; i < lanePathPoints.Length; i++)
            {
                if (!lanePathPoints[i]) continue;
                Vector3 p = lanePathPoints[i].position + Vector3.up * 0.15f;
                Gizmos.DrawCube(p, Vector3.one * 0.25f);
                int ni = NextLaneIndex(i, +1);
                if (ni >= 0 && lanePathPoints[ni])
                    Gizmos.DrawLine(p, lanePathPoints[ni].position + Vector3.up * 0.15f);
            }
        }

        if (useLineOfSight && target != null)
        {
            Vector3 tgt = target.position + Vector3.up * losHeightOffset;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, tgt);
        }

        if (Application.isPlaying && agent != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, agent.destination);
            Gizmos.DrawSphere(agent.destination, 0.2f);
        }
    }
#endif
}
