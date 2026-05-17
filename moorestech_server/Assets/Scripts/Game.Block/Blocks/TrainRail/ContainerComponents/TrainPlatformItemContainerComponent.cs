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
using Game.Context;
using Game.Train.Unit.Containers;
using JetBrains.Annotations;
using MessagePack;
using UniRx;

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
        private IDisposable _slotChangedSubscription;

        public TrainPlatformItemContainerComponent(BlockInstanceId blockInstanceId, BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent, TrainPlatformDockingComponent dockingComponent, TrainPlatformTransferComponent transferComponent, int slotsCount, IBlockInventoryInserter blockInventoryInserter)
        {
            BlockInstanceId = blockInstanceId;
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _slotsCount = slotsCount;
            _blockInventoryInserter = blockInventoryInserter;
            Container = ItemTrainCarContainer.CreateWithEmptySlots(_slotsCount);
            SubscribeContainerSlotChanges();
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
            // 復元後のコンテナを購読し直す。ロード経路はSetItemWithoutEventで埋まるので発火しない
            // Re-subscribe to the restored container; load path uses SetItemWithoutEvent so no events fire
            SubscribeContainerSlotChanges();

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
            if (dockedCar == null) return;
            if (!IsTargetContainer(out var targetContainer)) return;

            switch (_transferComponent.Mode)
            {
                case TrainPlatformTransferComponent.TransferMode.LoadToTrain:
                    HandleLoadToTrain();
                    break;
                case TrainPlatformTransferComponent.TransferMode.UnloadToPlatform:
                    HandleUnloadToPlatform();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            #region Internal

            void PushItemsToAdjacentBlocks()
            {
                var slots = Container.InventoryItems;
                for (var i = 0; i < slots.Count; i++)
                {
                    var setItem = _blockInventoryInserter.InsertItem(slots[i]);
                    Container.SetItem(i, setItem);
                }
            }

            bool IsTargetContainer([CanBeNull] out ItemTrainCarContainer trainCarContainer)
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

            bool CanTransfer(ItemTrainCarContainer to, ItemTrainCarContainer from)
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

            void HandleLoadToTrain()
            {
                if (!CanTransfer(targetContainer, Container)) { _dockingComponent.StartRetracting(); return; }

                _dockingComponent.StartExtending();
                if (_dockingComponent.ArmState != ArmState.Extended) return;

                if (Container.IsEmpty()) { _dockingComponent.StartRetracting(); return; }

                if (targetContainer == null)
                {
                    // 旧コンテナを列車車両へ渡し、プラットフォームは新しい空コンテナへ差し替える
                    // Hand the populated container to the train car and replace the platform with a fresh empty one
                    var handedOff = SwapWithEmptyContainerAndEmitClearedEvents();
                    dockedCar.SetContainer(handedOff);
                    _dockingComponent.StartRetracting();
                    return;
                }

                targetContainer.MergeFrom(Container);
                _dockingComponent.StartRetracting();
            }

            void HandleUnloadToPlatform()
            {
                if (!CanTransfer(Container, targetContainer)) { _dockingComponent.StartRetracting(); return; }

                _dockingComponent.StartExtending();
                if (_dockingComponent.ArmState != ArmState.Extended) return;

                if (targetContainer == null || targetContainer.IsEmpty()) { _dockingComponent.StartRetracting(); return; }

                Container.MergeFrom(targetContainer);
                _dockingComponent.StartRetracting();
            }

            ItemTrainCarContainer SwapWithEmptyContainerAndEmitClearedEvents()
            {
                var handedOff = Container;
                Container = ItemTrainCarContainer.CreateWithEmptySlots(_slotsCount);
                SubscribeContainerSlotChanges();

                // 旧コンテナの中身は車両側へ移譲済みなのでプラットフォーム視点では全スロットが空になる
                // Old contents now belong to the train car; from the platform's view all slots are empty
                var emptyStack = ServerContext.ItemStackFactory.CreatEmpty();
                for (var i = 0; i < handedOff.InventoryItems.Count; i++)
                {
                    _blockInventoryUpdateEvent.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, i, emptyStack));
                }

                return handedOff;
            }

            #endregion
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context) => Container.InsertItem(itemStack);
        public IItemStack InsertItem(IItemStack itemStack) => Container.InsertItem(itemStack);
        public IItemStack InsertItem(ItemId itemId, int count) => Container.InsertItem(itemId, count);
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) => Container.InsertItem(itemStacks);
        public bool InsertionCheck(List<IItemStack> itemStacks) => Container.InsertionCheck(itemStacks);
        public IItemStack GetItem(int slot) => Container.GetItem(slot);
        public void SetItem(int slot, IItemStack stack) => Container.SetItem(slot, stack);
        public void SetItem(int slot, ItemId itemId, int count) => Container.SetItem(slot, itemId, count);
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) => Container.ReplaceItem(slot, itemStack);
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count) => Container.ReplaceItem(slot, itemId, count);
        public int GetSlotSize() => Container.GetSlotSize();
        public ReadOnlyCollection<IItemStack> CreateCopiedItems() => Container.CreateCopiedItems();

        private void SubscribeContainerSlotChanges()
        {
            _slotChangedSubscription?.Dispose();
            _slotChangedSubscription = Container.OnSlotChanged.Subscribe(change =>
                _blockInventoryUpdateEvent.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, change.slot, change.stack)));
        }

        public void Destroy()
        {
            IsDestroy = true;
            _slotChangedSubscription?.Dispose();
            _slotChangedSubscription = null;
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
