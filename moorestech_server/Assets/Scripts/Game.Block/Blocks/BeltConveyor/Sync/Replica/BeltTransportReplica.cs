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
                // 範囲外のsegment番号も載らない搬入と同じく食い違いとして報告する
                // An out-of-range segment number is reported as a divergence just like an insert that does not fit
                var inRange = 0 <= insert.SegmentIndex && insert.SegmentIndex < Segments.Length;
                if (inRange && Segments[insert.SegmentIndex].TryReceive(insert.InputDirection, BeltTransportAssembly.MachineEntryLength, insert.ToItem())) continue;
                Debug.LogError($"[BeltTransport] replica could not place a machine push into segment {insert.SegmentIndex} from {insert.InputDirection} (segments: {Segments.Length}); the replica has diverged from the server.");
                consistent = false;
            }

            // 受け口の無い搬出も食い違いとして報告し、予告はできる受け口だけに出す
            // An extract without a receiver is also a divergence; only existing receivers get the announcement
            foreach (var extract in diff.Extracts)
            {
                if (_machineReceivers.TryGetValue((extract.SegmentIndex, extract.OutputDirection), out var receiver))
                {
                    receiver.AcceptOnce();
                    continue;
                }
                Debug.LogError($"[BeltTransport] replica has no machine receiver at segment {extract.SegmentIndex} toward {extract.OutputDirection}; the replica has diverged from the server.");
                consistent = false;
            }

            Simulation.Tick();

            foreach (var extract in diff.Extracts)
            {
                if (!_machineReceivers.TryGetValue((extract.SegmentIndex, extract.OutputDirection), out var receiver)) continue;
                if (!receiver.ClearPending()) continue;
                Debug.LogError($"[BeltTransport] replica did not hand an item from segment {extract.SegmentIndex} toward {extract.OutputDirection} as the server did; the replica has diverged from the server.");
                consistent = false;
            }
            return consistent;
        }
    }
}
