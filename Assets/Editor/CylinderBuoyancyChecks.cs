using System;
using System.Reflection;
using UnityEngine;

/// <summary>Native Unity checks; invoked by CylinderWorldChecks without exiting the editor.</summary>
public static class CylinderBuoyancyChecks
{
    private static int checks;

    public static void Validate()
    {
        checks = 0;
        Type type = typeof(ONeillWorld).Assembly.GetType("CylinderBuoyancy");
        Require(type != null, "The craft needs CylinderBuoyancy to float with its fans off.");
        var root = new GameObject("Buoyancy checks");
        try
        {
            var world = root.AddComponent<ONeillWorld>();
            world.terrainAmplitude = 0f;
            world.seaLevel = 20f;
            world.simulateCoriolis = false;
            var chunks = root.AddComponent<CylinderChunkManager>();
            chunks.world = world;
            var water = root.AddComponent<CylinderWaterSimulation>();
            water.world = world;
            water.chunks = chunks;
            var craft = new GameObject("Test hull");
            craft.transform.SetParent(root.transform, false);
            var body = craft.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 120f;
            var hull = craft.AddComponent<BoxCollider>();
            hull.size = new Vector3(4f, 1f, 6f);
            var buoyancy = craft.AddComponent(type);
            Set(buoyancy, "world", world);
            Set(buoyancy, "water", water);
            Set(buoyancy, "hull", hull);
            Set(buoyancy, "relativeDensity", .35f);
            Set(buoyancy, "linearWaterDrag", 2f);
            Set(buoyancy, "angularWaterDrag", 3f);

            // Removing the dry branch would damp a flying craft and create lift in air.
            Place(craft, water, 15f, 3f);
            Evaluate(buoyancy, out Vector3 dryAcceleration, out Vector3 dryTorque);
            Require(Fraction(buoyancy) == 0f && dryAcceleration == Vector3.zero && dryTorque == Vector3.zero,
                "A hull above the curved sea has no buoyancy.");
            body.velocity = new Vector3(3f, 4f, 5f);
            body.angularVelocity = new Vector3(.2f, .3f, .4f);
            Apply(buoyancy, .02f);
            Require((body.velocity - new Vector3(3f, 4f, 5f)).magnitude < .0001f &&
                (body.angularVelocity - new Vector3(.2f, .3f, .4f)).magnitude < .0001f,
                "Water drag leaves an airborne craft alone.");

            // A global-Y force fails the second, opposite-side land band.
            foreach (float angle in new[] {15f, 135f})
            {
                Place(craft, water, angle, -3f);
                Evaluate(buoyancy, out Vector3 acceleration, out Vector3 torque);
                Vector3 expected = -world.GetCentrifugalAcceleration(craft.transform.position) / .35f;
                Require(Mathf.Abs(Fraction(buoyancy) - 1f) < .0001f,
                    "A fully immersed hull displaces its complete volume.");
                Require((acceleration - expected).magnitude < .003f,
                    "Fully submerged buoyancy opposes centrifugal gravity with density-scaled magnitude.");
            }

            Place(craft, water, 15f, 0f);
            Evaluate(buoyancy, out Vector3 halfAcceleration, out Vector3 halfTorque);
            Require(Mathf.Abs(Fraction(buoyancy) - .5f) < .015f && halfAcceleration.magnitude > 10f,
                "A hull crossing the sea uses partial volume rather than a binary point test.");

            // Ignoring the stripe or cap mask creates invisible water in the glass or outside the cylinder.
            Place(craft, water, 90f, -3f);
            Evaluate(buoyancy, out Vector3 windowAcceleration, out Vector3 windowTorque);
            Require(Fraction(buoyancy) == 0f && windowAcceleration == Vector3.zero,
                "Window bands contain no buoyant water.");
            Place(craft, water, 15f, -3f, world.length * .5f + 10f);
            Evaluate(buoyancy, out Vector3 capAcceleration, out Vector3 capTorque);
            Require(Fraction(buoyancy) == 0f && capAcceleration == Vector3.zero,
                "The open axial ends contain no buoyant water.");
            world.seaLevel = -20f;
            Place(craft, water, 15f, -3f);
            Evaluate(buoyancy, out Vector3 terrainAcceleration, out Vector3 terrainTorque);
            Require(Fraction(buoyancy) == 0f && terrainAcceleration == Vector3.zero,
                "Terrain above sea level excludes water even when the hull is radially beyond the sea.");
            world.seaLevel = 20f;
            Place(craft, water, 15f, -30f);
            Evaluate(buoyancy, out Vector3 undergroundAcceleration, out Vector3 undergroundTorque);
            Require(Fraction(buoyancy) == 0f && undergroundAcceleration == Vector3.zero,
                "Solid terrain below a wet valley is not displaced water.");

            // Replacing the force distribution with one central force loses the restoring couple.
            Place(craft, water, 15f, .15f, 0f, 18f);
            Evaluate(buoyancy, out Vector3 heelAcceleration, out Vector3 heelTorque);
            Require(heelTorque.z < -.05f, "Positive heel produces a restoring torque from the submerged hull side.");
            Apply(buoyancy, .02f);
            Require((body.GetAccumulatedForce() / body.mass - heelAcceleration).magnitude < .01f &&
                body.GetAccumulatedTorque().z < -.01f,
                "Fixed-step buoyancy applies its distributed hull forces and restoring torque to the rigidbody.");
            Place(craft, water, 15f, .15f, 0f, -18f);
            Evaluate(buoyancy, out Vector3 reverseAcceleration, out Vector3 reverseTorque);
            Require(reverseTorque.z > .05f, "Negative heel reverses the restoring torque.");

            float originalFraction = Fraction(buoyancy);
            root.transform.position = new Vector3(-3900f, 2000f, 700f);
            Physics.SyncTransforms();
            Evaluate(buoyancy, out Vector3 translatedAcceleration, out Vector3 translatedTorque);
            Require(Mathf.Abs(Fraction(buoyancy) - originalFraction) < .001f &&
                (translatedAcceleration - reverseAcceleration).magnitude < .01f &&
                (translatedTorque - reverseTorque).magnitude < .01f,
                "A floating-origin translation preserves displaced volume, force, and restoring torque.");
            root.transform.position = Vector3.zero;
            Physics.SyncTransforms();

            // Density defines acceleration; changing rigidbody mass must scale actual force, not acceleration.
            Place(craft, water, 15f, -3f);
            Evaluate(buoyancy, out Vector3 lightAcceleration, out Vector3 lightTorque);
            body.mass = 480f;
            Evaluate(buoyancy, out Vector3 heavyAcceleration, out Vector3 heavyTorque);
            Require((lightAcceleration - heavyAcceleration).magnitude < .0001f,
                "Buoyant acceleration is stable when craft mass changes at fixed relative density.");
            Vector3 previousForce = body.GetAccumulatedForce();
            Apply(buoyancy, .02f);
            Require(((body.GetAccumulatedForce() - previousForce) / body.mass - heavyAcceleration).magnitude < .01f,
                "Actual rigidbody buoyancy scales with mass at fixed relative density.");

            body.velocity = new Vector3(6f, 8f, 0f);
            body.angularVelocity = new Vector3(.3f, .4f, 0f);
            Apply(buoyancy, .04f);
            Vector3 coarseLinear = body.velocity, coarseAngular = body.angularVelocity;
            body.velocity = new Vector3(6f, 8f, 0f);
            body.angularVelocity = new Vector3(.3f, .4f, 0f);
            Apply(buoyancy, .02f);
            Apply(buoyancy, .02f);
            Require((body.velocity - coarseLinear).magnitude < .0001f &&
                (body.angularVelocity - coarseAngular).magnitude < .0001f &&
                Mathf.Abs(coarseLinear.magnitude - 9.231164f) < .001f &&
                Mathf.Abs(coarseAngular.magnitude - .4434602f) < .001f,
                "Immersed linear and angular drag use exponential damping across fixed timestep sizes.");

            Place(craft, water, 15f, 0f);
            body.velocity = new Vector3(6f, 8f, 0f);
            body.angularVelocity = new Vector3(.3f, .4f, 0f);
            Apply(buoyancy, .04f);
            Require(Mathf.Abs(body.velocity.magnitude - 9.607894f) < .015f &&
                Mathf.Abs(body.angularVelocity.magnitude - .4708823f) < .001f,
                "A half-immersed hull receives half the water drag rate.");
            Debug.Log("CYLINDER_BUOYANCY_CHECKS_PASS " + checks);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void Place(GameObject craft, CylinderWaterSimulation water, float degrees,
        float height, float z = 0f, float heel = 0f)
    {
        float angle = degrees * Mathf.Deg2Rad;
        Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        craft.transform.localPosition = radial * (water.SurfaceRadiusAtAngle(angle) - height) + Vector3.forward * z;
        craft.transform.localRotation = Quaternion.LookRotation(Vector3.forward, -radial) *
            Quaternion.AngleAxis(heel, Vector3.forward);
        Physics.SyncTransforms();
    }

    private static void Evaluate(Component component, out Vector3 acceleration, out Vector3 torque)
    {
        MethodInfo method = component.GetType().GetMethod("EvaluateForces");
        Require(method != null, "Buoyancy exposes its displaced-volume force evaluation.");
        object[] args = { Vector3.zero, Vector3.zero };
        method.Invoke(component, args);
        acceleration = (Vector3)args[0]; torque = (Vector3)args[1];
    }

    private static void Apply(Component component, float deltaTime)
    {
        MethodInfo method = component.GetType().GetMethod("ApplyWaterForces");
        Require(method != null, "Buoyancy exposes fixed-step water force application.");
        method.Invoke(component, new object[] { deltaTime });
    }

    private static float Fraction(Component component)
    {
        PropertyInfo property = component.GetType().GetProperty("submergedFraction");
        Require(property != null && !property.CanWrite, "Displaced hull fraction is public and read-only.");
        return (float)property.GetValue(component);
    }

    private static void Set(Component component, string name, object value)
    {
        FieldInfo field = component.GetType().GetField(name);
        Require(field != null, "Missing buoyancy setting: " + name);
        field.SetValue(component, value);
    }

    private static void Require(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
