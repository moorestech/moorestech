using System;
using System.Collections.Generic;
using Game.BeltSegment;
namespace Game.Block.Blocks.BeltConveyor
{
    internal static class BeltWorldRebuild
    {
        internal static BeltSimulationGraph Build(BeltTopologyBuilder topology, BeltWorldItems items)
        {
            var paths = topology.Paths;
            var states = new BeltReplaySegmentState[paths.Owners.Count];
            for (int id = 0; id < states.Length; id++)
            {
                var path = paths.Owners[id];
                var running = new List<BeltItemState>();
                int previous = -1;
                // 出口から順に再配置し、間隔調整は元の所有セル内だけに限定する。
                // Reproject from output to input; spacing adjustments must stay inside the owned cell.
                for (int i = path.Length - 1; i >= 0; i--)
                {
                    var saved = items.Cells[path[i]].RunningItem;
                    if (saved == null) continue;
                    int lower = (path.Length - 1 - i) * 256;
                    int desired = lower + 256 - saved.Progress;
                    int placed = previous < 0 ? desired : Math.Max(desired, previous + 256);
                    if (placed < lower || placed >= lower + 256)
                        throw new InvalidOperationException($"Belt reprojection escapes cell {path[i].Position.OriginalPos}.");
                    running.Add(new BeltItemState(items.Restore(saved), placed));
                    previous = placed;
                }
                var tail = path[path.Length - 1];
                var state = items.Cells[tail];
                var kind = paths.Kinds[id];
                BeltItem? buffer = kind != BeltSegmentKind.Normal && state.BufferedItem != null ? items.Restore(state.BufferedItem) : null;
                if (kind == BeltSegmentKind.Normal && state.BufferedItem != null)
                    items.Cells[tail] = new BeltCellSaveState(state.PriorityIndex, state.RunningItem, null);
                // 中間セルのbufferはjunction消失として破棄し、RR自体は保持する。
                // Drop buffers whose junction disappeared while retaining their cell's RR.
                for (int i = 0; i < path.Length - 1; i++)
                {
                    var cell = items.Cells[path[i]];
                    if (cell.BufferedItem != null) items.Cells[path[i]] = new BeltCellSaveState(cell.PriorityIndex, cell.RunningItem, null);
                }
                states[id] = kind switch
                {
                    BeltSegmentKind.Merge => BeltReplaySegmentState.Merge(16, state.PriorityIndex, running.ToArray(), buffer),
                    BeltSegmentKind.Branch => BeltReplaySegmentState.Branch(path.Length, 16, state.PriorityIndex, running.ToArray(), buffer),
                    _ => BeltReplaySegmentState.Normal(path.Length, 16, running.ToArray())
                };
            }
            items.PrunePayloads();
            return new BeltSimulationGraph(new BeltReplaySnapshot(states, topology.Links, topology.Inputs, topology.Outputs),
                topology.Ports.Sources, topology.Ports.Receivers);
        }
    }
}
