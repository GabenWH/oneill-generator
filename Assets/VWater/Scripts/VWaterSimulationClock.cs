using System;

namespace Vertigo2
{
    /// <summary>Fixed simulation time, with bounded catch-up after a slow frame.</summary>
    public sealed class VWaterSimulationClock
    {
        private double remainder;

        public int Accumulate(float frameDelta, float rate, int maxSteps)
        {
            if (float.IsNaN(frameDelta) || float.IsInfinity(frameDelta) || frameDelta < 0 ||
                float.IsNaN(rate) || float.IsInfinity(rate) || rate <= 0 || maxSteps <= 0)
            {
                Reset();
                return 0;
            }
            double step = 1.0 / rate;
            // Drop excess time after a hitch instead of growing an unbounded backlog.
            remainder = Math.Min(remainder + frameDelta, step * maxSteps);
            int count = Math.Min(maxSteps, (int)Math.Floor((remainder + step * 1e-5) / step));
            remainder = Math.Max(0, remainder - count * step);
            return count;
        }

        public void Reset() { remainder = 0; }
    }
}
