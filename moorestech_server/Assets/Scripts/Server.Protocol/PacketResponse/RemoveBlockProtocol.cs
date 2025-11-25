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

            // プレイヤーインベントリーの取得
            // Get player inventory
            var playerMainInventory =
                _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;

            var isNotRemainItem = true;

            // インベントリがある時、ブロック内のアイテムをプレイヤーインベントリに挿入
            // When block has inventory, insert items from block to player inventory
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            if (worldBlockDatastore.TryGetBlock<IBlockInventory>(data.Pos, out var blockInventory))
            {
                for (var i = 0; i < blockInventory.GetSlotSize(); i++)
                {
                    // プレイヤーインベントリにアイテムを挿入
                    // Insert item to player inventory
                    var remainItem = playerMainInventory.InsertItem(blockInventory.GetItem(i));

                    // 余ったアイテムをブロックに戻す
                    // Return remaining items to block
                    blockInventory.SetItem(i, remainItem);

                    // アイテムが入りきらなかったらブロックを削除しないフラグを立てる
                    // Set flag to not delete block if items don't fit
                    var emptyItem = ServerContext.ItemStackFactory.CreatEmpty();
                    if (!remainItem.Equals(emptyItem)) isNotRemainItem = false;
                }
            }

            // 壊したブロックをインベントリーに挿入
            // Insert destroyed block to inventory
            var block = worldBlockDatastore.GetBlock(data.Pos);
            if (block == null) return null;

            // ギアチェーンなどの返却すべきアイテム情報を取得して挿入
            // Get and insert refundable items such as gear chain items
            if (block.ComponentManager.TryGetComponent(out IGetRefoundItemsInfo refundInfo))
            {
                foreach (var item in refundInfo.GetRefundItems())
                {
                    if (item.Count > 0)
                    {
                        var remainItem = playerMainInventory.InsertItem(item);
                        // 入りきらなかったらブロック削除しない
                        // Don't delete block if items don't fit
                        if (remainItem.Count > 0) isNotRemainItem = false;
                    }
                }
            }

            // ブロックのIDを取得してアイテムを挿入
            // Get block ID and insert item
            var blockItemId = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId).ItemGuid;
            var remainBlockItem = playerMainInventory.InsertItem(ServerContext.ItemStackFactory.Create(blockItemId, 1));

            // ブロック内のアイテムを全てインベントリに入れ、ブロックもインベントリに入れれた時だけブロックを削除する
            // Delete block only when all items from block and the block itself fit in inventory
            if (isNotRemainItem && remainBlockItem.Equals(ServerContext.ItemStackFactory.CreatEmpty()))
            {
                worldBlockDatastore.RemoveBlock(data.Pos, BlockRemoveReason.ManualRemove);
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
