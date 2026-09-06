Shader "Climb/Photographic Star Sky"
{
 Properties { _MainTex("Photographed star field",2D)="black"{} _Rotation("Azimuth",Float)=0 _Exposure("Starlight",Float)=1 }
 SubShader
 {
  Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
  Cull Off ZWrite Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float _Rotation,_Exposure;
   CBUFFER_END
   struct Attributes{float4 positionOS:POSITION;};
   struct Varyings{float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;};
   Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.direction=v.positionOS.xyz;return o;}
   half4 frag(Varyings i):SV_Target
   {
    float3 d=normalize(i.direction);
    // Sample only the photographed sky hemisphere: no golf course or terrestrial horizon.
    float2 uv=float2(atan2(d.x,d.z)/6.2831853+.5+_Rotation/360, .65+asin(clamp(d.y,-1,1))*.3183099);
    float3 photo=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb;
    float l=dot(photo,float3(.2126,.7152,.0722));
    float stars=pow(max(l-.035,0),2.1)*16*_Exposure;
    float3 night=lerp(float3(.008,.024,.085),float3(.002,.005,.022),saturate(d.y*.7+.4));
    return half4(night+photo*float3(.035,.06,.13)+stars*float3(.68,.8,1),1);
   }
   ENDHLSL
  }
 }
}
