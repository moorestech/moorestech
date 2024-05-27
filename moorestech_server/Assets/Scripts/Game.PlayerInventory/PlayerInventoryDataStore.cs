using System.Collections.Generic;
using Core.Item.Interface;
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


        private readonly MainInventoryUpdateEvent _mainInventoryUpdateEvent;
        private readonly Dictionary<int, PlayerInventoryData> _playerInventoryData = new();

        public PlayerInventoryDataStore(IMainInventoryUpdateEvent mainInventoryUpdateEvent, IGrabInventoryUpdateEvent grabInventoryUpdateEvent)
        {
            //イベントの呼び出しをアセンブリに隠蔽するため、インターフェースをキャストします。
            _mainInventoryUpdateEvent = (MainInventoryUpdateEvent)mainInventoryUpdateEvent;
            _grabInventoryUpdateEvent = (GrabInventoryUpdateEvent)grabInventoryUpdateEvent;
        }

        public PlayerInventoryData GetInventoryData(int playerId)
        {
            if (!_playerInventoryData.ContainsKey(playerId))
            {
                var main = new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent);
                var grab = new GrabInventoryData(playerId, _grabInventoryUpdateEvent);

                _playerInventoryData.Add(playerId, new PlayerInventoryData(main, grab));
            }

            return _playerInventoryData[playerId];
        }

        public List<PlayerInventoryJsonObject> GetSaveJsonObject()
        {
            var savePlayerInventoryList = new List<PlayerInventoryJsonObject>();
            //セーブデータに必要なデータをまとめる
            foreach (KeyValuePair<int, PlayerInventoryData> inventory in _playerInventoryData)
            {
                var saveInventoryData = new PlayerInventoryJsonObject(inventory.Key, inventory.Value);
                savePlayerInventoryList.Add(saveInventoryData);
            }

            return savePlayerInventoryList;
        }

        /// <summary>
        ///     プレイヤーのデータを置き換える
        /// </summary>
        public void LoadPlayerInventory(List<PlayerInventoryJsonObject> saveInventoryDataList)
        {
            foreach (var saveInventory in saveInventoryDataList)
            {
                var playerId = saveInventory.PlayerId;
                (List<IItemStack> mainItems, var grabItem) = saveInventory.GetPlayerInventoryData();

                //アイテムを復元
                var main = new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent, mainItems);
                var grab = new GrabInventoryData(playerId, _grabInventoryUpdateEvent, grabItem);

                var playerInventory = new PlayerInventoryData(main, grab);

                //インベントリの追加を行う　既にあるなら置き換える
                _playerInventoryData[playerId] = playerInventory;
            }
        }
    }
}