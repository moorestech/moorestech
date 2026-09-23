#ifndef BELT_GPU_QUEUE_INCLUDED
#define BELT_GPU_QUEUE_INCLUDED
#include "BeltGpuData.hlsl"

int Physical(GpuBeltTopology t, GpuBeltState s, int logical)
{
    int p = s.Head + logical;
    if (p >= t.Capacity) p -= t.Capacity;
    return t.Offset + p;
}
void SetGap(int p, int value, inout GpuBeltState s)
{
    s.TotalGap += value - _Gaps[p];
    _Gaps[p] = value;
}
void Enqueue(GpuBeltTopology t, inout GpuBeltState s, int gap, int kind)
{
    int p = Physical(t, s, s.Count);
    _Items[p] = kind;
    SetGap(p, gap, s);
    if (s.Count == 0 || gap != 0) _Blocks[p] = 1;
    else
    {
        int tail = p - 1;
        if (tail < t.Offset) tail += t.Capacity;
        int size = _Blocks[tail] + 1;
        int start = p - size + 1;
        if (start < t.Offset) start += t.Capacity;
        _Blocks[start] = size;
        _Blocks[p] = size;
    }
    s.Count++;
}
void Dequeue(GpuBeltTopology t, inout GpuBeltState s)
{
    int p = Physical(t, s, 0);
    int gap = _Gaps[p];
    int size = _Blocks[p] - 1;
    _Items[p] = 0;
    SetGap(p, 0, s);
    s.Head++;
    if (s.Head == t.Capacity) s.Head = 0;
    s.Count--;
    if (s.Count > 0)
    {
        int head = Physical(t, s, 0);
        SetGap(head, _Gaps[head] + gap + ItemWidth, s);
    }
    if (size > 0)
    {
        int head = Physical(t, s, 0);
        int tail = Physical(t, s, size - 1);
        _Blocks[head] = size;
        _Blocks[tail] = size;
    }
}
void Advance(GpuBeltTopology t, inout GpuBeltState s, int speed, bool sent)
{
    if (s.Count == 0) return;
    int head = Physical(t, s, 0);
    int gap = _Gaps[head];
    if (sent)
    {
        Dequeue(t, s);
        if (s.Count > 0)
        {
            head = Physical(t, s, 0);
            SetGap(head, _Gaps[head] - speed, s);
        }
        return;
    }
    if (speed <= gap) { SetGap(head, gap - speed, s); return; }
    int remaining = speed - gap;
    // 拒否時は出口に止め、密着ブロックの境界を詰める。
    // Stop at the exit on rejection and close touching-block gaps.
    if (gap != 0) SetGap(head, 0, s);
    while (_Blocks[head] < s.Count && remaining > 0)
    {
        int p = Physical(t, s, _Blocks[head]);
        int nextGap = _Gaps[p];
        if (nextGap <= remaining)
        {
            SetGap(p, 0, s);
            remaining -= nextGap;
            int size = _Blocks[p];
            int tail = Physical(t, s, _Blocks[head] + size - 1);
            _Blocks[head] += size;
            _Blocks[tail] = _Blocks[head];
        }
        else { SetGap(p, nextGap - remaining, s); break; }
    }
}

#endif
