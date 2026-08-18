Shader "ThesisAR/HologramURP"
{
    // Drop-in replacement for the avatar's Lit material. No "Surface Type = Transparent"
    // toggle needed in the Inspector — this shader is already always-transparent by design.
    //
    // Compatible as-is with the existing HologramEffectController.cs: that script only ever
    // touches ".color" (maps to _Color below) and "_EmissionColor" (below), both still present
    // with the same names, so the flicker/pulse/jitter script keeps working completely unchanged.
    // The rim glow and scanlines added here are a static "look" layered underneath that.

    Properties
    {
        [HDR] _Color ("Base Tint (alpha = base transparency)", Color) = (0, 0.65, 1, 0.55)
        [HDR] _EmissionColor ("Emission Pulse (driven by script)", Color) = (0, 0, 0, 0)

        [Header(Rim Glow)]
        [HDR] _RimColor ("Rim Glow Color", Color) = (0.3, 0.9, 1, 1)
        _RimPower ("Rim Power (higher = thinner edge)", Range(0.5, 8)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0, 5)) = 2.0

        [Header(Scan Lines)]
        _ScanLineSpeed ("Scan Line Scroll Speed", Range(0, 10)) = 1.5
        _ScanLineDensity ("Scan Line Density", Range(1, 300)) = 60
        _ScanLineIntensity ("Scan Line Brightness", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardHologram"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
                float  localY      : TEXCOORD2; // object-space height, used for scanlines so
                                                 // they stay fixed on the character's surface
                                                 // instead of sliding as it moves through the world
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _EmissionColor;
                float4 _RimColor;
                float  _RimPower;
                float  _RimIntensity;
                float  _ScanLineSpeed;
                float  _ScanLineDensity;
                float  _ScanLineIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vpi.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(vpi.positionWS);
                OUT.localY = IN.positionOS.y;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);

                // Fresnel rim glow: brighter where the surface faces away from the camera
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDirWS)), _RimPower);
                float3 rim = _RimColor.rgb * fresnel * _RimIntensity;

                // Horizontal scanlines scrolling upward over time, fixed to the mesh surface
                float scanRaw = sin((IN.localY * _ScanLineDensity) - (_Time.y * _ScanLineSpeed));
                float scanLine = smoothstep(0.85, 1.0, scanRaw) * _ScanLineIntensity;

                // Tint scanlines with cyan rim color instead of raw white
                float3 finalColor = _Color.rgb + _EmissionColor.rgb + rim + (scanLine * _RimColor.rgb);
                float finalAlpha = saturate(_Color.a + fresnel * 0.4 + scanLine);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
