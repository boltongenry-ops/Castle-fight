Shader "CastleFight/NativeUnit" {
    Properties {
        _MainTex("WC3 skin",2D)="white" {}
        _HDTexture("HD remaster",2D)="white" {}
        _HDPresent("Has HD texture",Float)=0
        _Team("Team layer",Float)=0
        _TeamColor("Team color",Color)=(0.1,0.3,0.95,1)
        _Tint("Native object tint",Color)=(1,1,1,1)
        _NativeAlpha("Native geoset alpha",Range(0,1))=1
        _SrcBlend("Native source blend",Float)=1
        _DstBlend("Native destination blend",Float)=0
        _ZWrite("Native depth write",Float)=1
        _Unlit("Native unlit layer",Float)=0
        _Cutoff("Native alpha cutoff",Range(0,1))=.02
    }
    SubShader {
        Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"}
        Cull Off
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Pass {
            Tags {"LightMode"="ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            sampler2D _MainTex,_HDTexture;float _HDPresent,_HumanHD; float4 _MainTex_ST; fixed4 _TeamColor,_Tint; float _Team,_NativeAlpha,_Unlit,_Cutoff;
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float3 normal:TEXCOORD1;};
            v2f vert(appdata_base v) {v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.texcoord,_MainTex);o.normal=UnityObjectToWorldNormal(v.normal);return o;}
            fixed4 frag(v2f i):SV_Target {
                fixed4 skin=tex2D(_MainTex,i.uv);skin.rgb=lerp(skin.rgb,tex2D(_HDTexture,i.uv).rgb,_HDPresent*_HumanHD);float alpha=lerp(skin.a,1,_Team)*_NativeAlpha*_Tint.a;
                clip(alpha-_Cutoff);
                fixed3 rgb=lerp(skin.rgb,lerp(_TeamColor.rgb,skin.rgb,skin.a),_Team);
                fixed3 lighting=UNITY_LIGHTMODEL_AMBIENT.rgb+_LightColor0.rgb*saturate(dot(normalize(i.normal),normalize(_WorldSpaceLightPos0.xyz)));
                return fixed4(rgb*_Tint.rgb*lerp(lighting,fixed3(1,1,1),_Unlit),alpha);
            }
            ENDCG
        }
    }
}
