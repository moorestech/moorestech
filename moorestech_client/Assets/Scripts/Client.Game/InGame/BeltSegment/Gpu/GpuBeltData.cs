using System.Runtime.InteropServices;
using Game.BeltSegment;

namespace Client.Game.InGame.BeltSegment.Gpu
{
    internal static class GpuBeltData
    {
        internal const int PortSegment = 0;
        internal const int PortBuffer = 1;
        internal const int PortExternal = 2;

        internal const int ReadyInput = 0;
        internal const int SuccessfulOutput = 1;
        internal const int Speed = 2;
        internal const int Insertion = 3;
    }

    // GPU ABIはint32のみ。
    // The GPU ABI uses only 32-bit integers.
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuBeltTopology
    {
        internal int Offset, Capacity, Kind, FirstInput;
        internal int InputCount, FirstOutput, OutputCount, NormalLinkIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuBeltPort
    {
        internal int Kind, Id, Direction, Unused;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuBeltState
    {
        internal int Head, Count, TotalGap, PriorityIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuBeltBufferState
    {
        internal int HasItem, ItemKind, PriorityIndex, Unused;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuBeltNormalLink
    {
        internal int Target, InputDirection, Unused0, Unused1;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuBeltNormalState
    {
        internal int AvailableSpace, Length, ItemKind, Unused;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct GpuBeltEvent
    {
        internal readonly int Kind, Id, Value, Extra;

        private GpuBeltEvent(int kind, int id, int value, int extra)
        {
            Kind = kind;
            Id = id;
            Value = value;
            Extra = extra;
        }

        internal static GpuBeltEvent ReadyInput(int inputId)
            => new GpuBeltEvent(GpuBeltData.ReadyInput, inputId, 1, 0);
        internal static GpuBeltEvent SuccessfulOutput(int outputId)
            => new GpuBeltEvent(GpuBeltData.SuccessfulOutput, outputId, 1, 0);
        internal static GpuBeltEvent SpeedChange(in BeltReplaySpeedChange change)
            => new GpuBeltEvent(GpuBeltData.Speed, change.SegmentId, change.Speed, 0);
        internal static GpuBeltEvent Insert(in BeltReplayInsertion insertion)
            => new GpuBeltEvent(GpuBeltData.Insertion, insertion.InputId, insertion.Length, insertion.Item.ItemId);
    }
}
