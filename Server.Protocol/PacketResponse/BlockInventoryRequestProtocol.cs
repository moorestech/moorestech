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
        private IWorldBlockDatastore _blockDatastore;

        //データのレスポンスを実行するdelegateを設定する
        private delegate byte[] InventoryResponse(int x, int y,IBlockConfigParam config);

        public BlockInventoryRequestProtocol(ServiceProvider serviceProvider)
        {
            serviceProvider.GetService<IWorldBlockDatastore>();
            _blockComponentDatastore = serviceProvider.GetService<IWorldBlockComponentDatastore<IOpenableInventory>>();
            _blockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            serviceProvider.GetService<IBlockConfig>();
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestBlockInventoryRequestProtocolMessagePack>(payload.ToArray());

            //開けるインベントリを持つブロックが存在するかどうかをチェック
            if (!_blockComponentDatastore.ExistsComponentBlock(data.X, data.Y)) return new List<List<byte>>();


            //存在したらアイテム数とアイテムIDをまとめてレスポンスする
            var itemIds = new List<int>();
            var itemCounts = new List<int>();

            foreach (var item in _blockComponentDatastore.GetBlock(data.X,data.Y).Items)
            {
                itemIds.Add(item.Id);
                itemCounts.Add(item.Count);
            }

            var blockId = _blockDatastore.GetBlock(data.X, data.Y).BlockId;

            var response = MessagePackSerializer.Serialize(new BlockInventoryResponseProtocolMessagePack(blockId,itemIds.ToArray(),itemCounts.ToArray())).ToList();

            return new List<List<byte>>(){response};
        }
    }
    
    
        
    [MessagePackObject(keyAsPropertyName :true)]
    public class RequestBlockInventoryRequestProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestBlockInventoryRequestProtocolMessagePack() { }

        public RequestBlockInventoryRequestProtocolMessagePack(int x, int y)
        {
            Tag = BlockInventoryRequestProtocol.Tag;
            X = x;
            Y = y;
        }

        public int X { get; set; }
        public int Y { get; set; }
    }
    [MessagePackObject(keyAsPropertyName :true)]
    public class BlockInventoryResponseProtocolMessagePack : ProtocolMessagePackBase
    {
        public BlockInventoryResponseProtocolMessagePack(int blockId, int[] itemIds, int[] itemCounts)
        {
            Tag = BlockInventoryRequestProtocol.Tag;
            BlockId = blockId;
            ItemIds = itemIds;
            ItemCounts = itemCounts;
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlockInventoryResponseProtocolMessagePack() { }


        public int BlockId { get; set; }
        public int[] ItemIds { get; set; }
        public int[] ItemCounts { get; set; }
    }
}