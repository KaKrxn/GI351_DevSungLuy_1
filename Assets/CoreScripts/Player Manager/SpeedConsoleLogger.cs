using UnityEngine;

public class SpeedConsoleLogger : MonoBehaviour
{
    [Tooltip("??????????? ?????????? Rigidbody ?????????/???????")]
    public Rigidbody rb;

    [Tooltip("???????????? log (??????)")]
    public float interval = 0.25f;

    [Tooltip("?????????????????")]
    [Range(0, 3)] public int decimals = 0;

    float timer;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb) rb = GetComponentInParent<Rigidbody>();
    }

    void Update()
    {
        if (!rb) return;

        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0f;
            float kmh = rb.linearVelocity.magnitude * 3.6f;
            Debug.Log($"[SPD] {kmh.ToString($"F{decimals}")} km/h");
        }
    }
}
