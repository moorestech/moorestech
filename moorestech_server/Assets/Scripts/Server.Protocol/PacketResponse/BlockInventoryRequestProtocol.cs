using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Context;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
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
            var blockId = blockDatastore.GetBlock(data.Pos).BlockId;
            
            return new BlockInventoryResponseProtocolMessagePack(blockId, blockDatastore.GetBlock<IOpenableBlockInventoryComponent>(data.Pos).InventoryItems);
        }
        
        //データのレスポンスを実行するdelegateを設定する
        private delegate byte[] InventoryResponse(Vector3Int pos, IBlockParam blockParam);
    }
    
    
    [MessagePackObject]
    public class RequestBlockInventoryRequestProtocolMessagePack : ProtocolMessagePackBase
    {
        [Key(2)] public Vector3IntMessagePack Pos { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestBlockInventoryRequestProtocolMessagePack()
        {
        }
        
        public RequestBlockInventoryRequestProtocolMessagePack(Vector3Int pos)
        {
            Tag = BlockInventoryRequestProtocol.Tag;
            Pos = new Vector3IntMessagePack(pos);
        }
    }
    
    [MessagePackObject]
    public class BlockInventoryResponseProtocolMessagePack : ProtocolMessagePackBase
    {
        [Key(2)] public int BlockId { get; set; }
     
        [Key(3)] public ItemMessagePack[] Items { get; set; }
        
        public BlockInventoryResponseProtocolMessagePack(BlockId blockId, IReadOnlyList<IItemStack> items)
        {
            Tag = BlockInventoryRequestProtocol.Tag;
            BlockId = (int)blockId;
            Items = items.Select(item => new ItemMessagePack(item)).ToArray();
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlockInventoryResponseProtocolMessagePack() { }
    }
}