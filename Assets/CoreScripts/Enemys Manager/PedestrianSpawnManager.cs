using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class PedestrianSpawnManager : MonoBehaviour
{
    [Header("Pedestrian Prefabs")]
    public GameObject[] pedestrianPrefabs;
    public int initialCount = 15;
    public float respawnInterval = 0f;

    [Header("Spawn Areas")]
    public BoxCollider[] spawnBoxes;       // <<< เปลี่ยนเป็น array
    public float sampleRadius = 8f;
    public int maxTriesPerSpawn = 10;

    [Header("Optional Variations")]
    public Vector2 randomScale = new Vector2(0.95f, 1.05f);
    public Material[] randomMaterials;

    [Header("NavMesh")]
    public bool snapToNavMesh = true;

    private List<GameObject> pool = new List<GameObject>();

    void Start()
    {
        for (int i = 0; i < initialCount; i++) TrySpawnOne();

        if (respawnInterval > 0f)
            InvokeRepeating(nameof(TrySpawnOne), respawnInterval, respawnInterval);
    }

    void TrySpawnOne()
    {
        if (pedestrianPrefabs == null || pedestrianPrefabs.Length == 0) return;

        GameObject prefab = pedestrianPrefabs[Random.Range(0, pedestrianPrefabs.Length)];
        Vector3 pos; Quaternion rot;
        if (!PickSpawnTransform(out pos, out rot)) return;

        var go = Instantiate(prefab, pos, rot);
        pool.Add(go);

        // variation
        float s = Random.Range(randomScale.x, randomScale.y);
        go.transform.localScale *= s;

        if (randomMaterials != null && randomMaterials.Length > 0)
        {
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null) smr.material = randomMaterials[Random.Range(0, randomMaterials.Length)];
        }
    }

    bool PickSpawnTransform(out Vector3 pos, out Quaternion rot)
    {
        pos = transform.position;
        rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // เลือก spawnBox แบบสุ่มจาก array
        BoxCollider chosenBox = null;
        if (spawnBoxes != null && spawnBoxes.Length > 0)
        {
            chosenBox = spawnBoxes[Random.Range(0, spawnBoxes.Length)];
        }

        for (int i = 0; i < maxTriesPerSpawn; i++)
        {
            Vector3 candidate;
            if (chosenBox != null)
            {
                Vector3 local = new Vector3(
                    Random.Range(-0.5f, 0.5f) * chosenBox.size.x,
                    Random.Range(-0.5f, 0.5f) * chosenBox.size.y,
                    Random.Range(-0.5f, 0.5f) * chosenBox.size.z
                );
                candidate = chosenBox.transform.TransformPoint(chosenBox.center + local);
            }
            else
            {
                candidate = transform.position + Random.insideUnitSphere * 10f;
                candidate.y = transform.position.y;
            }

            if (!snapToNavMesh)
            {
                pos = candidate;
                return true;
            }

            if (NavMesh.SamplePosition(candidate, out var hit, sampleRadius, NavMesh.AllAreas))
            {
                pos = hit.position;
                return true;
            }
        }
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (spawnBoxes == null) return;
        Gizmos.color = new Color(0, 1, 0, 0.25f);
        foreach (var box in spawnBoxes)
        {
            if (box == null) continue;
            Matrix4x4 m = box.transform.localToWorldMatrix;
            Gizmos.matrix = m;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
