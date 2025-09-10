using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavCarAI : MonoBehaviour
{
    [Header("Speed / Steering")]
    public float cruiseSpeed = 12f;          // ความเร็วเดินทาง (m/s)
    public float accel = 8f;                 // อัตราเร่ง
    public float turnLerp = 8f;              // softness การหมุนตัวรถเอง
    public bool alignWithVelocity = true;    // หมุนตามความเร็ว (ถ้าปิดจะใช้ agent.updateRotation แทน)

    [Header("Look Ahead / Junction")]
    public float lookAhead = 12f;            // ระยะสำรวจทางข้างหน้า
    public float postPickAdvance = 10f;      // จุดปลายทางที่วางล่วงหน้า หลังเลือกทิศ
    [Range(10f, 85f)]
    public float sideAngle = 55f;            // มุมซ้าย/ขวาที่จะสำรวจเป็น "ตัวเลือกเลี้ยว"
    public float decisionCooldown = 1.5f;    // กันการตัดสินใจถี่เกิน
    [Range(0f, 1f)]
    public float turnProbability = 0.5f;     // โอกาส "อยากเลี้ยว" เมื่อแยกเปิดให้เลี้ยวได้ (ถ้าไม่เลี้ยวและไปตรงได้ จะเลือกตรง)

    [Header("Ray Sensing (Obstacles)")]
    public LayerMask obstacleMask;           // Layer ของสิ่งกีดขวาง
    public float stopDistance = 4f;          // ระยะใกล้สุดที่ควรชะลอ/หยุด
    public float sideProbeDistance = 3f;     // ระยะเช็คซ้าย/ขวาเพื่อหลบสิ่งกีดขวาง
    public float rayHeight = 0.5f;

    [Header("NavMesh Sampling")]
    public float sampleRadius = 2.0f;        // รัศมีที่ยอมให้จุดเป้าหมายเลื่อนไปหา navmesh
    public int areaMask = NavMesh.AllAreas;  // จำกัดพื้นที่ navmesh ถ้าต้องการ

    [Header("Debug")]
    public bool drawDebug = true;

    private NavMeshAgent agent;
    private float currentSpeed;
    private float nextDecisionTime = 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        // เราจะคุมความเร็วเอง เพื่อชะลอ/เร่งตาม Ray
        agent.autoBraking = false;
        agent.updateRotation = !alignWithVelocity; // ถ้าเราหมุนเอง ให้ปิดของ Agent
        currentSpeed = cruiseSpeed;
    }

    void Start()
    {
        // ตั้งปลายทางเริ่มต้นเป็น "ข้างหน้า" บน NavMesh
        SetDestinationForward();
    }

    void Update()
    {
        SenseObstaclesAndAdjustSpeed();
        DecideAtJunctionIfAny();
        KeepMovingForwardIfIdle();
        ApplySpeed();

        if (alignWithVelocity) RotateToVelocity();
    }

    // ---------------------- Decision / Movement ----------------------

    void DecideAtJunctionIfAny()
    {
        if (Time.time < nextDecisionTime) return;

        // ดูทางที่ไปได้จริง 3 ทิศ: ซ้าย/ตรง/ขวา จากมุม sideAngle
        Vector3 forward = transform.forward;
        Vector3 leftDir = Quaternion.Euler(0f, -sideAngle, 0f) * forward;
        Vector3 rightDir = Quaternion.Euler(0f, sideAngle, 0f) * forward;

        var candidates = new List<(Vector3 dir, Vector3 point, bool isStraight)>();

        // ตรง
        if (TrySampleAhead(forward, lookAhead, out Vector3 straightPt) && PathIsReachable(straightPt))
            candidates.Add((forward, straightPt, true));

        // ซ้าย
        if (TrySampleAhead(leftDir, lookAhead, out Vector3 leftPt) && PathIsReachable(leftPt))
            candidates.Add((leftDir, leftPt, false));

        // ขวา
        if (TrySampleAhead(rightDir, lookAhead, out Vector3 rightPt) && PathIsReachable(rightPt))
            candidates.Add((rightDir, rightPt, false));

        // "ทางแยก" แบบง่าย = มีอย่างน้อย 2 ทางเลือกที่ไปได้
        if (candidates.Count >= 2)
        {
            Vector3 chosenPoint;

            // ถ้าอยาก "ไม่เลี้ยว" และมีตรง -> เลือกตรง
            bool wantStraight = Random.value > turnProbability;
            if (wantStraight)
            {
                var straight = candidates.Find(c => c.isStraight);
                if (straight.point != Vector3.zero)
                {
                    chosenPoint = AdvancePastPoint(straight.point, straight.dir, postPickAdvance);
                    SetDestination(chosenPoint);
                    nextDecisionTime = Time.time + decisionCooldown;
                    return;
                }
            }

            // เลือกสุ่ม (ซ้าย/ขวา หรือรวมตรงด้วย ถ้าข้างบนไม่ติด)
            var pick = candidates[Random.Range(0, candidates.Count)];
            chosenPoint = AdvancePastPoint(pick.point, pick.dir, postPickAdvance);
            SetDestination(chosenPoint);
            nextDecisionTime = Time.time + decisionCooldown;
        }
    }

    void KeepMovingForwardIfIdle()
    {
        // ถ้าปลายทางใกล้มาก ให้ปักไปข้างหน้าต่อเนื่อง
        if (!agent.pathPending && agent.remainingDistance < Mathf.Max(lookAhead * 0.35f, 2f))
        {
            SetDestinationForward();
        }
    }

    void SetDestinationForward()
    {
        Vector3 dir = transform.forward;
        if (TrySampleAhead(dir, lookAhead, out Vector3 pt))
        {
            SetDestination(AdvancePastPoint(pt, dir, postPickAdvance));
        }
        else
        {
            // หาเล็กน้อยซ้าย/ขวา ถ้าตรงไม่เจอ
            Vector3 left = Quaternion.Euler(0f, -sideAngle * 0.6f, 0f) * dir;
            Vector3 right = Quaternion.Euler(0f, sideAngle * 0.6f, 0f) * dir;

            if (TrySampleAhead(left, lookAhead * 0.8f, out Vector3 lpt) && PathIsReachable(lpt))
                SetDestination(AdvancePastPoint(lpt, left, postPickAdvance));
            else if (TrySampleAhead(right, lookAhead * 0.8f, out Vector3 rpt) && PathIsReachable(rpt))
                SetDestination(AdvancePastPoint(rpt, right, postPickAdvance));
        }
    }

    void SetDestination(Vector3 worldPoint)
    {
        agent.SetDestination(worldPoint);
        if (drawDebug)
        {
            Debug.DrawLine(transform.position, worldPoint, Color.cyan, 0.1f);
        }
    }

    // ---------------------- Ray / Obstacle ----------------------

    void SenseObstaclesAndAdjustSpeed()
    {
        Vector3 origin = transform.position + Vector3.up * rayHeight;

        // Ray หน้าเพื่อชะลอ
        bool frontBlocked = Physics.Raycast(origin, transform.forward, out RaycastHit hit, stopDistance, obstacleMask);
        if (drawDebug)
        {
            Debug.DrawRay(origin, transform.forward * stopDistance, frontBlocked ? Color.red : Color.green, 0.02f);
        }

        // ถ้าตันมาก ลองหันเล็กน้อยซ้าย/ขวาตามที่โล่งกว่า (soft avoid)
        if (frontBlocked)
        {
            // ชะลอ
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, accel * Time.deltaTime);

            bool leftClear = !Physics.Raycast(origin, Quaternion.Euler(0f, -sideAngle, 0f) * transform.forward, sideProbeDistance, obstacleMask);
            bool rightClear = !Physics.Raycast(origin, Quaternion.Euler(0f, sideAngle, 0f) * transform.forward, sideProbeDistance, obstacleMask);

            if (leftClear ^ rightClear) // XOR: โล่งด้านเดียว
            {
                Vector3 sideDir = leftClear
                    ? Quaternion.Euler(0f, -sideAngle * 0.6f, 0f) * transform.forward
                    : Quaternion.Euler(0f, sideAngle * 0.6f, 0f) * transform.forward;

                if (TrySampleAhead(sideDir, lookAhead * 0.6f, out Vector3 sidePt) && PathIsReachable(sidePt))
                {
                    SetDestination(AdvancePastPoint(sidePt, sideDir, postPickAdvance * 0.6f));
                    nextDecisionTime = Time.time + 0.5f; // กันสั่น
                }
            }
        }
        else
        {
            // เร่งกลับเข้าความเร็วล่อง
            currentSpeed = Mathf.MoveTowards(currentSpeed, cruiseSpeed, accel * Time.deltaTime);
        }
    }

    // ---------------------- NavMesh helpers ----------------------

    bool TrySampleAhead(Vector3 dir, float distance, out Vector3 sampled)
    {
        Vector3 desired = transform.position + dir.normalized * distance;
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, sampleRadius, areaMask))
        {
            sampled = hit.position;
            if (drawDebug) Debug.DrawLine(transform.position, sampled, Color.yellow, 0.02f);
            return true;
        }
        sampled = Vector3.zero;
        return false;
    }

    bool PathIsReachable(Vector3 target)
    {
        NavMeshPath path = new NavMeshPath();
        if (agent.CalculatePath(target, path) && path.status == NavMeshPathStatus.PathComplete)
            return true;
        return false;
    }

    Vector3 AdvancePastPoint(Vector3 basePoint, Vector3 alongDir, float extra)
    {
        // ดันจุดเลยไปอีกนิด เพื่อให้ agent ไม่หยุดคาและวิ่งต่อเนื่อง
        Vector3 ahead = basePoint + alongDir.normalized * Mathf.Max(1f, extra);
        if (NavMesh.SamplePosition(ahead, out NavMeshHit hit, sampleRadius, areaMask))
            return hit.position;
        return basePoint;
    }

    void ApplySpeed()
    {
        agent.speed = currentSpeed;
        // หากปิด updateRotation เราจะหมุนเองด้านล่าง
    }

    void RotateToVelocity()
    {
        Vector3 v = agent.desiredVelocity.sqrMagnitude > 0.01f ? agent.desiredVelocity : agent.velocity;
        v.y = 0f;
        if (v.sqrMagnitude > 0.0001f)
        {
            Quaternion target = Quaternion.LookRotation(v, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, turnLerp * Time.deltaTime);
        }
    }

    // ---------------------- Gizmos ----------------------
    void OnDrawGizmosSelected()
    {
        if (!drawDebug) return;
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, sampleRadius);
    }
}