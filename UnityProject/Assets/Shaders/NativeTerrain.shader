Shader "CastleFight/NativeTerrain" {
    Properties { _MainTex("WC3 atlas",2D)="white" {} _Layer("Overlay",Float)=0 }
    SubShader {
        Tags {"Queue"="Geometry" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            Tags {"LightMode"="ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            sampler2D _MainTex;float _Layer;
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float3 normal:TEXCOORD1;};
            v2f vert(appdata_base v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;o.normal=UnityObjectToWorldNormal(v.normal);return o;}
            fixed4 frag(v2f i):SV_Target {fixed4 c=tex2D(_MainTex,i.uv);c.a=lerp(1,c.a,_Layer);clip(c.a-.01);c.rgb*=UNITY_LIGHTMODEL_AMBIENT.rgb+_LightColor0.rgb*saturate(dot(normalize(i.normal),normalize(_WorldSpaceLightPos0.xyz)));return c;}
            ENDCG
        }
    }
}
