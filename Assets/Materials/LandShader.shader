Shader "Custom/LandShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _ValleyColor ("Valley Color", Color)= (0.08, 0.25,0.10,1)
        _LandColor ("Land Color", Color)= (0.25,0.50,15,1)
        _PeakColor ("Peak Color", Color)= (0.45, 0.30, 0.20, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            fixed4 color : COLOR;
        };
        fixed4 _ValleyColor;
        fixed4 _LandColor;
        fixed4 _PeakColor;
    
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float height = IN.color.r;
            fixed3 terrainColor = 
                height< 0.5
                ? lerp(_ValleyColor.rgb, _LandColor.rgb, height*2.0) 
                : lerp(_LandColor.rgb,_PeakColor.rgb, (height-0.5)*2.0);

            o.Albedo = terrainColor;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0f;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
