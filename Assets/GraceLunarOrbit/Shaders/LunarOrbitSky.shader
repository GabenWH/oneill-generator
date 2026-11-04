Shader "Grace/Lunar Orbit Sky"
{
    Properties
    {
        _MoonMap ("Moon equirectangular albedo", 2D) = "white" {}
        _MoonCenterKm ("Moon center relative to camera", Vector) = (-1837.4,0,0,0)
        _MoonRadiusKm ("Moon radius km", Float) = 1737.4
        _SunDirection ("Direction to Sun", Vector) = (1,0,0,0)
        _SunCosRadius ("Cos solar angular radius", Float) = 0.99998918
        _HasMoonMap ("Has Moon map", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; float3 direction : TEXCOORD0; };
            sampler2D _MoonMap;
            float3 _MoonCenterKm, _SunDirection;
            float _MoonRadiusKm, _SunCosRadius, _HasMoonMap;
            float4x4 _WorldToMoon, _WorldToInertial;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.direction = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                return o;
            }
            float hash(float3 p) { return frac(sin(dot(p,float3(12.9898,78.233,37.719))) * 43758.5453); }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 ray = normalize(i.direction);
                float3 sun = normalize(_SunDirection);
                float b = dot(ray, _MoonCenterKm);
                float c = dot(_MoonCenterKm,_MoonCenterKm) - _MoonRadiusKm*_MoonRadiusKm;
                float discriminant = b*b-c;
                if (discriminant >= 0 && b > 0)
                {
                    float t = b - sqrt(discriminant);
                    if (t > 0)
                    {
                        float3 n = normalize(ray*t-_MoonCenterKm);
                        float3 mapN = mul((float3x3)_WorldToMoon,n);
                        // Z is lunar north in the orbital model. A lat/long texture
                        // maps longitude horizontally and north at the top.
                        float2 uv = float2(atan2(mapN.y,mapN.x)/(2*UNITY_PI)+0.5,
                            asin(clamp(mapN.z,-1,1))/UNITY_PI+0.5);
                        float3 albedo = lerp(float3(0.38,0.38,0.37),tex2D(_MoonMap,uv).rgb,_HasMoonMap);
                        return fixed4(albedo * (0.018 + 0.982*max(0,dot(n,sun))),1);
                    }
                }
                float solar = dot(ray,sun);
                float width = max(fwidth(solar),0.0000001);
                float disc = smoothstep(_SunCosRadius-width,_SunCosRadius+width,solar);
                float glow = pow(max(0,solar),3000)*0.05;
                float3 inertialRay = mul((float3x3)_WorldToInertial,ray);
                float star = step(0.9997,hash(floor(inertialRay*1600))) * 0.65;
                return fixed4(float3(0.001,0.0015,0.003)+star+float3(1,0.96,0.84)*(disc+glow),1);
            }
            ENDCG
        }
    }
    Fallback Off
}
