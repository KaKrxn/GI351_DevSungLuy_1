// DeliveryPointManager.cs — now auto-updates ArrowPointer target on every spawn
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class DeliveryPointManager : MonoBehaviour
{
    [Header("Candidate points in scene")]
    public Transform[] points;

    [Header("Marker prefab (must have a Collider; no Rigidbody needed)")]
    public GameObject markerPrefab;

    [Header("Settings")]
    public string playerTag = "Player";
    public bool avoidImmediateRepeat = true;
    public float respawnDelay = 0.0f;   

    [Header("Events")]
    public UnityEvent onDelivered;

    [Header("UI Arrow (optional)")]
    [Tooltip("ลาก ArrowPointer จาก HUD มาใส่ ถ้าเว้นว่าง ระบบจะไม่อัปเดตลูกศร")]
    public ArrowPointer arrow;                 
    public bool hideArrowWhileRespawning = false;

    GameObject currentMarker;
    int lastIndex = -1;
    public int deliveredCount { get; private set; }

    void Start() => SpawnNext();

    public void SpawnNext()
    {
        if (points == null || points.Length == 0)
        {
            Debug.LogWarning("[Delivery] No points assigned.");
            return;
        }

        int idx = Random.Range(0, points.Length);
        if (avoidImmediateRepeat && points.Length > 1)
        {
            int guard = 0;
            while (idx == lastIndex && guard++ < 16)
                idx = Random.Range(0, points.Length);
        }
        lastIndex = idx;

        
        if (currentMarker) Destroy(currentMarker);

        
        Vector3 pos = points[idx].position;
        Quaternion rot = points[idx].rotation;

        currentMarker = markerPrefab
            ? Instantiate(markerPrefab, pos, rot)
            : GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        if (!markerPrefab)  
        {
            currentMarker.transform.SetPositionAndRotation(pos, rot);
            currentMarker.transform.localScale = new Vector3(2.5f, 0.2f, 2.5f);
            var r = currentMarker.GetComponent<Renderer>();
            if (r) r.material.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        }

        
        var col = currentMarker.GetComponent<Collider>();
        if (!col) col = currentMarker.AddComponent<BoxCollider>();
        col.isTrigger = true;

        
        var rb = currentMarker.GetComponent<Rigidbody>();
        if (!rb) rb = currentMarker.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        
        var trig = currentMarker.GetComponent<DeliveryTrigger>();
        if (!trig) trig = currentMarker.AddComponent<DeliveryTrigger>();
        trig.Init(this, playerTag);

        
        if (arrow) arrow.SetTarget(currentMarker.transform);
    }

    public void HandleDelivered()
    {
        deliveredCount++;
        onDelivered?.Invoke();

        
        if (hideArrowWhileRespawning && arrow) arrow.ClearTarget();

        StartCoroutine(RespawnNext());
    }

    IEnumerator RespawnNext()
    {
        if (currentMarker) Destroy(currentMarker);
        currentMarker = null;

        
        if (respawnDelay <= 0f) yield return null; else yield return new WaitForSeconds(respawnDelay);

        SpawnNext();  
    }

    
    public Transform CurrentMarkerTransform => currentMarker ? currentMarker.transform : null;
}

public class DeliveryTrigger : MonoBehaviour
{
    DeliveryPointManager manager;
    string playerTag;

    public void Init(DeliveryPointManager m, string tagName)
    {
        manager = m;
        playerTag = tagName;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            manager?.HandleDelivered();
            
        }
    }
}
