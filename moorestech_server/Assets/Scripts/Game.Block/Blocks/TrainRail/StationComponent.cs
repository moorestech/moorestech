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
    /// 鬧・TrainStation)逕ｨ縺ｮ繧ｳ繝ｳ繝昴・繝阪Φ繝医・
    /// 繧ｪ繝ｼ繝励Φ蜿ｯ閭ｽ縺ｪ繧､繝ｳ繝吶Φ繝医Μ繧呈戟縺｡縲√°縺､蛻苓ｻ翫′蛻ｰ逹繝ｻ蜃ｺ逋ｺ縺励◆迥ｶ諷九ｂ謖√▽縲・
    /// </summary>
    public class StationComponent : IBlockSaveState, ITrainDockingReceiver
    {
        public string StationName { get; }

        private readonly int _stationLength;
        public Guid? _dockedTrainId;
        private Guid? _dockedCarId;
        // 蛻苓ｻ企未騾｣
        private TrainUnit _currentTrain;


        // 繧､繝ｳ繝吶Φ繝医Μ繧ｹ繝ｭ繝・ヨ謨ｰ繧ФI譖ｴ譁ｰ縺ｮ縺溘ａ縺ｮ險ｭ螳・
        public int InventorySlotCount { get; private set; }
        public bool IsDestroy { get; private set; }

        /// <summary>
        /// 繧ｳ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ
        /// </summary>
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
        /// 鬧・・蛻苓ｻ企未騾｣讖溯・
        /// </summary>
        public bool TrainArrived(TrainUnit train)
        {
            if (_currentTrain != null) return false; // 譌｢縺ｫ蛛懆ｻ贋ｸｭ縺ｪ繧丑G
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
        /// 繧ｻ繝ｼ繝匁ｩ溯・・壹ヶ繝ｭ繝・け縺檎ｴ螢翫＆繧後◆繧翫し繝ｼ繝舌・繧定誠縺ｨ縺吶→縺咲畑
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
