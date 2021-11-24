using System.Collections.Generic;
using PlayerInventory;

namespace Server.PacketHandle.PacketResponse
{
    public class PlayerInventoryResponseProtocol : IPacketResponse
    {
        private PlayerInventoryDataStore _playerInventoryDataStore;

        public PlayerInventoryResponseProtocol(PlayerInventoryDataStore playerInventoryDataStore)
        {
            _playerInventoryDataStore = playerInventoryDataStore;
        }

        public List<byte[]> GetResponse(List<byte>  payload)
        {
            throw new System.NotImplementedException();
        }
    }
}