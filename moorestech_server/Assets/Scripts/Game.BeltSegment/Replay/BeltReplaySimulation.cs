using System;

namespace Game.BeltSegment
{
    /// <summary>完全な1tickの確定差分を適用する。失敗後の途中状態は全状態から再構築する。</summary>
    public sealed class BeltReplaySimulation
    {
        private readonly BeltSimulationGraph graph;
        private readonly BeltReplayPorts ports;

        public BeltReplaySimulation(BeltReplaySnapshot snapshot)
        {
            ports = new BeltReplayPorts(snapshot.Inputs.Length, snapshot.Outputs.Length);
            graph = new BeltSimulationGraph(snapshot, ports.Sources, ports.Receivers);
        }

        public BeltReplaySnapshot CaptureSnapshot() => graph.CaptureSnapshot();

        public void ApplyTick(BeltReplayTick tick, bool parallel)
        {
            // 外部可否と速度を固定し、既存Coreを一度だけ進める。
            // Fix external outcomes and speeds before advancing the existing Core once.
            ports.Prepare(tick);
            foreach (var change in tick.SpeedChanges) graph.SetSpeed(change.SegmentId, change.Speed);
            graph.Tick(parallel);
            ports.VerifyOutputs(tick);
            // 境界搬入は順序を保ち、このtickでは前進させない。
            // Preserve boundary insertion order without advancing those items in this tick.
            foreach (var insertion in tick.Insertions)
            {
                if (!graph.TryInsert(insertion.InputId, insertion.Length, insertion.Item))
                    throw new InvalidOperationException($"Recorded input {insertion.InputId} was rejected.");
            }
        }
    }
}
