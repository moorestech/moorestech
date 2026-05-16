using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Connector;
using Game.Block.Interface.Component;
using Game.Train.Unit.Containers;
using JetBrains.Annotations;
using MessagePack;

namespace Game.Block.Blocks.TrainRail.ContainerComponents
{
    public class TrainPlatformItemContainerComponent : IUpdatableBlockComponent, IOpenableBlockInventoryComponent, IBlockSaveState
    {
        public IReadOnlyList<IItemStack> InventoryItems => Container.InventoryItems;
        public bool IsDestroy { get; private set; }
        public ItemTrainCarContainer Container { get; private set; }
        private readonly TrainPlatformDockingComponent _dockingComponent;
        private readonly TrainPlatformTransferComponent _transferComponent;
        private readonly IBlockInventoryInserter _blockInventoryInserter;
        private readonly int _slotsCount;

        public TrainPlatformItemContainerComponent(TrainPlatformDockingComponent dockingComponent, TrainPlatformTransferComponent transferComponent, int slotsCount, IBlockInventoryInserter blockInventoryInserter)
        {
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _slotsCount = slotsCount;
            _blockInventoryInserter = blockInventoryInserter;
            Container = ItemTrainCarContainer.CreateWithEmptySlots(_slotsCount);
        }

        public TrainPlatformItemContainerComponent(TrainPlatformDockingComponent dockingComponent, TrainPlatformTransferComponent transferComponent, int slotsCount, IBlockInventoryInserter blockInventoryInserter, Dictionary<string, string> componentStates)
        {
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _slotsCount = slotsCount;
            _blockInventoryInserter = blockInventoryInserter;
            Container = ItemTrainCarContainer.CreateWithEmptySlots(_slotsCount);

            LoadContainer();
            
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
                    {
                        if (!CanTransfer(targetContainer, Container))
                        {
                            _dockingComponent.StartRetracting();
                            return;
                        }
                        
                        _dockingComponent.StartExtending();
                        
                        if (_dockingComponent.ArmState != ArmState.Extended) return;
                        
                        if (Container.IsEmpty())
                        {
                            _dockingComponent.StartRetracting();
                            return;
                        }
                        
                        if (targetContainer == null)
                        {
                            dockedCar.SetContainer(Container);
                            Container = ItemTrainCarContainer.CreateWithEmptySlots(_slotsCount);
                            _dockingComponent.StartRetracting();
                            return;
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
                            return;
                        }
                        
                        _dockingComponent.StartExtending();
                        
                        if (_dockingComponent.ArmState != ArmState.Extended) return;
                        
                        if (targetContainer == null || targetContainer.IsEmpty())
                        {
                            _dockingComponent.StartRetracting();
                            return;
                        }
                        
                        Container.MergeFrom(targetContainer);
                        
                        _dockingComponent.StartRetracting();
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context) { return Container.InsertItem(itemStack); }

        public IItemStack InsertItem(IItemStack itemStack) { return Container.InsertItem(itemStack); }

        public IItemStack InsertItem(ItemId itemId, int count) { return Container.InsertItem(itemId, count); }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) { return Container.InsertItem(itemStacks); }

        public bool InsertionCheck(List<IItemStack> itemStacks) { return Container.InsertionCheck(itemStacks); }

        public IItemStack GetItem(int slot) { return Container.GetItem(slot); }

        public void SetItem(int slot, IItemStack stack) { Container.SetItem(slot, stack); }

        public void SetItem(int slot, ItemId itemId, int count) { Container.SetItem(slot, itemId, count); }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return Container.ReplaceItem(slot, itemStack); }

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count) { return Container.ReplaceItem(slot, itemId, count); }

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
