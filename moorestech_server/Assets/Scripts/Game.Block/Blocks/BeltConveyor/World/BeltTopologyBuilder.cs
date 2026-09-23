using System;
using System.Collections.Generic;
using Game.BeltSegment;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;
namespace Game.Block.Blocks.BeltConveyor
{
    internal sealed class BeltTopologyBuilder
    {
        internal readonly BeltTopologyRoutes Paths;
        internal readonly BeltReplayLink[] Links;
        internal readonly BeltReplayInput[] Inputs;
        internal readonly BeltReplayOutput[] Outputs;
        internal readonly BeltMachinePortTable Ports;
        internal BeltTopologyBuilder(HashSet<SegmentBeltComponent> belts, IWorldBlockDatastore world, BeltWorldItems items)
        {
            var nodes = new List<BeltTopologyNode>();
            var lookup = new Dictionary<SegmentBeltComponent, BeltTopologyNode>();
            foreach (var belt in belts) { var node = new BeltTopologyNode(belt); nodes.Add(node); lookup.Add(belt, node); }
            nodes.Sort((a, b) => BeltTopologyGeometry.Compare(a.Belt.Position.OriginalPos, b.Belt.Position.OriginalPos));
            var edges = new List<BeltTopologyEdge>();
            // コネクタが確定した実接続を唯一のグラフ入力にする。
            // Actual settled connector edges are the sole graph input.
            foreach (var data in world.BlockMasterDictionary.Values)
            {
                var block = data.Block;
                if (!block.TryGetComponent<IBlockConnectorComponent<IBlockInventory>>(out var connector)) continue;
                block.TryGetComponent<SegmentBeltComponent>(out var source);
                foreach (var pair in connector.ConnectedTargets)
                {
                    var target = pair.Key as SegmentBeltComponent;
                    if (source == null && target == null) continue;
                    if (source != null && !belts.Contains(source) || target != null && !belts.Contains(target)) continue;
                    var edge = new BeltTopologyEdge(block, pair.Key, pair.Value);
                    edges.Add(edge);
                    if (source != null) lookup[source].Outgoing.Add(edge);
                    if (target != null) lookup[target].Incoming.Add(edge);
                }
            }
            edges.Sort(BeltTopologyEdge.Compare);
            foreach (var node in nodes)
            {
                node.Incoming.Sort((a, b) =>
                {
                    int compare = ((int)a.Direction ^ 1).CompareTo((int)b.Direction ^ 1);
                    return compare != 0 ? compare : BeltTopologyEdge.Compare(a, b);
                });
                node.Outgoing.Sort(BeltTopologyEdge.Compare);
                if (node.Incoming.Count > 3 || node.Outgoing.Count > 3 || node.IsMerge && node.Outgoing.Count > 1)
                    throw new InvalidOperationException($"Unsupported belt junction at {node.Belt.Position.OriginalPos}.");
            }
            Paths = new BeltTopologyRoutes(nodes, lookup);
            var links = new List<BeltReplayLink>();
            var inputs = new List<BeltReplayInput>();
            var outputs = new List<BeltReplayOutput>();
            Ports = new BeltMachinePortTable(items);
            foreach (var edge in edges)
            {
                edge.Source.TryGetComponent<SegmentBeltComponent>(out var source);
                var target = edge.Target as SegmentBeltComponent;
                if (source != null && target != null)
                {
                    int from = Paths.SegmentIds[source], to = Paths.SegmentIds[target];
                    var owner = Paths.Owners[from];
                    if (from != to || owner[owner.Length - 1] == source && owner[0] == target)
                        links.Add(new BeltReplayLink(from, to, edge.Direction));
                }
                else if (source == null)
                {
                    inputs.Add(new BeltReplayInput(Paths.SegmentIds[target], (BeltDirection)((int)edge.Direction ^ 1)));
                    Ports.AddSource(edge, target);
                }
                else
                {
                    outputs.Add(new BeltReplayOutput(Paths.SegmentIds[source], edge.Direction));
                    Ports.AddReceiver(edge);
                }
            }
            Links = links.ToArray(); Inputs = inputs.ToArray(); Outputs = outputs.ToArray();
        }
    }
}
