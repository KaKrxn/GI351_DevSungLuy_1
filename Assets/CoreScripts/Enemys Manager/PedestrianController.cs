using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PedestrianController : MonoBehaviour
{
    [Header("Roam Area (เลือกอย่างใดอย่างหนึ่ง)")]
    public BoxCollider roamBox;           // ถ้ามี จะสุ่มจุดในกรอบนี้
    public Transform roamCenter;          // ถ้าไม่ใช้ Box ให้ใช้ศูนย์กลาง + รัศมี
    public float roamRadius = 25f;

    [Header("Roam Settings")]
    public float minStopDistance = 0.2f;  // เหลือระยะเท่านี้ถือว่า "ถึง"
    public Vector2 idleTimeRange = new Vector2(0.5f, 2f);
    public Vector2 moveSpeedRange = new Vector2(1.2f, 2.2f); // สปีดสุ่มต่อคน
    public float destinationRetryRadius = 6f; // ไม่เจอ NavMesh ใกล้จุดสุ่ม ให้ขยายหาในรัศมีนี้
    public float repathInterval = 0.25f;     // เช็คปลายทางบ่อยแค่ไหน

    [Header("Avoid Player/Police (soft steer)")]
    public LayerMask avoidLayers;         // เช่น Player, Police
    public float avoidDistance = 3.0f;    // เข้าใกล้กว่านี้จะเบี่ยง
    public float avoidStrength = 2.0f;    // น้ำหนักการเบี่ยง

    [Header("Flee On Bump")]
    public float bumpFleeSpeed = 3.2f;    // สปีดช่วงหนี
    public float bumpFleeDuration = 1.2f; // เวลาหนี
    public float bumpImpulseThreshold = 1.5f; // แรงชนที่ถือว่าแรง (m/s)

    [Header("Physics Sync")]
    public float maxStep = 0.25f;         // จำกัดการย้ายต่อเฟรม (กันทะลุ)
    public float rotationLerp = 10f;      // หมุนหาทิศทาง

    [Header("Debug")]
    public bool drawGizmos = true;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private CapsuleCollider col;
    private Vector3 currentDestination;
    private bool hasDestination;
    private float nextRepathAt;
    private bool fleeing;
    private float fleeEndTime;
    private Vector3 fleeDir;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        // ตั้งค่าที่เหมาะกับการชนแบบเบา ๆ
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // ให้เราคุมการย้ายตำแหน่งเอง (เพื่อซิงก์กับ Rigidbody)
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.autoBraking = false;

        // สปีดสุ่มแต่ละคน
        agent.speed = Random.Range(moveSpeedRange.x, moveSpeedRange.y);
    }

    void OnEnable()
    {
        hasDestination = false;
        fleeing = false;
        nextRepathAt = 0f;
        StartCoroutine(RoamLoop());
    }

    IEnumerator RoamLoop()
    {
        var wait = new WaitForSeconds(repathInterval);
        while (enabled && gameObject.activeInHierarchy)
        {
            if (!hasDestination || ReachedDestination())
            {
                // idle เล็กน้อยให้ดูมีจังหวะ
                float idle = Random.Range(idleTimeRange.x, idleTimeRange.y);
                yield return new WaitForSeconds(idle);

                // เลือกจุดใหม่
                if (PickNewDestination(out currentDestination))
                {
                    agent.SetDestination(currentDestination);
                    hasDestination = true;
                }
                else
                {
                    hasDestination = false; // ลองใหม่ในรอบถัดไป
                }
            }
            yield return wait;
        }
    }

    void Update()
    {
        // ตรรกะหลบสิ่งที่ต้องหลบ (Player/Police) แบบนุ่มนวล
        if (!fleeing && hasDestination)
        {
            Vector3 steerOffset;
            if (TryComputeAvoidOffset(transform.position, out steerOffset))
            {
                var offsetTarget = agent.destination + steerOffset * avoidStrength;
                if (NavMesh.SamplePosition(offsetTarget, out var hit, destinationRetryRadius, agent.areaMask))
                {
                    agent.SetDestination(hit.position);
                }
            }
        }

        // โหมดหนีชั่วคราว
        if (fleeing)
        {
            if (Time.time >= fleeEndTime)
            {
                fleeing = false;
                agent.speed = Random.Range(moveSpeedRange.x, moveSpeedRange.y);
                hasDestination = false; // ให้สุ่มใหม่
            }
            else
            {
                // ดันปลายทางไปข้างหน้าตามทิศ flee
                Vector3 fleeTarget = transform.position + fleeDir * 6f;
                if (NavMesh.SamplePosition(fleeTarget, out var hit, destinationRetryRadius, agent.areaMask))
                    agent.SetDestination(hit.position);
            }
        }

        // ซิงก์ agent → Rigidbody/Transform
        SyncMovement();
    }

    void SyncMovement()
    {
        Vector3 next = agent.nextPosition;
        Vector3 delta = next - transform.position;

        if (delta.magnitude > maxStep)
            delta = delta.normalized * maxStep;

        rb.MovePosition(transform.position + delta);

        // หมุนตามทิศทางการเคลื่อนที่
        Vector3 vel = agent.desiredVelocity;
        if (vel.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(vel.normalized, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRot, rotationLerp * Time.deltaTime));
        }
    }

    bool ReachedDestination()
    {
        if (agent.pathPending) return false;
        if (agent.remainingDistance > Mathf.Max(agent.stoppingDistance, minStopDistance)) return false;
        return true;
    }

    bool PickNewDestination(out Vector3 pos)
    {
        pos = transform.position;

        // สุ่มใน Box
        if (roamBox != null)
        {
            Vector3 local = new Vector3(
                Random.Range(-0.5f, 0.5f) * roamBox.size.x,
                Random.Range(-0.5f, 0.5f) * roamBox.size.y,
                Random.Range(-0.5f, 0.5f) * roamBox.size.z
            );
            Vector3 world = roamBox.transform.TransformPoint(roamBox.center + local);
            if (NavMesh.SamplePosition(world, out var hit, destinationRetryRadius, agent.areaMask))
            {
                pos = hit.position;
                return true;
            }
            return false;
        }

        // หรือสุ่มในวงกลม
        Vector3 center = (roamCenter ? roamCenter.position : transform.position);
        for (int i = 0; i < 6; i++)
        {
            Vector2 r = Random.insideUnitCircle * roamRadius;
            Vector3 candidate = center + new Vector3(r.x, 0f, r.y);
            if (NavMesh.SamplePosition(candidate, out var hit, destinationRetryRadius, agent.areaMask))
            {
                pos = hit.position;
                return true;
            }
        }
        return false;
    }

    bool TryComputeAvoidOffset(Vector3 origin, out Vector3 offset)
    {
        offset = Vector3.zero;
        if (avoidLayers == 0 || avoidDistance <= 0f) return false;

        Collider[] hits = Physics.OverlapSphere(origin, avoidDistance, avoidLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        Vector3 sum = Vector3.zero; int count = 0;
        foreach (var h in hits)
        {
            Vector3 d = (transform.position - h.ClosestPoint(transform.position));
            d.y = 0f;
            float m = d.magnitude;
            if (m < 0.001f) continue;
            // น้ำหนักมากขึ้นเมื่อใกล้
            sum += d.normalized * (1f - Mathf.Clamp01(m / avoidDistance));
            count++;
        }

        if (count == 0) return false;
        offset = sum / count;
        return offset.sqrMagnitude > 0.0001f;
    }

    void OnCollisionEnter(Collision c)
    {
        // ถ้าชนกับผู้เล่น/ตำรวจ และความเร็วสัมพัทธ์สูง → หนีสั้น ๆ
        if (((1 << c.gameObject.layer) & avoidLayers) != 0)
        {
            float relSpeed = c.relativeVelocity.magnitude;
            if (relSpeed >= bumpImpulseThreshold)
            {
                fleeing = true;
                fleeEndTime = Time.time + bumpFleeDuration;

                Vector3 away = (transform.position - c.contacts[0].point);
                away.y = 0f;
                if (away.sqrMagnitude < 0.001f) away = transform.forward;
                fleeDir = away.normalized;

                agent.speed = bumpFleeSpeed;
                hasDestination = false; // ให้ระบบ Roam เลือกปลายทางใหม่หลังหนี
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = new Color(0, 1, 1, 0.3f);
        if (roamBox != null)
        {
            Matrix4x4 m = roamBox.transform.localToWorldMatrix;
            Gizmos.matrix = m;
            Gizmos.DrawWireCube(roamBox.center, roamBox.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else
        {
            Vector3 center = (roamCenter ? roamCenter.position : transform.position);
            UnityEditor.Handles.color = new Color(0, 1, 1, 0.5f);
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, roamRadius);
        }

        if (agent != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(agent.destination, 0.2f);
        }

        if (avoidDistance > 0)
        {
            UnityEditor.Handles.color = new Color(1, 0.5f, 0, 0.4f);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, avoidDistance);
        }
    }
#endif
}
