using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Train.Unit.Containers;
using JetBrains.Annotations;
using MessagePack;
using Newtonsoft.Json;

namespace Game.Block.Blocks.TrainRail.ContainerComponents
{
    public class TrainPlatformItemContainerComponent : IUpdatableBlockComponent, IBlockInventory, IBlockSaveState
    {
        public bool IsDestroy { get; private set; }
        [CanBeNull] public ItemTrainCarContainer Container { get; private set; }
        private readonly TrainPlatformDockingComponent _dockingComponent;
        private readonly TrainPlatformTransferComponent _transferComponent;
        private readonly int _slotsCount;
        
        public TrainPlatformItemContainerComponent(TrainPlatformDockingComponent dockingComponent, TrainPlatformTransferComponent transferComponent, int slotsCount)
        {
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _slotsCount = slotsCount;
        }
        
        public TrainPlatformItemContainerComponent(TrainPlatformDockingComponent dockingComponent, TrainPlatformTransferComponent transferComponent, int slotsCount, Dictionary<string, string> componentStates)
        {
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _slotsCount = slotsCount;
            
            var serialized = componentStates[SaveKey];
            var serializedBytes = MessagePackSerializer.ConvertFromJson(serialized);
            var saveData = MessagePackSerializer.Deserialize<TrainPlatformItemContainerComponentSaveData>(serializedBytes);
            if (saveData == null) return;
            
            Container = saveData.Container;
        }
        
        public void Update()
        {
            var dockedCar = _dockingComponent.DockedTrainCar;
            if (dockedCar == null) return;

            if (!IsTargetContainer(out var targetContainer)) return;
            
            switch (_transferComponent.Mode)
            {
                case TrainPlatformTransferComponent.TransferMode.LoadToTrain:
                    {
                        if (Container == null)
                        {
                            _dockingComponent.StartRetracting();
                            return;
                        }
                        
                        if (dockedCar.Container == null)
                        {
                            dockedCar.SetContainer(Container);
                            Container = null;
                            _dockingComponent.StartRetracting();
                            return;
                        }
                        
                        if (!CanTransfer(targetContainer, Container))
                        {
                            _dockingComponent.StartRetracting();
                            return;
                        }
                        
                        _dockingComponent.StartExtending();
                        
                        if (_dockingComponent.ArmState != ArmState.Extended) return;
                        
                        targetContainer.MergeFrom(Container);
                        
                        _dockingComponent.StartRetracting();
                        break;
                    }
                case TrainPlatformTransferComponent.TransferMode.UnloadToPlatform:
                    {
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

                        if (!CanTransfer(Container, targetContainer))
                        {
                            _dockingComponent.StartRetracting();
                            return;
                        }
                        
                        _dockingComponent.StartExtending();
                        
                        if (_dockingComponent.ArmState != ArmState.Extended) return;
                        
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
            if (Container == null) Container = ItemTrainCarContainer.CreateWithEmptySlots(_slotsCount);

            var remaining = itemStack;
            var slots = Container!.InventoryItems;
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i].Stack;
                if (!slot.IsAllowedToAddWithRemain(remaining)) continue;

                var result = slot.AddItem(remaining);
                Container.SetItem(i, result.ProcessResultItemStack);
                remaining = result.RemainderItemStack;

                if (remaining.Count == 0) return remaining;
            }

            return remaining;
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            if (Container == null) return true;

            var slotsCopy = Container.InventoryItems.ToArray().Select(s => s.Stack).ToList();
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

            return Container.InventoryItems[slot].Stack;
        }

        public void SetItem(int slot, IItemStack stack)
        {
            if (Container == null) Container = ItemTrainCarContainer.CreateWithEmptySlots(_slotsCount);

            Container.SetItem(slot, stack);
        }

        public int GetSlotSize()
        {
            return _slotsCount;
        }
        
        private bool IsTargetContainer(out ItemTrainCarContainer trainCarContainer)
        {
            var trainContainer = _dockingComponent.DockedTrainCar?.Container;
            if (trainContainer is ItemTrainCarContainer itemTrainCarContainer)
            {
                trainCarContainer = itemTrainCarContainer;
                return true;
            }

            trainCarContainer = null;
            return false;
        }
        
        private bool CanTransfer(ItemTrainCarContainer to, ItemTrainCarContainer from)
        {
            if (!to.CanInsert(from)) return false;
            return true;
        }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        public string SaveKey { get; } = typeof(TrainPlatformItemContainerComponent).FullName;
        
        public string GetSaveState()
        {
            return JsonConvert.SerializeObject(new TrainPlatformItemContainerComponentSaveData(Container));
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