using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.BeltSegment;
using Game.Context;
namespace Game.Block.Blocks.BeltConveyor
{
    internal sealed class BeltWorldItems
    {
        internal readonly Dictionary<SegmentBeltComponent, BeltCellSaveState> Cells = new();
        private readonly Dictionary<Guid, IItemStack> _payloads = new();
        private bool _captured = true;
        internal IItemStack Payload(Guid id) => _payloads[id];
        internal void Remove(Guid id) => _payloads.Remove(id);
        internal void Invalidate() => _captured = false;
        internal void Register(SegmentBeltComponent belt)
        {
            var state = belt.LoadedState;
            Cells.Add(belt, state);
            RestorePayload(state.RunningItem); RestorePayload(state.BufferedItem);
            belt.ClearLoadedState();
        }
        private void RestorePayload(BeltSavedItem saved)
        {
            if (saved == null) return;
            _payloads.Add(saved.TransportGuid, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(saved.ItemMasterGuid), 1));
        }
        internal BeltItem Create(IItemStack stack, BeltDirection entry)
        {
            var id = GameRandom.NextGuid();
            _payloads.Add(id, stack.SubItem(stack.Count - 1));
            return new BeltItem { Guid = id, ItemId = stack.Id.AsPrimitive(), AcceptedInput = entry };
        }
        internal BeltItem Restore(BeltSavedItem saved)
            => new() { Guid = saved.TransportGuid, ItemId = Payload(saved.TransportGuid).Id.AsPrimitive(), AcceptedInput = saved.Entry };
        internal BeltSavedItem Save(in BeltItem item, int progress)
            => new(item.Guid, MasterHolder.ItemMaster.GetItemMaster(Payload(item.Guid).Id).ItemGuid, progress, item.AcceptedInput);
        internal void Unregister(SegmentBeltComponent belt)
        {
            var state = Cells[belt];
            if (state.RunningItem != null) Remove(state.RunningItem.TransportGuid);
            if (state.BufferedItem != null) Remove(state.BufferedItem.TransportGuid);
            Cells.Remove(belt);
        }
        internal void Capture(BeltSimulationGraph graph, IReadOnlyList<SegmentBeltComponent[]> owners)
        {
            if (_captured) return;
            var snapshot = graph.CaptureSnapshot();
            for (int id = 0; id < owners.Count; id++)
            {
                var path = owners[id]; var segment = snapshot.Segments[id];
                foreach (var belt in path)
                    if (Cells.TryGetValue(belt, out var prior)) Cells[belt] = new BeltCellSaveState(prior.PriorityIndex, null, null);
                foreach (var item in segment.Items)
                {
                    int index = path.Length - 1 - item.DistanceToExit / 256;
                    var belt = path[index];
                    if (!Cells.TryGetValue(belt, out var state)) continue;
                    if (state.RunningItem != null) throw new InvalidOperationException("Two running items occupy one belt cell.");
                    Cells[belt] = new BeltCellSaveState(state.PriorityIndex, Save(item.Item, 256 - item.DistanceToExit % 256), null);
                }
                var junction = path[path.Length - 1];
                if (segment.Kind != BeltSegmentKind.Normal && Cells.TryGetValue(junction, out var current))
                    Cells[junction] = new BeltCellSaveState(segment.PriorityIndex, current.RunningItem,
                        segment.BufferedItem.HasValue ? Save(segment.BufferedItem.Value, 256) : null);
            }
            _captured = true;
        }
        internal void PrunePayloads()
        {
            var retained = new HashSet<Guid>();
            foreach (var state in Cells.Values)
            {
                if (state.RunningItem != null) retained.Add(state.RunningItem.TransportGuid);
                if (state.BufferedItem != null) retained.Add(state.BufferedItem.TransportGuid);
            }
            var removed = new List<Guid>();
            foreach (var id in _payloads.Keys) if (!retained.Contains(id)) removed.Add(id);
            foreach (var id in removed) Remove(id);
        }
    }
}
