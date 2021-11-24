using System.Collections.Generic;
using PlayerInventory;
using Server.Util;

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
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort();
            var playerId = payloadData.MoveNextToGetInt();
            var playerInventory = _playerInventoryDataStore.GetInventoryData(playerId);
        }
    }
}