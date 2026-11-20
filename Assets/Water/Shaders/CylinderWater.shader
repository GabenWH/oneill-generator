Shader "Grace/VWater/Cylinder Surface"
{
    Properties
    {
        _AbsorptionColor("Deep water", Color) = (.025,.16,.23,1)
        _ScatteringColor("Shallow water", Color) = (.12,.4,.43,1)
        _Density("Water density", Float) = .15
        _Smoothness("Smoothness", Range(0,1)) = .925
        [NoScaleOffset] _Normal("Water normals", 2D) = "bump" {}
        _FoamMap("Foam", 2D) = "white" {}
        _FoamDepth("Shore foam depth", Float) = .66
        _Refraction("Refraction", Range(0,.1)) = .015
        _DistantStart("Distant water fade starts (m)", Float) = 500
        _DistantEnd("Distant water fade ends (m)", Float) = 2000
        _DistantDepthBias("Distant depth bias (24-bit units)", Range(0,4)) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent-1" "RenderType"="Transparent" }
        GrabPass { "_CylinderWaterGrab" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "DownwashWaves.cginc"
            #include "CylinderWaterLighting.cginc"
            sampler2D _Normal, _FoamMap, _WaveBuffer, _CylinderWaterGrab, _TerrainHeightMap;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _AbsorptionColor, _ScatteringColor;
            float _Density, _Smoothness, _FoamDepth, _Refraction;
            float _DistantStart, _DistantEnd, _DistantDepthBias;
            float4x4 _CylinderWorldToLocal, _CylinderLocalToWorld;
            float _CylinderRadius, _CylinderAngularStep, _CylinderLength, _LandStripes;
            float4 _WaveBufferWorldPos;
            float _WaveBufferWorldSize, _DynamicsEnabled, _IsDetailPatch, _DetailPatchEnabled;
            int _DownwashWaveCount;
            float4 _DownwashWaves[64], _DownwashWaveMotion[64], _DownwashWaveShape;
            float _DownwashVisualTime;
            float4 _TerrainGrid;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; };
            struct v2f
            {
                float4 pos:SV_POSITION;
                float3 world:TEXCOORD0;
                float4 screen:TEXCOORD1;
                float4 grab:TEXCOORD2;
                UNITY_FOG_COORDS(3)
                SHADOW_COORDS(4)
                float terrainHeight:TEXCOORD5;
            };
            float2 SurfaceCoordinates(float3 local)
            {
                float angle=atan2(local.y,local.x);
                float reference=_WaveBufferWorldPos.x/_CylinderRadius;
                return float2((reference+atan2(sin(angle-reference),cos(angle-reference)))*_CylinderRadius,local.z);
            }
            float2 BufferUV(float2 surface) { return (surface-_WaveBufferWorldPos.xy)/_WaveBufferWorldSize+.5; }
            float PatchCoverage(float2 surface)
            {
                float radius=length(surface-_WaveBufferWorldPos.xy)/(_WaveBufferWorldSize*.5);
                return (1-smoothstep(.70,.95,radius))*_DetailPatchEnabled;
            }
            float TerrainSample(float a, float z)
            {
                float2 uv=float2((fmod(a+_TerrainGrid.x,_TerrainGrid.x)+.5)/_TerrainGrid.x,(z+.5)/(_TerrainGrid.y+1));
                return tex2Dlod(_TerrainHeightMap,float4(uv,0,0)).r;
            }
            float TerrainHeight(float2 surface)
            {
                float a=frac(surface.x/(_CylinderRadius*6.28318530718))*_TerrainGrid.x;
                float z=saturate(surface.y/_CylinderLength+.5)*_TerrainGrid.y;
                float ia=floor(a),iz=min(floor(z),_TerrainGrid.y-1);
                float u=a-ia,v=z-iz;
                float h00=TerrainSample(ia,iz),h10=TerrainSample(ia+1,iz);
                float h01=TerrainSample(ia,iz+1),h11=TerrainSample(ia+1,iz+1);
                // Same triangle diagonal and interpolation as the terrain mesh.
                float height=u<=v ? h00*(1-v)+h01*(v-u)+h11*u : h00*(1-u)+h10*(u-v)+h11*v;
                return height-_TerrainGrid.z;
            }
            float4 Dynamics(float2 surface)
            {
                float2 uv=BufferUV(surface);
                float edge=PatchCoverage(surface);
                return tex2Dlod(_WaveBuffer,float4(saturate(uv),0,0))*edge*_DynamicsEnabled;
            }
            float Height(float2 surface)
            {
                float h=Dynamics(surface).r*.12;
                for(int wave=0;wave<min(_DownwashWaveCount,64);wave++)
                    h+=GraceMovingRingHeight(surface,_DownwashWaves[wave],_DownwashWaveMotion[wave],_DownwashWaveShape,_DownwashVisualTime);
                return clamp(h,-.45,.45)*PatchCoverage(surface);
            }
            float DistantBlend(float eyeDepth)
            {
                return smoothstep(_DistantStart,max(_DistantStart+1,_DistantEnd),eyeDepth);
            }
            float3 DistantWater(float3 normal, float3 view, float3 world, float terrainHeight, float attenuation)
            {
                // Mesh height is stable even when far scene-depth samples quantize.
                float density=1-exp(-max(0,-terrainHeight)*_Density);
                float3 color=lerp(_ScatteringColor.rgb,_AbsorptionColor.rgb,density);
                color*=CylinderWaterIrradiance(normal,world,attenuation);
                float fresnel=.02+.98*pow(1-saturate(dot(normal,view)),5);
                float3 reflection=DecodeHDR(UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0,reflect(-view,normal),5),unity_SpecCube0_HDR);
                return lerp(color,reflection,fresnel*.65);
            }
            v2f vert(appdata v)
            {
                v2f o;
                if(_IsDetailPatch>.5)
                {
                    float2 surface=v.vertex.xz+_WaveBufferWorldPos.xy;
                    float angle=surface.x/_CylinderRadius;
                    // Match the coarse sea's polygon exactly at the detail boundary.
                    float offset=frac(angle/_CylinderAngularStep)*_CylinderAngularStep-_CylinderAngularStep*.5;
                    float radius=_CylinderRadius*cos(_CylinderAngularStep*.5)/cos(offset);
                    radius-=Height(surface);
                    v.vertex=float4(cos(angle)*radius,sin(angle)*radius,surface.y,1);
                }
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.terrainHeight=v.color.r-v.color.g;
                o.pos=UnityObjectToClipPos(v.vertex);
                // Bias depth only: keep the visible coastline and physical sea level
                // fixed. Fade in two depth-buffer units after the near-water range.
                float depthBias=DistantBlend(o.pos.w)*_DistantDepthBias/16777216.0;
                #if defined(UNITY_REVERSED_Z)
                    o.pos.z+=depthBias*o.pos.w;
                #else
                    // OpenGL's clip-depth range is [-1,1], rather than [0,1].
                    o.pos.z-=depthBias*(1-UNITY_NEAR_CLIP_VALUE)*o.pos.w;
                #endif
                o.screen=ComputeScreenPos(o.pos);
                o.grab=ComputeGrabScreenPos(o.pos);
                UNITY_TRANSFER_FOG(o,o.pos);
                TRANSFER_SHADOW(o);
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 local=mul(_CylinderWorldToLocal,float4(i.world,1)).xyz;
                float2 surface=SurfaceCoordinates(local);
                // At habitat scale the depth buffer cannot reliably distinguish
                // shallow water from terrain. Both meshes use this same height grid.
                float terrainHeight=TerrainHeight(surface);
                clip(-terrainHeight-.001);
                float angle=atan2(local.y,local.x);
                float sector=floor(frac(angle/6.28318530718)*_LandStripes*2);
                if(fmod(sector,2)>.5 || abs(local.z)>_CylinderLength*.5) discard;
                float2 uv=BufferUV(surface);
                float coverage=PatchCoverage(surface);
                // Complementary coverage avoids drawing two water layers or exposing
                // a hard replacement square. Both sides use identical surface data.
                float threshold=frac(52.9829189*frac(dot(floor(i.pos.xy),float2(.06711056,.00583715))));
                if(_IsDetailPatch>.5) clip(coverage-threshold-.00001);
                else clip(threshold-coverage);
                float3 radial=normalize(float3(local.xy,0));
                float3 tangent=float3(-radial.y,radial.x,0);
                float distant=DistantBlend(i.screen.w);
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float3 farNormal=normalize(mul((float3x3)_CylinderLocalToWorld,-radial));
                float attenuation=CylinderTransparentShadow(i.world);
                float3 farColor=DistantWater(farNormal,view,i.world,terrainHeight,attenuation);
                if(distant>=.999)
                {
                    // No depth/refraction, foam or subpixel ripple sampling at range.
                    fixed4 farResult=fixed4(farColor,1);
                    UNITY_APPLY_FOG(i.fogCoord,farResult);
                    return farResult;
                }
                float2 scroll=surface*.08+_Time.y*float2(.012,.007);
                float3 map=UnpackNormal(tex2D(_Normal,scroll));
                map.xy+=UnpackNormal(tex2D(_Normal,scroll*.63-_Time.y*.01)).xy*.5;
                float4 dynamics=Dynamics(surface);
                float dx=(Height(surface+float2(.08,0))-Height(surface-float2(.08,0)))/.16;
                float dz=(Height(surface+float2(0,.08))-Height(surface-float2(0,.08)))/.16;
                float3 normalLocal=normalize(-radial+tangent*(map.x*.18-dx)+float3(0,0,map.y*.18-dz));
                float3 normal=normalize(mul((float3x3)_CylinderLocalToWorld,normalLocal));
                float fresnel=.02+.98*pow(1-saturate(dot(normal,view)),5);
                float sceneDepth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                float depth=max(0,sceneDepth-i.screen.w);
                float density=1-exp(-depth*_Density);
                float foamTexture=tex2D(_FoamMap,surface*.35+_Time.y*.02).r;
                float shoreFade=1-smoothstep(100,500,i.screen.w);
                float foam=saturate((1-saturate(depth/max(.01,_FoamDepth)))*shoreFade+dynamics.g)*foamTexture;
                float2 offset=mul((float3x3)UNITY_MATRIX_V,normal).xy*_Refraction;
                float4 grab=i.grab;grab.xy+=offset*grab.w;
                float3 background=tex2Dproj(_CylinderWaterGrab,UNITY_PROJ_COORD(grab)).rgb;
                float3 color=lerp(_ScatteringColor.rgb,_AbsorptionColor.rgb,density);
                float3 light=normalize(UnityWorldSpaceLightDir(i.world));
                float diffuse=saturate(dot(normal,light));
                color*=CylinderWaterIrradiance(normal,i.world,attenuation);
                float3 halfVector=normalize(light+view);
                float specular=pow(saturate(dot(normal,halfVector)),lerp(16,256,_Smoothness))*diffuse;
                float3 reflected=reflect(-view,normal);
                float3 reflection=DecodeHDR(UNITY_SAMPLE_TEXCUBE(unity_SpecCube0,reflected),unity_SpecCube0_HDR);
                color=lerp(background,color,lerp(.18,.9,density));
                color=lerp(color,reflection,fresnel*.65)+_LightColor0.rgb*specular*attenuation*.45*CylinderHousingTransmission(i.world,light);
                float3 foamColor=float3(.82,.94,1)*saturate(CylinderWaterIrradiance(farNormal,i.world,attenuation));
                color=lerp(color,foamColor,foam*.8);
                fixed4 result=fixed4(lerp(color,farColor,distant),lerp(.96,1,distant));
                UNITY_APPLY_FOG(i.fogCoord,result);
                return result;
            }
            ENDCG
        }
    }
}
