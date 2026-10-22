// The soft light along the world map's coasts (MapWorldTR): the design strokes each coast 9 points wide in #A9E0DA at
// 22 % and 4 points wide in #BFEAE4 at 40 % under the land, so only the sea's half shows. Drawn on the coastlines'
// mesh (MapLine.shader says how a line keeps its width), this shader works out the colour those two strokes give
// over the sea at each pixel, the sea's own colour computed as the sea is (MapCommon.hlsl), and keeps whichever is
// lighter of that and what is there already. A coast that folds over itself, as fjords and islands do at the
// world's zoom, so lights each pixel once, as one stroke of the whole path would, and never twice.
Shader "Puzzle/Map Glow"
{
    Properties
    {
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        BlendOp Max
        Blend One One
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex GlowVertex
            #pragma fragment GlowFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MapCommon.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 mitre : TEXCOORD0;
                float2 sideAndAlong : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 sheet : TEXCOORD0;
                float across : TEXCOORD1;
            };

            static const float OuterHalf = 4.5;
            static const float InnerHalf = 2.0;
            static const float EdgeRoom = 0.75;

            Varyings GlowVertex(Attributes input)
            {
                Varyings output;
                float reach = OuterHalf + EdgeRoom;
                float side = input.sideAndAlong.x;
                float3 position = input.positionOS;
                position.xy += input.mitre * side * reach / max(_Camera.z, 1e-4);
                output.positionCS = TransformObjectToHClip(position);
                output.sheet = SheetPointOf(position.xy);
                output.across = side * reach;
                return output;
            }

            half4 GlowFragment(Varyings input) : SV_Target
            {
                ClipToTheSheet(input.sheet);
                float pixel = max(fwidth(input.across), 1e-4);
                float away = abs(input.across);
                half outer = saturate((OuterHalf - away) / pixel + 0.5);
                half inner = saturate((InnerHalf - away) / pixel + 0.5);
                half3 colour = SeaColourAt(input.sheet.y / _Sheet.y);
                colour = lerp(colour, half3(0.6627, 0.8784, 0.8549), 0.22 * outer);
                colour = lerp(colour, half3(0.7490, 0.9176, 0.8941), 0.40 * inner);
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
}
