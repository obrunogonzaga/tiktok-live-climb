Shader "Climb/Cloud Volume"
{
 Properties { _LitColor("Moonlit edges",Color)=(0.42,0.58,0.86,1) _ShadeColor("Cloud shadow",Color)=(0.035,0.065,0.16,1) _Density("Density",Float)=9 _Seed("Seed",Float)=0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
  Pass
  {
   Blend One OneMinusSrcAlpha
   ZWrite Off
   Cull Back
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.5
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _LitColor, _ShadeColor; float _Density, _Seed;
   CBUFFER_END
   struct Attributes { float4 positionOS:POSITION; };
   struct Varyings { float4 positionCS:SV_POSITION; float3 positionOS:TEXCOORD0; };
   Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.positionOS=v.positionOS.xyz;return o; }
   float hash(float3 p) { p=frac(p*.3183099+float3(.11,.17,.13));p*=17;return frac(p.x*p.y*p.z*(p.x+p.y+p.z)); }
   float noise(float3 p)
   {
    float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);
   }
   float density(float3 p)
   {
    float3 q=p*14+float3(_Seed,_Time.y*.025,0);
    float n=noise(q)*.57+noise(q*2.07)*.28+noise(q*4.13)*.15;
    float shape=-1;
    shape=max(shape,.27-length((p-float3(-.27,-.02,.02))*float3(1,1.45,1.2)));
    shape=max(shape,.31-length((p-float3(-.05,.06,-.03))*float3(1,1.2,1.1)));
    shape=max(shape,.27-length((p-float3(.18,.02,.05))*float3(1,1.35,1.2)));
    shape=max(shape,.19-length((p-float3(.36,-.04,0))*float3(1,1.5,1.2)));
    return saturate((shape+(n-.6)*.18)*9);
   }
   half4 frag(Varyings i):SV_Target
   {
    float3 origin=TransformWorldToObject(GetCameraPositionWS());float3 direction=normalize(i.positionOS-origin);
    float3 inv=rcp(direction);float3 t0=(-.5-origin)*inv,t1=(.5-origin)*inv;
    float nearT=max(max(min(t0.x,t1.x),min(t0.y,t1.y)),min(t0.z,t1.z));
    float farT=min(min(max(t0.x,t1.x),max(t0.y,t1.y)),max(t0.z,t1.z));
    nearT=max(nearT,0);if(farT<=nearT)return 0;
    float stepT=(farT-nearT)/40;float4 result=0;
    for(int s=0;s<40;s++)
    {
     float3 p=origin+direction*(nearT+(s+.5)*stepT);float d=density(p);
     float shadow=density(p+float3(-.05,.1,-.04))+density(p+float3(-.1,.2,-.08));
     float light=.16+.84*exp(-shadow*2.2);
     float alpha=1-exp(-d*stepT*_Density);
     float3 color=lerp(_ShadeColor.rgb,_LitColor.rgb,light);
     result.rgb+=(1-result.a)*alpha*color;result.a+=(1-result.a)*alpha;
     if(result.a>.97)break;
    }
    return result;
   }
   ENDHLSL
  }
 }
}
