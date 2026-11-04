using System;

namespace Grace.VWaterOriginal
{
    /// <summary>Own visual fan input, not a VWater/shader/solver replacement.</summary>
    public static class DownwashMath
    {
        public readonly struct V3
        {
            public readonly double X, Y, Z;
            public V3(double x, double y, double z) { X=x; Y=y; Z=z; }
        }
        public readonly struct Disc
        {
            public readonly V3 Point;
            public readonly double Height, Radius, Strength;
            public Disc(V3 point,double height,double radius,double strength)
            { Point=point; Height=height; Radius=radius; Strength=strength; }
        }
        public static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
        public static bool TryDisc(V3 origin,V3 direction,V3 surface,double halfX,double halfZ,double thrust,out Disc result)
        {
            result=default(Disc);
            if(!Valid(origin)||!Valid(direction)||!Valid(surface)||!Finite(halfX)||!Finite(halfZ)||halfX<=0||halfZ<=0||!Finite(thrust)||thrust<=0||thrust>1)return false;
            double length2=direction.X*direction.X+direction.Y*direction.Y+direction.Z*direction.Z;
            if(!Finite(length2)||length2<1e-18)return false;
            double length=Math.Sqrt(length2),dx=direction.X/length,dy=direction.Y/length,dz=direction.Z/length;
            double height=origin.Y-surface.Y;
            if(height<=0||height>100||dy>=-.15)return false;
            double travel=-height/dy;
            var hit=new V3(origin.X+dx*travel,surface.Y,origin.Z+dz*travel);
            if(!Valid(hit)||Math.Abs(hit.X-surface.X)>=halfX||Math.Abs(hit.Z-surface.Z)>=halfZ)return false;
            double h=height/1.2,strength=thrust*dy*dy/(1+h*h),radius=.4+.18*height;
            if(!Finite(strength)||!Finite(radius))return false;
            result=new Disc(hit,height,radius,strength);return true;
        }
        private static bool Valid(V3 v){return Finite(v.X)&&Finite(v.Y)&&Finite(v.Z);}
    }
}
