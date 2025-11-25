using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
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
        public const string ProtocolTag = "va:removeBlock";
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        
        public RemoveBlockProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RemoveBlockProtocolMessagePack>(payload.ToArray());
            
            var block = ServerContext.WorldBlockDatastore.GetBlock(data.Pos);
            if (block == null) return null;
            var itemId = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId).ItemGuid;
                
            // 破壊した後のアイテムをインベントリに挿入できるかチェック
            // Check if items after destruction can be inserted into inventory
            if (TryInsertRefundItems(out var refundItems)) return null;
            
            // 削除処理
            // Deletion process
            ServerContext.WorldBlockDatastore.RemoveBlock(data.Pos, BlockRemoveReason.ManualRemove);
            InsertItemsToPlayerInventory(refundItems);
            
            
            return null;
            
            #region Internal
            
            bool TryInsertRefundItems(out List<IItemStack> items)
            {
                var playerMainInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
                items = GetRefundItems();
                
                return playerMainInventory.InsertionCheck(items);
            }
            
            
            List<IItemStack> GetRefundItems()
            {
                var result = new List<IItemStack>();
                
                // 破壊したブロック自体のアイテムを追加
                // Add the item of the destroyed block itself
                result.Add(ServerContext.ItemStackFactory.Create(itemId, 1));
                
                // インベントリのアイテムを取得
                // Get items from block inventory
                if (ServerContext.WorldBlockDatastore.TryGetBlock<IBlockInventory>(data.Pos, out var blockInventory))
                {
                    for (var i = 0; i < blockInventory.GetSlotSize(); i++)
                    {
                        result.Add(blockInventory.GetItem(i));
                    }
                }
                
                // その他の返却すべきアイテム情報を取得する
                // Get refundable item information before block removal
                if (block.ComponentManager.TryGetComponent(out IGetRefoundItemsInfo refundInfo))
                {
                    result.AddRange(refundInfo.GetRefundItems());
                }
                
                return result;
            }
            
            void InsertItemsToPlayerInventory(List<IItemStack> items)
            {
                var playerMainInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
                playerMainInventory.InsertItem(items);
            }
            
            #endregion
        }
        
        
        [MessagePackObject]
        public class RemoveBlockProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public Vector3IntMessagePack Pos { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RemoveBlockProtocolMessagePack() { }
            public RemoveBlockProtocolMessagePack(int playerId, Vector3Int pos)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                Pos = new Vector3IntMessagePack(pos);
            }
        }
    }
}
