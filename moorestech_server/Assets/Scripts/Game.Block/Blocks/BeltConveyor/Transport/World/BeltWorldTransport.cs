using System;
using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Update;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    public sealed class BeltWorldTransport : IBeltExternalReceiverFactory, IBeltItemDropObserver
    {
        private readonly IWorldBlockDatastore world;
        private readonly Dictionary<Guid, IItemStack> stacks = new Dictionary<Guid, IItemStack>();
        private readonly SortedDictionary<int, int> pendingSpeeds = new SortedDictionary<int, int>();
        private readonly Dictionary<int, int> counts = new Dictionary<int, int>();
        private readonly BeltTransportJournal journal = new BeltTransportJournal();
        private readonly Subject<BeltTickDifference> differences = new Subject<BeltTickDifference>();
        private BeltWorldGraph graph;
        private BeltCommittedSnapshot committed;
        private bool initialized, dirty;
        internal BeltTransportNetwork Network { get; }
        public ulong CompletedTick { get; private set; }
        public IObservable<BeltTickDifference> OnTickCompleted => differences;

        public BeltWorldTransport(IWorldBlockDatastore world, IWorldBlockUpdateEvent changes)
        {
            this.world = world;
            Network = new BeltTransportNetwork(this, this);
            changes.OnBlockPlaceEvent.Subscribe(_ => dirty = true);
            changes.OnBlockRemoveEvent.Subscribe(_ => dirty = true);
        }

        public void Initialize()
        {
            if (initialized) return;
            graph = BeltWorldGraphBuilder.Capture(world);
            var items = GatherPending();
            var priorities = new List<BeltCellPriority>();
            foreach (var component in graph.Components.Values)
                if (component.LoadedPriority >= 0) priorities.Add(new BeltCellPriority(component.CellId, component.LoadedPriority));
            Network.Restore(new BeltNetworkSnapshot(graph.Cells.ToArray(), graph.Edges.ToArray(), items, priorities.ToArray()));
            foreach (var component in graph.Components.Values) component.Bind(this);
            initialized = true; dirty = false; CompletedTick = GameUpdater.CurrentTick;
            committed = new BeltCommittedSnapshot(CompletedTick, Network.Capture());
            PublishOccupancy();
        }

        public void BeginTick()
        {
            if (!initialized)
            {
                Initialize();
                CompletedTick = GameUpdater.CurrentTick - 1;
                committed = new BeltCommittedSnapshot(CompletedTick, Network.Capture());
            }
            journal.BeginTick();
        }
        public void Advance()
        {
            Network.Tick();
            journal.CompleteSimulation();
            PublishOccupancy();
        }

        public void CompleteTick()
        {
            // 世界変更の確定後、セーブと初回snapshotより前に再構築する。
            // Rebuild after world mutations and before saves or initial snapshots.
            if (dirty)
            {
                var previous = Network.Capture();
                graph = BeltWorldGraphBuilder.Capture(world);
                var addedItems = GatherPending();
                var change = BeltTopologyChange.Between(previous, graph.Cells.ToArray(), graph.Edges.ToArray(), addedItems);
                if (change.ChangedCells.Length + change.RemovedCells.Length + change.AddedConnections.Length + change.RemovedConnections.Length + addedItems.Length > 0)
                {
                    Network.Rebuild(graph.Cells.ToArray(), graph.Edges.ToArray(), addedItems);
                    foreach (var component in graph.Components.Values) component.Bind(this);
                    journal.Record(change);
                }
                dirty = false;
                PublishOccupancy();
            }
            // 搬出・破棄済みのpayloadを確定境界で解放する。
            // Release payloads for departed items at the committed boundary.
            var live = new HashSet<Guid>();
            foreach (var item in Network.CaptureItems()) live.Add(item.Item.Guid);
            foreach (var id in new List<Guid>(stacks.Keys)) if (!live.Contains(id)) stacks.Remove(id);
            CompletedTick = GameUpdater.CurrentTick;
            committed = new BeltCommittedSnapshot(CompletedTick, Network.Capture());
            differences.OnNext(journal.Complete(CompletedTick));
        }

        public BeltCommittedSnapshot CaptureCommittedSnapshot()
        {
            Initialize();
            return committed;
        }
        public IBeltReceiver Create(BeltNetworkConnection connection, int stage) =>
            new BeltMachineReceiver(this, connection, graph.Outputs[(connection.SourceId, connection.Direction)], stage);
        public void OnDropped(BeltCellItemState item, string reason)
        {
            Debug.LogWarning($"Belt item removed: cell={item.CellId}, item={item.Item.Guid}, reason={reason}");
        }
        internal IItemStack GetStack(Guid id) => stacks[id];
        internal void RegisterStack(Guid id, IItemStack stack) => stacks[id] = stack;
        internal bool HasInput(int cellId, int sourceId)
        {
            foreach (var edge in graph.Edges)
                if (!edge.SourceIsBelt && edge.TargetId == cellId && edge.SourceId == sourceId) return true;
            // 不正な直結はグラフ構築時にも原因を記録する。
            // Invalid direct connections are also diagnosed during graph construction.
            Debug.LogWarning($"Belt input refused: source={sourceId}, cell={cellId}, no committed external input edge.");
            return false;
        }
        internal bool CanInsert(int cellId) => Network.CanInsert(cellId);
        internal int GetSlotSize(int cellId)
        {
            var path = Network.GetPath(cellId);
            return path.Cells[path.Cells.Length - 1].Id == cellId && path.Segment.Buffer != null ? 2 : 1;
        }
        internal int GetPriority(int cellId)
        {
            var path = Network.GetPath(cellId);
            return path.Cells[path.Cells.Length - 1].Id == cellId ? path.Segment.PriorityOrder : 0;
        }
        internal BeltCellItemState[] CaptureCell(int cellId)
        {
            var result = new List<BeltCellItemState>();
            foreach (var item in Network.CaptureItems()) if (item.CellId == cellId) result.Add(item);
            return result.ToArray();
        }
        internal bool TryInsert(int cellId, BeltDirection direction, int length, BeltItem item, IItemStack stack)
        {
            if (!Network.TryInsert(cellId, direction, length, item)) return false;
            stacks[item.Guid] = stack;
            journal.Record(new BeltInputChange(cellId, direction, length, item));
            PublishOccupancy();
            return true;
        }
        internal void ReplaceCellItems(int cellId, BeltCellItemState[] items)
        {
            Network.ReplaceCellItems(cellId, items);
            journal.Record(new BeltCellItemsChange(cellId, items));
            PublishOccupancy();
        }
        internal void ChangeSpeed(int cellId, int speed) => pendingSpeeds[cellId] = speed;
        public void LatchSpeeds()
        {
            if (pendingSpeeds.Count == 0) return;
            var changes = new List<BeltCellSpeed>();
            foreach (var pair in pendingSpeeds) changes.Add(new BeltCellSpeed(pair.Key, pair.Value));
            pendingSpeeds.Clear();
            // 同時の速度変更を一括適用し、中間分割による欠落を防ぐ。
            // Apply simultaneous speed changes atomically to avoid intermediate repartition losses.
            var change = new BeltSpeedChange(changes.ToArray());
            change.Apply(Network);
            journal.Record(change);
        }
        internal void RecordOutput(BeltOutputResult result) => journal.RecordOutput(result);

        private BeltCellItemState[] GatherPending()
        {
            var items = new List<BeltCellItemState>();
            foreach (var component in graph.Components.Values)
            {
                foreach (var pair in component.PendingStacks) stacks[pair.Key] = pair.Value;
                if (component.PendingStacks.Count != 0) items.AddRange(component.CaptureItems());
            }
            return items.ToArray();
        }
        private void PublishOccupancy()
        {
            var updated = new Dictionary<int, int>();
            foreach (var item in Network.CaptureItems()) updated[item.CellId] = updated.TryGetValue(item.CellId, out var count) ? count + 1 : 1;
            foreach (var pair in graph.Components)
            {
                int before = counts.TryGetValue(pair.Key, out var previous) ? previous : 0;
                int after = updated.TryGetValue(pair.Key, out var current) ? current : 0;
                if (before != after) pair.Value.NotifyItemsChanged();
            }
            counts.Clear();
            foreach (var pair in updated) counts.Add(pair.Key, pair.Value);
        }
    }
}
