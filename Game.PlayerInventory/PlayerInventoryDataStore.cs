using System.Collections.Generic;

namespace PlayerInventory
{
    public class PlayerInventoryDataStore
    {
        readonly Dictionary<int,PlayerInventoryData> _playerInventoryData = new Dictionary<int,PlayerInventoryData>();
        public PlayerInventoryData GetInventoryData(int playerId)
        {
            if (!_playerInventoryData.ContainsKey(playerId))
            {
                _playerInventoryData.Add(playerId, new PlayerInventoryData(playerId));
            }

            return _playerInventoryData[playerId];
        }
    }
}