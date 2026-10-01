using System;
using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.InventoryConnectsModule;
using UniRx;

namespace Game.Block.Blocks.BeltConveyor
{
    public sealed class VanillaBeltConveyorComponent : IBlockInventory, IBlockSaveState, IItemCollectableBeltConveyor
    {
        private readonly InventoryConnects connectors;
        private readonly List<BeltCellItemState> pending = new List<BeltCellItemState>();
        private readonly Dictionary<Guid, IItemStack> pendingStacks = new Dictionary<Guid, IItemStack>();
        private readonly Subject<Unit> itemsChanged = new Subject<Unit>();
        private BeltWorldTransport transport;
        public int CellId { get; }
        public BlockPositionInfo Position { get; }
        public int Speed { get; private set; }
        public string SpeedProfile { get; }
        public BeltConveyorSlopeType SlopeType { get; }
        public bool IsDestroy { get; private set; }
        public string SaveKey => typeof(VanillaBeltConveyorComponent).FullName;
        public IObservable<Unit> OnItemsChanged => itemsChanged;
        internal int PriorityOrder => transport == null ? LoadedPriority : transport.GetPriority(CellId);
        internal int LoadedPriority { get; private set; } = -1;
        internal IReadOnlyDictionary<Guid, IItemStack> PendingStacks => pendingStacks;

        public VanillaBeltConveyorComponent(BlockInstanceId id, BlockPositionInfo position, double transitSeconds,
            bool gear, string speedProfile, BeltConveyorSlopeType slope, InventoryConnects connectors, Dictionary<string, object> componentStates)
        {
            CellId = id.AsPrimitive(); Position = position; SlopeType = slope;
            this.connectors = connectors;
            SpeedProfile = speedProfile;
            // RPM供給前の歯車搬送を防ぐ。
            // Prevent gear transport before RPM is supplied.
            SetTicksOfItemEnterToExit(gear ? uint.MaxValue : GameUpdater.SecondsToTicks(transitSeconds));
            if (componentStates != null) BeltCellSaveCodec.Load(this, componentStates[SaveKey], transitSeconds);
        }

        public IReadOnlyList<IOnBeltConveyorItem> BeltConveyorItems
        {
            get
            {
                var result = new List<IOnBeltConveyorItem>();
                foreach (var item in CaptureItems())
                    result.Add(new BeltSegmentItemView(item, connectors.InputConnects?[0], connectors.OutputConnects?[0]));
                return result;
            }
        }

        public IItemStack InsertItem(IItemStack stack, InsertItemContext context)
        {
            BlockException.CheckDestroy(this);
            if (stack.Count == 0 || !CanInsert()) return stack;
            if (transport != null && context.TargetConnector != null && !transport.HasInput(CellId, context.SourceBlockInstanceId.AsPrimitive())) return stack;
            // 分割も既存stackの操作へ委譲してメタデータを保つ。
            // Split through the existing stack operation to preserve metadata.
            var single = stack.Count == 1 ? stack : stack.SubItem(stack.Count - 1);
            var direction = FindInputDirection(context.TargetConnector?.ConnectorGuid);
            var item = new BeltItem(BeltTransportIdentity.ToGuid(single.ItemInstanceId), single.Id.AsPrimitive());
            bool accepted;
            if (transport != null) accepted = transport.TryInsert(CellId, direction, 1, item, single);
            else
            {
                AddPending(new BeltCellItemState(CellId, 1, direction, 0, item, false), single);
                accepted = true;
                itemsChanged.OnNext(Unit.Default);
            }
            return accepted ? stack.SubItem(1) : stack;
        }

        public bool InsertionCheck(List<IItemStack> stacks) => stacks.Count == 1 && stacks[0].Count == 1 && CanInsert();
        private bool CanInsert()
        {
            if (BeltTransportDirections.IsHorizontal(Position)) return transport == null ? pending.Count == 0 : transport.CanInsert(CellId);
            UnityEngine.Debug.LogWarning($"Belt {CellId} rejects input: vertical orientation {Position.BlockDirection} has no transport path.");
            return false;
        }
        public int GetSlotSize() => transport == null ? (connectors.OutputConnects != null && 1 < connectors.OutputConnects.Length ? 2 : 1) : transport.GetSlotSize(CellId);
        public IItemStack GetItem(int slot)
        {
            if (slot < 0 || GetSlotSize() <= slot) throw new ArgumentOutOfRangeException(nameof(slot));
            foreach (var item in CaptureItems())
                if (item.IsBuffer == (slot == 1)) return GetStack(item.Item.Guid);
            return ServerContext.ItemStackFactory.CreatEmpty();
        }

        public void SetItem(int slot, IItemStack stack)
        {
            BlockException.CheckDestroy(this);
            if (slot < 0 || GetSlotSize() <= slot) throw new ArgumentOutOfRangeException(nameof(slot));
            var items = new List<BeltCellItemState>(CaptureItems());
            // slotを走行列とbufferへ固定し、空いた走行slotへbufferを詰めない。
            // Keep running and buffer slots fixed instead of compacting a lone buffer into slot zero.
            for (int index = items.Count - 1; 0 <= index; index--)
                if (items[index].IsBuffer == (slot == 1)) items.RemoveAt(index);
            if (0 < stack.Count)
            {
                var single = stack.Count == 1 ? stack : stack.SubItem(stack.Count - 1);
                var item = new BeltItem(BeltTransportIdentity.ToGuid(single.ItemInstanceId), single.Id.AsPrimitive());
                items.Add(new BeltCellItemState(CellId, 256, FindInputDirection(null), 0, item, slot == 1));
                if (transport == null) pendingStacks[item.Guid] = single;
                else transport.RegisterStack(item.Guid, single);
            }
            if (transport == null)
            {
                // 未接続在庫も個数の変化だけを即時通知する。
                // Pending inventory also pushes only count changes immediately.
                int before = pending.Count;
                pending.Clear(); pending.AddRange(items);
                if (before != pending.Count) itemsChanged.OnNext(Unit.Default);
            }
            else transport.ReplaceCellItems(CellId, items.ToArray());
        }

        public void SetTicksOfItemEnterToExit(uint ticks)
        {
            int previous = Speed;
            Speed = ticks == uint.MaxValue || ticks == 0 ? 0 : Math.Max(1, Math.Min(128, (int)Math.Round(256d / ticks)));
            if (transport != null && previous != Speed) transport.ChangeSpeed(CellId, Speed);
        }
        public void Destroy() => IsDestroy = true;
        public object GetSaveState() => BeltCellSaveCodec.Capture(this);
        internal BeltCellItemState[] CaptureItems() => transport == null ? pending.ToArray() : transport.CaptureCell(CellId);
        internal IItemStack GetStack(Guid id) => transport == null ? pendingStacks[id] : transport.GetStack(id);
        internal void SetLoadedPriority(int priority) => LoadedPriority = priority;
        internal void AddPending(BeltCellItemState item, IItemStack stack)
        {
            pending.Add(item); pendingStacks[item.Item.Guid] = stack;
        }
        internal void Bind(BeltWorldTransport owner)
        {
            transport = owner;
            pending.Clear(); pendingStacks.Clear();
        }
        internal void NotifyItemsChanged() => itemsChanged.OnNext(Unit.Default);

        internal BeltDirection FindInputDirection(Guid? connectorGuid)
        {
            // 非参加姿勢の保存には固定方向を使い、水平変換しない。
            // Use a storage-only direction for unsupported orientations without horizontal conversion.
            if (!BeltTransportDirections.IsHorizontal(Position))
            {
                UnityEngine.Debug.Log($"Belt {CellId} keeps vertical inventory in pending storage with canonical entry direction.");
                return BeltDirection.Back;
            }
            foreach (var connector in connectors.InputConnects)
            {
                if (connector.ConnectorGuid != connectorGuid || connector.Directions == null) continue;
                foreach (var local in connector.Directions)
                    if (local.x != 0 || local.z != 0) return BeltTransportDirections.FromVector(Position.BlockDirection.ConvertLocalCell(local));
            }
            return BeltTransportDirections.Opposite(BeltTransportDirections.Forward(Position));
        }
    }
}
