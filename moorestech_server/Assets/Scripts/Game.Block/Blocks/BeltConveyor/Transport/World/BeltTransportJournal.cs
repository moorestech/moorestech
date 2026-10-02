using System.Collections.Generic;
using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    internal sealed class BeltTransportJournal
    {
        private readonly List<BeltBoundaryChange> before = new List<BeltBoundaryChange>();
        private readonly List<BeltBoundaryChange> after = new List<BeltBoundaryChange>();
        private readonly List<BeltOutputResult> outputs = new List<BeltOutputResult>();
        private bool completedSimulation;
        internal void BeginTick() => completedSimulation = false;
        internal void CompleteSimulation() => completedSimulation = true;
        internal void Record(BeltBoundaryChange change) => (completedSimulation ? after : before).Add(change);
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
            var result = new BeltTickDifference(tick, before.ToArray(), outputs.ToArray(), after.ToArray());
            before.Clear(); after.Clear(); outputs.Clear(); completedSimulation = false;
            return result;
        }
    }
}
