using System;
using Game.Block.Interface.Component;
using Game.Train.Unit.Containers;
using JetBrains.Annotations;

namespace Game.Block.Blocks.TrainRail.ContainerComponents
{
    public class TrainPlatformItemContainerComponent : IUpdatableBlockComponent
    {
        public bool IsDestroy { get; private set; }
        public ItemTrainCarContainer Container { get; private set; }
        private readonly TrainPlatformDockingComponent _dockingComponent;
        private readonly TrainPlatformTransferComponent _transferComponent;
        
        public TrainPlatformItemContainerComponent(TrainPlatformDockingComponent dockingComponent, TrainPlatformTransferComponent transferComponent)
        {
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
        }
        
        public void Update()
        {
            if (!IsTargetContainer(out var targetContainer)) return;
            
            switch (_transferComponent.Mode)
            {
                case TrainPlatformTransferComponent.TransferMode.LoadToTrain:
                    {
                        if (!CanTransfer(Container, targetContainer))
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
                        if (!CanTransfer(targetContainer, Container))
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
        
        private bool IsTargetContainer([CanBeNull] out ItemTrainCarContainer trainCarContainer)
        {
            var trainContainer = _dockingComponent.DockedTrainCar?.Container; 
            if (trainContainer is ItemTrainCarContainer itemTrainCarContainer)
            {
                trainCarContainer = itemTrainCarContainer;
                return true;
            }
            
            if (trainContainer == null)
            {
                trainCarContainer = null;
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
    }
}