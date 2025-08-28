using UnityEngine;


public class BirdEyeCameraFollow : MonoBehaviour
{
    public Transform target; 
    [Header("Rig")]
    public float distance = 12f; 
    public float pitch = 65f; 
    public float yawOffset = 0f; 


    [Header("Smoothing")]
    public float followDamping = 10f; 
    public float lookDamping = 15f;


    [Header("Lock Yaw to Player")]
    public bool lockYawToPlayer = false; 


    void LateUpdate()
    {
        if (!target) return;


        float yaw = lockYawToPlayer ? target.eulerAngles.y + yawOffset : yawOffset;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = rot * new Vector3(0f, 0f, -distance);
        Vector3 desiredPos = target.position + offset;


        
        float tPos = 1f - Mathf.Exp(-followDamping * Time.deltaTime);
        float tRot = 1f - Mathf.Exp(-lookDamping * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, tPos);


        
        Quaternion look = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, tRot);
    }
}