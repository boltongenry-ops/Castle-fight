Shader "CastleFight/ReforgedUnit" {
 Properties {
  _MainTex("Reforged diffuse",2D)="white" {}
  _NormalMap("Reforged normal",2D)="bump" {}
  _ORMMap("Reforged ORM",2D)="white" {}
  _EmissionMap("Reforged emission",2D)="black" {}
  _TeamMask("Reforged team mask",2D)="black" {}
  _TeamColor("Player color",Color)=(.1,.3,.95,1)
  _HasNormal("Has normal map",Float)=0
  _HasORM("Has ORM",Float)=0
  _NativeAlpha("Geoset alpha",Float)=1
  _Cutoff("Cutout",Float)=.125
  _SrcBlend("Source blend",Float)=1
  _DstBlend("Destination blend",Float)=0
  _ZWrite("Depth write",Float)=1
  _Unlit("Unlit",Float)=0
 }
 SubShader {
  Tags {"RenderType"="TransparentCutout" "Queue"="AlphaTest"}
  Cull Off
  Blend [_SrcBlend] [_DstBlend]
  ZWrite [_ZWrite]
  Pass {
   Tags {"LightMode"="ForwardBase"}
   CGPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   #include "Lighting.cginc"
   #include "UnityPBSLighting.cginc"
   sampler2D _MainTex,_NormalMap,_ORMMap,_EmissionMap,_TeamMask;
   float4 _TeamColor;float _NativeAlpha,_Cutoff,_HasNormal,_HasORM,_Unlit;
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 normal:TEXCOORD1;float3 world:TEXCOORD2;};
   v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;o.normal=UnityObjectToWorldNormal(v.normal);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
   fixed4 frag(v2f i):SV_Target {
    float4 skin=tex2D(_MainTex,i.uv);float alpha=skin.a*_NativeAlpha;clip(alpha-_Cutoff);
    float3 albedo=saturate(skin.rgb+_TeamColor.rgb*tex2D(_TeamMask,i.uv).r);
    float3 n=normalize(i.normal);float3 p1=ddx(i.world),p2=ddy(i.world);float2 u1=ddx(i.uv),u2=ddy(i.uv);
    float3 tangent=p1*u2.y-p2*u1.y;float3 bitangent=-p1*u2.x+p2*u1.x;float invLength=rsqrt(max(dot(tangent,tangent),dot(bitangent,bitangent))+1e-10);
    float3 bump=tex2D(_NormalMap,i.uv).xyz*2-1;
    if(_HasNormal>.5)n=normalize(tangent*invLength*bump.x+bitangent*invLength*bump.y+n*bump.z);
    float3 orm=lerp(float3(1,.7,0),tex2D(_ORMMap,i.uv).rgb,_HasORM);
    float3 specular;float oneMinusReflectivity;float3 diffuse=DiffuseAndSpecularFromMetallic(albedo,orm.b,specular,oneMinusReflectivity);
    UnityLight light;light.color=_LightColor0.rgb;light.dir=normalize(UnityWorldSpaceLightDir(i.world));light.ndotl=saturate(dot(n,light.dir));
    UnityIndirect indirect;indirect.diffuse=max(UNITY_LIGHTMODEL_AMBIENT.rgb,ShadeSH9(float4(n,1)))*orm.r;indirect.specular=UNITY_LIGHTMODEL_AMBIENT.rgb*.55*orm.r;
    float3 color=UNITY_BRDF_PBS(diffuse,specular,oneMinusReflectivity,1-orm.g,n,normalize(UnityWorldSpaceViewDir(i.world)),light,indirect).rgb;
    color+=albedo*.06*orm.r+tex2D(_EmissionMap,i.uv).rgb;
    return float4(lerp(color,albedo,_Unlit),alpha);
   }
   ENDCG
  }
 }
 Fallback "Diffuse"
}
