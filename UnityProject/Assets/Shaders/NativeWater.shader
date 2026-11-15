Shader "CastleFight/NativeWater" {
    Properties { _MainTex("Native ASha atlas",2D)="white" {} _CaptureTime("Fixed time (-1 = animate)",Float)=-1 }
    SubShader {
        Tags {"Queue"="Transparent-5" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float _CaptureTime;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            fixed4 frag(v2f i):SV_Target {
                float time=_CaptureTime>=0?_CaptureTime:_Time.y;float frame=floor(fmod(time*12,45));
                float2 cell=float2(fmod(frame,8),floor(frame/8));
                float2 tile=float2(saturate(i.uv.x),1-saturate(i.uv.y))*(1-1.0/128)+.5/128;
                float2 uv=(cell+tile)/float2(8,6);uv.y=1-uv.y;
                return tex2D(_MainTex,uv)*i.color;
            }
            ENDCG
        }
    }
}
