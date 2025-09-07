// DeliveryPointManager.cs — add scoring (per delivery + per second until GameOver)
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable] public class IntEvent : UnityEvent<int> { }

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

    // ---------- NEW: Scoring ----------
    [Header("Scoring")]
    [Tooltip("คะแนนรวมปัจจุบัน")]
    public int score = 0;
    [Tooltip("คะแนนต่อ 1 การส่งสำเร็จ")]
    public int pointsPerDelivery = 100;
    [Tooltip("คะแนนต่อเนื่องต่อวินาที (ตั้ง > 0 เพื่อให้นับเรื่อย ๆ หลังส่งครั้งแรก)")]
    public float pointsPerSecond = 0f;
    [Tooltip("เริ่มนับคะแนนต่อเนื่องตั้งแต่ส่งครั้งแรก")]
    public bool startContinuousOnFirstDelivery = true;
    [Tooltip("หยุดการนับต่อเนื่องเมื่อ GameOver")]
    public bool stopContinuousOnGameOver = true;

    [Tooltip("อีเวนต์แจ้งคะแนนเปลี่ยน (ส่งค่า score ปัจจุบัน)")]
    public IntEvent onScoreChanged;

    bool scoringActive = false;
    bool isGameOver = false;
    float scoreFrac = 0f; // เก็บเศษคะแนนจากการคูณ dt

    GameObject currentMarker;
    int lastIndex = -1;
    public int deliveredCount { get; private set; }

    void Start()
    {
        score = Mathf.Max(0, score);
        scoringActive = false;
        isGameOver = false;
        scoreFrac = 0f;
        SpawnNext();
        onScoreChanged?.Invoke(score);
    }

    void Update()
    {
        // นับคะแนนต่อเนื่อง/วินาที
        if (scoringActive && !isGameOver && pointsPerSecond > 0f)
        {
            scoreFrac += pointsPerSecond * Time.deltaTime;
            int add = (int)scoreFrac;
            if (add > 0)
            {
                score += add;
                scoreFrac -= add;
                onScoreChanged?.Invoke(score);
            }
        }
    }

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

        // เพิ่มคะแนนครั้งละจุด
        if (pointsPerDelivery > 0)
        {
            score += pointsPerDelivery;
            onScoreChanged?.Invoke(score);
        }

        // เริ่มนับต่อเนื่องหลังส่งครั้งแรก (ถ้าเปิดไว้)
        if (startContinuousOnFirstDelivery && !scoringActive)
            scoringActive = true;

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

    // ให้สคริปต์อื่นเรียกตอน GameOver
    public void SetGameOver()
    {
        isGameOver = true;
        if (stopContinuousOnGameOver) scoringActive = false;
    }

    // เผื่อสคริปต์อื่นอยากอ่านเป้าปัจจุบัน
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
