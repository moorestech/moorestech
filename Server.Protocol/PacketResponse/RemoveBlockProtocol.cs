using System.Collections.Generic;
using Core.Item;
using Core.Item.Config;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class RemoveBlockProtocol : IPacketResponse
    {
        
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private ItemStackFactory _itemStackFactory = new ItemStackFactory(new TestItemConfig());
        
        public  RemoveBlockProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();

        }
        
        playerInventoryData.InsertItem(_itemStackFactory.Create(blockConfigData.ItemId,1));
        
        
        
        public List<byte[]> GetResponse(List<byte> payload)
        {
            
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort();
            int x = payloadData.MoveNextToGetInt();
            int y = payloadData.MoveNextToGetInt();
            int playerId = payloadData.MoveNextToGetInt();

            _worldBlockDatastore.RemoveBlock(x, y);
            
            return new List<byte[]>();
            
            
        }

    }
}