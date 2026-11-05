// The world map's sea (MapWorldTR): the teal that runs darker towards the foot of the sheet, with a faint pattern of
// waves that stays the same size at every zoom, as the design canvas draws it in screen points over its whole map.
// It is drawn on one quad as large as the sheet; the quad's uv are the sheet's points from its top left corner, y
// growing downwards, and _SheetHeight is the sheet's height in those points.
Shader "Puzzle/Map Sea"
{
    Properties
    {
        _SheetHeight("Sheet height in points", Float) = 456
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend One Zero
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex SeaVertex
            #pragma fragment SeaFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MapCommon.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 sheet : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _SheetHeight;
            CBUFFER_END

            Varyings SeaVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.sheet = input.uv;
                return output;
            }

            // The design's wave: a 26 x 14 tile, in it one line from x = 1 to 24.6 at y = 9 that rises and falls by a
            // swing of 2.55, drawn 1.1 wide in white at 7.5 %.
            half WaveAt(float2 sheet)
            {
                float2 tile = float2(fmod(sheet.x, 26.0), fmod(sheet.y, 14.0));
                float along = (tile.x - 1.0) / 23.6;
                float rise = 2.55 * sin(along * 6.2831853);
                float across = abs(tile.y - (9.0 - rise));
                float slope = 2.55 * 6.2831853 / 23.6 * cos(along * 6.2831853);
                float distance = across / sqrt(1.0 + slope * slope);
                float pixel = max(fwidth(sheet.y), 1e-4);
                half onTheLine = saturate((0.55 - distance) / pixel + 0.5);
                half inTheTile = step(1.0, tile.x) * step(tile.x, 24.6);
                return onTheLine * inTheTile * 0.075;
            }

            half4 SeaFragment(Varyings input) : SV_Target
            {
                half3 sea = SeaColourAt(input.sheet.y / _SheetHeight);
                half wave = WaveAt(input.sheet);
                return half4(lerp(sea, half3(1, 1, 1), wave), 1);
            }
            ENDHLSL
        }
    }
}
