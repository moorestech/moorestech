using System;
using System.Collections.Generic;
using Core.Update;
using Game.Block.Interface.Component;
using Game.Train.Unit;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.TrainRail
{
    public class TrainPlatformDockingComponent : IBlockComponent, ITrainDockingReceiver, IBlockSaveState, IUpdatableBlockComponent
    {
        public Guid? DockedTrainId;
        private long? _dockedTrainCarInstanceId;
        private TrainCar _dockedTrainCar;
        private IBlockInventory _dockedStationInventory;
        private TrainDockHandle _dockedHandle;
        
        public ArmState ArmState { get; private set; } = ArmState.Idle;
        private int _armProgressTicks;
        private readonly int _armAnimationTicks;
        private bool _shouldStartOnDock;
        
        public string SaveKey { get; } = typeof(TrainPlatformDockingComponent).FullName;
        public bool IsDestroy { get; private set; }
        
        public TrainPlatformDockingComponent(float loadingAnimationSpeed)
        {
            var armAnimationTicks = GameUpdater.SecondsToTicks(loadingAnimationSpeed);
            _armAnimationTicks = armAnimationTicks > int.MaxValue ? int.MaxValue : (int)armAnimationTicks;
        }
        
        public TrainPlatformDockingComponent(Dictionary<string, string> componentStates, float loadingAnimationSpeed) : this(loadingAnimationSpeed)
        {
            var serialized = componentStates[SaveKey];
            var saveData = JsonConvert.DeserializeObject<TrainPlatformDockingComponentSaveData>(serialized);
            if (saveData == null) return;
            
            ArmState = (ArmState)saveData.armState;
            _armProgressTicks = saveData.armProgressTicks;
            _shouldStartOnDock = saveData.shouldStartOnDock;
        }
        
        
        public string GetSaveState()
        {
            return JsonConvert.SerializeObject(new TrainPlatformDockingComponentSaveData(ArmState, _armProgressTicks, _shouldStartOnDock));
        }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public void Update()
        {
            if (IsDestroy) return;
            
            var isDocked = IsDocked();
            if (isDocked && _dockedHandle != null && (_dockedTrainCar == null || _dockedStationInventory == null)) UpdateDockedReferences(_dockedHandle);
            
            switch (ArmState)
            {
                case ArmState.Idle:
                    break;
                case ArmState.Extending:
                    if (!isDocked)
                    {
                        StartRetracting();
                        break;
                    }
                    
                    if (_armProgressTicks < _armAnimationTicks)
                    {
                        _armProgressTicks++;
                        break;
                    }
                    
                    ArmState = ArmState.Extended;
                    
                    break;
                case ArmState.Retracting:
                    if (_armProgressTicks > 0)
                    {
                        _armProgressTicks--;
                        if (_armProgressTicks == 0) ArmState = ArmState.Idle;
                        break;
                    }
                    
                    ArmState = ArmState.Idle;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public void StartExtending()
        {
            ArmState = ArmState.Extending;
            _armProgressTicks = Math.Max(_armProgressTicks, 1);
        }
        
        public void StartRetracting()
        {
            ArmState = ArmState.Retracting;
            _armProgressTicks = Math.Min(_armProgressTicks, _armAnimationTicks);
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
            if (ArmState == ArmState.Extending) StartRetracting();
        }
        
        public void OnTrainDocked(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (DockedTrainId.HasValue && DockedTrainId != handle.TrainId) return;
            if (_dockedTrainCarInstanceId.HasValue && _dockedTrainCarInstanceId != handle.TrainCarInstanceId) return;
            DockedTrainId = handle.TrainId;
            _dockedTrainCarInstanceId = handle.TrainCarInstanceId;
            _dockedHandle = handle as TrainDockHandle;
            if (ArmState == ArmState.Idle && _armProgressTicks == 0) _shouldStartOnDock = true;
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
                if (ArmState == ArmState.Extending) StartRetracting();
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
            _shouldStartOnDock = false;
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
        
        public bool IsDocked()
        {
            return DockedTrainId.HasValue && _dockedTrainCarInstanceId.HasValue;
        }
        
        [Serializable]
        public class TrainPlatformDockingComponentSaveData
        {
            public int armState;
            public int armProgressTicks;
            public bool shouldStartOnDock;
            
            public TrainPlatformDockingComponentSaveData(ArmState armState, int armProgressTicks, bool shouldStartOnDock)
            {
                this.armState = (int)armState;
                this.armProgressTicks = armProgressTicks;
                this.shouldStartOnDock = shouldStartOnDock;
            }
        }
    }
    
    public enum ArmState
    {
        Idle,
        Extending,
        Extended,
        Retracting,
    }
}