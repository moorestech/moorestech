using System;
using System.Collections.Generic;
using industrialization.Core;
using industrialization.Core.Block.Machine.util;
using industrialization.OverallManagement.DataStore;
using industrialization.Server.Util;

namespace industrialization.Server.PacketResponse.ProtocolImplementation
{
    public static class PutBlockProtocol
    {
        public static List<byte[]> GetResponse(byte[] payload)
        {
            //パケットのパース、接続元、接続先のインスタンス取得
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort();
            uint blockId = payloadData.MoveNextToGetUint();
            payloadData.MoveNextToGetShort();
            int x = payloadData.MoveNextToGetInt();
            int y = payloadData.MoveNextToGetInt();
            
            var inputBlock = WorldBlockInventoryDatastore.GetBlock(payloadData.MoveNextToGetUint());

            var block = NormalMachineFactory.Create(blockId, IntId.NewIntId(), WorldBlockInventoryDatastore.GetBlock(payloadData.MoveNextToGetUint()));
            inputBlock.ChangeConnector(block);
            
            WorldBlockInventoryDatastore.AddBlock(block,block.IntId);
            WorldBlockDatastore.AddBlock(block, x, y);
            //返すものはない
            return new List<byte[]>();
        }
    }
}