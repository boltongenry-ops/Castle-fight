Shader "CastleFight/BlackHandSurface" {
    Properties {
        _MainTex("Diffuse", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha cutoff", Range(0,1)) = 0
    }
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _Cutoff;
            struct Varyings {
                float4 clip : SV_POSITION;
                half2 uv : TEXCOORD0;
                half light : TEXCOORD1;
            };
            Varyings vert(appdata_base input) {
                Varyings output;
                output.clip = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                half3 normal = UnityObjectToWorldNormal(input.normal);
                output.light = .48h + .62h * saturate(dot(normal, _WorldSpaceLightPos0.xyz));
                return output;
            }
            fixed4 frag(Varyings input) : SV_Target {
                fixed4 sample = tex2D(_MainTex, input.uv) * _Color;
                clip(sample.a - _Cutoff);
                return fixed4(sample.rgb * input.light, 1);
            }
            ENDCG
        }
    }
}
