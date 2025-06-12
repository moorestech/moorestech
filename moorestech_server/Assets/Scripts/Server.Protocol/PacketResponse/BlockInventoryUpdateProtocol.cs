using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.World.Interface.DataStore;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// ブロックインベントリの状態を取得するプロトコル
    /// リクエストで指定された位置のブロックインベントリデータを返す
    /// </summary>
    public class BlockInventoryUpdateProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:blockInvUpdate";
        
        public BlockInventoryUpdateProtocol()
        {
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<RequestBlockInventoryUpdateProtocolMessagePack>(payload.ToArray());
            
            // 開けるインベントリを持つブロックが存在するかどうかをチェック
            var blockDatastore = ServerContext.WorldBlockDatastore;
            if (!blockDatastore.ExistsComponent<IOpenableBlockInventoryComponent>(request.Pos))
            {
                return new BlockInventoryUpdateResponseProtocolMessagePack(new List<ItemMessagePack>());
            }
            
            // ブロックのインベントリを取得
            var blockOpenableInventory = blockDatastore.GetBlock<IOpenableBlockInventoryComponent>(request.Pos);
            
            // 各スロットの情報を収集
            var items = new List<ItemMessagePack>();
            var inventoryItems = blockOpenableInventory.InventoryItems;
            for (int i = 0; i < inventoryItems.Count; i++)
            {
                var item = inventoryItems[i];
                items.Add(new ItemMessagePack(item.Id, item.Count));
            }
            
            return new BlockInventoryUpdateResponseProtocolMessagePack(items);
        }
        
        [MessagePackObject]
        public class RequestBlockInventoryUpdateProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Pos { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestBlockInventoryUpdateProtocolMessagePack()
            {
            }
            
            public RequestBlockInventoryUpdateProtocolMessagePack(Vector3Int pos)
            {
                Tag = ProtocolTag;
                Pos = new Vector3IntMessagePack(pos);
            }
        }
        
        [MessagePackObject]
        public class BlockInventoryUpdateResponseProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public List<ItemMessagePack> Items { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public BlockInventoryUpdateResponseProtocolMessagePack()
            {
            }
            
            public BlockInventoryUpdateResponseProtocolMessagePack(List<ItemMessagePack> items)
            {
                Tag = ProtocolTag;
                Items = items;
            }
        }
    }
}