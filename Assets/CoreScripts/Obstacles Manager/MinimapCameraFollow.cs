
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MinimapCameraFollow : MonoBehaviour
{
    public Transform target;         
    public float height = 60f;       
    public float orthoSize = 25f;    
    public bool rotateWithTarget = true; 

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    void LateUpdate()
    {
        if (!target) return;
        transform.position = new Vector3(target.position.x, height, target.position.z);

        if (rotateWithTarget)
            transform.rotation = Quaternion.Euler(90f, target.eulerAngles.y, 0f);
        else
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    public void SetZoom(float size) { cam.orthographicSize = Mathf.Max(1f, size); }
}
