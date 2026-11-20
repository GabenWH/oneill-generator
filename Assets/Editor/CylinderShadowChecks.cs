using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Call Validate from the native Editor checks; it does not enter Play or exit Unity.</summary>
public static class CylinderShadowChecks
{
    public static void Validate()
    {
        var root = new GameObject("Cylinder shadow checks");
        try
        {
            var world = root.AddComponent<ONeillWorld>();
            var lightObject = new GameObject("Test sun", typeof(Light));
            lightObject.transform.SetParent(root.transform, false);
            var sun = lightObject.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            // Reflection keeps this regression runnable before the provider exists.
            Type providerType = typeof(ONeillWorld).Assembly.GetType("CylinderSunShadows");
            Require(providerType != null, "Water needs a world-space sunlight/shadow provider.");
            var provider = (MonoBehaviour)root.AddComponent(providerType);
            providerType.GetField("world").SetValue(provider, world);
            providerType.GetField("sun").SetValue(provider, sun);
            providerType.GetMethod("Refresh").Invoke(provider, null);
            var transmission = providerType.GetMethod("HousingTransmission");
            Require(transmission != null, "Housing transmission must be independently queryable.");
            Func<Vector3, Vector3, float> sample = (point, direction) =>
                (float)transmission.Invoke(provider, new object[] { point, direction });
            Vector3 land = Direction(30), glass = Direction(90);
            Require(sample(Vector3.zero, land) == 0f, "An exit through a land stripe blocks sunlight.");
            Require(Mathf.Abs(sample(Vector3.zero, glass) - .975f) < .0001f,
                "An exit through a glass stripe transmits its normal-incidence sunlight.");
            Require(Mathf.Abs(sample(Vector3.zero, Vector3.forward) - .975f) < .0001f,
                "An axial ray exits the glass cap without a radial divide by zero.");
            Require(sample(new Vector3(0, 0, world.length * .5f - 10f),
                new Vector3(land.x, land.y, 1f).normalized) > .9f,
                "A cap reached before the opaque radial band remains transmissive.");
            Require(sample(land * (world.OuterRadius + 1f), land) == 1f,
                "An exterior surface looking away from the habitat remains sunlit.");
            Require(sample(new Vector3(3700.01514f,2190.36251f,0f),new Vector3(.860519834f,.509416937f,0f))==1f,
                "The actual faceted outer land surface must not shadow its own outward sunlight.");
            Vector3 reportedSun=-(Quaternion.Euler(35f,-30f,0f)*Vector3.forward);
            Require(sample(new Vector3(1550f,-3770f,-6940f),reportedSun)==0f,
                "Land_19_4: a ray through the window that then hits the thick Land_0 wall stays shadowed beyond the camera shadow distance.");
            Require(sample(new Vector3(1650f,-3783f,-7000f),reportedSun)>.8f,
                "The adjacent ray that clears the thick wall remains transmissive.");
            float expected = sample(Vector3.zero, glass);
            world.transform.SetPositionAndRotation(new Vector3(-3900, 2000, 700), Quaternion.Euler(0, 0, 37));
            Require(Mathf.Abs(sample(world.transform.position, world.transform.TransformDirection(glass)) - expected) < .0001f,
                "Housing occlusion survives floating-origin translation and world rotation.");
            providerType.GetMethod("Refresh").Invoke(provider, null);
            Require(Shader.GetGlobalFloat("_CylinderHousingAvailable") == 1f,
                "An active configured provider publishes housing globals.");
            provider.enabled = false;
            // This static harness runs in Edit mode; normal runtime behaviours
            // do not receive Play-mode lifecycle callbacks in that context.
            if (!Application.isPlaying)
                providerType.GetMethod("OnDisable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(provider, null);
            Require(Shader.GetGlobalFloat("_CylinderHousingAvailable") == 0f &&
                Shader.GetGlobalFloat("_CylinderShadowAtlasAvailable") == 0f,
                "Disabling the provider resets all availability flags.");
            Require(sun.GetCommandBuffers(LightEvent.AfterShadowMap).Length == 0,
                "Disabling the provider removes its shadow capture command buffer.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Debug.Log("CYLINDER_SHADOW_CHECKS_PASS");
    }

    private static Vector3 Direction(float degrees)
        => new Vector3(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad), 0);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
