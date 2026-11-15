Shader "CastleFight/CentaurGlow" {
    Properties {
        _MainTex("Native team glow",2D)="white" {}
        _TeamColor("Team color",Color)=(.1,.3,.95,1)
        _Tint("Native tint",Color)=(1,1,1,1)
        _NativeAlpha("Native alpha",Range(0,1))=1
        _SrcBlend("Source blend",Float)=5
        _DstBlend("Destination blend",Float)=1
        _ZWrite("Depth write",Float)=0
        _Cutoff("Alpha cutoff",Float)=.002
    }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Cull Off ZWrite [_ZWrite] Blend [_SrcBlend] [_DstBlend]
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _MainTex_ST;fixed4 _TeamColor,_Tint;float _NativeAlpha,_Cutoff;
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata_base v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.texcoord,_MainTex);return o;}
            fixed4 frag(v2f i):SV_Target{fixed4 glow=tex2D(_MainTex,i.uv);float alpha=glow.a*_NativeAlpha*_Tint.a;clip(alpha-_Cutoff);float value=max(glow.r,max(glow.g,glow.b));return fixed4(value*_TeamColor.rgb*_Tint.rgb,alpha);}
            ENDCG
        }
    }
}
