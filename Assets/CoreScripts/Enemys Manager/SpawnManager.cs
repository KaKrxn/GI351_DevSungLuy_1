using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SpawnManager : MonoBehaviour
{
    [Header("Prefabs & Player")]
    public GameObject policePrefab;      // Prefab ที่มี PoliceController + NavMeshAgent
    public Transform target;             // Player (ถ้าไม่เซ็ต ตำรวจจะหา tag จากใน PoliceController เอง)

    [Header("Spawn Points")]
    public Transform[] spawnPoints;      // จุดเกิดตำรวจ
    public int startPoliceCount = 2;     // จำนวนเริ่มต้น
    public float spawnInterval = 30f;    // เวลาระหว่างเกิด (วินาที)

    [Header("Patrol Points (ทางเลือก)")]
    public Transform[] patrolPoints;     // ถ้าอยากให้มีวง patrol แบบ waypoint

    [Header("Lane Path (3 โหมดให้เลือกอย่างใดอย่างหนึ่ง)")]
    public Transform lanePathRoot;       // โหมด A: วางลูก ๆ ใต้ root นี้เป็นลำดับเลน
    public Transform[] lanePathRoots;    // โหมด B: หลาย root (สุ่มเลือก 1 ชุดตอนสปอว์น)
    public Transform[] lanePathPoints;   // โหมด C: ใส่จุดเลนเป็นอาเรย์ตรง ๆ
    public bool laneLoop = true;         // วนลูปเลนหรือไม่

    [Header("NavMesh Safety")]
    public bool snapSpawnToNavMesh = true;   // ดูดตำแหน่งเกิดให้ลงบน NavMesh ใกล้ ๆ
    public float navmeshSampleRadius = 12f;  // รัศมีที่ใช้หาจุด NavMesh ใกล้สุด
    public bool lockToRoadArea = false;      // ให้ Sample เฉพาะ Area "Road" (ถ้ากำหนดไว้ใน Navigation Area)

    [Header("Heat Control (ถ้าอยากตั้งจากนี่)")]
    public bool applyHeatFromHere = false;
    [Range(1, 5)] public int startHeat = 1;

    // ============ เพิ่มมาใหม่: สำหรับลูกศรชี้ตำรวจ ============
    [Header("UI Arrow (Optional)")]
    public ArrowPointer_Offscreen arrowPointer;  // ลูกศรชี้ตำรวจใน Canvas (ไม่ตั้งจะค้นหาให้)

    // ===== RUNTIME =====
    private List<Transform[]> laneSets;  // เก็บชุดเลนจากหลาย root

    void Start()
    {
        // เตรียม laneSets จากโหมด B (หลาย root)
        laneSets = new List<Transform[]>();
        if (lanePathRoots != null && lanePathRoots.Length > 0)
        {
            foreach (var root in lanePathRoots)
            {
                if (!root) continue;
                int c = root.childCount;
                var arr = new Transform[c];
                for (int i = 0; i < c; i++) arr[i] = root.GetChild(i);
                laneSets.Add(arr);
            }
        }

        // สปอว์นเริ่มต้น
        for (int i = 0; i < Mathf.Max(0, startPoliceCount); i++)
            SpawnPolice();

        // ลูปสปอว์นเพิ่มตามเวลา
        if (spawnInterval > 0f) StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            SpawnPolice();
        }
    }

    void SpawnPolice()
    {
        if (!policePrefab)
        {
            Debug.LogWarning("[SpawnManager] กรุณาตั้งค่า policePrefab");
            return;
        }
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[SpawnManager] กรุณาตั้งค่า spawnPoints");
            return;
        }

        // เลือกจุดเกิดแบบสุ่ม
        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (!sp) return;

        // หาตำแหน่งบน NavMesh ใกล้ ๆ (ถ้าเปิด)
        Vector3 spawnPos = sp.position;
        if (snapSpawnToNavMesh)
        {
            int mask = GetSampleMask();
            if (!TryFindNavMeshPosition(sp.position, navmeshSampleRadius, out spawnPos, mask))
            {
                // ลองไล่จุดอื่น ๆ
                bool found = false;
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    var sp2 = spawnPoints[i];
                    if (!sp2) continue;
                    if (TryFindNavMeshPosition(sp2.position, navmeshSampleRadius, out spawnPos, mask))
                    {
                        found = true;
                        sp = sp2;
                        break;
                    }
                }
                if (!found)
                {
                    Debug.LogWarning("[SpawnManager] Sample NavMesh ใกล้จุดเกิดไม่เจอ (เช็กการ Bake/AreaMask)");
                    return;
                }
            }
        }

        // สร้างตัวตำรวจ
        GameObject go = Instantiate(policePrefab, spawnPos, sp.rotation);

        // ตั้งค่า controller
        var pc = go.GetComponent<PoliceController>();
        if (!pc)
        {
            Debug.LogError("[SpawnManager] Prefab ไม่มี PoliceController");
            return;
        }

        // ส่ง Player
        if (target) pc.target = target;

        // ส่ง Patrol points (ถ้ามี)
        if (patrolPoints != null && patrolPoints.Length > 0)
            pc.SetPatrolPoints(patrolPoints);

        // ส่ง Lane path points (เลือกโหมดให้เหมาะ)
        var lanes = ResolveLanePointsForThisSpawn();
        if (lanes != null && lanes.Length > 0)
        {
            pc.laneLoop = laneLoop;
            pc.SetLanePathPoints(lanes);
        }

        // ตั้ง Heat จากนี่ (ถ้าเลือกใช้)
        if (applyHeatFromHere) pc.ApplyHeat(startHeat);

        // ============ เพิ่มมาใหม่: ลงทะเบียนให้ลูกศรรู้จักตำรวจคันนี้ทันที ============
        var arrowRef = arrowPointer ? arrowPointer : FindObjectOfType<ArrowPointer_Offscreen>();
        if (arrowRef)
        {
            // บอกลูกศรว่ามีคันนี้เกิดแล้ว และติดฮุคไว้จัดการ unregister ตอนปิด/ทำลาย
            arrowRef.RegisterCandidate(go.transform);
            var hook = go.GetComponent<ArrowCandidateHook>();
            if (!hook) hook = go.AddComponent<ArrowCandidateHook>();
            hook.Bind(arrowRef, go.transform);
        }
    }

    // -------- Helpers --------

    Transform[] ResolveLanePointsForThisSpawn()
    {
        // โหมด B: ถ้ามีหลาย root → สุ่มเลือกชุด
        if (laneSets != null && laneSets.Count > 0)
        {
            var chosen = laneSets[Random.Range(0, laneSets.Count)];
            return chosen;
        }

        // โหมด A/C: ใช้ lanePathPoints ที่เตรียมไว้
        if (lanePathPoints != null && lanePathPoints.Length > 0)
            return lanePathPoints;

        // ไม่มีเลน → ให้ null (ตำรวจจะใช้ patrol หรือไล่ตรง)
        return null;
    }

    // หาจุดบน NavMesh รอบ ๆ origin
    bool TryFindNavMeshPosition(Vector3 origin, float radius, out Vector3 hitPos, int areaMask)
    {
        if (NavMesh.SamplePosition(origin, out NavMeshHit hit, radius, areaMask))
        {
            hitPos = hit.position;
            return true;
        }
        hitPos = origin;
        return false;
    }

    // ใช้ AreaMask ให้ตรงกับดีไซน์ (เช่น ถ้าล็อกให้เดินเฉพาะ "Road")
    int GetSampleMask()
    {
        if (!lockToRoadArea) return NavMesh.AllAreas;

        int road = NavMesh.GetAreaFromName("Road");
        if (road >= 0) return 1 << road;

        Debug.LogWarning("[SpawnManager] lockToRoadArea=true แต่ไม่พบ Area 'Road' ใน Navigation; จะใช้ AllAreas แทน");
        return NavMesh.AllAreas;
    }
}

// ============ เพิ่มมาใหม่: ตัวช่วยจัดการวงจรชีวิตของเป้าหมายบนลูกศร ============
public class ArrowCandidateHook : MonoBehaviour
{
    ArrowPointer_Offscreen arrow;
    Transform target;
    bool registered;

    public void Bind(ArrowPointer_Offscreen a, Transform t)
    {
        arrow = a;
        target = t;
        // หากอินสแตนซ์ถูกสร้างแล้วและ enable อยู่ จะ re-register ใน OnEnable
    }

    void OnEnable()
    {
        if (arrow && target && !registered)
        {
            arrow.RegisterCandidate(target);
            registered = true;
        }
    }

    void OnDisable()
    {
        if (registered && arrow && target)
            arrow.UnregisterCandidate(target);
        registered = false;
    }

    void OnDestroy()
    {
        if (arrow && target)
            arrow.UnregisterCandidate(target);
    }
}
