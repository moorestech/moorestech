using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Chest;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Game.Train.Train;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using static Game.Block.Interface.BlockException;



namespace Game.Block.Blocks.TrainRail
{
    /// <summary>
    /// 駅(TrainStation)用のコンポーネント。
    /// オープン可能なインベントリを持ち、かつ列車が到着・出発した状態も持つ。
    /// </summary>
    public class StationComponent
        : IOpenableBlockInventoryComponent, // いわゆる「インベントリを開いて中身を見れる」ブロック用インターフェイス
          IBlockSaveState,
          IUpdatableBlockComponent
    {
        public BlockInstanceId BlockInstanceId { get; }
        public string StationName { get; }

        private readonly int _stationLength;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;

        // 列車関連
        private TrainUnit _currentTrain;


        // インベントリスロット数やUI更新のための設定
        public int InventorySlotCount { get; private set; }

        public bool IsDestroy { get; private set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public StationComponent(
            BlockInstanceId blockInstanceId,
            int stationLength,
            string stationName,
            int inventorySlotCount,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent
        )
        {
            BlockInstanceId = blockInstanceId;
            _stationLength = stationLength;
            StationName = stationName;
            InventorySlotCount = inventorySlotCount;

            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(
                OnInventoryUpdateInvoke,
                ServerContext.ItemStackFactory,
                inventorySlotCount
            );
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

        /// <summary>
        /// インベントリを外部に公開するためのIOpenableBlockInventoryComponent系実装
        /// ※ VanillaChestComponent などと似た構造にする
        /// </summary>
        public IReadOnlyList<IItemStack> InventoryItems => _itemDataStoreService.InventoryItems;

        public IItemStack InsertItem(IItemStack itemStack)
        {
            // ベルトコンベアなどが呼び出すメソッド
            return _itemDataStoreService.InsertItem(itemStack);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            return _itemDataStoreService.InsertionCheck(itemStacks);
        }

        public IItemStack GetItem(int slot)
        {
            return _itemDataStoreService.GetItem(slot);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItem(slot, itemStack);
        }

        public int GetSlotSize()
        {
            return _itemDataStoreService.GetSlotSize();
        }

        /// <summary>
        /// プレイヤーUIなどからの「置き換え要求」があったときのメソッド
        /// </summary>
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            var newStack = ServerContext.ItemStackFactory.Create(itemId, count);
            return _itemDataStoreService.ReplaceItem(slot, newStack);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            var newStack = ServerContext.ItemStackFactory.Create(itemId, count);
            return _itemDataStoreService.InsertItem(newStack);
        }

        /// <summary>
        /// インベントリのUIを開閉する際に使う
        /// </summary>
        public void OnInventoryOpen(int playerId)
        {
            // 何もしなくてもOK
        }

        public void OnInventoryClose(int playerId)
        {
            // 何もしなくてもOK
        }

        /// <summary>
        /// 更新通知。チェストや機械などと同様に、Slotが変化すると呼ばれる
        /// </summary>
        private void OnInventoryUpdateInvoke(int slotIndex, IItemStack updatedStack)
        {
            // イベントを通してUIに反映
            _blockInventoryUpdateEvent.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                this.BlockInstanceId,
                slotIndex,
                updatedStack
            ));
        }

        /// <summary>
        /// セーブ機能：ブロックが破壊されたりサーバーを落とすとき用
        /// </summary>
        public string SaveKey { get; } = typeof(StationComponent).FullName;

        public string GetSaveState()
        {
            CheckDestroy(this);
            var itemJson = new List<ItemStackSaveJsonObject>();
            foreach (var item in _itemDataStoreService.InventoryItems)
            {
                itemJson.Add(new ItemStackSaveJsonObject(item));
            }

            return JsonConvert.SerializeObject(itemJson);
        }


        /*
        /// <summary>
        /// ロード後にセットするための静的メソッド（VanillaTrainStationTemplate で使用想定）
        /// </summary>
        public void LoadFromJsonString(string json)
        {
            var obj = JsonConvert.DeserializeObject<StationSaveData>(json);
            // 名前や長さは基本固定なので省略可だが、一応書いておく
            // StationName = obj.StationName;
            // _stationLength = obj.StationLength;

            // アイテムのロード
            _itemDataStoreService.ImportJsonData(obj.InventoryData);
        }
        */
        /*
        private class StationSaveData
        {
            public string StationName;
            public int StationLength;
            public string InventoryData;
        }
        */




        public ReadOnlyCollection<IItemStack> CreateCopiedItems() { CheckDestroy(this); return _itemDataStoreService.CreateCopiedItems(); }
        public void SetItem(int slot, ItemId itemId, int count) { CheckDestroy(this); _itemDataStoreService.SetItem(slot, itemId, count); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { CheckDestroy(this); return _itemDataStoreService.ReplaceItem(slot, itemStack); }
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) { CheckDestroy(this); return _itemDataStoreService.InsertItem(itemStacks); }





        /// <summary>
        /// 毎フレーム処理（今回は特に列車到着処理などはないので空実装）
        /// </summary>
        public void Update()
        {
            // 必要なら列車の状態を毎フレームチェックなど
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
