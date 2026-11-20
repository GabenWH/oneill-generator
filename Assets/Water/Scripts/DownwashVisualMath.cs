using System;

namespace Grace.VWaterOriginal
{
    public readonly struct DownwashSurfaceInputs
    {
        public readonly double Pressure,Foam;
        public DownwashSurfaceInputs(double pressureMagnitude,double foam)
        {
            Pressure=DownwashMath.Finite(pressureMagnitude)?-Math.Max(0,Math.Min(1,pressureMagnitude)):0;
            Foam=DownwashMath.Finite(foam)?Math.Max(0,Math.Min(1,foam)):0;
        }
    }

    public readonly struct DownwashResponseValues
    {
        public readonly double MistAmount, SprayHeight, WaveHeight, Pressure, Foam, MistTurbulence;
        public DownwashResponseValues(double mistAmount,double sprayHeight,double waveHeight,double pressure,double foam)
            :this(mistAmount,sprayHeight,waveHeight,pressure,foam,.35) {}
        public DownwashResponseValues(double mistAmount,double sprayHeight,double waveHeight,double pressure,double foam,double mistTurbulence)
        {
            MistAmount=Limit(mistAmount,8);SprayHeight=Limit(sprayHeight,3);WaveHeight=Limit(waveHeight,.5);
            Pressure=Limit(pressure,1);Foam=Limit(foam,1);MistTurbulence=Limit(mistTurbulence,1);
        }
        private static double Limit(double value,double max)
        { return DownwashMath.Finite(value)?Math.Max(0,Math.Min(max,value)):0; }
    }

    public sealed class DownwashEmissionBudget
    {
        private double remainder;
        public int Consume(double rate, double delta)
        {
            if (!DownwashMath.Finite(rate) || !DownwashMath.Finite(delta) || rate <= 0 || delta < 0)
            { Reset(); return 0; }
            remainder = Math.Min(120, remainder + rate * delta);
            int count = (int)Math.Floor(remainder + 1e-8);
            remainder = Math.Max(0, remainder - count);
            return count;
        }
        public void Reset() { remainder = 0; }
    }

    public readonly struct DownwashFanWave
    {
        public readonly DownwashMath.V3 Position;
        public readonly double Time,Amplitude;
        public DownwashFanWave(DownwashMath.V3 position,double time,double amplitude)
        { Position=position;Time=time;Amplitude=amplitude; }
    }

    public sealed class DownwashFanWaveEmitter
    {
        private readonly double[] lastSampleTime={double.NaN,double.NaN};
        public bool TryEmit(int fan,DownwashMath.V3 position,double amplitude,double delta,double now,double interval,out DownwashFanWave wave)
        {
            wave=default(DownwashFanWave);
            if(fan<0 || fan>=lastSampleTime.Length)return false;
            if(!DownwashMath.Finite(position.X+position.Y+position.Z+amplitude+delta+now+interval) || amplitude<=0 || delta<0 || interval<=0)
            { Reset(fan);return false; }
            // Both fans use the same simulation clock, half a cycle apart.
            // A returning footprint rejoins that clock without replaying missed
            // rings or shifting the other fan's phase.
            double rate=1/Math.Max(.25,interval),phase=fan*.5;
            double previousTime=DownwashMath.Finite(lastSampleTime[fan])?lastSampleTime[fan]:now-delta;
            lastSampleTime[fan]=now;
            if(delta==0 || now<=previousTime)return false;
            double cycle=Math.Floor(now*rate+phase+1e-8);
            // Observe the actual previous timestamp: Unity's float now-delta
            // can overlap/skip a boundary after rounding.
            double previousCycle=Math.Floor(previousTime*rate+phase+1e-8);
            if(cycle<=previousCycle)return false;
            wave=new DownwashFanWave(position,now,amplitude);return true;
        }
        public void Reset(int fan)
        { if(fan>=0 && fan<lastSampleTime.Length)lastSampleTime[fan]=double.NaN; }
    }

    public static class DownwashVisualMath
    {
        public static DownwashSurfaceInputs ResolveSurfaceInputs(bool curveMode,DownwashResponseValues response,
            double power,double manualPressure,double manualFoam,double attenuatedStrength,double gain)
        {
            if(curveMode)return new DownwashSurfaceInputs(response.Pressure*power,response.Foam*power);
            double legacyPower=attenuatedStrength*Math.Max(0,Math.Min(8,gain));
            return new DownwashSurfaceInputs(manualPressure*legacyPower,manualFoam*legacyPower);
        }
        // Undo the projection helper's height attenuation. User curves now own
        // height response; only projected thrust and overall gain remain here.
        public static double ProjectedPower(double attenuatedStrength,double height,double gain)
        {
            if(!DownwashMath.Finite(attenuatedStrength+height+gain) || height<=0)return 0;
            double scaled=height/1.2;
            return Math.Max(0,Math.Min(4,attenuatedStrength*(1+scaled*scaled)*Math.Max(0,Math.Min(8,gain))/2.7));
        }
        public struct Particle
        {
            public DownwashMath.V3 Position, Velocity;
            public double Life, Size;
        }

        public static double RelativeStrength(double attenuatedStrength, double gain)
        {
            if (!DownwashMath.Finite(attenuatedStrength) || !DownwashMath.Finite(gain)) return 0;
            return Math.Max(0, Math.Min(4, attenuatedStrength * gain / (.2566336633663366 * 3)));
        }

        public static Particle Sample(DownwashMath.Disc disc, double strength, double lift, double spread,
            double randomAngle, double randomRadius, double randomSpeed, bool mist)
        {
            if (!DownwashMath.Finite(strength + lift + spread + randomAngle + randomRadius + randomSpeed) || strength <= 0)
                return default(Particle);
            double angle = randomAngle * Math.PI * 2, x = Math.Cos(angle), z = Math.Sin(angle);
            double r = disc.Radius * (.35 + .5 * Math.Sqrt(Math.Max(0, Math.Min(1, randomRadius))));
            double force = Math.Sqrt(Math.Min(4, strength));
            double speed = (mist ? .5 + randomSpeed * .65 : 1.8 + randomSpeed * 1.4) * Math.Max(.1, spread) * force;
            double tangent = speed * .35;
            double life = mist ? 1.4 + randomSpeed * 1.1 : .4 + randomRadius * .4;
            double upward = mist ? Math.Max(0, lift) / life * force : Math.Sqrt(2 * 9.81 * .65 * Math.Max(0, lift)) * (.65 + randomSpeed * .25) * force;
            return new Particle {
                Position = new DownwashMath.V3(disc.Point.X + x*r, disc.Point.Y + .06, disc.Point.Z + z*r),
                Velocity = new DownwashMath.V3(x*speed - z*tangent, upward, z*speed + x*tangent),
                Life = life,
                Size = mist ? .24 + randomRadius * .44 : .025 + randomRadius * .04
            };
        }
    }
}
