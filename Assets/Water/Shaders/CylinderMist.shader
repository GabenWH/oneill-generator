Shader "Grace/VWater/Cylinder Mist"
{
    Properties
    {
        _ShadeOpacity("Mist opacity in shade", Range(0,1)) = .025
        _SunOpacity("Mist opacity in direct sunlight", Range(0,1)) = .35
        _Droplet("Droplet layer",Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "DownwashMistShape.cginc"
            #include "CylinderWaterLighting.cginc"
            float4x4 _CylinderWorldToLocal;
            float _CylinderRadius, _CylinderAngularStep;
            float _ShadeOpacity, _SunOpacity, _Droplet;
            sampler2D _MistViewOpacityMap;
            struct Input { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output { float4 pos:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; UNITY_FOG_COORDS(2) SHADOW_COORDS(3) };
            Output vert(Input v)
            {
                Output o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.uv=v.uv;o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);TRANSFER_SHADOW(o);return o;
            }
            fixed4 frag(Output i):SV_Target
            {
                float3 local=mul(_CylinderWorldToLocal,float4(i.world,1)).xyz;
                float3 up=-normalize(float3(local.xy,0));
                float3 view=mul((float3x3)_CylinderWorldToLocal,_WorldSpaceCameraPos-i.world);
                float vertical=dot(view,up);
                float angle=atan2(abs(vertical),length(view-up*vertical))*57.2957795;
                float alpha=GraceMistAlpha(i.uv,_Time.y*.8+local.z*2.7,_Droplet);
                float angleAround=atan2(local.y,local.x);
                float step=max(.00001,_CylinderAngularStep);
                float facetOffset=frac(angleAround/step)*step-step*.5;
                float seaRadius=_CylinderRadius*cos(step*.5)/cos(facetOffset);
                alpha*=saturate((seaRadius-length(local.xy))/.08);
                alpha*=tex2D(_MistViewOpacityMap,GraceMistViewLookupUV(angle)).r;
                // Particle simulation space is the habitat, not the billboard basis.
                float3 worldUp=normalize(mul((float3x3)transpose(_CylinderWorldToLocal),up));
                float attenuation=CylinderTransparentShadow(i.world);
                float3 lightDir=normalize(UnityWorldSpaceLightDir(i.world));
                float direct=saturate(dot(worldUp,lightDir))*attenuation*CylinderHousingTransmission(i.world,lightDir);
                alpha*=lerp(_ShadeOpacity,_SunOpacity,direct);
                fixed4 col=fixed4(i.color.rgb*saturate(CylinderWaterIrradiance(worldUp,i.world,attenuation)),alpha*i.color.a);clip(col.a-.0001);
                UNITY_APPLY_FOG(i.fogCoord,col);return col;
            }
            ENDCG
        }
    }
}
