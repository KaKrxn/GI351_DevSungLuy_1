using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public float acceleration = 500f; // แรงเร่ง
    public float maxSpeed = 20f;      // ความเร็วสูงสุด
    public float turnSpeed = 100f;    // ความเร็วการเลี้ยว
    public float brakeForce = 1000f;  // แรงเบรก
    public Transform centerOfMass;    // จุดศูนย์ถ่วง (ทำให้รถไม่ล้ม)

    private Rigidbody rb;
    private float moveInput;
    private float steerInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 1500f; // น้ำหนักประมาณรถเก๋ง
        rb.linearDamping = 0.2f;  // ต้านแรง
        rb.angularDamping = 3f; // ลดการหมุนเกินจริง
        rb.linearDamping = 0.5f;

        if (centerOfMass != null)
            rb.centerOfMass = centerOfMass.localPosition;
    }

    void Update()
    {
        moveInput = Input.GetAxis("Vertical");   // W/S = เดินหน้า/ถอยหลัง
        steerInput = Input.GetAxis("Horizontal"); // A/D = เลี้ยว
    }

    void FixedUpdate()
{
    Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
    float currentSpeed = localVel.z;

    // เร่ง
    if (Mathf.Abs(moveInput) > 0.1f)
    {
        if (Mathf.Abs(currentSpeed) < maxSpeed)
            rb.AddForce(transform.forward * moveInput * acceleration * Time.fixedDeltaTime, ForceMode.Acceleration);
    }
    else
    {
        // Auto-Brake
        rb.AddForce(-rb.linearVelocity.normalized * brakeForce * 0.5f * Time.fixedDeltaTime, ForceMode.Acceleration);
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 2f);
    }

    // ลดไถลด้านข้าง
    localVel.x *= 0.9f;
    rb.linearVelocity = transform.TransformDirection(localVel);

    // จำกัดความเร็ว
    if (rb.linearVelocity.magnitude > maxSpeed)
        rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;

    // เลี้ยวช้าลง
    if (Mathf.Abs(currentSpeed) > 0.1f)
    {
        float minSpeedForTurn = 5f;
        float speedRatio = Mathf.Clamp01(currentSpeed / minSpeedForTurn);
        float turnAmount = steerInput * turnSpeed * 0.3f * speedRatio * Time.fixedDeltaTime * Mathf.Sign(currentSpeed);
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turnAmount, 0f));
    }

    // เบรก (Space)
    if (Input.GetKey(KeyCode.Space))
    {
        rb.AddForce(-rb.linearVelocity.normalized * brakeForce * Time.fixedDeltaTime, ForceMode.Acceleration);
    }
}



}


