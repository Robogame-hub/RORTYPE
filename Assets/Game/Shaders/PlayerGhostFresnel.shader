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
        }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _GhostColor;
            float _BaseAlpha;
            float _RimAlpha;
            float _FresnelPower;
            float _GhostVisibility;

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
                output.position = UnityObjectToClipPos(input.vertex);

                float3 worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.worldViewDir = _WorldSpaceCameraPos.xyz - worldPosition;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 normal = normalize(input.worldNormal);
                float3 viewDir = normalize(input.worldViewDir);
                float rim = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);
                float alpha = saturate((_BaseAlpha + rim * _RimAlpha) * _GhostVisibility) * _GhostColor.a;
                float3 color = _GhostColor.rgb * (0.45 + rim * 1.35);
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
