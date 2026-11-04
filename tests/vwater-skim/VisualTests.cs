using System;
using System.Collections.Generic;
using System.Reflection;
using Grace.VWaterOriginal;

static class VisualTests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void CheckAlternatingWaves()
    {
        foreach(int fps in new[]{30,60,144})
        {
            var emitter=new DownwashFanWaveEmitter();
            for(int run=0;run<2;run++)
            {
                var times=new[]{new List<double>(),new List<double>()};
                for(int frame=1;frame<=fps*2;frame++)
                {
                    double now=frame/(double)fps;
                    bool left=emitter.TryEmit(0,new DownwashMath.V3(-.54,0,0),.11,1.0/fps,now,.4,out var leftWave);
                    bool right=emitter.TryEmit(1,new DownwashMath.V3(.54,0,0),.11,1.0/fps,now,.4,out var rightWave);
                    Check(!(left && right),"Fan rings fired together at "+fps+" FPS.");
                    if(left)
                    {
                        times[0].Add(leftWave.Time);
                        Check(!emitter.TryEmit(0,new DownwashMath.V3(-.54,0,0),.11,1.0/fps,now,.4,out var duplicate),"One fan emitted twice in the same frame.");
                    }
                    if(right)times[1].Add(rightWave.Time);
                }
                Check(times[0].Count==5 && times[1].Count==5,"Both fans must sustain five rings in two seconds.");
                for(int i=0;i<5;i++)
                {
                    Check(Math.Abs(times[0][i]-(.4+i*.4))<=1.0/fps+1e-8,"Left fan cadence drifted.");
                    Check(Math.Abs(times[1][i]-(.2+i*.4))<=1.0/fps+1e-8,"Right fan must fire halfway between left fan rings.");
                }
                emitter.Reset(0);emitter.Reset(1);
            }
            // A footprint returning mid-cycle must rejoin its assigned
            // phase instead of starting a new timer alongside the other fan.
            emitter.Reset(0);emitter.Reset(1);
            int resumedRings=0;
            for(int frame=1;frame<=fps*2;frame++)
            {
                double now=frame/(double)fps;
                bool left=emitter.TryEmit(0,new DownwashMath.V3(-.54,0,0),.11,1.0/fps,now,.4,out var leftWave);
                bool right=false;
                if(now<=.25)emitter.Reset(1);
                else right=emitter.TryEmit(1,new DownwashMath.V3(.54,0,0),.11,1.0/fps,now,.4,out var rightWave);
                Check(!(left && right),"A returning fan synchronized with the other fan at "+fps+" FPS.");
                if(right)
                {
                    Check(Math.Abs(now-(.6+resumedRings*.4))<=1.0/fps+1e-8,"Returning fan changed its assigned wave phase.");
                    resumedRings++;
                }
            }
            Check(resumedRings==4,"Returning fan must resume its scheduled rings without replaying missed ones.");
            // Unity passes float timestamps/deltas. Their rounded difference
            // must not duplicate a boundary or skip a later ring.
            emitter=new DownwashFanWaveEmitter();
            var floatTimes=new[]{new List<double>(),new List<double>()};
            for(int frame=1;frame<=fps*120;frame++)
            {
                double now=(float)(frame/(double)fps);
                for(int fan=0;fan<2;fan++)
                {
                    if(!emitter.TryEmit(fan,new DownwashMath.V3(fan,0,0),.11,(float)(1.0/fps),now,(float).4,out var wave))continue;
                    var emitted=floatTimes[fan];
                    if(emitted.Count>0)
                    {
                        double gap=wave.Time-emitted[emitted.Count-1];
                        Check(Math.Abs(gap-.4)<=1.0/fps+1e-5,"Unity float clock duplicated or skipped a wave at "+fps+" FPS.");
                    }
                    emitted.Add(wave.Time);
                }
            }
            foreach(var emitted in floatTimes)
                Check(emitted.Count>=299 && emitted.Count<=300,"Unity float clock changed sustained wave frequency.");
        }
    }
    static void Main()
    {
        CheckAlternatingWaves();
        var assembly = Assembly.GetExecutingAssembly();
        var budgetType = assembly.GetType("Grace.VWaterOriginal.DownwashEmissionBudget");
        Check(budgetType != null, "FAIL: airborne emission budget is missing; the current model has no spray.");
        var consume = budgetType.GetMethod("Consume");
        foreach (int fps in new[] { 30, 60, 144 })
        {
            object budget = Activator.CreateInstance(budgetType);
            int count = 0;
            for (int i = 0; i < fps; i++) count += (int)consume.Invoke(budget, new object[] { 60.0, 1.0/fps });
            Check(count == 60, "Emission depends on frame rate: " + fps + " FPS yielded " + count);
        }
        object bounded = Activator.CreateInstance(budgetType);
        Check((int)consume.Invoke(bounded,new object[]{1000.0,10.0}) <= 120, "Hitch produced an unbounded burst.");
        Check((int)consume.Invoke(bounded,new object[]{0.0,1.0}) == 0, "Disabled fan emitted.");
        Check((int)consume.Invoke(bounded,new object[]{60.0,0.0}) == 0, "Paused time emitted.");
        Check((int)consume.Invoke(bounded,new object[]{double.NaN,1.0}) == 0, "Invalid input emitted.");
        budgetType.GetMethod("Reset").Invoke(bounded,null);
        Check((int)consume.Invoke(bounded,new object[]{1.0,.1}) == 0, "Old emission credit replayed after reset.");
        var maths = assembly.GetType("Grace.VWaterOriginal.DownwashVisualMath");
        Check(maths != null, "Missing spray sampler.");
        var power=maths.GetMethod("ProjectedPower");
        Check(power!=null,"FAIL: curves cannot own height response while visual power also attenuates with height.");
        foreach(double height in new[]{.8,1.9,5.5})
        {
            double attenuated=.9/(1+(height/1.2)*(height/1.2));
            double actual=(double)power.Invoke(null,new object[]{attenuated,height,3.0});
            Check(Math.Abs(actual-1)<1e-8,"A second height falloff was applied after the curve at "+height);
        }
        Check((double)power.Invoke(null,new object[]{.1,1.9,0.0})==0,"Zero gain must stop fresh emissions.");
        Check((double)power.Invoke(null,new object[]{0.0,1.9,3.0})==0,"Zero thrust must stop fresh emissions.");
        Check((double)power.Invoke(null,new object[]{double.NaN,1.9,3.0})==0,"Invalid motor power must not reach particles.");
        var responseType=assembly.GetType("Grace.VWaterOriginal.DownwashResponseValues");
        Check(responseType!=null,"Missing validated curve outputs.");
        object response=Activator.CreateInstance(responseType,new object[]{3.38,.20,.11,.230970297,.423445545});
        Check((double)responseType.GetField("MistAmount").GetValue(response)==3.38,"Mist calibration changed.");
        Check((double)responseType.GetField("SprayHeight").GetValue(response)==.20,"Spray calibration changed.");
        Check((double)responseType.GetField("WaveHeight").GetValue(response)==.11,"Wave calibration changed.");
        var turbulence=responseType.GetField("MistTurbulence");
        Check(turbulence!=null,"FAIL: fixed mist turbulence has no independently authorable height response.");
        object calm=Activator.CreateInstance(responseType,new object[]{3.38,.20,.11,.230970297,.423445545,0.0});
        Check((double)turbulence.GetValue(calm)==0,"A zero turbulence key must disable noise strength.");
        object rough=Activator.CreateInstance(responseType,new object[]{3.38,.20,.11,.230970297,.423445545,10.0});
        Check((double)turbulence.GetValue(rough)==1,"Turbulence output must remain bounded.");
        object bad=Activator.CreateInstance(responseType,new object[]{double.NaN,-1.0,100.0,100.0,-1.0});
        Check((double)responseType.GetField("MistAmount").GetValue(bad)==0,"Invalid mist output must be zero.");
        Check((double)responseType.GetField("SprayHeight").GetValue(bad)==0,"Negative height must be zero.");
        Check((double)responseType.GetField("WaveHeight").GetValue(bad)==.5,"Wave displacement bound lost.");
        Check((double)responseType.GetField("Pressure").GetValue(bad)==1,"Pressure bound lost.");
        Check((double)responseType.GetField("Foam").GetValue(bad)==0,"Negative foam must be zero.");
        var resolve=maths.GetMethod("ResolveSurfaceInputs");
        Check(resolve!=null,"FAIL: manual surface fallback must clamp final inputs, not the intermediate curve values.");
        double atReference=.9/(1+(1.9/1.2)*(1.9/1.2));
        object inputs=resolve.Invoke(null,new object[]{false,response,1.0/6,.3,2.0,atReference,.5});
        double actualFoam=(double)inputs.GetType().GetField("Foam").GetValue(inputs);
        Check(Math.Abs(actualFoam-.2566336633663366)<1e-8,"Manual fallback clamped before applying gain.");
        var draftType=assembly.GetType("Grace.VWaterOriginal.Editor.DownwashCurveDraft");
        Check(draftType!=null,"FAIL: numeric key draft must synchronize Undo/external edits.");
        object draft=Activator.CreateInstance(draftType);var sync=draftType.GetMethod("Sync");
        sync.Invoke(draft,new object[]{1,1.9,response,false});
        var values=draftType.GetProperty("Values");
        ((double[])values.GetValue(draft))[0]=5.0; // Intentional unsaved numeric edit.
        sync.Invoke(draft,new object[]{1,1.9,response,false});
        Check(((double[])values.GetValue(draft))[0]==5.0,"Repaint erased a pending numeric edit.");
        object external=Activator.CreateInstance(responseType,new object[]{2.0,.20,.11,.230970297,.423445545});
        sync.Invoke(draft,new object[]{1,1.9,external,false});
        Check(((double[])values.GetValue(draft))[0]==2.0,"Undo/external edit left stale numeric values.");
        ((double[])values.GetValue(draft))[0]=6.0;
        object turbulenceOnly=Activator.CreateInstance(responseType,new object[]{2.0,.20,.11,.230970297,.423445545,.7});
        sync.Invoke(draft,new object[]{1,1.9,turbulenceOnly,false});
        Check(((double[])values.GetValue(draft))[0]==2.0 && ((double[])values.GetValue(draft))[5]==.7,"Turbulence-only external edits must refresh the six-channel draft.");
        var emitterType=assembly.GetType("Grace.VWaterOriginal.DownwashFanWaveEmitter");
        Check(emitterType!=null,"FAIL: waves still aggregate both fan footprints into one centre.");
        object emitter=Activator.CreateInstance(emitterType);
        var emitWave=emitterType.GetMethod("TryEmit");
        object[] left={0,new DownwashMath.V3(-.54,0,0),.11,.1,.1,.65,null};
        object[] right={1,new DownwashMath.V3(.54,0,0),.17,.1,.1,.65,null};
        Check(!(bool)emitWave.Invoke(emitter,left) && !(bool)emitWave.Invoke(emitter,right),"Wave cadence emitted too early.");
        left[3]=.6;left[4]=.7;
        Check((bool)emitWave.Invoke(emitter,left),"Left fan missed its scheduled ring.");
        var packetType=left[6].GetType();
        var leftPosition=(DownwashMath.V3)packetType.GetField("Position").GetValue(left[6]);
        Check(leftPosition.X==-.54,"Left ring must originate under the left fan.");
        emitterType.GetMethod("Reset").Invoke(emitter,new object[]{0});
        right[3]=.3;right[4]=.4;
        Check((bool)emitWave.Invoke(emitter,right),"Resetting one fan shifted the other fan's schedule.");
        var rightPosition=(DownwashMath.V3)packetType.GetField("Position").GetValue(right[6]);
        Check(rightPosition.X==.54 && (double)packetType.GetField("Amplitude").GetValue(right[6])==.17,"Right ring centre/height was averaged with the other fan.");
        Check(leftPosition.X==-.54,"A previously emitted ring moved with a later fan submission.");
        var sample = maths.GetMethod("Sample");
        DownwashMath.Disc disc;
        Check(DownwashMath.TryDisc(new DownwashMath.V3(-.54,1.9,0),new DownwashMath.V3(0,-1,0),new DownwashMath.V3(0,0,0),9,9,.9,out disc), "Fan did not hit.");
        for (int i=0;i<100;i++)
        {
            object particle=sample.Invoke(null,new object[]{disc,1.0,1.6,1.0,i/100.0,.5,.5,i%2==0});
            var type=particle.GetType();
            var position=(DownwashMath.V3)type.GetField("Position").GetValue(particle);
            var velocity=(DownwashMath.V3)type.GetField("Velocity").GetValue(particle);
            Check(position.Y>0 && velocity.Y>0,"Spray must start above the water and rise.");
            Check((position.X-disc.Point.X)*velocity.X+(position.Z-disc.Point.Z)*velocity.Z>0,"Spray must move outward.");
            Check(DownwashMath.Finite(velocity.X+velocity.Y+velocity.Z),"Nonfinite spray.");
        }
        Console.WriteLine("PASS: alternating per-fan waves/reset at 30/60/144 FPS, curve power/calibration/bounds, manual fallback, draft synchronization, emission timing and spray samples.");
    }
}
