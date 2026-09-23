using System;
using System.Collections.Generic;
using Game.BeltSegment;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
namespace Game.Block.Blocks.BeltConveyor
{
    internal sealed class BeltMachinePortTable
    {
        internal readonly List<BeltMachineSource> Sources = new();
        internal readonly List<BeltMachineReceiver> Receivers = new();
        private readonly Dictionary<(SegmentBeltComponent, BlockInstanceId, Guid?, Guid?), int> _inputIds = new();
        private readonly BeltWorldItems _items;
        internal BeltMachinePortTable(BeltWorldItems items) => _items = items;
        internal void AddSource(BeltTopologyEdge edge, SegmentBeltComponent target)
        {
            if (!edge.Source.TryGetComponent<IBlockOutputAvailability>(out var output))
                throw new InvalidOperationException($"Connected belt source {edge.Source.BlockInstanceId} lacks output availability.");
            _inputIds.Add((target, edge.Source.BlockInstanceId, edge.Connection.SelfConnector?.ConnectorGuid,
                edge.Connection.TargetConnector?.ConnectorGuid), Sources.Count);
            Sources.Add(new BeltMachineSource(output));
        }
        internal void AddReceiver(BeltTopologyEdge edge) => Receivers.Add(new BeltMachineReceiver(edge.Target, edge.Context, _items));
        internal bool Resolve(SegmentBeltComponent target, InsertItemContext context, out int inputId)
            => _inputIds.TryGetValue((target, context.SourceBlockInstanceId, context.SourceConnector?.ConnectorGuid,
                context.TargetConnector?.ConnectorGuid), out inputId);
        internal int[] Freeze()
        {
            var ready = new List<int>();
            for (int i = 0; i < Sources.Count; i++) { Sources[i].Freeze(); if (Sources[i].Ready) ready.Add(i); }
            foreach (var receiver in Receivers) receiver.Reset();
            return ready.ToArray();
        }
        internal int[] Consumed()
        {
            var result = new List<int>();
            for (int i = 0; i < Receivers.Count; i++) if (Receivers[i].Consumed) result.Add(i);
            return result.ToArray();
        }
    }
}
