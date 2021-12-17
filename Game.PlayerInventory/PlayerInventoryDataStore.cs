using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using PlayerInventory.Event;

namespace PlayerInventory
{
    /// <summary>
    /// プレイヤーインベントリのデータを扱います。
    /// </summary>
    public class PlayerInventoryDataStore : IPlayerInventoryDataStore
    {
        readonly Dictionary<int,PlayerInventoryData> _playerInventoryData = new();
        private readonly PlayerInventoryUpdateEvent _playerInventoryUpdateEvent;
        private readonly ItemStackFactory _itemStackFactory;

        public PlayerInventoryDataStore(IPlayerInventoryUpdateEvent playerInventoryUpdateEvent, ItemStackFactory itemStackFactory)
        {
            //イベントの呼び出しをアセンブリに隠蔽するため、インターフェースをキャストします。
            _playerInventoryUpdateEvent = (PlayerInventoryUpdateEvent) playerInventoryUpdateEvent;
            _itemStackFactory = itemStackFactory;
        }

        public IInventory GetInventoryData(int playerId)
        {
            if (!_playerInventoryData.ContainsKey(playerId))
            {
                _playerInventoryData.Add(playerId, new PlayerInventoryData(playerId,_playerInventoryUpdateEvent,_itemStackFactory));
            }

            return _playerInventoryData[playerId];
        }

        public List<SaveInventoryData> GetSaveInventoryDataList()
        {
            return new List<SaveInventoryData>();
        }
    }
}