using System.Collections.Generic;
using Core.BeltTransport;
using Game.Train.Unit;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    internal sealed class BeltTransportJournal
    {
        private readonly List<BeltBoundaryChange> before = new List<BeltBoundaryChange>();
        private readonly List<BeltBoundaryChange> after = new List<BeltBoundaryChange>();
        private readonly List<BeltOutputResult> outputs = new List<BeltOutputResult>();
        private bool completedSimulation;
        private readonly TrainUpdateService tickSequence;
        private readonly List<uint> beforeSequenceIds = new();
        private readonly List<uint> afterSequenceIds = new();
        private uint simulationSequenceId;
        internal BeltTransportJournal(TrainUpdateService tickSequence) { this.tickSequence = tickSequence; }
        internal void BeginTick() => completedSimulation = false;
        internal void BeginSimulation() => simulationSequenceId = tickSequence.NextTickSequenceId();
        internal void CompleteSimulation() => completedSimulation = true;
        internal void Record(BeltBoundaryChange change)
        {
            // 発生時点でtrain・railと共通のtick内順序を確定する。
            // Allocate the shared train/rail sequence at the time the change occurs.
            (completedSimulation ? after : before).Add(change);
            (completedSimulation ? afterSequenceIds : beforeSequenceIds).Add(tickSequence.NextTickSequenceId());
        }
        internal void RecordOutput(BeltOutputResult result)
        {
            for (int i = 0; i < outputs.Count; i++)
            {
                if (outputs[i].SourceCellId != result.SourceCellId || outputs[i].Direction != result.Direction ||
                    outputs[i].TargetId != result.TargetId || outputs[i].Stage != result.Stage) continue;
                outputs[i] = result;
                return;
            }
            outputs.Add(result);
        }
        internal BeltTickDifference Complete(ulong tick)
        {
            var order = new BeltTickOrder(tickSequence.GetCurrentTick(), beforeSequenceIds.ToArray(), simulationSequenceId,
                afterSequenceIds.ToArray(), tickSequence.NextTickSequenceId());
            var result = new BeltTickDifference(tick, before.ToArray(), outputs.ToArray(), after.ToArray(), order);
            before.Clear(); after.Clear(); outputs.Clear(); completedSimulation = false;
            beforeSequenceIds.Clear(); afterSequenceIds.Clear();
            return result;
        }
    }
}
