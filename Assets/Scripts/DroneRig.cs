using UnityEngine;

/// <summary>Meter-scale fan bindings independent of the flight controller.</summary>
public class DroneRig : MonoBehaviour
{
    public Transform[] fanHubs = new Transform[0];
    public Transform[] fanUpMarkers = new Transform[0];
    public Transform[] fanBlades = new Transform[0];
    [Range(0f, 1f)] public float fanPower = 1f;
    public float fanRadius = .6f;

    private void Update()
    {
        foreach (Transform blade in fanBlades)
        {
            if (blade == null || blade.parent == null) continue;
            // Blades are independent mesh objects sharing the hub parent.
            Transform hub = null;
            foreach (Transform candidate in fanHubs)
                if (candidate != null && (hub == null ||
                    (blade.position - candidate.position).sqrMagnitude < (blade.position - hub.position).sqrMagnitude)) hub = candidate;
            if (hub != null) blade.RotateAround(hub.position, transform.up, 1800f * fanPower * Time.deltaTime);
        }
    }
}
