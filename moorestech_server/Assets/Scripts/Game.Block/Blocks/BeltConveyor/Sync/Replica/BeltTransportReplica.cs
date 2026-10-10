using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Blocks.BeltConveyor.Transport;
using UnityEngine;

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

        // サーバーと同じtickを再生する。搬入を同じ順で載せ、搬出成功を予告してから1tick進める
        // Replays the same tick as the server: apply the inserts in order, announce the successful extracts, then advance one tick
        // 載らない搬入や消費されない予告は、複製がサーバーと食い違った印。falseを返し、呼び出し側が停止を決める
        // An insert that does not fit or an announcement left unconsumed marks the replica as diverged; returns false and the caller decides to stop
        public bool Tick(BeltTickDiff diff)
        {
            var consistent = true;
            foreach (var insert in diff.Inserts)
            {
                if (Segments[insert.SegmentIndex].TryReceive(insert.InputDirection, BeltTransportAssembly.MachineEntryLength, insert.ToItem())) continue;
                Debug.LogError($"[BeltTransport] replica could not place a machine push into segment {insert.SegmentIndex} from {insert.InputDirection}; the replica has diverged from the server.");
                consistent = false;
            }
            foreach (var extract in diff.Extracts) MachineReceiverOf(extract.SegmentIndex, extract.OutputDirection).AcceptOnce();

            Simulation.Tick();

            foreach (var extract in diff.Extracts)
            {
                if (!MachineReceiverOf(extract.SegmentIndex, extract.OutputDirection).ClearPending()) continue;
                Debug.LogError($"[BeltTransport] replica did not hand an item from segment {extract.SegmentIndex} toward {extract.OutputDirection} as the server did; the replica has diverged from the server.");
                consistent = false;
            }
            return consistent;
        }
    }
}
