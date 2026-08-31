Shader "Custom/GlassShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Tint ("Tint", Color) = (0.5, 0.65, 0.7, 1)
        _BaseAlpha ("Base Alpha", Range(0,1)) = 0.02
        _EdgeAlpha ("Edge Alpha", Range(0,1)) = 0.4
        _FresnelPower ("Fresnel Power", Range(0.1,10)) = 3
        _Smoothness ("Smoothness", Range(0,1)) = 0.95
    }
    SubShader
    {
        Tags{
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard alpha:premul

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };
        fixed4 _Tint;
        half _BaseAlpha;
        half _EdgeAlpha;
        half _FresnelPower;
        half _Smoothness;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {

            // Albedo NO CUSTOM TEXTURES ON MY GLASS 🔫🤓
            o.Albedo = _Tint.rgb;
            // no metallic
            o.Metallic = 0;
            o.Smoothness = _Smoothness;

            //Self explanitory
            float3 viewDir= normalize(_WorldSpaceCameraPos.xyz - IN.worldPos);
            float3 normal = normalize(IN.worldNormal);

            //A bunch of math that says hey change the opacity a little bit why don't ya
            float frensel = 1.0 - saturate(abs(dot(normal,viewDir)));
            frensel = pow(frensel,_FresnelPower);
            o.Alpha = lerp(_BaseAlpha,_EdgeAlpha,frensel);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
