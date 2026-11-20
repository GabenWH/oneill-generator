using Unity.VisualScripting;
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

    [Header("Ground Effect")]
    [Min(0.1f)] public float groundEffectRange = 8f;
    [Min(0f)] public float groundEffectLift = 24f;
    [Min(0f)] public float groundEffectDamping = 4f;

    [Header("HUD")]
    public bool showSpeedometer = true;

    [Header("Thrusters")]
    public ParticleSystem leftThruster;
    public ParticleSystem rightThruster;

    private Rigidbody rb;
    private CylinderGravity cylinderGravity;
    private readonly RaycastHit[] groundHits = new RaycastHit[32];
    private GUIStyle speedStyle;
    private GUIStyle speedCaptionStyle;
    private void OnGUI()
    {
        if (!showSpeedometer || rb == null)
            return;

        if (speedStyle == null)
        {
            speedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            speedStyle.normal.textColor = Color.white;
            speedCaptionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            speedCaptionStyle.normal.textColor = new Color(0.7f, 0.85f, 0.9f);
        }

        // Physics runs in the habitat's rotating frame, so Rigidbody velocity
        // already measures motion relative to the habitat, in metres/second.
        float speed = rb.velocity.magnitude;
        float top = Mathf.Max(0f, Screen.height - 116f);
        GUI.Box(new Rect(20f, top, 260f, 96f), GUIContent.none);
        GUI.Label(new Rect(34f, top + 8f, 232f, 20f),
            "SPEED / HABITAT RELATIVE", speedCaptionStyle);
        GUI.Label(new Rect(34f, top + 28f, 232f, 36f),
            $"{speed:F1} m/s", speedStyle);
        GUI.Label(new Rect(34f, top + 66f, 232f, 20f),
            $"{speed * 3.6f:F1} km/h", speedCaptionStyle);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cylinderGravity = GetComponent<CylinderGravity>();

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
        ApplyGroundEffect();
    }

    // Vertical speed is positive away from the surface.
    public static float GroundEffectAcceleration(float distance, float verticalSpeed,
        float range, float lift, float damping)
    {
        if (range <= 0f || distance >= range) return 0f;
        float proximity = Mathf.Clamp01(1f - distance / range);
        return Mathf.Max(0f, (lift * proximity - damping * verticalSpeed) * proximity);
    }
    
    private void ApplyGroundEffect()
    {
        if (cylinderGravity == null) cylinderGravity = GetComponent<CylinderGravity>();
        ONeillWorld world = cylinderGravity != null ? cylinderGravity.world : null;
        if (world == null || groundEffectRange <= 0f) return;

        Vector3 radial = rb.position - world.transform.position;
        radial.z = 0f;
        if (radial.sqrMagnitude < .001f) return;
        Vector3 down = radial.normalized;
        int count = Physics.RaycastNonAlloc(rb.position, down, groundHits, groundEffectRange,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float closest = groundEffectRange;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            var land = hit.collider.GetComponentInParent<LandChunk>();
            if (hit.collider.GetComponentInParent<CylinderChunk>() == null &&
                hit.collider.GetComponentInParent<CylinderWindowChunk>() == null) continue;
            Vector3 surfaceRadius = hit.point - world.transform.position;
            surfaceRadius.z = 0f;
            // Submerged terrain is handled by the water simulation instead.
            if (land != null && surfaceRadius.magnitude >= world.WaterRadius) continue;
            closest = Mathf.Min(closest, hit.distance);
        }
        Vector3 local = world.transform.InverseTransformPoint(rb.position);
        float radius = new Vector2(local.x, local.y).magnitude;
        float angle = Mathf.Atan2(local.y, local.x);
        var water = world.GetComponentInChildren<CylinderWaterSimulation>();
        if (radius > .0001f && water!= null && water.TryGetSurfaceRadius(world, angle, local.z, out float waterRadius))
        {
            float gap = waterRadius - radius;
            if (gap >= 0f) closest = Mathf.Min(closest, gap);
        }
        float speed = Vector3.Dot(rb.velocity, -down);
        float acceleration = GroundEffectAcceleration(closest, speed, groundEffectRange,
            groundEffectLift, groundEffectDamping);
        rb.AddForce(-down * acceleration, ForceMode.Acceleration);
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
        if (leftThruster != null && rightThruster != null)
        {
        if(forward > 0f || Mathf.Abs(mouseX)>0.2f)
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
