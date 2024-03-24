using System.Collections.Generic;
using Core.Item;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Game.PlayerInventory.ItemManaged;

namespace Game.PlayerInventory
{
    /// <summary>
    ///     プレイヤーインベントリのデータを扱います。
    /// </summary>
    public class PlayerInventoryDataStore : IPlayerInventoryDataStore
    {
        private readonly GrabInventoryUpdateEvent _grabInventoryUpdateEvent;

        private readonly ItemStackFactory _itemStackFactory;
        private readonly MainInventoryUpdateEvent _mainInventoryUpdateEvent;
        private readonly Dictionary<int, PlayerInventoryData> _playerInventoryData = new();

        public PlayerInventoryDataStore(IMainInventoryUpdateEvent mainInventoryUpdateEvent, ItemStackFactory itemStackFactory, IGrabInventoryUpdateEvent grabInventoryUpdateEvent)
        {
            //イベントの呼び出しをアセンブリに隠蔽するため、インターフェースをキャストします。
            _mainInventoryUpdateEvent = (MainInventoryUpdateEvent)mainInventoryUpdateEvent;
            _grabInventoryUpdateEvent = (GrabInventoryUpdateEvent)grabInventoryUpdateEvent;

            _itemStackFactory = itemStackFactory;
        }

        public PlayerInventoryData GetInventoryData(int playerId)
        {
            if (!_playerInventoryData.ContainsKey(playerId))
            {
                var main = new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent, _itemStackFactory);
                var grab = new GrabInventoryData(playerId, _grabInventoryUpdateEvent, _itemStackFactory);

                _playerInventoryData.Add(playerId, new PlayerInventoryData(main, grab));
            }

            return _playerInventoryData[playerId];
        }

        public List<SaveInventoryData> GetSaveInventoryDataList()
        {
            var savePlayerInventoryList = new List<SaveInventoryData>();
            //セーブデータに必要なデータをまとめる
            foreach (KeyValuePair<int, PlayerInventoryData> inventory in _playerInventoryData)
            {
                var saveInventoryData = new SaveInventoryData(inventory.Key, inventory.Value);
                savePlayerInventoryList.Add(saveInventoryData);
            }

            return savePlayerInventoryList;
        }

        /// <summary>
        ///     プレイヤーのデータを置き換える
        /// </summary>
        public void LoadPlayerInventory(List<SaveInventoryData> saveInventoryDataList)
        {
            foreach (var saveInventory in saveInventoryDataList)
            {
                var playerId = saveInventory.PlayerId;
                (List<IItemStack> mainItems, List<IItemStack> craftItems, var grabItem) = saveInventory.GetPlayerInventoryData(_itemStackFactory);

                //アイテムを復元
                var main = new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent, _itemStackFactory,
                    mainItems);
                var grab = new GrabInventoryData(playerId, _grabInventoryUpdateEvent, _itemStackFactory, grabItem);

                var playerInventory = new PlayerInventoryData(main, grab);

                //インベントリの追加を行う　既にあるなら置き換える
                _playerInventoryData[playerId] = playerInventory;
            }
        }
    }
}