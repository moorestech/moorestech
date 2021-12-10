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
        readonly Dictionary<int,PlayerInventoryData> _playerInventoryData = new Dictionary<int,PlayerInventoryData>();
        private readonly PlayerInventoryUpdateEvent _playerInventoryUpdateEvent;
        private readonly ItemStackFactory _itemStackFactory;

        public PlayerInventoryDataStore(IPlayerInventoryUpdateEvent playerInventoryUpdateEvent, ItemStackFactory itemStackFactory)
        {
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
    }
}