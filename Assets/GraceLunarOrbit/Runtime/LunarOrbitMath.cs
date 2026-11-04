using System;

namespace GraceLunarOrbit
{
    // SI units; double precision is retained until the Unity presentation boundary.
    public static class LunarOrbitMath
    {
        public const double MoonRadius = 1737400.0;
        public const double MoonMu = 4904869500000.0;

        public static double OrbitRadius(double moonRadius, double altitude)
        {
            if (double.IsNaN(moonRadius) || double.IsInfinity(moonRadius) || moonRadius <= 0.0 ||
                double.IsNaN(altitude) || double.IsInfinity(altitude) || altitude < 0.0)
                throw new ArgumentOutOfRangeException("moonRadius/altitude");
            return moonRadius + altitude;
        }

        public static double AngularSpeed(double mu, double orbitRadius)
        {
            if (double.IsNaN(mu) || double.IsInfinity(mu) || mu <= 0.0 ||
                double.IsNaN(orbitRadius) || double.IsInfinity(orbitRadius) || orbitRadius <= 0.0)
                throw new ArgumentOutOfRangeException("mu/orbitRadius");
            return Math.Sqrt(mu / (orbitRadius * orbitRadius * orbitRadius));
        }

        public static double OrbitalPeriod(double mu, double orbitRadius)
        {
            return 2.0 * Math.PI / AngularSpeed(mu, orbitRadius);
        }

        public static double SpinRate(double gravity, double habitatRadius)
        {
            if (double.IsNaN(gravity) || double.IsInfinity(gravity) || gravity < 0.0 ||
                double.IsNaN(habitatRadius) || double.IsInfinity(habitatRadius) || habitatRadius <= 0.0)
                throw new ArgumentOutOfRangeException("gravity/habitatRadius");
            return Math.Sqrt(gravity / habitatRadius);
        }

        public static double Angle(double initialRadians, double rateRadiansPerSecond, double seconds)
        {
            if (double.IsNaN(initialRadians) || double.IsInfinity(initialRadians) ||
                double.IsNaN(rateRadiansPerSecond) || double.IsInfinity(rateRadiansPerSecond) ||
                double.IsNaN(seconds) || double.IsInfinity(seconds))
                throw new ArgumentOutOfRangeException("angle/time");
            double turns = (initialRadians + rateRadiansPerSecond * seconds) / (2.0 * Math.PI);
            return (turns - Math.Floor(turns)) * (2.0 * Math.PI);
        }

        public static double OrbitX(double radius, double angle) { return radius * Math.Cos(angle); }
        public static double OrbitY(double radius, double angle) { return radius * Math.Sin(angle); }

        // Artistic preview weighting, not a reflected-irradiance calculation.
        public static double MoonFillWeight(double moonAngularRadius, double phaseCosine)
        {
            double size = 1.0 - Math.Cos(Math.Max(0.0, Math.Min(Math.PI / 2.0, moonAngularRadius)));
            double phase = 0.5 + 0.5 * Math.Max(-1.0, Math.Min(1.0, phaseCosine));
            return size * phase;
        }

        // Fraction of a uniformly bright solar disc visible behind the Moon.
        // Angles are radians. This is geometric occultation, not limb darkening.
        public static double SunVisibility(double separation, double moonAngularRadius, double sunAngularRadius)
        {
            double d = Math.Max(0.0, separation);
            double r = Math.Max(0.0, moonAngularRadius);
            double s = Math.Max(0.000000001, sunAngularRadius);
            if (d >= r + s) return 1.0;
            if (d <= Math.Abs(r - s))
                return r >= s ? 0.0 : 1.0 - r * r / (s * s);
            double a = Math.Acos(Math.Max(-1.0, Math.Min(1.0, (d*d + s*s - r*r) / (2.0*d*s))));
            double b = Math.Acos(Math.Max(-1.0, Math.Min(1.0, (d*d + r*r - s*s) / (2.0*d*r))));
            double area = s*s*a + r*r*b - 0.5 * Math.Sqrt(Math.Max(0.0,
                (-d+s+r)*(d+s-r)*(d-s+r)*(d+s+r)));
            return Math.Max(0.0, Math.Min(1.0, 1.0 - area / (Math.PI*s*s)));
        }
    }
}
