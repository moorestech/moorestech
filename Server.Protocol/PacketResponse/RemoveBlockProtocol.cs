using System.Collections.Generic;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class RemoveBlockProtocol : IPacketResponse
    {
        
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        public  RemoveBlockProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();

        }
        
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