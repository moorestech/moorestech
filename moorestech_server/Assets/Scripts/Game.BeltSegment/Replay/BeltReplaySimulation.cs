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

        public uint ComputeStateHash() => graph.ComputeStateHash();

        public BeltReplaySnapshot CaptureSnapshot() => graph.CaptureSnapshot();

        public bool TryApplyTick(BeltReplayTick tick, uint previousHash, bool parallel, out string denyReason)
        {
            denyReason = "Previous state hash mismatch.";
            if (previousHash != graph.ComputeStateHash()) return false;
            // 現存itemと同frameの挿入を変異前に比較し、snapshot複製を避ける。
            // Compare live identities and same-frame insertions before mutation without cloning snapshots.
            for (int i = 0; i < tick.Insertions.Length; i++)
            {
                var identity = tick.Insertions[i].Item.Guid;
                denyReason = "Duplicate live item identity.";
                if (identity == Guid.Empty || graph.ContainsIdentity(identity)) return false;
                for (int j = 0; j < i; j++) if (tick.Insertions[j].Item.Guid == identity) return false;
            }
            return Apply(tick, parallel, out denyReason);
        }
        public void ApplyTick(BeltReplayTick tick, bool parallel)
        {
            if (!Apply(tick, parallel, out var reason)) throw new InvalidOperationException(reason);
        }
        private bool Apply(BeltReplayTick tick, bool parallel, out string denyReason)
        {
            denyReason = null;
            // 外部可否と速度を固定し、既存Coreを一度だけ進める。
            // Fix external outcomes and speeds before advancing the existing Core once.
            ports.Prepare(tick);
            foreach (var change in tick.SpeedChanges) graph.SetSpeed(change.SegmentId, change.Speed);
            graph.Tick(parallel);
            if (!ports.TryVerifyOutputs(tick, out denyReason)) return false;
            // 境界搬入は順序を保ち、このtickでは前進させない。
            // Preserve boundary insertion order without advancing those items in this tick.
            foreach (var insertion in tick.Insertions)
            {
                if (!graph.TryInsert(insertion.InputId, insertion.Length, insertion.Item))
                    { denyReason = $"Recorded input {insertion.InputId} was rejected after advancing the replay tick."; return false; }
            }
            return true;
        }
    }
}
