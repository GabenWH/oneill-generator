using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Publishes habitat sunlight and the sun's real shadow atlas for transparent receivers.</summary>
[DefaultExecutionOrder(1400)]
public sealed class CylinderSunShadows : MonoBehaviour
{
    public ONeillWorld world;
    public CylinderChunkManager chunks;
    public Light sun;
    public Material glassMaterial;

    private Light attachedLight;
    private CommandBuffer capture;
    private Texture2D terrainHeights;
    private float[] heightSamples;
    private int terrainAround, terrainAlong;
    private float terrainAmplitude, terrainLength;
    private static readonly int Atlas = Shader.PropertyToID("_CylinderDirectionalShadowMap");
    private static readonly int AtlasAvailable = Shader.PropertyToID("_CylinderShadowAtlasAvailable");
    private static readonly int HousingAvailable = Shader.PropertyToID("_CylinderHousingAvailable");

    private void OnEnable()
    {
        Camera.onPreCull += BeforeCamera;
        Camera.onPostRender += AfterCamera;
        Refresh();
    }

    private void LateUpdate() { Refresh(); }

    private void BeforeCamera(Camera camera)
    {
        // The next camera must not reuse a previous camera's shadow atlas.
        Shader.SetGlobalFloat(AtlasAvailable, 0f);
        Refresh();
    }

    private void AfterCamera(Camera camera) { Shader.SetGlobalFloat(AtlasAvailable, 0f); }

    public void Refresh()
    {
        bool active = isActiveAndEnabled && world != null && sun != null &&
            sun.isActiveAndEnabled && sun.type == LightType.Directional && sun.intensity > 0f;
        Shader.SetGlobalFloat(HousingAvailable, active ? 1f : 0f);
        if (active)
        {
            EnsureTerrainHeights();
            Shader.SetGlobalMatrix("_CylinderHousingWorldToLocal", world.transform.worldToLocalMatrix);
            Shader.SetGlobalVector("_CylinderHousingSize", new Vector4(world.radius, world.length * .5f,
                chunks != null ? Mathf.Max(1, chunks.landStripes) : 3, 0));
            Shader.SetGlobalVector("_CylinderHousingGlass", GlassParameters());
            Shader.SetGlobalTexture("_CylinderHousingTerrain", terrainHeights);
            Shader.SetGlobalVector("_CylinderHousingTerrainGrid",new Vector4(terrainAround,terrainAlong,
                world.OuterRadius,world.terrainAmplitude));
            Shader.SetGlobalVector("_CylinderShadowSunDirection", -sun.transform.forward);
            Shader.SetGlobalFloat("_CylinderShadowCascadeCount", Mathf.Max(1, QualitySettings.shadowCascades));
            Shader.SetGlobalFloat("_CylinderShadowUseSpheres", QualitySettings.shadowProjection == ShadowProjection.StableFit ? 1f : 0f);
        }
        bool shadows = active && sun.shadows != LightShadows.None && sun.shadowStrength > 0f &&
            QualitySettings.shadows != ShadowQuality.Disable && QualitySettings.shadowDistance > 0f;
        if (!shadows)
        {
            Detach();
            Shader.SetGlobalFloat(AtlasAvailable, 0f);
            return;
        }
        if (attachedLight == sun && capture != null) return;
        Detach();
        capture = new CommandBuffer { name = "Cylinder transparent receiver shadow atlas" };
        // Keep the depth atlas under its own name: _ShadowMapTexture is replaced
        // by Unity's opaque-depth, screen-space resolve before transparents draw.
        capture.SetGlobalTexture(Atlas, BuiltinRenderTextureType.CurrentActive, RenderTextureSubElement.Depth);
        capture.SetGlobalFloat(AtlasAvailable, 1f);
        sun.AddCommandBuffer(LightEvent.AfterShadowMap, capture);
        attachedLight = sun;
    }

    private Vector4 GlassParameters()
    {
        return new Vector4(
            glassMaterial != null && glassMaterial.HasProperty("_BaseAlpha") ? glassMaterial.GetFloat("_BaseAlpha") : .025f,
            glassMaterial != null && glassMaterial.HasProperty("_EdgeAlpha") ? glassMaterial.GetFloat("_EdgeAlpha") : .35f,
            glassMaterial != null && glassMaterial.HasProperty("_FresnelPower") ? glassMaterial.GetFloat("_FresnelPower") : 3f, 0);
    }

    private void EnsureTerrainHeights()
    {
        int around=chunks!=null ? chunks.angularChunks*chunks.angularResolution : 288;
        int along=chunks!=null ? chunks.longitudinalChunks*chunks.longitudinalResolution : 192;
        if(terrainHeights!=null && terrainAround==around && terrainAlong==along &&
            terrainAmplitude==world.terrainAmplitude && terrainLength==world.length)return;
        ReleaseTerrainHeights();
        terrainAround=around;terrainAlong=along;
        terrainAmplitude=world.terrainAmplitude;terrainLength=world.length;
        heightSamples=new float[around*(along+1)];
        for(int z=0;z<=along;z++)for(int a=0;a<around;a++)
            heightSamples[z*around+a]=world.SampleTerrainHeight(a*(Mathf.PI*2f/around),world.length*(z/(float)along-.5f));
        terrainHeights=new Texture2D(around,along+1,TextureFormat.RFloat,false,true)
        {
            name="Cylinder housing terrain shadows",filterMode=FilterMode.Point,
            wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp
        };
        terrainHeights.SetPixelData(heightSamples,0);terrainHeights.Apply(false,false);
    }

    private float GroundRadius(Vector3 point)
    {
        float angle=Mathf.Repeat(Mathf.Atan2(point.y,point.x),Mathf.PI*2f);
        float around=angle/(Mathf.PI*2f)*terrainAround;
        float along=Mathf.Clamp01(point.z/world.length+.5f)*terrainAlong;
        int a=Mathf.FloorToInt(around)%terrainAround,next=(a+1)%terrainAround;
        int row=Mathf.Min(Mathf.FloorToInt(along),terrainAlong-1);
        float u=around-Mathf.Floor(around),v=along-row;
        float h00=heightSamples[row*terrainAround+a],h10=heightSamples[row*terrainAround+next];
        float h01=heightSamples[(row+1)*terrainAround+a],h11=heightSamples[(row+1)*terrainAround+next];
        float height=u<=v ? h00*(1-v)+h01*(v-u)+h11*u : h00*(1-u)+h10*(u-v)+h11*v;
        float step=Mathf.PI*2f/terrainAround;
        return (world.radius-height)*Mathf.Cos(step*.5f)/Mathf.Cos(Mathf.Repeat(angle,step)-step*.5f);
    }

    private bool IsOpaqueBand(Vector3 point,int stripes)
        => Mathf.FloorToInt(Mathf.Repeat(Mathf.Atan2(point.y,point.x)/(Mathf.PI*2f),1f)*stripes*2)%2==0;

    private Vector2 OuterFacetNormal(Vector3 point)
    {
        float turn=Mathf.Repeat(Mathf.Atan2(point.y,point.x)/(Mathf.PI*2f),1f);
        float angle=(Mathf.Floor(turn*terrainAround)+.5f)*(Mathf.PI*2f/terrainAround);
        return new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
    }

    private bool ShellOccludes(Vector3 p,Vector3 d,int stripes)
    {
        float outer=world.OuterRadius,halfLength=world.length*.5f;
        // A window opening does not guarantee a clear ray: the land's outer
        // shell and exposed walls extend hundreds of metres beyond the glass.
        for(int edge=0;edge<stripes*2;edge++)
        {
            float angle=edge*Mathf.PI/stripes;
            Vector2 radial=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            Vector2 normal=new Vector2(-radial.y,radial.x);
            float denominator=normal.x*d.x+normal.y*d.y;
            if(Mathf.Abs(denominator)<.000001f)continue;
            float t=-(normal.x*p.x+normal.y*p.y)/denominator;
            if(t<=.01f)continue;
            Vector3 hit=p+d*t;
            float radius=hit.x*radial.x+hit.y*radial.y;
            if(radius>outer || radius<world.radius-world.terrainAmplitude || Mathf.Abs(hit.z)>halfLength)continue;
            if(radius>=GroundRadius(hit))return true;
        }
        float a=d.x*d.x+d.y*d.y,b=p.x*d.x+p.y*d.y;
        float discriminant=b*b-a*(p.x*p.x+p.y*p.y-outer*outer);
        float apothem=outer*Mathf.Cos(Mathf.PI/terrainAround);
        if(a>.000001f && discriminant>=0f)
        {
            float t=(-b+Mathf.Sqrt(discriminant))/a;
            // Refine the enclosing-circle intersection onto the actual polygon
            // face. Otherwise every outer face shadows itself by ~0.26 m.
            for(int refine=0;refine<3;refine++)
            {
                Vector2 normal=OuterFacetNormal(p+d*t);
                float denominator=normal.x*d.x+normal.y*d.y;
                if(denominator<=.000001f){t=-1f;break;}
                t=(apothem-normal.x*p.x-normal.y*p.y)/denominator;
            }
            Vector3 hit=p+d*t;
            if(t>.01f && Mathf.Abs(hit.z)<=halfLength && IsOpaqueBand(hit,stripes))return true;
        }
        if(Mathf.Abs(d.z)>.000001f)
        {
            float t=((d.z>0f?halfLength:-halfLength)-p.z)/d.z;
            Vector3 hit=p+d*t;
            float radius=new Vector2(hit.x,hit.y).magnitude;
            Vector2 normal=OuterFacetNormal(hit);
            if(t>.01f && normal.x*hit.x+normal.y*hit.y<=apothem && radius>=world.radius-world.terrainAmplitude &&
                IsOpaqueBand(hit,stripes) && radius>=GroundRadius(hit))return true;
        }
        return false;
    }

    /// <summary>Sunlight through the housing, including the solid land walls, independent of camera distance.</summary>
    public float HousingTransmission(Vector3 position, Vector3 direction)
    {
        if (world == null || direction.sqrMagnitude < .000001f) return 1f;
        Vector3 p = world.transform.InverseTransformPoint(position);
        Vector3 d = world.transform.worldToLocalMatrix.MultiplyVector(direction).normalized;
        float radius = Mathf.Max(.01f, world.radius), halfLength = world.length * .5f;
        // Outside an end cap, the ideal habitat must not enclose the receiver.
        if (Mathf.Abs(p.z) > halfLength + .01f) return 1f;
        EnsureTerrainHeights();
        int stripes=chunks!=null ? Mathf.Max(1,chunks.landStripes) : 3;
        if(ShellOccludes(p,d,stripes))return 0f;
        float a = d.x * d.x + d.y * d.y;
        float b = p.x * d.x + p.y * d.y;
        float c = p.x * p.x + p.y * p.y - radius * radius;
        float discriminant = b * b - a * c;
        float sideDistance = a > .000001f && discriminant >= 0f
            ? (-b + Mathf.Sqrt(Mathf.Max(0f, discriminant))) / a : float.PositiveInfinity;
        if (sideDistance <= .001f) sideDistance = float.PositiveInfinity;
        float capDistance = Mathf.Abs(d.z) > .000001f
            ? ((d.z > 0f ? halfLength : -halfLength) - p.z) / d.z : float.PositiveInfinity;
        if (capDistance <= .001f) capDistance = float.PositiveInfinity;
        float incidence;
        if (capDistance < sideDistance)
        {
            Vector3 hit = p + d * capDistance;
            if (hit.x * hit.x + hit.y * hit.y > radius * radius) return 1f;
            incidence = Mathf.Abs(d.z);
        }
        else
        {
            if (float.IsInfinity(sideDistance)) return 1f;
            Vector3 hit = p + d * sideDistance;
            if (Mathf.Abs(hit.z) > halfLength) return 1f;
            incidence = Mathf.Abs(Vector3.Dot(new Vector3(hit.x, hit.y, 0).normalized, d));
        }
        Vector4 glass = GlassParameters();
        float fresnel = Mathf.Pow(1f - Mathf.Clamp01(incidence), Mathf.Max(.1f, glass.z));
        return 1f - Mathf.Clamp01(Mathf.Lerp(glass.x, glass.y, fresnel));
    }

    private void Detach()
    {
        if (capture == null) return;
        if (attachedLight != null) attachedLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, capture);
        capture.Release();
        capture = null;
        attachedLight = null;
    }

    private void OnDisable()
    {
        Camera.onPreCull -= BeforeCamera;
        Camera.onPostRender -= AfterCamera;
        Detach();
        Shader.SetGlobalFloat(AtlasAvailable, 0f);
        Shader.SetGlobalFloat(HousingAvailable, 0f);
        Shader.SetGlobalTexture(Atlas, null);
        ReleaseTerrainHeights();
    }

    private void ReleaseTerrainHeights()
    {
        if(terrainHeights==null)return;
        if(Application.isPlaying)Destroy(terrainHeights);else DestroyImmediate(terrainHeights);
        terrainHeights=null;heightSamples=null;
    }
}
