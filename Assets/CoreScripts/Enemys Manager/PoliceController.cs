using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[RequireComponent(typeof(NavMeshAgent))]
public class PoliceController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;                 

    [Header("Patrol")]
    public Transform[] patrolPoints;         // จุดเดินลาดตระเวน (วนลูป)
    public float patrolWaitTime = 1.0f;      // เวลาหยุดรอที่แต่ละจุด

    [Header("Tuning")]
    public float agentSpeed = 6f;            // ความเร็วไล่ล่า (อัปเดต runtime ได้)
    public float detectRadius = 15f;         // ระยะตรวจจับเริ่มไล่
    public float loseSightRadius = 25f;      // ระยะที่ถือว่า "หลุดการติดตาม"
    public float captureRadius = 1.8f;       // ระยะที่ถือว่าจับผู้เล่นได้

    [Header("Line of Sight")]
    public bool useLineOfSight = true;       // เปิด/ปิดการมองเห็นจริงด้วย Raycast
    public LayerMask losObstacles = ~0;      // เลเยอร์ที่ถือว่าเป็นสิ่งกีดขวางสายตา
    public float losHeightOffset = 1.2f;     // ยกตำแหน่งตา (Ray origin) ให้พ้นพื้น
    public float lostGraceTime = 1.0f;       // อนุโลมไม่มี LOS ได้กี่วินาทีระหว่างไล่

    [Header("Search Settings")]
    public float searchDuration = 5f;        // ระยะเวลาหาเป้าหมาย

    [Header("Events")]
    public UnityEvent onPlayerCaptured;      // ผูกฟังก์ชันสิ้นสุดเกม/ลดชีวิต ฯลฯ

    NavMeshAgent agent;
    int patrolIndex = -1;

    float searchTimer;
    Vector3 lastKnownTargetPos;

    float timeSinceHadLOS = 999f;
    bool captured = false;

    enum State { Patrol, Chase, Search }
    State state = State.Patrol;

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
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) target = p.transform;
        }

        GoNextPatrol();
    }

    void OnDisable()
    {
        CancelInvoke();
    }

    void Update()
    {
        agent.speed = agentSpeed;

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
                hasLOS = hit.transform.CompareTag("Player") || hit.transform.root.CompareTag("Player");
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
                }
                PatrolTick();
                break;

            case State.Chase:
                if (hasLOS)
                {
                    lastKnownTargetPos = target.position;
                    timeSinceHadLOS = 0f;
                }
                else
                {
                    timeSinceHadLOS += Time.deltaTime;
                }

                agent.destination = target.position;

                if (!captured && dist <= captureRadius)
                {
                    captured = true;
                    onPlayerCaptured?.Invoke();
                }

                if (dist > loseSightRadius || timeSinceHadLOS >= lostGraceTime)
                {
                    state = State.Search;
                    agent.destination = lastKnownTargetPos;
                    searchTimer = 0f; // reset ตัวจับเวลา
                }
                break;

            case State.Search:
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    searchTimer += Time.deltaTime;

                    if (dist <= detectRadius && hasLOS)
                    {
                        state = State.Chase;
                        timeSinceHadLOS = 0f;
                        return;
                    }

                    if (searchTimer >= searchDuration)
                    {
                        ReturnToClosestPatrol();
                    }
                }
                break;
        }
    }

    void PatrolTick()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            if (!IsInvoking(nameof(GoNextPatrol)))
                Invoke(nameof(GoNextPatrol), patrolWaitTime);
        }
    }

    void GoNextPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        Vector3 dst = patrolPoints[patrolIndex].position;

        if (NavMesh.SamplePosition(dst, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            agent.destination = hit.position;
        else
            agent.destination = dst;
    }

    int FindClosestPatrolIndex()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return -1;

        float bestDist = Mathf.Infinity;
        int bestIndex = 0;

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            float d = Vector3.Distance(transform.position, patrolPoints[i].position);
            if (d < bestDist)
            {
                bestDist = d;
                bestIndex = i;
            }
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

    // เรียกจากระบบความยาก/Director เพื่ออัปเดตค่า runtime
    public void ApplyDifficulty(float newSpeed, float newDetectRadius, float newLoseSightRadius)
    {
        agentSpeed = newSpeed;
        detectRadius = newDetectRadius;
        loseSightRadius = newLoseSightRadius;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectRadius);
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, loseSightRadius);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, captureRadius);
    }//212
}

