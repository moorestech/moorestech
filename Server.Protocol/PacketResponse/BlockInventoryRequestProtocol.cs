using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class BlockInventoryRequestProtocol : IPacketResponse
    {
        public const string Tag = "va:blockInvReq";

        private readonly IWorldBlockDatastore _blockDatastore;

        public BlockInventoryRequestProtocol(ServiceProvider serviceProvider)
        {
            serviceProvider.GetService<IWorldBlockDatastore>();
            _blockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            serviceProvider.GetService<IBlockConfig>();
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestBlockInventoryRequestProtocolMessagePack>(payload.ToArray());

            
            if (!_blockDatastore.ExistsComponentBlock<IOpenableInventory>(data.X, data.Y)) return new List<List<byte>>();


            //ID
            var itemIds = new List<int>();
            var itemCounts = new List<int>();

            foreach (var item in _blockDatastore.GetBlock<IOpenableInventory>(data.X, data.Y).Items)
            {
                itemIds.Add(item.Id);
                itemCounts.Add(item.Count);
            }

            var blockId = _blockDatastore.GetBlock(data.X, data.Y).BlockId;

            var response = MessagePackSerializer.Serialize(new BlockInventoryResponseProtocolMessagePack(blockId, itemIds.ToArray(), itemCounts.ToArray())).ToList();

            return new List<List<byte>> { response };
        }

        //delegate
        private delegate byte[] InventoryResponse(int x, int y, IBlockConfigParam config);
    }


    [MessagePackObject(true)]
    public class RequestBlockInventoryRequestProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("。。")]
        public RequestBlockInventoryRequestProtocolMessagePack()
        {
        }

        public RequestBlockInventoryRequestProtocolMessagePack(int x, int y)
        {
            Tag = BlockInventoryRequestProtocol.Tag;
            X = x;
            Y = y;
        }

        public int X { get; set; }
        public int Y { get; set; }
    }

    [MessagePackObject(true)]
    public class BlockInventoryResponseProtocolMessagePack : ProtocolMessagePackBase
    {
        public BlockInventoryResponseProtocolMessagePack(int blockId, int[] itemIds, int[] itemCounts)
        {
            Tag = BlockInventoryRequestProtocol.Tag;
            BlockId = blockId;
            ItemIds = itemIds;
            ItemCounts = itemCounts;
        }

        [Obsolete("。。")]
        public BlockInventoryResponseProtocolMessagePack()
        {
        }


        public int BlockId { get; set; }
        public int[] ItemIds { get; set; }
        public int[] ItemCounts { get; set; }
    }
}