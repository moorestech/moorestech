using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Core.Block.Blocks;
using Core.Block.Blocks.Machine;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.ConfigParamGenerator;
using Core.Block.Config.LoadConfig.Param;
using Core.Inventory;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class BlockInventoryRequestProtocol : IPacketResponse
    {
        public const string Tag = "va:blockInvReq";
        
        private const int ProtocolId = 6;
        private IWorldBlockDatastore _blockDatastore;
        private IBlockConfig _blockConfig;

        //データのレスポンスを実行するdelegateを設定する
        private delegate byte[] InventoryResponse(int x, int y,IBlockConfigParam config);

        public BlockInventoryRequestProtocol(ServiceProvider serviceProvider)
        {
            _blockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            _blockConfig = serviceProvider.GetService<IBlockConfig>();
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var byteListEnumerator = new ByteListEnumerator(payload);
            byteListEnumerator.MoveNextToGetShort();
            var x = byteListEnumerator.MoveNextToGetInt();
            var y = byteListEnumerator.MoveNextToGetInt();

            var blockId = _blockDatastore.GetBlock(x, y).BlockId;
            var blockConfig = _blockConfig.GetBlockConfig(blockId);
            

            return new List<List<byte>>();
        }
    }
    
    
        
    [MessagePackObject(keyAsPropertyName :true)]
    public class BlockInventoryRequestProtocolMessagePack : ProtocolMessagePackBase
    {
        public int PlayerId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsOpen { get; set; }
    }
}