using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    internal static class BeltWorldGraphBuilder
    {
        internal static BeltWorldGraph Capture(IWorldBlockDatastore world)
        {
            var graph = new BeltWorldGraph();
            var blocks = new List<WorldBlockData>(world.BlockMasterDictionary.Values);
            blocks.Sort((left, right) => left.Block.BlockInstanceId.CompareTo(right.Block.BlockInstanceId));
            foreach (var data in blocks)
            {
                var block = data.Block;
                if (!block.ComponentManager.TryGetComponent<VanillaBeltConveyorComponent>(out var belt)) continue;
                // 上下姿勢の在庫はcomponentの保存領域に保持する。
                // Retain unsupported vertical inventories in the component's pending storage.
                if (!BeltTransportDirections.IsHorizontal(belt.Position))
                {
                    Debug.Log($"Belt transport excludes vertical orientation: {belt.Position.BlockDirection} at {belt.Position.OriginalPos}.");
                    continue;
                }
                var position = block.BlockPositionInfo.OriginalPos;
                graph.Components.Add(belt.CellId, belt);
                graph.Cells.Add(new BeltNetworkCell(belt.CellId, position.x, position.y, position.z, belt.Speed,
                    belt.SpeedProfile, BeltTransportDirections.Forward(block.BlockPositionInfo), belt.SlopeType switch
                    {
                        BeltConveyorSlopeType.Up => new BeltCellSurfaceProfile(0.1f, 1.1f),
                        BeltConveyorSlopeType.Down => new BeltCellSurfaceProfile(1.1f, 0.1f),
                        _ => new BeltCellSurfaceProfile(0, 0)
                    }));
            }

            // 接続resolverが確定したportだけを共有グラフへ写す。
            // Copy only ports already resolved by the existing connection resolver.
            foreach (var data in blocks)
            {
                var source = data.Block;
                if (!source.ComponentManager.TryGetComponent<IBlockConnectorComponent<IBlockInventory>>(out var connector)) continue;
                int sourceId = source.BlockInstanceId.AsPrimitive();
                bool sourceIsBelt = graph.Components.ContainsKey(sourceId);
                foreach (var pair in connector.ConnectedTargets)
                {
                    var target = pair.Value.TargetBlock;
                    int targetId = target.BlockInstanceId.AsPrimitive();
                    bool targetIsBelt = graph.Components.ContainsKey(targetId);
                    if (!sourceIsBelt && !targetIsBelt) continue;
                    var sourceCell = source.BlockPositionInfo.ConvertBlockLocalToWorldCell(pair.Value.SelfConnector.Offset);
                    var targetCell = target.BlockPositionInfo.ConvertBlockLocalToWorldCell(pair.Value.TargetConnector.Offset);
                    var direction = BeltTransportDirections.FromVector(targetCell - sourceCell);
                    graph.Edges.Add(new BeltNetworkConnection(sourceId, targetId, sourceIsBelt, targetIsBelt, direction, sourceCell.y - targetCell.y));
                    if (sourceIsBelt && !targetIsBelt)
                        graph.Outputs.Add((sourceId, direction), new BeltMachineConnection(source.BlockInstanceId, pair.Key, pair.Value));
                }
            }

            // 機械から合流への直結は搬送経路として登録しない。
            // Do not register direct machine inputs into a merge.
            var inputCounts = new Dictionary<int, int>();
            foreach (var edge in graph.Edges)
                if (edge.TargetIsBelt) inputCounts[edge.TargetId] = inputCounts.TryGetValue(edge.TargetId, out var count) ? count + 1 : 1;
            for (int i = graph.Edges.Count - 1; 0 <= i; i--)
            {
                var edge = graph.Edges[i];
                if (edge.SourceIsBelt || !edge.TargetIsBelt || inputCounts[edge.TargetId] <= 1) continue;
                Debug.LogWarning($"Machine {edge.SourceId} cannot connect directly to merging belt {edge.TargetId}; insert a normal belt.");
                graph.Edges.RemoveAt(i);
            }
            return graph;
        }
    }
}
