//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class PoliceSentrySpawner : MonoBehaviour
//{
//    [Header("Spawn Points (วาง Transform ข้างถนน)")]
//    public Transform[] spawnPoints;

//    [Header("Prefab & จำนวน")]
//    public GameObject policeSentryPrefab;
//    [Range(1, 100)] public int initialCount = 5;

//    [Header("Respawn Control")]
//    public float respawnDelay = 3f;

//    [Header("Player Ref (เว้นว่างจะหา tag=Player)")]
//    public Transform player;

//    public bool enableDebug = false;

//    HashSet<int> occupiedIndices = new HashSet<int>();
//    readonly List<PoliceSentry> activeSentries = new List<PoliceSentry>();

//    void Start()
//    {
//        if (!player)
//        {
//            var p = GameObject.FindGameObjectWithTag("Player");
//            if (p) player = p.transform;
//        }

//        if (spawnPoints == null || spawnPoints.Length == 0)
//        {
//            Debug.LogWarning("[PoliceSentrySpawner] ไม่มี SpawnPoints!");
//            return;
//        }

//        int count = Mathf.Min(initialCount, spawnPoints.Length);
//        for (int i = 0; i < count; i++)
//            TrySpawnOne();
//    }

//    void TrySpawnOne()
//    {
//        if (occupiedIndices.Count >= spawnPoints.Length) return;

//        int attempts = 0;
//        int idx = -1;
//        while (attempts < 64)
//        {
//            int candidate = Random.Range(0, spawnPoints.Length);
//            if (!occupiedIndices.Contains(candidate))
//            {
//                idx = candidate; break;
//            }
//            attempts++;
//        }
//        if (idx < 0) return;

//        Transform point = spawnPoints[idx];
//        var go = Instantiate(policeSentryPrefab, point.position, point.rotation);
//        var sentry = go.GetComponent<PoliceSentry>();
//        if (!sentry)
//        {
//            Debug.LogError("[PoliceSentrySpawner] Prefab ขาด PoliceSentry.cs");
//            Destroy(go);
//            return;
//        }

//        sentry.Manager = this;
//        sentry.SpawnIndex = idx;
//        sentry.Target = player;

//        occupiedIndices.Add(idx);
//        activeSentries.Add(sentry);

//        if (enableDebug) Debug.Log($"[Spawner] Spawn at index {idx} ({point.name})");
//    }

//    public void RequestDespawn(PoliceSentry sentry)
//    {
//        if (!sentry) return;

//        if (occupiedIndices.Contains(sentry.SpawnIndex))
//            occupiedIndices.Remove(sentry.SpawnIndex);

//        if (activeSentries.Contains(sentry))
//            activeSentries.Remove(sentry);

//        Destroy(sentry.gameObject);

//        StartCoroutine(RespawnAfterDelay());
//    }

//    IEnumerator RespawnAfterDelay()
//    {
//        yield return new WaitForSeconds(respawnDelay);
//        TrySpawnOne();
//    }

//    public void DespawnAll()
//    {
//        foreach (var s in new List<PoliceSentry>(activeSentries))
//        {
//            if (s) Destroy(s.gameObject);
//        }
//        activeSentries.Clear();
//        occupiedIndices.Clear();
//    }
//}
