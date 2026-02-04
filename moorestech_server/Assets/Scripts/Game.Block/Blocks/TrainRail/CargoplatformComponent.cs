using Core.Item.Interface;
using Core.Master;
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
        private Guid? _dockedCarId;
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
            _armAnimationTicks = _param.LoadingSpeed;
        }

        public CargoplatformComponent(Dictionary<string, string> componentStates, TrainCargoPlatformBlockParam param) : this(param)
        {
            // TransferModeを復元する
            var serialized = componentStates[SaveKey];
            var saveData = JsonConvert.DeserializeObject<CargoplatformComponentSaverData>(serialized);
            if (saveData == null) return;
            _transferMode = saveData.transferMode;
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
            if (!_dockedTrainId.HasValue && !_dockedCarId.HasValue) return true;
            return _dockedTrainId == handle.TrainId && _dockedCarId == handle.CarId;
        }

        public void OnTrainDocked(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (_dockedTrainId.HasValue && _dockedTrainId != handle.TrainId) return;
            if (_dockedCarId.HasValue && _dockedCarId != handle.CarId) return;
            _dockedTrainId = handle.TrainId;
            _dockedCarId = handle.CarId;
            _dockedHandle = handle as TrainDockHandle;
            _shouldStartOnDock = true;
            UpdateDockedReferences(handle);
        }

        public void OnTrainDockedTick(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (_dockedTrainId != handle.TrainId || _dockedCarId != handle.CarId) return;
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
            var isDocked = _dockedTrainId.HasValue && _dockedCarId.HasValue;
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
                _armProgressTicks = Math.Min(1, _armAnimationTicks);
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
                return HasTransferCandidate(
                    _dockedStationInventory.GetSlotSize(),
                    _dockedStationInventory.GetItem,
                    _dockedTrainCar.GetSlotSize(),
                    _dockedTrainCar.GetItem);
            }

            bool CanUnloadToPlatform()
            {
                return HasTransferCandidate(
                    _dockedTrainCar.GetSlotSize(),
                    _dockedTrainCar.GetItem,
                    _dockedStationInventory.GetSlotSize(),
                    _dockedStationInventory.GetItem);
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
                TransferByDestinationPriority(
                    _dockedStationInventory.GetSlotSize(),
                    _dockedStationInventory.GetItem,
                    _dockedStationInventory.SetItem,
                    _dockedTrainCar.GetSlotSize(),
                    _dockedTrainCar.GetItem,
                    _dockedTrainCar.SetItem);
            }

            void TransferItemsToStationInventory()
            {
                TransferByDestinationPriority(
                    _dockedTrainCar.GetSlotSize(),
                    _dockedTrainCar.GetItem,
                    _dockedTrainCar.SetItem,
                    _dockedStationInventory.GetSlotSize(),
                    _dockedStationInventory.GetItem,
                    _dockedStationInventory.SetItem);
            }

            bool IsEmptyStack(IItemStack stack) => stack == null || stack.Id == ItemMaster.EmptyItemId || stack.Count == 0;

            bool HasTransferCandidate(int sourceSlotCount, Func<int, IItemStack> getSource, int destinationSlotCount, Func<int, IItemStack> getDestination)
            {
                for (var destinationSlot = 0; destinationSlot < destinationSlotCount; destinationSlot++)
                {
                    var destination = getDestination(destinationSlot);
                    if (destination == null) continue;

                    for (var sourceSlot = 0; sourceSlot < sourceSlotCount; sourceSlot++)
                    {
                        var source = getSource(sourceSlot);
                        if (IsEmptyStack(source)) continue;
                        if (destination.IsAllowedToAddWithRemain(source)) return true;
                    }
                }

                return false;
            }

            void TransferByDestinationPriority(int sourceSlotCount, Func<int, IItemStack> getSource, Action<int, IItemStack> setSource, int destinationSlotCount, Func<int, IItemStack> getDestination, Action<int, IItemStack> setDestination)
            {
                for (var destinationSlot = 0; destinationSlot < destinationSlotCount; destinationSlot++)
                {
                    var destination = getDestination(destinationSlot);
                    if (destination == null) continue;

                    for (var sourceSlot = 0; sourceSlot < sourceSlotCount; sourceSlot++)
                    {
                        var source = getSource(sourceSlot);
                        if (IsEmptyStack(source)) continue;
                        if (!destination.IsAllowedToAddWithRemain(source)) continue;

                        var processResult = destination.AddItem(source);
                        if (IsSameStack(destination, processResult.ProcessResultItemStack) && IsSameStack(source, processResult.RemainderItemStack)) continue;

                        setDestination(destinationSlot, processResult.ProcessResultItemStack);
                        setSource(sourceSlot, processResult.RemainderItemStack);
                        destination = processResult.ProcessResultItemStack;
                        if (IsDestinationFull(destination)) break;
                    }
                }
            }

            bool IsDestinationFull(IItemStack destinationStack)
            {
                if (IsEmptyStack(destinationStack)) return false;
                return destinationStack.Count >= MasterHolder.ItemMaster.GetItemMaster(destinationStack.Id).MaxStack;
            }

            #endregion
        }

        public void OnTrainUndocked(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (_dockedTrainId == handle.TrainId && _dockedCarId == handle.CarId)
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
            var saveData = new CargoplatformComponentSaverData("cargo", _transferMode);
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
            _dockedCarId = null;
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

            public CargoplatformComponentSaverData(string name, CargoTransferMode mode) : base(name)
            {
                transferMode = mode;
            }
        }
    }
}
