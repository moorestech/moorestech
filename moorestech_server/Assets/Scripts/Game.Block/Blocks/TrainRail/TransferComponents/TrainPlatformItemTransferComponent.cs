using System;
using Game.Block.Interface.Component;
using Game.Train.Unit.Containers;

namespace Game.Block.Blocks.TrainRail.TransferComponents
{
    public class TrainPlatformItemTransferComponent : IUpdatableBlockComponent
    {
        public bool IsDestroy { get; private set; }
        private readonly TrainPlatformDockingComponent _dockingComponent;
        private readonly TrainPlatformContainerComponent _containerComponent;
        private readonly TrainPlatformTransferComponent _transferComponent;
        
        public TrainPlatformItemTransferComponent(TrainPlatformDockingComponent dockingComponent, TrainPlatformContainerComponent containerComponent, TrainPlatformTransferComponent transferComponent)
        {
            _dockingComponent = dockingComponent;
            _containerComponent = containerComponent;
            _transferComponent = transferComponent;
        }
        
        public void Update()
        {
            if (!IsTargetContainer(out var targetContainer, out var container)) return;
            
            switch (_transferComponent.Mode)
            {
                case TrainPlatformTransferComponent.TransferMode.LoadToTrain:
                    {
                        if (!CanTransfer(container, targetContainer))
                        {
                            _dockingComponent.StartRetracting();
                            return;
                        }
                        
                        _dockingComponent.StartExtending();
                        
                        if (_dockingComponent.ArmState != ArmState.Extended) return;
                        
                        targetContainer.MergeFrom(container);
                        
                        _dockingComponent.StartRetracting();
                        break;
                    }
                case TrainPlatformTransferComponent.TransferMode.UnloadToPlatform:
                    {
                        if (!CanTransfer(targetContainer, container))
                        {
                            _dockingComponent.StartRetracting();
                            return;
                        }
                        
                        _dockingComponent.StartExtending();
                        
                        if (_dockingComponent.ArmState != ArmState.Extended) return;
                        
                        container.MergeFrom(targetContainer);
                        
                        _dockingComponent.StartRetracting();
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private bool IsTargetContainer(out ItemTrainCarContainer trainCarContainer, out ItemTrainCarContainer stationContainer)
        {
            if (_dockingComponent.DockedTrainCar.Container is ItemTrainCarContainer itemTrainCarContainer
                && _containerComponent.Container is ItemTrainCarContainer itemStationContainer)
            {
                trainCarContainer = itemTrainCarContainer;
                stationContainer = itemStationContainer;
                return true;
            }
            
            trainCarContainer = null;
            stationContainer = null;
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
    }
}