using System;
using Game.Block.Interface.Component;
using Game.Train.Unit;

namespace Game.Block.Blocks.TrainRail
{
    public class TrainPlatformDockingComponent : IBlockComponent, ITrainDockingReceiver
    {
        public Guid? DockedTrainId;
        private long? _dockedTrainCarInstanceId;
        private TrainCar _dockedTrainCar;
        private IBlockInventory _dockedStationInventory;
        private TrainDockHandle _dockedHandle;
        
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public bool CanDock(ITrainDockHandle handle)
        {
            if (handle == null) return false;
            if (!DockedTrainId.HasValue && !_dockedTrainCarInstanceId.HasValue) return true;
            return DockedTrainId == handle.TrainId && _dockedTrainCarInstanceId == handle.TrainCarInstanceId;
        }
        
        public void ForceUndock()
        {
            ClearDockedReferences();
            //TODO: もう少しいい感じに一般化できないか
            // if (_armState == ArmState.Extending) StartRetractingFromCurrent();
        }
        
        public void OnTrainDocked(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (DockedTrainId.HasValue && DockedTrainId != handle.TrainId) return;
            if (_dockedTrainCarInstanceId.HasValue && _dockedTrainCarInstanceId != handle.TrainCarInstanceId) return;
            DockedTrainId = handle.TrainId;
            _dockedTrainCarInstanceId = handle.TrainCarInstanceId;
            _dockedHandle = handle as TrainDockHandle;
            //TODO: もう少しいい感じに一般化できないか
            // if (_armState == ArmState.Idle && _armProgressTicks == 0) _shouldStartOnDock = true;
            UpdateDockedReferences(handle);
        }
        
        public void OnTrainDockedTick(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (DockedTrainId != handle.TrainId || _dockedTrainCarInstanceId != handle.TrainCarInstanceId) return;
            _dockedHandle = handle as TrainDockHandle;
            if (_dockedTrainCar != null && _dockedStationInventory != null) return;
            UpdateDockedReferences(handle);
        }
        
        public void OnTrainUndocked(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (DockedTrainId == handle.TrainId && _dockedTrainCarInstanceId == handle.TrainCarInstanceId)
            {
                ClearDockedReferences();
                //TODO: もう少しいい感じに一般化できないか
                // if (_armState == ArmState.Extending) StartRetractingFromCurrent();
            }
        }
        
        private void UpdateDockedReferences(ITrainDockHandle handle)
        {
            if (handle is not TrainDockHandle dockHandle)
            {
                _dockedTrainCar = null;
                _dockedStationInventory = null;
                return;
            }
            
            _dockedHandle = dockHandle;
            _dockedTrainCar = dockHandle.TrainCar;
            _dockedStationInventory = ResolveStationInventory(_dockedTrainCar);
        }
        
        private void ClearDockedReferences()
        {
            DockedTrainId = null;
            _dockedTrainCarInstanceId = null;
            _dockedHandle = null;
            //TODO: もう少しいい感じに一般化できないか
            // _shouldStartOnDock = false;
            _dockedTrainCar = null;
            _dockedStationInventory = null;
        }
        
        //TODO: 必要性がわからない、なぜtrainCarから自身のInventoryを取得しているのか
        private IBlockInventory ResolveStationInventory(TrainCar trainCar)
        {
            if (trainCar?.dockingblock == null)
            {
                return null;
            }
            
            return trainCar.dockingblock.ComponentManager.TryGetComponent<IBlockInventory>(out var inventory)
                ? inventory
                : null;
        }
    }
}