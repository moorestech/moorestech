using Game.Block.Interface.Component;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Block.Blocks.TrainRail
{
    /// <summary>
    /// 貨物駅用のコンポーネント。
    /// オープン可能なインベントリを持ち、かつ列車が到着・出発した状態も持つ。
    /// </summary>
    public class CargoplatformComponent : IBlockSaveState, ITrainDockingReceiver
    {
        private readonly int _stationLength;
        private readonly HashSet<Guid> _dockedTrainIds = new();

        // インベントリスロット数やUI更新のための設定
        public int InputSlotCount { get; private set; }
        public int OutputSlotCount { get; private set; }

        public bool IsDestroy { get; private set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public CargoplatformComponent(
            int stationLength,
            int inputSlotCount,
            int outputSlotCount
        )
        {
            _stationLength = stationLength;
            InputSlotCount = inputSlotCount;
            OutputSlotCount = outputSlotCount;
        }

        public void OnTrainDocked(ITrainDockHandle handle)
        {
            if (handle == null) return;
            _dockedTrainIds.Add(handle.TrainId);
        }

        public void OnTrainDockedTick(ITrainDockHandle handle)
        {
            // TODO: アイテム搬入をここで実装予定
        }

        public void OnTrainUndocked(Guid trainId)
        {
            _dockedTrainIds.Remove(trainId);
        }

        /// <summary>
        /// セーブ機能：ブロックが破壊されたりサーバーを落とすとき用
        /// </summary>
        public string SaveKey { get; } = typeof(CargoplatformComponent).FullName;

        public string GetSaveState()
        {
            var stationComponentSaverData = new StationComponent.StationComponentSaverData("cargo");
            /*foreach (var item in _itemDataStoreService.InventoryItems)
            {
                stationComponentSaverData.itemJson.Add(new ItemStackSaveJsonObject(item));
            }*/
            return JsonConvert.SerializeObject(stationComponentSaverData);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}