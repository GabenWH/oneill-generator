#ifndef CYLINDER_SHADOWS
#define CYLINDER_SHADOWS

#include "UnityCG.cginc"
#include "UnityShadowLibrary.cginc"

UNITY_DECLARE_SHADOWMAP(_CylinderDirectionalShadowMap);
float _CylinderShadowAtlasAvailable, _CylinderHousingAvailable;
float _CylinderShadowCascadeCount, _CylinderShadowUseSpheres;
float4 _CylinderShadowSunDirection;
float4x4 _CylinderHousingWorldToLocal;
float4 _CylinderHousingSize; // radius, half length, land stripe count
float4 _CylinderHousingGlass; // face alpha, edge alpha, Fresnel power
sampler2D _CylinderHousingTerrain;
float4 _CylinderHousingTerrainGrid; // angular samples, axial intervals, outer radius, amplitude

float CylinderHousingHeightSample(float a,float z)
{
    float2 uv=float2((fmod(a+_CylinderHousingTerrainGrid.x,_CylinderHousingTerrainGrid.x)+.5)/_CylinderHousingTerrainGrid.x,
        (z+.5)/(_CylinderHousingTerrainGrid.y+1));
    return tex2Dlod(_CylinderHousingTerrain,float4(uv,0,0)).r;
}

float CylinderHousingGroundRadius(float3 localPosition)
{
    float turn=frac(atan2(localPosition.y,localPosition.x)/6.28318530718);
    float a=turn*_CylinderHousingTerrainGrid.x;
    float z=saturate(localPosition.z/(_CylinderHousingSize.y*2)+.5)*_CylinderHousingTerrainGrid.y;
    float ia=floor(a),iz=min(floor(z),_CylinderHousingTerrainGrid.y-1);
    float u=a-ia,v=z-iz;
    float h00=CylinderHousingHeightSample(ia,iz),h10=CylinderHousingHeightSample(ia+1,iz);
    float h01=CylinderHousingHeightSample(ia,iz+1),h11=CylinderHousingHeightSample(ia+1,iz+1);
    float height=u<=v ? h00*(1-v)+h01*(v-u)+h11*u : h00*(1-u)+h10*(u-v)+h11*v;
    float step=6.28318530718/_CylinderHousingTerrainGrid.x;
    return (_CylinderHousingSize.x-height)*cos(step*.5)/cos((u-.5)*step);
}

bool CylinderHousingOpaqueBand(float3 localPosition)
{
    float sector=floor(frac(atan2(localPosition.y,localPosition.x)/6.28318530718)*_CylinderHousingSize.z*2);
    return fmod(sector,2)<.5;
}

float2 CylinderHousingOuterFacetNormal(float2 localPosition)
{
    float turn=frac(atan2(localPosition.y,localPosition.x)/6.28318530718);
    float angle=(floor(turn*_CylinderHousingTerrainGrid.x)+.5)*(6.28318530718/_CylinderHousingTerrainGrid.x);
    return float2(cos(angle),sin(angle));
}

bool CylinderHousingShellOccluded(float3 p,float3 d)
{
    float outer=_CylinderHousingTerrainGrid.z,halfLength=_CylinderHousingSize.y;
    float minimumRadius=_CylinderHousingSize.x-_CylinderHousingTerrainGrid.w;
    // Match the exposed terrain walls, rather than assuming a zero-thickness
    // cylinder. Grazing rays can clear the glass then hit a neighbouring wall.
    [loop] for(int edge=0;edge<(int)(_CylinderHousingSize.z*2);edge++)
    {
        float angle=edge*3.14159265359/_CylinderHousingSize.z;
        float2 radial=float2(cos(angle),sin(angle)),normal=float2(-radial.y,radial.x);
        float denominator=dot(normal,d.xy);
        if(abs(denominator)<.000001)continue;
        float t=-dot(normal,p.xy)/denominator;
        if(t<=.01)continue;
        float3 hit=p+d*t;
        float radius=dot(hit.xy,radial);
        if(radius>outer || radius<minimumRadius || abs(hit.z)>halfLength)continue;
        if(radius>=CylinderHousingGroundRadius(hit))return true;
    }
    float a=dot(d.xy,d.xy),b=dot(p.xy,d.xy);
    float discriminant=b*b-a*(dot(p.xy,p.xy)-outer*outer);
    float apothem=outer*cos(3.14159265359/_CylinderHousingTerrainGrid.x);
    if(a>.000001 && discriminant>=0)
    {
        float t=(-b+sqrt(discriminant))/a;
        // Match the mesh's flat outer faces instead of self-occluding them
        // against an enclosing smooth cylinder.
        [loop] for(int refine=0;refine<3;refine++)
        {
            float2 normal=CylinderHousingOuterFacetNormal((p+d*t).xy);
            float denominator=dot(normal,d.xy);
            if(denominator<=.000001){t=-1;break;}
            t=(apothem-dot(normal,p.xy))/denominator;
        }
        float3 hit=p+d*t;
        if(t>.01 && abs(hit.z)<=halfLength && CylinderHousingOpaqueBand(hit))return true;
    }
    if(abs(d.z)>.000001)
    {
        float t=((d.z>0?halfLength:-halfLength)-p.z)/d.z;
        float3 hit=p+d*t;
        float radius=length(hit.xy);
        float2 normal=CylinderHousingOuterFacetNormal(hit.xy);
        if(t>.01 && dot(normal,hit.xy)<=apothem && radius>=minimumRadius && CylinderHousingOpaqueBand(hit) &&
            radius>=CylinderHousingGroundRadius(hit))return true;
    }
    return false;
}

// Samples the directional depth atlas at this receiver, never at the opaque
// terrain depth behind transparent water or a particle billboard.
float CylinderTransparentShadow(float3 world)
{
    if (_CylinderShadowAtlasAvailable < .5 || _WorldSpaceLightPos0.w != 0 ||
        dot(normalize(_WorldSpaceLightPos0.xyz), _CylinderShadowSunDirection.xyz) < .999)
        return 1;

    float viewDepth = dot(_WorldSpaceCameraPos - world, UNITY_MATRIX_V[2].xyz);
    float fade = UnityComputeShadowFade(UnityComputeShadowFadeDistance(world, viewDepth));
    if (fade >= .9999) return 1;
    float4 weights = float4(1, 0, 0, 0);
    if (_CylinderShadowCascadeCount > 1.5)
    {
        if (_CylinderShadowUseSpheres > .5)
        {
            float3 d0 = world - unity_ShadowSplitSpheres[0].xyz;
            float3 d1 = world - unity_ShadowSplitSpheres[1].xyz;
            float3 d2 = world - unity_ShadowSplitSpheres[2].xyz;
            float3 d3 = world - unity_ShadowSplitSpheres[3].xyz;
            float4 distances = float4(dot(d0,d0), dot(d1,d1), dot(d2,d2), dot(d3,d3));
            weights = float4(distances < unity_ShadowSplitSqRadii);
            weights.yzw = saturate(weights.yzw - weights.xyz);
        }
        else weights = float4(viewDepth >= _LightSplitsNear) * float4(viewDepth < _LightSplitsFar);
        if (_CylinderShadowCascadeCount < 3.5) weights.zw = 0;
        if (dot(weights, float4(1,1,1,1)) < .5) return 1;
    }
    float4 p = float4(world, 1);
    float3 coordinate = (mul(unity_WorldToShadow[0], p) * weights.x +
        mul(unity_WorldToShadow[1], p) * weights.y +
        mul(unity_WorldToShadow[2], p) * weights.z +
        mul(unity_WorldToShadow[3], p) * weights.w).xyz;
    if (any(coordinate < 0) || any(coordinate > 1)) return 1;
    float shadow = UNITY_SAMPLE_SHADOW(_CylinderDirectionalShadowMap, coordinate);
    shadow = lerp(_LightShadowData.r, 1, shadow);
    return lerp(shadow, 1, fade);
}

// Stable, camera-independent occlusion by the habitat's solid opaque bands.
// Local relief and the drone retain their normal finite-range shadow atlas.
float CylinderHousingTransmission(float3 world, float3 lightDirection)
{
    if (_CylinderHousingAvailable < .5 || _WorldSpaceLightPos0.w != 0 ||
        dot(normalize(lightDirection), _CylinderShadowSunDirection.xyz) < .999) return 1;
    float3 p = mul(_CylinderHousingWorldToLocal, float4(world, 1)).xyz;
    float3 d = normalize(mul((float3x3)_CylinderHousingWorldToLocal, lightDirection));
    float radius = max(.01, _CylinderHousingSize.x), halfLength = _CylinderHousingSize.y;
    if (abs(p.z) > halfLength + .01) return 1;
    if(CylinderHousingShellOccluded(p,d))return 0;
    float a = dot(d.xy, d.xy), b = dot(p.xy, d.xy);
    float c = dot(p.xy, p.xy) - radius * radius;
    float discriminant = b * b - a * c;
    float sideDistance = 1e20;
    if (a > .000001 && discriminant >= 0)
    {
        sideDistance = (-b + sqrt(max(0, discriminant))) / a;
        if (sideDistance <= .001) sideDistance = 1e20;
    }
    float capDistance = 1e20;
    if (abs(d.z) > .000001)
    {
        capDistance = ((d.z > 0 ? halfLength : -halfLength) - p.z) / d.z;
        if (capDistance <= .001) capDistance = 1e20;
    }
    float incidence;
    if (capDistance < sideDistance)
    {
        float3 hit = p + d * capDistance;
        if (dot(hit.xy, hit.xy) > radius * radius) return 1;
        incidence = abs(d.z);
    }
    else
    {
        if (sideDistance > 1e19) return 1;
        float3 hit = p + d * sideDistance;
        if (abs(hit.z) > halfLength) return 1;
        incidence = abs(dot(normalize(hit.xy), d.xy));
    }
    float fresnel = pow(1 - saturate(incidence), max(.1, _CylinderHousingGlass.z));
    return 1 - saturate(lerp(_CylinderHousingGlass.x, _CylinderHousingGlass.y, fresnel));
}
#endif
