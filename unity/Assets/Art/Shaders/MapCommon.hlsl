// What the world map's shaders share (MapWorldTR on the design canvas): the sea's colours, and the camera that puts
// the map's units on the sheet.
#ifndef PUZZLE_MAP_COMMON_INCLUDED
#define PUZZLE_MAP_COMMON_INCLUDED

// The sea runs from #2F959A at the sheet's top through #22808A at 55 % to #176670 at its foot.
half3 SeaColourAt(float downTheSheet)
{
    const half3 top = half3(0.1843, 0.5843, 0.6039);
    const half3 middle = half3(0.1333, 0.5020, 0.5412);
    const half3 foot = half3(0.0902, 0.4000, 0.4392);
    float t = saturate(downTheSheet);
    return t < 0.55 ? lerp(top, middle, t / 0.55) : lerp(middle, foot, (t - 0.55) / 0.45);
}

// The map's camera, set per renderer: a point of the map in its own units (x eastwards, y northwards as the mesh
// holds it, which is the map's y turned over) lies on the sheet at (_Camera.x + _Camera.z * x, _Camera.y - _Camera.z * y)
// points from the sheet's top left corner, y growing downwards. _Sheet is the sheet's width and height in points.
float4 _Camera;
float4 _Sheet;

float2 SheetPointOf(float2 mapPoint)
{
    return float2(_Camera.x + _Camera.z * mapPoint.x, _Camera.y - _Camera.z * mapPoint.y);
}

// Nothing of the map is drawn off the sheet: the frame stands over its edge.
void ClipToTheSheet(float2 sheetPoint)
{
    clip(sheetPoint.x);
    clip(sheetPoint.y);
    clip(_Sheet.x - sheetPoint.x);
    clip(_Sheet.y - sheetPoint.y);
}

#endif
