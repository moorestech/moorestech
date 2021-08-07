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
        public static byte[][] GetResponse(byte[] payload)
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
            
            WorldBlockInventoryDatastore.AddBlock(block,block.IntId);
            WorldBlockDatastore.AddBlock(block, x, y);
            //返すものはない
            return new byte[0][];
        }
    }
}