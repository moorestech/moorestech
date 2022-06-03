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

        private IWorldBlockComponentDatastore<IOpenableInventory> _blockComponentDatastore;

        //データのレスポンスを実行するdelegateを設定する
        private delegate byte[] InventoryResponse(int x, int y,IBlockConfigParam config);

        public BlockInventoryRequestProtocol(ServiceProvider serviceProvider)
        {
            serviceProvider.GetService<IWorldBlockDatastore>();
            _blockComponentDatastore = serviceProvider.GetService<IWorldBlockComponentDatastore<IOpenableInventory>>();
            serviceProvider.GetService<IBlockConfig>();
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestBlockInventoryRequestProtocolMessagePack>(payload.ToArray());

            if (!_blockComponentDatastore.ExistsComponentBlock(data.X, data.Y)) return new List<List<byte>>();


            var itemIds = new List<int>();
            var itemCounts = new List<int>();

            foreach (var item in _blockComponentDatastore.GetBlock(data.X,data.Y).Items)
            {
                itemIds.Add(item.Id);
                itemCounts.Add(item.Count);
            }

            var response = MessagePackSerializer.Serialize(new BlockInventoryResponseProtocolMessagePack()
            {
                Tag = Tag,
                ItemIds = itemIds.ToArray(),
                ItemCounts = itemCounts.ToArray(),
            }).ToList();

            return new List<List<byte>>(){response};
        }
    }
    
    
        
    [MessagePackObject(keyAsPropertyName :true)]
    public class RequestBlockInventoryRequestProtocolMessagePack : ProtocolMessagePackBase
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
    [MessagePackObject(keyAsPropertyName :true)]
    public class BlockInventoryResponseProtocolMessagePack : ProtocolMessagePackBase
    {
        public int BlockId { get; set; }
        public int[] ItemIds { get; set; }
        public int[] ItemCounts { get; set; }
    }
}