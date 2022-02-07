using System.Collections.Generic;
using System.ComponentModel;
using Core.Block.Blocks;
using Core.Block.Config;
using Game.World.Interface.DataStore;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    //TODO BlockInventoryRequestProtocolを作る
    public class BlockInventoryRequestProtocol : IPacketResponse
    {
        private const int ProtocolId = 6;
        private IWorldBlockDatastore _blockDatastore;
        private BlockConfig _blockConfig;

        public BlockInventoryRequestProtocol(IWorldBlockDatastore blockDatastore, BlockConfig blockConfig)
        {
            _blockDatastore = blockDatastore;
            _blockConfig = blockConfig;
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var enumerator = new ByteArrayEnumerator(payload);
            enumerator.MoveNextToGetShort();
            var x = enumerator.MoveNextToGetInt();
            var y = enumerator.MoveNextToGetInt();
            
            var blockType = _blockConfig.GetBlockConfig(_blockDatastore.GetBlock(x, y).GetBlockId()).Type;
            var response = new List<byte>();
            
            if (blockType == VanillaBlockType.Machine)
            {
                response.AddRange(ToByteList.Convert((short) 6));
                
            }

            return new List<byte[]> {response.ToArray()};
        }
    }
}