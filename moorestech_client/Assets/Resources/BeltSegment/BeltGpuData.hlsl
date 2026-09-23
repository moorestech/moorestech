#ifndef BELT_GPU_DATA_INCLUDED
#define BELT_GPU_DATA_INCLUDED

struct GpuBeltTopology {
    int Offset, Capacity, Kind, FirstInput;
    int InputCount, FirstOutput, OutputCount, NormalLinkIndex;
};
struct GpuBeltPort { int Kind, Id, Direction, Unused; };
struct GpuBeltState { int Head, Count, TotalGap, PriorityIndex; };
struct GpuBeltBufferState { int HasItem, ItemKind, PriorityIndex, Unused; };
struct GpuBeltNormalLink { int Source, Target, InputDirection, Unused; };
struct GpuBeltNormalState { int AvailableSpace, Length, ItemKind, Unused; };
struct GpuBeltEvent { int Kind, Id, Value, Extra; };

StructuredBuffer<GpuBeltTopology> _Topology;
StructuredBuffer<GpuBeltPort> _InputPorts, _OutputPorts, _ExternalInputs;
StructuredBuffer<GpuBeltNormalLink> _NormalLinks;
RWStructuredBuffer<GpuBeltState> _States;
RWStructuredBuffer<GpuBeltBufferState> _Buffers;
RWStructuredBuffer<int> _Gaps, _Blocks, _Items, _Speeds, _Reservations;
RWStructuredBuffer<int> _ExternalReady, _ExternalSuccess;
RWStructuredBuffer<GpuBeltNormalState> _NormalStates;
StructuredBuffer<GpuBeltEvent> _Events;

int _SegmentCount, _InputCount, _OutputCount, _NormalLinkCount, _EventCount;
int _DispatchOffset;

#endif
