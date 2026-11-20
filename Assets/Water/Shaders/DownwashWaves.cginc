#ifndef GRACE_DOWNWASH_WAVES
#define GRACE_DOWNWASH_WAVES
// wave: world X/Z, emission time, crest amplitude in metres.
// shape: propagation speed, packet width, temporal damping, maximum radius.
float GraceRingHeight(float2 world, float4 wave, float4 shape, float now)
{
    float age=now-wave.z;
    if(age<0.0 || wave.w<=0.0) return 0.0;
    float front=age*max(shape.x,0.3);
    if(front>shape.w+shape.y*2.0) return 0.0;
    float distance=length(world-wave.xy);
    float band=(distance-front)/max(shape.y,0.15);
    float envelope=exp(-2.0*band*band);
    float fade=1.0-smoothstep(shape.w*0.8,shape.w,front);
    return wave.w*cos(band*3.14159265)*envelope*exp(-age*shape.z)*fade/(1.0+front*0.3);
}
// New rings inherit surface motion at birth, then slow relative to the water.
// motion: arc/Z velocity, drag per second, unused fan identity.
float GraceMovingRingHeight(float2 world, float4 wave, float4 motion, float4 shape, float now)
{
    float age=max(0,now-wave.z);
    float travel=(1-exp(-max(.01,motion.z)*age))/max(.01,motion.z);
    wave.xy+=motion.xy*travel;
    return GraceRingHeight(world,wave,shape,now);
}
#endif
