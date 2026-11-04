Shader "Grace/VWater/Downwash Mist"
{
    Properties
    {
        _Droplet("Droplet layer",Float)=0
        _WaterLevel("Water level",Float)=0
        _MistViewOpacityMap("View angle opacity",2D)="white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "DownwashMistShape.cginc"
            struct Input { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output { float4 pos:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; UNITY_FOG_COORDS(2) };
            float _Droplet,_WaterLevel;
            sampler2D _MistViewOpacityMap;
            Output vert(Input v)
            {
                Output o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.uv=v.uv;o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(Output i):SV_Target
            {
                float phase=_Time.y*0.8+i.world.x*3.1+i.world.z*2.7;
                float alpha=GraceMistAlpha(i.uv,phase,_Droplet)*saturate((i.world.y-_WaterLevel)/0.08);
                float3 toCamera=lerp(_WorldSpaceCameraPos.xyz-i.world,UNITY_MATRIX_V[2].xyz,unity_OrthoParams.w);
                float angle=GraceMistViewAngle(toCamera);
                alpha*=tex2D(_MistViewOpacityMap,GraceMistViewLookupUV(angle)).r;
                fixed4 col=fixed4(i.color.rgb,alpha*i.color.a);
                clip(col.a-0.0001);
                UNITY_APPLY_FOG(i.fogCoord,col);return col;
            }
            ENDCG
        }
    }
}
