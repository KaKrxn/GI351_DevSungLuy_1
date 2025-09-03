// DeliveryPointManager.cs (robust)
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
    public float respawnDelay = 0.0f;   // ������ҡ�������͹��������

    [Header("Events")]
    public UnityEvent onDelivered;

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

        // ���ҧ����
        Vector3 pos = points[idx].position;
        Quaternion rot = points[idx].rotation;

        if (currentMarker) Destroy(currentMarker);

        currentMarker = markerPrefab
            ? Instantiate(markerPrefab, pos, rot)
            : GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        if (!markerPrefab)  // ���� fallback
        {
            currentMarker.transform.SetPositionAndRotation(pos, rot);
            currentMarker.transform.localScale = new Vector3(2.5f, 0.2f, 2.5f);
            var r = currentMarker.GetComponent<Renderer>();
            if (r) r.material.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        }

        // ����� Collider Ẻ Trigger
        var col = currentMarker.GetComponent<Collider>();
        if (!col) col = currentMarker.AddComponent<BoxCollider>();
        col.isTrigger = true;

        // ��� Rigidbody ������ԡ������� OnTrigger �ӧҹ��Ѻ CharacterController
        var rb = currentMarker.GetComponent<Rigidbody>();
        if (!rb) rb = currentMarker.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // �Դ Trigger handler
        var trig = currentMarker.GetComponent<DeliveryTrigger>();
        if (!trig) trig = currentMarker.AddComponent<DeliveryTrigger>();
        trig.Init(this, playerTag);
    }

    public void HandleDelivered()
    {
        deliveredCount++;
        onDelivered?.Invoke();
        StartCoroutine(RespawnNext());
    }

    IEnumerator RespawnNext()
    {
        if (currentMarker) Destroy(currentMarker);
        currentMarker = null;
        // ������������稻������ (�ѹ������ Destroy �Ѻ�Ѻ������ҧ����)
        if (respawnDelay <= 0f) yield return null; else yield return new WaitForSeconds(respawnDelay);
        SpawnNext();
    }
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
            // ������·���� �������� Manager �Ѵ���
        }
    }
}
