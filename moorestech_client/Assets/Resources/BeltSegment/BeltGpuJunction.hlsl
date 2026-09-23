#ifndef BELT_GPU_JUNCTION_INCLUDED
#define BELT_GPU_JUNCTION_INCLUDED
#include "BeltGpuPorts.hlsl"

// 速度0でも出口にある走行品を空bufferへ回収する。
// Collect an item already at the exit into an empty buffer even at speed zero.
[numthreads(BELT_GPU_THREAD_COUNT_X, 1, 1)]
void Collect(uint3 tid : SV_DispatchThreadID)
{
    uint i = tid.x + _DispatchOffset;
    if (i >= _SegmentCount) return;
    GpuBeltTopology t = _Topology[i];
    if (t.Kind == SegmentNormal) return;
    GpuBeltState s = _States[i];
    GpuBeltBufferState b = _Buffers[i];
    Advance(t, s, _Speeds[i], false);
    if (b.HasItem == 0 && s.Count > 0 && _Gaps[Physical(t, s, 0)] == 0)
    {
        b.ItemKind = _Items[Physical(t, s, 0)].x;
        b.HasItem = 1;
        Dequeue(t, s);
        _Buffers[i] = b;
    }
    _States[i] = s;
}
// 回収後の空Mergeで、登録順と開始indexから唯一の入口を予約する。
// Reserve one input by registration order and start index after collection.
[numthreads(BELT_GPU_THREAD_COUNT_X, 1, 1)]
void Reserve(uint3 tid : SV_DispatchThreadID)
{
    uint i = tid.x + _DispatchOffset;
    if (i >= _SegmentCount) return;
    GpuBeltTopology t = _Topology[i];
    if (t.Kind != SegmentMerge) return;
    _Reservations[i] = -1;
    GpuBeltState s = _States[i];
    if (s.Count != 0) return;
    for (int offset = 0; offset < t.InputCount; offset++)
    {
        int index = (s.PriorityIndex + offset) % t.InputCount;
        GpuBeltPort input = _InputPorts[t.FirstInput + index];
        if (!SourceReady(input)) continue;
        _Reservations[i] = input.Direction;
        return;
    }
}
// 成功時は選ばれた出力でなく旧開始indexを一つ進める。
// On success rotate from the old starting index, not the chosen output.
[numthreads(BELT_GPU_THREAD_COUNT_X, 1, 1)]
void Transfer(uint3 tid : SV_DispatchThreadID)
{
    uint i = tid.x + _DispatchOffset;
    if (i >= _SegmentCount) return;
    GpuBeltTopology t = _Topology[i];
    if (t.Kind == SegmentNormal) return;
    GpuBeltBufferState b = _Buffers[i];
    int speed = _Speeds[i];
    if (b.HasItem == 0 || speed == 0) return;
    for (int offset = 0; offset < t.OutputCount; offset++)
    {
        int index = (b.PriorityIndex + offset) % t.OutputCount;
        GpuBeltPort output = _OutputPorts[t.FirstOutput + index];
        int length = min(speed, OutputOffer(output));
        if (length <= 0) continue;
        if (!Send(output, length, b.ItemKind)) continue;
        b.HasItem = 0;
        b.ItemKind = 0;
        b.PriorityIndex = (b.PriorityIndex + 1) % t.OutputCount;
        _Buffers[i] = b;
        return;
    }
}

#endif
