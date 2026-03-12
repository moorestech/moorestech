using System;
using Game.Block.Interface.Component;
using Game.Train.Unit.Containers;
using JetBrains.Annotations;

namespace Game.Block.Blocks.TrainRail.ContainerComponents
{
    public class TrainPlatformItemContainerComponent : IUpdatableBlockComponent
    {
        public bool IsDestroy { get; private set; }
        [CanBeNull] public ItemTrainCarContainer Container { get; private set; }
        private readonly TrainPlatformDockingComponent _dockingComponent;
        private readonly TrainPlatformTransferComponent _transferComponent;
        private int _slotsCount;
        
        public TrainPlatformItemContainerComponent(TrainPlatformDockingComponent dockingComponent, TrainPlatformTransferComponent transferComponent, int slotsCount)
        {
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _slotsCount = slotsCount;
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
    }
}