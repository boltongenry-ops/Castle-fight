Shader "CastleFight/PcOverlay" {
    Properties { _Color("Color",Color)=(0.2,1,0.4,0.7) }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1,-1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct v2f { float4 vertex:SV_POSITION; };
            v2f vert(appdata_base v) { v2f o;o.vertex=UnityObjectToClipPos(v.vertex);return o; }
            fixed4 frag(v2f i):SV_Target {return _Color;}
            ENDCG
        }
    }
}
