using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.Common;
using Game.Train.Train;
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

        private readonly int _stationLength;
        public Guid? _dockedTrainId;
        private Guid? _dockedCarId;
        // 列車関連
        private TrainUnit _currentTrain;


        // インベントリスロット数やUI更新のための設定
        public int InventorySlotCount { get; private set; }
        public bool IsDestroy { get; private set; }

        public StationComponent(
            int stationLength,
            string stationName,
            int inventorySlotCount
        )
        {
            _stationLength = stationLength;
            StationName = stationName;
            InventorySlotCount = inventorySlotCount;
        }


        /// <summary>
        /// 駅の列車関連機能
        /// </summary>
        public bool TrainArrived(TrainUnit train)
        {
            if (_currentTrain != null) return false; // 既に停車中ならNG
            _currentTrain = train;
            return true;
        }
        public bool TrainDeparted(TrainUnit train)
        {
            if (_currentTrain == null) return false;
            _currentTrain = null;
            return true;
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
        }

        public void OnTrainDockedTick(ITrainDockHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            if (handle is not TrainDockHandle dockHandle)
            {
                return;
            }

            var trainCar = dockHandle.TrainCar;
            if (trainCar == null || trainCar.IsInventoryFull())
            {
                return;
            }

            var dockingBlock = trainCar.dockingblock;
            if (dockingBlock == null)
            {
                return;
            }

            if (!dockingBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var stationInventory))
            {
                return;
            }

            for (var slot = 0; slot < stationInventory.GetSlotSize(); slot++)
            {
                var slotStack = stationInventory.GetItem(slot);
                if (slotStack == null || slotStack.Id == ItemMaster.EmptyItemId || slotStack.Count == 0)
                {
                    continue;
                }

                var remainder = trainCar.InsertItem(slotStack);
                if (IsSameStack(slotStack, remainder))
                {
                    continue;
                }

                stationInventory.SetItem(slot, remainder);

                if (trainCar.IsInventoryFull())
                {
                    break;
                }
            }
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

        public void OnTrainUndocked(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (_dockedTrainId == handle.TrainId && _dockedCarId == handle.CarId)
            {
                _dockedTrainId = null;
                _dockedCarId = null;
            }
        }

        public void ForceUndock()
        {
            _dockedTrainId = null;
            _dockedCarId = null;
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
