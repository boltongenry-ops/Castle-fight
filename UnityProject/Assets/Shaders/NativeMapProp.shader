Shader "CastleFight/NativeMapProp" {
    Properties {
        _MainTex("Native skin",2D)="white" {} _Color("Tint",Color)=(1,1,1,1)
        _Team("Team layer",Float)=0 _TeamColor("Team color",Color)=(0.55,0.55,0.55,1)
        _NativeAlpha("Native visibility",Float)=1 _Unlit("Unlit",Float)=0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source blend",Float)=1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend",Float)=0
        _ZWrite("Depth write",Float)=1
    }
    SubShader {
        Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"}
        Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite] Cull Off
        Pass {
            Tags {"LightMode"="ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            sampler2D _MainTex;float4 _MainTex_ST;fixed4 _Color,_TeamColor;float _Team,_NativeAlpha,_Unlit;
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float3 normal:TEXCOORD1;};
            v2f vert(appdata v){UNITY_SETUP_INSTANCE_ID(v);v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.normal=UnityObjectToWorldNormal(v.normal);return o;}
            fixed4 frag(v2f i):SV_Target {
                fixed4 c=tex2D(_MainTex,i.uv);float a=lerp(c.a,1,_Team)*_NativeAlpha*_Color.a;clip(a-.02);
                fixed3 rgb=lerp(c.rgb,lerp(_TeamColor.rgb,c.rgb,c.a),_Team)*_Color.rgb;
                fixed3 lit=UNITY_LIGHTMODEL_AMBIENT.rgb+_LightColor0.rgb*saturate(dot(normalize(i.normal),normalize(_WorldSpaceLightPos0.xyz)));
                return fixed4(rgb*lerp(lit,1,_Unlit),a);
            }
            ENDCG
        }
    }
}
