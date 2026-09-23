#ifndef BELT_DRAW_GEOMETRY_INCLUDED
#define BELT_DRAW_GEOMETRY_INCLUDED
StructuredBuffer<float4> _Cells, _Entries;
StructuredBuffer<int2> _Routes;
float4 _CenterOffset;
float3 PositionOnRoute(int segment, int distance, int acceptedInput)
{
    int2 route = _Routes[segment];
    int cellIndex = route.y - 1 - distance / 256;
    float progress = (256 - distance % 256) / 256.0;
    float4 current = _Cells[route.x + cellIndex];
    float4 entry = cellIndex == 0 ? _Entries[segment * 4 + acceptedInput] : _Cells[route.x + cellIndex - 1];
    float3 boundary = float3((entry.x + current.x) * 0.5, current.w, (entry.z + current.z) * 0.5);
    return (progress < 0.5 ? lerp(entry.xyz, boundary, progress * 2)
        : lerp(boundary, current.xyz, progress * 2 - 1)) + _CenterOffset.xyz;
}
#endif
