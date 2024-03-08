using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

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

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data =
                MessagePackSerializer.Deserialize<RequestBlockInventoryRequestProtocolMessagePack>(payload.ToArray());

            //開けるインベントリを持つブロックが存在するかどうかをチェック
            if (!_blockDatastore.ExistsComponentBlock<IOpenableInventory>(data.Pos))
                return null;


            //存在したらアイテム数とアイテムIDをまとめてレスポンスする
            var itemIds = new List<int>();
            var itemCounts = new List<int>();

            foreach (var item in _blockDatastore.GetBlock<IOpenableInventory>(data.Pos).Items)
            {
                itemIds.Add(item.Id);
                itemCounts.Add(item.Count);
            }

            var blockId = _blockDatastore.GetBlock(data.Pos).BlockId;

            return new BlockInventoryResponseProtocolMessagePack(blockId, itemIds.ToArray(), itemCounts.ToArray());
        }

        //データのレスポンスを実行するdelegateを設定する
        private delegate byte[] InventoryResponse(Vector2Int pos, IBlockConfigParam config);
    }


    [MessagePackObject]
    public class RequestBlockInventoryRequestProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestBlockInventoryRequestProtocolMessagePack()
        {
        }

        public RequestBlockInventoryRequestProtocolMessagePack(Vector2Int pos)
        {
            Tag = BlockInventoryRequestProtocol.Tag;
            Pos = new Vector2IntMessagePack(pos);
        }
        
        [Key(2)]
        public Vector2IntMessagePack Pos { get; set; }
    }

    [MessagePackObject]
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
        public BlockInventoryResponseProtocolMessagePack()
        {
        }


        [Key(2)]
        public int BlockId { get; set; }
        [Key(3)]
        public int[] ItemIds { get; set; }
        [Key(4)]
        public int[] ItemCounts { get; set; }
    }
}