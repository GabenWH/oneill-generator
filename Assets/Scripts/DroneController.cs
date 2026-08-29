using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DroneController : MonoBehaviour
{
    [Header("Movement")]
    public float thrust = 30f;
    public float boostMultiplier = 4f;

    [Header("Rotation")]
    public float mouseSensitivity = 2f;
    public float rollSpeed = 80f;

    [Header("Damping")]
    public float linearDrag = 0.15f;
    public float angularDrag = 2f;

    [Header("Thrusters")]
    public ParticleSystem leftThruster;
    public ParticleSystem rightThruster;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.drag = linearDrag;
        rb.angularDrag = angularDrag;

        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;
    }

    private void Update()
    {
        HandleRotation();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState =
                CursorLockMode.None;

            Cursor.visible = true;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Cursor.lockState =
                CursorLockMode.Locked;

            Cursor.visible = false;
        }
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        float mouseX = 
            Input.GetAxis("Mouse X");
        float mouseY = 
            Input.GetAxis("Mouse Y");

        float forward =
            Input.GetAxisRaw("Vertical");
        float sideways =
            Input.GetAxisRaw("Horizontal");

        float vertical = 0f;

        if (Input.GetKey(KeyCode.Space))
            vertical += 1f;

        if (Input.GetKey(KeyCode.C))
            vertical -= 1f;

        float multiplier =
            Input.GetKey(KeyCode.LeftShift)
                ? boostMultiplier
                : 1f;

        Vector3 input =
            new Vector3(
                sideways,
                vertical,
                forward
            );

        if (input.sqrMagnitude > 1f)
            input.Normalize();
        if(forward > 0f || mouseX>0.2f)
        {
            var leftEmission = leftThruster.emission;
            var rightEmission = rightThruster.emission;
            var baseEmission=1f;
            var forwardSpeed = 1f;
            var turnRet = 20f;
            leftEmission.rateOverTime = baseEmission + forwardSpeed * 50 - Mathf.Max(mouseX,0f)*turnRet;
            rightEmission.rateOverTime= baseEmission + forwardSpeed * 50 + Mathf.Min(mouseX,0f)*turnRet;


            leftThruster.Play();
            rightThruster.Play();
        }
        else
        {
            leftThruster.Stop();
            rightThruster.Stop();
        }
        rb.AddRelativeForce(
            input *
            thrust *
            multiplier,
            ForceMode.Acceleration
        );
    }

    private void HandleRotation()
    {
        if (Cursor.lockState !=
            CursorLockMode.Locked)
            return;

        float mouseX =
            Input.GetAxis("Mouse X");

        float mouseY =
            Input.GetAxis("Mouse Y");

        float roll = 0f;

        if (Input.GetKey(KeyCode.Q))
            roll += 1f;

        if (Input.GetKey(KeyCode.E))
            roll -= 1f;

        Quaternion delta =
            Quaternion.Euler(
                -mouseY * mouseSensitivity,
                mouseX * mouseSensitivity,
                roll * rollSpeed * Time.deltaTime
            );

        rb.MoveRotation(
            rb.rotation * delta
        );
    }
}