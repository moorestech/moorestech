using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Connector;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Train.Unit.Containers;
using JetBrains.Annotations;
using MessagePack;

namespace Game.Block.Blocks.TrainRail.ContainerComponents
{
    public class TrainPlatformItemContainerComponent : IUpdatableBlockComponent, IOpenableBlockInventoryComponent, IBlockSaveState
    {
        public IReadOnlyList<IItemStack> InventoryItems => Container.InventoryItems;
        public BlockInstanceId BlockInstanceId { get; }
        public bool IsDestroy { get; private set; }
        public ItemTrainCarContainer Container { get; private set; }
        private readonly TrainPlatformDockingComponent _dockingComponent;
        private readonly TrainPlatformTransferComponent _transferComponent;
        private readonly IBlockInventoryInserter _blockInventoryInserter;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        private readonly int _slotsCount;
        private IItemStack[] _previousInventorySnapshot;

        public TrainPlatformItemContainerComponent(BlockInstanceId blockInstanceId, BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent, TrainPlatformDockingComponent dockingComponent, TrainPlatformTransferComponent transferComponent, int slotsCount, IBlockInventoryInserter blockInventoryInserter)
        {
            BlockInstanceId = blockInstanceId;
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _slotsCount = slotsCount;
            _blockInventoryInserter = blockInventoryInserter;
            Container = ItemTrainCarContainer.CreateWithEmptySlots(_slotsCount);
            InitializeSnapshot();
        }

        public TrainPlatformItemContainerComponent(BlockInstanceId blockInstanceId, BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent, TrainPlatformDockingComponent dockingComponent, TrainPlatformTransferComponent transferComponent, int slotsCount, IBlockInventoryInserter blockInventoryInserter, Dictionary<string, string> componentStates)
        {
            BlockInstanceId = blockInstanceId;
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _slotsCount = slotsCount;
            _blockInventoryInserter = blockInventoryInserter;
            Container = ItemTrainCarContainer.CreateWithEmptySlots(_slotsCount);

            LoadContainer();
            // 保存データ復元時はイベント発火不要なのでスナップショットだけ取り直す
            // Loading from save data must not raise events; only re-seed the snapshot
            InitializeSnapshot();

            #region Internal

            void LoadContainer()
            {
                if (!componentStates.TryGetValue(SaveKey, out var serialized)) return;

                var serializedBytes = MessagePackSerializer.ConvertFromJson(serialized);
                var saveData = MessagePackSerializer.Deserialize<TrainPlatformItemContainerComponentSaveData>(serializedBytes);
                if (saveData?.Container is not ItemTrainCarContainer loadedContainer) return;

                Container = loadedContainer;
            }

            #endregion
        }

        public void Update()
        {
            PushItemsToAdjacentBlocks();

            var dockedCar = _dockingComponent.DockedTrainCar;
            if (dockedCar == null)
            {
                EmitChangedSlots();
                return;
            }

            if (!IsTargetContainer(out var targetContainer))
            {
                EmitChangedSlots();
                return;
            }

            switch (_transferComponent.Mode)
            {
                case TrainPlatformTransferComponent.TransferMode.LoadToTrain:
                    {
                        if (!CanTransfer(targetContainer, Container))
                        {
                            _dockingComponent.StartRetracting();
                            break;
                        }

                        _dockingComponent.StartExtending();

                        if (_dockingComponent.ArmState != ArmState.Extended) break;

                        if (Container.IsEmpty())
                        {
                            _dockingComponent.StartRetracting();
                            break;
                        }

                        if (targetContainer == null)
                        {
                            dockedCar.SetContainer(Container);
                            Container = ItemTrainCarContainer.CreateWithEmptySlots(_slotsCount);
                            _dockingComponent.StartRetracting();
                            break;
                        }

                        targetContainer.MergeFrom(Container);

                        _dockingComponent.StartRetracting();
                        break;
                    }
                case TrainPlatformTransferComponent.TransferMode.UnloadToPlatform:
                    {
                        if (!CanTransfer(Container, targetContainer))
                        {
                            _dockingComponent.StartRetracting();
                            break;
                        }

                        _dockingComponent.StartExtending();

                        if (_dockingComponent.ArmState != ArmState.Extended) break;

                        if (targetContainer == null || targetContainer.IsEmpty())
                        {
                            _dockingComponent.StartRetracting();
                            break;
                        }

                        Container.MergeFrom(targetContainer);

                        _dockingComponent.StartRetracting();
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // 列車との転送やコンテナ差し替えで起こったスロット変化を通知
            // Emit slot changes caused by train transfer or container swap
            EmitChangedSlots();
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context) { var r = Container.InsertItem(itemStack); EmitChangedSlots(); return r; }

        public IItemStack InsertItem(IItemStack itemStack) { var r = Container.InsertItem(itemStack); EmitChangedSlots(); return r; }

        public IItemStack InsertItem(ItemId itemId, int count) { var r = Container.InsertItem(itemId, count); EmitChangedSlots(); return r; }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) { var r = Container.InsertItem(itemStacks); EmitChangedSlots(); return r; }

        public bool InsertionCheck(List<IItemStack> itemStacks) { return Container.InsertionCheck(itemStacks); }

        public IItemStack GetItem(int slot) { return Container.GetItem(slot); }

        public void SetItem(int slot, IItemStack stack) { Container.SetItem(slot, stack); EmitChangedSlots(); }

        public void SetItem(int slot, ItemId itemId, int count) { Container.SetItem(slot, itemId, count); EmitChangedSlots(); }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { var r = Container.ReplaceItem(slot, itemStack); EmitChangedSlots(); return r; }

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count) { var r = Container.ReplaceItem(slot, itemId, count); EmitChangedSlots(); return r; }

        public int GetSlotSize() { return Container.GetSlotSize(); }

        public ReadOnlyCollection<IItemStack> CreateCopiedItems() { return Container.CreateCopiedItems(); }

        private bool IsTargetContainer([CanBeNull] out ItemTrainCarContainer trainCarContainer)
        {
            var trainContainer = _dockingComponent.DockedTrainCar?.Container;
            if (trainContainer is ItemTrainCarContainer itemTrainCarContainer)
            {
                trainCarContainer = itemTrainCarContainer;
                return true;
            }

            if (trainContainer is null)
            {
                trainCarContainer = null;
                return true;
            }

            trainCarContainer = null;
            return false;
        }

        private bool CanTransfer(ItemTrainCarContainer to, ItemTrainCarContainer from)
        {
            // 移行元が存在しないか空なら転送しない
            // Do not transfer when the source is missing or empty
            if (from is null || from.IsEmpty()) return false;

            // 移行先が未装着ならコンテナごと移せる
            // A missing destination can receive the whole container
            if (to is null) return true;

            if (!to.CanInsert(from)) return false;
            return true;
        }

        private void PushItemsToAdjacentBlocks()
        {
            var slots = Container.InventoryItems;
            for (var i = 0; i < slots.Count; i++)
            {
                var setItem = _blockInventoryInserter.InsertItem(slots[i]);
                Container.SetItem(i, setItem);
            }
        }

        private void InitializeSnapshot()
        {
            // スナップショットは現在のコンテナ状態と完全に一致させる
            // Snapshot must exactly match the current container state
            var items = Container.InventoryItems;
            _previousInventorySnapshot = new IItemStack[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                _previousInventorySnapshot[i] = items[i];
            }
        }

        private void EmitChangedSlots()
        {
            // コンテナ差し替えでスロット数が変わった場合はスナップショットを作り直す
            // If the container was swapped and the slot count changed, rebuild the snapshot
            var items = Container.InventoryItems;
            if (_previousInventorySnapshot == null || _previousInventorySnapshot.Length != items.Count)
            {
                var rebuilt = new IItemStack[items.Count];
                for (var i = 0; i < items.Count; i++) rebuilt[i] = items[i];
                _previousInventorySnapshot = rebuilt;
                // 差分が不明なので全スロットを通知
                // We have no prior diff baseline, so notify every slot
                for (var i = 0; i < items.Count; i++)
                {
                    _blockInventoryUpdateEvent.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, i, items[i]));
                }
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var previous = _previousInventorySnapshot[i];
                var current = items[i];
                if (previous.Id == current.Id && previous.Count == current.Count) continue;
                _previousInventorySnapshot[i] = current;
                _blockInventoryUpdateEvent.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, i, current));
            }
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
        public string SaveKey { get; } = typeof(TrainPlatformItemContainerComponent).FullName;

        public string GetSaveState()
        {
            return MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(new TrainPlatformItemContainerComponentSaveData(Container)));
        }

        [MessagePackObject]
        public class TrainPlatformItemContainerComponentSaveData
        {
            [Key(0)] public ItemTrainCarContainer Container;

            public TrainPlatformItemContainerComponentSaveData(ItemTrainCarContainer container)
            {
                Container = container;
            }
        }
    }
}
