using System;
using Grace.VWaterOriginal;
using V3=Grace.VWaterOriginal.DownwashMath.V3;

static class Program
{
    static int count;
    static void Check(bool okay,string why){count++;if(!okay)throw new Exception(why);}
    static void Near(double a,double b,double e,string why){Check(DownwashMath.Finite(a)&&Math.Abs(a-b)<=e,why+": "+a+" != "+b);}
    static void Main()
    {
        var surface=new V3(0,0,0);var down=new V3(0,-1,0);
        Check(DownwashMath.TryDisc(new V3(-.54,1.9,0),down,surface,3,3,.9,out var low),"Actual low fan ray must hit original world-Y plane");
        Near(low.Point.X,-.54,1e-12,"actual hub X");Near(low.Point.Y,0,1e-12,"surface intersection");Near(low.Height,1.9,1e-12,"actual fan height");Near(low.Radius,.742,1e-12,"height broadening");
        Near(low.Strength,.9/(1+Math.Pow(1.9/1.2,2)),1e-12,"visual falloff");
        Check(DownwashMath.TryDisc(new V3(-.54,5.5,0),down,surface,3,3,.9,out var high),"High fan should still hit");
        Check(low.Strength>high.Strength*4&&high.Radius>low.Radius,"Raise fan must weaken/broaden original AddFlow input");
        Check(DownwashMath.TryDisc(new V3(0,2,0),new V3(.2,-1,.3),surface,3,3,1,out var tilted),"Tilted fan ray");
        Near(tilted.Point.X,.4,1e-12,"moving footprint X");Near(tilted.Point.Z,.6,1e-12,"moving footprint Z");
        Check(tilted.Strength<1/(1+Math.Pow(2/1.2,2)),"Tilt attenuates downwash");
        Check(!DownwashMath.TryDisc(new V3(0,2,0),new V3(0,1,0),surface,3,3,1,out _),"Upward fan rejected");
        Check(!DownwashMath.TryDisc(new V3(0,2,0),new V3(1,-.01,0),surface,3,3,1,out _),"Grazing fan rejected");
        Check(!DownwashMath.TryDisc(new V3(0,-1,0),down,surface,3,3,1,out _),"Submerged fan rejected");
        Check(!DownwashMath.TryDisc(new V3(4,2,0),down,surface,3,3,1,out _),"Off-pond fan rejected");
        Check(!DownwashMath.TryDisc(new V3(3,2,0),down,surface,3,3,1,out _),"Strict footprint boundary");
        Check(!DownwashMath.TryDisc(new V3(0,2,0),down,surface,3,3,0,out _),"Fans-off submits nothing");
        foreach(double bad in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
        {
            Check(!DownwashMath.TryDisc(new V3(bad,2,0),down,surface,3,3,1,out _),"Bad origin");
            Check(!DownwashMath.TryDisc(new V3(0,2,0),new V3(0,bad,0),surface,3,3,1,out _),"Bad axis");
            Check(!DownwashMath.TryDisc(new V3(0,2,0),down,surface,bad,3,1,out _),"Bad domain");
            Check(!DownwashMath.TryDisc(new V3(0,2,0),down,surface,3,3,bad,out _),"Bad thrust");
        }
        for(int i=0;i<100;i++)
        {
            double x=Math.Sin(i*.37),z=Math.Cos(i*.23),h=1.6+i*.01;
            Check(DownwashMath.TryDisc(new V3(x,h,z),down,surface,3,3,.9,out var a),"Moving source hit");
            Check(DownwashMath.TryDisc(new V3(x+4000,h-2000,z+3000),down,new V3(4000,-2000,3000),3,3,.9,out var b),"Translated ray math");
            Near(a.Strength,b.Strength,1e-12,"relative height math");Near(a.Point.X,b.Point.X-4000,1e-12,"relative source X math");Near(a.Point.Z,b.Point.Z-3000,1e-12,"relative source Z math");
        }
        Console.WriteLine("PASS: "+count+" own fan-input math assertions. NOT Unity compile/Play/GPU or original-kernel execution.");
    }
}
