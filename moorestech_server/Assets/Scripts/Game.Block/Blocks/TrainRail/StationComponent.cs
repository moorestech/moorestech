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
    /// 駅(TrainStation)用のコンポーネント。
    /// オープン可能なインベントリを持ち、かつ列車が到着・出発した状態も持つ。
    /// </summary>
    public class StationComponent : IBlockSaveState, ITrainDockingReceiver
    {
        public string StationName { get; }
        // ブロックパラメータ参照を保持
        // Keep reference to the generated train station parameter
        private readonly TrainStationBlockParam _param;
        public Guid? _dockedTrainId;
        private Guid? _dockedCarId;
        private TrainCar _dockedTrainCar;
        private IBlockInventory _dockedStationInventory;
        // インベントリスロット数やUI更新のための設定
        // マスター定義のスロット容量を公開
        // Expose inventory slot capacity defined in the block parameter
        public int InventorySlotCount => _param.ItemSlotCount;
        public bool IsDestroy { get; private set; }

        public StationComponent(string stationName, TrainStationBlockParam param)
        {
            StationName = stationName;
            _param = param;
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

            if (_dockedTrainCar.IsInventoryFull())
            {
                return;
            }

            TransferItemsToTrainCar();

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

            #endregion
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
        public string SaveKey { get; } = typeof(StationComponent).FullName;


        public string GetSaveState()
        {
            var stationComponentSaverData = new StationComponentSaverData(StationName);
            /*foreach (var item in _itemDataStoreService.InventoryItems)
            {
                stationComponentSaverData.itemJson.Add(new ItemStackSaveJsonObject(item));
            }*/
            return JsonConvert.SerializeObject(stationComponentSaverData);
        }

        [Serializable]
        public class StationComponentSaverData
        {
            public List<ItemStackSaveJsonObject> itemJson;
            public string stationName;
            public StationComponentSaverData(string name)
            {
                itemJson = new List<ItemStackSaveJsonObject>();
                stationName = name;
            }
        }




        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
