using System.Runtime.InteropServices;

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

    // GPU ABIは32-bit整数だけで構成する。
    // The GPU ABI contains only 32-bit integers.
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuBeltTopology
    {
        public int Offset, Capacity, Kind, FirstInput;
        public int InputCount, FirstOutput, OutputCount, NormalLinkIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuBeltPort
    {
        public int Kind, Id, Direction, Unused;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuBeltState
    {
        public int Head, Count, TotalGap, PriorityIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuBeltBufferState
    {
        public int HasItem, ItemKind, PriorityIndex, Unused;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuBeltNormalLink
    {
        public int Source, Target, InputDirection, Unused;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuBeltNormalState
    {
        public int AvailableSpace, Length, ItemKind, Unused;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuBeltEvent
    {
        public int Kind, Id, Value, Extra;
    }
}
