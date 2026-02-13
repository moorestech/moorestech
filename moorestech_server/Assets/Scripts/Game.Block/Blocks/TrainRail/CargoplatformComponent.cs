using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Interface.Component;
using Game.Train.Unit;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Game.Block.Blocks.TrainRail
{
    /// <summary>
    /// 貨物駅用のコンポーネント。
    /// オープン可能なインベントリを持ち、かつ列車が到着・出発した状態も持つ。
    /// </summary>
    public class CargoplatformComponent : IBlockSaveState, ITrainDockingReceiver, IUpdatableBlockComponent
    {
        // ブロックパラメータ参照を保持
        // Keep reference to generated block parameter
        private readonly TrainCargoPlatformBlockParam _param;
        private Guid? _dockedTrainId;
        private long? _dockedTrainCarInstanceId;
        private TrainCar _dockedTrainCar;
        private IBlockInventory _dockedStationInventory;
        private TrainDockHandle _dockedHandle;

        public enum CargoTransferMode
        {
            LoadToTrain,
            UnloadToPlatform
        }

        private CargoTransferMode _transferMode = CargoTransferMode.LoadToTrain;

        // アームアニメーションのtick設定
        // Arm animation tick settings
        private readonly int _armAnimationTicks;

        private enum ArmState
        {
            Idle,
            Extending,
            Retracting
        }

        private ArmState _armState = ArmState.Idle;
        private int _armProgressTicks;
        private bool _shouldStartOnDock;

        // インベントリスロット数やUI更新のための設定
        // プラットフォームのスロット数
        // Number of cargo platform slots
        public int SlotCount => _param.ItemSlotCount;

        public bool IsDestroy { get; private set; }

        public CargoTransferMode TransferMode => _transferMode;

        private void StartRetractingFromCurrent()
        {
            // 現在の進捗からリトラクト状態へ移行
            // Switch to retracting from the current arm progress
            _armState = ArmState.Retracting;
            _armProgressTicks = Math.Min(_armProgressTicks, _armAnimationTicks);
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public CargoplatformComponent(TrainCargoPlatformBlockParam param)
        {
            _param = param;
            var armAnimationTicks = GameUpdater.SecondsToTicks(_param.LoadingAnimeSpeed);
            _armAnimationTicks = armAnimationTicks > int.MaxValue ? int.MaxValue : (int)armAnimationTicks;
        }

        public CargoplatformComponent(Dictionary<string, string> componentStates, TrainCargoPlatformBlockParam param) : this(param)
        {
            // TransferModeを復元する
            var serialized = componentStates[SaveKey];
            var saveData = JsonConvert.DeserializeObject<CargoplatformComponentSaverData>(serialized);
            if (saveData == null) return;
            _transferMode = saveData.transferMode;
            _armState = (ArmState)saveData.armState;
            _armProgressTicks = Math.Min(Math.Max(0, saveData.armProgressTicks), _armAnimationTicks);
            _shouldStartOnDock = saveData.shouldStartOnDock;
        }

        public void SetTransferMode(CargoTransferMode mode)
        {
            _transferMode = mode;
        }

        public CargoTransferMode ToggleTransferMode()
        {
            _transferMode = _transferMode == CargoTransferMode.LoadToTrain
                ? CargoTransferMode.UnloadToPlatform
                : CargoTransferMode.LoadToTrain;
            return _transferMode;
        }

        public bool CanDock(ITrainDockHandle handle)
        {
            if (handle == null) return false;
            if (!_dockedTrainId.HasValue && !_dockedTrainCarInstanceId.HasValue) return true;
            return _dockedTrainId == handle.TrainId && _dockedTrainCarInstanceId == handle.TrainCarInstanceId;
        }

        public void OnTrainDocked(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (_dockedTrainId.HasValue && _dockedTrainId != handle.TrainId) return;
            if (_dockedTrainCarInstanceId.HasValue && _dockedTrainCarInstanceId != handle.TrainCarInstanceId) return;
            _dockedTrainId = handle.TrainId;
            _dockedTrainCarInstanceId = handle.TrainCarInstanceId;
            _dockedHandle = handle as TrainDockHandle;
            if (_armState == ArmState.Idle && _armProgressTicks == 0) _shouldStartOnDock = true;
            UpdateDockedReferences(handle);
        }

        public void OnTrainDockedTick(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (_dockedTrainId != handle.TrainId || _dockedTrainCarInstanceId != handle.TrainCarInstanceId) return;
            _dockedHandle = handle as TrainDockHandle;
            if (_dockedTrainCar != null && _dockedStationInventory != null) return;
            UpdateDockedReferences(handle);
        }

        public void Update()
        {
            // アームアニメーションと転送タイミングを更新
            // Advance arm animation and transfer timing per tick
            if (IsDestroy) return;

            // ドッキング状態に応じて参照を補完
            // Resolve docking references when needed
            var isDocked = _dockedTrainId.HasValue && _dockedTrainCarInstanceId.HasValue;
            if (isDocked && _dockedHandle != null && (_dockedTrainCar == null || _dockedStationInventory == null)) UpdateDockedReferences(_dockedHandle);

            // アーム状態を進めて転送タイミングを処理
            // Advance arm state and transfer timing
            switch (_armState)
            {
                case ArmState.Idle:
                    if (isDocked && (_shouldStartOnDock || CanTransferNow())) StartExtending();
                    _shouldStartOnDock = false;
                    break;

                case ArmState.Extending:
                    if (!isDocked)
                    {
                        StartRetractingFromCurrent();
                        break;
                    }

                    if (_armProgressTicks < _armAnimationTicks)
                    {
                        _armProgressTicks++;
                        break;
                    }

                    if (CanTransferNow()) ExecuteTransfer();
                    StartRetractingFromFull();
                    break;

                case ArmState.Retracting:
                    if (_armProgressTicks > 0)
                    {
                        _armProgressTicks--;
                        if (_armProgressTicks == 0) _armState = ArmState.Idle;
                        break;
                    }

                    _armState = ArmState.Idle;
                    break;
            }

            #region Internal

            void StartExtending()
            {
                _armState = ArmState.Extending;
                _armProgressTicks = 1;
            }

            void StartRetractingFromFull()
            {
                _armState = ArmState.Retracting;
                _armProgressTicks = _armAnimationTicks;
            }

            bool CanTransferNow()
            {
                if (!isDocked) return false;
                if (_dockedTrainCar == null || _dockedStationInventory == null) return false;
                return _transferMode == CargoTransferMode.LoadToTrain ? CanLoadToTrain() : CanUnloadToPlatform();
            }

            bool CanLoadToTrain()
            {
                return true;
            }

            bool CanUnloadToPlatform()
            {
                return true;
            }

            void ExecuteTransfer()
            {
                if (_transferMode == CargoTransferMode.LoadToTrain)
                {
                    TransferItemsToTrainCar();
                    return;
                }

                TransferItemsToStationInventory();
            }

            void TransferItemsToTrainCar()
            {
            }

            void TransferItemsToStationInventory()
            {
            }

            #endregion
        }

        public void OnTrainUndocked(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (_dockedTrainId == handle.TrainId && _dockedTrainCarInstanceId == handle.TrainCarInstanceId)
            {
                ClearDockedReferences();
                if (_armState == ArmState.Extending) StartRetractingFromCurrent();
            }
        }

        public void ForceUndock()
        {
            ClearDockedReferences();
            if (_armState == ArmState.Extending) StartRetractingFromCurrent();
        }

        /// <summary>
        /// セーブ機能：ブロックが破壊されたりサーバーを落とすとき用
        /// </summary>
        public string SaveKey { get; } = typeof(CargoplatformComponent).FullName;

        public string GetSaveState()
        {
            var saveData = new CargoplatformComponentSaverData("cargo", (int)_armState, _armProgressTicks, _shouldStartOnDock, _transferMode);
            /*foreach (var item in _itemDataStoreService.InventoryItems)
            {
                saveData.itemJson.Add(new ItemStackSaveJsonObject(item));
            }*/
            return JsonConvert.SerializeObject(saveData);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }

        private static bool IsSameStack(IItemStack left, IItemStack right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return left.Id == right.Id && left.Count == right.Count;
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
            _dockedTrainId = null;
            _dockedTrainCarInstanceId = null;
            _dockedHandle = null;
            _shouldStartOnDock = false;
            _dockedTrainCar = null;
            _dockedStationInventory = null;
        }

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

        [Serializable]
        private sealed class CargoplatformComponentSaverData : StationComponent.StationComponentSaverData
        {
            public CargoTransferMode transferMode;

            public CargoplatformComponentSaverData(string name, int state, int progressTicks, bool startOnDock, CargoTransferMode mode) : base(name, state, progressTicks, startOnDock)
            {
                transferMode = mode;
            }
        }
    }
}
