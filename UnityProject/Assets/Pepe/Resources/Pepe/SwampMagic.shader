Shader "CastleFight/SwampMagic" {
    SubShader {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off Lighting Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Vertex {float4 pos:POSITION; fixed4 color:COLOR;};
            struct Fragment {float4 pos:SV_POSITION; fixed4 color:COLOR;};
            Fragment vert(Vertex v){Fragment o;o.pos=UnityObjectToClipPos(v.pos);o.color=v.color;return o;}
            fixed4 frag(Fragment f):SV_Target{return f.color;}
            ENDCG
        }
    }
}
