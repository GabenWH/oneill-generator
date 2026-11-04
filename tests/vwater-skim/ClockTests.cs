// Runs production C# without Unity's native runtime. No copied clock model.
using System;
using System.Reflection;
using Grace.VWaterOriginal;

static class ClockTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Main()
    {
        var type = Assembly.GetExecutingAssembly().GetType("Vertigo2.VWaterSimulationClock");
        Check(type != null, "FAIL: no fixed-step clock; the inherited coroutine understeps wall time.");
        var add = type.GetMethod("Accumulate");
        var reset = type.GetMethod("Reset");
        foreach (int fps in new[] { 30, 60, 120, 144 })
        {
            object clock = Activator.CreateInstance(type);
            int steps = 0;
            for (int frame = 0; frame < fps; frame++)
                steps += (int)add.Invoke(clock, new object[] { 1f / fps, 60f, 4 });
            Check(steps == 60, fps + " FPS: expected 60 steps, got " + steps);
        }
        object irregular = Activator.CreateInstance(type);
        int total = 0;
        for (int i = 0; i < 30; i++)
        {
            total += (int)add.Invoke(irregular, new object[] { 1f / 120, 60f, 4 });
            total += (int)add.Invoke(irregular, new object[] { 1f / 40, 60f, 4 });
        }
        Check(total == 60, "Irregular frames must advance one second.");
        object bounded = Activator.CreateInstance(type);
        Check((int)add.Invoke(bounded, new object[] { 10f, 60f, 4 }) == 4, "Long frame must be bounded.");
        Check((int)add.Invoke(bounded, new object[] { 0f, 60f, 4 }) == 0, "No catch-up backlog.");
        add.Invoke(bounded, new object[] { 1f / 120, 60f, 4 });
        reset.Invoke(bounded, null);
        Check((int)add.Invoke(bounded, new object[] { 1f / 120, 60f, 4 }) == 0, "Disable/cull reset must discard fractional time.");
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, -1f })
            Check((int)add.Invoke(bounded, new object[] { bad, 60f, 4 }) == 0, "Invalid delta must not step.");
        Check((int)add.Invoke(bounded, new object[] { 1f, 0f, 4 }) == 0, "Zero rate must not step.");
        Check((int)add.Invoke(bounded, new object[] { 1f, float.NaN, 4 }) == 0, "Invalid rate must not step.");
        Check((int)add.Invoke(bounded, new object[] { 1f, 60f, 0 }) == 0, "Zero budget must not step.");
        // Real fan projection: approved saved-scene geometry and height falloff.
        DownwashMath.Disc low, high;
        Check(DownwashMath.TryDisc(new DownwashMath.V3(-.54,1.9,0),new DownwashMath.V3(0,-.25,0),new DownwashMath.V3(0,0,0),3,3,.9,out low), "Low fan must hit.");
        Check(Math.Abs(low.Point.X + .54) < 1e-8 && Math.Abs(low.Radius - .742) < 1e-8, "Wrong fan footprint.");
        Check(DownwashMath.TryDisc(new DownwashMath.V3(.54,5.5,0),new DownwashMath.V3(0,-.25,0),new DownwashMath.V3(0,0,0),3,3,.9,out high), "High fan must hit.");
        Check(high.Strength < low.Strength / 5, "Raised fans must weaken.");
        Console.WriteLine("PASS: real C# clock timing, catch-up bounds, reset, invalid inputs and fan projections.");
    }
}
