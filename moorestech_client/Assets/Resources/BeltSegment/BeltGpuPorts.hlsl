#ifndef BELT_GPU_PORTS_INCLUDED
#define BELT_GPU_PORTS_INCLUDED
#include "BeltGpuQueue.hlsl"

int Offer(int target, int direction)
{
    GpuBeltTopology t = _Topology[target];
    if (t.Kind == 1 && _Reservations[target] != direction) return 0;
    GpuBeltState s = _States[target];
    return t.Capacity * 256 - s.TotalGap - s.Count * 256;
}
bool Receive(int target, int direction, int length, int itemKind)
{
    int offer = Offer(target, direction);
    if (offer < length) return false;
    GpuBeltTopology t = _Topology[target];
    GpuBeltState s = _States[target];
    Enqueue(t, s, offer - length, itemKind);
    if (t.Kind == 1) s.PriorityIndex = (s.PriorityIndex + 1) % t.InputCount;
    _States[target] = s;
    return true;
}
bool SourceReady(GpuBeltPort input)
{
    if (input.Kind == 2) return _ExternalReady[input.Id] != 0;
    GpuBeltTopology source = _Topology[input.Id];
    if (input.Kind == 0)
    {
        GpuBeltState s = _States[input.Id];
        return s.Count > 0 && _Speeds[input.Id] > _Gaps[Physical(source, s, 0)];
    }
    GpuBeltBufferState b = _Buffers[input.Id];
    if (b.HasItem == 0) return false;
    GpuBeltPort output = _OutputPorts[source.FirstOutput + b.PriorityIndex];
    return (output.Direction ^ 1) == input.Direction;
}
int OutputOffer(GpuBeltPort output)
{
    if (output.Kind == 2) return _ExternalSuccess[output.Id] != 0 ? 256 : 0;
    return Offer(output.Id, output.Direction ^ 1);
}
bool Send(GpuBeltPort output, int length, int kind)
{
    if (output.Kind == 2) return _ExternalSuccess[output.Id] != 0;
    return Receive(output.Id, output.Direction ^ 1, length, kind);
}

#endif
