using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerControllerTest : MonoBehaviour
{
    public float walkSpeed = 4f, runSpeed = 7f, acceleration = 20f;
    public float jumpHeight = 1.5f, gravity = -20f;
    public float rotationSpeed = 720f;
    public bool moveRelativeToCamera = true;

    CharacterController controller;
    Vector3 velocity;
    Camera cam;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main;
        
        if (controller != null && !controller.enabled) controller.enabled = true;
    }

    void Update()
    {
        if (controller == null || !controller.enabled) return; 

        float ix = Input.GetAxisRaw("Horizontal");
        float iz = Input.GetAxisRaw("Vertical");
        Vector2 inVec = Vector2.ClampMagnitude(new Vector2(ix, iz), 1f);
        bool wantRun = Input.GetKey(KeyCode.LeftShift);

        Vector3 fwd, right;
        if (moveRelativeToCamera && cam != null)
        {
            fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
        }
        else { fwd = Vector3.forward; right = Vector3.right; }

        Vector3 moveDir = (fwd * inVec.y + right * inVec.x).normalized;
        float targetSpeed = (wantRun ? runSpeed : walkSpeed) * inVec.magnitude;

        Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 desiredVel = moveDir * targetSpeed;
        horizontalVel = Vector3.MoveTowards(horizontalVel, desiredVel, acceleration * Time.deltaTime);

        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion tgt = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, tgt, rotationSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        if (controller.isGrounded && Input.GetKeyDown(KeyCode.Space))
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        velocity.x = horizontalVel.x; velocity.z = horizontalVel.z;

        controller.Move(velocity * Time.deltaTime);
    }
}
