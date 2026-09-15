Shader "Rortype/FootstepRing"
{
 Properties { _TintColor ("Dust tint", Color) = (0.68,0.70,0.72,0.85) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Name "VFXForward"
   Tags { "LightMode"="SRPDefaultUnlit" }
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
   struct V { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
   CBUFFER_START(UnityPerMaterial)
   half4 _TintColor;
   CBUFFER_END
   V vert(A input) { V o; o.vertex=TransformObjectToHClip(input.vertex.xyz); o.uv=input.uv; return o; }
   half4 frag(V input):SV_Target
   {
    float2 p = input.uv * 2 - 1;
    float r = length(p);
    float angle = atan2(p.y,p.x);
    float irregularity = sin(angle*9)*0.018 + sin(angle*17)*0.012;
    float ring = smoothstep(0.49,0.64,r+irregularity) * (1-smoothstep(0.72,0.94,r+irregularity));
    float dust = 0.72 + 0.28*sin(angle*13 + r*29);
    return half4(_TintColor.rgb, _TintColor.a*ring*dust);
   }
   ENDHLSL
  }
 }
}