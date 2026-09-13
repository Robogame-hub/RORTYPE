Shader "RorType/Player/GhostFresnelThroughOccluders"
{
    Properties
    {
        _GhostColor ("Ghost Color", Color) = (0.18, 0.72, 1, 0.85)
        _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.08
        _RimAlpha ("Rim Alpha", Range(0, 2)) = 0.85
        _FresnelPower ("Fresnel Power", Range(0.25, 8)) = 2.2
        _GhostVisibility ("Ghost Visibility", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite Off
            ZTest Always
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _GhostColor;
            float _BaseAlpha;
            float _RimAlpha;
            float _FresnelPower;
            float _GhostVisibility;
            CBUFFER_END

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldViewDir : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = TransformObjectToHClip(input.vertex.xyz);

                float3 worldPosition = TransformObjectToWorld(input.vertex.xyz);
                output.worldNormal = TransformObjectToWorldNormal(input.normal);
                output.worldViewDir = GetWorldSpaceViewDir(worldPosition);
                return output;
            }

            half4 frag(v2f input) : SV_Target
            {
                float3 normal = normalize(input.worldNormal);
                float3 viewDir = normalize(input.worldViewDir);
                float rim = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);
                float alpha = saturate((_BaseAlpha + rim * _RimAlpha) * _GhostVisibility) * _GhostColor.a;
                float3 color = _GhostColor.rgb * (0.45 + rim * 1.35);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
