using UnityEngine;
using TMPro;

public class SpeedHUDTMP : MonoBehaviour
{
    [Tooltip("Rigidbody ของรถ (ว่างได้ จะหาเอง)")]
    public Rigidbody rb;

    [Tooltip("TMP Text ที่จะแสดงความเร็ว")]
    public TextMeshProUGUI speedLabel;

    [Tooltip("รูปแบบข้อความ")]
    public string format = "Speed: {0:0} km/h";

    void Reset()
    {
        rb = GetComponentInParent<Rigidbody>();
        if (!speedLabel) speedLabel = GetComponentInChildren<TextMeshProUGUI>();
    }

    void Update()
    {
        if (!rb || !speedLabel) return;

        float kmh = rb.linearVelocity.magnitude * 3.6f;
        speedLabel.text = string.Format(format, kmh);
    }
}
