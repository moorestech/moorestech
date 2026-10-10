using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.State;

namespace Game.Block.Blocks.BeltConveyor.Sync.Replica
{
    // 全量から組み立てた、ワールドを持たない搬送状態の複製。クライアントはこれを毎tick進め、機械との搬送は差分で与える
    // A replica of the transport state assembled from the full state without a world; the client ticks it and feeds machine handoffs from diffs
    public sealed class BeltTransportReplica
    {
        // 添字はsegment番号
        // Indices are segment numbers
        public readonly BeltSegmentShape[] Shapes;
        public readonly BeltConveyorSegment[] Segments;
        public readonly BeltSimulation Simulation;
        // segment番号と搬出方向で引く、機械への出力1本ごとの受け手
        // One receiver per output into a machine, looked up by segment number and output direction
        private readonly Dictionary<(int, BeltDirection), BeltReplicaMachineReceiver> _machineReceivers;

        public BeltTransportReplica(BeltSegmentShape[] shapes, BeltConveyorSegment[] segments, Dictionary<(int, BeltDirection), BeltReplicaMachineReceiver> machineReceivers)
        {
            Shapes = shapes;
            Segments = segments;
            Simulation = new BeltSimulation(segments);
            _machineReceivers = machineReceivers;
        }

        public BeltReplicaMachineReceiver MachineReceiverOf(int segmentIndex, BeltDirection outputDirection)
        {
            return _machineReceivers[(segmentIndex, outputDirection)];
        }

        public BeltTransportFullState CaptureFullState()
        {
            return BeltTransportFullStateCapture.Capture(Shapes, Segments);
        }
    }
}
