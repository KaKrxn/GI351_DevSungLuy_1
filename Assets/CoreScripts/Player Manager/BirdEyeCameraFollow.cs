using UnityEngine;

public class BirdEyeCameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Rig")]
    public float distance = 12f;
    public float minDistance = 5f;   // ซูมเข้าได้มากสุด
    public float maxDistance = 20f;  // ซูมออกได้มากสุด
    public float zoomSpeed = 5f;
    public float pitch = 65f;  
    public float yawOffset = 0f;

    [Header("Mouse Control")]
    public float mouseSensitivity = 3f;
    public bool invertY = false;

    [Header("Smoothing")]
    public float followDamping = 10f;
    public float lookDamping = 15f;

    [Header("Auto Align")]
    public bool autoAlign = true;      
    public float autoAlignSpeed = 2f;  
    public float idleTimeBeforeAlign = 1.5f;

    private float yaw;
    private float currentPitch;
    private float lastMouseInputTime;

    void Start()
    {
        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) target = p.transform;
        }
        yaw = yawOffset;
        currentPitch = pitch;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // ควบคุมหมุนกล้องด้วยเมาส์
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * (invertY ? 1 : -1);

        if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
        {
            yaw += mouseX;
            currentPitch = Mathf.Clamp(currentPitch + mouseY, 20f, 80f);
            lastMouseInputTime = Time.time;
        }
        else if (autoAlign && Time.time - lastMouseInputTime > idleTimeBeforeAlign)
        {
            float targetYaw = target.eulerAngles.y + yawOffset;
            yaw = Mathf.LerpAngle(yaw, targetYaw, Time.deltaTime * autoAlignSpeed);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance -= scroll * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
    }

    void LateUpdate()
    {
        if (!target) return;

        Quaternion rot = Quaternion.Euler(currentPitch, yaw, 0f);
        Vector3 offset = rot * new Vector3(0f, 0f, -distance);
        Vector3 desiredPos = target.position + offset;

        float tPos = 1f - Mathf.Exp(-followDamping * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, tPos);

        Quaternion look = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
        float tRot = 1f - Mathf.Exp(-lookDamping * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, tRot);
    }
}
