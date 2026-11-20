#ifndef CYLINDER_WATER_LIGHTING
#define CYLINDER_WATER_LIGHTING
#include "CylinderShadows.cginc"
// Shared by near/far water, surface foam and airborne spray.
float3 CylinderWaterIrradiance(float3 normal, float3 world, float attenuation)
{
    float3 ambient=max(ShadeSH9(float4(normal,1)),.15);
    float3 light=normalize(UnityWorldSpaceLightDir(world));
    float housing = CylinderHousingTransmission(world, light);
    return ambient+_LightColor0.rgb*saturate(dot(normal,light))*attenuation*housing;
}
#endif
