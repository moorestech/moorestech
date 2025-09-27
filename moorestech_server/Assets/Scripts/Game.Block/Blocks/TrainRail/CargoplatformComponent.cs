using Game.Block.Interface.Component;
using System;
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
        private Guid? _dockedTrainId;
        private Guid? _dockedCarId;

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
            // TODO: アイテム搬入をここで実装予定
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