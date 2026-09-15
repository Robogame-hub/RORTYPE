Shader "Rortype/BolterParticles"
{
 Properties
 {
  _MainTex ("Texture", 2D) = "white" {}
  _TintColor ("Tint", Color) = (1,1,1,1)
  [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 1
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Name "VFXForward"
   Tags { "LightMode"="SRPDefaultUnlit" }
   Blend SrcAlpha [_DstBlend]
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float4 _MainTex_ST;
   half4 _TintColor;
   CBUFFER_END
   Varyings vert(Attributes input)
   {
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = TRANSFORM_TEX(input.uv, _MainTex);
    output.color = input.color * _TintColor;
    return output;
   }
   half4 frag(Varyings input):SV_Target
   { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color; }
   ENDHLSL
  }
 }
}