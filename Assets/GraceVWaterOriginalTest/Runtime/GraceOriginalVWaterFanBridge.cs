using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vertigo2;

namespace Grace.VWaterOriginal
{
    /// <summary>Drives the adapted VWater compute simulation from the two real fan footprints.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class GraceOriginalVWaterFanBridge : MonoBehaviour
    {
        public VWater water;
        public VWaterRenderer originalRenderer;
        public VWaterDynamicsManager originalDynamics;
        public Transform dronePlacement;
        public Transform[] fanHubs = new Transform[0];
        public Transform[] fanUpMarkers = new Transform[0];
        public Transform[] fanBlades = new Transform[0];
        public GraceOriginalVWaterCameraOrbit cameraOrbit;
        [Header("Visual downwash; tune in Play mode. Not a CFD model.")]
        public bool fansEnabled;
        public bool moveDrone;
        public bool showControls=true;
        public Rect ControlPanelRect { get { return showControls ? new Rect(12,12,590,435) : new Rect(12,12,220,30); } }
        [Min(.05f)] public float fanHeight=1.9f;
        [Range(0,1)] public float thrust=.9f;
        [Tooltip("Overall gain after curve outputs. At gain 3 and thrust 0.9, curve outputs are unscaled. Zero stops fresh input.")]
        [Range(0,8f)] public float responseGain=3f;
        [Range(0,1f)] public float sourceAmplitude=.3f;
        [Range(0,2f)] public float skimFoam=.55f;
        [Tooltip("Reject frame-to-frame footprint jumps larger than this, to avoid teleport streaks.")]
        public float maxFootprintSpeed=8f;
        [Header("Airborne spray and visible ring waves")]
        public bool sprayEnabled=true;
        public bool wavesEnabled=true;
        [Header("Height response curves")]
        public bool useHeightResponse=true;
        public DownwashResponseProfile responseProfile;
        [Header("Manual settings (used when curves are off)")]
        [Range(0,8)] public float mistAmount=3.38f;
        [Range(0,3)] public float sprayHeight=.20f;
        [Range(.2f,3)] public float spraySpread=1f;
        [Range(0,1)] public float mistTurbulence=.35f;
        [Range(0,.5f)] public float waveHeight=.11f;
        [Range(.3f,3)] public float waveSpeed=1.6f;
        [Range(.15f,.8f)] public float waveWidth=.35f;
        [Tooltip("Seconds between rings from each fan. The second fan is offset by half an interval; lower values emit more often.")]
        [Range(.25f,2)] public float waveInterval=.4f;
        [Range(1,8)] public float waveRadius=5f;
        public Shader sprayShader;
        public Shader waveShader;
        public int AirborneParticleCount { get { return visuals!=null?visuals.ParticleCount:0; } }
        public int ActiveWaveCount { get { return visuals!=null?visuals.WaveCount:0; } }
        private GraceDownwashVisuals visuals;
        private bool reportedVisuals;
        private float activeSince=-1;
        public int CallsSubmittedThisFrame { get; private set; }
        public int CallsAcceptedThisFrame { get; private set; }
        private readonly Vector3[] previousHits = new Vector3[2];
        private readonly bool[] hasPreviousHit = new bool[2];
        private Vector3 hubCenterOffset,waterPosition,waterScale;
        private Quaternion waterRotation;
        private float bladeAngle;
        private readonly List<BladeRest> rests=new List<BladeRest>();
        private string fault;
        private struct BladeRest { public Transform blade; public Vector3 position; public Quaternion rotation; public int fan; }

        public DownwashResponseValues EvaluateResponse(float actualFanHeight)
        {
            if(useHeightResponse && responseProfile!=null)return responseProfile.Evaluate(actualFanHeight);
            double h=actualFanHeight/1.2;
            double legacy=2.7/(1+h*h);
            return new DownwashResponseValues(mistAmount,sprayHeight,waveHeight,sourceAmplitude*legacy,skimFoam*legacy,mistTurbulence);
        }
        public DownwashSurfaceInputs EvaluateSurfaceInputs(DownwashMath.Disc hit,DownwashResponseValues response,double power)
        {
            return DownwashVisualMath.ResolveSurfaceInputs(useHeightResponse && responseProfile!=null,response,power,sourceAmplitude,skimFoam,hit.Strength,responseGain);
        }

        private void Start()
        {
            if(!ValidateBindings(out fault)){Debug.LogError(fault,this);enabled=false;return;}
            waterPosition=water.transform.position;waterRotation=water.transform.rotation;waterScale=water.transform.lossyScale;
            hubCenterOffset=((fanHubs[0].position+fanHubs[1].position)*.5f)-dronePlacement.position;
            foreach(Transform blade in fanBlades)
            {
                int nearest=(blade.position-fanHubs[0].position).sqrMagnitude<(blade.position-fanHubs[1].position).sqrMagnitude?0:1;
                rests.Add(new BladeRest{blade=blade,position=blade.localPosition,rotation=blade.localRotation,fan=nearest});
            }
            PlaceDrone();
            visuals=new GraceDownwashVisuals(this);
        }
        public bool ValidateBindings(out string problem)
        {
            problem=null;
            if(water==null||originalRenderer==null||originalDynamics==null||dronePlacement==null||fanHubs.Length!=2||fanUpMarkers.Length!=2||fanBlades.Length!=10)
            {problem="Original baseline requires original water/renderer/manager and actual two hubs, two axis markers and ten blades. No proxy fallback.";return false;}
            if(fanHubs.Any(v=>v==null)||fanUpMarkers.Any(v=>v==null)||fanBlades.Any(v=>v==null))
            {problem="Actual drone bindings contain missing objects.";return false;}
            if(Quaternion.Angle(water.transform.rotation,Quaternion.identity)>.01f)
            {problem="This ORIGINAL baseline is stationary world-Y water. Cylinder/pond rotation is not supported; no shader adaptation is hidden here.";return false;}
            return true;
        }
        private void PlaceDrone()
        {
            float t=Time.timeSinceLevelLoad;
            Vector3 fanCenter=water.transform.position+new Vector3(moveDrone ? .55f*Mathf.Sin(t*.55f) : 0,0,moveDrone ? .45f*Mathf.Cos(t*.41f) : 0);
            fanCenter.y=water.Position+fanHeight;
            dronePlacement.position=fanCenter-hubCenterOffset;
        }
        private void LateUpdate()
        {
            CallsSubmittedThisFrame=0; CallsAcceptedThisFrame=0;
            if(water==null||originalDynamics==null)return;
            if(water.transform.position!=waterPosition||Quaternion.Angle(water.transform.rotation,waterRotation)>.001f||water.transform.lossyScale!=waterScale)
            {fault="Baseline water moved/scaled/rotated: stop Play and restore stationary flat water. Original renderer is not a local-cylinder adapter.";Debug.LogError(fault,this);enabled=false;return;}
            PlaceDrone();
            if(visuals!=null)visuals.BeginFrame(Time.deltaTime,Time.time);
            if(fansEnabled)bladeAngle=Mathf.Repeat(bladeAngle+Time.deltaTime*thrust*5400,360);
            foreach(BladeRest rest in rests)
            {
                rest.blade.localPosition=rest.position;rest.blade.localRotation=rest.rotation;
                Vector3 axis=(fanUpMarkers[rest.fan].position-fanHubs[rest.fan].position).normalized;
                rest.blade.RotateAround(fanHubs[rest.fan].position,axis,bladeAngle);
            }
            if(!fansEnabled||Time.timeSinceLevelLoad<.5f)
            { hasPreviousHit[0]=hasPreviousHit[1]=false; activeSince=-1; if(visuals!=null)visuals.EndFrame(); return; }
            if(activeSince<0)activeSince=Time.time;
            Vector3 surface=water.transform.position;surface.y=water.Position;
            for(int i=0;i<2;i++)
            {
                Vector3 origin=fanHubs[i].position,jet=fanHubs[i].position-fanUpMarkers[i].position;
                if(DownwashMath.TryDisc(V(origin),V(jet),V(surface),water.bounds.size.x*.5,water.bounds.size.z*.5,thrust,out DownwashMath.Disc hit))
                {
                    Vector3 point=new Vector3((float)hit.Point.X,(float)hit.Point.Y,(float)hit.Point.Z);
                    Vector3 hitVelocity=Vector3.zero;
                    if(hasPreviousHit[i] && Time.deltaTime>0.00001f)
                    {
                        Vector3 delta=point-previousHits[i];
                        float speed=delta.magnitude/Time.deltaTime;
                        if(speed<=Mathf.Max(0.1f,maxFootprintSpeed)) hitVelocity=delta/Time.deltaTime;
                    }
                    previousHits[i]=point; hasPreviousHit[i]=true;
                    var response=EvaluateResponse((float)hit.Height);
                    double power=DownwashVisualMath.ProjectedPower(hit.Strength,hit.Height,responseGain);
                    var inputs=EvaluateSurfaceInputs(hit,response,power);
                    if(visuals!=null)visuals.Emit(i,hit,response,power);
                    if(VWaterDynamicsManager.instance==originalDynamics)
                    {
                        CallsSubmittedThisFrame++;
                        if(originalDynamics.AddSkim(point,(float)hit.Radius,(float)inputs.Pressure,hitVelocity,(float)inputs.Foam,i))
                            CallsAcceptedThisFrame++;
                    }
                }
                else hasPreviousHit[i]=false;
            }
            if(visuals!=null)visuals.EndFrame();
            if(!reportedVisuals && Time.time-activeSince>2)
            {
                reportedVisuals=true;
                Debug.Log("[Downwash running] Airborne particles="+AirborneParticleCount+", visible wave packets="+ActiveWaveCount+", surface inputs accepted/submitted="+CallsAcceptedThisFrame+"/"+CallsSubmittedThisFrame+". Particles and geometric waves use fan impacts; inspect the GPU separately for compute proof.",this);
                ReportGpuOnce();
            }
        }
        private void ReportGpuOnce()
        {
            var buffer=VWaterDynamicsManager.waterBuffer;
            if(buffer==null || VWaterDynamicsManager.instance!=originalDynamics || !SystemInfo.supportsAsyncGPUReadback)return;
            UnityEngine.Rendering.AsyncGPUReadback.Request(buffer,0,TextureFormat.RGBAFloat,request=>
            {
                if(request.hasError){Debug.LogWarning("[Downwash GPU] Readback failed; no compute success claimed.");return;}
                if(this==null || buffer==null || VWaterDynamicsManager.waterBuffer!=buffer)return;
                float peakHeight=0,peakFoam=0;int invalid=0;
                foreach(Color pixel in request.GetData<Color>())
                {
                    if(!DownwashMath.Finite(pixel.r+pixel.g+pixel.b+pixel.a)){invalid++;continue;}
                    peakHeight=Mathf.Max(peakHeight,Mathf.Abs(pixel.r));peakFoam=Mathf.Max(peakFoam,pixel.g);
                }
                Debug.Log("[Downwash GPU] Live compute buffer peak |height|="+peakHeight.ToString("F4")+", foam="+peakFoam.ToString("F4")+", nonfinite="+invalid+". Separate from the particle and geometric-wave visuals.",this);
            });
        }
        private void OnDisable(){if(visuals!=null)visuals.Clear();activeSince=-1;reportedVisuals=false;}
        private void OnDestroy(){if(visuals!=null){visuals.Dispose();visuals=null;}}
        private static DownwashMath.V3 V(Vector3 p){return new DownwashMath.V3(p.x,p.y,p.z);}
        private void OnGUI()
        {
            if(!showControls)
            {
                if(GUI.Button(ControlPanelRect,"Show downwash controls"))showControls=true;
                return;
            }
            GUI.Box(ControlPanelRect,"ADAPTED VWATER / Unity Built-in / flat world-Y test");
            if(GUI.Button(new Rect(480,16,110,22),"Hide controls"))showControls=false;
            GUI.Label(new Rect(24,40,565,22),"Fan downwash: rising mist, droplets and outward-moving wave crests.");
            if(GUI.Button(new Rect(24,70,128,30),fansEnabled?"Fans OFF":"Fans ON"))fansEnabled=!fansEnabled;
            if(GUI.Button(new Rect(160,70,128,30),moveDrone?"Hold drone":"Move drone"))moveDrone=!moveDrone;
            if(GUI.Button(new Rect(296,70,128,30),fanHeight<3?"Raise fans":"Lower fans"))fanHeight=fanHeight<3?5.5f:1.9f;
            if(GUI.Button(new Rect(432,70,150,30),cameraOrbit!=null&&cameraOrbit.autoOrbit?"Stop CAMERA orbit":"Orbit CAMERA"))if(cameraOrbit!=null)cameraOrbit.autoOrbit=!cameraOrbit.autoOrbit;
            bool keyword=originalRenderer!=null&&originalRenderer.GetComponent<Renderer>().sharedMaterial!=null&&originalRenderer.GetComponent<Renderer>().sharedMaterial.IsKeywordEnabled("DYNAMICS_ENABLED");
            GUI.Label(new Rect(24,110,560,22),"Fan height "+fanHeight.ToString("F1")+"m | skim accepted/submitted "+CallsAcceptedThisFrame+"/"+CallsSubmittedThisFrame+" | dynamic keyword "+keyword);
            GUI.Label(new Rect(24,137,560,22),"GPU dispatches "+(originalDynamics!=null?originalDynamics.TotalDispatches:0)+" | sources in last dispatch "+(originalDynamics!=null?originalDynamics.LastSkimSourcesDispatched:0));
            fanHeight=Slider(166,"Fan height (m)",fanHeight,.25f,8);
            if(GUI.Button(new Rect(24,194,270,25),useHeightResponse?"Height curves ON":"Manual settings"))useHeightResponse=!useHeightResponse;
            if(GUI.Button(new Rect(305,194,277,25),"Select curve controls"))
            {
#if UNITY_EDITOR
                UnityEditor.Selection.activeGameObject=gameObject;
#endif
            }
            if(useHeightResponse && responseProfile!=null)
            {
                var value=EvaluateResponse(fanHeight);
                GUI.Label(new Rect(24,224,560,22),"Curve outputs at "+fanHeight.ToString("F2")+" m (before thrust / gain):");
                GUI.Label(new Rect(24,250,560,22),"Mist "+value.MistAmount.ToString("F2")+" | Spray "+value.SprayHeight.ToString("F2")+" m | Waves "+value.WaveHeight.ToString("F2")+" m");
                GUI.Label(new Rect(24,278,560,22),"Pressure "+value.Pressure.ToString("F3")+" | Surface foam "+value.Foam.ToString("F3"));
                GUI.Label(new Rect(24,306,560,22),"Edit and save curve keys in the Inspector; the profile survives leaving Play.");
            }
            else
            {
                mistAmount=Slider(224,"Manual mist",mistAmount,0,8);
                sprayHeight=Slider(252,"Manual spray (m)",sprayHeight,0,3);
                waveHeight=Slider(280,"Manual wave (m)",waveHeight,0,.5f);
                GUI.Label(new Rect(24,306,560,22),"Manual pressure / foam and thrust / gain are in the Inspector.");
            }
            if(GUI.Button(new Rect(24,338,270,28),sprayEnabled?"Mist / droplets ON":"Mist / droplets OFF"))sprayEnabled=!sprayEnabled;
            if(GUI.Button(new Rect(305,338,277,28),wavesEnabled?"Ring waves ON":"Ring waves OFF"))wavesEnabled=!wavesEnabled;
            GUI.Label(new Rect(24,374,560,22),"Airborne particles "+AirborneParticleCount+" | travelling wave packets "+ActiveWaveCount);
            GUI.Label(new Rect(24,402,565,22),"Fans ON → Move → Raise. Right-drag/wheel camera; Hide controls for a clear view.");
            if(fault!=null)GUI.Label(new Rect(24,455,800,40),fault);
        }
        private static float Slider(float y,string label,float value,float min,float max)
        {
            GUI.Label(new Rect(24,y,185,22),label+" "+value.ToString("F2"));
            bool previousChange=GUI.changed;GUI.changed=false;
            EventType input=Event.current.type;
            float edited=GUI.HorizontalSlider(new Rect(215,y+6,365,20),value,min,max);
            bool changedHere=GUI.changed;GUI.changed|=previousChange;
            // A numeric Inspector value beyond the scrub range must not be
            // silently clamped by an unrelated Game-view repaint.
            bool interaction=input==EventType.MouseDown || input==EventType.MouseDrag || input==EventType.KeyDown || input==EventType.ScrollWheel;
            return changedHere && interaction?edited:value;
        }
    }
}
