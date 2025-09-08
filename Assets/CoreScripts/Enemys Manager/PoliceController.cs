using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[RequireComponent(typeof(NavMeshAgent))]
public class PoliceController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;                 // ถ้าเว้นว่าง จะค้นหา GameObject ที่ Tag = playerTag อัตโนมัติ
    public string playerTag = "Player";

    [Header("Patrol (Waypoint แบบเดิม)")]
    [SerializeField] private Transform[] patrolPoints;   // จุดเดินลาดตระเวน (วนลูป)
    public float patrolWaitTime = 1.0f;                  // เวลาหยุดรอที่แต่ละจุด
    int patrolIndex = -1;
    float patrolWaitTimer = 0f;

    [Header("Lane Path (วิ่งตามเลน)")]
    [SerializeField] private Transform[] lanePathPoints; // จุดกลางเลนตามทาง
    public bool laneLoop = true;
    public bool useLaneForPatrol = true;                 // ใช้เลนตอนเดิน Patrol
    public bool useLaneForChase = true;                 // ใช้เลนตอน Chase
    public float laneReachRadius = 2.0f;                 // ระยะถึงจุดเลนที่ถือว่า "ถึงแล้ว"
    int laneIndex = -1;

    [Header("Tuning")]
    public float agentSpeed = 6f;                         // ความเร็วฐาน (อัปเดต runtime ได้)
    public float detectRadius = 15f;                      // ระยะตรวจจับเริ่มไล่
    public float loseSightRadius = 25f;                   // ระยะที่ถือว่า "หลุดการติดตาม"
    public float captureRadius = 1.8f;                    // ระยะที่ถือว่าจับผู้เล่นได้ (เผื่อใช้ในอนาคต)

    [Header("Line of Sight")]
    public bool useLineOfSight = true;                    // เปิด/ปิดการมองเห็นจริงด้วย Raycast
    public LayerMask losObstacles = ~0;                   // เลเยอร์สิ่งกีดขวางสายตา
    public float losHeightOffset = 1.2f;                  // ยกตำแหน่งตา (Ray origin) ให้พ้นพื้น
    public float lostGraceTime = 1.0f;                    // อนุโลมไม่มี LOS ได้กี่วินาทีระหว่างไล่

    [Header("Search Settings")]
    public float searchDuration = 5f;

    [Header("Events")]
    public UnityEvent onPlayerHit;                        // ผูกฟังก์ชันสิ้นสุดเกม/ลดชีวิต ฯลฯ

    // ==== Rush / Heat Scaling (ปรับค่าตัวแปรเดิมตาม Heat 1–5) ====
    [Header("Rush Scaling (Heat 1–5)")]
    [Tooltip("เปิดคำนวณ Heat อัตโนมัติจากเวลาเล่น (0/60/120/180/240s) หรือปิดเพื่อรับจากภายนอกผ่าน ApplyHeat()")]
    public bool autoHeat = true;
    [Range(1, 5)] public int currentHeat = 1;

    public AnimationCurve speedByHeat = AnimationCurve.Linear(1, 6f, 5, 14f);
    public AnimationCurve detectRadiusByHeat = AnimationCurve.Linear(1, 15f, 5, 28f);
    public AnimationCurve loseSightRadiusByHeat = AnimationCurve.Linear(1, 25f, 5, 40f);
    public AnimationCurve lostGraceByHeat = AnimationCurve.Linear(1, 1.0f, 5, 0.6f);
    public AnimationCurve patrolWaitByHeat = AnimationCurve.Linear(1, 1.2f, 5, 0.2f);

    [Header("Contact Damage")]
    public int contactDamage = 1;
    public float hitCooldown = 0.6f;   // คูลดาวน์กันโดนย้ำถี่ ๆ
    private float nextHitAt = 0f;

    [Header("Catch-up (Rush)")]
    public float catchUpDistance = 30f;        // ระยะที่ถ้าห่างเกินนี้จะบูสต์ความเร็ว
    public float catchUpMultiplier = 1.25f;    // อัตราเร่งตามเมื่อห่างมาก

    [Header("Lane Chase Fallback (แก้ติดตรึง)")]
    public bool allowTemporaryOffLane = true;  // อนุญาตตัดเลนชั่วคราวเพื่อเข้าหาผู้เล่น
    public float offLaneSwitchDist = 12f;      // ถ้าอยู่ใกล้ผู้เล่น <= ระยะนี้ ให้ตัดเลนไล่ตรง
    public float stuckTimeout = 1.75f;         // ถ้าไม่คืบหน้า (ระยะไม่ลด) เกินเวลานี้ ให้ตัดเลน
    public float resumeLaneDist = 18f;         // เมื่อห่าง > ระยะนี้ ค่อยกลับไปตามเลนอีกครั้ง
    bool offLaneOverride = false;
    float lastChaseProgressDist = float.MaxValue;
    float lastChaseProgressTime = -999f;

    [Header("Debug Gizmos")]
    public bool debugDrawGizmos = true;        // เปิด/ปิดการวาด Gizmos ใน Scene

    // ===== Internals =====
    NavMeshAgent agent;
    enum State { Patrol, Chase, Search }
    State state = State.Patrol;

    float searchTimer = 0f;
    Vector3 lastKnownTargetPos;
    float timeSinceHadLOS = 999f;

    // ---------- Init ----------
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.autoBraking = false;
    }

    void Start()
    {
        agent.speed = agentSpeed;

        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag(playerTag);
            if (p) target = p.transform;
        }

        if (patrolPoints != null && patrolPoints.Length > 0)
            ReturnToClosestPatrol();

        if (lanePathPoints != null && lanePathPoints.Length > 0)
            SnapToClosestLaneIndex();
    }

    void OnEnable()
    {
        // กันโดนชนตั้งแต่เฟรมแรกหลังเกิด
        nextHitAt = Time.time + 0.5f;
        offLaneOverride = false;
        lastChaseProgressDist = float.MaxValue;
        lastChaseProgressTime = Time.time;
    }

    // ----------------------------- Update Loop -----------------------------
    void Update()
    {
        // agent.speed ใช้ agentSpeed เป็นฐาน แล้วไปเพิ่ม/ลดชั่วคราวตอน Chase
        agent.speed = agentSpeed;

        // Auto heat ถ้าเปิดไว้
        if (autoHeat)
        {
            int h = GetHeatByTime(Time.timeSinceLevelLoad);
            if (h != currentHeat) ApplyHeat(h);
        }

        if (target == null)
        {
            if (state != State.Patrol) { state = State.Patrol; GoNextPatrol(); }
            PatrolTick();
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);

        bool hasLOS = true;
        if (useLineOfSight)
        {
            Vector3 eyeFrom = transform.position + Vector3.up * losHeightOffset;
            Vector3 eyeTo = target.position + Vector3.up * losHeightOffset;

            if (Physics.Linecast(eyeFrom, eyeTo, out RaycastHit hit, losObstacles, QueryTriggerInteraction.Ignore))
            {
                hasLOS = hit.transform.CompareTag(playerTag) || hit.transform.root.CompareTag(playerTag);
            }
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
                PatrolTick();
                break;

            case State.Chase:
                if (hasLOS) { lastKnownTargetPos = target.position; timeSinceHadLOS = 0f; }
                else { timeSinceHadLOS += Time.deltaTime; }

                if (dist > loseSightRadius || timeSinceHadLOS >= lostGraceTime)
                {
                    state = State.Search;
                    searchTimer = 0f;
                    agent.destination = lastKnownTargetPos;
                    offLaneOverride = false;
                    break;
                }

                // อัปเดตความคืบหน้า (ลดระยะ = คืบหน้า)
                if (dist < lastChaseProgressDist - 0.25f) // ลดลงอย่างน้อย 0.25m ถือว่าคืบหน้า
                {
                    lastChaseProgressDist = dist;
                    lastChaseProgressTime = Time.time;
                }

                // เงื่อนไข "ตัดเลนชั่วคราว"
                if (allowTemporaryOffLane)
                {
                    // 1) ใกล้ผู้เล่นมากพอ -> ตัดเลนเพื่อเข้าหาทันที
                    if (dist <= offLaneSwitchDist) offLaneOverride = true;

                    // 2) ติด (ไม่มีความคืบหน้า) เกินเวลา -> ตัดเลน
                    if ((Time.time - lastChaseProgressTime) >= stuckTimeout) offLaneOverride = true;

                    // 3) ถ้าตัดเลนอยู่แล้ว และห่างออกไปมาก -> กลับไปตามเลน
                    if (offLaneOverride && dist >= resumeLaneDist) offLaneOverride = false;
                }
                else
                {
                    offLaneOverride = false;
                }

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

                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
                {
                    searchTimer += Time.deltaTime;
                    if (searchTimer >= searchDuration)
                    {
                        ReturnToClosestPatrol();
                    }
                    else
                    {
                        GoNextPatrol();
                    }
                }
                break;
        }
    }

    // ------------------------------ Patrol ------------------------------
    void PatrolTick()
    {
        // ถ้าใช้เลนและมีเลน → ให้ไหลไปตามเลน (แทน Patrol point)
        if (useLaneForPatrol && lanePathPoints != null && lanePathPoints.Length > 0)
        {
            if (laneIndex < 0 || laneIndex >= lanePathPoints.Length || lanePathPoints[laneIndex] == null)
                SnapToClosestLaneIndex();

            if (!agent.pathPending && agent.remainingDistance <= laneReachRadius)
            {
                laneIndex = NextLaneIndex(laneIndex, +1);
            }

            if (laneIndex >= 0 && lanePathPoints[laneIndex] != null)
                agent.destination = lanePathPoints[laneIndex].position;

            return;
        }

        // ---- โหมด Patrol แบบเดิม (Waypoint) ----
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
        {
            if (!IsInvoking(nameof(GoNextPatrol)))
                Invoke(nameof(GoNextPatrol), patrolWaitTime);
        }
    }

    void GoNextPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        if (patrolPoints[patrolIndex])
            agent.destination = patrolPoints[patrolIndex].position;
    }

    int FindClosestPatrolIndex()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return -1;

        int bestIndex = -1;
        float bestDist = float.MaxValue;
        Vector3 pos = transform.position;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (!patrolPoints[i]) continue;
            float d = (patrolPoints[i].position - pos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; bestIndex = i; }
        }
        return bestIndex;
    }

    void ReturnToClosestPatrol()
    {
        state = State.Patrol;
        int closest = FindClosestPatrolIndex();
        if (closest >= 0)
        {
            patrolIndex = closest;
            agent.destination = patrolPoints[patrolIndex].position;
        }
        else
        {
            GoNextPatrol();
        }
    }

    // ------------------------------ Chase ------------------------------
    void ChaseTick(float currentDistToPlayer)
    {
        bool canUseLane = useLaneForChase && lanePathPoints != null && lanePathPoints.Length > 0 && !offLaneOverride;

        if (canUseLane)
        {
            if (laneIndex < 0) SnapToClosestLaneIndex();

            int playerIdx = GetClosestLaneIndex(target.position);

            // ขยับ laneIndex ไปทาง playerIdx ทีละขั้น (และรองรับ loop)
            if (playerIdx >= 0 && playerIdx != laneIndex)
            {
                int forward = NextLaneIndex(laneIndex, +1);
                int backward = NextLaneIndex(laneIndex, -1);

                float df = (lanePathPoints[forward].position - lanePathPoints[playerIdx].position).sqrMagnitude;
                float db = (lanePathPoints[backward].position - lanePathPoints[playerIdx].position).sqrMagnitude;

                laneIndex = (df < db) ? forward : backward;
            }

            if (laneIndex >= 0 && lanePathPoints[laneIndex] != null)
                agent.destination = lanePathPoints[laneIndex].position;
        }
        else
        {
            // ---- โหมดไล่ล่าแบบตรง (ตัดเลนชั่วคราว หรือไม่ได้ใช้เลน) ----
            agent.destination = target.position;
        }

        // เพิ่มอารมณ์ rush: เร่งตามเมื่อห่างมาก
        float spd = agentSpeed;
        if (currentDistToPlayer > catchUpDistance) spd *= catchUpMultiplier;
        agent.speed = spd;
    }

    // ----------------------- Lane Helpers -----------------------
    void SnapToClosestLaneIndex()
    {
        laneIndex = GetClosestLaneIndex(transform.position);
        if (laneIndex >= 0 && lanePathPoints[laneIndex] != null)
            agent.destination = lanePathPoints[laneIndex].position;
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
        else
        {
            n = Mathf.Clamp(n, 0, lanePathPoints.Length - 1);
        }
        return n;
    }

    // ----------------------- External Hooks -----------------------
    public void SetPatrolPoints(Transform[] points)   // เรียกจาก SpawnManager (เหมือนเดิม)
    {
        patrolPoints = points;

        if (patrolPoints == null || patrolPoints.Length == 0) return;

        int closest = FindClosestPatrolIndex();
        if (closest >= 0)
        {
            patrolIndex = closest - 1; // ให้ GoNextPatrol เลือกเป็น closest
            GoNextPatrol();
        }
    }

    public void SetLanePathPoints(Transform[] points) // เรียกจาก SpawnManager
    {
        lanePathPoints = points;
        if (lanePathPoints == null || lanePathPoints.Length == 0) return;
        SnapToClosestLaneIndex();
    }

    // ใช้เมื่อมีระบบกลางอยากบังคับ Heat เอง (ถ้าไม่ใช้ ให้เปิด autoHeat)
    public void ApplyHeat(int heat)
    {
        currentHeat = Mathf.Clamp(heat, 1, 5);
        agentSpeed = speedByHeat.Evaluate(currentHeat);
        detectRadius = detectRadiusByHeat.Evaluate(currentHeat);
        loseSightRadius = loseSightRadiusByHeat.Evaluate(currentHeat);
        lostGraceTime = lostGraceByHeat.Evaluate(currentHeat);
        patrolWaitTime = patrolWaitByHeat.Evaluate(currentHeat);
    }

    int GetHeatByTime(float t)
    {
        if (t < 60f) return 1;
        if (t < 120f) return 2;
        if (t < 180f) return 3;
        if (t < 240f) return 4;
        return 5;
    }

    // ------------------- Damage on contact (Trigger) -------------------
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (Time.time < nextHitAt) return; // คูลดาวน์กันโดนย้ำ

        var hp = other.GetComponent<PlayerHealth>();
        if (hp != null)
        {
            hp.TakeDamage(contactDamage);
            onPlayerHit?.Invoke();
        }

        // เริ่มคูลดาวน์ใหม่ทุกครั้งหลังชน
        nextHitAt = Time.time + hitCooldown;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!debugDrawGizmos) return;

        Vector3 origin = transform.position + Vector3.up * losHeightOffset;

        // Detect / LoseSight
        Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
        Gizmos.DrawWireSphere(origin, detectRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawWireSphere(origin, loseSightRadius);

        // Patrol points
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

        // Lane path points
        if (lanePathPoints != null && lanePathPoints.Length > 0)
        {
            Gizmos.color = offLaneOverride ? new Color(1f, 0.2f, 0.2f, 0.9f) : Color.cyan; // แดงถ้ากำลังตัดเลนอยู่
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

        // LOS line
        if (useLineOfSight && target != null)
        {
            Vector3 tgt = target.position + Vector3.up * losHeightOffset;
            bool blocked = Physics.Linecast(origin, tgt, out RaycastHit hit, losObstacles, QueryTriggerInteraction.Ignore)
                           && !(hit.transform.CompareTag(playerTag) || hit.transform.root.CompareTag(playerTag));

            Gizmos.color = blocked ? Color.red : Color.green;
            Gizmos.DrawLine(origin, tgt);
        }

        // Destination (runtime)
        if (Application.isPlaying && agent != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, agent.destination);
            Gizmos.DrawSphere(agent.destination, 0.2f);
        }
    }
#endif
}
