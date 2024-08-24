using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RemoveBlockProtocol : IPacketResponse
    {
        public const string Tag = "va:removeBlock";
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        
        public RemoveBlockProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RemoveBlockProtocolMessagePack>(payload.ToArray());
            
            
            //プレイヤーインベントリーの取得
            var playerMainInventory =
                _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
            
            var isNotRemainItem = true;
            
            //インベントリがある時は
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            if (worldBlockDatastore.TryGetBlock<IBlockInventory>(data.Pos, out var blockInventory))
                //プレイヤーインベントリにブロック内のアイテムを挿入
                for (var i = 0; i < blockInventory.GetSlotSize(); i++)
                {
                    //プレイヤーインベントリにアイテムを挿入
                    var remainItem = playerMainInventory.InsertItem(blockInventory.GetItem(i));
                    //余ったアイテムをブロックに戻す
                    //この時、もしプレイヤーインベントリにアイテムを入れれたのなら、空のアイテムをブロックに戻すようになっているs
                    blockInventory.SetItem(i, remainItem);
                    
                    //アイテムが入りきらなかったらブロックを削除しないフラグを立てる
                    var emptyItem = ServerContext.ItemStackFactory.CreatEmpty();
                    if (!remainItem.Equals(emptyItem)) isNotRemainItem = false;
                }
            
            
            //インベントリに削除するブロックを入れる
            
            //壊したブロックをインベントリーに挿入
            //ブロックIdの取得
            var block = worldBlockDatastore.GetBlock(data.Pos);
            if (block == null) return null;
            
            //ブロックのIDを取得
            var blockItemId = BlockMaster.GetBlockMaster(block.BlockId).ItemGuid;
            //アイテムを挿入
            var remainBlockItem = playerMainInventory.InsertItem(ServerContext.ItemStackFactory.Create(blockItemId, 1));
            
            
            //ブロック内のアイテムを全てインベントリに入れ、ブロックもインベントリに入れれた時だけブロックを削除する
            if (isNotRemainItem && remainBlockItem.Equals(ServerContext.ItemStackFactory.CreatEmpty()))
                worldBlockDatastore.RemoveBlock(data.Pos);
            
            return null;
        }
    }
    
    
    [MessagePackObject]
    public class RemoveBlockProtocolMessagePack : ProtocolMessagePackBase
    {
        public RemoveBlockProtocolMessagePack(int playerId, Vector3Int pos)
        {
            Tag = RemoveBlockProtocol.Tag;
            PlayerId = playerId;
            Pos = new Vector3IntMessagePack(pos);
        }
        
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RemoveBlockProtocolMessagePack()
        {
        }
        
        [Key(2)] public int PlayerId { get; set; }
        
        [Key(3)] public Vector3IntMessagePack Pos { get; set; }
    }
}