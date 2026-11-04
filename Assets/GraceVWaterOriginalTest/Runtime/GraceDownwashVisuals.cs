using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Vertigo2;

namespace Grace.VWaterOriginal
{
    /// <summary>Owned runtime spray and geometric wave packets, driven by actual fan impacts.</summary>
    internal sealed class GraceDownwashVisuals : IDisposable
    {
        // Covers the slowest advertised speed and shortest emission interval
        // through the full radius, including the packet's fading tail.
        private const int MaxWaves = 256; // Up to 128 live packets per fan.
        private readonly GraceOriginalVWaterFanBridge owner;
        private readonly GameObject root;
        private readonly ParticleSystem[,] particles = new ParticleSystem[2,2];
        private readonly DownwashEmissionBudget[,] budgets = new DownwashEmissionBudget[2,2];
        private readonly DownwashFanWaveEmitter waveEmitter=new DownwashFanWaveEmitter();
        private readonly bool[] hitThisFrame = new bool[2];
        private readonly System.Random random = new System.Random(41027);
        private readonly List<Vector4> waves = new List<Vector4>(MaxWaves);
        private readonly Vector4[] waveBuffer = new Vector4[MaxWaves];
        private readonly Material mistMaterial, dropMaterial, surface;
        private const int ViewLutSize=256;
        private readonly Texture2D viewOpacityLut;
        private readonly Color32[] viewOpacityPixels=new Color32[ViewLutSize];
        private readonly AnimationCurve fallbackViewCurve=DownwashResponseProfile.DefaultViewOpacity();
        private int previousViewHash;
        private bool viewLutInitialized;
        private readonly Shader originalShader;
        private readonly MeshFilter meshFilter;
        private readonly Mesh originalMesh, grid;
        private float delta, now;
        private readonly DownwashMath.V3[] impactCenters=new DownwashMath.V3[2];
        private readonly double[] impactWaveHeights=new double[2];
        public int ParticleCount { get { int count=0; foreach(var p in particles) if(p!=null) count+=p.particleCount; return count; } }
        public int WaveCount { get { return waves.Count; } }

        public GraceDownwashVisuals(GraceOriginalVWaterFanBridge owner)
        {
            this.owner=owner;
            root=new GameObject("Downwash mist and droplets (runtime)");
            root.transform.SetParent(owner.transform,false);
            Shader spray=owner.sprayShader!=null?owner.sprayShader:Shader.Find("Grace/VWater/Downwash Mist");
            if(spray!=null && spray.isSupported)
            {
                mistMaterial=new Material(spray){name="Downwash wisps (runtime)"};
                dropMaterial=new Material(spray){name="Downwash droplets (runtime)"};
                dropMaterial.SetFloat("_Droplet",1);
                viewOpacityLut=new Texture2D(ViewLutSize,1,TextureFormat.RGBA32,false,true)
                    {name="Mist view opacity (runtime)",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                mistMaterial.SetTexture("_MistViewOpacityMap",viewOpacityLut);
                dropMaterial.SetTexture("_MistViewOpacityMap",viewOpacityLut);
                UpdateViewOpacity();
                for(int fan=0;fan<2;fan++) for(int layer=0;layer<2;layer++)
                {
                    budgets[fan,layer]=new DownwashEmissionBudget();
                    particles[fan,layer]=MakeParticles(fan,layer==0,layer==0?mistMaterial:dropMaterial);
                }
            }
            else Debug.LogError("Downwash mist shader missing/unsupported. Refresh Assets and check shader errors.",owner);
            Shader wave=owner.waveShader!=null?owner.waveShader:Shader.Find("Grace/VWater/Visible Downwash Surface");
            meshFilter=owner.water.GetComponent<MeshFilter>();
            if(wave!=null && wave.isSupported && meshFilter!=null)
            {
                // VWaterRenderer already instantiated this material in Start. Keep
                // that same instance: its buffer/keyword writes must stay visible.
                surface=owner.originalRenderer.GetComponent<Renderer>().material;
                originalShader=surface.shader;
                surface.shader=wave;
                surface.SetTexture("_WaveBuffer",VWaterDynamicsManager.waterBuffer);
                originalMesh=meshFilter.sharedMesh;
                grid=MakeGrid(originalMesh.bounds);
                meshFilter.sharedMesh=grid;
            }
            else Debug.LogError("Visible downwash surface shader missing/unsupported. Refresh Assets and check shader errors.",owner);
        }

        private ParticleSystem MakeParticles(int fan,bool mist,Material material)
        {
            var obj=new GameObject("Fan "+fan+(mist?" wisps":" droplets"));obj.transform.SetParent(root.transform,false);
            var system=obj.AddComponent<ParticleSystem>();
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;
            main.playOnAwake=false;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.maxParticles=mist?1600:2400;main.gravityModifier=mist?0:.65f;
            main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=system.emission;emission.enabled=false;
            var shape=system.shape;shape.enabled=false;
            var color=system.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.8f,.91f,1),1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.65f,.65f),new GradientAlphaKey(0,1)});
            color.color=gradient;
            var size=system.sizeOverLifetime;size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.55f),new Keyframe(.5f,1),new Keyframe(1,mist?1.7f:.45f)));
            if(mist)
            {
                var noise=system.noise;noise.enabled=true;noise.strength=0;noise.frequency=.8f;
                noise.scrollSpeed=.45f;noise.octaveCount=2;noise.quality=ParticleSystemNoiseQuality.Medium;
                var rotation=system.rotationOverLifetime;rotation.enabled=true;rotation.z=.6f;
            }
            var renderer=obj.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
            renderer.renderMode=mist?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.Stretch;
            renderer.lengthScale=mist?1:2.2f;renderer.velocityScale=mist?0:.06f;
            renderer.maxParticleSize=.4f;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            system.Play();return system;
        }

        private static Mesh MakeGrid(Bounds bounds)
        {
            const int n=192;var vertices=new Vector3[(n+1)*(n+1)];var normals=new Vector3[vertices.Length];
            var uv=new Vector2[vertices.Length];var triangles=new int[n*n*6];
            for(int z=0;z<=n;z++) for(int x=0;x<=n;x++)
            {
                int i=z*(n+1)+x;float u=x/(float)n,v=z/(float)n;
                vertices[i]=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,u),bounds.center.y,Mathf.Lerp(bounds.min.z,bounds.max.z,v));
                normals[i]=Vector3.up;uv[i]=new Vector2(u,v);
                if(x==n||z==n)continue;int k=(z*n+x)*6;
                triangles[k]=i;triangles[k+1]=i+n+1;triangles[k+2]=i+1;
                triangles[k+3]=i+1;triangles[k+4]=i+n+1;triangles[k+5]=i+n+2;
            }
            var mesh=new Mesh{name="Visible downwash grid (runtime)",vertices=vertices,normals=normals,uv=uv,triangles=triangles};
            mesh.RecalculateBounds();var expanded=mesh.bounds;expanded.Expand(new Vector3(0,2,0));mesh.bounds=expanded;return mesh;
        }

        public void BeginFrame(float delta,float time)
        {
            this.delta=delta;now=time;hitThisFrame[0]=hitThisFrame[1]=false;
        }

        public void Emit(int fan,DownwashMath.Disc disc,DownwashResponseValues response,double strength)
        {
            hitThisFrame[fan]=true;
            var mist=particles[fan,0];
            if(mist!=null){var noise=mist.noise;noise.strength=(float)response.MistTurbulence;}
            if(owner.sprayEnabled && strength>0 && response.MistAmount>0)
            {
                for(int layer=0;layer<2;layer++)
                {
                    var system=particles[fan,layer];if(system==null)continue;
                    int count=budgets[fan,layer].Consume((layer==0?60:180)*strength*response.MistAmount,delta);
                    if(count>0 && !system.isPlaying)system.Play();
                    for(int i=0;i<count;i++)
                    {
                        var sample=DownwashVisualMath.Sample(disc,strength,response.SprayHeight,owner.spraySpread,random.NextDouble(),random.NextDouble(),random.NextDouble(),layer==0);
                        var parameters=new ParticleSystem.EmitParams {
                            position=V(sample.Position),velocity=V(sample.Velocity),startLifetime=(float)sample.Life,startSize=(float)sample.Size,
                            startColor=new Color(.9f,.96f,1,layer==0?.32f:.8f),rotation=(float)random.NextDouble()*360
                        };
                        system.Emit(parameters,1);
                    }
                }
            }
            else for(int layer=0;layer<2;layer++) if(budgets[fan,layer]!=null)budgets[fan,layer].Reset();
            impactCenters[fan]=disc.Point;
            impactWaveHeights[fan]=strength>0?response.WaveHeight*Math.Sqrt(strength):0;
        }

        public void EndFrame()
        {
            UpdateViewOpacity();
            for(int fan=0;fan<2;fan++) if(!hitThisFrame[fan])
            {
                for(int layer=0;layer<2;layer++) if(budgets[fan,layer]!=null)budgets[fan,layer].Reset();
            }
            // Each fan owns its cadence and emits from its actual water footprint.
            // Existing packets keep their world-space centre as the drone moves.
            for(int fan=0;fan<2;fan++)
            {
                if(!owner.wavesEnabled || surface==null || !hitThisFrame[fan])
                { waveEmitter.Reset(fan);continue; }
                if(waveEmitter.TryEmit(fan,impactCenters[fan],impactWaveHeights[fan],delta,now,owner.waveInterval,out DownwashFanWave packet))
                {
                    if(waves.Count==MaxWaves)waves.RemoveAt(0);
                    waves.Add(new Vector4((float)packet.Position.X,(float)packet.Position.Z,(float)packet.Time,(float)packet.Amplitude));
                }
            }
            float speed=Mathf.Max(.3f,owner.waveSpeed),width=Mathf.Max(.15f,owner.waveWidth);
            if(!owner.wavesEnabled)waves.Clear();
            for(int i=waves.Count-1;i>=0;i--)if((now-waves[i].z)*speed>owner.waveRadius+width*2)waves.RemoveAt(i);
            if(surface!=null)
            {
                for(int i=0;i<MaxWaves;i++)waveBuffer[i]=i<waves.Count?waves[i]:Vector4.zero;
                surface.SetInt("_DownwashWaveCount",waves.Count);surface.SetVectorArray("_DownwashWaves",waveBuffer);
                surface.SetVector("_DownwashWaveShape",new Vector4(speed,width,.35f,Mathf.Clamp(owner.waveRadius,1,8)));
                surface.SetFloat("_DownwashVisualTime",now);surface.SetFloat("_DownwashWaterLevel",owner.water.Position);
            }
            if(mistMaterial!=null)mistMaterial.SetFloat("_WaterLevel",owner.water.Position);
            if(dropMaterial!=null)dropMaterial.SetFloat("_WaterLevel",owner.water.Position);
        }

        private void UpdateViewOpacity()
        {
            if(viewOpacityLut==null)return;
            var profile=owner.responseProfile;
            bool enabled=profile==null || profile.useMistViewFade;
            var curve=profile!=null?profile.mistOpacityByViewAngle:fallbackViewCurve;
            int hash=enabled?1:0;
            unchecked
            {
                if(curve!=null)
                {
                    hash=hash*31+(int)curve.preWrapMode;hash=hash*31+(int)curve.postWrapMode;
                    foreach(var key in curve.keys)
                    {
                        hash=hash*31+key.time.GetHashCode();hash=hash*31+key.value.GetHashCode();
                        hash=hash*31+key.inTangent.GetHashCode();hash=hash*31+key.outTangent.GetHashCode();
                        hash=hash*31+key.inWeight.GetHashCode();hash=hash*31+key.outWeight.GetHashCode();
                        hash=hash*31+(int)key.weightedMode;
                    }
                }
            }
            if(viewLutInitialized && hash==previousViewHash)return;
            for(int i=0;i<ViewLutSize;i++)
            {
                float value=enabled?(curve!=null && curve.length>0?curve.Evaluate(i*90f/(ViewLutSize-1)):0):1;
                if(!DownwashMath.Finite(value))value=0;
                byte opacity=(byte)Mathf.RoundToInt(Mathf.Clamp01(value)*255);
                viewOpacityPixels[i]=new Color32(opacity,opacity,opacity,255);
            }
            viewOpacityLut.SetPixels32(viewOpacityPixels);viewOpacityLut.Apply(false,false);
            previousViewHash=hash;viewLutInitialized=true;
        }

        public void Clear()
        {
            waves.Clear();foreach(var system in particles)if(system!=null)system.Clear();
            foreach(var budget in budgets)if(budget!=null)budget.Reset();
            waveEmitter.Reset(0);waveEmitter.Reset(1);
            if(surface!=null)surface.SetInt("_DownwashWaveCount",0);
        }

        public void Dispose()
        {
            Clear();
            if(meshFilter!=null && meshFilter.sharedMesh==grid)meshFilter.sharedMesh=originalMesh;
            if(surface!=null)surface.shader=originalShader;
            if(grid!=null)UnityEngine.Object.Destroy(grid);
            if(mistMaterial!=null)UnityEngine.Object.Destroy(mistMaterial);
            if(dropMaterial!=null)UnityEngine.Object.Destroy(dropMaterial);
            if(viewOpacityLut!=null)UnityEngine.Object.Destroy(viewOpacityLut);
            if(root!=null)UnityEngine.Object.Destroy(root);
        }
        private static Vector3 V(DownwashMath.V3 value){return new Vector3((float)value.X,(float)value.Y,(float)value.Z);}
    }
}
