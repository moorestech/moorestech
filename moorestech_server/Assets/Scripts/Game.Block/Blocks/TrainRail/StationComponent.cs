using Core.Item.Interface;
using Game.Block.Interface;
using Game.Block.Interface.Component;
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
        private readonly HashSet<Guid> _dockedTrainIds = new();
        // 列車関連
        private TrainUnit _currentTrain;


        // インベントリスロット数やUI更新のための設定
        public int InventorySlotCount { get; private set; }
        public bool IsDestroy { get; private set; }


        /// <summary>
        /// コンストラクタ
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

        public void OnTrainDocked(ITrainDockHandle handle)
        {
            if (handle == null) return;
            _dockedTrainIds.Add(handle.TrainId);
        }

        public void OnTrainDockedTick(ITrainDockHandle handle)
        {
            // TODO: アイテム搬出をここで実装予定
        }

        public void OnTrainUndocked(Guid trainId)
        {
            _dockedTrainIds.Remove(trainId);
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
