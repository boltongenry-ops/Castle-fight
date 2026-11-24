Shader "CastleFight/WinterFront" {
    Properties { _MainTex("WC3 atlas",2D)="white" {} _Layer("Overlay",Float)=0 _WinterTex("Snow atlas",2D)="white" {} }
    SubShader {
        Tags {"Queue"="Geometry" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            Tags {"LightMode"="ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local __ CF_WINTER_FRONT
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            sampler2D _MainTex;float _Layer;
            #ifdef CF_WINTER_FRONT
            sampler2D _WinterTex;
            float4 _CFWinterFront; // world-space origin XZ, radius, feather width
            #endif
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float3 normal:TEXCOORD1;
                #ifdef CF_WINTER_FRONT
                float2 ground:TEXCOORD2;
                #endif
            };
            v2f vert(appdata_base v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;o.normal=UnityObjectToWorldNormal(v.normal); 
                #ifdef CF_WINTER_FRONT
                o.ground=mul(unity_ObjectToWorld,v.vertex).xz;
                #endif
                return o;}
            fixed4 frag(v2f i):SV_Target {fixed4 c=tex2D(_MainTex,i.uv);c.a=lerp(1,c.a,_Layer);clip(c.a-.01);
                #ifdef CF_WINTER_FRONT
                float2 p=i.ground-_CFWinterFront.xy;
                float fringe=sin(p.x*.61+p.y*.83)*.85 + sin(p.y*1.71-p.x*.39)*.35;
                float frost=1-smoothstep(-_CFWinterFront.w,_CFWinterFront.w,
                    length(p)+fringe+(c.g-.5)*1.4-_CFWinterFront.z);
                c.rgb=lerp(c.rgb,tex2D(_WinterTex,i.uv).rgb,frost);
                #endif
                c.rgb*=UNITY_LIGHTMODEL_AMBIENT.rgb+_LightColor0.rgb*saturate(dot(normalize(i.normal),normalize(_WorldSpaceLightPos0.xyz)));return c;}
            ENDCG
        }
    }
}
