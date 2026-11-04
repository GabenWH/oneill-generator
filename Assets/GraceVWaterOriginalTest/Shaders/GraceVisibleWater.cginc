#define vert GraceVWaterBaseVert
#define frag GraceVWaterBaseFrag
#include "../../VWater/Shaders/VWaterLighting.cginc"
#undef vert
#undef frag
#include "DownwashWaves.cginc"

int _DownwashWaveCount;
float4 _DownwashWaves[256];
float4 _DownwashWaveShape;
float _DownwashVisualTime,_DownwashWaterLevel;

float GraceDisplacement(float2 world)
{
    float height=0.0;
    for(int wave=0;wave<min(_DownwashWaveCount,256);wave++)
        height+=GraceRingHeight(world,_DownwashWaves[wave],_DownwashWaveShape,_DownwashVisualTime);
    return clamp(height,-0.45,0.45);
}

v2f vert(appdata_full v)
{
    float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
    float height=GraceDisplacement(world.xz);
    float dx=(GraceDisplacement(world.xz+float2(0.04,0))-GraceDisplacement(world.xz-float2(0.04,0)))/0.08;
    float dz=(GraceDisplacement(world.xz+float2(0,0.04))-GraceDisplacement(world.xz-float2(0,0.04)))/0.08;
    world.y+=height;
    v.vertex=mul(unity_WorldToObject,float4(world,1));
    // Convert the displaced world normal to object space for the base vertex path.
    v.normal=normalize(mul(float3(-dx,1,-dz),(float3x3)unity_ObjectToWorld));
    return GraceVWaterBaseVert(v);
}

fixed4 frag(v2f i):SV_Target
{
    fixed4 col=GraceVWaterBaseFrag(i);
    #if defined(FORWARD_BASE_PASS)
        float crest=smoothstep(0.025,0.14,max(0.0,i.worldPos.y-_DownwashWaterLevel));
        col.rgb=lerp(col.rgb,float3(0.82,0.94,1.0),crest*0.65);
    #endif
    return col;
}
