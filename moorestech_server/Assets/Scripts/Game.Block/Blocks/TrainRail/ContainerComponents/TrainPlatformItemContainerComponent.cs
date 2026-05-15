using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Connector;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Train.Unit.Containers;
using JetBrains.Annotations;
using MessagePack;

namespace Game.Block.Blocks.TrainRail.ContainerComponents
{
    public class TrainPlatformItemContainerComponent : IUpdatableBlockComponent, IOpenableBlockInventoryComponent, IBlockSaveState
    {
        public IReadOnlyList<IItemStack> InventoryItems => Container?.InventoryItems ?? CreateEmptySlotItems();
        public bool IsDestroy { get; private set; }
        [CanBeNull] public ItemTrainCarContainer Container { get; private set; }
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
        }

        public TrainPlatformItemContainerComponent(TrainPlatformDockingComponent dockingComponent, TrainPlatformTransferComponent transferComponent, int slotsCount, IBlockInventoryInserter blockInventoryInserter, Dictionary<string, string> componentStates)
        {
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _slotsCount = slotsCount;
            _blockInventoryInserter = blockInventoryInserter;

            var serialized = componentStates[SaveKey];
            var serializedBytes = MessagePackSerializer.ConvertFromJson(serialized);
            var saveData = MessagePackSerializer.Deserialize<TrainPlatformItemContainerComponentSaveData>(serializedBytes);
            if (saveData == null) return;

            Container = saveData.Container;
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
                        
                        if (Container == null)
                        {
                            _dockingComponent.StartRetracting();
                            return;
                        }
                        
                        if (targetContainer == null)
                        {
                            dockedCar.SetContainer(Container);
                            Container = null;
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
                        
                        if (dockedCar.Container == null)
                        {
                            _dockingComponent.StartRetracting();
                            return;
                        }
                        if (Container == null)
                        {
                            Container = dockedCar.Container as ItemTrainCarContainer;
                            dockedCar.SetContainer(null);
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
        
        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            EnsureContainer();
            var remaining = itemStack;
            var slots = Container!.InventoryItems;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!slot.IsAllowedToAddWithRemain(remaining)) continue;

                var result = slot.AddItem(remaining);
                Container.SetItem(i, result.ProcessResultItemStack);
                remaining = result.RemainderItemStack;

                if (remaining.Count == 0) return remaining;
            }

            return remaining;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            EnsureContainer();
            return Container!.InsertItem(itemStack);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            EnsureContainer();
            return Container!.InsertItem(itemId, count);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            EnsureContainer();
            return Container!.InsertItem(itemStacks);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            if (Container == null) return true;

            var slotsCopy = Container.InventoryItems.ToList();
            foreach (var itemStack in itemStacks)
            {
                var remaining = itemStack;
                for (var i = 0; i < slotsCopy.Count; i++)
                {
                    if (!slotsCopy[i].IsAllowedToAddWithRemain(remaining)) continue;

                    var result = slotsCopy[i].AddItem(remaining);
                    slotsCopy[i] = result.ProcessResultItemStack;
                    remaining = result.RemainderItemStack;

                    if (remaining.Count == 0) break;
                }

                if (remaining.Count > 0) return false;
            }

            return true;
        }

        public IItemStack GetItem(int slot)
        {
            if (Container == null) return ServerContext.ItemStackFactory.CreatEmpty();

            return Container.InventoryItems[slot];
        }

        public void SetItem(int slot, IItemStack stack)
        {
            EnsureContainer();
            Container.SetItem(slot, stack);
        }

        public void SetItem(int slot, ItemId itemId, int count)
        {
            EnsureContainer();
            Container!.SetItem(slot, itemId, count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            EnsureContainer();
            return Container!.ReplaceItem(slot, itemStack);
        }

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            EnsureContainer();
            return Container!.ReplaceItem(slot, itemId, count);
        }

        public int GetSlotSize()
        {
            return _slotsCount;
        }

        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            return new ReadOnlyCollection<IItemStack>(InventoryItems.ToList());
        }
        
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
            if (to is null) return true;
            if (from is null) return false;
            if (!to.CanInsert(from)) return false;
            return true;
        }
        
        private void PushItemsToAdjacentBlocks()
        {
            if (Container == null) return;

            var slots = Container.InventoryItems;
            for (var i = 0; i < slots.Count; i++)
            {
                var setItem = _blockInventoryInserter.InsertItem(slots[i]);
                Container.SetItem(i, setItem);
            }
        }

        private void EnsureContainer()
        {
            if (Container == null) Container = ItemTrainCarContainer.CreateWithEmptySlots(_slotsCount);
        }

        private IReadOnlyList<IItemStack> CreateEmptySlotItems()
        {
            // UI表示用の空スロットはプラットフォームに実コンテナを生成しない
            // Empty slots for UI display must not create a real platform container
            var items = new List<IItemStack>(_slotsCount);
            for (var i = 0; i < _slotsCount; i++) items.Add(ServerContext.ItemStackFactory.CreatEmpty());
            return items;
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
