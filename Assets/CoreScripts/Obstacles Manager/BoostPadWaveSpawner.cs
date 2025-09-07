// BoostPadWaveSpawner.cs
// สุ่มวาง BoostPad จากรายชื่อจุด (Transform[]) ครั้งละ N ชิ้น (3–5) อยู่ LifeTime วินาที
// จากนั้นลบทั้งหมด แล้วสุ่มใหม่เป็นรอบๆ ไปเรื่อยๆ
//
// ใช้ได้กับ SpeedBoostPad.cs หรือ SpeedBoostPadKmh.cs ที่คุณมีอยู่แล้ว

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class BoostPadWaveSpawner : MonoBehaviour
{
    [Header("Spawn Points (ตำแหน่งให้สุ่ม)")]
    [Tooltip("ใส่จุดในแมพที่อนุญาตให้เกิด BoostPad ได้")]
    public Transform[] points;

    [Header("Prefabs")]
    [Tooltip("พรีแฟบ BoostPad หลัก (ถ้าไม่ได้ใส่ใน list ข้างล่าง)")]
    public GameObject padPrefab;
    [Tooltip("ถ้าใส่หลายพรีแฟบ จะสุ่มชนิดให้แต่ละจุด")]
    public List<GameObject> padPrefabs = new List<GameObject>();

    [Header("Wave Settings")]
    [Tooltip("จำนวนต่ำสุด/สูงสุดของ BoostPad ต่อหนึ่งรอบ")]
    public int minCount = 3;
    public int maxCount = 5;

    [Tooltip("อายุการอยู่ของ BoostPad ในหนึ่งรอบ (วินาที)")]
    public float lifeTime = 20f;

    [Tooltip("หน่วงเวลาก่อนเริ่มรอบถัดไป (วินาที)")]
    public float respawnGap = 2f;

    [Tooltip("ไม่ใช้จุดซ้ำกับรอบก่อนทันที (ถ้ามีจุดพอ)")]
    public bool avoidImmediateRepeat = true;

    [Tooltip("ใช้เวลาจริง (ไม่โดน timeScale)")]
    public bool useRealtimeTimer = false;

    [Header("Placement Options")]
    [Tooltip("ให้หมุน Y แบบสุ่มบ้างไหม")]
    public bool randomYaw = false;
    [Tooltip("ตำแหน่งชดเชยท้องถิ่นจากจุด (เช่นยกขึ้นเล็กน้อย)")]
    public Vector3 localOffset = Vector3.zero;
    [Tooltip("ใส่พาเรนต์ของอินสแตนซ์ (เว้นว่าง = ใต้ GameObject นี้)")]
    public Transform instancesParent;

    [Header("Events")]
    public UnityEvent onWaveSpawned;
    public UnityEvent onWaveCleared;

    // ---- runtime ----
    readonly List<GameObject> activePads = new List<GameObject>();
    HashSet<int> lastUsedIndices = new HashSet<int>();
    Coroutine loopCo;

    void Start()
    {
        if (!instancesParent) instancesParent = transform;
        StartLoop();
    }

    public void StartLoop()
    {
        if (loopCo != null) StopCoroutine(loopCo);
        loopCo = StartCoroutine(Loop());
    }

    public void StopLoop()
    {
        if (loopCo != null) { StopCoroutine(loopCo); loopCo = null; }
        ClearAll();
    }

    IEnumerator Loop()
    {
        // วนไปเรื่อย ๆ
        while (enabled)
        {
            SpawnWave();
            onWaveSpawned?.Invoke();

            if (useRealtimeTimer) yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, lifeTime));
            else yield return new WaitForSeconds(Mathf.Max(0.01f, lifeTime));

            ClearAll();
            onWaveCleared?.Invoke();

            if (respawnGap > 0f)
            {
                if (useRealtimeTimer) yield return new WaitForSecondsRealtime(respawnGap);
                else yield return new WaitForSeconds(respawnGap);
            }
        }
    }

    void SpawnWave()
    {
        ClearAll(); // กันกรณีมีค้าง

        if (points == null || points.Length == 0)
        {
            Debug.LogWarning("[BoostPadWaveSpawner] No points assigned.");
            return;
        }

        int want = Mathf.Clamp(Random.Range(minCount, maxCount + 1), 0, points.Length);

        // เตรียมรายชื่อ index ผู้สมัคร
        List<int> candidates = new List<int>(points.Length);
        for (int i = 0; i < points.Length; i++) candidates.Add(i);

        // ตัด index ที่ใช้ล่าสุดออกชั่วคราว ถ้าจำนวนจุดยังพอ
        if (avoidImmediateRepeat && lastUsedIndices.Count > 0 && candidates.Count - lastUsedIndices.Count >= want)
        {
            candidates.RemoveAll(idx => lastUsedIndices.Contains(idx));
        }

        // สุ่มไม่ซ้ำ k ตัวจาก candidates
        Shuffle(candidates);
        List<int> chosen = new List<int>(want);
        for (int i = 0; i < want && i < candidates.Count; i++) chosen.Add(candidates[i]);

        // จำไว้ว่าใช้จุดไหนในรอบนี้
        lastUsedIndices = new HashSet<int>(chosen);

        // สร้างอินสแตนซ์
        foreach (int idx in chosen)
        {
            Transform p = points[idx];
            if (!p) continue;

            GameObject prefab = PickPrefab();
            if (!prefab)
            {
                Debug.LogWarning("[BoostPadWaveSpawner] padPrefab is missing.");
                break;
            }

            Vector3 pos = p.TransformPoint(localOffset);
            Quaternion rot = p.rotation;
            if (randomYaw) rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            GameObject go = Instantiate(prefab, pos, rot, instancesParent);

            // ป้องกันลืมตั้ง Trigger ในพรีแฟบ
            var col = go.GetComponent<Collider>();
            if (!col) col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;

            activePads.Add(go);
        }
    }

    void ClearAll()
    {
        for (int i = activePads.Count - 1; i >= 0; i--)
        {
            if (activePads[i]) Destroy(activePads[i]);
        }
        activePads.Clear();
    }

    GameObject PickPrefab()
    {
        if (padPrefabs != null && padPrefabs.Count > 0)
        {
            // เลือกจากลิสต์แบบสุ่ม (ข้าม null)
            for (int guard = 0; guard < 8; guard++)
            {
                var pf = padPrefabs[Random.Range(0, padPrefabs.Count)];
                if (pf) return pf;
            }
        }
        return padPrefab;
    }

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        minCount = Mathf.Max(0, minCount);
        maxCount = Mathf.Max(minCount, maxCount);
        lifeTime = Mathf.Max(0.01f, lifeTime);
        respawnGap = Mathf.Max(0f, respawnGap);
    }

    void OnDrawGizmosSelected()
    {
        if (points == null) return;
        Gizmos.color = Color.cyan;
        foreach (var p in points)
        {
            if (!p) continue;
            Vector3 pos = p.TransformPoint(localOffset);
            Gizmos.DrawWireSphere(pos, 0.6f);
            Gizmos.DrawLine(p.position, pos);
        }
    }
#endif
}
