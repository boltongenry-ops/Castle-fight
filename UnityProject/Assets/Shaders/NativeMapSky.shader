Shader "CastleFight/NativeMapSky" {
    Properties {
        _Top("Sky top",Color)=(0.22,0.42,0.70,1)
        _Horizon("Sky horizon",Color)=(0.74,0.83,0.92,1)
        _Ground("Ground bottom",Color)=(0.14,0.12,0.08,1)
        _GroundHorizon("Ground horizon",Color)=(0.42,0.37,0.26,1)
    }
    SubShader {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Top,_Horizon,_Ground,_GroundHorizon;
            struct v2f {float4 vertex:SV_POSITION;float3 direction:TEXCOORD0;};
            v2f vert(appdata_base v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;}
            fixed4 frag(v2f i):SV_Target {float y=normalize(i.direction).y;return y>=0?lerp(_Horizon,_Top,pow(saturate(y),.45)):lerp(_GroundHorizon,_Ground,pow(saturate(-y),.45));}
            ENDCG
        }
    }
}
