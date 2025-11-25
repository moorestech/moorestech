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

            // ブロック自体のアイテムを取得
            // Get the block item itself
            var blockItemStack = ServerContext.ItemStackFactory.Create(itemId, 1);

            // ブロックインベントリのアイテムを取得
            // Get items from block inventory
            var blockInventoryItems = new List<IItemStack>();
            if (ServerContext.WorldBlockDatastore.TryGetBlock<IBlockInventory>(data.Pos, out var blockInventory))
            {
                for (var i = 0; i < blockInventory.GetSlotSize(); i++)
                {
                    var item = blockInventory.GetItem(i);
                    if (item.Count > 0)
                    {
                        blockInventoryItems.Add(item);
                    }
                }
            }

            // その他の返却アイテムを取得
            // Get other refund items
            var otherRefundItems = new List<IItemStack>();
            if (block.ComponentManager.TryGetComponent(out IGetRefoundItemsInfo refundInfo))
            {
                foreach (var item in refundInfo.GetRefundItems())
                {
                    if (item.Count > 0)
                    {
                        otherRefundItems.Add(item);
                    }
                }
            }

            // すべてのアイテムをマージ
            // Merge all items
            var allRefundItems = new List<IItemStack>();
            allRefundItems.Add(blockItemStack);
            allRefundItems.AddRange(blockInventoryItems);
            allRefundItems.AddRange(otherRefundItems);

            var playerMainInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;

            // すべてのアイテムが挿入可能かチェック
            // Check if all items can be inserted
            if (playerMainInventory.InsertionCheck(allRefundItems))
            {
                // すべて挿入可能な場合、ブロックを削除してアイテムを挿入
                // If all items can be inserted, remove block and insert items
                ServerContext.WorldBlockDatastore.RemoveBlock(data.Pos, BlockRemoveReason.ManualRemove);
                playerMainInventory.InsertItem(allRefundItems);
            }
            else
            {
                // 一部しか挿入できない場合、ブロックインベントリアイテムのみ部分挿入を試みる
                // If only some items can be inserted, try partial insertion of block inventory items only

                // まず、ブロック自体のアイテムとその他のアイテムを挿入
                // First, insert block item and other refund items
                var mustInsertItems = new List<IItemStack>();
                mustInsertItems.Add(blockItemStack);
                mustInsertItems.AddRange(otherRefundItems);

                playerMainInventory.InsertItem(mustInsertItems);

                // ブロックインベントリのアイテムを可能な限り挿入
                // Insert block inventory items as much as possible
                var remainingBlockInventoryItems = playerMainInventory.InsertItem(blockInventoryItems);

                // ブロックインベントリをクリアして残りを設定
                // Clear block inventory and set remaining items
                if (blockInventory != null)
                {
                    for (var i = 0; i < blockInventory.GetSlotSize(); i++)
                    {
                        blockInventory.SetItem(i, ServerContext.ItemStackFactory.CreatEmpty());
                    }

                    var slotIndex = 0;
                    foreach (var item in remainingBlockInventoryItems)
                    {
                        if (slotIndex >= blockInventory.GetSlotSize()) break;
                        blockInventory.SetItem(slotIndex++, item);
                    }
                }
            }


            return null;
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
