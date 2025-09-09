using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CivilianCarController : MonoBehaviour
{
    [Header("Lane / Waypoints")]
    [Tooltip("จุดเลน (วางตามกลางเลน) เรียงลำดับไปข้างหน้า")]
    public Transform[] lanePoints;
    public bool loop = true;
    public float reachRadius = 2.0f; // ถึงจุดแล้วจะไปจุดถัดไป

    [Header("Speed / Accel")]
    public Vector2 cruiseSpeedRange = new Vector2(7f, 12f); // สปีดสุ่มต่อคัน
    public float accel = 6f;           // m/s^2
    public float brake = 10f;          // m/s^2
    public float turnSpeed = 120f;     // NavMeshAgent.angularSpeed

    [Header("Following (คันหน้า)")]
    public LayerMask vehicleLayers;    // ใส่เลเยอร์ของรถ/สิ่งกีดขวางบนถนน
    public float lookAhead = 8f;       // มองหน้ารถระยะนี้
    public float followDistance = 4f;  // รักษาระยะขั้นต่ำ
    public float stopBuffer = 1.5f;    // เบรกเผื่อ

    [Header("Traffic Light (ถ้ามี)")]
    [Tooltip("เลเยอร์หรือ Collider ที่ใช้ระบุเส้น stop line ของไฟแดง")]
    public LayerMask trafficLightLayers;
    public float trafficCheckAhead = 6f;
    public string trafficLightTag = "TrafficLight"; // กรณีใช้ Tag กับคอมโพเนนต์ TrafficLight

    [Header("NavMesh Area")]
    public bool lockToRoadArea = false; // ตั้ง AreaMask เฉพาะ Road
    public string roadAreaName = "Road";

    [Header("Perf")]
    public float decisionInterval = 0.06f; // tick ทุก ~60ms
    float nextTick;

    [Header("Debug")]
    public bool drawGizmos = true;

    // ---- runtime ----
    NavMeshAgent agent;
    int laneIndex = -1;
    float targetCruiseSpeed; // ความเร็วเป้าหมายของคันนี้
    float currentSpeed;      // ความเร็วปัจจุบัน (จำลองเร่ง/เบรก)
    bool hardStop;           // ถูกสั่งหยุด (มีรถ/ไฟแดงขวาง)

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        agent.updateRotation = true;
        agent.updatePosition = true;
        agent.autoBraking = false;
        agent.angularSpeed = turnSpeed;
        agent.acceleration = 1000f; // เราจำลอง accel เอง

        if (lockToRoadArea)
        {
            int road = NavMesh.GetAreaFromName(roadAreaName);
            if (road >= 0) agent.areaMask = 1 << road;
        }
    }

    void Start()
    {
        targetCruiseSpeed = Random.Range(cruiseSpeedRange.x, cruiseSpeedRange.y);
        currentSpeed = targetCruiseSpeed * 0.5f;

        if (lanePoints != null && lanePoints.Length > 0)
        {
            laneIndex = FindClosestIndex(transform.position);
            SetDestination(laneIndex);
        }
    }

    void Update()
    {
        if (Time.time < nextTick) return;
        nextTick = Time.time + decisionInterval;

        if (lanePoints == null || lanePoints.Length == 0) return;

        // ไปจุดถัดไปเมื่อถึงระยะ
        if (!agent.pathPending && agent.remainingDistance <= reachRadius)
        {
            laneIndex = NextIndex(laneIndex, +1);
            SetDestination(laneIndex);
        }

        // ตรวจหน้ารถ (คันหน้า/สิ่งกีดขวาง/ไฟแดง)
        hardStop = CheckFrontBlockers();

        // ปรับความเร็วอย่างนุ่มนวล
        float desired = hardStop ? 0f : targetCruiseSpeed;
        float a = (desired > currentSpeed) ? accel : brake;
        currentSpeed = Mathf.MoveTowards(currentSpeed, desired, a * decisionInterval);

        agent.speed = currentSpeed;
    }

    // ---------- Sensors ----------
    bool CheckFrontBlockers()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 dir = transform.forward;

        // A) คันหน้า/สิ่งกีดขวางบนถนน
        bool blocker = false;
        if (vehicleLayers.value != 0)
        {
            if (Physics.SphereCast(origin, 0.6f, dir, out var hit, lookAhead, vehicleLayers, QueryTriggerInteraction.Ignore))
            {
                float d = hit.distance;
                if (d <= followDistance + stopBuffer)
                    blocker = true;
            }
        }
        /*
        // B) ไฟจราจร (หยุดถ้าแดง)
        if (!blocker && trafficLightLayers.value != 0)
        {
            if (Physics.Raycast(origin, dir, out var hit2, trafficCheckAhead, trafficLightLayers, QueryTriggerInteraction.Ignore))
            {
                // ถ้าใช้สคริปต์ TrafficLight (มี property IsRed) ให้เช็คแบบนี้
                var tl = hit2.collider.GetComponentInParent<TrafficLight>();
                if (!tl && hit2.collider.CompareTag(trafficLightTag))
                    tl = hit2.collider.GetComponent<TrafficLight>();

                if (tl != null && tl.IsRedNow())
                    blocker = true;
            }
        }
        */
        return blocker;
    }

    // ---------- Lane helpers ----------
    int FindClosestIndex(Vector3 pos)
    {
        int best = -1; float bestDist = Mathf.Infinity;
        for (int i = 0; i < lanePoints.Length; i++)
        {
            var t = lanePoints[i]; if (!t) continue;
            float d = (t.position - pos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return (best >= 0) ? best : 0;
    }

    int NextIndex(int i, int dir)
    {
        if (lanePoints.Length == 0) return 0;
        int n = i + dir;
        if (loop)
        {
            int len = lanePoints.Length;
            n = (n % len + len) % len;
        }
        else
        {
            n = Mathf.Clamp(n, 0, lanePoints.Length - 1);
        }
        return n;
    }

    void SetDestination(int i)
    {
        if (i < 0 || i >= lanePoints.Length) return;
        var t = lanePoints[i];
        if (!t) return;

        // ปรับปลายทางให้เลยไปข้างหน้าเล็กน้อยเพื่อลดการหยุดคา waypoint
        Vector3 dest = t.position;
        dest += (i < lanePoints.Length - 1 ? (lanePoints[NextIndex(i, +1)].position - t.position) : transform.forward).normalized * 0.2f;

        agent.SetDestination(dest);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        if (lanePoints != null && lanePoints.Length > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < lanePoints.Length; i++)
            {
                if (!lanePoints[i]) continue;
                Vector3 p = lanePoints[i].position + Vector3.up * 0.15f;
                Gizmos.DrawCube(p, Vector3.one * 0.25f);
                int n = (i + 1);
                if (loop) n %= lanePoints.Length;
                if (n < lanePoints.Length && lanePoints[n])
                    Gizmos.DrawLine(p, lanePoints[n].position + Vector3.up * 0.15f);
            }
        }

        // เซนเซอร์หน้า
        Gizmos.color = new Color(1, 0.5f, 0, 0.8f);
        Vector3 o = transform.position + Vector3.up * 0.5f;
        Gizmos.DrawLine(o, o + transform.forward * lookAhead);
        UnityEditor.Handles.color = new Color(1, 0.5f, 0, 0.25f);
        UnityEditor.Handles.DrawWireDisc(o + transform.forward * followDistance, Vector3.up, 0.6f);
    }
#endif
}

