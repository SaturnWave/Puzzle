// The dark at the world map's edges (MapWorldTR): a radial gradient over the whole sheet in the sheet's own
// proportions, centred at half its width and 42 % of its height, clear out to 55 % of 0.78 and #04242B at 38 % at
// 0.78 and beyond. Drawn on one quad as large as the sheet, uv from its top left corner as shares of its sides.
Shader "Puzzle/Map Vignette"
{
    Properties
    {
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex VignetteVertex
            #pragma fragment VignetteFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings VignetteVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 VignetteFragment(Varyings input) : SV_Target
            {
                float reach = length(input.uv - float2(0.5, 0.42)) / 0.78;
                half dark = 0.38 * saturate((reach - 0.55) / 0.45);
                return half4(0.0157, 0.1412, 0.1686, dark);
            }
            ENDHLSL
        }
    }
}
