Shader "Custom/CylinderStandard"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 0
        _MetallicGlossMap ("Metallic / Smoothness", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = .5
        _GlossMapScale ("Smoothness scale", Range(0,1)) = 1
        [Enum(Metallic Alpha,0,Albedo Alpha,1)] _SmoothnessTextureChannel ("Smoothness source", Float) = 0
        _BumpScale ("Normal scale", Float) = 1
        [Normal] _BumpMap ("Normal map", 2D) = "bump" {}
        _OcclusionStrength ("Occlusion strength", Range(0,1)) = 1
        _OcclusionMap ("Occlusion", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission color", Color) = (0,0,0,1)
        _EmissionMap ("Emission", 2D) = "white" {}
        [ToggleOff] _SpecularHighlights ("Specular highlights", Float) = 1
        [ToggleOff] _GlossyReflections ("Glossy reflections", Float) = 1
        [HideInInspector] _Mode ("Mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf CylinderStandard fullforwardshadows addshadow exclude_path:deferred
        #pragma shader_feature_local _NORMALMAP
        #pragma shader_feature_local _METALLICGLOSSMAP
        #pragma shader_feature_local _EMISSION
        #pragma shader_feature_local _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
        #pragma shader_feature_local _GLOSSYREFLECTIONS_OFF
        #include "UnityPBSLighting.cginc"
        #include "UnityStandardUtils.cginc"
        #include "../Water/Shaders/CylinderShadows.cginc"

        inline half4 LightingCylinderStandard(SurfaceOutputStandard s, float3 viewDir, UnityGI gi)
        {
            return LightingStandard(s, viewDir, gi);
        }

        inline void LightingCylinderStandard_GI(SurfaceOutputStandard s, UnityGIInput data, inout UnityGI gi)
        {
            LightingStandard_GI(s, data, gi);
            // Ambient/GI, reflection probes and material emission stay unchanged.
            gi.light.color *= CylinderHousingTransmission(data.worldPos, data.light.dir);
        }

        sampler2D _MainTex, _MetallicGlossMap, _BumpMap, _OcclusionMap, _EmissionMap;
        fixed4 _Color;
        half4 _EmissionColor;
        half _Metallic, _Glossiness, _GlossMapScale, _BumpScale, _OcclusionStrength;
        struct Input { float2 uv_MainTex; };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = albedo.rgb * _Color.rgb;
            o.Alpha = 1;
            #if defined(_METALLICGLOSSMAP)
                half4 metallic = tex2D(_MetallicGlossMap, IN.uv_MainTex);
                o.Metallic = metallic.r;
                o.Smoothness = metallic.a * _GlossMapScale;
            #else
                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;
            #endif
            #if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
                o.Smoothness = albedo.a * _GlossMapScale;
            #endif
            #if defined(_NORMALMAP)
                o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            #endif
            o.Occlusion = lerp(1, tex2D(_OcclusionMap, IN.uv_MainTex).g, _OcclusionStrength);
            #if defined(_EMISSION)
                o.Emission = tex2D(_EmissionMap, IN.uv_MainTex).rgb * _EmissionColor.rgb;
            #else
                o.Emission = 0;
            #endif
        }
        ENDCG
    }
    Fallback "Standard"
}
