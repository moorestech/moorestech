using System;
using System.Collections.Generic;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;
using Game.Context;
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
        
        public BlockInventoryRequestProtocol(ServiceProvider serviceProvider)
        {
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestBlockInventoryRequestProtocolMessagePack>(payload.ToArray());
            
            //開けるインベントリを持つブロックが存在するかどうかをチェック
            var blockDatastore = ServerContext.WorldBlockDatastore;
            if (!blockDatastore.ExistsComponent<IOpenableBlockInventoryComponent>(data.Pos))
                return null;
            
            
            //存在したらアイテム数とアイテムIDをまとめてレスポンスする
            var itemIds = new List<int>();
            var itemCounts = new List<int>();
            
            foreach (var item in blockDatastore.GetBlock<IOpenableBlockInventoryComponent>(data.Pos).InventoryItems)
            {
                itemIds.Add(item.Id);
                itemCounts.Add(item.Count);
            }
            
            var blockId = blockDatastore.GetBlock(data.Pos).BlockId;
            
            return new BlockInventoryResponseProtocolMessagePack(blockId, itemIds.ToArray(), itemCounts.ToArray());
        }
        
        //データのレスポンスを実行するdelegateを設定する
        private delegate byte[] InventoryResponse(Vector3Int pos, IBlockConfigParam config);
    }
    
    
    [MessagePackObject]
    public class RequestBlockInventoryRequestProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestBlockInventoryRequestProtocolMessagePack()
        {
        }
        
        public RequestBlockInventoryRequestProtocolMessagePack(Vector3Int pos)
        {
            Tag = BlockInventoryRequestProtocol.Tag;
            Pos = new Vector3IntMessagePack(pos);
        }
        
        [Key(2)] public Vector3IntMessagePack Pos { get; set; }
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
        
        
        [Key(2)] public int BlockId { get; set; }
        
        [Key(3)] public int[] ItemIds { get; set; }
        
        [Key(4)] public int[] ItemCounts { get; set; }
    }
}