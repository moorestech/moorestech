using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Update;
using Game.BeltSegment;
using Game.Block.Interface.Component;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;
namespace Game.Block.Blocks.BeltConveyor
{
    public sealed class BeltWorldDatastore : IBeltWorldLookup, IBeltWorldMutation, IPostLoadInitializable
    {
        private readonly IWorldBlockDatastore _world;
        private readonly HashSet<SegmentBeltComponent> _belts = new();
        private readonly BeltWorldItems _items = new();
        private readonly BeltWorldTickUpdater _ticks = new();
        private readonly Subject<BeltWorldSnapshot> _rebuilt = new();
        private readonly Subject<BeltWorldFrame> _frames = new();
        private BeltTopologyBuilder _topology;
        private BeltSimulationGraph _graph;
        private BeltStreamPosition _position;
        private ulong _generation;
        private bool _dirty = true;
        public IObservable<BeltWorldSnapshot> OnRebuilt => _rebuilt;
        public IObservable<BeltWorldFrame> OnFrame => _frames;
        public BeltWorldDatastore(IWorldBlockDatastore world, IWorldBlockUpdateEvent events)
        {
            _world = world;
            events.OnBlockPlaceEvent.Subscribe(_ => _dirty = true);
            events.OnBlockRemoveEvent.Subscribe(_ => _dirty = true);
            _graph = new BeltSimulationGraph(new BeltReplaySnapshot(Array.Empty<BeltReplaySegmentState>(),
                Array.Empty<BeltReplayLink>(), Array.Empty<BeltReplayInput>(), Array.Empty<BeltReplayOutput>()),
                Array.Empty<IBeltSource>(), Array.Empty<IBeltReceiver>());
        }
        public void Load()
        {
            Rebuild();
            _position = new BeltStreamPosition(GameUpdater.CurrentTick, 0);
        }
        public void Register(SegmentBeltComponent component) { _belts.Add(component); _items.Register(component); _dirty = true; }
        public void Unregister(SegmentBeltComponent component)
        {
            CaptureCells(); _items.Unregister(component); _belts.Remove(component); _dirty = true;
        }
        public BeltCellSaveState CaptureCell(SegmentBeltComponent target) { CaptureCells(); return _items.Cells[target]; }
        public IItemStack GetCellItem(SegmentBeltComponent target)
        {
            var saved = CaptureCell(target).RunningItem;
            return saved == null ? ServerContext.ItemStackFactory.CreatEmpty() : _items.Payload(saved.TransportGuid);
        }
        public void SetCellItem(SegmentBeltComponent target, IItemStack stack)
        {
            if (_ticks.InWindow) throw new InvalidOperationException("SetItem is a belt boundary operation.");
            if (stack.Count > 1) throw new ArgumentException("A belt cell holds one running item.");
            var state = CaptureCell(target);
            if (state.RunningItem != null) _items.Remove(state.RunningItem.TransportGuid);
            BeltSavedItem saved = stack.Count == 0 ? null : _items.Save(_items.Create(stack,
                BeltTopologyGeometry.Direction(target.Position.BlockDirection.ConvertLocalCell(Vector3Int.back))), 16);
            _items.Cells[target] = new BeltCellSaveState(state.PriorityIndex, saved, state.BufferedItem);
            _dirty = true;
        }
        public IItemStack Insert(SegmentBeltComponent target, IItemStack stack, InsertItemContext context)
        {
            if (stack.Count == 0) return stack;
            if (!_ticks.InWindow)
            {
                if (GetCellItem(target).Count != 0) return stack;
                SetCellItem(target, stack.SubItem(stack.Count - 1));
                return stack.SubItem(1);
            }
            if (!_topology.Ports.Resolve(target, context, out int id))
            {
                Debug.LogWarning($"Unbound belt input at {target.Position.OriginalPos}, source {context.SourceBlockInstanceId}.");
                return stack;
            }
            int length = Math.Min(16, _graph.GetInputOffer(id));
            if (length <= 0) return stack;
            var item = _items.Create(stack, _topology.Inputs[id].InputDirection);
            if (!_graph.TryInsert(id, length, item)) throw new InvalidOperationException("A belt offer changed during synchronous insertion.");
            _ticks.Insert(new BeltReplayInsertion(id, length, item)); _items.Invalidate();
            return stack.SubItem(1);
        }
        public void BeginTick(ulong tick)
        {
            if (_dirty)
            {
                Rebuild();
                _position = new BeltStreamPosition(_position.Tick, checked(_position.Sequence + 1));
                _rebuilt.OnNext(CaptureSnapshot());
            }
            _ticks.Begin(tick, _position, _graph, _topology.Ports);
            _items.Invalidate();
        }
        public void CompleteTick()
        {
            var frame = _ticks.Complete(_generation, _topology.Ports);
            _position = frame.Position; _frames.OnNext(frame);
        }
        // 読取要求はdirtyでも確定済みgraphを返し、世界変更を先取りしない。
        // Reads return the completed graph even when dirty, without advancing pending world mutations.
        public BeltWorldSnapshot CaptureSnapshot()
            => new(_position, _generation, _graph.CaptureSnapshot(), _topology == null ? Array.Empty<BeltRoute>() : _topology.Paths.Routes.ToArray());
        private void CaptureCells()
        {
            if (_topology != null) _items.Capture(_graph, _topology.Paths.Owners);
        }
        private void Rebuild()
        {
            CaptureCells();
            _topology = new BeltTopologyBuilder(_belts, _world, _items);
            _graph = BeltWorldRebuild.Build(_topology, _items);
            _items.Invalidate();
            _generation++; _dirty = false;
        }
    }
}
