using System.Collections.Generic;
using Core.Item;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class CraftProtocol : IPacketResponse
    {
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public CraftProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }
        
        public List<byte[]> GetResponse(List<byte> payload)
        {
            var response = new ByteArrayEnumerator(payload);
            response.MoveNextToGetShort(); // Packet ID
            var playerId = response.MoveNextToGetInt();

            var craftingInventory = _playerInventoryDataStore.GetInventoryData(playerId).CraftingOpenableInventory;
            
            //クラフトの実行
            craftingInventory.Craft();

            return new List<byte[]>();
        }
    }
}