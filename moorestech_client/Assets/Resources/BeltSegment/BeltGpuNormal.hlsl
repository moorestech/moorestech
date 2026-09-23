#ifndef BELT_GPU_NORMAL_INCLUDED
#define BELT_GPU_NORMAL_INCLUDED
#include "BeltGpuPorts.hlsl"

[numthreads(64, 1, 1)]
void CaptureNormalOffers(uint3 tid : SV_DispatchThreadID)
{
    uint i = tid.x + _DispatchOffset;
    if (i >= _NormalLinkCount) return;
    GpuBeltNormalLink link = _NormalLinks[i];
    GpuBeltNormalState state = _NormalStates[i];
    state.AvailableSpace = Offer(link.Target, link.InputDirection);
    state.Length = 0;
    _NormalStates[i] = state;
}
[numthreads(64, 1, 1)]
void AdvanceNormal(uint3 tid : SV_DispatchThreadID)
{
    uint i = tid.x + _DispatchOffset;
    if (i >= _SegmentCount) return;
    GpuBeltTopology t = _Topology[i];
    if (t.Kind != 0) return;
    GpuBeltState s = _States[i];
    int speed = _Speeds[i];
    bool sent = false;
    if (s.Count > 0 && t.OutputCount > 0)
    {
        int head = Physical(t, s, 0);
        int length = speed - _Gaps[head];
        if (length > 0)
        {
            int kind = _Items[head];
            if (t.NormalLinkIndex >= 0)
            {
                GpuBeltNormalState staged = _NormalStates[t.NormalLinkIndex];
                if (staged.AvailableSpace >= length)
                {
                    staged.Length = length;
                    staged.ItemKind = kind;
                    _NormalStates[t.NormalLinkIndex] = staged;
                    sent = true;
                }
            }
            else
            {
                GpuBeltPort output = _OutputPorts[t.FirstOutput];
                sent = Send(output, length, kind);
            }
        }
    }
    Advance(t, s, speed, sent);
    _States[i] = s;
}
[numthreads(64, 1, 1)]
void CommitNormal(uint3 tid : SV_DispatchThreadID)
{
    uint i = tid.x + _DispatchOffset;
    if (i >= _NormalLinkCount) return;
    GpuBeltNormalState staged = _NormalStates[i];
    if (staged.Length == 0) return;
    GpuBeltNormalLink link = _NormalLinks[i];
    Receive(link.Target, link.InputDirection, staged.Length, staged.ItemKind);
}

#endif
