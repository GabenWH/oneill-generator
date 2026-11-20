#ifndef GRACE_DOWNWASH_MIST_SHAPE
#define GRACE_DOWNWASH_MIST_SHAPE
float GraceMistViewAngle(float3 toCamera)
{
    if(dot(toCamera,toCamera)<0.00000001)return 0.0;
    return atan2(abs(toCamera.y),length(toCamera.xz))*57.2957795;
}
float2 GraceMistViewLookupUV(float angle)
{
    // The runtime LUT has 256 samples including both 0 and 90 degree endpoints.
    return float2((saturate(angle/90.0)*255.0+0.5)/256.0,0.5);
}
float GraceMistAlpha(float2 uv,float phase,float droplet)
{
    float2 q=uv*2.0-1.0;
    if(droplet>0.5)
        return pow(saturate(1.0-dot(q*float2(1.0,0.65),q*float2(1.0,0.65))),2.0)
            *(1.0-smoothstep(0.75,1.0,abs(q.y)));
    q.x+=0.18*sin(q.y*7.0+phase);
    float edge=1.0-smoothstep(0.55,1.0,length(q));
    float ribbons=0.6+0.4*sin(q.y*11.0+q.x*5.0+phase);
    return exp(-2.8*dot(q*float2(1.5,0.8),q*float2(1.5,0.8)))*edge*ribbons;
}
#endif
