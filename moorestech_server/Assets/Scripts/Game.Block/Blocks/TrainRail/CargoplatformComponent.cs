using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.Common;
using Game.Train.Train;
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
    public class CargoplatformComponent : IBlockSaveState, ITrainDockingReceiver
    {
        // ブロックパラメータ参照を保持
        // Keep reference to generated block parameter
        private readonly TrainCargoPlatformBlockParam _param;
        private Guid? _dockedTrainId;
        private Guid? _dockedCarId;
        private TrainCar _dockedTrainCar;
        private IBlockInventory _dockedStationInventory;

        public enum CargoTransferMode
        {
            LoadToTrain,
            UnloadToPlatform
        }

        private CargoTransferMode _transferMode = CargoTransferMode.LoadToTrain;

        // インベントリスロット数やUI更新のための設定
        // プラットフォームのスロット数
        // Number of cargo platform slots
        public int SlotCount => _param.ItemSlotCount;

        public bool IsDestroy { get; private set; }

        public CargoTransferMode TransferMode => _transferMode;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public CargoplatformComponent(TrainCargoPlatformBlockParam param)
        {
            _param = param;
        }

        public CargoplatformComponent(Dictionary<string, string> componentStates, TrainCargoPlatformBlockParam param) : this(param)
        {
            // セーブデータが存在する場合は転送モードを復元する
            // Restore transfer mode when save data is available
            if (componentStates == null || !componentStates.TryGetValue(SaveKey, out var serialized) || string.IsNullOrEmpty(serialized))
            {
                return;
            }

            var saveData = JsonConvert.DeserializeObject<CargoplatformComponentSaverData>(serialized);
            if (saveData == null)
            {
                return;
            }

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
            UpdateDockedReferences(handle);
        }

        public void OnTrainDockedTick(ITrainDockHandle handle)
        {
            if (!IsValidDockingHandle(handle))
            {
                return;
            }

            switch (_transferMode)
            {
                case CargoTransferMode.LoadToTrain:
                    if (_dockedTrainCar.IsInventoryFull())
                    {
                        return;
                    }

                    TransferItemsToTrainCar();
                    break;

                case CargoTransferMode.UnloadToPlatform:
                    if (_dockedTrainCar.IsInventoryEmpty())
                    {
                        return;
                    }

                    TransferItemsToStationInventory();
                    break;
            }

            #region Internal

            bool IsValidDockingHandle(ITrainDockHandle currentHandle)
            {
                if (currentHandle == null)
                {
                    return false;
                }

                if (_dockedTrainId != currentHandle.TrainId || _dockedCarId != currentHandle.CarId)
                {
                    return false;
                }

                if (_dockedTrainCar == null || _dockedStationInventory == null)
                {
                    UpdateDockedReferences(currentHandle);

                    if (_dockedTrainCar == null || _dockedStationInventory == null)
                    {
                        return false;
                    }
                }

                return true;
            }

            void TransferItemsToTrainCar()
            {
                for (var slot = 0; slot < _dockedStationInventory.GetSlotSize(); slot++)
                {
                    var slotStack = _dockedStationInventory.GetItem(slot);
                    if (slotStack == null || slotStack.Id == ItemMaster.EmptyItemId || slotStack.Count == 0)
                    {
                        continue;
                    }

                    var remainder = _dockedTrainCar.InsertItem(slotStack);
                    if (IsSameStack(slotStack, remainder))
                    {
                        continue;
                    }

                    _dockedStationInventory.SetItem(slot, remainder);

                    if (_dockedTrainCar.IsInventoryFull())
                    {
                        break;
                    }
                }
            }

            void TransferItemsToStationInventory()
            {
                for (var slot = 0; slot < _dockedTrainCar.GetSlotSize(); slot++)
                {
                    var slotStack = _dockedTrainCar.GetItem(slot);
                    if (slotStack == null || slotStack.Id == ItemMaster.EmptyItemId || slotStack.Count == 0)
                    {
                        continue;
                    }

                    var remainder = _dockedStationInventory.InsertItem(slotStack);
                    if (IsSameStack(slotStack, remainder))
                    {
                        continue;
                    }

                    _dockedTrainCar.SetItem(slot, remainder);

                    if (_dockedTrainCar.IsInventoryEmpty())
                    {
                        break;
                    }
                }
            }

            #endregion
        }

        public void OnTrainUndocked(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (_dockedTrainId == handle.TrainId && _dockedCarId == handle.CarId)
            {
                ClearDockedReferences();
            }
        }

        public void ForceUndock()
        {
            ClearDockedReferences();
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

            _dockedTrainCar = dockHandle.TrainCar;
            _dockedStationInventory = ResolveStationInventory(_dockedTrainCar);
        }

        private void ClearDockedReferences()
        {
            _dockedTrainId = null;
            _dockedCarId = null;
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
