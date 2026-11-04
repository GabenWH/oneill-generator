using System;
using UnityEditor;
using UnityEngine;

namespace GraceLunarOrbit.Editor
{
    // No NUnit/Test Framework dependency. Run after Unity imports the package.
    public static class LunarOrbitChecks
    {
        [MenuItem("Tools/Grace/Lunar Orbit/Run math and frame checks")]
        public static void Run()
        {
            int checks = 0;
            Action<double,double,double> near = (actual, expected, tolerance) =>
            {
                if (double.IsNaN(actual) || Math.Abs(actual-expected) > tolerance)
                    throw new Exception($"Lunar orbit check failed: {actual} != {expected}");
                checks++;
            };
            near(LunarOrbitMath.OrbitalPeriod(4904869500000.0,1837400.0),7065.968677733854,0.0001);
            near(LunarOrbitMath.SpinRate(9.81,4000),0.04952272205765754,1e-12);
            near(LunarOrbitMath.SunVisibility(0,0.5,0.01),0,1e-12);
            near(LunarOrbitMath.SunVisibility(1,0.5,0.01),1,1e-12);
            GameObject test = new GameObject("Temporary lunar test");
            try
            {
                LunarOrbitSky orbit = test.AddComponent<LunarOrbitSky>();
                orbit.enabled = false;
                orbit.inclinationDegrees = 0;
                orbit.simulationSeconds = 0;
                orbit.EvaluatePose();
                near(Vector3.Distance(orbit.StationPositionKm,new Vector3(1837.4f,0,0)),0,0.001);
                orbit.simulationSeconds = orbit.OrbitPeriodSeconds/4.0;
                orbit.EvaluatePose();
                near(Vector3.Distance(orbit.StationPositionKm,new Vector3(0,1837.4f,0)),0,0.001);
                orbit.simulationSeconds = orbit.SpinPeriodSeconds/4.0;
                orbit.EvaluatePose();
                near(Vector3.Distance(orbit.HabitatToInertial*Vector3.right,Vector3.up),0,0.0001);
                near(Vector3.Distance(Quaternion.Inverse(orbit.HabitatToInertial)*Vector3.right,Vector3.down),0,0.0001);
                orbit.inclinationDegrees=90;
                orbit.simulationSeconds=orbit.OrbitPeriodSeconds/4.0;
                orbit.EvaluatePose();
                near(Vector3.Distance(orbit.StationPositionKm,new Vector3(0,0,1837.4f)),0,0.001);
                Vector3 before=orbit.StationPositionKm;
                test.transform.position=new Vector3(-4000f,2000f,1000f);
                orbit.EvaluatePose();
                near(Vector3.Distance(before,orbit.StationPositionKm),0,0.0001);
                orbit.simulationSeconds = double.NaN;
                bool invalidRejected = false;
                try { orbit.EvaluatePose(); }
                catch (ArgumentOutOfRangeException) { invalidRejected = true; }
                if (!invalidRejected) throw new Exception("NaN time must be rejected before presenting the sky.");
                checks++;
                if (Shader.Find("Grace/Lunar Orbit Sky") == null || Shader.Find("Grace/Lunar Reference Surface") == null)
                    throw new Exception("Lunar shaders are missing.");
                checks++;
                Debug.Log($"Lunar orbit: {checks} native math/frame/shader-presence checks passed. This does not establish rendering or flight compatibility.");
            }
            finally { UnityEngine.Object.DestroyImmediate(test); }
        }
    }
}
