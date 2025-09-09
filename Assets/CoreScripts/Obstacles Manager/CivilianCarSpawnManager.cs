using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class CivilianCarSpawnManager : MonoBehaviour
{
    [Header("Car Prefabs")]
    public GameObject[] carPrefabs;
    public int initialCount = 10;
    public float respawnInterval = 0f;

    [Header("Lane Sets")]
    [Tooltip("ใส่ Root ของเลนหลายชุด แต่ละ Root มีลูกเป็นจุดเลน")]
    public Transform[] laneRoots;

    [Header("Spawn Settings")]
    public float navmeshSampleRadius = 6f;
    public float minSpawnSeparation = 3.5f; // กันเกิดทับคันอื่น/ผู้เล่น
    public LayerMask avoidOverlapLayers;    // รถ/ผู้เล่น/สิ่งขวาง

    [Header("NavMesh")]
    public bool lockToRoadArea = false;
    public string roadAreaName = "Road";

    readonly List<GameObject> alive = new List<GameObject>();

    void Start()
    {
        for (int i = 0; i < initialCount; i++)
            TrySpawnOne();

        if (respawnInterval > 0f)
            InvokeRepeating(nameof(TrySpawnOne), respawnInterval, respawnInterval);
    }

    void TrySpawnOne()
    {
        if (carPrefabs == null || carPrefabs.Length == 0) return;
        if (laneRoots == null || laneRoots.Length == 0) return;

        GameObject prefab = carPrefabs[Random.Range(0, carPrefabs.Length)];
        Transform root = laneRoots[Random.Range(0, laneRoots.Length)];
        if (!root || root.childCount == 0) return;

        // สร้างอาเรย์ lanePoints จากลูก ๆ ของ root
        var lane = new Transform[root.childCount];
        for (int i = 0; i < root.childCount; i++) lane[i] = root.GetChild(i);

        // ตำแหน่งเกิด: เลือกจากจุดแรกของเลน หรือตามจุดใด ๆ ก็ได้
        Transform anchor = lane[0] ? lane[0] : root;
        Vector3 spawnPos = anchor.position;
        Quaternion spawnRot = Quaternion.LookRotation((lane[Mathf.Min(1, lane.Length - 1)].position - anchor.position).normalized, Vector3.up);

        // ดูดลง NavMesh
        int mask = NavMesh.AllAreas;
        if (lockToRoadArea)
        {
            int road = NavMesh.GetAreaFromName(roadAreaName);
            if (road >= 0) mask = 1 << road;
        }
        if (NavMesh.SamplePosition(spawnPos, out var hit, navmeshSampleRadius, mask))
            spawnPos = hit.position;

        // กันเกิดทับสิ่งอื่น
        if (avoidOverlapLayers.value != 0)
        {
            if (Physics.CheckSphere(spawnPos, minSpawnSeparation, avoidOverlapLayers, QueryTriggerInteraction.Ignore))
                return;
        }

        // สร้าง
        var go = Instantiate(prefab, spawnPos, spawnRot);
        alive.Add(go);

        // ใส่ lanePoints ให้รถ
        var car = go.GetComponent<CivilianCarController>();
        if (!car)
        {
            Debug.LogWarning("[CivilianCarSpawnManager] Prefab ไม่มี CivilianCarController");
            return;
        }
        car.lanePoints = lane;

        // ตั้งค่า AreaMask ของ agent ให้ตรงกับถนน (ถ้าเลือก)
        if (lockToRoadArea)
        {
            var agent = go.GetComponent<NavMeshAgent>();
            int road = NavMesh.GetAreaFromName(roadAreaName);
            if (agent && road >= 0) agent.areaMask = 1 << road;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (laneRoots == null) return;
        Gizmos.color = Color.cyan;
        foreach (var r in laneRoots)
        {
            if (!r) continue;
            for (int i = 0; i < r.childCount; i++)
            {
                var t = r.GetChild(i);
                Vector3 p = t.position + Vector3.up * 0.2f;
                Gizmos.DrawSphere(p, 0.2f);
                if (i + 1 < r.childCount)
                    Gizmos.DrawLine(p, r.GetChild(i + 1).position + Vector3.up * 0.2f);
            }
        }
    }
#endif
}
