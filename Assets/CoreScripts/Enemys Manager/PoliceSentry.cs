//using UnityEngine;

//public class PoliceSentry : MonoBehaviour
//{
//    [Header("อ้างอิงจาก Spawner (ตั้งอัตโนมัติ)")]
//    public PoliceSentrySpawner Manager { get; set; }
//    public int SpawnIndex { get; set; }
//    public Transform Target { get; set; } // player

//    [Header("การตรวจจับผู้เล่น")]
//    public float detectRadius = 22f;
//    public float fieldOfView = 140f;
//    [Tooltip("เลเยอร์ที่บล็อกสายตา (ใส่เฉพาะกำแพง/ตึก)")]
//    public LayerMask lineOfSightBlockers;

//    [Header("Barbed Trap")]
//    public GameObject barbedTrapPrefab;
//    public Transform trapDropPoint;
//    public float trapDeployDelay = 0.3f;
//    public float trapLifetime = 10f;

//    [Header("Lifecycle")]
//    public float lifeTime = 20f;
//    public float afterTrapDespawnDelay = 1.25f;

//    public bool enableDebug = false;

//    float lifeTimer;
//    bool trapDeployed;

//    void OnEnable()
//    {
//        lifeTimer = 0f;
//        trapDeployed = false;
//    }

//    void Update()
//    {
//        lifeTimer += Time.deltaTime;

//        if (lifeTimer >= lifeTime && Manager != null)
//        {
//            if (enableDebug) Debug.Log($"[PoliceSentry] Lifetime end → Despawn (index {SpawnIndex})");
//            Manager.RequestDespawn(this);
//            return;
//        }

//        if (!Target) return;

//        if (!trapDeployed && IsTargetDetected())
//        {
//            if (enableDebug) Debug.Log("[PoliceSentry] Player detected → schedule trap");
//            trapDeployed = true;
//            Invoke(nameof(DeployTrapAndDespawn), trapDeployDelay);
//        }
//    }

//    bool IsTargetDetected()
//    {
//        Vector3 toTarget = (Target.position - transform.position);
//        float dist = toTarget.magnitude;
//        if (dist > detectRadius) return false;

//        Vector3 dir = toTarget.normalized;
//        float angle = Vector3.Angle(transform.forward, dir);
//        if (angle > fieldOfView * 0.5f) return false;

//        // Line of sight
//        if (Physics.Raycast(transform.position + Vector3.up * 1.6f, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
//        {
//            if (hit.collider && hit.collider.transform != Target &&
//               ((1 << hit.collider.gameObject.layer) & lineOfSightBlockers) != 0)
//            {
//                return false;
//            }
//        }
//        return true;
//    }

//    void DeployTrapAndDespawn()
//    {
//        if (barbedTrapPrefab)
//        {
//            Vector3 pos = trapDropPoint ? trapDropPoint.position : (transform.position + transform.forward * 1.5f);
//            Quaternion rot = trapDropPoint ? trapDropPoint.rotation : Quaternion.LookRotation(transform.forward, Vector3.up);

//            var trap = Instantiate(barbedTrapPrefab, pos, rot);
//            var trapComp = trap.GetComponent<PoliceBarbedTrap>();
//            if (trapComp)
//            {
//                trapComp.lifetime = trapLifetime;
//                trapComp.enableDebug = enableDebug;
//            }
//            else
//            {
//                Destroy(trap, trapLifetime);
//            }

//            if (enableDebug) Debug.Log("[PoliceSentry] Barbed trap deployed");
//        }
//        else if (enableDebug) Debug.LogWarning("[PoliceSentry] ไม่มี Prefab ของ Barbed Trap");

//        if (Manager != null)
//            Invoke(nameof(RequestDespawnSelf), afterTrapDespawnDelay);
//        else
//            Destroy(gameObject, afterTrapDespawnDelay);
//    }

//    void RequestDespawnSelf()
//    {
//        if (Manager != null)
//        {
//            if (enableDebug) Debug.Log($"[PoliceSentry] Despawn requested (index {SpawnIndex})");
//            Manager.RequestDespawn(this);
//        }
//        else Destroy(gameObject);
//    }

//    void OnDrawGizmosSelected()
//    {
//        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.35f);
//        Gizmos.DrawWireSphere(transform.position, detectRadius);

//        Vector3 fwd = transform.forward;
//        Quaternion left = Quaternion.AngleAxis(-fieldOfView * 0.5f, Vector3.up);
//        Quaternion right = Quaternion.AngleAxis(fieldOfView * 0.5f, Vector3.up);
//        Gizmos.color = new Color(1f, 0.3f, 0f, 0.6f);
//        Gizmos.DrawLine(transform.position, transform.position + (left * fwd) * detectRadius);
//        Gizmos.DrawLine(transform.position, transform.position + (right * fwd) * detectRadius);
//    }
//}
