using System.Collections.Generic;
using Core.Item;
using PlayerInventory.Event;

namespace PlayerInventory
{
    /// <summary>
    /// プレイヤーインベントリのデータを扱います。
    /// 今はServerから直接参照されているけど、依存性の逆転をしたほうがいいかも...
    /// </summary>
    public class PlayerInventoryDataStore
    {
        readonly Dictionary<int,PlayerInventoryData> _playerInventoryData = new Dictionary<int,PlayerInventoryData>();
        private readonly PlayerInventoryUpdateEvent playerInventoryUpdateEvent;
        private readonly ItemStackFactory _itemStackFactory;

        public PlayerInventoryDataStore(PlayerInventoryUpdateEvent playerInventoryUpdateEvent)
        {
            this.playerInventoryUpdateEvent = playerInventoryUpdateEvent;
        }

        public PlayerInventoryData GetInventoryData(int playerId)
        {
            if (!_playerInventoryData.ContainsKey(playerId))
            {
                _playerInventoryData.Add(playerId, new PlayerInventoryData(playerId,playerInventoryUpdateEvent,_itemStackFactory));
            }

            return _playerInventoryData[playerId];
        }
    }
}