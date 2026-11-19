using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CylinderBeachTown
{
    public sealed partial class BeachTownPrototype : MonoBehaviour
    {
        public GameObject harborLocalModel;
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly List<Vector3> checkpoints = new List<Vector3>();
        private readonly string[] destinations = {"MARKET SQUARE", "SOUTH BEACH", "FIELD OVERLOOK"};
        private int visited;
        private const float StopA = 0f, StopB = 94f, RailY = .11f, RailZ = -12f, Dwell = 8f, Run = 19f;
        private static readonly Vector3 Spawn = new Vector3(-6, .12f, 14);
        private CharacterController walker;
        private Camera eyes;
        private Transform tram;
        private float tramClock, tramX, pitch, yaw = 178, fall;
        private bool riding;
        private int dock = 0;
        private Transform district;
        private System.Random rng = new System.Random(1976);

        private void Awake()
        {
            ConfigureLight();
            BuildTown();
            var p = new GameObject("First-person walker");
            p.transform.SetParent(transform, false);
            p.transform.position = Spawn;
            walker = p.AddComponent<CharacterController>();
            walker.height = 1.75f;
            walker.radius = .29f;
            walker.center = Vector3.up * .875f;
            walker.stepOffset = .3f;
            walker.skinWidth = .035f;
            var c = new GameObject("Pedestrian camera");
            c.transform.SetParent(p.transform, false);
            c.transform.localPosition = Vector3.up * 1.63f;
            eyes = c.AddComponent<Camera>();
            c.AddComponent<AudioListener>();
            eyes.farClipPlane = 4800;
            eyes.nearClipPlane = .045f;
            eyes.fieldOfView = 67;
            eyes.clearFlags = CameraClearFlags.SolidColor;
            eyes.backgroundColor = Hex("b4d9dc");
        }

        private void Update()
        {
            ReadControls(out Vector2 move, out Vector2 look, out bool run, out bool action, out bool reset, out bool release);
            if (release) Unlock();
            if (reset) ResetWalk();
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, -83, 83);
            }
            walker.transform.rotation = Quaternion.Euler(0, yaw, 0);
            eyes.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            TickTram();
            if (action && dock >= 0)
            {
                if (riding) { riding = false; PlaceWalker(Platform); }
                else if (NearPlatform) riding = true;
            }
            // The study is an exterior shell, so use an offset ride camera instead of entering the mesh.
            if (riding) walker.transform.position = new Vector3(tramX + 4.8f, 2.12f, RailZ + 3.2f);
            else
            {
                if (walker.isGrounded && fall < 0) fall = -3;
                fall -= 15 * Time.deltaTime;
                float speed = run ? 7.0f : 4.55f;
                Vector3 step = walker.transform.TransformDirection(new Vector3(move.x, 0, move.y)) * speed;
                step.y = fall;
                walker.Move(step * Time.deltaTime);
                Vector3 p = walker.transform.position;
                if (p.y < -4 || !WithinMap(p)) ResetWalk();
                for (int i = 0; i < checkpoints.Count; i++)
                    if ((p - checkpoints[i]).sqrMagnitude < 49) visited |= 1 << i;
            }
        }

        private static bool WithinMap(Vector3 p)
        {
            return p.x > -77 && p.x < 139 && p.z > -52 && p.z < 107;
        }
        private Vector3 Platform => new Vector3(dock == 1 ? StopB : StopA, .21f, RailZ + 3.75f);
        private bool NearPlatform => (walker.transform.position - Platform).sqrMagnitude < 37;
        private void ResetWalk() { riding = false; pitch = 0; yaw = 178; fall = -3; PlaceWalker(Spawn); }
        private void PlaceWalker(Vector3 p)
        {
            walker.enabled = false; walker.transform.position = p; walker.enabled = true;
        }

        private void ReadControls(out Vector2 move, out Vector2 look, out bool run, out bool action, out bool reset, out bool release)
        {
            move = look = Vector2.zero;
            run = action = reset = release = false;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                move.x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
                move.y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
                run = k.leftShiftKey.isPressed; action = k.eKey.wasPressedThisFrame;
                reset = k.rKey.wasPressedThisFrame; release = k.escapeKey.wasPressedThisFrame;
            }
            var m = Mouse.current;
            if (m != null)
            {
                if (m.leftButton.wasPressedThisFrame) Lock();
                look = m.delta.ReadValue() * .105f;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            move.x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            move.y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            run = Input.GetKey(KeyCode.LeftShift); action = Input.GetKeyDown(KeyCode.E);
            reset = Input.GetKeyDown(KeyCode.R); release = Input.GetKeyDown(KeyCode.Escape);
            if (Input.GetMouseButtonDown(0)) Lock();
            look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 2;
#endif
            move = Vector2.ClampMagnitude(move, 1);
        }

        private static void Lock() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        private static void Unlock() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        private void OnDisable() => Unlock();
        private void OnApplicationFocus(bool active) { if (!active) Unlock(); }

        private void TickTram()
        {
            tramClock = (tramClock + Time.deltaTime) % ((Dwell + Run) * 2);
            if (tramClock < Dwell) { dock = 0; tramX = StopA; }
            else if (tramClock < Dwell + Run)
            { dock = -1; tramX = Mathf.SmoothStep(StopA, StopB, (tramClock - Dwell) / Run); }
            else if (tramClock < 2 * Dwell + Run) { dock = 1; tramX = StopB; }
            else
            { dock = -1; tramX = Mathf.SmoothStep(StopB, StopA, (tramClock - 2 * Dwell - Run) / Run); }
            tram.position = new Vector3(tramX, RailY, RailZ);
        }

        private string ServiceText()
        {
            if (dock == 0) return "Docked at Harbor Beach";
            if (dock == 1) return "Docked at Field Overlook";
            return tramClock < (Dwell + Run) ? "Approaching Field Overlook" : "Approaching Harbor Beach";
        }

        private void OnGUI()
        {
            if (walker == null) return;
            Color old = GUI.color;
            GUI.color = new Color(.08f,.16f,.15f,.86f);
            GUI.DrawTexture(new Rect(16,16,290,154), Texture2D.whiteTexture);
            GUI.color = new Color(1f,.95f,.82f);
            var title = new GUIStyle(GUI.skin.label) {fontSize=20, fontStyle=FontStyle.Bold};
            var body = new GUIStyle(GUI.skin.label) {fontSize=13};
            GUI.Label(new Rect(28,22,275,29), "HARBOR BEACH   /   1976", title);
            GUI.Label(new Rect(28,51,275,20), ServiceText(), body);
            for (int i=0;i<destinations.Length;i++)
                GUI.Label(new Rect(28,74+18*i,275,20), ((visited & (1<<i)) != 0 ? "[X] " : "[  ] ") + destinations[i], body);
            if (visited == 7) GUI.Label(new Rect(28,131,275,23), "Promenade circuit complete", body);
            string hint = riding && dock < 0 ? "Riding Harbor Local   •   Exit at the next stop" :
                dock >= 0 && (riding || NearPlatform) ? "E   Board / exit tram" : "Explore the square, shore, and overlook";
            if (Cursor.lockState != CursorLockMode.Locked) hint = "Click to capture the mouse";
            GUI.color = new Color(.08f,.16f,.15f,.88f);
            GUI.DrawTexture(new Rect(16, Screen.height-67, Mathf.Min(535,Screen.width-32), 51), Texture2D.whiteTexture);
            GUI.color = new Color(1f,.95f,.82f);
            GUI.Label(new Rect(27, Screen.height-66, 485, 22), hint, body);
            GUI.Label(new Rect(27, Screen.height-46, 480, 22), "WASD walk   ·   Shift jog   ·   R return   ·   Esc release mouse", body);
            if (Cursor.lockState == CursorLockMode.Locked) GUI.Label(new Rect(Screen.width/2f-3, Screen.height/2f-8, 15,20), "+");
            GUI.color = old;
        }
    }
}
