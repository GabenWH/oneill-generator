using System;
using System.Collections.Generic;
using Grace.VWaterOriginal;
using UnityEngine;
using Vertigo2;

/// <summary>Runs the custom VWater solver in a camera-following unwrapped cylinder chart.</summary>
[DefaultExecutionOrder(1200)]
public class CylinderWaterSimulation : MonoBehaviour
{
    public ONeillWorld world;
    public CylinderChunkManager chunks;
    public Transform follow;
    public DroneRig drone;
    public ComputeShader compute;
    public Texture2D flowMap;
    public Material surfaceMaterial;
    public Shader mistShader;
    public DownwashResponseProfile responseProfile;
    [Min(16)] public int bufferSize = 2048;
    [Min(8f)] public float universeSize = 320f;
    [Min(1f)] public float simulationRate = 60f;
    [Range(0f, 1f)] public float sprayVelocityInheritance = 1f;
    [Range(0f, 1f)] public float waveVelocityInheritance = .5f;
    [Min(.01f)] public float waveVelocityDrag = 1.5f;
    [Min(.05f)] public float foamHalfLife = .3f;
    [Min(0f)] public float airDrag = 5f;
    [Range(0f,4f)] public float responseGain = 1f;
    [Range(.2f,3f)] public float spraySpread = 1f;
    public float waveSpeed = 1.6f, waveWidth = .35f, waveRadius = 5f, waveInterval = .4f;
    public int EmittedMistParticles { get; private set; }
    public int EmittedDroplets { get; private set; }
    public bool DynamicsAvailable { get; private set; }
    public int TotalDispatches { get; private set; }
    public Vector2 UniversePosition { get; private set; }

    private RenderTexture waterBuffer, sourceBuffer, oldBuffer;
    private ComputeShader runtimeCompute;
    private Material detailMaterial, mistMaterial, dropMaterial;
    private Mesh detailMesh;
    private GameObject detailObject;
    private int waterKernel, offsetKernel;
    private bool initialized;
    private float lastAngle, unwrappedAngle;
    private readonly VWaterSimulationClock clock = new VWaterSimulationClock();
    private readonly Vector4[] flowPositions = new Vector4[4], skimPositions = new Vector4[4], skimVelocities = new Vector4[4];
    private readonly float[] flowRadii = new float[16], flowAmplitudes = new float[16];
    private readonly Vector4[] waveBuffer = new Vector4[64], waveMotionBuffer = new Vector4[64];
    private readonly List<Vector4> waves = new List<Vector4>();
    private readonly List<Vector4> waveMotions = new List<Vector4>();
    private readonly CylinderWakeTrail[] waveTrails = { new CylinderWakeTrail(), new CylinderWakeTrail() };
    private readonly List<Vector3> waveSamples = new List<Vector3>(32);
    private readonly Vector3[] previousHits = new Vector3[2];
    private readonly bool[] hasPreviousHit = new bool[2];
    private readonly Vector2[] sourceTargets = new Vector2[4], dispatchedPositions = new Vector2[4], simulationStarts = new Vector2[4];
    private readonly bool[] hasDispatchedPosition = new bool[4];
    private readonly Vector4[] flowPreviousPositions = new Vector4[4];
    private readonly DownwashEmissionBudget[] mistBudget = { new DownwashEmissionBudget(), new DownwashEmissionBudget() };
    private readonly ParticleSystem[] mist = new ParticleSystem[2];
    private Texture2D mistViewLookup, terrainHeightMap;
    private float[] terrainHeightSamples;
    private int terrainAround, terrainAlong;
    private float cachedTerrainAmplitude, cachedTerrainLength;
    private int previousViewHash;
    private bool viewLookupReady;
    private readonly DownwashEmissionBudget[] dropBudget = { new DownwashEmissionBudget(), new DownwashEmissionBudget() };
    private readonly ParticleSystem[] droplets = new ParticleSystem[2];
    private readonly System.Random particleRandom = new System.Random(41027);
    private readonly ParticleSystem.Particle[] sprayMotionBuffer = new ParticleSystem.Particle[2400];

    public static float UnwrapAngle(float reference, float angle)
        => reference + Mathf.DeltaAngle(reference * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad;

    public Vector2 SurfaceCoordinates(Vector3 localPosition)
    {
        float angle = UnwrapAngle(unwrappedAngle, Mathf.Atan2(localPosition.y, localPosition.x));
        return new Vector2(angle * world.WaterRadius, localPosition.z);
    }

    public void Initialize()
    {
        if (initialized || world == null || surfaceMaterial == null || follow == null) return;
        initialized = true;
        Vector3 local = world.transform.InverseTransformPoint(follow.position);
        lastAngle = unwrappedAngle = Mathf.Atan2(local.y, local.x);
        UniversePosition = SurfaceCoordinates(local);
        CreateTerrainHeightMap();
        CreateDetailSurface();
        CreateMist();
        DynamicsAvailable = compute != null && SystemInfo.supportsComputeShaders &&
            SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf);
        if (DynamicsAvailable)
        {
            runtimeCompute = Instantiate(compute);
            bufferSize = Mathf.Clamp(bufferSize, 32, 2048);
            waterBuffer = CreateBuffer("Cylinder VWater");
            sourceBuffer = CreateBuffer("Cylinder VWater source");
            oldBuffer = CreateBuffer("Cylinder VWater history");
            waterKernel = runtimeCompute.FindKernel("CSWater");
            offsetKernel = runtimeCompute.FindKernel("CSOffsetBuffers");
            runtimeCompute.SetInt("BUFFER_SIZE", bufferSize);
            runtimeCompute.SetFloat("worldSize", universeSize);
            runtimeCompute.SetFloat("flowScale", 10f);
            runtimeCompute.SetTexture(waterKernel, "WaterBuffer", waterBuffer);
            runtimeCompute.SetTexture(waterKernel, "SourceBuffer", sourceBuffer);
            runtimeCompute.SetTexture(waterKernel, "OldBuffer", oldBuffer);
            runtimeCompute.SetTexture(waterKernel, "_FlowMap", flowMap != null ? flowMap : Texture2D.grayTexture);
        }
        else Debug.Log("Cylinder water: GPU dynamics unavailable; animated surface and authored downwash rings remain enabled.", this);
        Publish(surfaceMaterial, false);
        Publish(detailMaterial, true);
    }
    public bool TryGetSurfaceRadius(ONeillWorld world, float angle,
    float z, out float surfaceRadius)
    {
        surfaceRadius = 0f;
        if (chunks == null || chunks.angularChunks <= 0 ||
            Mathf.Abs(z) >= world.length * .5f) return false;

        // Later, check bounded pond and river surfaces here.
        float turn = 2f * Mathf.PI;
        angle = Mathf.Repeat(angle, turn);
        int count = chunks.angularChunks;
        int sector = Mathf.Min(count - 1, Mathf.FloorToInt(angle / turn * count));
        if (!chunks.IsLand(sector) || TerrainHeightAt(angle, z) >= world.seaLevel)
            return false;

        surfaceRadius = SurfaceRadiusAtAngle(angle);
        return true;
    }

    private RenderTexture CreateBuffer(string label)
    {
        var buffer = new RenderTexture(bufferSize, bufferSize, 0, RenderTextureFormat.ARGBHalf)
            { name = label, enableRandomWrite = true, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        buffer.Create();
        Clear(buffer);
        return buffer;
    }

    private static void Clear(RenderTexture buffer)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = buffer;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = previous;
    }

    private void LateUpdate()
    {
        if (!initialized) Initialize();
        if (!initialized || follow == null || world == null) return;
        EnsureTerrainHeightMap();
        Vector3 local = world.transform.InverseTransformPoint(follow.position);
        float angle = Mathf.Atan2(local.y, local.x);
        unwrappedAngle += Mathf.DeltaAngle(lastAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        lastAngle = angle;
        float cell = universeSize / bufferSize;
        Vector2 target = new Vector2(Mathf.Round(unwrappedAngle * world.WaterRadius / cell) * cell,
            Mathf.Round(local.z / cell) * cell);
        Vector2 shift = target - UniversePosition;
        if (shift.sqrMagnitude > cell * cell * .1f)
        {
            if (DynamicsAvailable)
            {
                if (Mathf.Abs(shift.x) >= universeSize || Mathf.Abs(shift.y) >= universeSize)
                { Clear(waterBuffer); Clear(sourceBuffer); Clear(oldBuffer); }
                else { Offset(waterBuffer, shift); Offset(oldBuffer, shift); }
            }
            UniversePosition = target;
        }
        Array.Clear(flowRadii, 0, flowRadii.Length);
        Array.Clear(flowAmplitudes, 0, flowAmplitudes.Length);
        Array.Clear(skimPositions, 0, skimPositions.Length);
        Array.Clear(skimVelocities, 0, skimVelocities.Length);
        UpdateViewOpacity();
        UpdateSprayMotion();
        SubmitFans();
        for (int i = waves.Count - 1; i >= 0; i--)
            if (Time.time - waves[i].z > (waveRadius + waveWidth * 2f) / Mathf.Max(.3f, waveSpeed)) { waves.RemoveAt(i); waveMotions.RemoveAt(i); }
        while (waves.Count > waveBuffer.Length) { waves.RemoveAt(0); waveMotions.RemoveAt(0); }
        Array.Clear(waveBuffer, 0, waveBuffer.Length);
        Array.Clear(waveMotionBuffer, 0, waveMotionBuffer.Length);
        waves.CopyTo(waveBuffer);
        waveMotions.CopyTo(waveMotionBuffer);
        if (DynamicsAvailable)
        {
            runtimeCompute.SetVector("worldPos", new Vector4(UniversePosition.x, UniversePosition.y, 0, 0));
            runtimeCompute.SetFloat("time", Time.time);
            runtimeCompute.SetFloat("foamDecayRate", Mathf.Log(2f) / Mathf.Max(.05f, foamHalfLife));
            runtimeCompute.SetFloats("fsRadius", flowRadii);
            runtimeCompute.SetFloats("fsAmp", flowAmplitudes);
            runtimeCompute.SetVectorArray("skimVelocityFoam", skimVelocities);
            int steps = clock.Accumulate(Time.deltaTime, simulationRate, 4);
            runtimeCompute.SetFloat("deltaTime", 1f / simulationRate);
            for (int fan = 0; fan < 4; fan++)
            {
                if (skimVelocities[fan].w <= 0f) hasDispatchedPosition[fan] = false;
                simulationStarts[fan] = hasDispatchedPosition[fan] &&
                    Vector2.Distance(dispatchedPositions[fan], sourceTargets[fan]) < universeSize * .5f
                    ? dispatchedPositions[fan] : sourceTargets[fan];
            }
            for (int step = 0; step < steps; step++)
            {
                for (int fan = 0; fan < 4; fan++)
                {
                    Vector2 from = Vector2.Lerp(simulationStarts[fan], sourceTargets[fan], step / (float)steps);
                    Vector2 to = Vector2.Lerp(simulationStarts[fan], sourceTargets[fan], (step + 1f) / steps);
                    flowPreviousPositions[fan] = new Vector4(from.x, from.y, 0, 0);
                    flowPositions[fan] = new Vector4(to.x, to.y, 0, 0);
                    Vector4 skim = skimPositions[fan]; skim.x = to.x; skim.y = to.y; skimPositions[fan] = skim;
                }
                runtimeCompute.SetVectorArray("fsPos", flowPositions);
                runtimeCompute.SetVectorArray("fsPreviousPos", flowPreviousPositions);
                runtimeCompute.SetVectorArray("skimPositionRadiusAmplitude", skimPositions);
                Graphics.Blit(waterBuffer, sourceBuffer);
                // Scrolling dispatches bind these same UAV names to other targets.
                // Rebind the solver's complete set before every simulation dispatch.
                runtimeCompute.SetTexture(waterKernel, "WaterBuffer", waterBuffer);
                runtimeCompute.SetTexture(waterKernel, "SourceBuffer", sourceBuffer);
                runtimeCompute.SetTexture(waterKernel, "OldBuffer", oldBuffer);
                runtimeCompute.Dispatch(waterKernel, Mathf.CeilToInt(bufferSize / 32f), Mathf.CeilToInt(bufferSize / 32f), 1);
                Graphics.Blit(sourceBuffer, oldBuffer);
                TotalDispatches++;
            }
            if (steps > 0)
                for (int fan = 0; fan < 4; fan++)
                {
                    dispatchedPositions[fan] = sourceTargets[fan];
                    hasDispatchedPosition[fan] = skimVelocities[fan].w > 0f;
                }
        }
        UpdateDetailBounds();
        Publish(surfaceMaterial, false);
        Publish(detailMaterial, true);
        if (dropMaterial != null)
        {
            dropMaterial.SetMatrix("_CylinderWorldToLocal", world.transform.worldToLocalMatrix);
            dropMaterial.SetFloat("_CylinderRadius", world.WaterRadius);
            dropMaterial.SetFloat("_CylinderAngularStep", Mathf.PI*2/(chunks.angularChunks*chunks.angularResolution));
        }
        if (mistMaterial != null)
        {
            mistMaterial.SetMatrix("_CylinderWorldToLocal", world.transform.worldToLocalMatrix);
            mistMaterial.SetFloat("_CylinderRadius", world.WaterRadius);
            mistMaterial.SetFloat("_CylinderAngularStep", Mathf.PI*2/(chunks.angularChunks*chunks.angularResolution));
        }
    }

    private void Offset(RenderTexture buffer, Vector2 shift)
    {
        Graphics.Blit(buffer, sourceBuffer);
        runtimeCompute.SetVector("offsetVector", new Vector4(shift.x, shift.y, 0, 0));
        runtimeCompute.SetTexture(offsetKernel, "SourceBuffer", sourceBuffer);
        runtimeCompute.SetTexture(offsetKernel, "WaterBuffer", buffer);
        runtimeCompute.Dispatch(offsetKernel, Mathf.CeilToInt(bufferSize / 32f), Mathf.CeilToInt(bufferSize / 32f), 1);
    }

    public bool TryFanHit(Transform hub, Vector3 direction, out Vector3 localHit, out float distance)
    {
        localHit = Vector3.zero; distance = 0f;
        if (hub == null || chunks == null || world == null) return false;
        Vector3 origin = world.transform.InverseTransformPoint(hub.position);
        Vector3 ray = world.transform.InverseTransformDirection(direction).normalized;
        float a = ray.x * ray.x + ray.y * ray.y;
        float b = 2f * (origin.x * ray.x + origin.y * ray.y);
        if (a < .000001f) return false;
        float radius = SurfaceRadiusAtAngle(Mathf.Atan2(origin.y,origin.x));
        float travel = 0f;
        for (int iteration = 0; iteration < 3; iteration++)
        {
            float c = origin.x * origin.x + origin.y * origin.y - radius * radius;
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f || c >= 0f) return false;
            travel = (-b + Mathf.Sqrt(discriminant)) / (2f * a);
            if (travel <= 0f || travel > 100f) return false;
            localHit = origin + ray * travel;
            radius = SurfaceRadiusAtAngle(Mathf.Atan2(localHit.y,localHit.x));
        }
        float angle = Mathf.Repeat(Mathf.Atan2(localHit.y, localHit.x), Mathf.PI * 2f);
        float step = Mathf.PI * 2f / (chunks.angularChunks * chunks.angularResolution);
        float mid = (Mathf.Floor(angle / step) + .5f) * step;
        distance = Vector3.Dot(localHit - origin,new Vector3(Mathf.Cos(mid),Mathf.Sin(mid),0));
        if (distance <= 0f || distance > 30f) return false;
        int angularIndex = Mathf.FloorToInt(angle / (Mathf.PI * 2f) * chunks.angularChunks);
        return Mathf.Abs(localHit.z) < world.length * .5f && chunks.IsLand(angularIndex) &&
            TerrainHeightAt(angle, localHit.z) < world.seaLevel;
    }

    public float SurfaceRadiusAtAngle(float angle)
    {
        float step = Mathf.PI * 2f / (chunks.angularChunks * chunks.angularResolution);
        float offset = Mathf.Repeat(angle,step) - step*.5f;
        return world.WaterRadius * Mathf.Cos(step*.5f) / Mathf.Cos(offset);
    }

    public float TerrainHeightAt(float angle, float z)
    {
        EnsureTerrainHeightMap();
        if (terrainHeightSamples == null) return world.SampleTerrainHeight(angle,z);
        float around=Mathf.Repeat(angle/(Mathf.PI*2f),1f)*terrainAround;
        float along=Mathf.Clamp01(z/world.length+.5f)*terrainAlong;
        int a=Mathf.FloorToInt(around), row=Mathf.Min(Mathf.FloorToInt(along),terrainAlong-1),next=(a+1)%terrainAround;
        float u=around-a,v=along-row;
        float h00=terrainHeightSamples[row*terrainAround+a],h10=terrainHeightSamples[row*terrainAround+next];
        float h01=terrainHeightSamples[(row+1)*terrainAround+a],h11=terrainHeightSamples[(row+1)*terrainAround+next];
        return u<=v ? h00*(1-v)+h01*(v-u)+h11*u : h00*(1-u)+h10*(u-v)+h11*v;
    }

    private void SubmitFans()
    {
        if (drone == null || drone.fanPower <= 0f)
        {
            for (int fan = 0; fan < 2; fan++) ResetFan(fan);
            return;
        }
        Rigidbody body = drone.GetComponent<Rigidbody>();
        for (int fan = 0; fan < Mathf.Min(2, drone.fanHubs.Length); fan++)
        {
            Transform hub = drone.fanHubs[fan];
            if (hub == null) { ResetFan(fan); continue; }
            Vector3 up = fan < drone.fanUpMarkers.Length && drone.fanUpMarkers[fan] != null
                ? drone.fanUpMarkers[fan].position - hub.position : drone.transform.up;
            if (!TryFanHit(hub, -up, out Vector3 hit, out float height)) { ResetFan(fan); continue; }
            Vector2 point = SurfaceCoordinates(hit);
            if (Mathf.Abs(point.x - UniversePosition.x) > universeSize * .5f ||
                Mathf.Abs(point.y - UniversePosition.y) > universeSize * .5f) { ResetFan(fan); continue; }
            var response = EvaluateResponse(height);
            Vector3 inward = -new Vector3(hit.x, hit.y, 0).normalized;
            float projection = Mathf.Max(0f, Vector3.Dot(world.transform.InverseTransformDirection(up).normalized, inward));
            // Match vwatertest: curves alone own height response. Thrust/gain only scale their outputs.
            float gain = Mathf.Clamp(drone.fanPower * responseGain * projection * projection, 0f, 4f);
            float radius = Mathf.Clamp(.4f + height * .18f, .1f, 5f);
            flowPositions[fan] = new Vector4(point.x, point.y, 0, 0);
            sourceTargets[fan] = point;
            flowRadii[fan * 4] = radius;
            flowAmplitudes[fan * 4] = -(float)response.Pressure * gain;
            skimPositions[fan] = new Vector4(point.x, point.y, radius, -(float)response.Pressure * gain * .25f);
            Vector3 velocity = body != null ? body.GetPointVelocity(hub.position) : Vector3.zero;
            Vector3 localVelocity = world.transform.InverseTransformDirection(velocity);
            Vector3 tangent = new Vector3(-hit.y, hit.x, 0).normalized;
            Vector2 surfaceVelocity = new Vector2(Vector3.Dot(localVelocity, tangent), localVelocity.z);
            skimVelocities[fan] = new Vector4(surfaceVelocity.x, surfaceVelocity.y, (float)response.Foam * gain, 1);
            float spacing = Mathf.Max(radius * 2f, surfaceVelocity.magnitude * .08f);
            waveTrails[fan].Sample(point, Time.time, spacing, waveSamples, waveInterval);
            foreach (Vector3 sample in waveSamples)
            {
                waves.Add(new Vector4(sample.x, sample.y, sample.z, (float)response.WaveHeight * Mathf.Sqrt(gain)));
                // Arc/Z velocity, damping rate, and fan identity for diagnostics.
                waveMotions.Add(new Vector4(surfaceVelocity.x * waveVelocityInheritance,
                    surfaceVelocity.y * waveVelocityInheritance, waveVelocityDrag, fan));
            }
            Vector3 inheritedVelocity = Vector3.ProjectOnPlane(localVelocity, inward) * sprayVelocityInheritance;
            Vector3 previous = hasPreviousHit[fan] && Time.deltaTime <= .25f &&
                Vector3.Distance(previousHits[fan], hit) < 32f ? previousHits[fan] : hit;
            previousHits[fan] = hit; hasPreviousHit[fan] = true;
            EmitSpray(fan, false, previous, hit, tangent, inward, inheritedVelocity, height, radius, response, gain);
            EmitSpray(fan, true, previous, hit, tangent, inward, inheritedVelocity, height, radius, response, gain);
        }
    }

    private void ResetFan(int fan)
    {
        waveTrails[fan].Reset();
        mistBudget[fan].Reset();
        dropBudget[fan].Reset();
        hasPreviousHit[fan] = false;
        hasDispatchedPosition[fan] = false;
    }

    public DownwashResponseValues EvaluateResponse(float height)
        => responseProfile != null ? responseProfile.Evaluate(height) : new DownwashResponseValues(3.38,.2,.11,.23,.42);

    private void EmitSpray(int fan, bool drop, Vector3 previous, Vector3 hit, Vector3 tangent, Vector3 inward,
        Vector3 inherited, float height, float radius, DownwashResponseValues response, float power)
    {
        var system = drop ? droplets[fan] : mist[fan];
        var budget = drop ? dropBudget[fan] : mistBudget[fan];
        if (system == null) { budget.Reset(); return; }
        if (!drop) { var noise = system.noise; noise.strength = (float)response.MistTurbulence; }
        if (power <= 0 || response.MistAmount <= 0) { budget.Reset(); return; }
        double rate=(drop ? 180f : 60f)*power*response.MistAmount;
        int count=0,steps=Mathf.Max(1,Mathf.CeilToInt(Time.deltaTime*120f));
        // Keep the original per-submission cap without losing authored rates
        // when rendering stalls for several simulation frames.
        for(int step=0;step<steps;step++)count+=budget.Consume(rate,Time.deltaTime/steps);
        var disc = new DownwashMath.Disc(new DownwashMath.V3(0,0,0), height, radius, power);
        for (int n = 0; n < count; n++)
        {
            var sample = DownwashVisualMath.Sample(disc, power, response.SprayHeight, spraySpread,
                particleRandom.NextDouble(), particleRandom.NextDouble(), particleRandom.NextDouble(), !drop);
            float fraction = (n + .5f) / count, age = Time.deltaTime * (1f - fraction);
            float lift=CompensatedLift((float)sample.Velocity.Y,(float)sample.Life,drop);
            Vector3 velocity = inherited + tangent * (float)sample.Velocity.X + inward * lift + Vector3.forward * (float)sample.Velocity.Z;
            float decay = Mathf.Exp(-airDrag * age);
            float travel = airDrag > .001f ? (1f - decay) / airDrag : age;
            var particle = new ParticleSystem.EmitParams {
                position = Vector3.Lerp(previous, hit, fraction) + tangent * (float)sample.Position.X +
                    inward * (float)sample.Position.Y + Vector3.forward * (float)sample.Position.Z + velocity * travel,
                velocity = velocity * decay, startSize = (float)sample.Size,
                startLifetime = Mathf.Max(.05f, (float)sample.Life - age),
                startColor = new Color(.9f,.96f,1f,drop?.8f:.32f), rotation = (float)particleRandom.NextDouble()*360f
            };
            system.Emit(particle, 1);
        }
        if (drop) EmittedDroplets += count; else EmittedMistParticles += count;
    }

    private float CompensatedLift(float original, float life, bool drop)
    {
        float drag=Mathf.Max(0,airDrag);
        if(drag<.001f || original<=0)return original;
        // Keep the authored rise setting while adding air resistance.
        if(!drop)return original*life*drag/Mathf.Max(.001f,1-Mathf.Exp(-drag*life));
        float target=original*original/(2*9.81f*.65f),gravity=Mathf.Max(.001f,world.surfaceGravity*.65f);
        float speed=Mathf.Sqrt(2*gravity*target)+drag*target;
        for(int i=0;i<6;i++)
        {
            float height=speed/drag-gravity/(drag*drag)*Mathf.Log(1+drag*speed/gravity);
            float derivative=Mathf.Max(.0001f,speed/(gravity+drag*speed));
            speed=Mathf.Max(0,speed-(height-target)/derivative);
        }
        return speed;
    }

    private void UpdateSprayMotion()
    {
        float delta=Time.deltaTime,drag=Mathf.Max(0,airDrag),decay=Mathf.Exp(-drag*delta);
        float accelerationTime=drag>.001f ? (1-decay)/drag : delta;
        for (int fan = 0; fan < 2; fan++)
        {
            for(int layer=0;layer<2;layer++)
            {
                var system=layer==0 ? mist[fan] : droplets[fan];
                if(system==null)continue;
                int count=system.GetParticles(sprayMotionBuffer);
                for(int i=0;i<count;i++)
                {
                    var particle=sprayMotionBuffer[i];
                    Vector3 acceleration=Vector3.zero;
                    if(layer==1)
                        acceleration=world.transform.InverseTransformDirection(world.GetCentrifugalAcceleration(
                            world.transform.TransformPoint(particle.position)))*.65f;
                    particle.velocity=particle.velocity*decay+acceleration*accelerationTime;
                    sprayMotionBuffer[i]=particle;
                }
                system.SetParticles(sprayMotionBuffer,count);
            }
        }
    }

    private void CreateTerrainHeightMap()
    {
        DisposeObject(terrainHeightMap);
        int around = chunks.angularChunks * chunks.angularResolution;
        int along = chunks.longitudinalChunks * chunks.longitudinalResolution;
        terrainAround=around;terrainAlong=along;
        cachedTerrainAmplitude=world.terrainAmplitude;cachedTerrainLength=world.length;
        terrainHeightMap = new Texture2D(around, along + 1, TextureFormat.RFloat, false, true)
            { name = "Water shared terrain grid", filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
        var heights = new float[around * (along + 1)];
        terrainHeightSamples=heights;
        for (int z = 0; z <= along; z++)
            for (int a = 0; a < around; a++)
                heights[z * around + a] = world.SampleTerrainHeight(a * (Mathf.PI * 2f / around),
                    world.length * (z / (float)along - .5f));
        terrainHeightMap.SetPixelData(heights,0); terrainHeightMap.Apply(false, false);
    }

    private void EnsureTerrainHeightMap()
    {
        if(!initialized || world==null || chunks==null)return;
        if(terrainHeightMap==null || terrainAround!=chunks.angularChunks*chunks.angularResolution ||
            terrainAlong!=chunks.longitudinalChunks*chunks.longitudinalResolution ||
            cachedTerrainAmplitude!=world.terrainAmplitude || cachedTerrainLength!=world.length)
            CreateTerrainHeightMap();
    }

    private void UpdateDetailBounds()
    {
        if (detailMesh == null) return;
        float angle = UniversePosition.x / world.WaterRadius;
        Vector3 center = new Vector3(Mathf.Cos(angle) * world.WaterRadius, Mathf.Sin(angle) * world.WaterRadius, UniversePosition.y);
        detailMesh.bounds = new Bounds(center, Vector3.one * (universeSize + 4f));
    }

    private void UpdateViewOpacity()
    {
        if (mistViewLookup == null) return;
        var profile = responseProfile;
        bool enabled = profile == null || profile.useMistViewFade;
        var curve = profile != null ? profile.mistOpacityByViewAngle : null;
        int hash = enabled ? 1 : 0;
        unchecked
        {
            if (curve != null)
            {
                hash = hash * 31 + (int)curve.preWrapMode; hash = hash * 31 + (int)curve.postWrapMode;
                foreach (var key in curve.keys)
                {
                    hash = hash * 31 + key.time.GetHashCode(); hash = hash * 31 + key.value.GetHashCode();
                    hash = hash * 31 + key.inTangent.GetHashCode(); hash = hash * 31 + key.outTangent.GetHashCode();
                    hash = hash * 31 + key.inWeight.GetHashCode(); hash = hash * 31 + key.outWeight.GetHashCode();
                    hash = hash * 31 + (int)key.weightedMode;
                }
            }
        }
        if (viewLookupReady && hash == previousViewHash) return;
        for (int i = 0; i < 256; i++)
        {
            float opacity = enabled && curve != null ? curve.Evaluate(i * 90f / 255f) : 1f;
            if (float.IsNaN(opacity) || float.IsInfinity(opacity)) opacity = 0;
            mistViewLookup.SetPixel(i, 0, new Color(Mathf.Clamp01(opacity),0,0,1));
        }
        mistViewLookup.Apply(false, false); previousViewHash = hash; viewLookupReady = true;
    }

    private void Publish(Material material, bool detail)
    {
        material.SetMatrix("_CylinderWorldToLocal", world.transform.worldToLocalMatrix);
        material.SetMatrix("_CylinderLocalToWorld", world.transform.localToWorldMatrix);
        material.SetFloat("_CylinderRadius", world.WaterRadius);
        material.SetFloat("_CylinderLength", world.length);
        material.SetTexture("_TerrainHeightMap", terrainHeightMap);
        material.SetVector("_TerrainGrid", new Vector4(chunks.angularChunks * chunks.angularResolution,
            chunks.longitudinalChunks * chunks.longitudinalResolution, world.seaLevel, 0));
        material.SetFloat("_LandStripes", chunks.landStripes);
        material.SetFloat("_CylinderAngularStep", Mathf.PI * 2f / (chunks.angularChunks * chunks.angularResolution));
        material.SetFloat("_WaveBufferWorldSize", universeSize);
        material.SetVector("_WaveBufferWorldPos", new Vector4(UniversePosition.x, UniversePosition.y, 0, 0));
        material.SetFloat("_DynamicsEnabled", DynamicsAvailable ? 1f : 0f);
        material.SetTexture("_WaveBuffer", DynamicsAvailable ? (Texture)waterBuffer : Texture2D.blackTexture);
        material.SetFloat("_IsDetailPatch", detail ? 1f : 0f);
        material.SetFloat("_DetailPatchEnabled", isActiveAndEnabled ? 1f : 0f);
        material.SetInt("_DownwashWaveCount", waves.Count);
        material.SetVectorArray("_DownwashWaves", waveBuffer);
        material.SetVectorArray("_DownwashWaveMotion", waveMotionBuffer);
        material.SetVector("_DownwashWaveShape", new Vector4(waveSpeed, waveWidth, .35f, waveRadius));
        material.SetFloat("_DownwashVisualTime", Time.time);
    }

    private void CreateDetailSurface()
    {
        detailMesh = CylinderWaterSurface.BuildDetailMesh(universeSize);
        UpdateDetailBounds();
        detailObject = new GameObject("VWater Detail Surface", typeof(MeshFilter), typeof(MeshRenderer));
        detailObject.transform.SetParent(world.transform, false);
        detailObject.GetComponent<MeshFilter>().sharedMesh = detailMesh;
        detailMaterial = new Material(surfaceMaterial) { name = "Cylinder water detail (runtime)" };
        var renderer = detailObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = detailMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
    }

    private void CreateMist()
    {
        if (mistShader == null) return;
        mistMaterial = new Material(mistShader);
        mistViewLookup = new Texture2D(256, 1, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp };
        mistViewLookup.Apply();
        mistMaterial.SetTexture("_MistViewOpacityMap", mistViewLookup);
        dropMaterial = new Material(mistMaterial); dropMaterial.SetFloat("_Droplet", 1f);
        UpdateViewOpacity();
        for (int fan = 0; fan < 2; fan++)
        {
            mist[fan] = CreateSpraySystem(fan, false, mistMaterial);
            droplets[fan] = CreateSpraySystem(fan, true, dropMaterial);
        }
    }

    private ParticleSystem CreateSpraySystem(int fan, bool drop, Material material)
    {
        var go = new GameObject("Fan " + fan + (drop ? " droplets" : " mist"));
        go.transform.SetParent(world.transform, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Custom;
        main.customSimulationSpace = world.transform;
        main.maxParticles = drop ? 2400 : 1600;
        main.playOnAwake = false;
        main.gravityModifier = 0;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = system.emission; emission.enabled = false;
        var shape = system.shape; shape.enabled = false;
        var drag = system.limitVelocityOverLifetime;
        // Exact exponential drag is applied in habitat space by UpdateSprayMotion.
        drag.enabled = false;
        drag.multiplyDragByParticleSize = false; drag.multiplyDragByParticleVelocity = false;
        var color = system.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(new Color(.8f,.91f,1),1) },
            new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.12f), new GradientAlphaKey(.65f,.65f), new GradientAlphaKey(0,1) });
        color.color = gradient;
        var size = system.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0,.55f),new Keyframe(.5f,1),new Keyframe(1,drop?.45f:1.7f)));
        if (!drop)
        {
            var noise = system.noise; noise.enabled = true; noise.strength = 0; noise.frequency = .8f;
            noise.scrollSpeed = .45f; noise.octaveCount = 2;
            var rotation = system.rotationOverLifetime; rotation.enabled = true; rotation.z = .6f;
        }
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = drop ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        renderer.lengthScale = drop ? 2.2f : 1f; renderer.velocityScale = drop ? .06f : 0f;
        renderer.maxParticleSize = .4f;
        renderer.receiveShadows = true; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        system.Play();
        return system;
    }

    private void OnDisable()
    {
        clock.Reset(); waves.Clear();
        waveMotions.Clear();
        for (int fan = 0; fan < 2; fan++) ResetFan(fan);
        if (detailObject != null) detailObject.SetActive(false);
        if (surfaceMaterial != null) surfaceMaterial.SetFloat("_DetailPatchEnabled", 0f);
        if (surfaceMaterial != null) { surfaceMaterial.SetFloat("_DynamicsEnabled", 0f); surfaceMaterial.SetInt("_DownwashWaveCount", 0); }
        foreach (var particles in mist) if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach (var particles in droplets) if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    private void OnEnable()
    {
        if (!initialized) return;
        if (detailObject != null) detailObject.SetActive(true);
        if (DynamicsAvailable) { Clear(waterBuffer); Clear(sourceBuffer); Clear(oldBuffer); }
        foreach (var particles in mist) if (particles != null) particles.Play();
        foreach (var particles in droplets) if (particles != null) particles.Play();
    }
    private void OnDestroy()
    {
        foreach (var buffer in new[] { waterBuffer, sourceBuffer, oldBuffer })
            if (buffer != null) { buffer.Release(); DisposeObject(buffer); }
        foreach (var particles in mist) if (particles != null) DisposeObject(particles.gameObject);
        foreach (var particles in droplets) if (particles != null) DisposeObject(particles.gameObject);
        DisposeObject(runtimeCompute); DisposeObject(detailObject); DisposeObject(detailMesh);
        DisposeObject(detailMaterial); DisposeObject(mistMaterial); DisposeObject(dropMaterial); DisposeObject(mistViewLookup); DisposeObject(terrainHeightMap);
    }
    private static void DisposeObject(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
