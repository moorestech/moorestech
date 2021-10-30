using System.Collections.Generic;
using Core.Block.Machine.util;
using Core.Util;
using Server.Util;
using World.DataStore;

namespace Server.PacketHandle.PacketResponse
{
    public class PutBlockProtocol : IPacketResponse
    {
        private readonly WorldBlockDatastore _worldBlockDatastore;

        public PutBlockProtocol(WorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }

        public List<byte[]> GetResponse(byte[] payload)
        {
            //パケットのパース、接続元、接続先のインスタンス取得
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort();
            int blockId = payloadData.MoveNextToGetInt();
            payloadData.MoveNextToGetShort();
            int x = payloadData.MoveNextToGetInt();
            int y = payloadData.MoveNextToGetInt();
            
            var inputBlock = WorldBlockInventoryDatastore.GetBlock(payloadData.MoveNextToGetInt());

            var block = NormalMachineFactory.Create(blockId, IntId.NewIntId(), WorldBlockInventoryDatastore.GetBlock(payloadData.MoveNextToGetInt()));
            inputBlock.ChangeConnector(block);
            
            WorldBlockInventoryDatastore.AddBlock(block,block.GetIntId());
            _worldBlockDatastore.AddBlock(block, x, y);
            //返すものはない
            return new List<byte[]>();
        }
    }
}