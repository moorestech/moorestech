#ifndef BELT_GPU_EXTERNAL_INCLUDED
#define BELT_GPU_EXTERNAL_INCLUDED
#include "BeltGpuPorts.hlsl"

[numthreads(64, 1, 1)]
void ClearExternal(uint3 tid : SV_DispatchThreadID)
{
    uint i = tid.x + _DispatchOffset;
    if (i < _InputCount) _ExternalReady[i] = 0;
    if (i < _OutputCount) _ExternalSuccess[i] = 0;
}
[numthreads(64, 1, 1)]
void ApplyExternal(uint3 tid : SV_DispatchThreadID)
{
    uint i = tid.x + _DispatchOffset;
    if (i >= _EventCount) return;
    GpuBeltEvent e = _Events[i];
    if (e.Kind == 0) _ExternalReady[e.Id] = 1;
    else if (e.Kind == 1) _ExternalSuccess[e.Id] = 1;
    else if (e.Kind == 2) _Speeds[e.Id] = e.Value;
}
[numthreads(64, 1, 1)]
void InsertExternal(uint3 tid : SV_DispatchThreadID)
{
    uint i = tid.x + _DispatchOffset;
    if (i >= _EventCount) return;
    GpuBeltEvent e = _Events[i];
    if (e.Kind != 3) return;
    GpuBeltPort input = _ExternalInputs[e.Id];
    Receive(input.Id, input.Direction, e.Value, e.Extra);
}

#endif
