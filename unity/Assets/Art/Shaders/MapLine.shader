// The world map's lines (MapWorldTR): the coastlines, the lakes' shores and the dashed graticule. A line keeps its
// width in points at every zoom, as the design's non-scaling strokes do: its mesh holds each point twice, on the
// line itself, with the way out to either side (the mitre, uv) and which side (uv2.x), and this shader moves each
// corner out by half the line's width in points, turned into map units by the camera's zoom. Its edges are smoothed
// over a pixel. A dashed line counts its way along (uv2.y, in map units) in points: _Dash is the period of a dash
// and its gap, nothing for a solid line.
Shader "Puzzle/Map Line"
{
    Properties
    {
        _Colour("Colour", Color) = (0.58, 0.443, 0.235, 1)
        _HalfWidth("Half the width in points", Float) = 0.55
        _Dash("Dash and gap in points", Float) = 0
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
            #pragma vertex LineVertex
            #pragma fragment LineFragment

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
                float along : TEXCOORD2;
            };

            // A line reaches this far past its half width, in points, so its smoothed edge has room.
            static const float EdgeRoom = 0.75;

            float4 _Colour;
            float _HalfWidth;
            float _Dash;

            Varyings LineVertex(Attributes input)
            {
                Varyings output;
                float reach = _HalfWidth + EdgeRoom;
                float side = input.sideAndAlong.x;
                float3 position = input.positionOS;
                position.xy += input.mitre * side * reach / max(_Camera.z, 1e-4);
                output.positionCS = TransformObjectToHClip(position);
                output.sheet = SheetPointOf(position.xy);
                output.across = side * reach;
                output.along = input.sideAndAlong.y * _Camera.z;
                return output;
            }

            half4 LineFragment(Varyings input) : SV_Target
            {
                ClipToTheSheet(input.sheet);
                float pixel = max(fwidth(input.across), 1e-4);
                half cover = saturate((_HalfWidth - abs(input.across)) / pixel + 0.5);
                if (_Dash > 0)
                {
                    float phase = frac(input.along / _Dash);
                    float blur = max(fwidth(input.along / _Dash), 1e-4);
                    cover *= saturate((0.25 - abs(phase - 0.25)) / blur + 0.5);
                }

                return half4(_Colour.rgb, _Colour.a * cover);
            }
            ENDHLSL
        }
    }
}
