using Game.Block.Interface.Component;
using Game.Train.Unit.Containers;

namespace Game.Block.Blocks.TrainRail.TransferComponents
{
    public class TrainPlatformItemTransferComponent : IUpdatableBlockComponent
    {
        public bool IsDestroy { get; private set; }
        private readonly TrainPlatformDockingComponent _dockingComponent;
        private readonly TrainPlatformContainerComponent _containerComponent;
        
        public TrainPlatformItemTransferComponent(TrainPlatformDockingComponent dockingComponent, TrainPlatformContainerComponent containerComponent)
        {
            _dockingComponent = dockingComponent;
            _containerComponent = containerComponent;
        }
        
        public void Update()
        {
            if (!IsTargetContainer(out var targetContainer, out var container)) return;
            if (!CanTransfer(targetContainer, container))
            {
                _dockingComponent.StartRetracting();
                return;
            }
            
            _dockingComponent.StartExtending();
            
            if (_dockingComponent.ArmState != ArmState.Extended) return;
            
            targetContainer.MergeFrom(container);
            
            _dockingComponent.StartRetracting();
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
        
        private bool CanTransfer(ItemTrainCarContainer trainCarContainer, ItemTrainCarContainer stationContainer)
        {
            if (!trainCarContainer.CanInsert(stationContainer)) return false;
            return true;
        }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}