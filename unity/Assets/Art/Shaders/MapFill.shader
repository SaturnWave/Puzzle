// The world map's land and lakes (MapWorldTR): the triangles of their outlines in the colour of each corner (the
// land's cream runs from #FBF2DA at the north to #E7D3A4 at the south, as the design's gradient over the land's
// height does), cut off where the map runs past the sheet. The mesh is in the map's units; the renderer's _Camera
// says where those lie on the sheet (MapCommon.hlsl).
Shader "Puzzle/Map Fill"
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
            #pragma vertex FillVertex
            #pragma fragment FillFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MapCommon.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 sheet : TEXCOORD0;
            };

            Varyings FillVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.sheet = SheetPointOf(input.positionOS.xy);
                return output;
            }

            half4 FillFragment(Varyings input) : SV_Target
            {
                ClipToTheSheet(input.sheet);
                return input.color;
            }
            ENDHLSL
        }
    }
}
